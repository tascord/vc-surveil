/**
 * demo-mod — full showcase for the Vampire Crawlers TS modding API.
 * Install (`surveil install` demo): after `npm run build:demo` copy
 * `mods/demo-mod/` (mod.json + dist/) to the game's `Mods/demo-mod/` and
 * launch with PancakeModLoader (BepInEx IL2CPP).
 *
 * Types come from @vc-surveil/api; at runtime `globalThis.vc` is injected by the loader.
 * Anything beyond cards is a DECLARATIVE showcase pending assembly verification:
 * the loader stores these defs and round-trips them via list/patch, but native
 * in-game effects (draft pools, map gen, run-mode mutation) are not claimed.
 */
import type {
  ArcanaDef,
  BiomeDef,
  CardDef,
  CrawlerDef,
  DevConsoleContext,
  EnemyDef,
  EvolutionRecipe,
  GemDef,
  HookEvent,
  MapPackageDef,
  RelicDef,
  RelicOptionValue,
  RoomTemplate,
  RunToggleDef,
  SettingDef,
  SettingValue,
  VcApi,
  WeaponDef,
} from "@vc-surveil/api";

declare global {
  var vc: VcApi;
}

// Loader injects `vc` into globalThis at runtime. Fallback shim lets local `node`
// exercise the file (covers every VcApi namespace used below).
function makeListStore<T extends { id: string }>() {
  const items: T[] = [];
  return {
    items,
    register(def: T) {
      const i = items.findIndex((x) => x.id === def.id);
      if (i >= 0) items[i] = def;
      else items.push(def);
    },
    patch(id: string, p: Partial<T>) {
      const f = items.find((x) => x.id === id);
      if (f) Object.assign(f, p);
    },
    list: () => items.map((x) => x.id),
  };
}
const fallbackCards = makeListStore<CardDef>();
const fallbackWeapons = makeListStore<WeaponDef>();
const fallbackCrawlers = makeListStore<CrawlerDef>();
const fallbackGems = makeListStore<{ id: string }>();
const fallbackArcanas = makeListStore<ArcanaDef>();
const fallbackRelics = makeListStore<RelicDef>();
const fallbackEnemies = makeListStore<EnemyDef>();
const fallbackBiomes = makeListStore<BiomeDef>();
const fallbackRooms = makeListStore<RoomTemplate>();
const fallbackMaps = makeListStore<MapPackageDef>();
const fallbackRecipes: EvolutionRecipe[] = [];
const fallbackOptions = new Map<string, RelicOptionValue>();
const fallbackSettingDefs = makeListStore<SettingDef>();
const fallbackSettingValues = new Map<string, SettingValue>();
let fallbackInverse = false;
const fallbackInverseListeners: Array<(enabled: boolean) => void> = [];
const fallbackToggleDefs = makeListStore<RunToggleDef>();
const fallbackToggleState = new Map<string, boolean>();
const fallbackToggleListeners = new Map<string, Array<(on: boolean) => void>>();
function fallbackToggleDef(id: string): RunToggleDef | undefined {
  return fallbackToggleDefs.items.find((t) => t.id === id);
}
function fallbackIsToggleUnlocked(id: string): boolean {
  const def = fallbackToggleDef(id);
  if (!def) return false;
  const requiresRelics = def.conditions?.requiresRelics ?? [];
  const unlocksVia = def.unlocksVia;
  if (!unlocksVia && requiresRelics.length === 0) return true;
  const owned = new Set(fallbackRelics.list());
  if (unlocksVia && owned.has(unlocksVia)) return true;
  for (const r of requiresRelics) if (owned.has(r)) return true;
  return false;
}
function fallbackIsToggleEnabled(id: string): boolean {
  if (fallbackToggleState.has(id)) return fallbackToggleState.get(id) as boolean;
  return fallbackToggleDef(id)?.defaultEnabled ?? false;
}
function fallbackIsActiveAtRuntime(id: string, biomeId: string, floor: number): boolean {
  const def = fallbackToggleDef(id);
  if (!def) return false;
  if (!fallbackIsToggleEnabled(id)) return false;
  if (!fallbackIsToggleUnlocked(id)) return false;
  const cond = def.conditions;
  if (!cond) return true;
  if (cond.biomes && cond.biomes.length > 0 && !cond.biomes.includes(biomeId)) return false;
  if (cond.minFloor !== undefined && floor < cond.minFloor) return false;
  if (cond.maxFloor !== undefined && floor > cond.maxFloor) return false;
  return true;
}
const vc: VcApi = globalThis.vc ?? {
  meta: { id: "demo-mod", version: "0.1.0" },
  log: (...a: unknown[]) => console.log("[demo-mod]", ...a),
  cards: {
    register(d: CardDef) { fallbackCards.register(d); console.log("[demo-mod] register card", d.id); },
    patch(id: string, p: Partial<CardDef>) { fallbackCards.patch(id, p); },
    list: () => fallbackCards.list(),
  },
  weapons: {
    register(d: WeaponDef) { fallbackWeapons.register(d); console.log("[demo-mod] register weapon", d.id); },
    patch(id: string, p: Partial<WeaponDef>) { fallbackWeapons.patch(id, p); },
    list: () => fallbackWeapons.list(),
  },
  crawlers: {
    register(d: CrawlerDef) { fallbackCrawlers.register(d); console.log("[demo-mod] register crawler", d.id); },
    patch(id: string, p: Partial<CrawlerDef>) { fallbackCrawlers.patch(id, p); },
    list: () => fallbackCrawlers.list(),
  },
  gems: {
    register(d) { fallbackGems.register(d); },
    patch(id: string, p: Partial<GemDef>) { fallbackGems.patch(id, p); },
    list: () => fallbackGems.list(),
  },
  arcanas: {
    register(d: ArcanaDef) { fallbackArcanas.register(d); console.log("[demo-mod] register arcana", d.id); },
    patch(id: string, p: Partial<ArcanaDef>) { fallbackArcanas.patch(id, p); },
    list: () => fallbackArcanas.list(),
  },
  relics: {
    register(d: RelicDef) { fallbackRelics.register(d); console.log("[demo-mod] register relic", d.id); },
    patch(id: string, p: Partial<RelicDef>) { fallbackRelics.patch(id, p); },
    list: () => fallbackRelics.list(),
    setOption(relicId: string, key: string, value: RelicOptionValue) {
      fallbackOptions.set(`${relicId}:${key}`, value);
    },
    getOption(relicId: string, key: string) {
      if (fallbackOptions.has(`${relicId}:${key}`)) return fallbackOptions.get(`${relicId}:${key}`);
      const def = fallbackRelics.items.find((r) => r.id === relicId);
      return def?.defaultOptions?.[key] ?? def?.options?.find((o) => o.key === key)?.default;
    },
  },
  enemies: {
    register(d: EnemyDef) { fallbackEnemies.register(d); console.log("[demo-mod] register enemy", d.id); },
    patch(id: string, p: Partial<EnemyDef>) { fallbackEnemies.patch(id, p); },
    list: () => fallbackEnemies.list(),
  },
  audio: { register() {}, play() {} },
  evolutions: {
    register(r: EvolutionRecipe) { fallbackRecipes.push(r); },
    list: () => fallbackRecipes.map((r) => r.result),
  },
  dungeons: {
    registerBiome(d: BiomeDef) { fallbackBiomes.register(d); console.log("[demo-mod] register biome", d.id); },
    patchBiome(id: string, p: Partial<BiomeDef>) { fallbackBiomes.patch(id, p); },
    registerRoom(t: RoomTemplate) { fallbackRooms.register(t); console.log("[demo-mod] register room", t.id); },
    patchRoom(id: string, p: Partial<RoomTemplate>) { fallbackRooms.patch(id, p); },
    listBiomes: () => fallbackBiomes.list(),
    listRooms: () => fallbackRooms.list(),
  },
  maps: {
    registerMap(pkg: MapPackageDef) { fallbackMaps.register(pkg); console.log("[demo-mod] register map", pkg.id); },
    patchMap(id: string, p: Partial<MapPackageDef>) { fallbackMaps.patch(id, p); },
    listMaps: () => fallbackMaps.list(),
  },
  state: {
    getMana: () => 0,
    getCombo: () => 0,
    getMight: () => 0,
    isInverse: () => fallbackInverse,
    getRunFlags: () => ({ inverse: fallbackInverse }),
  },
  hooks: {
    on(ev: HookEvent, cb: (...args: unknown[]) => void) { console.log("[demo-mod] hooks.on", ev); void cb; },
  },
  inverse: {
    isUnlocked: () => fallbackRelics.list().some((id) => fallbackRelics.items.find((r) => r.id === id)?.unlocksToggle === "inverse"),
    isEnabled: () => fallbackInverse,
    setEnabled(b: boolean) {
      fallbackInverse = b;
      console.log("[demo-mod] inverse setEnabled", b);
      for (const cb of fallbackInverseListeners) cb(b);
    },
    onToggle(cb: (enabled: boolean) => void) { fallbackInverseListeners.push(cb); },
  },
  runModes: {
    list: () => ["inverse"],
    isActive: (id: string) => (id === "inverse" ? fallbackInverse : false),
    setActive(id: string, enabled: boolean) { if (id === "inverse") fallbackInverse = enabled; },
  },
  toggles: {
    register(d: RunToggleDef) { fallbackToggleDefs.register(d); console.log("[demo-mod] register toggle", d.id); },
    patch(id: string, p: Partial<RunToggleDef>) { fallbackToggleDefs.patch(id, p); },
    list: () => fallbackToggleDefs.list(),
    isUnlocked: (id: string) => fallbackIsToggleUnlocked(id),
    isEnabled: (id: string) => fallbackIsToggleEnabled(id),
    setEnabled(id: string, on: boolean) {
      fallbackToggleState.set(id, on);
      console.log("[demo-mod] toggle setEnabled", id, on);
      for (const cb of fallbackToggleListeners.get(id) ?? []) cb(on);
    },
    onChanged(id: string, cb: (on: boolean) => void) {
      const arr = fallbackToggleListeners.get(id) ?? [];
      arr.push(cb);
      fallbackToggleListeners.set(id, arr);
    },
    checkLevelEnter(biomeId: string, floor: number) {
      const active = fallbackToggleDefs.list().filter((id) => fallbackIsActiveAtRuntime(id, biomeId, floor));
      console.log("[demo-mod] checkLevelEnter", biomeId, floor, `active=[${active.join(",")}]`);
    },
    isActiveAtRuntime: (id: string, biomeId: string, floor: number) => fallbackIsActiveAtRuntime(id, biomeId, floor),
  },
  settings: {
    register(d: SettingDef) { fallbackSettingDefs.register(d); console.log("[demo-mod] register setting", d.id); },
    setSetting(id: string, value: SettingValue) { fallbackSettingValues.set(id, value); },
    getSetting(id: string) {
      if (fallbackSettingValues.has(id)) return fallbackSettingValues.get(id);
      return fallbackSettingDefs.items.find((s) => s.id === id)?.default;
    },
    listSettings: (tab?: string) =>
      tab === undefined
        ? fallbackSettingDefs.list()
        : fallbackSettingDefs.items.filter((s) => s.tab === tab).map((s) => s.id),
  },
  console: {
    register(..._args: unknown[]) {},
    log(...a: unknown[]) { console.log("[demo-mod.console]", ...a); },
    clear() {},
    open() {},
    close() {},
    toggle() {},
    isOpen() { return false; },
  },
};

