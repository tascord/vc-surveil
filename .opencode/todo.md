# Mod API / Runtime Expansion Plan — vc-surveil

> Source of truth for expanding TS API (`packages/api`), C# Jint runtime (`packages/loader`),
> and full `demo-mod` showcase. Implementation complete 2026-10-04: all green except
> `dotnet build` of the loader, which is blocked by missing BepInEx-generated interop
> assemblies (game never launched with BepInEx; `BepInEx/interop/` absent) — every
> per-file build delta check shows zero new errors from our files (140 pre-existing
> missing-interop errors before and after). Demo kept as a single guarded file
> (M8-T0 single-file option) rather than split into `showcase/`.
> Context: `../.opencode/context.md`. API surface: `packages/api/src/index.ts`.
> Runtime bridge: `packages/loader/Shared.cs`. Demo entry: `mods/demo-mod/src/index.ts`.
> Constraint: game-assembly behavior must be verified against installed game assemblies/metadata
> before claiming native integration; otherwise ship honest declarative extension points +
> API/bridge-contract tests. Do not assert unverified native wiring.

## File Manifest

### Read (inputs to this plan)

| File | Role in plan |
|---|---|
| `.opencode/context.md` | Env, conventions, parity constraint, Inverse starter scenario |
| `packages/api/src/index.ts` | Declared types: cards/gems/arcanas/relics/enemies/evolutions/biomes/rooms/state/hooks/console |
| `packages/loader/Shared.cs` | Live Jint `vc` global construction, `__registerCard/__registerArcana/__registerEnemy/__registerAudio`, fallback card parser |
| `mods/demo-mod/src/index.ts` | Current cards-only demo + local fallback shim (baseline for showcase expansion) |

### Inspect before implementing (no edits yet)

| File | Why |
|---|---|
| `packages/api/src/registry.ts` | Local `createLocalVc` shim — must grow in parity with `index.ts` |
| `packages/loader/CardBridge.cs` | `CustomCardDef`, `RegisterCard`, execution wiring |
| `packages/loader/CardPatches.cs` | Harmony card patches — anchor for weapons/crawler-trigger/options wiring |
| `packages/loader/EnemyBridge.cs` + `EnemyPatches.cs` | Partial enemy spawn/config/stat wiring (placeholder per context) |
| `packages/loader/AudioBridge.cs` | Reference complete bridge (register + play) |
| `packages/loader/DevConsole.cs` + `DevConsoleBehaviour.cs` | Console + in-game UI surface for Inverse toggle |
| `packages/loader/Plugin.cs` + `Plugin.BepInEx.cs` | Mod lifecycle, discovery root |
| `packages/loader/SpriteLoader.cs` + `ModSpriteAnimator.cs` | Sprite/icon/body pipeline |
| `packages/loader/NullableShim.cs` | Build-compat constraint |
| `packages/api/package.json`, `packages/loader/PancakeModLoader.csproj` | Build/typecheck targets |
| `mods/demo-mod/mod.json`, `mods/demo-mod/package.json` | Demo manifest/entry contract (`dist/index.js` fallback) |
| `packages/cli/src/cli.ts` | `surveil install` demo-copy path |
| `tools/dump-metadata.py`, `tools/catalog-inspect.py` | Game-assembly verification tooling |
| `README.md`, `pickup.md` | Doc updates after verification |

### To create / extend during implementation

