# vc-surveil — Vampire Crawlers Modding API (TypeScript)

IL2CPP game at `~/.local/share/Steam/steamapps/common/Vampire Crawlers` — codename `Pancake` (`Nosebleed.Pancake.*`). TS-first modding API: author in TypeScript, compile to JS, load via BepInEx 6 IL2CPP + Jint bridge.

## Quick start (devenv — no `npm install` or `surveil setup` needed)

```bash
cd ~/Projects/vc-surveil
devenv shell        # or: direnv allow  — auto-provisions BepInEx + loader + ~/.vc-mods
surveil --help      # ⟵ devenv script, replaces npx
surveil list        # → mods in ~/.vc-mods (canonical home)
surveil launch --dry-run   # preview: steam -applaunch 3265700 + log streaming
cat pickup.md
```

All run deps are auto-provisioned on shell entry via `surveil-ensure-game`:
BepInEx 6 BE (Unity.IL2CPP win-x64), `PancakeModLoader.dll` (built via `dotnet build`), and `~/.vc-mods/demo-mod` are created/synced to `game/Mods/` (symlinked for Proton). No manual `surveil setup` — disable with `VC_SURVEIL_AUTO_SETUP=0 devenv shell`.

To force a clean build: `surveil-build` (or `npm run build`).

## Install a mod from GitHub (like `npx @vc-surveil/surveil install`)

```bash
# Inside devenv shell — surveil is on PATH, installs to ~/.vc-mods:
surveil install https://github.com/<org>/<mod>          # clones → ~/.vc-mods/<mod> and builds
surveil install https://github.com/<org>/<mod>#subpath --force
surveil install ./my-local-mod                         # → ~/.vc-mods (no --mods-dir needed)
surveil list                                           # verify

# Outside devenv / CI — same via npx:
npx @vc-surveil/surveil install https://github.com/<org>/<mod>
npx @vc-surveil/surveil list
npx surveil install https://github.com/<org>/<mod>  # unscoped alias also works
```

## Dev console (`) + card rarity

Press `` ` `` (backtick) or `F1` in-game to toggle the dev console (terminal also stays open and streams `BepInEx/LogOutput.log`):

```bash
help                          # list commands
cards                         # registered mod cards with rarity/weight/pools
give demo-mod:test_strike 3   # add cards (hook → deck/hand, scaffold logs to console+terminal)
mods                          # loaded mod ids
clear
```

**Card rarity / chance** (`packages/api/src/index.ts:19`):
Cards take `rarity` (`Common`…`UltraRare`), `weight` (or `drop.weight`), `drop.pools` (`levelUp`/`chestT1`/`chestT2`/`chestT3`/`shop`/`reward`), `drop.luckFactor`, and `chestTiers`. Stats are sourced from the game's `LevelUpRewardWeighting` / `ChestRewardWeighting` / `CardRewardPool` + `ChestTier` pools (see `tools/dump-metadata.py` grep for `Rarity|Weight|ChestTier|Reward`). Example (demo-mod):

```ts
vc.cards.register({ id: "demo-mod:test_joker", rarity: "UltraRare", weight: 8, drop: { pools: ["chestT3"], weight: 8, luckFactor: 0.1 }, ... })
```

Loader mirrors these into `ModRegistry.RegisteredCards` for `cards` console view and will patch `ChooseCardFromRewardPool` / `ChestRewardWeighting` via Harmony once real game types are dump-bound.

## Launch with mods injected

```bash
surveil launch --dry-run                    # preview: steam -applaunch 3265700 (terminal stays, tails logs)
surveil launch                              # via Steam — terminal stays, streams BepInEx/Player.log (Ctrl+C stops tail, game stays)
surveil launch --direct --dry-run           # direct exe launch
surveil launch -- --mods-dir ~/.vc-mods     # forward extra args to game

# npx equivalents:
npx @vc-surveil/surveil launch --dry-run
npx @vc-surveil/surveil launch
```

Fallbacks (no devenv): `npm install && npm run build` still works; then `node packages/cli/dist/cli.js --help`.

## Publish (cli + lib — builds first)

```bash
# Devenv (no npm login check in dry-run):
surveil-publish --dry-run          # build + npm publish --dry-run for both
./publish.sh --dry-run             # same, without devenv

# Real publish (requires `npm login`):
surveil-publish                    # publishes @vc-surveil/api then @vc-surveil/surveil
./publish.sh --api-only            # only lib
./publish.sh --cli-only --tag=next # only CLI with custom tag
npm run publish:all                # via package.json scripts

## Custom maps, run toggles, mod settings

**Map packages** (`packages/api/src/index.ts:830 VcMaps` — `registerMap/patchMap/listMaps`):
- `vc.maps.registerMap({ id, biomeId, tileMaterials, rooms })`: `biomeId` is vanilla texture set (`madforest|inlaidlibrary|dairyplant|gallotower|capellamagna|moongolow|tinybridge`) or custom `BiomeDef.id`; `tileMaterials` maps tile keys to material names.
- `rooms: RoomMapDef[]`: `{ id, width, height, tiles, walls, props, lights, exits, encounterTable }`; `tiles` = `height` rows of `width` keys; `walls.tiles: {x,y,tile,walkable}[]`; `props: {id,type,x,y,destructible,hp,drops,interact}[]`; `lights: {id,x,y,radius,color,flicker}[]`; `exits: {id,x,y,toRoom,direction}[]`.
- Offline editor: open `tools/map-editor/index.html` (no build/server) — paint tiles/walls, place props/lights/exits, Resize/Add/Duplicate, Export/Import `map.json`.
- Example: `mods/demo-mod/assets/ember-depths-map.json` (mirrored in-code as `demo-mod:ember_depths_map`).

**Run toggles** (`packages/api/src/index.ts:633 VcRunToggles` — generic per-run flags):
- `vc.toggles.register({ id, description, defaultEnabled, conditions: { biomes, minFloor/maxFloor, requiresRelics, requiresFlags }, unlocksVia })` + `patch/list/isUnlocked/isEnabled/setEnabled/onChanged`.
- Gating: `vc.toggles.checkLevelEnter(biomeId, floor)` re-evaluates on entry; `vc.toggles.isActiveAtRuntime(id, biomeId, floor)` = enabled+unlocked+conditions-match.
- `inverse` is the legacy preset (`vc.inverse` mirrors toggle `inverse`; unlocked via `RelicDef.unlocksToggle`).
- Console (`` ` ``/F1): `toggle <id> [on|off|status|toggle]` (+ `inverse ...` alias).

**Mod settings tabs** (`packages/api/src/index.ts:916 VcSettings`):
- `vc.settings.register({ id, tab, kind: "toggle"|"choice"|"slider", default, min/max|choices, label, description })`; `setSetting(id, v)` / `getSetting(id)` / `listSettings(tab?)`.
- Demo: `demo-mod:damage_multiplier` slider 0.5–2 (`Demo` tab) + `demo-mod:hard_mode` toggle (default tab); declarative pending native menu wiring.

**Controller / accessibility:**
- In-game inverse toggle widget is keyboard-focusable (Tab/Enter/Space) with accessible name `Inverse mode toggle`; gamepad confirm flips it (`DevConsoleBehaviour.cs` overlay, no new input map).
- Map editor is keyboard/screen-reader usable: canvas `tabindex=0` (Arrows move, Space paints), toolbar/rooms use `role=toolbar/group`, every control has `aria-label` + shortcut hints (`P` export, `N` +Room).
```
