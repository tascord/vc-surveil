/**
 * demo-mod — 5 test cards for the Vampire Crawlers TS modding API.
 * This file is the `surveil install` demo: after `npm run build` copy
 * `mods/demo-mod/` (mod.json + dist/) to the game's `Mods/demo-mod/` and
 * launch with PancakeModLoader (BepInEx IL2CPP).
 *
 * Types come from @vc-surveil/api; at runtime `globalThis.vc` is injected by the loader.
 */
import type { CardDef, VcApi, DevConsoleContext } from "@vc-surveil/api";

declare global {
  var vc: VcApi;
}

// Loader injects `vc` into globalThis at runtime. Fallback shim lets local `node` or tests exercise the file.
const vc: VcApi = globalThis.vc ?? {
  meta: { id: "demo-mod", name: "demo-mod", version: "0.1.0" },
  log: (...a: unknown[]) => console.log("[demo-mod]", ...a),
  cards: {
    register(d: CardDef) { console.log("[demo-mod] register", d.id, d.color, d.mana, d.rarity, d.weight, d.text); },
    patch() {},
    list() { return []; },
  },
  gems: { register() {}, patch() {}, list() { return []; } },
  arcanas: { register() {}, list() { return []; } },
  relics: { register() {}, list() { return []; } },
  enemies: { register() {}, patch() {}, list() { return []; } },
  evolutions: { register() {}, list() { return []; } },
  dungeons: { registerBiome() {}, registerRoom() {} },
  state: { getMana() { return 0; }, getCombo() { return 0; }, getMight() { return 0; } },
  hooks: { on(ev: string, cb: (...args: unknown[]) => void) { console.log("[demo-mod] hooks.on", ev); void cb; } },
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
];

export function register() {
  for (const c of cards) vc.cards.register(c);
  vc.log(`demo-mod registered ${cards.length} cards (targeting & effects demo)`);
}

// Auto-register on load (loader executes the bundle). Also works as ESM import.
register();

// Optional lifecycle hooks — shows `vc.hooks.on` surface.
vc.hooks.on("init", () => vc.log("demo-mod init"));
vc.hooks.on("dungeonEnter", () => vc.log("demo-mod dungeonEnter"));

// Dev console demo — press ` (backtick) in-game to open console, try: help, give demo-mod:test_strike, cards
if (vc.console) {
  vc.console.register("demo", "demo-mod hello", (args: string[], ctx: DevConsoleContext) => {
    ctx.log(`demo args: ${args.join(" ")} — try: give demo-mod:test_strike 3`);
  });
  vc.console.register("give-demo", "Give all demo cards", (_args: string[], ctx: DevConsoleContext) => {
    for (const c of cards) ctx.log(`would give ${c.id} — use: give ${c.id}`);
  });
}
