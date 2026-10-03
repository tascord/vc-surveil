# Work Log — vc-surveil

## Active Sessions

| Session | Date (UTC) | Focus | Status |
|---|---|---|---|
| plan-mod-api-expansion | 2026-10-03 | Read `context.md`, `packages/api/src/index.ts`, `packages/loader/Shared.cs`, `mods/demo-mod/src/index.ts`; wrote hierarchical M/T/S plan (`todo.md`) + this log | Done (plan-only, no source edits) |
| audit-loader-coverage | 2026-10-03 | Read-only audit: `Shared/CardBridge/CardPatches/EnemyBridge/EnemyPatches/AudioBridge/DevConsole/DevConsoleBehaviour/Plugin.BepInEx/Plugin/SpriteLoader/ModSpriteAnimator/Arcana*.bak` + `api/src/index.ts` + `registry.ts`; ran `dump-metadata.py` + `catalog-inspect.py`; wrote `.opencode/docs/loader-audit.md` (no source edits) | Done |

## File Status

| File | Status | Notes |
|---|---|---|
| `.opencode/context.md` | Read | Env/conventions/parity constraints; Inverse starter scenario |
| `packages/api/src/index.ts` | Read → Extended (M1-T1,M2-T1,M3-T1,M4-T1,M5-T1,M6-T1,M7-T1 API) | Declared `CardDef`/`GemDef`/`ArcanaDef`/`RelicDef`/`EnemyDef`/`BiomeDef`/`Vc*` — cards rich, relic/arcana/biome/enemy minimal → now: WeaponDef/VcWeapons, CrawlerDef/VcCrawlers, ArcanaDef+/RelicDef+/RelicOptionDef, BiomeDef+/RoomTemplate+, EnemyDef+, VcGems+/VcEvolutions+, VcInverse/VcRunModes, VcState+/VcHooks+ (additive only; tsc clean, build:api green) |
| `packages/api/src/registry.ts` | Listed for inspect | Local shim, needs parity with every new namespace |
| `packages/loader/Shared.cs` | Read | Live Jint `vc` global: cards/arcanas(parser-only)/enemies/audio/console/hooks; `patch`/`list`/gems/relics/evolutions/dungeons/state missing or stubbed; regex fallback card parser |
| `mods/demo-mod/src/index.ts` | Read | 6 cards (targeting/effects), local shim, `hooks.on`, console `demo`/`give-demo`; baseline for M8 showcase |
| `packages/loader/CardBridge.cs` | Listed for inspect | `CustomCardDef` + execution wiring |
| `packages/loader/CardPatches.cs` | Listed for inspect | Harmony anchor for card/weapon/crawler effects |
| `packages/loader/EnemyBridge.cs` + `EnemyPatches.cs` | Listed for inspect | Partial per context — spawn/config/stat placeholders |
| `packages/loader/AudioBridge.cs` | Listed for inspect | Reference complete bridge |
| `packages/loader/DevConsole.cs` + `DevConsoleBehaviour.cs` | Listed for inspect | Console + overlay surface for Inverse toggle (M7-T2) |
| `packages/loader/Plugin.cs` + `Plugin.BepInEx.cs` | Listed for inspect | Lifecycle/discovery |
| `packages/loader/SpriteLoader.cs` + `ModSpriteAnimator.cs` | Listed for inspect | Sprite pipeline for cards/enemies/arcanas |
| `mods/demo-mod/mod.json` | Listed for inspect | Manifest/entry contract |
| `packages/cli/src/cli.ts` | Listed for inspect | `surveil install` path |
| `tools/dump-metadata.py` + `tools/catalog-inspect.py` | Listed for inspect | Assembly verification tooling (M3-T0/M5-T0/M7-T0) |
| `.opencode/docs/loader-audit.md` | Created (audit) | Jint-vs-VcApi coverage, stubs, fallback/enemy/arcana gaps, console/Inverse points, sprite pipeline, Harmony anchors + tool output |
| `.opencode/todo.md` | Created | Strict M1–M9 / T / S plan + manifest + parallel groups G-A…G-E |
| `.opencode/work-log.md` | Created (this file) | Canonical log — update per session |

## Pending Integration

- [x] G-A API types (index.ts only): M1-T1 (WeaponDef/VcWeapons + CardDef.weaponId/evolution),
  M2-T1 (CrawlerDef/VcCrawlers + CardDef.crawlerId/crawlerTrigger link),
  M3-T1 (ArcanaDef modifiers/hooks/rarity/unlockId + VcArcanas patch/list),
  M4-T1 (RelicDef icon/rarity/options/defaultOptions/effects/unlocksToggle + RelicOptionDef + VcRelics get/set options),
  M5-T1 (BiomeDef background/music/encounterTable/bossFloor/modifiers + RoomTemplate biomeId/minFloor/maxFloor/enemies/reward + VcDungeons full CRUD/list),
  M6-T1 (EnemyDef behavior/stats/drops + VcEnemies patch/list; VcGems patch/list; VcEvolutions list),
  M7-T1 (VcInverse/VcRunModes + VcState.isInverse/getRunFlags + VcHooks relicOptionChanged/inverseToggled)
  — done in `packages/api/src/index.ts` (tsc --noEmit clean, build:api green, lsp clean).
  Backward compatible: all pre-existing exports/signatures unchanged (additive only).
  Native wiring throughout marked declarative pending assembly verification.
  Still pending: registry.ts parity shim, loader bridges, demo showcase, G-E verification.

