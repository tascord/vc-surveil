# VS Inverse Mode — Research Notes

Sources (all fetched 2026-10-04 via MediaWiki API; HTML pages 403/401-blocked so wikitext used):
- `https://vampire-survivors.fandom.com/api.php?...titles=Stages` (Inverse/Hurry/Hyper/Endless sections) — canonical VS page `https://vampire-survivors.fandom.com/wiki/Stages`
- `https://vampire-survivors.fandom.com/api.php?...titles=Gracia%27s_Mirror` — canonical `https://vampire-survivors.fandom.com/wiki/Gracia%27s_Mirror`
- `https://vampire-survivors.fandom.com/api.php?...titles=Eudaimonia_Machine` — canonical `https://vampire-survivors.fandom.com/wiki/Eudaimonia_Machine`
- `https://vampire-survivors.fandom.com/api.php?...titles=Relics` — canonical `https://vampire-survivors.fandom.com/wiki/Relics`
- `https://vampire-survivors.fandom.com/api.php?...titles=Inverse_mode` → redirect to `Stages#Inverse Mode`
- `https://vampire-survivors.fandom.com/api.php?...titles=Endless_mode` → redirect to `Stages#Endless Mode`

## What Inverse mode does (VS) — confidence HIGH
Per `Stages` wikitext ("===Inverse Mode ===" section), verbatim mechanics:
- Stat modifiers: gold gain **+200%**, Luck bonus **+20%**. Enemies start with **+200% HP** and gain **+5% HP and +0.5 movement speed per minute**. Additive with other stage enemy modifiers (e.g. The Bone Zone, Bat Country).
- Layout: stage layout rotated **180°**; map darker / different hue. Some stages place stage items differently. Visual inversion is user-toggleable via Settings → Gameplay → "**Visually Invert Stages**". Does **not** apply on Ode to Castlevania.
- Merchant: in Inverse, merchant also offers **Skips, Banishes, Rerolls, and an extra Arcana** (20,000 gold per `Arcanas` page; repeatable every Endless cycle).
- Music: some stages switch to alternate stage theme; character-specific themes override.
- Stacking: all four modes (Hyper, Hurry, Inverse, Endless) are **unlockable + toggleable per-stage in Stage Selection**, combinable concurrently and with gameplay modifiers (Arcanas, Limit Break, Randomizers).

## How it is unlocked (VS) — confidence HIGH
- Relic: **Gracia's Mirror** — "Permanently allows to access a harder version of any stage." Effect: unlocks Inverse Mode.
- Location/flow: granted in **Eudaimonia Machine** (special stage, unlocked after obtaining all standard relics; entered at top of level select; no enemies; walk upward to the Directer entity).
  - First visit: Directer asks a question. Picking **"Too Easy"** grants Gracia's Mirror (and boots back to title with everything inverted = Inverse available). Picking "Too Hard" grants Seventh Trumpet (Endless) instead.
  - Recovery: enter + exit any stage with the newly unlocked mode → Eudaimonia re-opens → second visit grants the other relic. Then a third visit triggers the Directer boss fight (→ Greatest Jubilee).
  - Spell shortcut: `Forbidden Scrolls of Morbane` secret menu, spell **"freezearrow"** also grants it (confidence MEDIUM — single-page claim).
- Trivia: required to unlock character Avatar Infernas.

## Toggle behavior (VS) — confidence HIGH
- Per-stage toggles in **Stage Selection menu** (not in-run UI): Hyper / Hurry / Inverse / Endless checkboxes before entering.
- Gameplay modifiers (Arcanas, Limit Break, Randomizers) are separate toggles on the same screen.
- Inverse persists as a permanent account unlock once the relic is collected; enabling it is opt-in per run.
- Note: our `VcInverse`/`VcRunModes` API models an **in-run toggle** — that is a Crawlers-mod design choice, NOT VS behavior. VS has no in-run Inverse switch (confidence HIGH).

## Crawlers mapping — do NOT assume VS == Crawlers
- **LOW confidence / likely ABSENT in Crawlers:** no `Inverse*` / `Mirror` gameplay strings in `global-metadata.dat`. All `Inverse*` hits are Unity math (`InverseTransformPoint`, `inverseViewMatrix`, …) plus generic `allowInverse`/`modInverse` (unscoped — could be shader/math, not a run mode). No `Gracia`, `Mirror`-relic, or `VisuallyInvert` strings found.
- **HIGH confidence PRESENT in Crawlers:** `EndlessModeActive`, `SetEndlessMode`, `ToggleEndlessMode`, `AllowsEndlessMode`, `IsThisDungeonEndless`, `EndlessModeStatConfig`, `HurryModeActive`, `SetHurryMode`, `ToggleHurryMode`. So Crawlers natively models **Endless + Hurry** run flags with setter/toggle/stat-config accessors — same shape as `VcRunModes.setActive/isActive`.
- Implication for M7: implement `vc.inverse` / `vc.runModes` as **mod-level state** (in-memory toggle gated on relic ownership, `hooks.on("inverseToggled")`), exactly as `index.ts` already declares. Do NOT claim native layout rotation, enemy-HP scaling, or merchant-extension behavior in Crawlers until assembly verification (M7-T2/T3). The "in-run UI toggle" starter scenario is a new mod feature, not a VS port.

## Unknowns
- Whether Crawlers has any hidden/rotated-layout or difficulty-toggle relic not named "Inverse" (metadata string dump is name-only; needs assembly/level-select UI inspection).
- Exact Crawlers Endless/Hurry stat effects (`EndlessModeStatConfig` contents) and whether `ToggleEndlessMode`/`ToggleHurryMode` are UI- or code-driven.
- Whether Crawlers `RelicShop` sells mode unlocks (see `vs-content.md` relics section).
