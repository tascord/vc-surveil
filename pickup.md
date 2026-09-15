# pickup.md — VC Surveil / Vampire Crawlers Modding API

> Persistent tracker. Update every session. This is how we pick up where we left off.

## Goal
TypeScript modding API for Vampire Crawlers (Unity IL2CPP, codename `Pancake`, `Nosebleed.Pancake.*`). Mods authored in TS, compiled to JS, loaded at runtime via BepInEx IL2CPP + JS bridge (Jint early, V8 later).

## Where we left off
- [x] Reverse-engineered install at `~/.local/share/Steam/steamapps/common/Vampire Crawlers/` — IL2CPP, `GameAssembly.dll` 83MB, `global-metadata.dat` v31, Addressables in `StreamingAssets/aa/` (biome/room/localization bundles), no vanilla mod support.
- [x] Wiki crawl (`Cards`, `Gems`, `Arcanas`, `Relics`, `Dungeons`, `Enemies`, `Evolution`) — catalogued moddable domains.
- [x] Scaffolding complete — devenv, `@vc-surveil/api`, `@vc-surveil/surveil` CLI, loader stub, demo-mod with 5 test cards. All workspaces build. CLI install/launch verified (dry-run + local copy).

## Repo layout
```
vc-surveil/
  pickup.md
  devenv.nix / devenv.yaml  # devenv shell provides `surveil` + `surveil-build` (no npm i needed)
  package.json              # npm workspaces: api + cli + demo-mod
  packages/
    api/                    # @vc-surveil/api — TS types for mods
    cli/                    # @vc-surveil/surveil — `surveil` CLI (also `npx @vc-surveil/surveil`)
    loader/                 # PancakeModLoader — C# BepInEx IL2CPP plugin (Jint bridge)
  mods/
    demo-mod/               # runnable demo mod: 5 test cards
  .mods/                    # (gitignored) local Mods/ for `surveil install` tests
  tools/
    dump-metadata.py        # parses global-metadata.dat strings for Nosebleed.Pancake namespaces
    catalog-inspect.py      # inspects StreamingAssets/aa/catalog.bin
  analysis/                 # (gitignored) Cpp2IL dummy DLLs, catalog.json dumps
  docs/
    reverse-engineering.md
    api.md
```

## Next steps (ordered)
1. [x] `devenv.nix` + `devenv.yaml` — nodejs_22, typescript, dotnet-sdk 8, python3; exposes `surveil` + `surveil-build` scripts (no npm i)
2. [x] `packages/api` — typed `vc.*` namespaces (cards/gems/arcanas/relics/enemies/evolutions/dungeons/state/hooks)
3. [x] `mods/demo-mod` — 5 test cards built with `vc.cards.register` (attack/defense/mana/wild/character proof) → `dist/index.js`
4. [x] `packages/cli` (`@vc-surveil/surveil`) — `surveil install <github>`, `list`, `init`, `build`, `launch` (via `steam -applaunch 3265700`)
5. [x] `packages/loader` — BepInEx6 IL2CPP stub: ModDiscovery, Jint host, `vc` global, Harmony patch points
6. [x] `tools/dump-metadata.py` + `catalog-inspect.py`
7. [x] Verify: `surveil-build` / `npm run build` pass; `surveil install ./mods/demo-mod --mods-dir ./.mods` + `surveil list` + `surveil launch --dry-run` verified
8. [x] Publish flow: `publish.sh` + `surveil-publish` devenv script (builds first, verifies `dist/`, then `npm publish --access public --tag latest`; supports `--dry-run`, `--api-only`, `--cli-only`)

## Demo mod — test cards (spec)
File: `mods/demo-mod/src/index.ts` registers via `vc.cards.register`:
- `test_strike` — attack, 1 mana, 30 dmg “Deal 30 damage.” (baseline attack)
- `test_shield` — defense, 1 mana, +5 Armor “Add 5 Armor.” (baseline defense)
- `test_battery` — mana, 3 mana cost → +5 Mana “Overcharge: Add 5 Mana.” (mana economy test)
- `test_joker` — wild, W cost, “Free: Draw 1, +1 Mana. Wild.” (wild/combo test)
- `test_antonio_clone` — character/crawler, 0 mana, “Add 3 Armor. Crawler trigger: +10% dmg on red.” (crawler trigger test)
All have `modId: "demo-mod"` prefix, `gemSlots: 1`, icons reused from vanilla via `iconKey`.

