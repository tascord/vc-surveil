/**
 * @vc-surveil/api — TypeScript modding API for Vampire Crawlers (Pancake).
 * Loader injects a global `vc` matching these types into each mod's Jint/V8 realm.
 * Mods `import { vc } from "@vc-surveil/api"` for type-checking only; at runtime use the global.
 *
 * Declarative-status note (honest contract):
 * Card registration has live loader wiring; every other namespace in this file
 * (weapons, crawlers, arcanas, relics/options, dungeons/rooms, enemies-ex,
 * gems-ex, evolutions-ex, inverse/run-modes) is a DECLARATIVE extension point
 * pending assembly verification. The C# loader stores these definitions and
 * exposes `register`/`patch`/`list` round-trips, but native game wiring
 * (spawn tables, draft pools, map generation, run-mode effects) must be
 * verified against installed game assemblies/metadata before claiming
 * in-game behavior. See `.opencode/todo.md` M1–M7 and `tools/dump-metadata.py`.
 */

// ── common ──
export type ManaCost = number | "W" | "X"; // W = wild/free, X = remaining mana
export type CardColor = "attack" | "defense" | "stat" | "mana" | "wild" | "temporary" | "character" | "crawler";
export type Rarity = "Common" | "Uncommon" | "Rare" | "VeryRare" | "UltraRare";
export type TriggerColor = "red" | "blue" | "yellow" | "purple" | "rainbow";

/** All lifecycle / gameplay hook events. Loader invokes these; mods subscribe via `vc.hooks.on`. */
export type HookEvent =
  | "init"
  | "dungeonEnter"
  | "encounterStart"
  | "cardPlayed"
  | "beforeSave"
  | "afterLoad"
  | "relicOptionChanged"
  | "inverseToggled";

export interface ModMeta {
  id: string; // e.g. "demo-mod"
  version?: string;
}

// ── cards ──
export type CardRarity = Rarity; // alias — cards share gem rarities; see drop pools below
export interface CardDropConfig {
  /** Pools this card can appear in. Defaults to levelUp + chestT1. */
  pools?: ("levelUp" | "chestT1" | "chestT2" | "chestT3" | "shop" | "reward")[];
  /** Weight within its pool(s) — higher = more common. Vanilla ~10–200. */
  weight?: number;
  /** Per-tier chest override: which chest tiers this shows in when pooled via chest. */
  chestTiers?: (1 | 2 | 3)[];
  /** How Luck stat scales this drop (0 = unaffected, 1 = strongly luck-scaled). */
  luckFactor?: number;
  /** Minimum dungeon / progression unlock. */
  unlockId?: string;
}

// ── targeting & effects ──
export type TargetScope =
  | "single"
  | "all"
  | "front_row"
  | "back_row"
  | "all_rows"
  | "row"
  | "column"
  | "random"
  | "lowest_health"
  | "highest_health"
  | "prioritize_attackers"
  | "multiple";

export interface CardTargetConfig {
  /** Target scope / pattern. Defaults to "single". */
  scope?: TargetScope;
  /**
   * Number of creatures to hit.
   * Can be a number (e.g. 1, 2, 3) or "all". Default: 1 (or all if scope is "all" / "*_row").
   */
  count?: number | "all";
  /** Row targeting: "front" (row 0), "back", "all", or a specific 0-based row index. */
  row?: "front" | "back" | "all" | number;
  /** Column targeting: 0-based column index within the row (0..3). */
  column?: number;
  /** Whether the attack pierces through enemies in the column. */
  pierce?: boolean | number;
  /** Random target selection flag. */
  random?: boolean;
}

export interface MinecartEffectConfig {
  damage?: number;
  knockback?: boolean;
  sound?: boolean;
}