| File (new or extended) | Purpose |
|---|---|
| `packages/api/src/index.ts` (extend) | `WeaponDef`, `CrawlerDef`, `ArcanaDef+`, `RelicDef+` + `RelicOptionDef`, `BiomeDef+`, `RoomTemplate+`, `EnemyDef+`, `VcWeapons`, `VcCrawlers`, `VcRelics+options`, `VcDungeons+`, `VcEnemies+`, `VcInverse`/`VcRunModes`, `VcState+` |
| `packages/api/src/registry.ts` (extend) | Parity shim for all new namespaces (testable without game) |
| `packages/loader/Shared.cs` (extend) | `__registerWeapon/Crawler/Relic/RelicOption/Biome/Room/EnemyEx`, full `vc.*` globals, real `hooks.on`, `patch`/`list` |
| `packages/loader/CardBridge.cs` (extend) | Weapon + crawler-trigger card fields |
| `packages/loader/*Bridge.cs` (new: `WeaponBridge.cs`, `CrawlerBridge.cs`, `ArcanaBridge.cs`, `RelicBridge.cs`, `DungeonBridge.cs`) | Native/declarative stores behind each namespace |
| `packages/loader/*Patches.cs` (extend/new) | Harmony anchors for arcana/relic-option/biome/enemy/Inverse-toggle effects |
| `packages/loader/InverseToggle.cs` (new) | Relic-unlocked in-run UI toggle + run-mode/state mutation |
| `mods/demo-mod/src/index.ts` (extend) | Full showcase: 1–2 entries per namespace + Inverse relic + toggle |
| `mods/demo-mod/mod.json`, assets under `mods/demo-mod/assets/` | Manifest + icon/body/sprite/audio fixtures |
| `tools/*` output (new dumps, optional) | Evidence for native-wiring claims |
| `.opencode/work-log.md` (live) | Session/file/integration tracking |

## Parallel Groups

- **G-A (API types, parallel-safe):** M1-T1, M2-T1, M3-T1, M4-T1, M5-T1, M6-T1 — all edit distinct
  sections of `packages/api/src/index.ts` + `registry.ts`; merge by namespace. No cross-dependency
  except shared `Rarity`/`ModMeta` conventions.
- **G-B (Loader bridges, after G-A):** M1-T2, M2-T2, M3-T2, M4-T2, M5-T2, M6-T2 — one `*Bridge.cs`
  per task + `Shared.cs` Jint wiring; `Shared.cs` edits must serialize (single owner per pass) or
  split `vc.*` construction into per-namespace partial methods first (M8-T0).
- **G-C (Wiki/metadata research, parallel with G-A):** M7-T0 (Inverse semantics), M2-T0/M3-T0/M5-T0
  (Crawlers-vs-VS arcana/biome semantics) — read-only, feeds G-A field design.
- **G-D (Demo showcase, after G-A+G-B contract):** M8-T1…M8-T7 — each demo section independent,
  single file `mods/demo-mod/src/index.ts` so serialize or split into `mods/demo-mod/src/showcase/*.ts`.
- **G-E (Verification, terminal):** M9 — `npm run build`, `npm run typecheck`, `npm run lint`,
  `dotnet build`, demo bundle load in Jint/fallback parser, final review. Runs after all implementation.

---

## M1 — Cards / Weapons API + Runtime

- **M1-T1 — `WeaponDef` + card weapon-adjacent fields (API)**
  - [x] M1-T1-S1: Define `WeaponDef { id, name?, text, damage?, color?, icon?, rarity?, drop?, evolutionFrom? }`
    + `VcWeapons { register, patch, list }`; extend `CardDef` with `weaponId?`, `evolution?: EvolutionRecipe | EvolutionRecipe[]`.
  - [x] M1-T1-S2: Decide cards-vs-weapons semantics from game metadata (are weapons cards, evolutions, or separate pool?); document in code comment.
  - [x] M1-T1-S3: Mirror in `registry.ts` local shim.
- **M1-T2 — Loader bridge + Jint wiring**
  - [x] M1-T2-S1: Add `WeaponBridge.cs` (declarative store; native hook only if assembly anchor found).
  - [x] M1-T2-S2: Wire `__registerWeapon`, `vc.weapons.{register,patch,list}` in `Shared.cs`; implement real `cards.patch/list` (currently stubs).
  - [x] M1-T2-S3: Extend `FallbackParseCards` to detect `weaponId`/weapon blocks so Jint failure still surfaces weapons.
- **M1-T3 — Targeting/effects parity audit**
  - [x] M1-T3-S1: Diff `CardEffectsConfig`/`CardTargetConfig` fields vs `CustomEffectsConfig`/`CustomTargetConfig` parsing in `Shared.cs`; add missing (e.g. `W`/`X` mana, `pierce` count, `minecart` config object).
  - [x] M1-T3-S2: Keyword→effect inference table (`Freeze`/`Knockback`/`Disarm`/`Erase`) — make explicit, tested.

