import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import { spawnSync } from "node:child_process";

export function log(msg: string) {
  console.log(msg);
}
export function warn(msg: string) {
  console.warn(`warn: ${msg}`);
}
export function fail(msg: string): never {
  console.error(`error: ${msg}`);
  process.exit(1);
  throw new Error(msg); // satisfy TS never
}

// ── banner + info box ──────────────────────────────────────────────

const BANNER_LINES = [
  "  ___ _   _ ___ __   __ ___ ___ ___ ",
  " / __| | | | _ \\ \\ / /| __|_ _| _ \\",
  " \\__ \\ |_| |   /  \\ V / | _| | ||   /",
  " |___/ \\___/|_|_\\   \\_/  |___|___|_|_\\",
];

function isTty(): boolean {
  return !!process.stdout.isTTY;
}

function dim(s: string): string {
  return isTty() ? `\x1b[90m${s}\x1b[0m` : s;
}
function cyan(s: string): string {
  return isTty() ? `\x1b[36m${s}\x1b[0m` : s;
}
function bold(s: string): string {
  return isTty() ? `\x1b[1m${s}\x1b[0m` : s;
}

export function printBanner(version = "0.1.0"): void {
  // skip if not a TTY and not explicitly requested — keep --json / piped output clean
  if (process.env.NO_COLOR) {
    log(`surveil v${version} — Vampire Crawlers mod toolkit`);
    return;
  }
  for (const l of BANNER_LINES) log(cyan(l));
  log(dim(`  vampire crawlers  ·  surveil  v${version}  ·  https://github.com/vc-surveil`));
  log("");
}

function shorten(p: string): string {
  const home = os.homedir();
  if (p.startsWith(home)) return "~" + p.slice(home.length);
  return p;
}

export function printInfoBox(rows: Array<[string, string]>): void {
  const labelW = Math.max(...rows.map(([k]) => k.length), 8);
  const termW = process.stdout.columns ?? 80;
  const innerW = Math.min(64, Math.max(48, termW - 4));
  const h = "─".repeat(innerW);
  const top = dim(`┌${h}┐`);
  const mid = dim(`├${h}┤`);
  const bot = dim(`└${h}┘`);
  const pad = (s: string, w: number) => s + " ".repeat(Math.max(0, w - s.length));

  log(top);
  // title row inside box — reuse banner style if TTY
  const title = ` ${bold("surveil")} ${dim("— Vampire Crawlers mod toolkit")}`;
  // crude strip of ansi for width calc
  const stripAnsi = (s: string) => s.replace(/\x1b\[[0-9;]*m/g, "");
  const titlePad = innerW - stripAnsi(title).length - 1;
  log(`${dim("│")}${title}${" ".repeat(Math.max(0, titlePad))}${dim("│")}`);
  log(mid);
  for (const [k, v] of rows) {
    const label = pad(k, labelW);
    // truncate value to fit inner width
    const maxVal = innerW - labelW - 4;
    let val = v;
    if (stripAnsi(val).length > maxVal) val = val.slice(0, maxVal - 1) + "…";
    // value already may be dim/etc; keep as-is
    const left = ` ${dim(label)}  ${val}`;
    const used = stripAnsi(left).length;
    const rightPad = innerW - used - 1;
    log(`${dim("│")}${left}${" ".repeat(Math.max(0, rightPad))}${dim("│")}`);
  }
  log(bot);
}

export function fmtPath(p: string): string {
  return shorten(p);
}

export const SURVEIL_HOME = path.join(os.homedir(), ".vc-mods");
export const SURVEIL_MODS_LEGACY_GAME = path.join(os.homedir(), ".local/share/Steam/steamapps/common/Vampire Crawlers/Mods");

export function defaultModsDir(): string {
  // New canonical home: ~/.vc-mods
  return SURVEIL_HOME;
}

export function resolveModsDir(explicit: string): string {
  if (explicit && explicit.trim()) return path.resolve(explicit);
  const env = process.env.VC_MODS_DIR || process.env.SURVEIL_MODS_DIR;
  if (env) return path.resolve(env);
  return defaultModsDir();
}

export function legacyGameModsDir(): string {
  return SURVEIL_MODS_LEGACY_GAME;
}

export function run(cmd: string, args: string[], cwd: string, opts: { allowFail?: boolean } = {}) {
  const r = spawnSync(cmd, args, { cwd, stdio: "inherit", shell: false });
  if (r.status !== 0 && !opts.allowFail) {
    fail(`${cmd} ${args.join(" ")} failed with ${r.status}`);
  }
  return r.status ?? 0;
}

export function hasCmd(cmd: string): boolean {
  const r = spawnSync(cmd, ["--version"], { stdio: "ignore" });
  return r.status === 0;
}

export function copyDir(src: string, dest: string) {
  fs.cpSync(src, dest, { recursive: true, force: true });
}

export function parseGithubSource(src: string): { url: string; subpath: string; name: string } | null {
  // Accept:
  //  https://github.com/org/repo
  //  https://github.com/org/repo.git
  //  https://github.com/org/repo/tree/main/subpath
  //  https://github.com/org/repo#subpath
  //  org/repo  -> https://github.com/org/repo
  //  github:org/repo
  let url = src;
  let subpath = "";
  // fragment subpath
  const hashIdx = url.indexOf("#");
  if (hashIdx !== -1) {
    subpath = url.slice(hashIdx + 1).replace(/^\/+/, "");
    url = url.slice(0, hashIdx);
  }
  // /tree/<branch>/<path> form
  const treeMatch = url.match(/^(https?:\/\/github\.com\/[^/]+\/[^/]+)\/tree\/[^/]+\/(.+)$/);
  if (treeMatch) {
    url = treeMatch[1]!;
    subpath = treeMatch[2]!;
  }
  // shorthand org/repo
  if (/^[^/]+\/[^/]+$/.test(url) && !url.includes("://")) {
    url = `https://github.com/${url}`;
  }
  if (url.startsWith("github:")) {
    url = `https://github.com/${url.slice("github:".length)}`;
  }
  if (!/^https?:\/\/github\.com\//.test(url)) return null;
  if (!url.endsWith(".git")) url += ".git";
  // derive folder name
  const m = url.match(/github\.com\/[^/]+\/([^\/.]+)(?:\.git)?/);
  const name = m ? m[1]! : "mod";
  return { url, subpath, name };
}

export function inferNameFromRepoUrl(url: string): string {
  const m = url.match(/\/([^\/.]+)(?:\.git)?(?:\/|$)/);
  return m ? m[1]! : "mod";
}