const cards: CardDef[] = [
  {
    id: "demo-mod:fun",
    name: "Fun time with friends :)",
    color: "defense",
    mana: -10,
    text: ":P",
    gemSlots: 2,
    iconKey: "Armor",
    effects: { heal: 1500, armor: 1000000, draw: 12, damage: 12600 },
    rarity: "Common",
    weight: 300,
    target: { scope: "front_row" },
    drop: { pools: ["levelUp"], weight: 120 },
  },
  {
    id: "demo-mod:tarot_fool",
    name: "The Fool's Gambit",
    color: "attack",
    mana: 1,
    text: "Custom card body + skull icon test. Deal 50 damage to front row + Knockback.",
    gemSlots: 3,
    body: "assets/custom_body.jpg",
    icon: "assets/skull_icon.png",
    damage: 50,
    target: { scope: "front_row" },
    effects: { damage: 50, knockback: true },
    rarity: "Uncommon",
    weight: 150,
    drop: { pools: ["levelUp", "chestT1"], weight: 150 },
  },
  {
    id: "demo-mod:tarot_magician",
    name: "The Magician",
    color: "mana",
    mana: 0,
    text: "Custom tarot body + Magician art. Gain 3 mana, draw 3 cards, +1 Combo.",
    gemSlots: 2,
    body: "assets/custom_body.jpg",
    icon: "assets/custom_icon.jpg",
    effects: { mana: 3, draw: 3, combo: 1 },
    rarity: "Rare",
    weight: 100,
    drop: { pools: ["levelUp", "chestT2"], weight: 100 },
  },
  {
    id: "demo-mod:tarot_devil",
    name: "The Devil",
    color: "wild",
    mana: "W",
    freeToPlay: true,
    text: "Custom tarot body + Devil art. Freeze 3 stacks, disarm, and deal 66 damage to all enemies.",
    gemSlots: 3,
    body: "assets/custom_body.jpg",
    icon: "assets/devil_art.jpg",
    target: { scope: "all" },
    effects: { freeze: 3, disarm: true, damage: 66, combo: 2 },
    rarity: "UltraRare",
    weight: 50,
    drop: { pools: ["chestT3"], weight: 50 },
  },
  {
    id: "demo-mod:friendship_amulet",
    name: "Friendship Amulet",
    color: "wild",
    mana: "W",
    freeToPlay: true,
    retain: true,
    text: "Retain. Free to play. Increases combo counter by +2.",
    gemSlots: 1,
    icon: "assets/friendship_amulet.png",
    effects: { combo: 2 },
    rarity: "Rare",
    weight: 100,
    drop: { pools: ["levelUp", "chestT2"], weight: 100 },
  },
  {
    id: "demo-mod:combo_twist",
    name: "Combo Twist",
    color: "wild",
    mana: 1,
    text: "Reverse combo direction. Increase combo by +1. Draw 2 cards.",
    gemSlots: 2,
    icon: "assets/skull_icon.png",
    effects: { combo: 1, reverseCombo: true, draw: 2 },
    rarity: "Uncommon",
    weight: 120,
    drop: { pools: ["levelUp", "chestT1"], weight: 120 },
  },
  // M8-T1: minecart sweep showcase card (targeting + effects).
  {
    id: "demo-mod:minecart_rush",
    name: "Minecart Rush",
    color: "attack",
    mana: 2,
    text: "Minecart sweep across the front row. Deals damage + knockback.",
    gemSlots: 1,
    damage: 25,
    target: { scope: "front_row" },
    effects: { minecart: { damage: 25, knockback: true, sound: true }, knockback: true },
    rarity: "Uncommon",
    weight: 120,
    drop: { pools: ["levelUp", "chestT1"], weight: 120 },
  },
  // M8-T2: crawler-linked card (crawlerId + inline crawlerTrigger).
  {
    id: "demo-mod:ember_strike",
    name: "Ember Strike",
    color: "crawler",
    mana: 1,
    text: "Ember Crawler attack. Triggers on red: deal 10% more damage.",
    damage: 20,
    gemSlots: 1,
    crawlerId: "demo-mod:ember_crawler",
    crawlerTrigger: { when: "red", effect: "Deal 10% more damage" },
    target: { scope: "single" },
    effects: { damage: 20 },
    rarity: "Common",
    weight: 150,
    drop: { pools: ["levelUp"], weight: 150 },
  },
  // M8-T1: weapon-granted card (weaponId + evolution link).
  {
    id: "demo-mod:rusty_swing",
    name: "Rusty Swing",
    color: "attack",
    mana: 1,
    text: "Basic swing of the Rusty Whip. Evolves with Garlic into True Whip.",
    damage: 15,
    gemSlots: 1,
    weaponId: "demo-mod:rusty_whip",
    evolution: { primary: "demo-mod:rusty_swing", secondary: "Garlic", result: "demo-mod:true_whip" },
    target: { scope: "single" },
    effects: { damage: 15 },
    rarity: "Common",
    weight: 150,
    drop: { pools: ["levelUp", "chestT1"], weight: 150 },
  },
];