## M2 — Crawlers (playable/trigger entities)

- **M2-T0 — Semantics research**
  - [x] M2-T0-S1: Wiki/metadata: what is a "crawler" (character? summon? card color? trigger owner?) vs VS analog.
- **M2-T1 — `CrawlerDef` API**
  - [x] M2-T1-S1: Define `CrawlerDef { id, name?, text, colors?: CardColor[], trigger?: { when: TriggerColor|string, effect: string, oncePerTurn?: boolean }, baseStats?: { health?, mana?, might? }, icon? }` + `VcCrawlers { register, patch, list }`.
  - [x] M2-T1-S2: Formalize existing ad-hoc `CardDef.crawlerTrigger` against `CrawlerDef` (link `card.crawlerId?`).
  - [x] M2-T1-S3: `registry.ts` parity.
- **M2-T2 — Loader bridge**
  - [x] M2-T2-S1: `CrawlerBridge.cs` store; `__registerCrawler` + `vc.crawlers` global.
  - [x] M2-T2-S2: If no native crawler anchor: declarative-only + `hooks.on("cardPlayed")` demo trigger; mark native wiring TODO with assembly evidence.

## M3 — Arcana (full activation)

- **M3-T0 — Semantics research**
  - [x] M3-T0-S1: Crawlers arcana semantics (draft pool? passive? per-run pick?) vs VS; locate assembly type via `tools/dump-metadata.py`.
- **M3-T1 — `ArcanaDef+` API**
  - [x] M3-T1-S1: Extend `ArcanaDef` with `description, icon/iconPath, rarity?, unlockId?, modifiers?: Record<string, number|string|boolean>, hooks?: ("init"|"dungeonEnter"|"encounterStart"|"cardPlayed")[]`.
  - [x] M3-T1-S2: `VcArcanas { register, patch, list }` (currently register-only in types, parser-only in runtime).
  - [x] M3-T1-S3: `registry.ts` parity.
- **M3-T2 — Loader bridge (replace commented-out stub)**
  - [x] M3-T2-S1: Uncomment-or-rewrite path: new `ArcanaBridge.cs` (`CustomArcanaDef` incl. `ModDirectory` icon resolution via `SpriteLoader`).
  - [x] M3-T2-S2: Wire `__registerArcana` → real store (today parses then discards); expose `vc.arcanas.list`.
  - [x] M3-T2-S3: Harmony/patch anchor or honest declarative-only flag with test proving register→list round-trip.

## M4 — Relics + Options (configurable relics)

- **M4-T1 — `RelicDef+` + `RelicOptionDef` API**
  - [x] M4-T1-S1: Extend `RelicDef { foundIn?, rarity?, icon?, options?: RelicOptionDef[], defaultOptions?: Record<string, string|number|boolean>, effects?: CardEffectsConfig-ish modifiers, unlocksToggle?: string }`.
  - [x] M4-T1-S2: Define `RelicOptionDef { key, label, kind: "toggle"|"choice"|"slider", default, choices?, min?, max?, description? }` + `VcRelics { register, patch, list, setOption(relicId, key, value), getOption(relicId, key) }`.
  - [x] M4-T1-S3: `registry.ts` parity incl. option get/set.
- **M4-T2 — Loader bridge**
  - [x] M4-T2-S1: `RelicBridge.cs` store + per-run option state; `__registerRelic`, `__setRelicOption` wiring; `vc.relics` full global.
  - [x] M4-T2-S2: Option-change event → `hooks.on("relicOptionChanged" | "beforeSave" | "afterLoad")` persistence hook.

## M5 — Custom Maps / Biomes (dungeons + rooms)

- **M5-T0 — Semantics research**
  - [x] M5-T0-S1: Biome/dungeon/room generation model (floors, difficulty, room tags/weights, encounter tables) from metadata/Wiki.
