import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import { spawn, spawnSync } from "node:child_process";
import { fail, log, resolveModsDir, warn, printBanner, printInfoBox, fmtPath } from "../util.js";
import { findGamePids, analyzeCrash, renderCrashReport } from "../crashAnalyzer.js";
import { formatLogLine, LogLineBuffer } from "../logFormatter.js";

const APP_ID = "3265700";
const GAME_EXE = "Vampire Crawlers.exe";

function findGameDir(explicit: string): string {
  if (explicit && explicit.trim()) return path.resolve(explicit);
  const env = process.env.VC_GAME_DIR || process.env.SURVEIL_GAME_DIR;
  if (env) return path.resolve(env);
  const steam = path.join(os.homedir(), ".local/share/Steam/steamapps/common/Vampire Crawlers");
  if (fs.existsSync(steam)) return steam;
  return path.resolve(process.cwd());
}

function hasBepInEx(gameDir: string): boolean {
  return (
    fs.existsSync(path.join(gameDir, "BepInEx")) ||
    fs.existsSync(path.join(gameDir, "doorstop_config.ini")) ||
    fs.existsSync(path.join(gameDir, "winhttp.dll")) ||
    fs.existsSync(path.join(gameDir, "BepInExPack"))
  );
}

function steamAvailable(): boolean {
  const r = spawnSync("steam", ["--version"], { stdio: "ignore" });
  if (r.status === 0) return true;
  const r2 = spawnSync("which", ["steam"], { stdio: "ignore" });
  return r2.status === 0;
}

function ensureSteamLaunchOptions(): void {
  const desired = "WINEDLLOVERRIDES=winhttp=n,b %command%";
  const base = path.join(os.homedir(), ".local/share/Steam/userdata");
  let files: string[] = [];
  try {
    const users = fs.readdirSync(base, { withFileTypes: true }).filter((d: any) => d.isDirectory()).map((d: any) => path.join(base, d.name, "config/localconfig.vdf"));
    files = users.filter((p) => fs.existsSync(p));
  } catch {}
  if (files.length === 0) files = [path.join(os.homedir(), ".local/share/Steam/userdata/219157431/config/localconfig.vdf")].filter((p) => fs.existsSync(p));
  for (const file of files) {
    try {
      let txt = fs.readFileSync(file, "utf8");
      // Find "apps" block first — LaunchOptions for 3265700 must be inside apps, not other sections
      const appsIdx = txt.indexOf('"apps"');
      if (appsIdx === -1) continue;
      const appsBrace = txt.indexOf("{", appsIdx);
      if (appsBrace === -1) continue;
      let depth = 0, appsEnd = -1;
      for (let i = appsBrace; i < txt.length; i++) {
        if (txt[i] === "{") depth++;
        else if (txt[i] === "}") { depth--; if (depth === 0) { appsEnd = i; break; } }
      }
      if (appsEnd === -1) continue;
      const appsBlock = txt.slice(appsBrace, appsEnd);
      const appTag = `"${APP_ID}"`;
      const localIdx = appsBlock.indexOf(appTag);
      if (localIdx === -1) continue; // 3265700 not in apps (should not happen)
      const brace = appsBlock.indexOf("{", localIdx);
      if (brace === -1) continue;
      let d2 = 0, end2 = -1;
      for (let i = brace; i < appsBlock.length; i++) {
        if (appsBlock[i] === "{") d2++;
        else if (appsBlock[i] === "}") { d2--; if (d2 === 0) { end2 = i; break; } }
      }
      if (end2 === -1) continue;
      const block = appsBlock.slice(brace, end2);
      if (block.includes("WINEDLLOVERRIDES=winhttp=n,b")) continue; // already correct
      if (!block.includes("LaunchOptions")) {
        // Insert before closing } of the 3265700 block
        const globalBrace = appsBrace + brace;
        const globalEnd = appsBrace + end2;
        const insert = `\n\t\t\t\t\t"LaunchOptions"\t\t"${desired}"`;
        txt = txt.slice(0, globalEnd) + insert + txt.slice(globalEnd);
        fs.writeFileSync(file, txt, "utf8");
        log(`Auto-set Steam launch options for ${APP_ID} in ${file} → ${desired} (restart Steam if game was running)`);
      } else if (block.includes("LaunchOptions") && !block.includes("winhttp")) {
        const loRegex = /"LaunchOptions"\s+"(?:\\.|[^"\\])*"/;
        const m = block.match(loRegex);
        if (m && m.index !== undefined) {
          const globalLoPos = appsBrace + brace + m.index;
          const globalLoEnd = globalLoPos + m[0].length;
          txt = txt.slice(0, globalLoPos) + `"LaunchOptions"\t\t"${desired}"` + txt.slice(globalLoEnd);
          fs.writeFileSync(file, txt, "utf8");
          log(`Updated Steam launch options for ${APP_ID} in ${file}`);
        }
      }
    } catch (e: any) {
      warn(`Could not patch ${file}: ${e?.message ?? e}`);
    }
  }
}