// M8-T1: 1 weapon + 1 evolution recipe.
const weapons: WeaponDef[] = [
  {
    id: "demo-mod:rusty_whip",
    name: "Rusty Whip",
    text: "A worn but honest whip. Starter weapon demo (declarative).",
    damage: 15,
    color: "attack",
    icon: "assets/rusty_whip.png",
    rarity: "Common",
    drop: { pools: ["levelUp", "chestT1"], weight: 150 },
    evolutionFrom: "Whip",
  },
];
const evolutions: EvolutionRecipe[] = [
  { primary: "demo-mod:rusty_swing", secondary: "Garlic", result: "demo-mod:true_whip" },
];

// M8-T2: 1 crawler.
const crawlers: CrawlerDef[] = [
  {
    id: "demo-mod:ember_crawler",
    name: "Ember Crawler",
    text: "A scrappy fire crawler. Red triggers burn brighter (declarative).",
    colors: ["attack", "crawler"],
    trigger: { when: "red", effect: "Deal 10% more damage", oncePerTurn: false },
    baseStats: { health: 100, mana: 3, might: 1 },
    icon: "assets/ember_crawler.png",
  },
];

// M8-T3: 1 arcana with icon + modifier + hook.
const arcanas: ArcanaDef[] = [
  {
    id: "demo-mod:arcana_spark",
    name: "Spark of the Forge",
    text: "Your attacks burn a little hotter.",
    description: "Might +2 while held. Listens to encounterStart (declarative).",
    icon: "assets/arcana_spark.png",
    rarity: "Rare",
    unlockId: "demo-mod:basement",
    modifiers: { might: 2 },
    hooks: ["encounterStart"],
  },
];

