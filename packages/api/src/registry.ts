/**
 * Optional helper for mods that want a local registry shim (useful in tests / without loader).
 * Not used by the loader; loader provides its own `vc`.
 *
 * Full parity with `VcApi` in `index.ts`: every namespace supports
 * `register`/`patch`/`list` round-trips, relics persist per-run option state
 * (with defaults), and inverse/run-mode state is gated on relic ownership
 * (mirrors the loader's honest declarative contract).
 *
 * Plus a local-minimal `settings` namespace (`index.ts` currently declares no
 * `VcSettings`, so the minimal interface below lives here for tests/local use
 * only and does not change the loader contract).
 */
import type {
  ArcanaDef,
  AudioDef,
  BiomeDef,
  CardDef,
  CrawlerDef,
  DevConsoleCommand,
  EnemyDef,
  EvolutionRecipe,
  GemDef,
  HookEvent,
  MapPackageDef,
  RelicDef,
  RelicOptionValue,
  RoomTemplate,
  RunFlags,
  RunToggleDef,
  SettingDef,
  VcApi,
  VcConsole,
  VcMaps,
  VcRunToggles,
  WeaponDef,
} from "./index.js";

// ── local-minimal settings (index.ts has no VcSettings yet) ──
export type LocalSettingValue = string | number | boolean;
export type LocalSettingKind = "toggle" | "choice" | "slider" | "string" | "number";
export interface LocalSettingDef {
  id: string;
  key?: string;
  label?: string;
  tab?: string;
  kind?: LocalSettingKind;
  default?: LocalSettingValue;
  choices?: LocalSettingValue[];
  min?: number;
  max?: number;
  description?: string;
}
export interface LocalSettingsApi {
  register(def: SettingDef): void;
  registerSetting(def: LocalSettingDef): void;
  setSetting(id: string, value: LocalSettingValue): void;
  getSetting(id: string): LocalSettingValue | undefined;
  listSettings(tab?: string): string[];
}

// ── maps shim (mirrors dungeons shim pattern) ──
// Uses `MapPackageDef`/`VcMaps` from `index.ts` (`VcApi.maps?` optional;
// consumers guard via `vc.maps?.registerMap(...)` / `if (vc.maps)`).
// In-memory `registerMap`/`patchMap`/`listMaps` round-trips; rooms carry
// tiles/props/lights/exits via `RoomMapDef` (see `index.ts` map-format note).

export interface LocalVcExtras {
  settings: LocalSettingsApi;
  /** Optional to mirror `VcApi.maps?` — shim always provides it; guards use `vc.maps?.`. */
  maps?: VcMaps;
  _cards: CardDef[];
  _gems: GemDef[];
  _weapons: WeaponDef[];
  _crawlers: CrawlerDef[];
  _arcanas: ArcanaDef[];
  _relics: RelicDef[];
  _enemies: EnemyDef[];
  _biomes: BiomeDef[];
  _rooms: RoomTemplate[];
  _maps: MapPackageDef[];
  _recipes: EvolutionRecipe[];
  _audios: AudioDef[];
  _settingDefs: LocalSettingDef[];
  _toggles: RunToggleDef[];
}

export type LocalVc = VcApi & LocalVcExtras;

interface Store<T extends { id: string }> {
  register(def: T): void;
  patch(id: string, p: Partial<T>): void;
  list(): string[];
}

function makeStore<T extends { id: string }>(items: T[]): Store<T> {
  return {
    register(def: T): void {
      const i = items.findIndex((x) => x.id === def.id);
      if (i >= 0) items[i] = def;
      else items.push(def);
    },
    patch(id: string, p: Partial<T>): void {
      const c = items.find((x) => x.id === id);
      if (c) Object.assign(c, p);
    },
    list(): string[] {
      return items.map((c) => c.id);
    },
  };
}