export interface CardEffectsConfig {
  /** Stacks of freeze to apply to target(s) (e.g. Clock Lancet, Orologion). */
  freeze?: number;
  /** Disarm targeted enemies, canceling their upcoming attack intent (e.g. Garlic, Soul Eater). */
  disarm?: boolean;
  /** Knock back target(s) (e.g. Axe, Hellfire, Unholy Vespers). */
  knockback?: boolean | number;
  /** Instant defeat / erase target(s) (e.g. Pentagram, Gorgeous Moon). */
  erase?: boolean;
  /** Deal additional flat damage (or use card.damage). */
  damage?: number;
  /** Grant armor to player (e.g. Armor, Golden Armor). */
  armor?: number;
  /** Restore player health (e.g. Bloody Tear, Soul Eater, Little Heart). */
  heal?: number;
  /** Grant player mana (e.g. Empty Tome, Light Tome). */
  mana?: number;
  /** Draw cards from deck (e.g. Attractorb). */
  draw?: number;
  /** Boost player might temporarily (e.g. Spinach). */
  might?: number;
  /** Add gold coins to player (e.g. Stone Mask, Vicious Hunger). */
  coins?: number;
  /** Add XP to player (e.g. Crown, Imelda). */
  xp?: number;
  /** Combo increase when played (e.g. Friendship Amulet, Wings, Combo cards). */
  combo?: number;
  /** Reverses the combo direction. */
  reverseCombo?: boolean;
  /**
   * Minecart sweep effect: spawns a speeding minecart across the row/field,
   * dealing damage, knockback, and playing the minecart SFX.
   */
  minecart?: boolean | MinecartEffectConfig;
  /** Custom SFX to play upon card play. */
  sound?: string;
}

export interface CardDef {
  /** Unique id, namespaced by mod: "demo-mod:test_strike" */
  id: string;
  /** Display name; if omitted derived from id */
  name?: string;
  color: CardColor;
  mana: ManaCost;
  text: string;
  /** Damage shown on card (pre-combo). Omit for non-attack. */
  damage?: number;
  /** Armor granted. */
  armor?: number;
  /** Gem slots on this card (0-3 typical). */
  gemSlots?: number;
  /** Retain card in hand across turns (e.g. Friendship Amulet, Wings). */
  retain?: boolean;
  /** Card is free to play (costs 0 mana, can always be cast). */
  freeToPlay?: boolean;
  /** Relative path to custom PNG/JPG sprite/icon (e.g. "assets/icon.png") or sprite key. */
  icon?: string;
  /** Alias for `icon`. */
  iconPath?: string;
  /** Relative path to custom PNG/JPG sprite for the card body/frame background (e.g. "assets/card_body.jpg"). */
  body?: string;
  /** Alias for `body`. */
  bodyPath?: string;
  /** Vanilla icon key to reuse (e.g. "Whip", "Armor", "Empty_Tome"). Optional custom art key later. */
  iconKey?: string;
  /** Keywords for synergy (Knockback, Crit, Freeze, etc.) */
  keywords?: string[];
  /**
   * Crawler trigger (inline, card-level): e.g. `{ when: "red", effect: "Deal 10% more damage" }`.
   * Declarative pending assembly verification — loader records it; native
   * trigger dispatch is not yet verified. Prefer linking `crawlerId` to a
   * `CrawlerDef` (see `VcCrawlers`) for the owned-trigger model, and use this
   * field for ad-hoc card text.
   */
  crawlerTrigger?: { when: TriggerColor | string; effect: string };
  /**
   * Owning crawler id (`CrawlerDef.id`) when this card belongs to / is
   * triggered by a crawler. Declarative link pending assembly verification.
   */
  crawlerId?: string;
  /**
   * Weapon this card belongs to / is granted by (`WeaponDef.id`).
   * Declarative link pending assembly verification of the cards-vs-weapons
   * model (are weapons cards, evolutions, or a separate pool?).
   */
  weaponId?: string;
  /**
   * Evolution(s) this card participates in. Shorthand so card authors can
   * declare `primary + secondary -> result` next to the card instead of only
   * via `vc.evolutions.register`. Declarative pending assembly verification.
   */
  evolution?: EvolutionRecipe | EvolutionRecipe[];

  /** Targeting configuration (target count, rows, columns, etc.) */
  target?: CardTargetConfig;

  /** Built-in status effects & gameplay effects (freeze, minecart, heal, knockback, etc.) */
  effects?: CardEffectsConfig;

  /** Optional JS effect hook (loader may call this per play). Keep pure-ish. */
  onPlay?: (ctx: CardPlayCtx) => void;

  // ── rarity / drop chance ──
  /** Card rarity — drives chest/level-up weighting alongside `weight`/`drop`. */
  rarity?: CardRarity;
  /** Simple weight shortcut for `drop.weight` (default 100). */
  weight?: number;
  /** Full drop tuning — overrides `weight`/`rarity` pool handling when present. */
  drop?: CardDropConfig;
  /** Back-compat: alias for chestTiers inside drop. */
  chestTiers?: (1 | 2 | 3)[];
  /** Back-compat aliases for legacy weight fields. */
  rewardWeight?: number;
  shopWeight?: number;
  levelUpWeight?: number;
}

