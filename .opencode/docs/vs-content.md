# VS Content Survey — Relics / Stages / Weapons / Arcana / Enemies

Sources (fetched 2026-10-04 via `vampire-survivors.fandom.com/api.php` wikitext; HTML blocked):
- Relics list: `https://vampire-survivors.fandom.com/wiki/Relics`
- Stages/modes: `https://vampire-survivors.fandom.com/wiki/Stages`
- Weapons: `https://vampire-survivors.fandom.com/wiki/Weapons` (+ `Category:Weapons` member list)
- Arcana: `https://vampire-survivors.fandom.com/wiki/Arcanas` (+ `Category:Arcanas` member list)
- Enemies: `https://vampire-survivors.fandom.com/wiki/Enemies` (+ category tree)
- Eudaimonia: `https://vampire-survivors.fandom.com/wiki/Eudaimonia_Machine`

## 1. Relics — VS (confidence HIGH)
Model: permanent items, spawned on map, collected on touch, kept forever, never respawn. Pre-pickup locations shown by green arrows. Each unlocks a mechanic/feature:
| Relic | Unlocks | Found in |
|---|---|---|
| Grim Grimoire | evolution/union preview in pause menu | west of Inlaid Library |
| Ars Gouda | Bestiary (enemy list, main menu) | south of Dairy Plant |
| Milky Way Map | pause-menu map + stage-item markers | south of Dairy Plant |
| Magic Banger | music selection in Stage Select | south-east of Green Acres |
| Glass Vizard | Merchant in all stages | bought from Moongolow merchant |
| Mindbender | cosmetics + max-weapon-loadout setting | 50 Collection entries |
| Yellow Sign | hidden items (rings/metaglios), hidden relics/events | east of Holy Forbidden |
| Forbidden Scrolls of Morbane | Secrets menu + spell casting | Sketamari drop, south of Bone Zone |
| Sorceress' Tears | Hurry Mode | south of Gallo Tower |
| Great Gospel | Limit Break | Ender kill in Cappella Magna (needs Yellow Sign) |
| **Gracia's Mirror** | **Inverse Mode** | Eudaimonia Machine ("Too Easy") |
| Seventh Trumpet | Endless Mode | Eudaimonia Machine ("Too Hard") |
| Randomazzo | Arcana system + Sarabande of Healing | (relic; exact stage per page) |
| Darkasso | Darkanas set | south of Room 1665 |
| (more rows truncated in fetch: Vlad, etc.) | | |

Key API mapping: VS relics are **account-permanent unlock flags**, not per-run options. Our `RelicDef.options[]` / `setOption/getOption` + `relicOptionChanged` is a Crawlers-mod extension (no VS equivalent). `RelicDef.unlocksToggle: "inverse"` mirrors Gracia's Mirror → Inverse gating and is the right pattern.