// M8-T4: 1 relic with foundIn + 2 options (toggle + choice) + unlocksToggle inverse.
const relics: RelicDef[] = [
  {
    id: "demo-mod:brass_lantern",
    name: "Brass Lantern",
    text: "Lights dark halls. Configurable brightness (declarative).",
    foundIn: "demo-mod:ember_depths",
    icon: "assets/brass_lantern.png",
    rarity: "Uncommon",
    options: [
      { key: "lit", label: "Lit", kind: "toggle", default: true, description: "Whether the lantern burns." },
      {
        key: "brightness",
        label: "Brightness",
        kind: "choice",
        default: "dim",
        choices: ["dim", "bright", "blinding"],
        description: "How far the light reaches.",
      },
    ],
    defaultOptions: { lit: true, brightness: "dim" },
    effects: { heal: 5 },
    unlocksToggle: "inverse",
  },
  // M8-T7: inverse-unlocking relic.
  {
    id: "demo-mod:inverse_relic",
    name: "Inverted Mirror",
    text: "A mirror that shows the halls backwards. Unlocks the Inverse toggle (declarative).",
    foundIn: "demo-mod:ember_depths",
    icon: "assets/inverse_mirror.png",
    rarity: "VeryRare",
    unlocksToggle: "inverse",
  },
];

