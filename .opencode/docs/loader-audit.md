# Loader Audit — Jint `vc` global vs `VcApi`, stubs, gaps, anchors

Date (UTC): 2026-10-03. Scope: read-only audit of `packages/loader/*` + `packages/api/src/index.ts`
(`VcApi`) + `tools/dump-metadata.py` / `tools/catalog-inspect.py` output.
No source edits made. Follow-ups belong to the G-A/G-B/G-C plan in `.opencode/todo.md`.

Files read (line refs below are to current HEAD):
`Shared.cs` (656), `CardBridge.cs` (306), `CardPatches.cs` (1123), `EnemyBridge.cs` (123),
`EnemyPatches.cs` (35), `AudioBridge.cs` (97), `DevConsole.cs` (361),
`DevConsoleBehaviour.cs` (170), `Plugin.BepInEx.cs` (117), `Plugin.cs` (81),
`SpriteLoader.cs` (181), `ModSpriteAnimator.cs` (76),
`ArcanaBridge.cs.bak` (141), `ArcanaPatches.cs.bak` (218).

## 1. Jint `vc` global coverage vs `VcApi` (`packages/api/src/index.ts` L255-325)

`JsRuntime.LoadMod` builds the global at `Shared.cs` L131-162. Per-namespace status:

| `VcApi` namespace | Jint `vc.*` today | JS→C# entry | Status |
|---|---|---|---|
| `meta` | absent | — | MISSING (shim `registry.ts` fakes `{id:"local"}`; loader never sets per-mod meta) |
| `cards.register` | `__registerCard` | `RegisterCardFromJs` L176-465 (full field parse + `onPlay` closure) | LIVE |
| `cards.patch` | `() => {}` L140 | none | STUB (no-op; `VcCards.patch` contract unimplemented) |
| `cards.list` | `() => []` L141 | none (C# side has `CardBridge.RegisteredDefs` + `ModRegistry.RegisteredCards`, never exposed) | STUB (always empty) |
| `gems` | absent | — | MISSING (`GemDef`/`VcGems` have no loader counterpart at all) |
| `arcanas.register` | `__registerArcana` | `RegisterArcanaFromJs` L467-507 parses then **discards** (see §4) | PARSE-ONLY / NO-OP |
| `relics` | absent | — | MISSING (no `RelicBridge`, no console cmd) |
| `enemies.register` | `__registerEnemy` | `RegisterEnemyFromJs` L585-622 → `EnemyBridge` placeholder (see §5) | PARTIAL |
| `audio.register/play` | `__registerAudio`/`__playAudio` | `RegisterAudioFromJs` L624-639 → `AudioBridge` | COMPLETE (reference bridge) |
| `evolutions` | absent | — | MISSING |
| `dungeons` (`registerBiome`/`registerRoom`) | absent | — | MISSING |
| `state` (`getMana/getCombo/getMight`) | absent | — | MISSING (closest is `onPlay` ctx fields `playerMana`/`combo`, L416-417) |
| `hooks.on` | `(ev,cb) => __log(...)` L154 | none stored/invoked | STUB (log-only; no registry, no fire sites) |
| `console.register` | `__consoleRegister` L112-129 | `DevConsole.Register(name,desc,handler)`; ctx = `{log}` only | PARTIAL (3-arg form only; `DevConsoleCommand` object form, `api` in ctx, `clear/open/close/toggle/isOpen` all missing) |
| `console.log` / `vc.log` | `__log` | `LoaderLog.Info` | LIVE |
| `audio` ctx detail | — | `AudioBridge.PlayAudio`: cached clip → temp `AudioSource`, else `MasterAudio.PlaySound(id)` passthrough | LIVE w/ vanilla fallback |

`registry.ts` (local shim, 42 lines) is *ahead* of the loader on `cards.patch`/`list`
(working local impls) but *behind-or-equal* elsewhere (`arcanas/relics/enemies/evolutions`
no-op, no `audio.play`, no `state` beyond zeros, `hooks.on` no-op). Any new namespace must
be added in **three** places: `index.ts` types → `Shared.cs` Jint global → `registry.ts`
shim (M8-T8/M9-T3 round-trip depends on this).

Other `Shared.cs` notes:
- `export` stripping is a single regex (`\bexport\s+`, L164) — `export default`, `import`
  statements, TS syntax, and multi-export bundles will throw inside Jint and land in the
  fallback parser (§3). No module loader / multi-file support.
- One `new Jint.Engine()` per mod (L102); `engine` is captured by `onPlay` closures
  (L411-462) — per-mod isolation is incidental (separate engines) but `CardBridge`/
  `EnemyBridge` registries are process-global with last-write-wins dedup.
- `ModRegistry` (L642-648) records loaded ids + card `(id, rarity, weight, pools)` but is
  never surfaced to JS (`list` stub) or console beyond `mods`/`cards` commands.

## 2. `patch` / `list` / `hooks` stubs (exact locations)

- `vc.cards.patch: () => {}` — `Shared.cs` L140. No `__patchCard` host function exists.
  `CardBridge` has `RegisteredDefs` + `FindDef`/`FindCard`, so patch is implementable as
  mutate-`CustomCardDef`-then-flag-recreate; today nothing does.
- `vc.cards.list: () => []` — `Shared.cs` L141. Data available (`RegisteredDefs`,
  `ModRegistry.RegisteredCards`) but not projected. Console `cards` command
  (`DevConsole.cs` L78-85) is the only working list surface (text, not JS).
- `vc.hooks.on: __log only` — `Shared.cs` L153-155. No subscriber store, no
  `emit`/`trigger`, no call sites in any `*Patches.cs`. Planned events
  (`init/dungeonEnter/encounterStart/cardPlayed/beforeSave/afterLoad` + `inverseToggled`,
  per `todo.md` M7-T1-S2) need both a registry and fire-sites in Harmony postfixes
  (`Patch_CardConfig_OnPlayCard` is the natural first `cardPlayed` emitter).
- `vc.console` gaps: `DevConsole` has `Open/Close/Toggle/Clear/WriteLine/Execute`
  (L138-211) but none are bound into JS. `__consoleRegister` ctx (L120) is
  `{ log }` only — `DevConsoleContext.api: VcApi` (index.ts L284-286) is not provided,
  so JS console commands cannot call back into `vc`.

## 3. Fallback parser gaps (`FallbackParseCards`, `Shared.cs` L509-583)

Runs only when `engine.Execute` throws (L168-173). Cards-only; everything else silently dropped.
- **Trigger conditions**: any Jint exception (TS syntax, `import`, `export default`,
  optional chaining version gaps) — common for esbuild bundles, so fallback承担 more than
  it should.
- **Id regex** `id\s*:\s*"([^"]+)"` (L513): double-quotes only (misses `'...'`, backticks,
  unquoted/trailing-comma styles); requires `:` in id and skips already-registered
  (L517) — reasonable dedup but means partial re-registers are lost.
- **Window**: 900-char snippet around the match (L521) — fields outside the window are
  missed; large defs (long `text`, `onPlay` bodies) push later fields out.
- **Fields parsed**: name/text/color/mana/damage/armor/gemSlots/weight/rarity,
  scope/count, freeze/knockback/disarm/erase/minecart-flag/heal/coins/xp/combo/reverseCombo,
  icon|iconPath|sprite, body|bodyPath|background, retain/freeToPlay flags (L523-568).
  **Not parsed**: `drop`/`pools` (hardcoded `["levelUp","chestT1"]`, L574),
  `chestTiers/luckFactor/unlockId/shopWeight/rewardWeight/levelUpWeight`,
  `keywords` array, `crawlerTrigger/iconKey`, `target.row` strings beyond front/back/all,
  `target.column/pierce/random`, `effects.damage/armor/mana/draw/might/sound`,
  `minecart` object form (`{damage,...}`), `mana: "W"/"X"` (Jint path handles `"W"→0`,
  L200; regex path only `\d+`), `onPlay` (impossible via regex — accepted loss, but then
  the card silently loses behavior).
- Extension point for G-B: replace per-field regexes with a brace-balanced object
  extractor + one shared `CustomCardDef`-builder used by both Jint and fallback paths;
  add `FallbackParseArcanas/Enemies/Audio` (currently nonexistent).

## 4. Arcana commented-out status (three layers, all dormant)

1. `Shared.cs` `RegisterArcanaFromJs` (L467-507): parses id/name/text/description/icon
   then hits `/* … */` — `CustomArcanaDef` construction + `ArcanaBridge.RegisterArcana`
   never compile. Net effect: `vc.arcanas.register` is a silent no-op.
2. `DevConsole.cs` L86-132: `arcanas` + `give_arcana` commands fully commented out
   (they reference `ArcanaBridge.RegisteredDefs/FindArcana`).
3. `ArcanaBridge.cs.bak` + `ArcanaPatches.cs.bak`: excluded from build by extension
   (`.bak` — `PancakeModLoader.csproj` globs `*.cs`). The code itself is the most
   complete dormant asset:
   - Bridge: `CustomArcanaDef`, template-clone from `global::ArcanaConfig` via
     `Resources.FindObjectsOfTypeAll`, `EnsureCustomArcanasCreated`, `FindArcana/FindDef`
     (same fuzzy match as cards), per-def `arcana.sprite` via `SpriteLoader`.
   - Patches: reward-pool postfix with **dynamic `TargetMethod` fallback**
     (`ChooseArcanaModal.GetArcanaRewardPool` → `ArcanaManager.GetPool`, L16-31);
     `PlayerModel.InitDeck` postfix (currently no-op body); `ArcanaConfig.Name`
     getter + `GetDescription`/`GetEffectDescription` prefixes; `ArcanaChoiceView.SetReward`
     and `CardView.SetArcanaConfig` postfixes. `RunMetaSaveData.Init` block already
     commented *inside* the bak (L73-95).
   - Risks before re-enabling: `global::ArcanaConfig` top-level namespace assumption,
     `ChooseArcanaModal` method name, and `ArcanaConfigDatabase`-vs-direct-pool flow are
     all **unverified** — metadata (§7) confirms `ArcanaConfig`, `ArcanaConfigDatabase`,
     `ArcanaConfigMultiDatabase`, `ChooseArcanaModal`, `AddArcana/EquipArcana`,
     `CreateArcanaAssets/CreateArcanaObject` exist as strings, but signatures need
     Cpp2IL/dnSpy confirmation (M3-T0).

## 5. Enemy placeholder gaps

- `RegisterEnemyFromJs` (`Shared.cs` L585-622) reads id/name/health/xp/boss/maxHit/maxArmor/
  spritePath/frames/fps only. No intents/attack patterns, movement/row logic, drops,
  scaling, boss phases, or sound.
- `EnemyBridge.EnsureCustomEnemiesCreated` (L38-76): clones the first non-`mod_` template
  and **applies nothing** — comment (L69-72) openly defers to `EnemyPatches`, which never
  overrides stats. `Health/Xp/Boss/MaxHit/MaxArmor` are stored on `CustomEnemyDef` and
  die there.
- `EnemyBridge.SpawnEnemy` (L89-121): resolves config + encounter model, logs, then
  `Instantiate(config)` with `... setup enemy Model and View ...` unwritten — no row/col
  placement, no encounter-list insertion, no view spawn. Console `spawn` command
  (`DevConsole.cs` L59-69) calls into this, so it prints success text while spawning nothing.
- `EnemyPatches.cs` (35 lines, single class): only `EnemyView.Awake` prefix → attaches
  `ModSpriteAnimator` when `SpritePath` set. No stat application, no spawn hook, no
  pool/encounter-table injection (the card-side `GetCardRewardPool` analog is missing for
  enemies — M6 work).
- `CustomEnemyDef` defaults (`Health=10, Xp=1`, `Frames=1/Fps=5`) are sane but arbitrary;
  `Name` defaults to `""` (never falls back to id, unlike cards L186).

## 6. Console / UI overlay extension points for the Inverse toggle (M7-T2)

- **Host**: `DevConsoleBehaviour` (`DevConsoleBehaviour.cs` L7-30) — `DontDestroyOnLoad`
  `VCSurveilDevConsoleHost`, IL2CPP-registered in `Plugin.BepInEx.cs` L79-90
  (`ClassInjector.RegisterTypeInIl2Cpp<DevConsoleBehaviour>()` +
  `ModSpriteAnimator.RegisterClass()` + `EnsureHostExists()`). Any Inverse widget should
  reuse this pattern: either extend `DevConsoleBehaviour.OnGUI` or add a sibling
  `InverseToggleBehaviour` registered the same way (`InverseToggle.cs` in plan).
- **Toggle input** (triple-redundant, L49-123 + L131-155): new Input System
  (`Keyboard.current`: Ctrl+Tab / Backquote / F1 / Esc), legacy `Input.GetKey`, and raw
  IMGUI `Event.current` in `OnGUI` — the IMGUI path is the reliable one across backends.
  An Inverse hotkey can hook the same three sites.
- **Overlay**: `DevConsole.OnGUI` (L218-357) — `GUI.depth = -10000`, centered panel
  (`min(800, w-32) × min(480, h-80)`), `GUI.Box/Label/Button` only (deliberately
  unstripped primitives), 16-line log window, input bar, 6 quick-command buttons
  (`quickY`, L327-351 — the natural row to append an `inverse status` button).
  Input suppression while open: `Patch_InputManager_Update` prefix returns false
  (`CardPatches.cs` L35-46) + `SetGameInputEnabled` disables `InputAsset/ActionMap`
  (`DevConsole.cs` L160-186) + all key events `Use()`d (L285-291).
- **Commands** (L21-132): `help/clear/mods/give/spawn/heal/cards` live; `arcanas/give_arcana`
  commented (§4). `Execute` (L195-211) strips `/ ! \`` prefixes, splits args, history-preserving.
  Planned `inverse on|off|status` (M7-T2-S3) slots in here; JS-registered commands arrive via
  `__consoleRegister` and run with `(args, ctx)` where `ctx={log}` — extend to `{log, api}`
  for toggle callbacks.
- **Headless surface**: `Plugin.cs` (CI scaffold, 81 lines) mirrors discovery + `JsRuntime`
  without BepInEx/Harmony/IL2CPP — console commands that only touch registries
  (`cards`, `inverse status`) stay testable here; anything needing `PlayerModel`/spawn
  needs the game.

## 7. Sprite pipeline (`SpriteLoader.cs`, `ModSpriteAnimator.cs`, call sites)

- `SpriteLoader.LoadSprite(pathOrName, modDir)` (L56-179): 4-key case-insensitive cache;
  candidate order — raw → cleanRel → `modDir+rel` (×2 sep variants) → `modDir/assets+file`
  → `modDir+file` → Wine-mapped equivalents (`S:/`→Steam, `C:/users/`→`.vc-mods`, L41-54)
  → **hardcoded demo-mod dirs** (L91-94, game `Mods/demo-mod` + `~/.vc-mods/demo-mod`).
  Load via `LoadImage_Injected` icall resolved once (`EnsureNativeDelegate`, L23-37),
  `Texture2D RGBA32` → `Sprite.Create(rect full, pivot .5/.5, 100ppu)`,
  `HideAndDontSave|DontUnloadUnusedAsset`. Returns null with info-log on miss (L110-114).
  **Generalize-before-M8**: hardcoded demo paths + filename-only `assets/` fallback can
  collide across mods; key cache by resolved full path or `(modDir, rel)` tuple.
- `ModSpriteAnimator` (76 lines): IL2CPP `MonoBehaviour`, `Initialize(imagePath, frames, fps)`
  → `ImageConversion.LoadImage` + horizontal-strip split, `Update()` cycles at `fps`
  (default 5). **Only wired for enemies** (`EnemyPatches.Awake`, L17-33). Cards use static
  single sprites; arcana bak uses static `arcana.sprite`. Animated card art / multi-frame
  arcana icons would need new wiring.
- Call sites: `CardBridge.EnsureCustomCardsCreated` replaces `card.sprites` with a
  single-element `Il2CppReferenceArray` (L231-254); `CardVisualsHelper.Apply`
  (`CardPatches.cs` L366-465) sets `_cardImage(+Shadow)` from `IconPath` and
  `_frontBackground` from `BodyPath` (disabling `_cardTypeBackground`), re-resolving via
  `SpriteLoader` on every `SetCardConfig` (cache makes this cheap, but per-view repeat
  loads on cache-miss log spam — see L422/455 warns).

## 8. Harmony anchors (all in `CardPatches.cs` / `EnemyPatches.cs`; arcana in `.bak`)

Cards & reward flow:
- `Patch_ChooseCardModal_GetCardRewardPool` (L12-33): `ChooseCardModal.GetCardRewardPool`
  postfix — appends `CustomCardConfigs`. **Proven injection anchor.**
- `Patch_PlayerModel_InitDeck` (L58-80): adds every custom card to starting deck
  (dev convenience; keep gated or remove before ship — it bypasses pools/rarity).
- `Patch_CardConfig_OnPlayCard` (L968-1122): **the** execution anchor — resolves def,
  computes targets, applies freeze/disarm/knockback → damage → erase, minecart sweep,
  sound, armor/heal/mana/draw/might/coins/xp/combo/reverseCombo, then `OnPlayAction`
  JS closure. Null-tolerant (re-resolves encounter/player models, L979-996).
- Display/metadata prefixes (all `FindDef`-gated, safe no-ops for vanilla):
  `CardConfig.Name` (L271), `GetEffectDescription` (L290), `GetBaseEffectDescription` (L309),
  `GetLevelUpDescription` (L328), `GetMaxLevel→1` (L347); `FccConfig.FirstName/FullName/
  GetAllActionDescriptions/GetFccDescription` (L486-561); `CardView.SetCardConfig`
  → `CardVisualsHelper.Apply` (L467-482).
- Modal input-unblock cluster (L82-269): `ChooseCardModal.OnOpened/LateUpdate/
  SetRewardChoices/AddCard/CardChosen`, `CardChoiceView.SetReward/
  OnBackgroundButtonPressed`, `ChoosableCard.OnEnable/PickCard`,
  `PointerEvents.OnPointerDown` (resets `s_hasPointerDown`). Broad but load-bearing for
  custom-card clickability; touch minimally.
- `Patch_InputManager_Update` (L35-56): console-open input gate + `EnsureHostExists/
  CheckToggleInput` pump. Shared with §6.
- Targeting engine `CardExecutionHelper.GetTargetEnemies` (L627-837): scope/row/col/
  pierce/random/sort(all/front/back/row/col/single + prioritize_attackers/lowest/
  highest/random/multiple), multi-source enumeration (`AliveEnemies/FrontEnemies/
  EnemyGroups` + `RowMovementModel` fallback), `count=-1`=all. Reusable for any
  future "hit N enemies" effect (weapons/crawlers/relics).

Enemies: `EnemyPatches.EnemyView.Awake` prefix only (§5). Missing anchors: stat override
(post-`Awake` or model-init), encounter-table/pool injection, spawn API.

Arcana (dormant, `.bak` §4): pool postfix w/ dynamic target, `Name`/`GetDescription`
prefixes, `ArcanaChoiceView.SetReward` + `CardView.SetArcanaConfig` postfixes.

Lifecycle (`Plugin.BepInEx.cs` L30-115): mods resolve (`VC_MODS_DIR`/`SURVEIL_MODS_DIR`/
`~/.vc-mods` primary, `game/Mods` fallback) → `JsRuntime.LoadMod` each → Harmony
`PatchAll` → IL2CPP behaviours → discovery log. Crash handler writes `last-crash.log`
(L47-65). `Plugin.cs` is the BepInEx-free CI mirror.

## 9. Tool output (verbatim excerpts, 2026-10-03 run)

`python3 tools/dump-metadata.py 2>&1 | head -200` — namespaces include
`Nosebleed.Pancake.Arcana`, `Nosebleed.Pancake.GameConfig` (+`.Relics`,
`.ShopConfigs.Arcana`, `.TriggerTypes`, `.Keywords`), `Nosebleed.Pancake.GameLogic`
(+`.Weapons`, `.DeckBox`, `.GameStates`), `Nosebleed.Pancake.Modal/.Models/.View`,
`.Shops(.Arcana/.Crawlers/.Blacksmith/.PowerUps)`, `.MetaProgression/.SaveData/
.ProceduralGeneration/.DungeonEnvironment`. Card strings confirm `CardConfig`,
`AddCardConfigToDeck/Hand`, `AddFccCard`, `ChooseCardModal`-adjacent flows.
`Total unique strings: 144087` (tail).

`… | sed -n '/== Gem ==/,/Total/p'`: `GemConfig`, `GemChestPool/Manager/Marker/View`,
`EvoGem`, `FreeToPlay/Discharge/Swift/Mild/Preserve/Uncrackable` gem effects,
`FilterGemPool/FilterOutGemExclusionTags…`, `DEBUG_UnlockGem/ResetGemFrequencies…` —
a real gem subsystem the loader has zero bindings for (M4).

`… | sed -n '/== Arcana ==/,/Total/p'`: `ArcanaConfig`, `ArcanaConfigDatabase`,
`ArcanaConfigMultiDatabase`, `ChooseArcanaModal`, `ArcanaChoiceView`,
`AddArcana/EquipArcana/IsArcanaEquipped/EquippedArcanaIds`, `CreateArcanaAssets/
CreateArcanaObject`, `OnArcanasChanged/ArcanaSelected/ArcanaDeselected`,
`TryApplyArcana_*`, `ArcanaShop`, `NumberOfArcanasUnlocked` — supports re-enabling the
`.bak` arcana path pending signature verification (M3-T0).

`python3 tools/catalog-inspect.py`:
- `…/StreamingAssets/aa/catalog.json` → **missing**.
- `…/StreamingAssets/aa/catalog.bin` → present, **203224 bytes**, binary (hex head
  `4289e30d…`; needs Addressables-aware decoding — catalog-driven sprite/config
  discovery is future work, not a shortcut today).
- `…/StreamingAssets/aa/settings.json` → present (1146 bytes):
  `StandaloneWindows64`, Addressables `2.7.4`, `m_DisableCatalogUpdateOnStart: false`.

## 10. Implication checklist for G-A/G-B/G-C

- G-A (`index.ts`): add `WeaponDef/CrawlerDef/ArcanaDef+/RelicDef+(+RelicOptionDef)/
  BiomeDef+/RoomTemplate+/EnemyDef+/VcInverse|VcRunModes/VcState+` while keeping every
  existing field (parity constraint); mirror each in `registry.ts`.
- G-B (`Shared.cs` + bridges): implement `cards.patch/list`, `hooks.on` store+emit,
  `gems/relics/evolutions/dungeons/state` globals, `console` object-form + `api` ctx +
  open/close/toggle/isOpen; un-comment arcana path (rename `.bak`→`.cs` only after M3-T0
  signature check); flesh out `EnemyBridge` stat application + real spawn; extend
  fallback parser or scope it explicitly to cards; generalize `SpriteLoader` demo-mod
  hardcodes; new `InverseToggle.cs` reusing `DevConsoleBehaviour` host pattern.
- G-C (research): M7-T0 Inverse semantics + M2/M3/M5 Crawlers-vs-VS semantics before
  freezing toggle behavior; metadata strings above are the starting corpus.
- G-E (verification): `dotnet build` today compiles `.bak`-excluded set — renaming back
  will surface `global::ArcanaConfig`/modal-signature errors; that is expected and is
  the M3-T0 verification gate, not a regression.