- [x] G-C research (2026-10-04, general researcher): Inverse semantics (M7-T0) + crawler/arcana/biome Crawlers-vs-VS semantics (M2-T0/M3-T0/M5-T0) — `.opencode/docs/vs-inverse.md`, `.opencode/docs/vs-content.md`, `.opencode/docs/crawlers-mapping.md`. No source edits.
  - VS Inverse (HIGH): +200% gold, +20% luck, enemies +200% HP then +5% HP +0.5 speed/min; 180° rotated darker map ("Visually Invert Stages" opt-out); merchant adds Skips/Banishes/Rerolls + 20k Arcana; unlocked by Gracia's Mirror via Eudaimonia Machine "Too Easy" (spell "freezearrow" alt, MEDIUM); pre-run per-stage toggle in Stage Select, combinable with Hyper/Hurry/Endless + Arcanas/Limit Break/Randomizers. No in-run Inverse switch in VS.
  - VS content (HIGH model / MEDIUM roster): 14-row relic unlock table; 27 stages; weapon 6-slot unique + evolve-with-passive model (100+ roster via Category:Weapons); arcana 3-draft (start + 11:00/21:00 bosses; Inverse-merchant extra; Queen Sigma/Moonlight Bolero extras); enemy minute-waves/300-cap/bosses/Luck-scaled map events.
  - Crawlers mapping (local global-metadata.dat + bundles): Endless + Hurry flags native (HIGH); Dungeons + biome-* bundles + roomtemplates (HIGH/MEDIUM); weapons≠cards, gems socket into cards (HIGH); multi-shop economy incl. Crawlers/Relic/Arcana/Blacksmith/PowerUps (HIGH); relic discovery+equip+toggle plausible (MEDIUM-HIGH). Inverse/Hyper/Gracia absent (LOW — mod-level feature only). Crawler/arcana-draft/encounter-table semantics open (LOW). Unknowns listed in crawlers-mapping.md.
  - Sources: vampire-survivors.fandom.com/api.php wikitext (HTML 403/401-blocked; Inverse_mode/Endless_mode redirect to Stages sections); local game dir + tools/dump-metadata.py. VS≠Crawlers throughout; per-claim confidence tags.
- [ ] G-A API types: M1-T1 (weapons), M2-T1 (crawlers), M3-T1 (arcana+), M4-T1 (relics+options), M5-T1 (biomes/rooms+), M6-T1 (enemies+), M7-T1 (Inverse/run-mode/state).
- [ ] G-B loader bridges: `Weapon/Crawler/Arcana/Relic/DungeonBridge.cs` + `InverseToggle.cs`, `Shared.cs` Jint globals, `patch`/`list`/`hooks.on` completion, fallback-parser extension.
- [ ] G-D demo showcase: M8-T0 scaffold, M8-T1…T7 per-namespace demos + Inverse relic, M8-T8 shim/docs.
- [ ] G-E verification: `npm run build` / `build:api` / `build:demo` / `dotnet build`, `npm run typecheck`, `npm run lint`, registry-shim round-trip + Jint contract harness (M9-T3), native-claim audit (M9-T4).
- [ ] Docs: `README.md` / `pickup.md` updates only after G-E green.

## Implementation sessions (2026-10-04, via general subagents)

| Session | Focus | Status |
|---|---|---|
| api-index-extended | `packages/api/src/index.ts`: WeaponDef, CrawlerDef, ArcanaDef+, RelicDef+/RelicOptionDef, BiomeDef+/RoomTemplate+, EnemyDef+, VcGems+/VcEvolutions+, VcInverse/VcRunModes, VcState+, HookEvent | Done (tsc+build:api green) |
| api-settings | `VcSettings`/`SettingDef`, `VcApi.settings` | Done |
| api-toggles | `RunToggleDef`/`VcRunToggles`, `VcApi.toggles?`, VcInverse legacy docs | Done |
| api-maps | `MapTile/WallDef/PropPlacement/LightSourceDef/MapExit/RoomMapDef/MapPackageDef`, `VcMaps`, `VcApi.maps?` | Done |
| registry-parity | Full `createLocalVc` parity incl. settings/toggles/maps/inverse delegation | Done (smoke-tested) |
| bridges | New: `WeaponBridge`, `CrawlerBridge`, `RelicBridge`, `DungeonBridge`, `MapBridge`, `RunToggleBridge`, `ArcanaBridge`, `InverseToggle` (compat), `RunStateStore` (save/load) | Done |
| shared-wiring | `Shared.cs`: all `__register*` + `vc.*` globals, hooks store + FireHook, patch/list, fallback router, dungeonEnter→CheckLevelEnter, beforeSave/afterLoad persistence | Done (build delta +0) |
| console-ui | `inverse` + generic `toggle` commands, overlay widget (focusable, gamepad confirm) | Done (stub-compile 0 errors) |
| demo-showcase | All namespaces + options/toggles/inverse/maps/settings, contract harness green | Done |
| map-editor | `tools/map-editor/index.html` offline editor, API-aligned export | Done |
| docs | README maps/toggles/settings/a11y section | Done |
| verification | build/typecheck/lint/demo/harness all exit 0; dotnet blocked (no interop) | Done with noted exception |