export interface CardPlayCtx {
  playerMana: number;
  combo: number;
  damageMultiplier: number;
  addMana(n: number): void;
  addArmor(n: number): void;
  heal(n: number): void;
  draw(n: number): void;
  dealDamage(n: number): void;
  freeze(stacks?: number): void;
  disarm(): void;
  knockback(): void;
  erase(): void;
  addCoins(n: number): void;
  addXp(n: number): void;
  addCombo(n: number): void;
  getCombo(): number;
  reverseCombo(): void;
  minecart(cfg?: MinecartEffectConfig): void;
}

// ── weapons ──
/**
 * Weapon definition.
 * Declarative pending assembly verification — the loader stores the def and
 * exposes `vc.weapons` round-trips; whether weapons are cards, evolutions, or
 * a separate pool must be decided from game metadata before claiming native
 * behavior (M1-T1-S2). Cards link back via `CardDef.weaponId` / `evolution`.
 */
export interface WeaponDef {
  /** Unique id, namespaced by mod: "demo-mod:rusty_whip" */
  id: string;
  /** Display name; if omitted derived from id. */
  name?: string;
  /** Rules text shown for the weapon. */
  text: string;
  /** Base damage (pre-combo/modifiers). Omit for utility weapons. */
  damage?: number;
  /** Card color family this weapon's cards belong to. Defaults to "attack". */
  color?: CardColor;
  /** Relative asset path or sprite key for the weapon icon. */
  icon?: string;
  /** Alias for `icon`. */
  iconPath?: string;
  /** Rarity — drives chest/shop weighting alongside `drop`. */
  rarity?: CardRarity;
  /** Drop tuning (pools/weight/chest tiers). Same shape as cards. */
  drop?: CardDropConfig;
  /**
   * Evolution this weapon upgrades from (e.g. base weapon id or full recipe).
   * String form = base card/weapon id; object form = full `EvolutionRecipe`.
   */
  evolutionFrom?: string | EvolutionRecipe;
}

// ── crawlers ──
/** Trigger owned by a crawler (color match → named effect). */
export interface CrawlerTriggerDef {
  /** Trigger color (or custom trigger key). */
  when: TriggerColor | string;
  /** Human-readable rules text for the trigger effect. */
  effect: string;
  /** If true, fires at most once per turn. Defaults to false. */
  oncePerTurn?: boolean;
}

/** Base run stats granted/associated with a crawler. */
export interface CrawlerBaseStats {
  health?: number;
  mana?: number;
  might?: number;
}

/**
 * Crawler definition (playable/trigger entity).
 * Declarative pending assembly verification — exact Crawlers-specific
 * semantics (character vs summon vs card-color owner) must be confirmed via
 * wiki/metadata research (M2-T0) before claiming native behavior.
 * Cards link via `CardDef.crawlerId`; ad-hoc text via `CardDef.crawlerTrigger`.
 */
export interface CrawlerDef {
  /** Unique id, namespaced by mod: "demo-mod:ember_crawler" */
  id: string;
  /** Display name; if omitted derived from id. */
  name?: string;
  /** Rules/lore text for the crawler. */
  text: string;
  /** Card colors affiliated with this crawler. */
  colors?: CardColor[];
  /** Owned trigger (color match → effect). */
  trigger?: CrawlerTriggerDef;
  /** Base stats for runs using this crawler. */
  baseStats?: CrawlerBaseStats;
  /** Relative asset path or sprite key for the crawler portrait/icon. */
  icon?: string;
  /** Alias for `icon`. */
  iconPath?: string;
}

export interface GemDef {
  id: string;
  name?: string;
  rarity: Rarity;
  text: string;
  /** Effect tags for filtering: "Double_Damage", "Draw", "Retain", etc. */
  tags?: string[];
  /** Tier chests this appears in. */
  chestTiers?: (1 | 2 | 3)[];
}

/**
 * Arcana definition (extended).
 * Declarative pending assembly verification — loader keeps a real store with
 * `register`/`patch`/`list` round-trips, but draft-pool/passive application
 * must be verified against game assemblies (M3-T0/M3-T2) before claiming
 * native activation. The commented-out native stub in the loader is NOT active.
 */
