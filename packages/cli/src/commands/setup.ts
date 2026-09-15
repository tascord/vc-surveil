import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import { spawnSync } from "node:child_process";
import { fail, log, warn, printBanner, printInfoBox, fmtPath } from "../util.js";

export const DEFAULT_BEPINEX_URL =
  "https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip";

function findGameDir(explicit?: string): string {
  if (explicit && explicit.trim()) return path.resolve(explicit);
  const env = process.env.VC_GAME_DIR || process.env.SURVEIL_GAME_DIR;
  if (env) return path.resolve(env);
  return path.join(os.homedir(), ".local/share/Steam/steamapps/common/Vampire Crawlers");
}

function isLibCpp2ILOutdated(gameDir: string): boolean {
  const libCpp = path.join(gameDir, "BepInEx/core/LibCpp2IL.dll");
  if (fs.existsSync(libCpp)) {
    try {
      const stat = fs.statSync(libCpp);
      // Older BE builds (< be.764) only supported metadata 23-29 and are ~274KB.
      // Build 788+ is ~312KB and supports metadata up to 106.
      if (stat.size < 300_000) {
        return true;
      }
      return false; // LibCpp2IL is build 788+
    } catch {}
  }

  const logFile = path.join(gameDir, "BepInEx/LogOutput.log");
  if (fs.existsSync(logFile)) {
    try {
      const content = fs.readFileSync(logFile, "utf8");
      if (content.includes("Unsupported metadata version found")) {
        return true;
      }
    } catch {}
  }
  return false;
}

function findRepoRoot(): string | null {
  const cand = [
    process.env.DEVENV_ROOT,
    process.cwd(),
    path.resolve(process.cwd(), ".."),
    path.resolve(process.cwd(), "../.."),
  ].filter(Boolean) as string[];

  for (const c of cand) {
    if (fs.existsSync(path.join(c, "packages/loader/PancakeModLoader.csproj"))) {
      return c;
    }
  }
  return null;
}