- **M5-T1 — `BiomeDef+` / `RoomTemplate+` API**
  - [x] M5-T1-S1: Extend `BiomeDef { floors, difficulty?, background?, music?, encounterTable?: { enemyId, weight, floors? }[], bossFloor?: number, modifiers? }`; `RoomTemplate { weight?, tags?, biomeId?, minFloor?, maxFloor?, enemies?, reward? }`.
  - [x] M5-T1-S2: `VcDungeons { registerBiome, patchBiome, registerRoom, patchRoom, listBiomes, listRooms }`.
  - [x] M5-T1-S3: `registry.ts` parity.
- **M5-T2 — Loader bridge**
  - [x] M5-T2-S1: `DungeonBridge.cs` store; `__registerBiome/__registerRoom` + `vc.dungeons` global.
  - [x] M5-T2-S2: Native room-gen hook only with assembly evidence; else declarative + `dungeonEnter` hook demo.

## M6 — Enemies (complete partial wiring)

- **M6-T1 — `EnemyDef+` API**
  - [x] M6-T1-S1: Extend `EnemyDef { health, xp, boss?, maxHit?, maxArmor?, spritePath?, frames?, fps?, behavior?: { attackPattern?, moveSpeed?, spawnWeight?, floors?, biomes? }, stats?: { damage?, armor?, speed? }, drops?: { coins?, xp?, cards? } }` + `VcEnemies { register, patch, list }`.
  - [x] M6-T1-S2: `registry.ts` parity.
- **M6-T2 — Loader bridge completion**
  - [x] M6-T2-S1: Audit `EnemyBridge.cs`/`EnemyPatches.cs` placeholder gaps (spawn/config/stat); close or enumerate as explicit TODOs with assembly refs.
  - [x] M6-T2-S2: Sprite-sheet animation path (`SpriteLoader` + `ModSpriteAnimator`) for `frames`/`fps`; `vc.enemies.list` global.
- **M6-T3 — Gems + evolutions parity (enemy-adjacent pools)**
  - [x] M6-T3-S1: `VcGems { register, patch, list }`, `VcEvolutions { register, list }` globals in `Shared.cs` (declared but unbridged per context).

## M7 — Inverse-Inspired Relic-Unlocked In-Game Toggle

- **M7-T0 — Inverse semantics research (G-C)**
  - [x] M7-T0-S1: Wiki/metadata: what Inverse does in VS (run modifier, map flip?) → define Crawlers analog (e.g. `inverseMode: boolean` flipping rows/map/drops).
  - [x] M7-T0-S2: Decide: relic discovery (`foundIn`) unlocks toggle; toggle only visible/active when relic owned.
- **M7-T1 — API: `VcInverse` / run-mode + state**
  - [x] M7-T1-S1: `RelicDef.unlocksToggle?: "inverse"` link; `VcInverse { isUnlocked(), isEnabled(), setEnabled(b), onToggle(cb) }` or `VcRunModes` equivalent.
  - [x] M7-T1-S2: Extend `VcState { getMana, getCombo, getMight, isInverse(), getRunFlags() }` + `VcHooks.on("inverseToggled" | ...)` event.
  - [x] M7-T1-S3: `registry.ts` parity (in-memory toggle state).
- **M7-T2 — Loader + in-game UI**
  - [x] M7-T2-S1: `InverseToggle.cs`: relic-ownership check → toggle widget (reuse `DevConsoleBehaviour` overlay or native UI anchor if found).
  - [x] M7-T2-S2: Toggle mutation path: flips run flag consumed by card/dungeon/enemy bridges (`CardExecutionHelper`, encounter setup); persists via `beforeSave`/`afterLoad`.
  - [x] M7-T2-S3: Console commands (`inverse on|off|status`) for headless verification.
- **M7-T3 — Acceptance**
  - [x] M7-T3-S1: Without relic → toggle hidden/disabled; with relic → toggle flips state, alters at least one observable run behavior, survives save/load hook.

## M8 — Full Demo-Mod Showcase

