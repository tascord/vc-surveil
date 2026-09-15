/**
 * @vc-surveil/api — TypeScript modding API for Vampire Crawlers (Pancake).
 * Loader injects a global `vc` matching these types into each mod's Jint/V8 realm.
 * Mods `import { vc } from "@vc-surveil/api"` for type-checking only; at runtime use the global.
 */

// ── common ──
export type ManaCost = number | "W" | "X"; // W = wild/free, X = remaining mana
export type CardColor = "attack" | "defense" | "stat" | "mana" | "wild" | "temporary" | "character" | "crawler";
export type Rarity = "Common" | "Uncommon" | "Rare" | "VeryRare" | "UltraRare";
export type TriggerColor = "red" | "blue" | "yellow" | "purple" | "rainbow";

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
  /** Crawler trigger: e.g. { when: "red", effect: "Deal 10% more damage" } */
  crawlerTrigger?: { when: TriggerColor | string; effect: string };

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

export interface ArcanaDef {
  id: string;
  name?: string;
  text: string;
  description?: string;
  icon?: string;
  iconPath?: string;
}

export interface RelicDef {
  id: string;
  name?: string;
  text: string;
  /** Dungeon id where this relic is discovered, if any. */
  foundIn?: string;
}

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
}

export interface EvolutionRecipe {
  primary: string; // card id
  secondary: string | string[]; // card id or alternatives
  result: string; // card id
}

export interface BiomeDef {
  id: string;
  name?: string;
  floors: number;
  difficulty?: number;
}

export interface RoomTemplate {
  id: string;
  weight?: number;
  tags?: string[];
}

export interface AudioDef {
  id: string;
  path: string;
}

// ── runtime namespaces (injected by loader) ──
export interface VcCards {
  register(def: CardDef): void;
  patch(id: string, partial: Partial<CardDef>): void;
  list(): string[];
}
export interface VcGems { register(def: GemDef): void; }
export interface VcArcanas { register(def: ArcanaDef): void; }
export interface VcRelics { register(def: RelicDef): void; }
export interface VcEnemies { register(def: EnemyDef): void; }
export interface VcAudio {
  register(def: AudioDef): void;
  play(id: string): void;
}
export interface VcEvolutions { register(recipe: EvolutionRecipe): void; }
export interface VcDungeons {
  registerBiome(def: BiomeDef): void;
  registerRoom(tpl: RoomTemplate): void;
}
export interface VcState {
  getMana(): number;
  getCombo(): number;
  getMight(): number;
}
export interface VcHooks {
  on(event: "init" | "dungeonEnter" | "encounterStart" | "cardPlayed" | "beforeSave" | "afterLoad", cb: (...args: unknown[]) => void): void;
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

export interface VcApi {
  meta: ModMeta;
  cards: VcCards;
  gems: VcGems;
  arcanas: VcArcanas;
  relics: VcRelics;
  enemies: VcEnemies;
  audio: VcAudio;
  evolutions: VcEvolutions;
  dungeons: VcDungeons;
  state: VcState;
  hooks: VcHooks;
  console: VcConsole;
  log(...args: unknown[]): void;
}

// Global injected by loader; typed import is for tooling.
declare global {
  var vc: VcApi;
}
export const vc: VcApi = globalThis.vc;
