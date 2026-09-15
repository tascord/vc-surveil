#!/usr/bin/env node
import { cac } from "cac";
import { cmdInstall } from "./commands/install.js";
import { cmdList } from "./commands/list.js";
import { cmdInit } from "./commands/init.js";
import { cmdBuild } from "./commands/build.js";
import { cmdLaunch } from "./commands/launch.js";
import { cmdSetup } from "./commands/setup.js";
import { printBanner } from "./util.js";

const cli = cac("surveil");

// surveil install https://github.com/org/mod [--mods-dir <path>] [--branch main] [--no-build]
cli
  .command("install <source>", "Install a mod from a GitHub URL or local path")
  .option("--mods-dir <dir>", "Mods folder (default: ~/.vc-mods)", { default: "" })
  .option("--branch <branch>", "Git branch/tag to checkout", { default: "" })
  .option("--no-build", "Skip npm install && npm run build in the fetched mod")
  .option("--force", "Overwrite existing mod with same name")
  .action(cmdInstall);

// surveil list
cli
  .command("list", "List installed mods")
  .option("--mods-dir <dir>", "Mods folder (default: ~/.vc-mods)", { default: "" })
  .option("--json", "Output JSON")
  .action(cmdList);

// surveil init <name>
cli
  .command("init <name>", "Scaffold a new mod from template (creates ./<name>/)")
  .option("--template <t>", "Template to use", { default: "basic" })
  .action(cmdInit);

// surveil build [path]
cli
  .command("build [modPath]", "Run npm install && npm run build in a mod")
  .option("--mods-dir <dir>", "Unused; for symmetry", { default: "" })
  .action(cmdBuild);

cli
  .command("setup", "One-time BepInEx setup so mods become visible in-game")
  .option("--game-dir <dir>", "Game install dir", { default: "" })
  .option("--yes", "Auto-download BepInEx", { default: false })
  .option("--force", "Force re-download / overwrite existing BepInEx", { default: false })
  .option("--dry-run", "Preview without downloading", { default: false })
  .action(cmdSetup);

// surveil launch — npx @vc-surveil/surveil launch
cli
  .command("launch", "Launch Vampire Crawlers via Steam with mods injected")
  .option("--game-dir <dir>", "Game install dir (default: ~/.local/share/Steam/steamapps/common/Vampire Crawlers)", { default: "" })
  .option("--mods-dir <dir>", "Mods folder (default: ~/.vc-mods)", { default: "" })
  .option("--direct", "Launch exe directly instead of via `steam -applaunch`")
  .option("--proton <path>", "Proton path for direct launch (or 'auto')", { default: "" })
  .option("--dry-run", "Print what would be executed without launching")
  .option("--no-tail", "Don't stream BepInEx/Player logs (terminal still stays, but quiet)")
  .option("--no-sync", "Don't sync ~/.vc-mods → <game>/Mods before launch")
  .option("--raw-logs", "Output raw unformatted logs without badges or noise filtering")
  .action((opts: any) => {
    const raw: string[] = (cli as any).rawArgs ?? [];
    const sepIdx = raw.indexOf("--");
    const extra = sepIdx !== -1 ? raw.slice(sepIdx + 1) : [];
    const direct = !!opts.direct;
    return cmdLaunch({
      gameDir: opts.gameDir ?? "",
      modsDir: opts.modsDir ?? "",
      viaSteam: !direct,
      direct,
      dryRun: !!opts.dryRun,
      proton: opts.proton ?? "",
      extraArgs: extra,
      tail: opts.tail !== false,
      noSync: !!opts.noSync,
      rawLogs: !!opts.rawLogs,
    });
  });

cli.help();
cli.version("0.1.0");

// figlet banner before help / version output — not for --json or subcommands that print their own
{
  const argv = process.argv.slice(2);
  const noBanner = argv.includes("--json") || argv.includes("--raw-logs");
  const isHelp = argv.length === 0 || argv.includes("-h") || argv.includes("--help") || argv.includes("-v") || argv.includes("--version");
  const isSub = ["launch", "setup", "install", "list", "init", "build"].some((c) => argv.includes(c));
  if (isHelp && !noBanner && !isSub) printBanner();
}

cli.parse();
