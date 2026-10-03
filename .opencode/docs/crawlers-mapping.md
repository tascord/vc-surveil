# Crawlers Mapping — VS vs Vampire Crawlers (Pancake)

Evidence base (2026-10-04, local install — no wiki involved):
- Game dir: `/home/flora/.local/share/Steam/steamapps/common/Vampire Crawlers/` (`GameAssembly.dll` 87 MB, `Vampire Crawlers_Data/`: `globalgamemanagers*`, `level0–3`, `sharedassets*`, `il2cpp_data/Metadata/global-metadata.dat`).
- `tools/dump-metadata.py` string dump of `global-metadata.dat` (namespace + member names only — no logic).
- Asset bundles: `StreamingAssets/aa/StandaloneWindows64/` (`biome-*`, `roomtemplates-*`, `tilemapmaterials_*`, `localization-string-tables-*`).
- `tools/catalog-inspect.py` (Addressables `catalog.bin` binary + `settings.json`).
- Workspace: `packages/api/src/index.ts` (`vc` namespaces), `.opencode/context.md`.

Game identity: internal namespace **`Nosebleed.Pancake`** ("Pancake" = Crawlers codename; matches `context.md`). Sub-namespaces: `GameConfig(.Relics/.ShopConfigs.Arcana+PowerUps)`, `GameLogic(.Controller/.DeckBox/.GameStates/.Weapons)`, `LevelSelect`, `Shops(.Arcana/.Blacksmith/.Crawlers/.PowerUps)`, `ProceduralGeneration(.Tags)`, `DungeonEnvironment`, `MetaProgression`, `SaveData`, `CardEffectBehaviours`, `Dialogue`, `CharacterSelection`.

## Distinctions (explicit non-equivalence)