export interface ArcanaDef {
  id: string;
  name?: string;
  text: string;
  description?: string;
  icon?: string;
  iconPath?: string;
  /** Rarity — drives draft/chest weighting when natively wired. */
  rarity?: CardRarity;
  /** Minimum dungeon / progression unlock. */
  unlockId?: string;
  /**
   * Numeric/flag modifiers applied while the arcana is active,
   * e.g. `{ might: 2, magnet: 10 }`. Keys are game-stat names;
   * unknown keys are stored verbatim for forward-compat.
   */
  modifiers?: Record<string, number | string | boolean>;
  /**
   * Hook events this arcana listens to. Loader may invoke arcana logic on
   * these events once native wiring lands; today they are stored declaratively.
   */
  hooks?: HookEvent[];
}

/** Value type for configurable relic options. */
export type RelicOptionValue = string | number | boolean;

/**
 * A single configurable option on a relic (toggle / choice / slider).
 * Declarative pending assembly verification — the loader persists per-run
 * option state and fires `relicOptionChanged`; in-run native effects of
 * options must be verified against game assemblies.
 */
export interface RelicOptionDef {
  /** Option key, unique within the relic (e.g. "enabled", "mode"). */
  key: string;
  /** Human-readable label for in-game UI. */
  label: string;
  /** Control kind: on/off switch, fixed choice, or numeric slider. */
  kind: "toggle" | "choice" | "slider";
  /** Default value when the relic is first acquired. */
  default: RelicOptionValue;
  /** Allowed values for `choice` kind. */
  choices?: RelicOptionValue[];
  /** Minimum value for `slider` kind. */
  min?: number;
  /** Maximum value for `slider` kind. */
  max?: number;
  /** Help text shown under the option. */
  description?: string;
}

/**
 * Relic definition (extended).
 * Declarative pending assembly verification — discovery (`foundIn`),
 * option UI, and passive effects are stored by the loader; native application
 * must be verified against game assemblies before claiming in-game behavior.
 * A relic may unlock an in-run toggle (e.g. `unlocksToggle: "inverse"`, M7).
 */
export interface RelicDef {
  id: string;
  name?: string;
  text: string;
  /** Dungeon id where this relic is discovered, if any. */
  foundIn?: string;
  /** Relative asset path or sprite key for the relic icon. */
  icon?: string;
  /** Alias for `icon`. */
  iconPath?: string;
  /** Rarity — drives chest/reward weighting when natively wired. */
  rarity?: Rarity;
  /** Minimum dungeon / progression unlock. */
  unlockId?: string;
  /** Configurable options exposed in the relic UI. */
  options?: RelicOptionDef[];
  /** Default per-option values (mirrors `options[].default`; explicit wins). */
  defaultOptions?: Record<string, RelicOptionValue>;
  /** Passive run modifiers granted while the relic is held. */
  effects?: CardEffectsConfig;
  /**
   * In-run toggle this relic unlocks (e.g. `"inverse"`). The toggle UI is
   * hidden/disabled until the relic is owned (M7-T3 acceptance).
   */
  unlocksToggle?: string;
}

/**
 * Enemy behavior tuning (movement / targeting / spawn placement).
 * Declarative pending assembly verification — loader stores it; native
 * spawn/AI wiring must be verified against game assemblies (M6-T2).
 */
export interface EnemyBehaviorConfig {
  /** Attack pattern key (game-specific; stored verbatim). */
  attackPattern?: string;
  /** Movement speed multiplier or absolute speed. */
  moveSpeed?: number;
  /** Weight within encounter/spawn tables. Higher = more common. */
  spawnWeight?: number;
  /** Floor numbers (or ranges) this enemy can appear on. */
  floors?: number[];
  /** Biome ids this enemy can appear in. */
  biomes?: string[];
}

/** Enemy combat stats (applied on top of `health`/`maxHit`/`maxArmor`). */
export interface EnemyStatsConfig {
  damage?: number;
  armor?: number;
  speed?: number;
}

/** Enemy reward drops granted on defeat. */
export interface EnemyDropsConfig {
  coins?: number;
  xp?: number;
  /** Card ids (or pool tags) this enemy can drop. */
  cards?: string[];
}

/**
 * Enemy definition (extended).
 * Partial native wiring exists (definition parsing + sprite animation), but
 * real spawn/config/stat application contains placeholders — treat new
 * `behavior`/`stats`/`drops` fields as declarative pending assembly
 * verification (M6-T2). Sprite sheets via `spritePath` + `frames`/`fps`.
 */