// M8-T5: 1 biome (2 floors) + 2 room templates.
const biomes: BiomeDef[] = [
  {
    id: "demo-mod:ember_depths",
    name: "Ember Depths",
    floors: 2,
    difficulty: 2,
    background: "assets/ember_bg.png",
    music: "demo-mod:ember_theme",
    encounterTable: [
      { enemyId: "demo-mod:soot_imp", weight: 100, floors: [1, 2] },
      { enemyId: "demo-mod:cinder_brute", weight: 40, floors: [2] },
    ],
    bossFloor: 2,
    modifiers: { darkness: 1 },
  },
];
const rooms: RoomTemplate[] = [
  {
    id: "demo-mod:ember_hall",
    weight: 100,
    tags: ["combat", "ember"],
    biomeId: "demo-mod:ember_depths",
    minFloor: 1,
    maxFloor: 1,
    enemies: ["demo-mod:soot_imp"],
    reward: { coins: 10, xp: 5 },
  },
  {
    id: "demo-mod:ember_shrine",
    weight: 30,
    tags: ["treasure", "ember"],
    biomeId: "demo-mod:ember_depths",
    minFloor: 2,
    maxFloor: 2,
    enemies: [],
    reward: { coins: 50, xp: 20, cards: ["demo-mod:ember_strike"] },
  },
];

// M8-T6: 1 normal + 1 boss enemy with sprite sheets + drops.
const enemies: EnemyDef[] = [
  {
    id: "demo-mod:soot_imp",
    name: "Soot Imp",
    health: 30,
    xp: 5,
    maxHit: 8,
    spritePath: "assets/soot_imp.png",
    frames: 4,
    fps: 8,
    behavior: { attackPattern: "melee", moveSpeed: 1, spawnWeight: 100, floors: [1, 2], biomes: ["demo-mod:ember_depths"] },
    stats: { damage: 8, speed: 1 },
    drops: { coins: 3, xp: 5, cards: ["demo-mod:ember_strike"] },
  },
  {
    id: "demo-mod:cinder_brute",
    name: "Cinder Brute",
    health: 300,
    xp: 60,
    boss: true,
    maxHit: 40,
    maxArmor: 10,
    spritePath: "assets/cinder_brute.png",
    frames: 6,
    fps: 6,
    behavior: { attackPattern: "slam", moveSpeed: 0.5, spawnWeight: 40, floors: [2], biomes: ["demo-mod:ember_depths"] },
    stats: { damage: 40, armor: 10, speed: 0.5 },
    drops: { coins: 100, xp: 60, cards: ["demo-mod:tarot_devil"] },
  },
];