export async function cmdSetup(opts: { gameDir?: string; yes?: boolean; force?: boolean; dryRun?: boolean }) {
  const gameDir = findGameDir(opts.gameDir);
  printBanner();
  printInfoBox([["Game dir", fmtPath(gameDir)]]);
  if (!fs.existsSync(gameDir)) {
    warn(`Game dir not found: ${gameDir} — set --game-dir / $VC_GAME_DIR`);
  }

  const hasBep =
    fs.existsSync(path.join(gameDir, "BepInEx/core/BepInEx.Core.dll")) ||
    fs.existsSync(path.join(gameDir, "doorstop_config.ini"));
  const outdated = hasBep && isLibCpp2ILOutdated(gameDir);

  if (hasBep && !outdated && !opts.force) {
    log(`BepInEx present and compatible in ${gameDir} (Unity 6 / metadata v31 ready)`);
  } else {
    if (outdated) {
      warn(
        `Installed BepInEx lacks Unity 6 / metadata v31 support (causes 'Unsupported metadata version found! We support 23-29, got 31').`
      );
      log(`Upgrading to BepInEx 6 Bleeding Edge (#788)...`);
    } else if (opts.force) {
      log(`--force specified: re-installing BepInEx...`);
    } else {
      log(`BepInEx not found in ${gameDir}. Installing BepInEx 6 Bleeding Edge (#788)...`);
    }

    let url = process.env.BEPINEX_URL || DEFAULT_BEPINEX_URL;
    if (url.includes("v6.0.0-pre.2") || url.includes("releases/download/v6.0.0-pre")) {
      url = DEFAULT_BEPINEX_URL;
    }

    if (opts.dryRun) {
      log(`[dry-run] would download ${url}`);
      log(`[dry-run] would extract to ${gameDir}`);
      log(`[dry-run] would wipe ${path.join(gameDir, "BepInEx/interop")}`);
      log(`[dry-run] would install PancakeModLoader.dll and sync ~/.vc-mods`);
      return;
    }

    const tmpZip = path.join(os.tmpdir(), `bepinex-${Date.now()}.zip`);
    log(`Downloading BepInEx 6 BE (#788) → ${tmpZip}`);
    const curl = spawnSync("curl", ["-L", "--fail", "-o", tmpZip, url], { stdio: "inherit" });
    if (curl.status !== 0) {
      fail(`Failed to download BepInEx from ${url}`);
      return;
    }

    // Wipe interop cache to ensure fresh generation for metadata v31
    const interopDir = path.join(gameDir, "BepInEx/interop");
    if (fs.existsSync(interopDir)) {
      try {
        fs.rmSync(interopDir, { recursive: true, force: true });
        log(`Cleaned stale interop cache: ${interopDir}`);
      } catch (err: any) {
        warn(`Could not clear ${interopDir}: ${err?.message}`);
      }
    }

    // Remove stale logs from failed runs
    for (const lf of ["BepInEx/LogOutput.log", "BepInEx/ErrorLog.log"]) {
      const p = path.join(gameDir, lf);
      if (fs.existsSync(p)) {
        try { fs.unlinkSync(p); } catch {}
      }
    }

    // Ensure existing files are writable before unzipping over them
    if (process.platform === "linux") {
      spawnSync("chmod", ["-R", "u+rwX", path.join(gameDir, "BepInEx"), path.join(gameDir, "dotnet")]);
      spawnSync("chmod", ["u+rw", path.join(gameDir, "doorstop_config.ini"), path.join(gameDir, "winhttp.dll")]);
    }

    log(`Extracting BepInEx to ${gameDir}...`);
    const unzip = spawnSync("unzip", ["-o", "-q", tmpZip, "-d", gameDir], { stdio: "inherit" });
    try {
      fs.unlinkSync(tmpZip);
    } catch {}

    if (unzip.status !== 0) {
      fail(`Failed to extract BepInEx zip`);
      return;
    }

    // Fix permissions on Linux for newly unpacked files
    if (process.platform === "linux") {
      spawnSync("chmod", ["-R", "u+rwX", path.join(gameDir, "BepInEx"), path.join(gameDir, "dotnet")]);
      spawnSync("chmod", ["u+rw", path.join(gameDir, "doorstop_config.ini"), path.join(gameDir, "winhttp.dll")]);
    }

    log(`BepInEx 6 BE (#788) installed successfully.`);
  }

  // Next: ensure loader is built & copied
  const repoRoot = findRepoRoot();
  if (repoRoot) {
    const csproj = path.join(repoRoot, "packages/loader/PancakeModLoader.csproj");
    const loaderDll = path.join(repoRoot, "packages/loader/bin/Release/net6.0/PancakeModLoader.dll");
    const pluginsDir = path.join(gameDir, "BepInEx/plugins");
    const targetDll = path.join(pluginsDir, "PancakeModLoader.dll");

    const dotnetCheck = spawnSync("dotnet", ["--version"], { stdio: "ignore" });
    if (dotnetCheck.status === 0) {
      log(`Building PancakeModLoader.dll...`);
      const build = spawnSync("dotnet", ["build", csproj, "-c", "Release"], { stdio: "inherit" });
      const releaseDir = path.dirname(loaderDll);
      if (build.status === 0 && fs.existsSync(releaseDir)) {
        fs.mkdirSync(pluginsDir, { recursive: true });
        for (const file of fs.readdirSync(releaseDir)) {
          if (file.endsWith(".dll") || file.endsWith(".pdb")) {
            fs.copyFileSync(path.join(releaseDir, file), path.join(pluginsDir, file));
          }
        }
        log(`PancakeModLoader & dependencies installed -> ${pluginsDir}`);
      } else {
        warn(`Failed to build PancakeModLoader.dll`);
      }
    } else if (fs.existsSync(loaderDll)) {
      const releaseDir = path.dirname(loaderDll);
      fs.mkdirSync(pluginsDir, { recursive: true });
      for (const file of fs.readdirSync(releaseDir)) {
        if (file.endsWith(".dll") || file.endsWith(".pdb")) {
          fs.copyFileSync(path.join(releaseDir, file), path.join(pluginsDir, file));
        }
      }
      log(`Copied existing PancakeModLoader & dependencies -> ${pluginsDir}`);
    }
  }

  // Ensure ~/.vc-mods and game/Mods are synced
  const homeMods = path.join(os.homedir(), ".vc-mods");
  fs.mkdirSync(homeMods, { recursive: true });
  const gameMods = path.join(gameDir, "Mods");
  fs.mkdirSync(gameMods, { recursive: true });

  if (repoRoot) {
    const demoSrc = path.join(repoRoot, "mods/demo-mod");
    const demoDest = path.join(homeMods, "demo-mod");
    if (fs.existsSync(demoSrc) && !fs.existsSync(demoDest)) {
      try {
        fs.cpSync(demoSrc, demoDest, { recursive: true });
        log(`Seeded ~/.vc-mods/demo-mod`);
      } catch {}
    }
  }

  // Sync to game/Mods for Wine/Proton
  try {
    const entries = fs.readdirSync(homeMods, { withFileTypes: true });
    for (const ent of entries) {
      if (!ent.isDirectory()) continue;
      const target = path.join(gameMods, ent.name);
      if (!fs.existsSync(target)) {
        try {
          fs.symlinkSync(path.join(homeMods, ent.name), target);
        } catch {
          fs.cpSync(path.join(homeMods, ent.name), target, { recursive: true });
        }
      }
    }
  } catch {}

  log("");
  printInfoBox([
    ["Status", "Setup complete"],
    ["Next", "surveil launch"],
  ]);
}