export interface EnemyDef {
  id: string;
  name?: string;
  health: number;
  xp: number;
  boss?: boolean;
  maxHit?: number;
  maxArmor?: number;
  /** Relative path to a sprite sheet or a single sprite image. */
  spritePath?: string;
  /** Optional frames for sprite animation */
  frames?: number;
  fps?: number;
  /** Movement/targeting/spawn placement tuning. */
  behavior?: EnemyBehaviorConfig;
  /** Combat stats overlay. */
  stats?: EnemyStatsConfig;
  /** Reward drops granted on defeat. */
  drops?: EnemyDropsConfig;
}

export interface EvolutionRecipe {
  primary: string; // card id
  secondary: string | string[]; // card id or alternatives
  result: string; // card id
}

/** Single entry in a biome's encounter table. */
export interface BiomeEncounterEntry {
  /** Enemy id (`EnemyDef.id`, vanilla or modded). */
  enemyId: string;
  /** Spawn weight within the table. Higher = more common. */
  weight: number;
  /** Floor numbers this entry applies to (omit = all floors). */
  floors?: number[];
}

/**
 * Biome / dungeon definition (extended).
 * Declarative pending assembly verification — floor generation, background/
 * music application, and encounter-table consumption must be verified against
 * game assemblies (M5-T0/M5-T2). The loader stores the def and exposes it via
 * `vc.dungeons` round-trips plus a `dungeonEnter` hook for demos.
 */
export interface BiomeDef {
  id: string;
  name?: string;
  floors: number;
  difficulty?: number;
  /** Relative asset path or background key for this biome. */
  background?: string;
  /** Music/audio id played in this biome. */
  music?: string;
  /** Weighted encounter table consumed by the spawner when natively wired. */
  encounterTable?: BiomeEncounterEntry[];
  /** 1-based floor where the boss encounter spawns (omit = none). */
  bossFloor?: number;
  /** Biome-wide run modifiers, e.g. `{ darkness: 1 }` (stored verbatim). */
  modifiers?: Record<string, number | string | boolean>;
}

/** Reward granted by a room template. */
export interface RoomRewardDef {
  coins?: number;
  xp?: number;
  cards?: string[];
}

/**
 * Room template (extended).
 * Declarative pending assembly verification — room-gen consumption must be
 * verified against game assemblies (M5-T0/M5-T2).
 */
export interface RoomTemplate {
  id: string;
  weight?: number;
  tags?: string[];
  /** Biome this room belongs to (`BiomeDef.id`; omit = any). */
  biomeId?: string;
  /** Minimum floor (1-based) this room can appear on. */
  minFloor?: number;
  /** Maximum floor (1-based) this room can appear on. */
  maxFloor?: number;
  /** Enemy ids (or `enemyId:count` entries) placed in this room. */
  enemies?: string[];
  /** Reward granted on room clear. */
  reward?: RoomRewardDef;
}

export interface AudioDef {
  id: string;
  path: string;
}

// ── run modes / inverse ──
/** Snapshot of active run flags/modifiers (e.g. `{ inverse: true }`). */
export type RunFlags = Record<string, string | number | boolean>;

/**
 * Inverse run-modifier namespace (M7).
 * Legacy preset of the generic run-toggles system: `inverse` is equivalent
 * to the `VcRunToggles` entry with id `"inverse"` (see `VcApi.toggles`).
 * Kept for backward compatibility; new modes should use `vc.toggles.register`.
 * Declarative pending assembly verification — the loader holds in-memory
 * toggle state gated on relic ownership (`RelicDef.unlocksToggle: "inverse"`);
 * observable run-behavior changes must be verified against game assemblies
 * (M7-T2/M7-T3) before claiming native effects.
 */
export interface VcInverse {
  /** True when the owning relic has been discovered this run/save. */
  isUnlocked(): boolean;
  /** True when inverse mode is currently enabled. */
  isEnabled(): boolean;
  /** Enable/disable inverse mode (no-op while locked unless forced by tests). */
  setEnabled(b: boolean): void;
  /** Subscribe to toggle flips (also observable via `hooks.on("inverseToggled")`). */
  onToggle(cb: (enabled: boolean) => void): void;
}

/**
 * Generic run-mode namespace (M7).
 * Declarative pending assembly verification — same status as `VcInverse`;
 * `"inverse"` is the starter mode id.
 */