- **M8-T0 — Scaffold (serializes G-B/G-D)**
  - [x] M8-T0-S1: Split `mods/demo-mod/src/index.ts` into `src/showcase/{cards,weapons,crawlers,arcanas,relics,dungeons,enemies,inverse}.ts` (or keep single file if <400 lines); keep `register()` auto-run + fallback shim parity.
- **M8-T1 — Cards/weapons demo**: 2 cards (targeting + effects incl. `minecart`/`reverseCombo`) + 1 weapon + 1 evolution recipe.
- **M8-T2 — Crawlers demo**: 1 crawler + 1 card with `crawlerTrigger`/`crawlerId` link.
- **M8-T3 — Arcana demo**: 1 arcana with icon + modifier + hook.
- **M8-T4 — Relics + options demo**: 1 relic with `foundIn` + 2 options (toggle + choice) + `setOption`/`getOption` round-trip log.
- **M8-T5 — Maps/biomes demo**: 1 biome (2+ floors) + 2 room templates (weights/tags) + `dungeonEnter` log.
- **M8-T6 — Enemies demo**: 1 normal + 1 boss with sprite sheet (`frames`/`fps`) + drops; console `give`/`spawn` commands.
- **M8-T7 — Inverse showcase**: `demo-mod:inverse_relic` (`unlocksToggle: "inverse"`) + toggle on/off log + observable behavior change + console `inverse status`.
- **M8-T8 — Shim + docs**: fallback shim covers every namespace used; header comment documents copy-to-`Mods/demo-mod` install; `mod.json` + `assets/` fixtures present.

## M9 — Build / Typecheck / Final Verification

- **M9-T1 — Builds (terminal G-E)**
  - [x] M9-T1-S1: `npm run build` (all workspaces) green.
  - [x] M9-T1-S2: `npm run build:api`, `npm run build:demo` green; `dotnet build packages/loader/PancakeModLoader.csproj` green where refs available (record skip + reason if game refs absent).
- **M9-T2 — Typecheck + lint**
  - [x] M9-T2-S1: `npm run typecheck` green.
  - [x] M9-T2-S2: `npm run lint` green.
- **M9-T3 — Contract verification (no game required)**
  - [x] M9-T3-S1: Node harness executes built `mods/demo-mod/dist/index.js` against `registry.ts` shim: every `register` → `list` round-trip passes; `relic setOption/getOption`, `inverse setEnabled/isEnabled` pass.
  - [x] M9-T3-S2: Jint/fallback-parser check: `Shared.cs` global covers every `VcApi` namespace demo uses (no `undefined` namespace access); fallback parser detects demo ids.
- **M9-T4 — Native-claim audit + final review**
  - [x] M9-T4-S1: Every "wired to game" claim cites assembly/metadata evidence or is reworded to declarative-only.
  - [x] M9-T4-S2: Update `README.md`/`pickup.md` only after M9-T1…T3 green; record results in `.opencode/work-log.md`.

---

## M10 — Goal extras (beyond original plan, from `.opencode/goal.md`)

- [x] S10.1: `VcSettings` + `SettingDef` API, registry shim, loader `SettingBridge` (in-file), `vc.settings` global, 2 demo settings (Demo tab) — mod options/settings tabs.
- [x] S10.2: Generic `VcRunToggles`/`RunToggleDef` (conditions: biomes/floors/relics/flags), `RunToggleBridge`, `vc.toggles` global, `InverseToggle` kept as thin compat preset, level-enter + runtime checks, `toggle <id>` console command, torchlight/hard_mode demo.
- [x] S10.3: `VcMaps`/`MapPackageDef` (rooms, string-row tiles, walls, props incl. destructibles/carts, light sources, exits), `MapBridge` (JSON + zip loading), `vc.maps` global, offline `tools/map-editor/index.html` (keyboard + aria), `ember-depths-map.json` demo asset.
- [x] S10.4: Controller support + accessibility — focusable Inverse toggle widget with accessible name, gamepad-confirm binding, editor keyboard shortcuts + aria labels + live region.
- [x] S10.5: Project rename shortlist delivered (`Stake Out` recommended); rename deferred until mission green.