## 2. Stages / maps — VS (confidence HIGH)
- 27 official stages: 20 base (5 normal, 5 bonus, 7 challenge, 2 special, 1 hidden) + 7 DLC. Unlock via achievements (except Mad Forest default).
- Completion: survive to time limit → +500 gold (+100 per unused revival); then a Reaper/minute unless Endless.
- Per-stage base stat modifiers (player move/projectile speed, greed, luck; enemy speed/HP/quantity), stacking additively with mode modifiers.
- Modes (all per-stage unlockable/toggleable, combinable): **Hyper** (+65–75% move, +15–25% projectile, +50% gold; per-stage luck/HP extras; unlock usually by boss kill), **Hurry** (2× clock, +25% XP; unlocked by Sorceress' Tears), **Inverse** (see `vs-inverse.md`), **Endless** (no post-limit Reapers; waves restart per cycle; enemies +100% HP / +50% spawn / +25% dmg per cycle; player damage-cap −1; merchant respawns + Revival instead of Golden Eggs).
- Gameplay modifiers (same select screen): **Arcanas**, **Limit Break** (Great Gospel; over-level weapons past cap), **Random Events**, **Random LevelUp**.

## 3. Weapons — VS (confidence HIGH on model, MEDIUM on roster details)
- Model: character-held, unique per run (one copy; exceptions: single-use Candybox/Arma Dio). Up to 6 via level-ups; stage-spawned extras can exceed. Rarity = level-up/chest weight. Level-ups upgrade owned weapons (resets cooldown, instant proc). Max-level weapon + paired passive + chest → **Evolution** (base removed, effect restarts; some characters auto-evolve on morph with relic condition).
- Stats: max level, rarity, base damage (±5 display jitter only), area, speed, amount, duration, pierce, cooldown.
- Roster: `Category:Weapons` is large (100+ incl. DLC/variants: Whip, Axe, Knife, Garlic, Santa Water, Clock Lancet, Pentagram, Gorgeous Moon, Greatest Jubilee, …). Full roster intentionally NOT copied here — query the category API when M1 needs it.
- Crawlers note: `Nosebleed.Pancake.GameLogic.Weapons` + `GenerateWeaponEvoItems` + `GetPlayerEvolvableWeapons` + `DungeonCamWeapons` prove weapons+evolutions are a **distinct native system from cards**. Our `WeaponDef` (separate from `CardDef`, linked via `weaponId`/`evolutionFrom`/`EvolutionRecipe`) matches this split. Whether Crawlers weapons follow VS equip-6/evolve-with-passive rules is UNVERIFIED (LOW).

## 4. Arcana — VS (confidence HIGH on model, MEDIUM on roster)
- Unlock: Randomazzo relic → system + Sarabande of Healing (VI). More via level-50-with-character or minute-31-in-stage; Darkasso adds Darkanas (Roman-numeral tarot set; `Category:Arcanas` lists ~21 base + ~8 dark incl. Gemini (I), Twilight Requiem (II), … Heart of Fire (XIX), plus Game Killer (0)).
- Draft: enable/disable in Stage Select; run grants 3 — 1× free pick at start (any unlocked), 1× Arcana-Chest boss at 11:00, 1× at 21:00 (4 choices, rerollable; 6 choices + free reroll at 23+ unlocked). ≥3 owned → bosses drop normal chests. Disabled → bosses don't spawn. Short stages (Boss Rash, Bat Country, Tiny Bridge): bosses at 5:00/10:00. Extras: Queen Sigma bonus picks; **Inverse merchant sells Arcana (20k)**; Moonlight Bolero (VI) boss chests can contain infinite Arcanas. Some characters carry innate no-slot arcana effects.
- Secret codes: `randomazzami`, `darkassami`, `aintnobodygottimeforthat`.
- Crawlers note: native `Nosebleed.Pancake.Arcana` + `Shops.Arcana` + `ShopConfigs.Arcana` namespaces exist → arcana system likely present (MEDIUM). Draft timing/boss/chest rules are VS-only (LOW for Crawlers). Our `ArcanaDef` (+`modifiers`/`hooks`) stays declarative pending M3-T2.

## 5. Enemies — VS (confidence HIGH on model, LOW on roster parity)
- Model: contact-damage entities; kills grant XP/gems. Spawn outside screen, despawn at distance. Waves every minute (min-count quota + interval; 300-alive cap pauses periodic spawns; bosses/map-events exempt). Bosses: tankier, resist effects, drop chests, teleport back instead of despawn. Map events: timed swarm/trap/special spawns (Dairy Plant / Gallo Tower pressure plates; Luck-scaled chance `eventChance/totalLuck` and trap cooldown `trapCooldown × totalLuck`). Stats: Health / Power / Speed / Knockback / XP + Freeze resistance etc.
- Roster: `Category:Enemies` is a tree (by spawn type/stage/resistances/skills), Bestiary-gated. Not enumerated here — query per-stage when M6 needs it.
- Crawlers note: `GenerateEncounter` (`SelectHigherEnemy`/`SelectLowerEnemy`), `NukeDungeonEncounters`, `ClearDungeonEncountersExceptBosses`, encounter/boss floor concepts prove a native spawner exists (MEDIUM). VS wave math is NOT evidenced (LOW). Our `EnemyDef` + `behavior`/`stats`/`drops` stays declarative pending M6-T2.

## Confidence legend
- HIGH: quoted directly from fetched wikitext, multiple corroborating sections.
- MEDIUM: single-page claim or category-list inference (spell shortcuts, secret codes, roster counts).
- LOW: any VS→Crawlers transfer (see `crawlers-mapping.md`).
