/**
 * Optional helper for mods that want a local registry shim (useful in tests / without loader).
 * Not used by the loader; loader provides its own `vc`.
 */
import type { CardDef, GemDef, VcApi } from "./index.js";

export function createLocalVc(): VcApi & { _cards: CardDef[]; _gems: GemDef[] } {
  const cards: CardDef[] = [];
  const gems: GemDef[] = [];
  const vc: any = {
    meta: { id: "local" },
    cards: {
      register(def: CardDef) { cards.push(def); },
      patch(id: string, p: Partial<CardDef>) {
        const c = cards.find((x) => x.id === id);
        if (c) Object.assign(c, p);
      },
      list() { return cards.map((c) => c.id); },
    },
    gems: { register(def: GemDef) { gems.push(def); } },
    arcanas: { register() {} },
    relics: { register() {} },
    enemies: { register() {} },
    evolutions: { register() {} },
    dungeons: { registerBiome() {}, registerRoom() {} },
    state: { getMana: () => 0, getCombo: () => 0, getMight: () => 0 },
    hooks: { on() {} },
    console: {
      register(a: any, b?: any, c?: any) { console.log("[vc.console.register]", a, b); void c; },
      log(...a: unknown[]) { console.log("[vc.console]", ...a); },
      clear() {},
      open() {},
      close() {},
      toggle() {},
      isOpen() { return false; },
    },
    log(...a: unknown[]) { console.log("[vc]", ...a); },
    _cards: cards,
    _gems: gems,
  };
  return vc;
}