export interface VcRunModes {
  /** All known run-mode ids. */
  list(): string[];
  /** True when the given mode is currently active. */
  isActive(id: string): boolean;
  /** Enable/disable a run mode. */
  setActive(id: string, enabled: boolean): void;
}

// ── run toggles (generic per-run feature flags) ──
/**
 * Gate conditions for when a run toggle may become active.
 * All present fields must hold for the toggle to apply at runtime.
 * Declarative pending assembly verification — same status as `VcInverse`.
 */
export interface RunToggleCondition {
  /** Biome ids this toggle applies in (omit = all biomes). */
  biomes?: string[];
  /** Minimum floor (1-based) this toggle applies on. */
  minFloor?: number;
  /** Maximum floor (1-based) this toggle applies on. */
  maxFloor?: number;
  /** Relic ids that must be owned for the toggle to apply. */
  requiresRelics?: string[];
  /** Run-flag key/values that must match (see `VcState.getRunFlags`). */
  requiresFlags?: Record<string, string | number | boolean>;
}

/**
 * Run toggle definition (generic per-run feature flag).
 * Declarative pending assembly verification — loader stores the def and
 * exposes `VcRunToggles` round-trips; native run effects must be verified
 * against game assemblies before claiming in-game behavior.
 * The legacy `"inverse"` preset maps to a toggle with id `"inverse"`.
 */
export interface RunToggleDef {
  /** Unique toggle id, namespaced by mod except the `"inverse"` legacy preset. */
  id: string;
  /** Display name; if omitted derived from id. */
  name?: string;
  /** Human-readable description shown in toggle UI. */
  description?: string;
  /** Initial enabled state before unlock/condition checks. Defaults to false. */
  defaultEnabled?: boolean;
  /** Gate conditions for runtime activation (biome/floor/relics/flags). */
  conditions?: RunToggleCondition;
  /** Relic id that unlocks this toggle (mirrors `RelicDef.unlocksToggle`). */
  unlocksVia?: string;
}

/**
 * Generic run-toggle registry (M7).
 * Declarative pending assembly verification — same status as `VcInverse`;
 * `"inverse"` is the starter/legacy toggle id.
 */
export interface VcRunToggles {
  /** Register a run toggle definition. */
  register(def: RunToggleDef): void;
  /** Patch an existing toggle definition. */
  patch(id: string, partial: Partial<RunToggleDef>): void;
  /** All known run-toggle ids. */
  list(): string[];
  /** True when the owning relic (if any) has been discovered this run/save. */
  isUnlocked(id: string): boolean;
  /** True when the toggle is currently enabled. */
  isEnabled(id: string): boolean;
  /** Enable/disable a toggle (no-op while locked unless forced by tests). */
  setEnabled(id: string, on: boolean): void;
  /** Subscribe to enabled flips for the given toggle id. */
  onChanged(id: string, cb: (on: boolean) => void): void;
  /** Notify the toggle store of a level/biome entry (re-evaluates conditions). */
  checkLevelEnter(biomeId: string, floor: number): void;
  /** True when the toggle is enabled, unlocked, and its conditions match the given location. */
  isActiveAtRuntime(id: string, biomeId: string, floor: number): boolean;
}

// ── runtime namespaces (injected by loader) ──
export interface VcCards {
  register(def: CardDef): void;
  patch(id: string, partial: Partial<CardDef>): void;
  list(): string[];
}
/**
 * Weapon registry.
 * Declarative pending assembly verification (see `WeaponDef`).
 */
export interface VcWeapons {
  register(def: WeaponDef): void;
  patch(id: string, partial: Partial<WeaponDef>): void;
  list(): string[];
}
/**
 * Crawler registry.
 * Declarative pending assembly verification (see `CrawlerDef`).
 */
