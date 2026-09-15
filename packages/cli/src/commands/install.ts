import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import { spawnSync } from "node:child_process";
import { copyDir, defaultModsDir, fail, hasCmd, log, parseGithubSource, resolveModsDir, run, warn, printBanner, printInfoBox, fmtPath } from "../util.js";

export async function cmdInstall(
  source: string,
  opts: { modsDir: string; branch: string; build: boolean; force: boolean },
) {
  const modsDir = resolveModsDir(opts.modsDir);
  fs.mkdirSync(modsDir, { recursive: true });
  printBanner();
  printInfoBox([["Mods dir", fmtPath(modsDir)]]);

  const isLocal = fs.existsSync(path.resolve(source));
  let fetchedPath: string;
  let inferredName: string;

  if (isLocal) {
    const abs = path.resolve(source);
    const stat = fs.statSync(abs);
    const srcDir = stat.isDirectory() ? abs : path.dirname(abs);

    // Build the local mod in its source workspace first so all workspace references and dist exist
    if (opts.build !== false && hasCmd("npm")) {
      const srcPkg = path.join(srcDir, "package.json");
      if (fs.existsSync(srcPkg)) {
        try {
          const pkg = JSON.parse(fs.readFileSync(srcPkg, "utf8"));
          if (pkg.scripts && pkg.scripts.build) {
            log(`Building local mod in ${srcDir} ...`);
            run("npm", ["run", "build"], srcDir, { allowFail: true });
          }
        } catch {}
      }
    }

    // Try to read mod.json id for dest name
    let modId: string | null = null;
    try {
      const j = JSON.parse(fs.readFileSync(path.join(srcDir, "mod.json"), "utf8"));
      modId = j.id || null;
    } catch {}
    inferredName = modId || path.basename(srcDir);
    const dest = path.join(modsDir, inferredName);
    if (fs.existsSync(dest) && !opts.force) fail(`Destination already exists: ${dest} (use --force)`);
    if (fs.existsSync(dest)) fs.rmSync(dest, { recursive: true, force: true });
    copyDir(srcDir, dest);
    fetchedPath = dest;
    log(`Copied local mod ${srcDir} → ${dest}`);
  } else {
    const parsed = parseGithubSource(source);
    if (!parsed) fail(`Not a GitHub URL or local path: ${source}\nExamples: surveil install https://github.com/org/mod  |  surveil install ./my-mod`);

    if (!hasCmd("git")) fail(`git not found — needed to fetch ${parsed.url}`);

    const tmpBase = fs.mkdtempSync(path.join(os.tmpdir(), "surveil-"));
    const cloneDir = path.join(tmpBase, "repo");
    log(`Cloning ${parsed.url} → ${cloneDir}`);
    const cloneArgs = ["clone", "--depth", "1"];
    if (opts.branch) cloneArgs.push("--branch", opts.branch);
    cloneArgs.push(parsed.url, cloneDir);
    const st = spawnSync("git", cloneArgs, { stdio: "inherit" });
    if (st.status !== 0) fail(`git clone failed`);

    let srcInRepo = cloneDir;
    if (parsed.subpath) {
      srcInRepo = path.join(cloneDir, parsed.subpath);
      if (!fs.existsSync(srcInRepo)) fail(`Subpath not found in repo: ${parsed.subpath}`);
    }

    // Discover mod root: walk up from subpath if needed, look for mod.json
    let modRoot = srcInRepo;
    if (!fs.existsSync(path.join(modRoot, "mod.json"))) {
      // if repo root has mod.json, use that
      if (fs.existsSync(path.join(cloneDir, "mod.json"))) modRoot = cloneDir;
      // else if subpath is a folder containing a mod, use subpath
      // fallback: use srcInRepo
    }
    let modId: string | null = null;
    try {
      const j = JSON.parse(fs.readFileSync(path.join(modRoot, "mod.json"), "utf8"));
      modId = j.id || null;
    } catch {}
    inferredName = modId || parsed.name || path.basename(modRoot);
    const dest = path.join(modsDir, inferredName);
    if (fs.existsSync(dest) && !opts.force) fail(`Destination already exists: ${dest} (use --force)`);
    if (fs.existsSync(dest)) fs.rmSync(dest, { recursive: true, force: true });
    copyDir(modRoot, dest);
    // Don't copy .git
    fs.rmSync(path.join(dest, ".git"), { recursive: true, force: true });
    fetchedPath = dest;
    log(`Installed ${parsed.url}${parsed.subpath ? `#${parsed.subpath}` : ""} → ${dest}`);
    fs.rmSync(tmpBase, { recursive: true, force: true });
  }

  // Optionally build (npm install && npm run build)
  if (opts.build === false) {
    log(`Skipping build (--no-build). You may need to run: surveil build ${fetchedPath}`);
    logDone(fetchedPath, modsDir);
    return;
  }

  const pkgJson = path.join(fetchedPath, "package.json");
  if (fs.existsSync(pkgJson)) {
    const pkg = JSON.parse(fs.readFileSync(pkgJson, "utf8"));
    const hasBuild = !!(pkg.scripts && pkg.scripts.build);
    if (hasCmd("npm")) {
      const hasDist =
        fs.existsSync(path.join(fetchedPath, "dist/index.js")) ||
        fs.existsSync(path.join(fetchedPath, "dist/mod.js")) ||
        (fs.existsSync(path.join(fetchedPath, "dist")) && fs.readdirSync(path.join(fetchedPath, "dist")).length > 0);

      if (hasDist) {
        log(`Skipping build — ${fetchedPath}/dist already present (fresh copy)`);
      } else {
        if (!fs.existsSync(path.join(fetchedPath, "node_modules"))) {
          log(`npm install in ${fetchedPath} ...`);
          run("npm", ["install", "--ignore-scripts"], fetchedPath, { allowFail: true });
        }
        if (hasBuild) {
          log(`npm run build in ${fetchedPath} ...`);
          const st = spawnSync("npm", ["run", "build"], { cwd: fetchedPath, stdio: "inherit" });
          if (st.status !== 0) warn(`build failed in ${fetchedPath} — mod may need manual build`);
        }
      }
    } else {
      warn(`npm not found — skipping build; run manually in ${fetchedPath}`);
    }
  } else {
    log(`No package.json in ${fetchedPath} — skipping build`);
  }

  logDone(fetchedPath, modsDir);
}

function logDone(dest: string, modsDir: string) {
  const def = defaultModsDir();
  log("");
  const rows: Array<[string, string]> = [
    ["Installed", fmtPath(dest)],
    ["Home", fmtPath(def) + "  (canonical)"],
  ];
  if (modsDir !== def) rows.push(["Custom dir", fmtPath(modsDir) + "  via $VC_MODS_DIR"]);
  const noDist = !fs.existsSync(path.join(dest, "dist")) && !fs.existsSync(path.join(dest, "dist/mod.js")) && !fs.existsSync(path.join(dest, "dist/index.js"));
  if (noDist) rows.push(["Next", `surveil build ${fmtPath(dest)}`]);
  printInfoBox(rows);
}
