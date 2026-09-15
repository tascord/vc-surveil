import fs from "node:fs";
import path from "node:path";
import { fail, log, printBanner, printInfoBox, fmtPath } from "../util.js";

export async function cmdInit(name: string, opts: { template: string }) {
  if (!/^[a-z0-9_-]+$/.test(name)) fail(`Invalid mod name "${name}" — use [a-z0-9_-]`);
  const dest = path.resolve(name);
  if (fs.existsSync(dest)) fail(`Destination already exists: ${dest}`);

  fs.mkdirSync(dest, { recursive: true });
  fs.mkdirSync(path.join(dest, "src"), { recursive: true });

  const modJson = {
    id: name,
    name: name,
    version: "0.1.0",
    entry: "dist/index.js",
    apiVersion: "0.1.0",
    description: "A Vampire Crawlers mod",
  };
  fs.writeFileSync(path.join(dest, "mod.json"), JSON.stringify(modJson, null, 2) + "\n");

  fs.writeFileSync(
    path.join(dest, "package.json"),
    JSON.stringify(
      {
        name: name,
        private: true,
        version: "0.1.0",
        type: "module",
        scripts: { build: "tsc -p tsconfig.json", typecheck: "tsc --noEmit -p tsconfig.json" },
        dependencies: { "@vc-surveil/api": "^0.1.0" },
      },
      null,
      2,
    ) + "\n",
  );

  fs.writeFileSync(
    path.join(dest, "tsconfig.json"),
    JSON.stringify(
      {
        extends: "@vc-surveil/api/tsconfig.base.json",
        compilerOptions: { outDir: "dist", rootDir: "src", composite: true },
        include: ["src/**/*"],
        references: [{ path: "node_modules/@vc-surveil/api" }],
      },
      null,
      2,
    ) + "\n",
  );

  // Minimal example index.ts — references the demo-mod cards as a starting point
  fs.writeFileSync(
    path.join(dest, "src/index.ts"),
    `import type { CardDef, VcApi } from "@vc-surveil/api";

declare global {
  var vc: VcApi;
}

const vc: VcApi = globalThis.vc;

const myCard: CardDef = {
  id: "${name}:my_first_card",
  name: "My First Card",
  color: "attack",
  mana: 1,
  damage: 25,
  text: "Deal 25 damage.",
  gemSlots: 1,
  iconKey: "Whip",
};

vc.cards.register(myCard);
vc.log("${name} loaded");
`,
  );

  fs.writeFileSync(path.join(dest, ".gitignore"), "node_modules/\ndist/\n");

  printBanner();
  printInfoBox([
    ["Created", fmtPath(dest)],
    ["Next", `cd ${name} && npm install && npm run build`],
  ]);
}
