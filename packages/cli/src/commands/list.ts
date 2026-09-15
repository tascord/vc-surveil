import fs from "node:fs";
import path from "node:path";
import { resolveModsDir, log, printBanner, printInfoBox, fmtPath } from "../util.js";

export async function cmdList(opts: { modsDir: string; json: boolean }) {
  const modsDir = resolveModsDir(opts.modsDir);
  if (!fs.existsSync(modsDir)) {
    if (opts.json) console.log("[]");
    else log(`No Mods dir yet: ${modsDir}`);
    return;
  }
  const entries = fs.readdirSync(modsDir, { withFileTypes: true }).filter((d: any) => d.isDirectory());
  const mods = entries.map((d: any) => {
    const dir = path.join(modsDir, d.name);
    let manifest: any = null;
    try {
      manifest = JSON.parse(fs.readFileSync(path.join(dir, "mod.json"), "utf8"));
    } catch {}
    const hasDist = fs.existsSync(path.join(dir, "dist"));
    return { name: d.name, dir, manifest, hasDist };
  });

  if (opts.json) {
    console.log(JSON.stringify(mods, null, 2));
    return;
  }

  printBanner();
  const tty = !!process.stdout.isTTY;
  const dim = (s: string) => (tty ? `\x1b[90m${s}\x1b[0m` : s);
  const green = (s: string) => (tty ? `\x1b[32m${s}\x1b[0m` : s);
  const yellow = (s: string) => (tty ? `\x1b[33m${s}\x1b[0m` : s);
  printInfoBox([
    ["Mods dir", fmtPath(modsDir)],
    ["Count", String(mods.length)],
  ]);

  if (mods.length === 0) {
    log(dim("  (none — install one with: surveil install <path-or-github-url>)"));
    return;
  }

  // table header
  const idW = Math.max(...mods.map((m) => (m.manifest?.id ?? m.name).length), 6);
  const verW = Math.max(...mods.map((m) => (m.manifest?.version ?? "?").length), 5);
  log("");
  log(`${dim(pad("  ID", idW + 2))}  ${dim(pad("Version", verW))}  ${dim("Status")}   ${dim("Entry")}`);
  log(dim(`  ${"─".repeat(idW)}  ${"─".repeat(verW)}  ${"─".repeat(7)}  ${"─".repeat(20)}`));
  for (const m of mods) {
    const id = m.manifest?.id ?? m.name;
    const ver = m.manifest?.version ?? "?";
    const entry = m.manifest?.entry ?? "dist/mod.js";
    const built = m.hasDist ? green("built") : yellow("not built");
    const plainBuilt = m.hasDist ? "built" : "not built";
    // pad without ansi for alignment
    const idPad = id + " ".repeat(Math.max(0, idW - id.length));
    const verPad = ver + " ".repeat(Math.max(0, verW - ver.length));
    const builtPad = plainBuilt + " ".repeat(Math.max(0, 7 - plainBuilt.length));
    // render with colors but keep visual alignment via plain lengths
    const builtCol = m.hasDist ? green(builtPad) : yellow(builtPad);
    log(`  ${idPad}  ${verPad}  ${builtCol}  ${dim(entry)} ${dim("·")} ${dim(fmtPath(m.dir))}`);
  }

function pad(s: string, w: number): string {
  return s + " ".repeat(Math.max(0, w - s.length));
}
}