export interface VcCrawlers {
  register(def: CrawlerDef): void;
  patch(id: string, partial: Partial<CrawlerDef>): void;
  list(): string[];
}
export interface VcGems {
  register(def: GemDef): void;
  patch(id: string, partial: Partial<GemDef>): void;
  list(): string[];
}
export interface VcArcanas {
  register(def: ArcanaDef): void;
  patch(id: string, partial: Partial<ArcanaDef>): void;
  list(): string[];
}
export interface VcRelics {
  register(def: RelicDef): void;
  patch(id: string, partial: Partial<RelicDef>): void;
  list(): string[];
  /** Set a per-run option value (fires `relicOptionChanged` when wired). */
  setOption(relicId: string, key: string, value: RelicOptionValue): void;
  /** Get the current per-run option value (default when unset). */
  getOption(relicId: string, key: string): RelicOptionValue | undefined;
}
export interface VcEnemies {
  register(def: EnemyDef): void;
  patch(id: string, partial: Partial<EnemyDef>): void;
  list(): string[];
}
export interface VcAudio {
  register(def: AudioDef): void;
  play(id: string): void;
}
export interface VcEvolutions {
  register(recipe: EvolutionRecipe): void;
  list(): string[];
}
export interface VcDungeons {
  registerBiome(def: BiomeDef): void;
  patchBiome(id: string, partial: Partial<BiomeDef>): void;
  registerRoom(tpl: RoomTemplate): void;
  patchRoom(id: string, partial: Partial<RoomTemplate>): void;
  listBiomes(): string[];
  listRooms(): string[];
}

// ── maps (tile-grid room maps mirroring game evidence) ──
/**
 * Tile-grid map format mirroring game evidence: room templates + tile grids +
 * props + destructibles + light sources + exits.
 *
 * Biome texture sets (game evidence) are named like `madforest`,
 * `inlaidlibrary`, `dairyplant`, `gallotower`, `capellamagna`, `moongolow`,
 * `tinybridge`. Use one of those values for `MapPackageDef.biomeId` when
 * targeting a vanilla biome, or a custom `BiomeDef.id` for modded biomes.
 * `MapPackageDef.tileMaterials` maps tile keys (used in `RoomMapDef.tiles`
 * rows) to material/texture names within the biome's texture set.
 *
 * Declarative pending assembly verification — same status as `BiomeDef` /
 * `RoomTemplate`: the loader stores the package and exposes `VcMaps`
 * round-trips; native map-generation consumption must be verified against
 * game assemblies before claiming in-game behavior.
 */

/** Known vanilla biome texture-set names (game evidence); custom biomes use any string. */
export type MapBiomeTextureSet =
  | "madforest"
  | "inlaidlibrary"
  | "dairyplant"
  | "gallotower"
  | "capellamagna"
  | "moongolow"
  | "tinybridge"
  | (string & {});

/** Single wall/overlay tile entry. */
export interface MapTile {
  x: number;
  y: number;
  tile: string;
  walkable?: boolean;
}

/** Wall overlay for a room map. */
export interface WallDef {
  tiles: MapTile[];
  material?: string;
}

/** Prop / destructible placement within a room map. */
export interface PropPlacement {
  id: string;
  type: string;
  x: number;
  y: number;
  destructible?: boolean;
  hp?: number;
  drops?: string[];
  interact?: string;
}

/** Light source within a room map. */
export interface LightSourceDef {
  id: string;
  x: number;
  y: number;
  radius?: number;
  color?: string;
  flicker?: boolean;
}

/** Exit / doorway linking to another room. */
export interface MapExit {
  id: string;
  x: number;
  y: number;
  toRoom?: string;
  direction?: string;
}

/**
 * Single room map: fixed-size tile grid plus props, lights, and exits.
 * `tiles` holds `height` rows of tile keys; each row should be `width`
 * entries long (keys resolved via `MapPackageDef.tileMaterials`).
 */
export interface RoomMapDef {
  id: string;
  name?: string;
  width: number;
  height: number;
  /** Rows of tile keys (`height` rows, each `width` keys long). */
  tiles: string[];
  walls?: WallDef;
  props?: PropPlacement[];
  lights?: LightSourceDef[];
  exits?: MapExit[];
  encounterTable?: { enemyId: string; weight: number }[];
}

/**
 * Map package: one biome's worth of room maps sharing a tile-material set.
 * `biomeId` is a `BiomeDef.id` (vanilla texture sets: `madforest`,
 * `inlaidlibrary`, `dairyplant`, `gallotower`, `capellamagna`, `moongolow`,
 * `tinybridge`) and `tileMaterials` maps tile keys to material/texture names.
 */
export interface MapPackageDef {
  id: string;
  name?: string;
  biomeId: string;
  rooms: RoomMapDef[];
  tileMaterials?: Record<string, string>;
}

/**
 * Tile-grid map registry.
 * Declarative pending assembly verification (see map-format note above).
 */