// Showcase settings: 2 settings, one in the "Demo" tab.
const settings: SettingDef[] = [
  {
    id: "demo-mod:damage_multiplier",
    label: "Damage Multiplier",
    tab: "Demo",
    kind: "slider",
    default: 1,
    min: 0.5,
    max: 2,
    description: "Scales demo showcase damage (declarative).",
  },
  {
    id: "demo-mod:hard_mode",
    label: "Hard Mode",
    kind: "toggle",
    default: false,
    description: "Enable extra-spicy demo encounters (declarative).",
  },
];

export function register() {
  for (const c of cards) vc.cards.register(c);
  if (vc.weapons) {
    for (const w of weapons) vc.weapons.register(w);
  }
  if (vc.evolutions) {
    for (const r of evolutions) vc.evolutions.register(r);
  }
  if (vc.crawlers) {
    for (const c of crawlers) vc.crawlers.register(c);
  }
  if (vc.arcanas) {
    for (const a of arcanas) vc.arcanas.register(a);
  }
  if (vc.relics) {
    for (const r of relics) vc.relics.register(r);
  }
  if (vc.dungeons) {
    for (const b of biomes) vc.dungeons.registerBiome(b);
    for (const t of rooms) vc.dungeons.registerRoom(t);
  }
  // Ember Depths map package (mirrors assets/ember-depths-map.json).
  if (vc.maps) {
    const emberDepthsMap: MapPackageDef = {
      id: "demo-mod:ember_depths_map",
      biomeId: "demo-mod:ember_depths",
      tileMaterials: {
        floor: "ember_floor",
        wall: "ember_wall",
      },
      rooms: [
        {
          id: "demo-mod:ember_hall",
          width: 8,
          height: 6,
          tiles: ["########", "#......#", "#......#", "#......#", "#......#", "########"],
          walls: {
            material: "ember_wall",
            tiles: [
              { x: 0, y: 0, tile: "wall", walkable: false },
              { x: 1, y: 0, tile: "wall", walkable: false },
              { x: 2, y: 0, tile: "wall", walkable: false },
              { x: 3, y: 0, tile: "wall", walkable: false },
              { x: 4, y: 0, tile: "wall", walkable: false },
              { x: 5, y: 0, tile: "wall", walkable: false },
              { x: 6, y: 0, tile: "wall", walkable: false },
              { x: 7, y: 0, tile: "wall", walkable: false },
              { x: 0, y: 5, tile: "wall", walkable: false },
              { x: 1, y: 5, tile: "wall", walkable: false },
              { x: 2, y: 5, tile: "wall", walkable: false },
              { x: 3, y: 5, tile: "wall", walkable: false },
              { x: 4, y: 5, tile: "wall", walkable: false },
              { x: 5, y: 5, tile: "wall", walkable: false },
              { x: 6, y: 5, tile: "wall", walkable: false },
              { x: 7, y: 5, tile: "wall", walkable: false },
              { x: 0, y: 1, tile: "wall", walkable: false },
              { x: 0, y: 2, tile: "wall", walkable: false },
              { x: 0, y: 3, tile: "wall", walkable: false },
              { x: 0, y: 4, tile: "wall", walkable: false },
              { x: 7, y: 1, tile: "wall", walkable: false },
              { x: 7, y: 2, tile: "wall", walkable: false },
              { x: 7, y: 3, tile: "wall", walkable: false },
              { x: 7, y: 4, tile: "wall", walkable: false },
            ],
          },
          exits: [{ id: "hall_exit", x: 7, y: 3, toRoom: "demo-mod:ember_shrine", direction: "shrine_exit_back" }],
          props: [
            { id: "crate_1", type: "crate", x: 2, y: 2, destructible: true, hp: 10 },
            { id: "brazier_1", type: "brazier", x: 5, y: 2 },
            { id: "cart_1", type: "cart", x: 4, y: 4 },
          ],
          lights: [{ id: "brazier_light", x: 5, y: 2, color: "orange", radius: 5 }],
        },
        {
          id: "demo-mod:ember_shrine",
          width: 6,
          height: 6,
          tiles: ["######", "#....#", "#....#", "#....#", "#....#", "######"],
          walls: {
            material: "ember_wall",
            tiles: [
              { x: 0, y: 0, tile: "wall", walkable: false },
              { x: 1, y: 0, tile: "wall", walkable: false },
              { x: 2, y: 0, tile: "wall", walkable: false },
              { x: 3, y: 0, tile: "wall", walkable: false },
              { x: 4, y: 0, tile: "wall", walkable: false },
              { x: 5, y: 0, tile: "wall", walkable: false },
              { x: 0, y: 5, tile: "wall", walkable: false },
              { x: 1, y: 5, tile: "wall", walkable: false },
              { x: 2, y: 5, tile: "wall", walkable: false },
              { x: 3, y: 5, tile: "wall", walkable: false },
              { x: 4, y: 5, tile: "wall", walkable: false },
              { x: 5, y: 5, tile: "wall", walkable: false },
              { x: 0, y: 1, tile: "wall", walkable: false },
              { x: 0, y: 2, tile: "wall", walkable: false },
              { x: 0, y: 3, tile: "wall", walkable: false },
              { x: 0, y: 4, tile: "wall", walkable: false },
              { x: 5, y: 1, tile: "wall", walkable: false },
              { x: 5, y: 2, tile: "wall", walkable: false },
              { x: 5, y: 3, tile: "wall", walkable: false },
              { x: 5, y: 4, tile: "wall", walkable: false },
            ],
          },
          exits: [{ id: "shrine_exit_back", x: 0, y: 3, toRoom: "demo-mod:ember_hall", direction: "hall_exit" }],
          props: [
            {
              id: "chest_1",
              type: "chest",
              x: 2,
              y: 2,
              interact: "open",
              drops: ["demo-mod:ember_strike"],
            },
            { id: "torch_1", type: "torch", x: 1, y: 1 },
            { id: "torch_2", type: "torch", x: 4, y: 1 },
          ],
          lights: [
            { id: "torch_light_1", x: 1, y: 1, color: "orange", radius: 4 },
            { id: "torch_light_2", x: 4, y: 1, color: "orange", radius: 4 },
          ],
        },
      ],
    };
    vc.maps.registerMap(emberDepthsMap);
    vc.log(`maps: registered ${emberDepthsMap.id} for ${emberDepthsMap.biomeId} (${emberDepthsMap.rooms.length} rooms)`);
  }
  if (vc.enemies) {
    for (const e of enemies) vc.enemies.register(e);
  }
  if (vc.settings) {
    for (const s of settings) vc.settings.register(s);
    vc.log(`settings: ${vc.settings.listSettings().join(", ")} (Demo tab: ${vc.settings.listSettings("Demo").join(", ")})`);
  }
  vc.log(
    `demo-mod showcase: ${cards.length} cards, ${weapons.length} weapons, ${crawlers.length} crawlers, ` +
      `${arcanas.length} arcanas, ${relics.length} relics, ${biomes.length} biomes, ` +
      `${rooms.length} rooms, ${enemies.length} enemies, ${evolutions.length} evolutions, ` +
      `${settings.length} settings`,
  );

  // M8-T4: relic setOption/getOption round-trip log.
  if (vc.relics) {
    vc.relics.setOption("demo-mod:brass_lantern", "brightness", "bright");
    vc.log(`lantern brightness: ${String(vc.relics.getOption("demo-mod:brass_lantern", "brightness"))}`);
    vc.log(`lantern lit (default): ${String(vc.relics.getOption("demo-mod:brass_lantern", "lit"))}`);
  }

  // M8-T7: inverse status log + onToggle log + observable run-flag change.
  if (vc.inverse) {
    vc.log(`inverse unlocked: ${String(vc.inverse.isUnlocked())} (relic demo-mod:brass_lantern unlocksToggle=inverse)`);
    vc.inverse.onToggle((enabled) => vc.log(`inverse onToggle → enabled=${String(enabled)}`));
    if (vc.state) {
      vc.inverse.setEnabled(true);
      vc.log(`inverse on → isEnabled=${String(vc.inverse.isEnabled())} flags=${JSON.stringify(vc.state.getRunFlags())}`);
      vc.inverse.setEnabled(false);
      vc.log(`inverse off → isEnabled=${String(vc.inverse.isEnabled())} flags=${JSON.stringify(vc.state.getRunFlags())}`);
    } else {
      vc.inverse.setEnabled(true);
      vc.log(`inverse on → isEnabled=${String(vc.inverse.isEnabled())}`);
      vc.inverse.setEnabled(false);
      vc.log(`inverse off → isEnabled=${String(vc.inverse.isEnabled())}`);
    }
  }

  // Generic run toggles showcase (keeps inverse_relic demo above).
  if (vc.toggles) {
    vc.toggles.register({
      id: "demo-mod:torchlight",
      name: "Torchlight",
      description: "Lights ember halls from floor 2 (requires Brass Lantern).",
      conditions: { biomes: ["demo-mod:ember_depths"], minFloor: 2, requiresRelics: ["demo-mod:brass_lantern"] },
    });
    vc.toggles.register({
      id: "demo-mod:hard_mode",
      name: "Hard Mode",
      description: "Extra-spicy demo encounters (no conditions).",
    });
    vc.log(`toggles: ${vc.toggles.list().join(", ")}`);
    vc.toggles.onChanged("demo-mod:torchlight", (on) => vc.log(`torchlight onChanged → on=${String(on)}`));
    vc.toggles.setEnabled("demo-mod:torchlight", true);
    vc.log(
      `torchlight on → enabled=${String(vc.toggles.isEnabled("demo-mod:torchlight"))} ` +
        `active@ember_depths:2=${String(vc.toggles.isActiveAtRuntime("demo-mod:torchlight", "demo-mod:ember_depths", 2))} ` +
        `active@ember_depths:1=${String(vc.toggles.isActiveAtRuntime("demo-mod:torchlight", "demo-mod:ember_depths", 1))}`,
    );
    vc.toggles.checkLevelEnter("demo-mod:ember_depths", 2);
    vc.toggles.checkLevelEnter("demo-mod:ember_depths", 1);
    vc.toggles.setEnabled("demo-mod:hard_mode", true);
    vc.log(
      `hard_mode on → enabled=${String(vc.toggles.isEnabled("demo-mod:hard_mode"))} ` +
        `active@ember_depths:1=${String(vc.toggles.isActiveAtRuntime("demo-mod:hard_mode", "demo-mod:ember_depths", 1))}`,
    );
    vc.toggles.checkLevelEnter("demo-mod:ember_depths", 1);
    vc.toggles.setEnabled("demo-mod:torchlight", false);
    vc.toggles.setEnabled("demo-mod:hard_mode", false);
    vc.log(
      `toggles off → torchlight enabled=${String(vc.toggles.isEnabled("demo-mod:torchlight"))} ` +
        `hard_mode enabled=${String(vc.toggles.isEnabled("demo-mod:hard_mode"))}`,
    );
  }
}