| # | VS concept | Crawlers evidence | Verdict + confidence |
|---|---|---|---|
| 1 | Inverse mode (Gracia's Mirror, 180° map, scaling) | NO `Inverse*` gameplay strings; hits are Unity math (`InverseTransform*`, `inverseViewMatrix`) + unscoped `allowInverse`/`modInverse`. No `Gracia`/`Mirror`-relic/`VisuallyInvert` strings | **Absent/unverified — LOW** (do not port; implement mod-level) |
| 2 | Endless / Hurry modes | `EndlessModeActive`, `Set/ToggleEndlessMode`, `AllowsEndlessMode`, `IsThisDungeonEndless`, `EndlessModeStatConfig/Settings`, `HurryModeActive`, `Set/ToggleHurryMode` | **Present natively — HIGH** |
| 3 | Hyper mode | Only hyperlink/UI `Hyperlink*` hits; no mode strings | **Absent/unverified — LOW** |
| 4 | Stages (30-min arenas, per-stage modifiers) | `Dungeon*` dominates (`CurrentDungeon`, `GenerateDungeonFloor`, `ClearDungeonFloor`, `BeginDungeonMovement`, `Dungeon3DMapModel`, `CompleteDungeon`, dungeon achievements/selection). `Biome*` is only `BiomeName`/`BiomeEvents`/`BiomeEventSprite`. Bundles: `biome-madforest/-inlaidlibrary/-dairyplant/-gallotower/-capellamagna/-moongolowteaser/-tinybridge` + `biome-shared` | **Dungeons exist — HIGH**; biome↔dungeon equivalence **MEDIUM** (asset prefix `biome-` vs code `Dungeon*`; needs assembly check) |
| 5 | Weapons (6-slot, evolve w/ passive) | `GameLogic.Weapons`, `WeaponsVFX`, `FireWeapon(s)`, `GetPlayerEvolvableWeapons`, `GenerateWeaponEvoItems`, `DungeonCamWeapons`, `GunWeapon`/`BaseGunWeapon` | **Distinct weapon+evo system — HIGH**; VS 6-slot/evolve rules **LOW** |
| 6 | Cards (level-up draft pool) | `DeckBox`, `CardEffectBehaviours`, `CardGemsModel`, piles (`CardPile`, draw/discard), gem sockets (`AddGemToCard`, `GenerateWeaponEvoItems` adjacent), `CardEffectGemData` | **Cards distinct from weapons — HIGH** |
| 7 | Gems (passives in VS) | Gems socket **into cards** (`AddCardEffectGem`, `AddGemToCardAndCloseModal`, `CardEffectGemData/GemEffect`, evo insertion anims) | **Socketable card gems — HIGH** (NOT VS-style passive items) |
| 8 | Crawlers (≈ characters?) | `Shops.Crawlers`, `CrawlerItemView`, `CreateCrawlerCardMenu`, `CharacterCardTypeTrigger`, `CharacterSelectionScene`, `_pickACrawlerText`, `NumberRequiredCrawlersPurchased`, `CrawlerKeyword`, shop `SetCrawlers`/`GoToCrawler(s)` | **Purchasable/selectable entities tied to cards/shop — HIGH**; character-vs-summon-vs-deck-owner semantics **LOW** (M2-T0 open) |
| 9 | Arcana (tarot draft at 11:00/21:00) | `Nosebleed.Pancake.Arcana`, `Shops.Arcana`, `ShopConfigs.Arcana`, `TryApplyArcana_JestersHat` | **Arcana system likely — MEDIUM**; VS draft/boss timing **LOW** |
| 10 | Relics (map pickups → permanent unlocks) | `GameConfig.Relics`, `RelicConfig(Database/Multi)`, `RelicSettingsConfig`, `ActiveRelics`/`EnabledRelics`, `EquipRelic`/`IsRelicActive`, `OnRelicCollected/Toggled/DisplayedInModal`, `RelicShop`, `RelicsInLevel`, `DestructibleRelic(Container)`, `DEBUG_UnlockAndEquipRelic(s)` | **Relic discovery+equip+toggle natively plausible — MEDIUM-HIGH**; per-relic option sliders/in-run effects **LOW** (needs wiring check) |
| 11 | Enemies (minute waves, 300-cap) | `GenerateEncounter` (`SelectHigher/LowerEnemy`), `NukeDungeonEncounters`, `ClearDungeonEncountersExceptBosses`, floors/bosses encounter concepts | **Spawner exists — MEDIUM**; VS wave math **LOW** |
| 12 | Rooms/map-gen | `roomtemplates-simple/-unique(-shared)/-relics` bundles, `tilemapmaterials_*`, `ProceduralGeneration(.Tags)`, `DungeonEnvironment`, `ApplyDungeonLayers` | **Template-based dungeon gen — MEDIUM-HIGH** |
| 13 | Shops/Merchant (VS merchant sells arcana in Inverse) | `Shops.*`: `Arcana`, `Blacksmith`, `Crawlers`, `PowerUps` (+ `CharacterShop`, `RelicShop`, `JewellerRelic`, `BlacksmithShopConfig/Controller`, `PowerUpShop`, `CoinsSpentAtPowerupShop`) — a **multi-shop economy**, not one merchant | **Multi-shop — HIGH**; any Inverse-like shop extension **LOW** |
| 14 | Meta/progression/save | `MetaProgression`, `SaveData`, `RuntimeData`, `Model(s)`, `Metrics`, `Achievements`, `DEBUG_*` unlock/complete helpers | **Present — MEDIUM-HIGH** |

## What this means for the API (`packages/api/src/index.ts`)
- `WeaponDef` vs `CardDef` split: **justified** (finding 5+6). Keep `weaponId`/`evolutionFrom` links declarative.
- `CrawlerDef` (colors/trigger/baseStats): reasonable holder for finding 8; trigger-dispatch native wiring unverified — keep declarative (M2).
- `ArcanaDef.modifiers/hooks`, `RelicDef.options/unlocksToggle/effects`, `BiomeDef.encounterTable`/`RoomTemplate`, `EnemyDef.behavior/stats/drops`, `VcInverse`/`VcRunModes`/`VcState.isInverse/getRunFlags`: all correctly marked declarative; loader must store + round-trip without claiming native effects.
- `vc.relics.setOption/getOption` + `relicOptionChanged`, `vc.inverse.setEnabled/onToggle` + `inverseToggled`: no VS counterpart (VS relics are flag unlocks; VS Inverse toggles pre-run in Stage Select). These are **new mod capabilities** — document as such in M8 demos.

## Unknowns (need assembly/level-select UI work, NOT more wiki)
1. Full `RelicConfig`/`RelicSettingsConfig` fields (options? toggles? stat effects?) — inspect game assemblies (M4-T0/M7-T0).
2. `EndlessModeStatConfig` contents + `ToggleEndlessMode`/`ToggleHurryMode` call sites (UI vs debug) — M7-T2.
3. `LevelSelect` dungeon list + `CurrentDungeonSetting`/`AllowedDungeonTags` (dungeon ids for `foundIn`/`biomeId`) — M5-T0.
4. Crawler purchase/select flow (`Shops.Crawlers` ↔ `CharacterSelectionScene` ↔ `CreateCrawlerCardMenu`) and `CharacterCardTypeTrigger` semantics — M2-T0.
5. Arcana draft/activation path (`Shops.Arcana` vs run draft) — M3-T0.
6. Encounter-table shape behind `GenerateEncounter` (weights? floors? biomes?) — M6-T0.
7. Whether any relic-gated difficulty/layout toggle exists under a non-"Inverse" name.
8. Localization string tables are inside binary bundles (`localization-string-tables-english(en)_assets_all.bundle`) — need bundle extraction for display names/descriptions.