export interface VcMaps {
  registerMap(pkg: MapPackageDef): void;
  patchMap(id: string, partial: Partial<MapPackageDef>): void;
  listMaps(): string[];
}
export interface VcState {
  getMana(): number;
  getCombo(): number;
  getMight(): number;
  /** True when inverse run mode is currently enabled. */
  isInverse(): boolean;
  /** Snapshot of active run flags/modifiers. */
  getRunFlags(): RunFlags;
}
export interface VcHooks {
  on(event: HookEvent, cb: (...args: unknown[]) => void): void;
}

export interface DevConsoleContext {
  log: (...args: unknown[]) => void;
  api: VcApi;
}

export interface DevConsoleCommand {
  name: string;
  description?: string;
  usage?: string;
  handler: (args: string[], ctx: DevConsoleContext) => void | Promise<void>;
}

export interface VcConsole {
  /** Register a console command (triggered via ` key). Name is without leading `/` or `!`. */
  register(cmd: DevConsoleCommand): void;
  /** Same, but shorthand: name, description, handler. */
  register(name: string, description: string, handler: DevConsoleCommand["handler"]): void;
  /** Write a line to the console + BepInEx log. */
  log(...args: unknown[]): void;
  /** Clear console buffer. */
  clear(): void;
  /** Open/close/toggle programmatically. */
  open(): void;
  close(): void;
  toggle(): void;
  isOpen(): boolean;
}

// ── settings ──
/** Value type for per-mod settings. */
export type SettingValue = string | number | boolean;

/**
 * A single per-mod setting definition.
 * Settings tabs surface in the settings menu; entries are controller-navigable;
 * each mod declares its own per-mod options via `vc.settings.register`.
 * Declarative pending assembly verification — the loader stores definitions and
 * per-run values; native settings-menu wiring must be verified against game assemblies.
 */
export interface SettingDef {
  /** Unique setting id, namespaced by mod: "demo-mod:damage_multiplier" */
  id: string;
  /** Back-compat alias for `id` (option key within the mod). When both are present, `id` wins. */
  key?: string;
  /** Human-readable label for in-game UI. */
  label: string;
  /** Settings-menu tab this setting appears under (omit = mod default tab). */
  tab?: string;
  /** Control kind: on/off switch, fixed choice, or numeric slider. */
  kind: "toggle" | "choice" | "slider";
  /** Default value applied before the user overrides it. */
  default: SettingValue;
  /** Allowed values for `choice` kind. */
  choices?: SettingValue[];
  /** Minimum value for `slider` kind. */
  min?: number;
  /** Maximum value for `slider` kind. */
  max?: number;
  /** Help text shown under the setting. */
  description?: string;
}

/**
 * Per-mod settings registry (controller-navigable settings menu).
 * Settings tabs surface in the settings menu; entries are controller-navigable;
 * each mod owns its per-mod options via this namespace.
 * Declarative pending assembly verification — same status as `SettingDef`.
 */
export interface VcSettings {
  /** Register a per-mod setting definition (surfaced under its settings tab). */
  register(def: SettingDef): void;
  /** Set a per-mod setting value. */
  setSetting(id: string, value: SettingValue): void;
  /** Get the current per-mod setting value (default when unset). */
  getSetting(id: string): SettingValue | undefined;
  /** List registered setting ids, optionally filtered by settings-menu tab. */
  listSettings(tab?: string): string[];
}

export interface VcApi {
  meta: ModMeta;
  cards: VcCards;
  weapons: VcWeapons;
  crawlers: VcCrawlers;
  gems: VcGems;
  arcanas: VcArcanas;
  relics: VcRelics;
  enemies: VcEnemies;
  audio: VcAudio;
  evolutions: VcEvolutions;
  dungeons: VcDungeons;
  /** Tile-grid map registry (room templates + tile grids + props + lights + exits). Optional for backward compat with older loaders/shims. */
  maps?: VcMaps;
  state: VcState;
  hooks: VcHooks;
  inverse: VcInverse;
  runModes: VcRunModes;
  /** Generic run-toggles registry; `"inverse"` id mirrors the legacy `inverse` preset. Optional for backward compat with older loaders/shims. */
  toggles?: VcRunToggles;
  console: VcConsole;
  /** Per-mod settings registry (controller-navigable settings-menu tabs). */
  settings: VcSettings;
  log(...args: unknown[]): void;
}

// Global injected by loader; typed import is for tooling.
declare global {
  var vc: VcApi;
}
export const vc: VcApi = globalThis.vc;