// Auto-register on load (loader executes the bundle). Also works as ESM import.
register();

// Lifecycle hooks — shows `vc.hooks.on` surface incl. new events.
if (vc.hooks) {
  vc.hooks.on("init", () => vc.log("demo-mod init"));
  vc.hooks.on("dungeonEnter", () => vc.log("demo-mod dungeonEnter"));
  vc.hooks.on("cardPlayed", () => vc.log("demo-mod cardPlayed"));
  vc.hooks.on("relicOptionChanged", () => vc.log("demo-mod relicOptionChanged"));
  vc.hooks.on("inverseToggled", () => vc.log("demo-mod inverseToggled"));
}

// Dev console demo — press ` (backtick) in-game to open console.
if (vc.console) {
  vc.console.register("demo", "demo-mod hello", (args: string[], ctx: DevConsoleContext) => {
    ctx.log(`demo args: ${args.join(" ")} — try: give demo-mod:ember_strike 3`);
  });
  vc.console.register("give-demo", "Give all demo cards", (_args: string[], ctx: DevConsoleContext) => {
    for (const c of cards) ctx.log(`would give ${c.id} — use: give ${c.id}`);
  });
  // M8-T7: inverse status console command for headless verification.
  vc.console.register("inverse", "inverse run-mode status/control — inverse [on|off|status]", (args: string[], ctx: DevConsoleContext) => {
    const sub = (args[0] ?? "status").toLowerCase();
    if (vc.inverse) {
      if (sub === "on") vc.inverse.setEnabled(true);
      else if (sub === "off") vc.inverse.setEnabled(false);
      ctx.log(`inverse: unlocked=${String(vc.inverse.isUnlocked())} enabled=${String(vc.inverse.isEnabled())}`);
    } else {
      ctx.log("inverse: unavailable in this runtime");
    }
  });
}
