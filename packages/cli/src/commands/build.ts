import fs from "node:fs";
import path from "node:path";
import { fail, hasCmd, log, run, printBanner, printInfoBox, fmtPath } from "../util.js";

export async function cmdBuild(modPath: string | undefined, _opts: { modsDir: string }) {
  const dir = path.resolve(modPath || ".");
  if (!fs.existsSync(path.join(dir, "package.json"))) fail(`No package.json in ${dir}`);
  if (!hasCmd("npm")) fail(`npm not found`);
  const pkg = JSON.parse(fs.readFileSync(path.join(dir, "package.json"), "utf8"));
  if (!pkg.scripts || !pkg.scripts.build) {
    fail(`No "build" script in ${dir}/package.json`);
  }
  printBanner();
  printInfoBox([["Building", fmtPath(dir)]]);
  if (!fs.existsSync(path.join(dir, "node_modules"))) {
    log(`npm install in ${dir} ...`);
    run("npm", ["install", "--ignore-scripts"], dir);
  }
  log(`npm run build in ${dir} ...`);
  run("npm", ["run", "build"], dir);
  printInfoBox([["Built", fmtPath(dir)]]);
}