export function createLocalVc(): LocalVc {
  const cards: CardDef[] = [];
  const weapons: WeaponDef[] = [];
  const crawlers: CrawlerDef[] = [];
  const gems: GemDef[] = [];
  const arcanas: ArcanaDef[] = [];
  const relics: RelicDef[] = [];
  const enemies: EnemyDef[] = [];
  const biomes: BiomeDef[] = [];
  const rooms: RoomTemplate[] = [];
  const maps: MapPackageDef[] = [];
  const recipes: EvolutionRecipe[] = [];
  const audios: AudioDef[] = [];
  const settingDefs: LocalSettingDef[] = [];

  const cardStore = makeStore(cards);
  const weaponStore = makeStore(weapons);
  const crawlerStore = makeStore(crawlers);
  const gemStore = makeStore(gems);
  const arcanaStore = makeStore(arcanas);
  const relicStore = makeStore(relics);
  const enemyStore = makeStore(enemies);
  const biomeStore = makeStore(biomes);
  const roomStore = makeStore(rooms);
  const mapStore = makeStore(maps);
  const audioStore = makeStore(audios);
  const settingDefStore = makeStore(settingDefs);

  // ── hooks (records listeners; loader invokes them on game events) ──
  const hookListeners = new Map<HookEvent, Array<(...args: unknown[]) => void>>();
  function emitHook(ev: HookEvent, ...args: unknown[]): void {
    for (const cb of hookListeners.get(ev) ?? []) {
      try {
        cb(...args);
      } catch (err) {
        console.log(`[vc.hooks:${ev} error]`, err);
      }
    }
  }

  // ── relic per-run option state (defaults from defaultOptions ?? options[].default) ──
  const relicOptions = new Map<string, Map<string, RelicOptionValue>>();
  function relicDefault(relicId: string, key: string): RelicOptionValue | undefined {
    const def = relics.find((r) => r.id === relicId);
    if (!def) return undefined;
    if (def.defaultOptions && key in def.defaultOptions) return def.defaultOptions[key];
    return def.options?.find((o) => o.key === key)?.default;
  }

  // ── settings values (defaults from LocalSettingDef.default) ──
  const settingValues = new Map<string, LocalSettingValue>();
  function settingDefault(id: string): LocalSettingValue | undefined {
    return settingDefs.find((s) => s.id === id)?.default;
  }

  // ── run toggles (generic per-run feature flags; "inverse" is legacy preset) ──
  const toggles: RunToggleDef[] = [];
  const toggleStore = makeStore(toggles);
  const toggleEnabled = new Map<string, boolean>();
  const toggleListeners = new Map<string, Array<(on: boolean) => void>>();
  const runModes = new Map<string, boolean>();

  function findToggle(id: string): RunToggleDef | undefined {
    return toggles.find((t) => t.id === id);
  }

  function ownedRelicIds(): Set<string> {
    return new Set(relics.map((r) => r.id));
  }

  function isToggleUnlocked(id: string): boolean {
    const def = findToggle(id);
    if (!def) return false;
    const requiresRelics = def.conditions?.requiresRelics ?? [];
    const hasExplicitGate = def.unlocksVia !== undefined || requiresRelics.length > 0;
    if (!hasExplicitGate) {
      if (id === "inverse") {
        // Legacy preset: gated on any relic with unlocksToggle === "inverse".
        return relics.some((r) => r.unlocksToggle === "inverse");
      }
      return true;
    }
    const owned = ownedRelicIds();
    for (const rid of requiresRelics) {
      if (!owned.has(rid)) return false;
    }
    if (def.unlocksVia !== undefined) {
      if (owned.has(def.unlocksVia)) return true;
      // Reverse mapping also satisfies unlock: any owned relic claiming this toggle.
      return relics.some((r) => r.unlocksToggle === id);
    }
    return true;
  }

  function isToggleEnabled(id: string): boolean {
    const def = findToggle(id);
    if (!def) return false;
    const v = toggleEnabled.get(id);
    if (v !== undefined) return v;
    return def.defaultEnabled ?? false;
  }

  function currentRunFlags(): RunFlags {
    const flags: RunFlags = {};
    for (const [k, v] of runModes) flags[k] = v;
    for (const t of toggles) {
      flags[t.id] = isToggleEnabled(t.id);
    }
    if (!("inverse" in flags)) flags["inverse"] = isToggleEnabled("inverse");
    return flags;
  }

  function isToggleActiveAtRuntime(id: string, biomeId: string, floor: number): boolean {
    const def = findToggle(id);
    if (!def) return false;
    if (!isToggleEnabled(id)) return false;
    if (!isToggleUnlocked(id)) return false;
    const c = def.conditions;
    if (!c) return true;
    if (c.biomes !== undefined && c.biomes.length > 0 && !c.biomes.includes(biomeId)) return false;
    if (c.minFloor !== undefined && floor < c.minFloor) return false;
    if (c.maxFloor !== undefined && floor > c.maxFloor) return false;
    if (c.requiresRelics !== undefined && c.requiresRelics.length > 0) {
      const owned = ownedRelicIds();
      for (const rid of c.requiresRelics) {
        if (!owned.has(rid)) return false;
      }
    }
    if (c.requiresFlags !== undefined) {
      const flags = currentRunFlags();
      for (const [k, expected] of Object.entries(c.requiresFlags)) {
        if (flags[k] !== expected) return false;
      }
    }
    return true;
  }

  function setToggleEnabled(id: string, on: boolean): void {
    const def = findToggle(id);
    if (!def) return;
    if (!isToggleUnlocked(id)) return; // toggle hidden/disabled until relic owned
    if (isToggleEnabled(id) === on) return;
    toggleEnabled.set(id, on);
    if (id === "inverse") {
      runModes.set("inverse", on);
    }
    const listeners = toggleListeners.get(id) ?? [];
    for (const cb of listeners) {
      try {
        cb(on);
      } catch (err) {
        console.log(`[vc.toggles:${id} onChanged error]`, err);
      }
    }
    if (id === "inverse") {
      emitHook("inverseToggled", on);
    }
  }

  function doCheckLevelEnter(biomeId: string, floor: number): void {
    const actives = toggles.filter((t) => isToggleActiveAtRuntime(t.id, biomeId, floor)).map((t) => t.id);
    console.log(`[vc.toggles] checkLevelEnter biome=${biomeId} floor=${String(floor)} actives=${actives.join(",")}`);
  }

  function subscribeToggle(id: string, cb: (on: boolean) => void): void {
    const arr = toggleListeners.get(id) ?? [];
    arr.push(cb);
    toggleListeners.set(id, arr);
  }

  const togglesApi: VcRunToggles = {
    register(def: RunToggleDef): void {
      toggleStore.register(def);
    },
    patch(id: string, p: Partial<RunToggleDef>): void {
      toggleStore.patch(id, p);
    },
    list(): string[] {
      return toggleStore.list();
    },
    isUnlocked(id: string): boolean {
      return isToggleUnlocked(id);
    },
    isEnabled(id: string): boolean {
      return isToggleEnabled(id);
    },
    setEnabled(id: string, on: boolean): void {
      setToggleEnabled(id, on);
    },
    onChanged(id: string, cb: (on: boolean) => void): void {
      subscribeToggle(id, cb);
    },
    checkLevelEnter(biomeId: string, floor: number): void {
      doCheckLevelEnter(biomeId, floor);
    },
    isActiveAtRuntime(id: string, biomeId: string, floor: number): boolean {
      return isToggleActiveAtRuntime(id, biomeId, floor);
    },
  };

  // Auto-register legacy "inverse" preset (delegated by vc.inverse / vc.runModes / vc.state).
  togglesApi.register({
    id: "inverse",
    name: "Inverse",
    description: "Legacy inverse run modifier",
    defaultEnabled: false,
  });

  // ── console (full VcConsole, in-memory) ──
  const consoleCommands: DevConsoleCommand[] = [];
  const consoleLines: unknown[][] = [];
  let consoleOpen = false;
  const consoleApi: VcConsole = {
    register: ((cmdOrName: DevConsoleCommand | string, description?: string, handler?: DevConsoleCommand["handler"]): void => {
      if (typeof cmdOrName === "string") {
        if (typeof handler !== "function") return;
        const cmd: DevConsoleCommand = {
          name: cmdOrName,
          description: description ?? "",
          handler,
        };
        const i = consoleCommands.findIndex((c) => c.name === cmd.name);
        if (i >= 0) consoleCommands[i] = cmd;
        else consoleCommands.push(cmd);
        return;
      }
      const i = consoleCommands.findIndex((c) => c.name === cmdOrName.name);
      if (i >= 0) consoleCommands[i] = cmdOrName;
      else consoleCommands.push(cmdOrName);
    }) as VcConsole["register"],
    log(...args: unknown[]): void {
      consoleLines.push(args);
      console.log("[vc.console]", ...args);
    },
    clear(): void {
      consoleLines.length = 0;
    },
    open(): void {
      consoleOpen = true;
    },
    close(): void {
      consoleOpen = false;
    },
    toggle(): void {
      consoleOpen = !consoleOpen;
    },
    isOpen(): boolean {
      return consoleOpen;
    },
  };

  const vc: LocalVc = {
    meta: { id: "local" },
    cards: {
      register(def: CardDef): void {
        cardStore.register(def);
      },
      patch(id: string, p: Partial<CardDef>): void {
        cardStore.patch(id, p);
      },
      list(): string[] {
        return cardStore.list();
      },
    },
    weapons: {
      register(def: WeaponDef): void {
        weaponStore.register(def);
      },
      patch(id: string, p: Partial<WeaponDef>): void {
        weaponStore.patch(id, p);
      },
      list(): string[] {
        return weaponStore.list();
      },
    },
    crawlers: {
      register(def: CrawlerDef): void {
        crawlerStore.register(def);
      },
      patch(id: string, p: Partial<CrawlerDef>): void {
        crawlerStore.patch(id, p);
      },
      list(): string[] {
        return crawlerStore.list();
      },
    },
    gems: {
      register(def: GemDef): void {
        gemStore.register(def);
      },
      patch(id: string, p: Partial<GemDef>): void {
        gemStore.patch(id, p);
      },
      list(): string[] {
        return gemStore.list();
      },
    },
    arcanas: {
      register(def: ArcanaDef): void {
        arcanaStore.register(def);
      },
      patch(id: string, p: Partial<ArcanaDef>): void {
        arcanaStore.patch(id, p);
      },
      list(): string[] {
        return arcanaStore.list();
      },
    },
    relics: {
      register(def: RelicDef): void {
        relicStore.register(def);
      },
      patch(id: string, p: Partial<RelicDef>): void {
        relicStore.patch(id, p);
      },
      list(): string[] {
        return relicStore.list();
      },
      setOption(relicId: string, key: string, value: RelicOptionValue): void {
        let opts = relicOptions.get(relicId);
        if (!opts) {
          opts = new Map<string, RelicOptionValue>();
          relicOptions.set(relicId, opts);
        }
        opts.set(key, value);
        emitHook("relicOptionChanged", relicId, key, value);
      },
      getOption(relicId: string, key: string): RelicOptionValue | undefined {
        return relicOptions.get(relicId)?.get(key) ?? relicDefault(relicId, key);
      },
    },
    enemies: {
      register(def: EnemyDef): void {
        enemyStore.register(def);
      },
      patch(id: string, p: Partial<EnemyDef>): void {
        enemyStore.patch(id, p);
      },
      list(): string[] {
        return enemyStore.list();
      },
    },
    audio: {
      register(def: AudioDef): void {
        audioStore.register(def);
      },
      play(_id: string): void {
        // Local stub: no audio backend outside the game loader.
      },
    },
    evolutions: {
      register(recipe: EvolutionRecipe): void {
        const i = recipes.findIndex(
          (r) =>
            r.primary === recipe.primary &&
            JSON.stringify(r.secondary) === JSON.stringify(recipe.secondary) &&
            r.result === recipe.result,
        );
        if (i < 0) recipes.push(recipe);
      },
      list(): string[] {
        return recipes.map((r) => r.result);
      },
    },
    dungeons: {
      registerBiome(def: BiomeDef): void {
        biomeStore.register(def);
      },
      patchBiome(id: string, p: Partial<BiomeDef>): void {
        biomeStore.patch(id, p);
      },
      registerRoom(tpl: RoomTemplate): void {
        roomStore.register(tpl);
      },
      patchRoom(id: string, p: Partial<RoomTemplate>): void {
        roomStore.patch(id, p);
      },
      listBiomes(): string[] {
        return biomeStore.list();
      },
      listRooms(): string[] {
        return roomStore.list();
      },
    },
    maps: {
      registerMap(pkg: MapPackageDef): void {
        mapStore.register(pkg);
      },
      patchMap(id: string, p: Partial<MapPackageDef>): void {
        mapStore.patch(id, p);
      },
      listMaps(): string[] {
        return mapStore.list();
      },
    },
    state: {
      getMana: (): number => 0,
      getCombo: (): number => 0,
      getMight: (): number => 0,
      isInverse: (): boolean => isToggleEnabled("inverse"),
      getRunFlags: (): RunFlags => currentRunFlags(),
    },
    hooks: {
      on(ev: HookEvent, cb: (...args: unknown[]) => void): void {
        const arr = hookListeners.get(ev) ?? [];
        arr.push(cb);
        hookListeners.set(ev, arr);
      },
    },
    inverse: {
      isUnlocked: (): boolean => isToggleUnlocked("inverse"),
      isEnabled: (): boolean => isToggleEnabled("inverse"),
      setEnabled(b: boolean): void {
        setToggleEnabled("inverse", b);
      },
      onToggle(cb: (enabled: boolean) => void): void {
        subscribeToggle("inverse", cb);
      },
    },
    runModes: {
      list: (): string[] => {
        const ids = new Set<string>(["inverse"]);
        for (const t of toggles) ids.add(t.id);
        for (const k of runModes.keys()) ids.add(k);
        return [...ids];
      },
      isActive: (id: string): boolean => {
        if (id === "inverse" || findToggle(id) !== undefined) return isToggleEnabled(id);
        return runModes.get(id) ?? false;
      },
      setActive: (id: string, enabled: boolean): void => {
        if (id === "inverse" || findToggle(id) !== undefined) {
          setToggleEnabled(id, enabled);
          return;
        }
        runModes.set(id, enabled);
      },
    },
    toggles: togglesApi,
    settings: {
      register(def: SettingDef): void {
        settingDefStore.register(def);
      },
      registerSetting(def: LocalSettingDef): void {
        settingDefStore.register(def);
      },
      setSetting(id: string, value: LocalSettingValue): void {
        settingValues.set(id, value);
      },
      getSetting(id: string): LocalSettingValue | undefined {
        return settingValues.get(id) ?? settingDefault(id);
      },
      listSettings(tab?: string): string[] {
        if (tab === undefined) return settingDefStore.list();
        return settingDefs.filter((s) => s.tab === tab).map((s) => s.id);
      },
    },
    console: consoleApi,
    log(...args: unknown[]): void {
      console.log("[vc]", ...args);
    },
    _cards: cards,
    _gems: gems,
    _weapons: weapons,
    _crawlers: crawlers,
    _arcanas: arcanas,
    _relics: relics,
    _enemies: enemies,
    _biomes: biomes,
    _rooms: rooms,
    _maps: maps,
    _recipes: recipes,
    _audios: audios,
    _settingDefs: settingDefs,
    _toggles: toggles,
  };
  return vc;
}