## How to demo (devenv — surveil is on PATH)

```bash
cd ~/Projects/vc-surveil
devenv shell            # or direnv allow — provides `surveil` + `surveil-build`
surveil --help          # no npx / npm install needed
surveil-build           # or: npm run build
surveil install ./mods/demo-mod --mods-dir ./.mods --force   # local install test
surveil list --mods-dir ./.mods
surveil launch --dry-run                               # → steam -applaunch 3265700
# npx equivalents (outside devenv):
npx @vc-surveil/surveil install https://github.com/<org>/<mod>
npx @vc-surveil/surveil launch --dry-run
cat mods/demo-mod/dist/index.js | head -n 40
# when loader is installed in game dir: surveil install ... --mods-dir ~/.local/share/Steam/.../Vampire\ Crawlers/Mods && surveil launch
```

## Decisions / constraints
- IL2CPP only — no Mono hotpatch; BepInEx 6 BE + Il2CppInterop required. Cpp2IL dump needed for full type map (not committed).
- Addressables: `catalog.bin` binary; start with loader-side catalog merge, not bundle rebuild. Custom art later via `addressables/*.bundle`.
- Save isolation: demo cards flagged `modded:true` so vanilla saves degrade gracefully.

## Log
- 2026-09-07: Init tracker. Scaffolding started. Devenv target: nodejs_22 + dotnet 8 + python3. Demo mod spec frozen as above.
- 2026-09-07: CLI `@vc-surveil/surveil` added (`install` GitHub/local, `launch` via Steam with BepInEx injection, `list/init/build`). Devenv now exposes `surveil` + `surveil-build` scripts — replaces `npx`/`npm i`. Demo install + launch dry-run verified. README updated.
- 2026-09-07: Build fix: `devenv.nix` `nodePackages.typescript` → `pkgs.typescript` (nodePackages removed on nixpkgs rolling). `publish.sh` + `surveil-publish` added — builds all workspaces and publishes `@vc-surveil/api` + `@vc-surveil/surveil` with auth check, pack preview, and tag/access flags. Verified via `--dry-run`.
- 2026-09-07: Installs moved to `~/.vc-mods` (was `game/Mods`), loader now scans `~/.vc-mods` + legacy fallback, launch syncs via symlink for Proton and streams BepInEx logs with terminal stay-alive (Ctrl+C stops tail, game stays). BepInEx perm fix (`d---------` → `u+rwX`).
- 2026-09-07: Card rarity/weight added (`CardDropConfig` `pools`/`weight`/`luckFactor`/`chestTiers`) — stats located via `dump-metadata.py` (`LevelUpRewardWeighting`/`ChestRewardWeighting`/`CardRewardPool`/`ChestTier`). Dev console (`DevConsole.cs`) with `` ` ``/F1 toggle, commands `help, mods, give, cards, clear` + `vc.console.register` API. Devenv now auto-provisions run deps on shell entry (`surveil-ensure-game`): BepInEx 6 BE, `PancakeModLoader.dll`, `~/.vc-mods/demo-mod` — no `surveil setup` needed (override with `VC_SURVEIL_AUTO_SETUP=0`).
- 2026-09-07: Unity 6 metadata v31 fix: upgraded BepInEx 6 BE to build #788 (`LibCpp2IL` supports metadata versions 23-106, fixing `Unsupported metadata version found! We support 23-29, got 31`). Updated `surveil setup` and `surveil-ensure-game` in `devenv.nix` with auto-detection of metadata 31 incompatibility, stale interop cache clearing, pre/post permission fixes for `dotnet/`, automatic compilation of `PancakeModLoader.dll`, and `--force` support.