// Sync ~/.vc-mods -> <game>/Mods for Proton where env vars don't cross the Wine boundary.
// Uses symlink per-mod (cheap) with copy fallback if symlink fails (e.g. different FS).
function syncModsToGame(modsDir: string, gameDir: string): void {
  if (path.resolve(modsDir) === path.resolve(path.join(gameDir, "Mods"))) return;
  if (!fs.existsSync(modsDir)) return;
  const destRoot = path.join(gameDir, "Mods");
  try {
    fs.mkdirSync(destRoot, { recursive: true });
  } catch {}
  // Mirror: ensure every mod in ~/.vc-mods has a symlink/copy in game/Mods
  let entries: fs.Dirent[] = [];
  try {
    entries = fs.readdirSync(modsDir, { withFileTypes: true }).filter((d: any) => d.isDirectory());
  } catch { return; }
  for (const e of entries) {
    const src = path.join(modsDir, e.name);
    const dest = path.join(destRoot, e.name);
    try {
      const st = fs.lstatSync(dest);
      if (st.isSymbolicLink() && fs.readlinkSync(dest) === src) continue;
      if (st.isDirectory() || st.isSymbolicLink()) fs.rmSync(dest, { recursive: true, force: true });
    } catch {}
    if (fs.existsSync(dest)) continue;
    try {
      fs.symlinkSync(src, dest, "dir");
      log(`Linked ${e.name} → game/Mods/${e.name}`);
    } catch {
      try {
        fs.cpSync(src, dest, { recursive: true, force: true });
        log(`Copied ${e.name} → game/Mods/${e.name} (symlink failed)`);
      } catch (err: any) {
        warn(`Could not sync ${e.name} to game Mods: ${err?.message ?? err}`);
      }
    }
  }
}

function candidateLogFiles(gameDir: string): string[] {
  const home = os.homedir();
  return [
    path.join(home, ".vc-mods/last-crash.log"),
    path.join(gameDir, "BepInEx/LogOutput.log"),
    path.join(gameDir, "LogOutput.log"),
    path.join(gameDir, "Vampire Crawlers_Data/Player.log"),
    path.join(gameDir, "Player.log"),
    path.join(home, ".config/unity3d/Nosebleed Interactive/Vampire Crawlers/Player.log"),
    path.join(home, ".local/share/Steam/steamapps/compatdata", APP_ID, "pfx/drive_c/users/steamuser/AppData/LocalLow/Nosebleed Interactive/Vampire Crawlers/Player.log"),
    path.join(home, ".local/share/Steam/steamapps/compatdata", APP_ID, "pfx/drive_c/users/steamuser/AppData/LocalLow/Nosebleed Interactive/Vampire Crawlers/Player-prev.log"),
    path.join(os.tmpdir(), "vc-surveil-last-launch.log"),
    path.join(home, ".vc-mods/vc-surveil.log"),
  ].filter((p) => {
    try { return fs.existsSync(p); } catch { return false; }
  });
}

