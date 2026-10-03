#!/usr/bin/env node
/**
 * M9-T3-S1 contract harness (no game required).
 * Executes the built demo-mod bundle against the registry.ts local shim
 * and asserts every register → list round-trip, relic option get/set,
 * and inverse toggle gating. Fails non-zero on any mismatch.
 */
import { createLocalVc } from "../packages/api/dist/registry.js";

const vc = createLocalVc();
globalThis.vc = vc;

let failures = 0;
function check(name, cond, detail = "") {
  if (cond) console.log(`ok   ${name}`);
  else {
    failures++;
    console.log(`FAIL ${name} ${detail}`);
  }
}

await import("../mods/demo-mod/dist/index.js");

// ── register → list round-trips ──
check("cards: 9 registered", vc.cards.list().length === 9, JSON.stringify(vc.cards.list()));
for (const id of [
  "demo-mod:fun", "demo-mod:tarot_fool", "demo-mod:tarot_magician",
  "demo-mod:tarot_devil", "demo-mod:friendship_amulet", "demo-mod:combo_twist",
  "demo-mod:minecart_rush", "demo-mod:ember_strike", "demo-mod:rusty_swing",
]) check(`cards.list contains ${id}`, vc.cards.list().includes(id));
check("weapons: rusty_whip", vc.weapons.list().join() === "demo-mod:rusty_whip");
check("crawlers: ember_crawler", vc.crawlers.list().join() === "demo-mod:ember_crawler");
check("arcanas: arcana_spark", vc.arcanas.list().join() === "demo-mod:arcana_spark");
check(
  "relics: lantern + inverse_relic",
  vc.relics.list().includes("demo-mod:brass_lantern") && vc.relics.list().includes("demo-mod:inverse_relic"),
  JSON.stringify(vc.relics.list()),
);
check("biomes: ember_depths", vc.dungeons.listBiomes().join() === "demo-mod:ember_depths");
check("rooms: hall + shrine", vc.dungeons.listRooms().length === 2, JSON.stringify(vc.dungeons.listRooms()));
check("enemies: imp + brute", vc.enemies.list().length === 2, JSON.stringify(vc.enemies.list()));
check("evolutions: true_whip", vc.evolutions.list().join() === "demo-mod:true_whip");

// ── patch round-trips ──
vc.cards.patch("demo-mod:tarot_fool", { damage: 51 });
check("cards.patch applies", vc._cards.find((c) => c.id === "demo-mod:tarot_fool").damage === 51);
vc.weapons.patch("demo-mod:rusty_whip", { damage: 16 });
check("weapons.patch applies", vc._weapons[0].damage === 16);
vc.relics.patch("demo-mod:brass_lantern", { rarity: "Rare" });
check("relics.patch applies", vc._relics.find((r) => r.id === "demo-mod:brass_lantern").rarity === "Rare");
vc.dungeons.patchBiome("demo-mod:ember_depths", { difficulty: 3 });
check("dungeons.patchBiome applies", vc._biomes[0].difficulty === 3);
vc.dungeons.patchRoom("demo-mod:ember_hall", { weight: 90 });
check("dungeons.patchRoom applies", vc._rooms.find((r) => r.id === "demo-mod:ember_hall").weight === 90);
vc.enemies.patch("demo-mod:soot_imp", { health: 31 });
check("enemies.patch applies", vc._enemies.find((e) => e.id === "demo-mod:soot_imp").health === 31);
vc.arcanas.patch("demo-mod:arcana_spark", { name: "Spark+" });
check("arcanas.patch applies", vc._arcanas[0].name === "Spark+");
vc.crawlers.patch("demo-mod:ember_crawler", { name: "Ember+" });
check("crawlers.patch applies", vc._crawlers[0].name === "Ember+");

// ── relic options (explicit set + defaults) ──
check("relic setOption/getOption round-trip", vc.relics.getOption("demo-mod:brass_lantern", "brightness") === "bright");
check("relic default fallback (lit)", vc.relics.getOption("demo-mod:brass_lantern", "lit") === true);

// relicOptionChanged hook fires
let optEvent = null;
vc.hooks.on("relicOptionChanged", (...a) => { optEvent = a; });
vc.relics.setOption("demo-mod:brass_lantern", "lit", false);
check("relicOptionChanged emitted", Array.isArray(optEvent) && optEvent[0] === "demo-mod:brass_lantern" && optEvent[1] === "lit");

// ── inverse gating: locked without relic ──
const locked = createLocalVc();
globalThis.vc = locked;
locked.inverse.setEnabled(true); // no relic registered → must no-op
check("inverse locked without relic", locked.inverse.isEnabled() === false && locked.inverse.isUnlocked() === false);

// ── inverse toggle with relic (main vc has inverse_relic) ──
check("inverse unlocked via unlocksToggle relic", vc.inverse.isUnlocked() === true);
let toggles = [];
vc.inverse.onToggle((b) => toggles.push(b));
let hookArg = null;
vc.hooks.on("inverseToggled", (b) => { hookArg = b; });
vc.inverse.setEnabled(true);
check("inverse enables + runFlags reflect", vc.inverse.isEnabled() === true && vc.state.getRunFlags().inverse === true);
vc.inverse.setEnabled(false);
check("inverse disables", vc.inverse.isEnabled() === false && vc.state.getRunFlags().inverse === false);
check("onToggle fired [true,false]", JSON.stringify(toggles) === "[true,false]", JSON.stringify(toggles));
check("inverseToggled hook fired", hookArg === false, String(hookArg));
check("runModes mirrors inverse", vc.runModes.isActive("inverse") === false && vc.runModes.list().includes("inverse"));

if (failures > 0) {
  console.log(`${failures} FAILURE(S)`);
  process.exit(1);
}
console.log("contract harness: all green");