interface TailTarget {
  file: string;
  pos: number;
  buffer: LogLineBuffer;
}

async function tailLogs(gameDir: string, abort: AbortSignal, rawLogs = false): Promise<void> {
  const files = candidateLogFiles(gameDir);
  if (files.length === 0) {
    log(`(no BepInEx/Player logs found yet — game may still be starting)`);
    log(`  expected: ${path.join(gameDir, "BepInEx/LogOutput.log")}`);
    log(`  hint: install BepInEx: surveil setup  or  surveil install --setup-bepinex`);
    return;
  }
  log(`\n— Streaming logs (Ctrl+C to stop) —`);

  const emit = (line: string, file: string) => {
    if (rawLogs) {
      process.stdout.write(`[${path.basename(file)}] ${line}\n`);
    } else {
      const formatted = formatLogLine(line, path.basename(file), true);
      if (formatted) console.log(formatted);
    }
  };

  const tails: TailTarget[] = files.map((f) => {
    let pos = 0;
    try {
      pos = fs.statSync(f).size;
      // Show up to 15 recent formatted lines on startup for immediate context
      const txt = fs.readFileSync(f, "utf8");
      const lines = txt.split("\n").slice(-30);
      const recent: string[] = [];
      for (const l of lines) {
        if (rawLogs) {
          if (l.trim()) recent.push(`[${path.basename(f)}] ${l}`);
        } else {
          const formatted = formatLogLine(l, path.basename(f), true);
          if (formatted) recent.push(formatted);
        }
      }
      if (recent.length > 0) {
        console.log(`\x1b[90m--- Recent: ${path.basename(f)} ---\x1b[0m`);
        for (const r of recent.slice(-15)) console.log(r);
      }
    } catch {}
    return { file: f, pos, buffer: new LogLineBuffer() };
  });

  return new Promise<void>((resolve) => {
    const interval = setInterval(() => {
      if (abort.aborted) {
        clearInterval(interval);
        for (const t of tails) t.buffer.flush((line) => emit(line, t.file));
        resolve();
        return;
      }
      for (const t of tails) {
        try {
          const st = fs.statSync(t.file);
          if (st.size < t.pos) t.pos = 0; // truncated
          if (st.size > t.pos) {
            const fd = fs.openSync(t.file, "r");
            const len = st.size - t.pos;
            const buf = Buffer.alloc(Math.min(len, 1 << 20));
            const read = fs.readSync(fd, buf, 0, buf.length, t.pos);
            fs.closeSync(fd);
            t.pos += read;
            const chunk = buf.subarray(0, read).toString("utf8");
            t.buffer.processChunk(chunk, (line) => emit(line, t.file));
          }
        } catch {}
      }
      // Refresh file list — new BepInEx log may appear after game boots
      const nowFiles = candidateLogFiles(gameDir);
      for (const f of nowFiles) {
        if (!tails.some((t) => t.file === f)) {
          tails.push({ file: f, pos: 0, buffer: new LogLineBuffer() });
        }
      }
    }, 400);

    abort.addEventListener(
      "abort",
      () => {
        clearInterval(interval);
        for (const t of tails) t.buffer.flush((line) => emit(line, t.file));
        resolve();
      },
      { once: true }
    );
  });
}

export async function cmdLaunch(opts: {
  gameDir: string;
  modsDir: string;
  viaSteam: boolean;
  direct: boolean;
  dryRun: boolean;
  proton: string;
  extraArgs: string[];
  tail: boolean;
  noSync: boolean;
  rawLogs?: boolean;
}) {
  const gameDir = findGameDir(opts.gameDir);
  const modsDir = resolveModsDir(opts.modsDir);

  printBanner();
  fs.mkdirSync(modsDir, { recursive: true });

  // One-time migration: if ~/.vc-mods empty but old .mods or game/Mods has mods, copy them
  try {
    const dstEmpty = fs.readdirSync(modsDir).filter((n) => !n.startsWith(".")).length === 0;
    if (dstEmpty) {
      for (const legacy of [path.join(process.cwd(), ".mods"), path.join(gameDir, "Mods")]) {
        if (fs.existsSync(legacy) && legacy !== modsDir) {
          const ents = fs.readdirSync(legacy, { withFileTypes: true }).filter((d: any) => d.isDirectory());
          if (ents.length) {
            log(`Migrating ${ents.length} mod(s) from ${legacy} → ${modsDir} (one-time)`);
            for (const e of ents) {
              const s = path.join(legacy, e.name);
              const d = path.join(modsDir, e.name);
              if (!fs.existsSync(d)) fs.cpSync(s, d, { recursive: true, force: true });
            }
            break;
          }
        }
      }
    }
  } catch {}

  // Sync to game/Mods for Proton visibility (steam launch env doesn't cross Wine)
  if (!opts.noSync) {
    syncModsToGame(modsDir, gameDir);
  }

  const exePath = path.join(gameDir, GAME_EXE);
  const hasExe = fs.existsSync(exePath);
  const bep = hasBepInEx(gameDir);
  if (bep && !opts.noSync) {
    try { ensureSteamLaunchOptions(); } catch {}
  }

  let printable: string[] = [];
  try {
    const mods = fs.readdirSync(modsDir, { withFileTypes: true }).filter((d: any) => d.isDirectory());
    printable = mods.map((d: any) => {
      try {
        const m = JSON.parse(fs.readFileSync(path.join(modsDir, d.name, "mod.json"), "utf8"));
        return `${m.id ?? d.name}@${m.version ?? "?"}`;
      } catch { return d.name; }
    });
  } catch {}

  {
    const tty = !!process.stdout.isTTY;
    const ok = (s: string) => (tty ? `\x1b[32m${s}\x1b[0m` : s);
    const bad = (s: string) => (tty ? `\x1b[33m${s}\x1b[0m` : s);
    const dim2 = (s: string) => (tty ? `\x1b[90m${s}\x1b[0m` : s);
    const bepStr = bep ? ok("ready") : bad("not installed — run: surveil setup");
    const modsStr = printable.length ? printable.join(", ") : dim2("(none — surveil install ./mods/demo-mod)");
    const exeStr = hasExe ? ok("found") : bad("missing");
    printInfoBox([
      ["Game", `${fmtPath(gameDir)}  ${exeStr}`],
      ["Mods", `${fmtPath(modsDir)}${printable.length ? `  (${printable.length})` : ""}`],
      ["BepInEx", bepStr],
      ["Library", modsStr],
    ]);
  }

  if (!hasExe) warn(`Game exe not found at ${exePath} — launch may fail. Set --game-dir / VC_GAME_DIR.`);
  if (!bep) warn(`BepInEx not detected in ${gameDir} (no BepInEx/ or winhttp.dll/doorstop_config.ini). Mods won't be visible yet. Run: surveil setup — or launch anyway to test vanilla.`);
  if (printable.length === 0) warn(`Nothing to show in-game until you install a mod to ~/.vc-mods`);

  const useSteam = opts.direct ? false : opts.viaSteam !== false;
  let cmd: string;
  let args: string[];
  let envOverlay: Record<string, string> = {};

  // Try to find a Proton `proton` script for --direct
  function findProton(): string | null {
    const candidates = [
      path.join(os.homedir(), ".local/share/Steam/steamapps/common/Proton 9.0 (Beta)/proton"),
      path.join(os.homedir(), ".local/share/Steam/steamapps/common/Proton - Experimental/proton"),
      path.join(os.homedir(), ".local/share/Steam/steamapps/common/Proton Hotfix/proton"),
      path.join(os.homedir(), ".local/share/Steam/steamapps/common/Proton 8.0/proton"),
    ];
    for (const c of candidates) if (fs.existsSync(c)) return c;
    return null;
  }

  if (useSteam && steamAvailable()) {
    cmd = "steam";
    args = [`-applaunch`, APP_ID];
    if (opts.extraArgs.length) args.push("--", ...opts.extraArgs);
    log(`Launch method: steam -applaunch ${APP_ID}  (terminal will stay and stream logs)`);
    if (bep) log(`  NOTE: Proton needs WINEDLLOVERRIDES=winhttp=n,b — if BepInEx still doesn't load, set Steam launch options for Vampire Crawlers to: WINEDLLOVERRIDES=winhttp=n,b %command%`);
  } else if (useSteam && !steamAvailable()) {
    warn(`steam not found in PATH — falling back to direct launch.`);
    const proton = findProton();
    if (proton) {
      cmd = proton;
      args = ["run", exePath, ...opts.extraArgs];
      envOverlay["STEAM_COMPAT_DATA_PATH"] = path.join(os.homedir(), ".local/share/Steam/steamapps/compatdata", APP_ID);
      envOverlay["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = path.join(os.homedir(), ".local/share/Steam");
      log(`Launch method: direct proton ${proton} run ...`);
    } else {
      cmd = exePath;
      args = [...opts.extraArgs];
      log(`Launch method: direct ${cmd} ${args.join(" ")}`);
    }
  } else {
    // --direct
    const proton = opts.proton && opts.proton !== "auto" ? opts.proton : findProton();
    if (proton && fs.existsSync(proton)) {
      cmd = proton;
      args = ["run", exePath, ...opts.extraArgs];
      envOverlay["STEAM_COMPAT_DATA_PATH"] = path.join(os.homedir(), ".local/share/Steam/steamapps/compatdata", APP_ID);
      envOverlay["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = path.join(os.homedir(), ".local/share/Steam");
      if (opts.proton && opts.proton !== "auto") envOverlay["PROTON_PATH"] = opts.proton;
      log(`Launch method: direct proton ${cmd} run ...`);
    } else if (opts.proton) {
      cmd = "proton";
      args = ["run", exePath, ...opts.extraArgs];
      if (opts.proton !== "auto") envOverlay["PROTON_PATH"] = opts.proton;
      log(`Launch method: direct ${cmd} ${args.join(" ")}`);
    } else {
      // Fallback: try wine
      const wine = spawnSync("which", ["wine"], { stdio: "ignore" }).status === 0 ? "wine" : exePath;
      cmd = wine;
      args = wine === "wine" ? [exePath, ...opts.extraArgs] : [...opts.extraArgs];
      log(`Launch method: direct ${cmd} ${args.join(" ")}${wine === "wine" ? " (wine)" : ""}`);
    }
  }

  // Proton/Wine needs winhttp override for BepInEx Doorstop — without it BepInEx never loads → no LogOutput.log / no HUD
  if (!envOverlay["WINEDLLOVERRIDES"] && !process.env.WINEDLLOVERRIDES) envOverlay["WINEDLLOVERRIDES"] = "winhttp=n,b";
  if (!envOverlay["DOORSTOP_ENABLE"] && !process.env.DOORSTOP_ENABLE) envOverlay["DOORSTOP_ENABLE"] = "TRUE";
  const childEnv = { ...process.env, VC_MODS_DIR: modsDir, SURVEIL_MODS_DIR: modsDir, ...envOverlay } as Record<string, string>;

  if (opts.dryRun) {
    log(`[dry-run] would exec: ${cmd} ${args.join(" ")}`);
    log(`[dry-run] env VC_MODS_DIR=${modsDir} WINEDLLOVERRIDES=${childEnv.WINEDLLOVERRIDES} DOORSTOP_ENABLE=${childEnv.DOORSTOP_ENABLE}`);
    log(`[dry-run] game/Mods synced: ${path.join(gameDir, "Mods")}`);
    log(`[dry-run] logs would stream: BepInEx/LogOutput.log + Player.log (Ctrl+C to stop)`);
    if (!bep) log(`[dry-run] NOTE: BepInEx not detected — first launch after install may take longer; check BepInEx/LogOutput.log appears`);
    return;
  }

  log(`\nLaunching: ${cmd} ${args.join(" ")}`);
  if (useSteam && steamAvailable()) {
    // Steam launch is async — fire it, then track game process and stream logs
    const child = spawn(cmd, args, { stdio: "inherit", env: childEnv, detached: false });
    child.on("error", (e) => warn(`steam spawn error: ${e.message} — game may still have launched`));

    const ac = new AbortController();
    let userAborted = false;
    const sig = () => {
      userAborted = true;
      ac.abort();
    };
    process.on("SIGINT", sig);
    process.on("SIGTERM", sig);

    log(`Game launched via Steam. Tracking game process (CLI will auto-close when game closes, or press Ctrl+C)...`);

    // Stream logs in background
    let tailPromise: Promise<void> | null = null;
    if (opts.tail !== false) {
      await new Promise((r) => setTimeout(r, 1500));
      tailPromise = tailLogs(gameDir, ac.signal, opts.rawLogs);
    } else {
      log(`(tail disabled with --no-tail)`);
    }

    // Monitor game process lifecycle
    let gameDetected = false;
    let absentTicks = 0;
    const maxWaitMs = 45000;
    const startTime = Date.now();

    while (!ac.signal.aborted) {
      await new Promise((r) => setTimeout(r, 1000));
      if (ac.signal.aborted) break;

      const pids = findGamePids();
      if (pids.length > 0) {
        if (!gameDetected) {
          gameDetected = true;
          log(`Vampire Crawlers running (PID: ${pids.join(", ")}). Monitoring session...`);
        }
        absentTicks = 0;
      } else {
        if (gameDetected) {
          absentTicks++;
          // Require 2 consecutive checks to confirm game process closure
          if (absentTicks >= 2) {
            log(`Vampire Crawlers has closed.`);
            break;
          }
        } else {
          // Still waiting for process to launch
          if (Date.now() - startTime > maxWaitMs) {
            warn(`Game process not detected within ${Math.round(maxWaitMs / 1000)}s.`);
            break;
          }
        }
      }
    }

    // Give logs 1.2s to flush any final writes
    await new Promise((r) => setTimeout(r, 1200));
    ac.abort();
    if (tailPromise) {
      try { await tailPromise; } catch {}
    }

    process.off("SIGINT", sig);
    process.off("SIGTERM", sig);

    if (userAborted) {
      log(`\nLog streaming stopped (game may still be running).`);
      process.exit(0);
    }

    // Check for crash and output readable trace
    const crash = analyzeCrash(gameDir);
    if (crash) {
      console.error(renderCrashReport(crash));
      process.exit(1);
    } else {
      log(`Game session exited cleanly.`);
      process.exit(0);
    }
  }

  // Direct launch: inherit stdio so game logs appear inline; keep terminal until game exits
  log(`Terminal will stay active until the game exits (Ctrl+C to kill). Streaming BepInEx/Player logs inline...`);
  const ac2 = new AbortController();
  const child2 = spawn(cmd, args, { stdio: "inherit", env: childEnv, detached: false });
  // Tail in parallel
  let tailP: Promise<void> | null = null;
  if (opts.tail !== false) {
    await new Promise((r) => setTimeout(r, 1500));
    tailP = tailLogs(gameDir, ac2.signal, opts.rawLogs);
  }
  const exit: number = await new Promise((resolve) => {
    child2.on("close", (code) => resolve(code ?? 0));
    child2.on("error", (e) => { fail(`failed to launch: ${e.message}`); resolve(1); });
  });
  ac2.abort();
  if (tailP) await tailP;
  log(`Game process exited with code ${exit}`);

  // Check for crash report
  const crashDirect = analyzeCrash(gameDir);
  if (crashDirect) {
    console.error(renderCrashReport(crashDirect));
    process.exit(1);
  } else if (exit !== 0) {
    process.exit(exit);
  }

  log(`Game session exited cleanly.`);
  process.exit(0);
}
