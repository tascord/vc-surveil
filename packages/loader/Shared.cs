using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Jint;
using Jint.Native;
using Jint.Native.Object;


namespace PancakeModLoader
{
    public record ModManifest(string Id, string Version, string Entry, string? Name = null);
    public record DiscoveredMod(ModManifest Manifest, string RootDir, string EntryPath);

    public class ModDiscovery
    {
        private readonly string _primaryRoot;
        private readonly string? _fallbackRoot;
        public ModDiscovery(string primaryRoot, string? fallbackRoot = null)
        {
            _primaryRoot = primaryRoot;
            _fallbackRoot = fallbackRoot;
        }
        private IEnumerable<string> Roots()
        {
            if (Directory.Exists(_primaryRoot)) yield return _primaryRoot;
            else LoaderLog.Info($"Primary mods missing (will create): {_primaryRoot}");
            if (_fallbackRoot != null && _fallbackRoot != _primaryRoot && Directory.Exists(_fallbackRoot))
                yield return _fallbackRoot;
            var cwdMods = Path.Combine(Directory.GetCurrentDirectory(), ".mods");
            if (Directory.Exists(cwdMods) && cwdMods != _primaryRoot && cwdMods != _fallbackRoot) yield return cwdMods;
        }
        public List<DiscoveredMod> Discover()
        {
            var list = new List<DiscoveredMod>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in Roots())
            {
                LoaderLog.Info($"Scanning mods in {root}");
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var manifestPath = Path.Combine(dir, "mod.json");
                    if (!File.Exists(manifestPath)) continue;
                    try
                    {
                        var json = File.ReadAllText(manifestPath);
                        var id = ExtractJson(json, "id") ?? Path.GetFileName(dir);
                        if (seen.Contains(id)) { LoaderLog.Info($"Skipping duplicate mod {id} from {root}"); continue; }
                        var version = ExtractJson(json, "version") ?? "0.0.0";
                        var entry = ExtractJson(json, "entry") ?? "dist/mod.js";
                        var manifest = new ModManifest(id, version, entry);
                        var entryPath = Path.Combine(dir, entry);
                        if (!File.Exists(entryPath))
                        {
                            var alt = Path.Combine(dir, "dist/index.js");
                            if (File.Exists(alt)) entryPath = alt;
                        }
                        if (File.Exists(entryPath))
                        {
                            list.Add(new DiscoveredMod(manifest, dir, entryPath));
                            seen.Add(id);
                        }
                        else LoaderLog.Warn($"mod {id}: entry not found {entryPath}");
                    }
                    catch (Exception ex) { LoaderLog.Warn($"bad manifest {manifestPath}: {ex.Message}"); }
                }
            }
            return list;
        }
        private static string? ExtractJson(string json, string key)
        {
            var needle = $"\"{key}\"";
            var idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) return null;
            var colon = json.IndexOf(':', idx);
            if (colon < 0) return null;
            var start = json.IndexOf('"', colon + 1);
            if (start < 0) return null;
            var end = json.IndexOf('"', start + 1);
            if (end < 0) return null;
            return json.Substring(start + 1, end - start - 1);
        }
    }

    // ── Declarative loader-owned registries (gems / evolutions / settings) ──
    // These mirror VcGems / VcEvolutions / VcSettings from packages/api.
    // The loader stores defs + per-run values and exposes register/patch/list
    // round-trips via the `vc` global. Native consumption (gem pools, evolution
    // tables, settings-menu wiring) is pending assembly verification, same as
    // the Weapon/Crawler/Relic/Dungeon bridges.
    public class CustomGemDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Rarity { get; set; } = "Common";
        public string Text { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public List<int> ChestTiers { get; set; } = new();
        public string ModDirectory { get; set; } = "";
    }

    public static class GemBridge
    {
        public static List<CustomGemDef> RegisteredDefs { get; } = new();

        public static void Register(CustomGemDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return;
            RegisteredDefs.RemoveAll(x => x.Id == def.Id);
            RegisteredDefs.Add(def);
            LoaderLog.Info($"[GemBridge] Registered gem: {def.Id} ('{def.Name}') — declarative, native wiring pending assembly anchor");
        }

        public static void RegisterGem(CustomGemDef def) => Register(def);

        public static void Patch(string id, Action<CustomGemDef> apply)
        {
            var def = RegisteredDefs.Find(x => x.Id == id);
            if (def == null) { LoaderLog.Warn($"[GemBridge] Patch target not found: {id}"); return; }
            apply(def);
        }

        public static void PatchGem(string id, Action<CustomGemDef> apply) => Patch(id, apply);

        public static CustomGemDef FindDef(string id) => RegisteredDefs.Find(x => x.Id == id);

        public static CustomGemDef GetDef(string id) => FindDef(id);

        public static string[] ListIds() => RegisteredDefs.Select(x => x.Id).ToArray();
    }

    public class CustomEvolutionRecipe
    {
        public string Primary { get; set; } = "";
        public List<string> Secondary { get; set; } = new();
        public string Result { get; set; } = "";
        public string ModDirectory { get; set; } = "";
    }

    public static class EvolutionBridge
    {
        public static List<CustomEvolutionRecipe> RegisteredRecipes { get; } = new();

        public static void Register(CustomEvolutionRecipe recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.Primary) || string.IsNullOrEmpty(recipe.Result)) return;
            var key = SecondaryKey(recipe.Secondary);
            if (RegisteredRecipes.Exists(x => x.Primary == recipe.Primary && SecondaryKey(x.Secondary) == key && x.Result == recipe.Result)) return;
            RegisteredRecipes.Add(recipe);
            LoaderLog.Info($"[EvolutionBridge] Registered evolution: {recipe.Primary} + [{string.Join(",", recipe.Secondary)}] -> {recipe.Result} — declarative, native wiring pending assembly anchor");
        }

        public static void RegisterEvolution(CustomEvolutionRecipe recipe) => Register(recipe);

        private static string SecondaryKey(List<string> s) => s == null ? "" : string.Join("|", s);

        public static string[] ListResults() => RegisteredRecipes.Select(x => x.Result).ToArray();
    }

    public class CustomSettingDef
    {
        public string Id { get; set; } = "";
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Tab { get; set; } = "";
        public string Kind { get; set; } = "toggle";
        public object Default { get; set; } = null;
        public List<object> Choices { get; set; } = new();
        public double Min { get; set; } = 0;
        public double Max { get; set; } = 0;
        public bool HasMin { get; set; } = false;
        public bool HasMax { get; set; } = false;
        public string Description { get; set; } = "";
        public string ModDirectory { get; set; } = "";
    }

    public static class SettingBridge
    {
        public static List<CustomSettingDef> RegisteredDefs { get; } = new();
        public static Dictionary<string, object> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public static void Register(CustomSettingDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return;
            RegisteredDefs.RemoveAll(x => string.Equals(x.Id, def.Id, StringComparison.OrdinalIgnoreCase));
            RegisteredDefs.Add(def);
            if (!Values.ContainsKey(def.Id) && def.Default != null)
                Values[def.Id] = def.Default;
            LoaderLog.Info($"[SettingBridge] Registered setting: {def.Id} ('{def.Label}', {def.Kind}) — declarative, native menu wiring pending assembly anchor");
        }

        public static void RegisterSetting(CustomSettingDef def) => Register(def);

        public static void Set(string id, object value)
        {
            if (string.IsNullOrEmpty(id)) return;
            Values[id] = value;
        }

        public static object Get(string id)
        {
            if (!string.IsNullOrEmpty(id) && Values.TryGetValue(id, out var v)) return v;
            var def = RegisteredDefs.Find(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            return def != null ? def.Default : null;
        }

        public static string[] ListIds(string tab = null)
        {
            if (string.IsNullOrEmpty(tab)) return RegisteredDefs.Select(x => x.Id).ToArray();
            return RegisteredDefs.Where(x => string.Equals(x.Tab ?? "", tab, StringComparison.OrdinalIgnoreCase)).Select(x => x.Id).ToArray();
        }
    }

    public class JsRuntime
    {
        // Stored JS hook callbacks: event -> list of (owning engine, callback).
        // Mods subscribe via vc.hooks.on / vc.inverse.onToggle; game code (or
        // future loader patches) invokes them via the static FireHook helper.
        private static readonly Dictionary<string, List<(Engine engine, JsValue callback)>> _hookCallbacks =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<(Engine engine, JsValue callback)> _inverseToggleCallbacks = new();
        private static readonly Dictionary<string, bool> _runModeState =
            new(StringComparer.OrdinalIgnoreCase);

        static JsRuntime()
        {
            // Forward native C# inverse toggles to stored JS onToggle listeners
            // plus the inverseToggled hook event.
            try { InverseToggle.OnToggled += enabled => NotifyInverseToggled(enabled); }
            catch { }
            try { RunStateStore.WireHooks(); } catch { }
        }

        /// Invoke stored JS callbacks for a hook event. Safe to call from game
        /// patches; per-callback failures are logged and contained.
        public static void FireHook(string eventName, params object[] args)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            try
            {
                if (string.Equals(eventName, "dungeonEnter", StringComparison.OrdinalIgnoreCase))
                {
                    var dungeonArgs = ExtractDungeonEnterArgs(args);
                    try { RunToggleBridge.CheckLevelEnter(dungeonArgs.biomeId, dungeonArgs.floor); } catch { }
                }
            }
            catch { }
            List<(Engine engine, JsValue callback)> snapshot = null;
            lock (_hookCallbacks)
            {
                if (_hookCallbacks.TryGetValue(eventName, out var list))
                    snapshot = new List<(Engine, JsValue)>(list);
            }
            if (snapshot == null) return;
            foreach (var entry in snapshot)
            {
                try
                {
                    if (entry.callback != null && !entry.callback.IsUndefined() && !entry.callback.IsNull())
                        entry.engine.Invoke(entry.callback, args ?? new object[0]);
                }
                catch (Exception ex)
                {
                    LoaderLog.Warn($"[JsRuntime] hook '{eventName}' callback error: {ex.Message}");
                }
            }
        }

        private static (string biomeId, int floor) ExtractDungeonEnterArgs(object[] args)
        {
            string biomeId = "";
            int floor = 0;
            try
            {
                if (args == null || args.Length == 0) return (biomeId, floor);
                if (args.Length == 1)
                {
                    var single = args[0];
                    if (single is ObjectInstance singleOi)
                    {
                        var b = singleOi.Get("biomeId");
                        if (b.IsUndefined() || b.IsNull()) b = singleOi.Get("biome");
                        if (b.IsString()) biomeId = b.AsString();
                        var f = singleOi.Get("floor");
                        if (f.IsUndefined() || f.IsNull()) f = singleOi.Get("floorNumber");
                        if (f.IsNumber()) floor = (int)f.AsNumber();
                        else if (f.IsString() && int.TryParse(f.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pf)) floor = pf;
                        if (!string.IsNullOrEmpty(biomeId) || floor != 0) return (biomeId, floor);
                    }
                    else if (single is JsValue singleJv && singleJv is ObjectInstance singleJoi)
                    {
                        var b = singleJoi.Get("biomeId");
                        if (b.IsUndefined() || b.IsNull()) b = singleJoi.Get("biome");
                        if (b.IsString()) biomeId = b.AsString();
                        var f = singleJoi.Get("floor");
                        if (f.IsNumber()) floor = (int)f.AsNumber();
                        if (!string.IsNullOrEmpty(biomeId) || floor != 0) return (biomeId, floor);
                    }
                    else if (single != null && !(single is string) && !(single is bool) && !(single is double) && !(single is float) && !(single is int) && !(single is long))
                    {
                        try
                        {
                            var t = single.GetType();
                            var pb = t.GetProperty("biomeId") ?? t.GetProperty("biome") ?? t.GetProperty("BiomeId") ?? t.GetProperty("Biome");
                            var pfProp = t.GetProperty("floor") ?? t.GetProperty("Floor") ?? t.GetProperty("floorNumber") ?? t.GetProperty("FloorNumber");
                            bool any = false;
                            if (pb != null)
                            {
                                try
                                {
                                    var bv = pb.GetValue(single);
                                    if (bv != null)
                                    {
                                        var bvs = bv.ToString();
                                        if (bvs != null) { biomeId = bvs; any = true; }
                                    }
                                }
                                catch { }
                            }
                            if (pfProp != null)
                            {
                                try
                                {
                                    var fv = pfProp.GetValue(single);
                                    if (fv != null) { floor = Convert.ToInt32(fv); any = true; }
                                }
                                catch { }
                            }
                            if (any) return (biomeId ?? "", floor);
                        }
                        catch { }
                    }
                }
                foreach (var a in args)
                {
                    if (string.IsNullOrEmpty(biomeId) && !IsDungeonEnterNumericArg(a))
                    {
                        var s = DungeonEnterArgToString(a);
                        if (!string.IsNullOrEmpty(s)) biomeId = s;
                    }
                    if (floor == 0 && IsDungeonEnterNumericArg(a))
                    {
                        floor = DungeonEnterArgToInt(a);
                    }
                    if (!string.IsNullOrEmpty(biomeId) && floor != 0) break;
                }
                if (string.IsNullOrEmpty(biomeId) && args.Length >= 1 && !IsDungeonEnterNumericArg(args[0]))
                {
                    var s0 = DungeonEnterArgToString(args[0]);
                    if (!string.IsNullOrEmpty(s0)) biomeId = s0;
                }
                if (floor == 0 && args.Length >= 2) floor = DungeonEnterArgToInt(args[1]);
            }
            catch { }
            return (biomeId ?? "", floor);
        }

        private static bool IsDungeonEnterNumericArg(object arg)
        {
            try
            {
                if (arg == null) return false;
                if (arg is double || arg is float || arg is int || arg is long) return true;
                if (arg is JsValue jv)
                {
                    if (jv.IsNumber()) return true;
                    return false;
                }
                return false;
            }
            catch { return false; }
        }

        private static string DungeonEnterArgToString(object arg)
        {
            try
            {
                if (arg == null) return "";
                if (arg is string s) return s;
                if (arg is JsValue jv)
                {
                    if (jv.IsString()) return jv.AsString();
                    if (jv.IsNumber()) return jv.AsNumber().ToString(CultureInfo.InvariantCulture);
                    if (jv.IsBoolean()) return jv.AsBoolean() ? "true" : "false";
                    return "";
                }
                if (arg is double d) return d.ToString(CultureInfo.InvariantCulture);
                if (arg is float f) return f.ToString(CultureInfo.InvariantCulture);
                if (arg is int i) return i.ToString(CultureInfo.InvariantCulture);
                if (arg is long l) return l.ToString(CultureInfo.InvariantCulture);
                if (arg is bool b) return b ? "true" : "false";
                var st = arg.ToString();
                return st ?? "";
            }
            catch { return ""; }
        }

        private static int DungeonEnterArgToInt(object arg)
        {
            try
            {
                if (arg == null) return 0;
                if (arg is int ii) return ii;
                if (arg is long ll) return (int)ll;
                if (arg is double dd) return (int)dd;
                if (arg is float ff) return (int)ff;
                if (arg is bool bb) return bb ? 1 : 0;
                if (arg is string str)
                {
                    if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pi)) return pi;
                    if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var pd)) return (int)pd;
                    return 0;
                }
                if (arg is JsValue jv)
                {
                    if (jv.IsNumber()) return (int)jv.AsNumber();
                    if (jv.IsString())
                    {
                        var s = jv.AsString();
                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pi)) return pi;
                        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var pd)) return (int)pd;
                        return 0;
                    }
                    if (jv.IsBoolean()) return jv.AsBoolean() ? 1 : 0;
                    return 0;
                }
                try { return Convert.ToInt32(arg); } catch { return 0; }
            }
            catch { return 0; }
        }

        private static void NotifyInverseToggled(bool enabled)
        {
            List<(Engine engine, JsValue callback)> snapshot;
            lock (_inverseToggleCallbacks) { snapshot = new List<(Engine, JsValue)>(_inverseToggleCallbacks); }
            foreach (var entry in snapshot)
            {
                try
                {
                    if (entry.callback != null && !entry.callback.IsUndefined() && !entry.callback.IsNull())
                        entry.engine.Invoke(entry.callback, enabled);
                }
                catch (Exception ex)
                {
                    LoaderLog.Warn($"[JsRuntime] inverse onToggle callback error: {ex.Message}");
                }
            }
            try { FireHook("inverseToggled", enabled); } catch { }
        }

        private readonly DevConsole _console;

        public JsRuntime(DevConsole console = null)
        {
            _console = console;
        }

        public void LoadMod(DiscoveredMod mod)
        {
            var code = File.ReadAllText(mod.EntryPath);
            LoaderLog.Info($"[JsRuntime] Loading {mod.Manifest.Id} ({code.Length} bytes) from {mod.EntryPath}");

            // 1. Try Jint execution
            try
            {
                var engine = new Jint.Engine();

                engine.SetValue("__log", new Action<string>(msg => LoaderLog.Info($"[{mod.Manifest.Id}] {msg}")));
                engine.SetValue("__warn", new Action<string>(msg => LoaderLog.Warn($"[{mod.Manifest.Id}] {msg}")));
                engine.SetValue("__error", new Action<string>(msg => LoaderLog.Error($"[{mod.Manifest.Id}] {msg}")));
                engine.SetValue("__registerCard", new Action<JsValue>(val => RegisterCardFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__registerArcana", new Action<JsValue>(val => RegisterArcanaFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__patchArcana", new Action<string, JsValue>((id, p) => PatchArcanaFromJs(id, p, engine)));
                engine.SetValue("__listArcanas", new Func<string[]>(() => ArcanaBridge.ListIds()));
                engine.SetValue("__registerEnemy", new Action<JsValue>(val => RegisterEnemyFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__registerAudio", new Action<JsValue>(val => RegisterAudioFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__playAudio", new Action<string>(val => AudioBridge.PlayAudio(val)));
                engine.SetValue("__consoleRegister", new Action<string, string, JsValue>((name, desc, fn) =>
                {
                    _console?.Register(name, desc, (args, c) =>
                    {
                        try
                        {
                            if (fn != null && !fn.IsUndefined() && !fn.IsNull())
                            {
                                var ctx = new { log = new Action<string>(msg => c.WriteLine(msg)) };
                                engine.Invoke(fn, args, ctx);
                            }
                        }
                        catch (Exception ex)
                        {
                            c.WriteLine($"command error: {ex.Message}");
                        }
                    });
                }));

                engine.SetValue("__registerWeapon", new Action<JsValue>(val => RegisterWeaponFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__patchWeapon", new Action<string, JsValue>((id, p) => PatchWeaponFromJs(id, p)));
                engine.SetValue("__listWeapons", new Func<string[]>(() => WeaponBridge.RegisteredDefs.Select(x => x.Id).ToArray()));
                engine.SetValue("__registerCrawler", new Action<JsValue>(val => RegisterCrawlerFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__patchCrawler", new Action<string, JsValue>((id, p) => PatchCrawlerFromJs(id, p)));
                engine.SetValue("__listCrawlers", new Func<string[]>(() => CrawlerBridge.RegisteredDefs.Select(x => x.Id).ToArray()));
                engine.SetValue("__registerRelic", new Action<JsValue>(val => RegisterRelicFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__patchRelic", new Action<string, JsValue>((id, p) => PatchRelicFromJs(id, p, engine)));
                engine.SetValue("__listRelics", new Func<string[]>(() => RelicBridge.RegisteredDefs.Select(x => x.Id).ToArray()));
                engine.SetValue("__setRelicOption", new Action<string, string, JsValue>((relicId, key, value) => SetRelicOptionFromJs(relicId, key, value)));
                engine.SetValue("__getRelicOption", new Func<string, string, object>((relicId, key) => GetRelicOptionValue(relicId, key)));
                engine.SetValue("__registerToggle", new Action<JsValue>(val => RegisterToggleFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__patchToggle", new Action<string, JsValue>((id, p) => PatchToggleFromJs(id, p, engine)));
                engine.SetValue("__listToggles", new Func<string[]>(() => RunToggleBridge.ListIds()));
                engine.SetValue("__isToggleUnlocked", new Func<string, bool>(id => RunToggleBridge.IsUnlocked(id)));
                engine.SetValue("__isToggleEnabled", new Func<string, bool>(id => RunToggleBridge.IsEnabled(id)));
                engine.SetValue("__setToggleEnabled", new Action<string, JsValue>((id, v) => SetToggleEnabledFromJs(id, v)));
                engine.SetValue("__toggleOnChanged", new Action<string, JsValue>((id, cb) => ToggleOnChangedFromJs(id, cb, engine)));
                engine.SetValue("__checkToggleLevelEnter", new Action<JsValue, JsValue>((b, f) => CheckToggleLevelEnterFromJs(b, f)));
                engine.SetValue("__isToggleActiveAtRuntime", new Func<string, JsValue, JsValue, bool>((id, b, f) => IsToggleActiveAtRuntimeFromJs(id, b, f)));
                engine.SetValue("__registerBiome", new Action<JsValue>(val => RegisterBiomeFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__patchBiome", new Action<string, JsValue>((id, p) => PatchBiomeFromJs(id, p, engine)));
                engine.SetValue("__listBiomes", new Func<string[]>(() => DungeonBridge.ListBiomes()));
                engine.SetValue("__registerRoom", new Action<JsValue>(val => RegisterRoomFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__patchRoom", new Action<string, JsValue>((id, p) => PatchRoomFromJs(id, p)));
                engine.SetValue("__listRooms", new Func<string[]>(() => DungeonBridge.ListRooms()));
                engine.SetValue("__registerMap", new Action<JsValue>(val => RegisterMapFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__patchMap", new Action<string, JsValue>((id, p) => PatchMapFromJs(id, p, engine)));
                engine.SetValue("__listMaps", new Func<string[]>(() => MapBridge.ListMaps()));
                engine.SetValue("__registerGem", new Action<JsValue>(val => RegisterGemFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__patchGem", new Action<string, JsValue>((id, p) => PatchGemFromJs(id, p)));
                engine.SetValue("__listGems", new Func<string[]>(() => GemBridge.ListIds()));
                engine.SetValue("__registerEvolution", new Action<JsValue>(val => RegisterEvolutionFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__listEvolutions", new Func<string[]>(() => EvolutionBridge.ListResults()));
                engine.SetValue("__registerSetting", new Action<JsValue>(val => RegisterSettingFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__setSetting", new Action<string, JsValue>((id, value) => SetSettingFromJs(id, value)));
                engine.SetValue("__getSetting", new Func<string, object>(id => GetSettingValue(id)));
                engine.SetValue("__listSettings", new Func<JsValue, string[]>(tab => SettingBridge.ListIds(JsTabName(tab))));
                engine.SetValue("__patchCard", new Action<string, JsValue>((id, p) => PatchCardFromJs(id, p)));
                engine.SetValue("__listCards", new Func<string[]>(() => CardBridge.RegisteredDefs.Select(x => x.Id).ToArray()));
                engine.SetValue("__patchEnemy", new Action<string, JsValue>((id, p) => PatchEnemyFromJs(id, p)));
                engine.SetValue("__listEnemies", new Func<string[]>(() => EnemyBridge.RegisteredDefs.Select(x => x.Id).ToArray()));
                engine.SetValue("__hooksOn", new Action<string, JsValue>((ev, cb) =>
                {
                    if (string.IsNullOrEmpty(ev) || cb == null || cb.IsUndefined() || cb.IsNull()) return;
                    lock (_hookCallbacks)
                    {
                        if (!_hookCallbacks.TryGetValue(ev, out var list)) { list = new List<(Engine, JsValue)>(); _hookCallbacks[ev] = list; }
                        list.Add((engine, cb));
                    }
                    LoaderLog.Info($"[{mod.Manifest.Id}] hook registered: {ev}");
                }));
                engine.SetValue("__consoleClear", new Action(() => { try { _console?.Clear(); } catch { } }));
                engine.SetValue("__consoleOpen", new Action(() => { try { _console?.Open(); } catch { } }));
                engine.SetValue("__consoleClose", new Action(() => { try { _console?.Close(); } catch { } }));
                engine.SetValue("__consoleToggle", new Action(() => { try { _console?.Toggle(); } catch { } }));
                engine.SetValue("__consoleIsOpen", new Func<bool>(() => _console != null && _console.IsOpen));
                engine.SetValue("__setInverse", new Action<JsValue>(v => SetInverseFromJs(v)));
                engine.SetValue("__isInverseUnlocked", new Func<bool>(() => InverseToggle.IsUnlocked()));
                engine.SetValue("__isInverseEnabled", new Func<bool>(() => InverseToggle.IsEnabled()));
                engine.SetValue("__inverseOnToggle", new Action<JsValue>(cb =>
                {
                    if (cb == null || cb.IsUndefined() || cb.IsNull()) return;
                    lock (_inverseToggleCallbacks) { _inverseToggleCallbacks.Add((engine, cb)); }
                }));
                engine.SetValue("__listRunModes", new Func<string[]>(() => ListRunModeIds()));
                engine.SetValue("__isRunModeActive", new Func<string, bool>(id => IsRunModeActive(id)));
                engine.SetValue("__setRunModeActive", new Action<string, bool>((id, b) => SetRunModeActive(id, b)));
                engine.SetValue("__getMana", new Func<double>(() => 0.0));
                engine.SetValue("__getCombo", new Func<double>(() => 0.0));
                engine.SetValue("__getMight", new Func<double>(() => 0.0));
                engine.SetValue("__isInverseState", new Func<bool>(() => InverseToggle.IsEnabled()));
                engine.SetValue("__getRunFlags", new Func<JsValue>(() => BuildRunFlags(engine)));

                engine.Execute(@"
const console = {
    log: (...args) => __log(args.join(' ')),
    warn: (...args) => __warn(args.join(' ')),
    error: (...args) => __error(args.join(' '))
};
globalThis.vc = {
    cards: {
        register: (c) => __registerCard(c),
        patch: (id, p) => __patchCard(id, p),
        list: () => __listCards()
    },
    weapons: {
        register: (c) => __registerWeapon(c),
        patch: (id, p) => __patchWeapon(id, p),
        list: () => __listWeapons()
    },
    crawlers: {
        register: (c) => __registerCrawler(c),
        patch: (id, p) => __patchCrawler(id, p),
        list: () => __listCrawlers()
    },
    gems: {
        register: (c) => __registerGem(c),
        patch: (id, p) => __patchGem(id, p),
        list: () => __listGems()
    },
    arcanas: {
        register: (c) => __registerArcana(c),
        patch: (id, p) => __patchArcana(id, p),
        list: () => __listArcanas()
    },
    relics: {
        register: (c) => __registerRelic(c),
        patch: (id, p) => __patchRelic(id, p),
        list: () => __listRelics(),
        setOption: (relicId, key, value) => __setRelicOption(relicId, key, value),
        getOption: (relicId, key) => __getRelicOption(relicId, key)
    },
    toggles: {
        register: (d) => __registerToggle(d),
        patch: (id, p) => __patchToggle(id, p),
        list: () => __listToggles(),
        isUnlocked: (id) => __isToggleUnlocked(id),
        isEnabled: (id) => __isToggleEnabled(id),
        setEnabled: (id, b) => __setToggleEnabled(id, b),
        onChanged: (id, cb) => __toggleOnChanged(id, cb),
        checkLevelEnter: (biomeId, floor) => __checkToggleLevelEnter(biomeId, floor),
        isActiveAtRuntime: (id, biomeId, floor) => __isToggleActiveAtRuntime(id, biomeId, floor)
    },
    enemies: {
        register: (c) => __registerEnemy(c),
        patch: (id, p) => __patchEnemy(id, p),
        list: () => __listEnemies()
    },
    audio: {
        register: (c) => __registerAudio(c),
        play: (id) => __playAudio(id)
    },
    evolutions: {
        register: (r) => __registerEvolution(r),
        list: () => __listEvolutions()
    },
    dungeons: {
        registerBiome: (d) => __registerBiome(d),
        patchBiome: (id, p) => __patchBiome(id, p),
        registerRoom: (t) => __registerRoom(t),
        patchRoom: (id, p) => __patchRoom(id, p),
        listBiomes: () => __listBiomes(),
        listRooms: () => __listRooms()
    },
    maps: {
        registerMap: (m) => __registerMap(m),
        patchMap: (id, p) => __patchMap(id, p),
        listMaps: () => __listMaps()
    },
    state: {
        getMana: () => __getMana(),
        getCombo: () => __getCombo(),
        getMight: () => __getMight(),
        isInverse: () => __isInverseState(),
        getRunFlags: () => __getRunFlags()
    },
    hooks: {
        on: (ev, cb) => __hooksOn(ev, cb)
    },
    inverse: {
        isUnlocked: () => __isInverseUnlocked(),
        isEnabled: () => __isInverseEnabled(),
        setEnabled: (b) => __setInverse(b),
        onToggle: (cb) => __inverseOnToggle(cb)
    },
    runModes: {
        list: () => __listRunModes(),
        isActive: (id) => __isRunModeActive(id),
        setActive: (id, b) => __setRunModeActive(id, b)
    },
    settings: {
        register: (d) => __registerSetting(d),
        setSetting: (id, v) => __setSetting(id, v),
        getSetting: (id) => __getSetting(id),
        listSettings: (tab) => __listSettings(tab)
    },
    console: {
        register: (n, d, f) => __consoleRegister(n, d, f),
        log: (...args) => __log(args.join(' ')),
        clear: () => __consoleClear(),
        open: () => __consoleOpen(),
        close: () => __consoleClose(),
        toggle: () => __consoleToggle(),
        isOpen: () => __consoleIsOpen()
    },
    log: (...args) => __log(args.join(' '))
};
");

                var scriptCode = System.Text.RegularExpressions.Regex.Replace(code, @"\bexport\s+", "");
                engine.Execute(scriptCode);
                LoaderLog.Info($"[JsRuntime] Executed {mod.Manifest.Id} in Jint successfully ✓");
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[JsRuntime] Jint note: {ex.Message} — running fallback card parser.");
                // Fallback parser ensuring registered cards are detected if JS runtime failed
                FallbackParseCards(code, mod.Manifest.Id, mod.RootDir);
            }
        }

        private void RegisterCardFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;

            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var id = idVal.AsString();
            if (string.IsNullOrEmpty(id)) return;

            var nameVal = obj.Get("name");
            var name = nameVal.IsString() ? nameVal.AsString() : id;

            var textVal = obj.Get("text");
            var text = textVal.IsString() ? textVal.AsString() : "";

            var colorVal = obj.Get("color");
            var color = colorVal.IsString() ? colorVal.AsString() : "attack";

            var rarityVal = obj.Get("rarity");
            var rarity = rarityVal.IsString() ? rarityVal.AsString() : "Common";

            var manaVal = obj.Get("mana");
            var mana = 1;
            if (manaVal.IsNumber()) mana = (int)manaVal.AsNumber();
            else if (manaVal.IsString() && manaVal.AsString() == "W") mana = 0;

            var damageVal = obj.Get("damage");
            var damage = damageVal.IsNumber() ? (int)damageVal.AsNumber() : 0;

            var armorVal = obj.Get("armor");
            var armor = armorVal.IsNumber() ? (int)armorVal.AsNumber() : 0;

            var gemSlotsVal = obj.Get("gemSlots");
            var gemSlots = gemSlotsVal.IsNumber() ? Math.Clamp((int)gemSlotsVal.AsNumber(), 0, 3) : 1;

            var weightVal = obj.Get("weight");
            var weight = weightVal.IsNumber() ? (int)weightVal.AsNumber() : 100;

            var pools = new List<string>();
            var dropVal = obj.Get("drop");
            if (dropVal is ObjectInstance dropObj)
            {
                var poolsVal = dropObj.Get("pools");
                if (poolsVal.IsArray())
                {
                    foreach (var elem in poolsVal.AsArray())
                    {
                        if (elem.IsString()) pools.Add(elem.AsString());
                    }
                }
            }
            if (pools.Count == 0) pools.Add("levelUp");

            var keywords = new List<string>();
            var kwVal = obj.Get("keywords");
            if (kwVal.IsArray())
            {
                foreach (var k in kwVal.AsArray())
                {
                    if (k.IsString()) keywords.Add(k.AsString());
                }
            }

            var target = new CustomTargetConfig();
            var targetVal = obj.Get("target");
            if (targetVal is ObjectInstance targetObj)
            {
                var scopeVal = targetObj.Get("scope");
                if (scopeVal.IsString()) target.Scope = scopeVal.AsString();

                var countVal = targetObj.Get("count");
                if (countVal.IsNumber()) target.Count = (int)countVal.AsNumber();
                else if (countVal.IsString() && countVal.AsString().Equals("all", StringComparison.OrdinalIgnoreCase)) target.Count = -1;

                var rowVal = targetObj.Get("row");
                if (rowVal.IsNumber()) target.Row = (int)rowVal.AsNumber();
                else if (rowVal.IsString())
                {
                    var rStr = rowVal.AsString().ToLowerInvariant();
                    if (rStr == "front") target.Row = 0;
                    else if (rStr == "back") target.Row = -2;
                    else if (rStr == "all") target.Row = -3;
                }

                var colVal = targetObj.Get("column");
                if (colVal.IsNumber()) target.Column = (int)colVal.AsNumber();

                var pierceVal = targetObj.Get("pierce");
                if (pierceVal.IsBoolean()) target.Pierce = pierceVal.AsBoolean();
                else if (pierceVal.IsNumber()) target.Pierce = pierceVal.AsNumber() > 0;

                var randVal = targetObj.Get("random");
                if (randVal.IsBoolean()) target.Random = randVal.AsBoolean();
            }

            var effects = new CustomEffectsConfig();
            var fxVal = obj.Get("effects");
            if (fxVal is ObjectInstance fxObj)
            {
                var freezeVal = fxObj.Get("freeze");
                if (freezeVal.IsNumber()) effects.Freeze = (int)freezeVal.AsNumber();

                var kbVal = fxObj.Get("knockback");
                if (kbVal.IsBoolean()) effects.Knockback = kbVal.AsBoolean();
                else if (kbVal.IsNumber()) effects.Knockback = kbVal.AsNumber() > 0;

                var dmgVal = fxObj.Get("damage");
                if (dmgVal.IsNumber()) effects.Damage = (int)dmgVal.AsNumber();

                var armVal = fxObj.Get("armor");
                if (armVal.IsNumber()) effects.Armor = (int)armVal.AsNumber();

                var healVal = fxObj.Get("heal");
                if (healVal.IsNumber()) effects.Heal = (int)healVal.AsNumber();

                var manaFxVal = fxObj.Get("mana");
                if (manaFxVal.IsNumber()) effects.Mana = (int)manaFxVal.AsNumber();

                var drawVal = fxObj.Get("draw");
                if (drawVal.IsNumber()) effects.Draw = (int)drawVal.AsNumber();

                var mightVal = fxObj.Get("might");
                if (mightVal.IsNumber()) effects.Might = (int)mightVal.AsNumber();

                var cartVal = fxObj.Get("minecart");
                if (cartVal.IsBoolean()) effects.Minecart = cartVal.AsBoolean();
                else if (cartVal is ObjectInstance cartObj)
                {
                    effects.Minecart = true;
                    var cartDmg = cartObj.Get("damage");
                    if (cartDmg.IsNumber()) effects.MinecartDamage = (int)cartDmg.AsNumber();
                }

                var disarmVal = fxObj.Get("disarm");
                if (disarmVal.IsBoolean()) effects.Disarm = disarmVal.AsBoolean();
                else if (disarmVal.IsNumber()) effects.Disarm = disarmVal.AsNumber() > 0;

                var eraseVal = fxObj.Get("erase");
                if (eraseVal.IsBoolean()) effects.Erase = eraseVal.AsBoolean();
                else if (eraseVal.IsNumber()) effects.Erase = eraseVal.AsNumber() > 0;

                var coinsVal = fxObj.Get("coins");
                if (coinsVal.IsNumber()) effects.Coins = (int)coinsVal.AsNumber();

                var xpVal = fxObj.Get("xp");
                if (xpVal.IsNumber()) effects.Xp = (int)xpVal.AsNumber();

                var comboVal = fxObj.Get("combo");
                if (comboVal.IsNumber()) effects.Combo = (int)comboVal.AsNumber();

                var revVal = fxObj.Get("reverseCombo");
                if (revVal.IsBoolean()) effects.ReverseCombo = revVal.AsBoolean();

                var sndVal = fxObj.Get("sound");
                if (sndVal.IsString()) effects.Sound = sndVal.AsString();
            }

            var retainVal = obj.Get("retain");
            var retain = retainVal.IsBoolean() && retainVal.AsBoolean();

            var ftpVal = obj.Get("freeToPlay");
            var freeToPlay = ftpVal.IsBoolean() && ftpVal.AsBoolean();

            var iconPath = "";
            var iconVal = obj.Get("icon");
            if (iconVal.IsString()) iconPath = iconVal.AsString();
            else
            {
                var ipVal = obj.Get("iconPath");
                if (ipVal.IsString()) iconPath = ipVal.AsString();
                else
                {
                    var spVal = obj.Get("sprite");
                    if (spVal.IsString()) iconPath = spVal.AsString();
                }
            }

            var bodyPath = "";
            var bodyVal = obj.Get("body");
            if (bodyVal.IsString()) bodyPath = bodyVal.AsString();
            else
            {
                var bpVal = obj.Get("bodyPath");
                if (bpVal.IsString()) bodyPath = bpVal.AsString();
                else
                {
                    var bgVal = obj.Get("background");
                    if (bgVal.IsString()) bodyPath = bgVal.AsString();
                }
            }

            if (effects.Freeze == 0 && keywords.Exists(k => k.Equals("Freeze", StringComparison.OrdinalIgnoreCase)))
            {
                effects.Freeze = 1;
            }
            if (!effects.Knockback && keywords.Exists(k => k.Equals("Knockback", StringComparison.OrdinalIgnoreCase)))
            {
                effects.Knockback = true;
            }
            if (!effects.Disarm && keywords.Exists(k => k.Equals("Disarm", StringComparison.OrdinalIgnoreCase)))
            {
                effects.Disarm = true;
            }
            if (!effects.Erase && keywords.Exists(k => k.Equals("Erase", StringComparison.OrdinalIgnoreCase)))
            {
                effects.Erase = true;
            }

            var def = new CustomCardDef
            {
                Id = id,
                Name = name,
                Text = text,
                Color = color,
                Mana = mana,
                Damage = damage,
                Armor = armor,
                GemSlots = gemSlots,
                Retain = retain,
                FreeToPlay = freeToPlay,
                IconPath = iconPath,
                BodyPath = bodyPath,
                ModDirectory = modDir,
                Rarity = rarity,
                Weight = weight,
                Pools = pools,
                Keywords = keywords,
                Target = target,
                Effects = effects
            };

            var onPlayVal = obj.Get("onPlay");
            Action<Nosebleed.Pancake.Models.PlayerModel, Nosebleed.Pancake.Models.CardModel, Nosebleed.Pancake.Models.IEncounterModel> onPlayAction = null;
            if (onPlayVal != null && !onPlayVal.IsUndefined() && !onPlayVal.IsNull())
            {
                onPlayAction = (player, cardModel, encounterModel) =>
                {
                    try
                    {
                        var ctx = engine.Evaluate("({})").AsObject();
                        ctx.Set("playerMana", JsValue.FromObject(engine, player.CachedMana));
                        ctx.Set("combo", JsValue.FromObject(engine, player.ComboMultiplier));
                        ctx.Set("damageMultiplier", JsValue.FromObject(engine, 1.0));
                        ctx.Set("addMana", JsValue.FromObject(engine, new Action<int>(n => player.TryAddMana(n, false, true))));
                        ctx.Set("addArmor", JsValue.FromObject(engine, new Action<int>(n => player.TryAddArmor(n, true))));
                        ctx.Set("heal", JsValue.FromObject(engine, new Action<int>(n => player.PlayerStats?.Health?.AddValue((float)n, true))));
                        ctx.Set("draw", JsValue.FromObject(engine, new Action<int>(n => player.DrawCards(n, encounterModel))));
                        ctx.Set("dealDamage", JsValue.FromObject(engine, new Action<int>(n => {
                            var tgts = CardExecutionHelper.GetTargetEnemies(def, encounterModel);
                            foreach (var t in tgts) CardExecutionHelper.HitEnemy(t, n, cardModel);
                        })));
                        ctx.Set("freeze", JsValue.FromObject(engine, new Action<int>(stacks => {
                            var tgts = CardExecutionHelper.GetTargetEnemies(def, encounterModel);
                            foreach (var t in tgts) CardExecutionHelper.FreezeEnemy(t, stacks > 0 ? stacks : 1);
                        })));
                        ctx.Set("knockback", JsValue.FromObject(engine, new Action(() => {
                            var tgts = CardExecutionHelper.GetTargetEnemies(def, encounterModel);
                            foreach (var t in tgts) CardExecutionHelper.KnockbackEnemy(t);
                        })));
                        ctx.Set("disarm", JsValue.FromObject(engine, new Action(() => {
                            var tgts = CardExecutionHelper.GetTargetEnemies(def, encounterModel);
                            foreach (var t in tgts) CardExecutionHelper.DisarmEnemy(t);
                        })));
                        ctx.Set("erase", JsValue.FromObject(engine, new Action(() => {
                            var tgts = CardExecutionHelper.GetTargetEnemies(def, encounterModel);
                            foreach (var t in tgts) CardExecutionHelper.EraseEnemy(t);
                        })));
                        ctx.Set("addCoins", JsValue.FromObject(engine, new Action<int>(n => player.AddCoins(n, true))));
                        ctx.Set("addXp", JsValue.FromObject(engine, new Action<int>(n => player.AddXp(n, true))));
                        ctx.Set("addCombo", JsValue.FromObject(engine, new Action<int>(n => {
                            if (player.CardCombo != null) player.CardCombo.Value = Math.Max(0, player.CardCombo.Value + n);
                        })));
                        ctx.Set("getCombo", JsValue.FromObject(engine, new Func<int>(() => player.CardCombo != null ? player.CardCombo.Value : 0)));
                        ctx.Set("reverseCombo", JsValue.FromObject(engine, new Action(() => player.SetComboReverse())));
                        ctx.Set("minecart", JsValue.FromObject(engine, new Action(() => {
                            CardExecutionHelper.TriggerMinecart(def, player, encounterModel, cardModel);
                        })));

                        engine.Invoke(onPlayVal, ctx);
                    }
                    catch (Exception ex)
                    {
                        LoaderLog.Error($"[JsRuntime] Error executing onPlay for card {id}: {ex.Message}");
                    }
                };
            }
            def.OnPlayAction = onPlayAction;

            CardBridge.RegisterCard(def);
        }

        private void RegisterArcanaFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;

            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var id = idVal.AsString();
            if (string.IsNullOrEmpty(id)) return;

            var nameVal = obj.Get("name");
            var name = nameVal.IsString() ? nameVal.AsString() : id;

            var textVal = obj.Get("text");
            var text = textVal.IsString() ? textVal.AsString() : "";

            var descVal = obj.Get("description");
            var desc = descVal.IsString() ? descVal.AsString() : "";

            var iconPath = "";
            var iconVal = obj.Get("icon");
            if (iconVal.IsString()) iconPath = iconVal.AsString();
            else
            {
                var ipVal = obj.Get("iconPath");
                if (ipVal.IsString()) iconPath = ipVal.AsString();
            }

            /*
            var def = new CustomArcanaDef
            {
                Id = id,
                Name = name,
                Text = text,
                Description = desc,
                IconPath = iconPath,
                ModDirectory = modDir
            };

            ArcanaBridge.RegisterArcana(def);
            */
        }

        // ── JsValue helpers (same conventions as RegisterCardFromJs) ──
        private static bool HasJsValue(JsValue v) => v != null && !v.IsUndefined() && !v.IsNull();

        private static string GetJsString(ObjectInstance obj, string key, string fallback = "")
        {
            if (obj == null) return fallback;
            var v = obj.Get(key);
            return v.IsString() ? v.AsString() : fallback;
        }

        private static int GetJsInt(ObjectInstance obj, string key, int fallback = 0)
        {
            if (obj == null) return fallback;
            var v = obj.Get(key);
            if (v.IsNumber()) return (int)v.AsNumber();
            return fallback;
        }

        private static double GetJsDouble(ObjectInstance obj, string key, double fallback = 0)
        {
            if (obj == null) return fallback;
            var v = obj.Get(key);
            if (v.IsNumber()) return v.AsNumber();
            return fallback;
        }

        private static bool GetJsBool(ObjectInstance obj, string key, bool fallback = false)
        {
            if (obj == null) return fallback;
            var v = obj.Get(key);
            if (v.IsBoolean()) return v.AsBoolean();
            if (v.IsNumber()) return v.AsNumber() > 0;
            return fallback;
        }

        private static List<string> GetJsStringArray(ObjectInstance obj, string key)
        {
            var list = new List<string>();
            if (obj == null) return list;
            var v = obj.Get(key);
            if (v.IsArray())
            {
                foreach (var elem in v.AsArray())
                {
                    if (elem.IsString()) list.Add(elem.AsString());
                }
            }
            return list;
        }

        private static List<int> GetJsIntArray(ObjectInstance obj, string key)
        {
            var list = new List<int>();
            if (obj == null) return list;
            var v = obj.Get(key);
            if (v.IsArray())
            {
                foreach (var elem in v.AsArray())
                {
                    if (elem.IsNumber()) list.Add((int)elem.AsNumber());
                }
            }
            return list;
        }

        /// Convert a JS option/setting value to a C# primitive (bool/double/string).
        private static object JsToOptionObject(JsValue v)
        {
            if (!HasJsValue(v)) return null;
            if (v.IsBoolean()) return v.AsBoolean();
            if (v.IsNumber()) return v.AsNumber();
            if (v.IsString()) return v.AsString();
            try { return v.ToString(); } catch { return null; }
        }

        private static string JsToOptionString(JsValue v)
        {
            var o = JsToOptionObject(v);
            if (o == null) return "";
            if (o is bool b) return b ? "true" : "false";
            if (o is double d) return d.ToString(CultureInfo.InvariantCulture);
            return o.ToString();
        }

        /// Convert a stored option string back to a typed JS-facing value.
        private static object OptionStringToValue(string s)
        {
            if (s == null) return "";
            if (bool.TryParse(s, out var b)) return b;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            return s;
        }

        private static string JsTabName(JsValue v) => v.IsString() ? v.AsString() : "";

        /// Enumerate own keys of a JS object using only proven engine APIs
        /// (SetValue/Evaluate/Get/AsArray) to avoid new interop surface.
        private static List<string> JsObjectKeys(Engine engine, JsValue val)
        {
            var keys = new List<string>();
            try
            {
                engine.SetValue("__tmpKeysProbe", val);
                var arr = engine.Evaluate("Object.keys(__tmpKeysProbe)");
                if (arr.IsArray())
                {
                    foreach (var k in arr.AsArray())
                    {
                        if (k.IsString()) keys.Add(k.AsString());
                    }
                }
                engine.Evaluate("delete globalThis.__tmpKeysProbe");
            }
            catch { }
            return keys;
        }

        private void RegisterWeaponFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var id = idVal.AsString();
            if (string.IsNullOrEmpty(id)) return;

            var pools = new List<string>();
            var weight = GetJsInt(obj, "weight", 100);
            var dropVal = obj.Get("drop");
            if (dropVal is ObjectInstance dropObj)
            {
                var dw = dropObj.Get("weight");
                if (dw.IsNumber()) weight = (int)dw.AsNumber();
                var poolsVal = dropObj.Get("pools");
                if (poolsVal.IsArray())
                {
                    foreach (var elem in poolsVal.AsArray())
                    {
                        if (elem.IsString()) pools.Add(elem.AsString());
                    }
                }
            }

            // evolutionFrom: string id, or a full EvolutionRecipe object.
            var evoFrom = "";
            var evoVal = obj.Get("evolutionFrom");
            if (evoVal.IsString()) evoFrom = evoVal.AsString();
            else if (evoVal is ObjectInstance evoObj)
            {
                evoFrom = GetJsString(evoObj, "primary", "");
                if (string.IsNullOrEmpty(evoFrom)) evoFrom = GetJsString(evoObj, "result", "");
            }

            var iconPath = GetJsString(obj, "icon", "");
            if (string.IsNullOrEmpty(iconPath)) iconPath = GetJsString(obj, "iconPath", "");

            WeaponBridge.RegisterWeapon(new CustomWeaponDef
            {
                Id = id,
                Name = GetJsString(obj, "name", id),
                Text = GetJsString(obj, "text", ""),
                Damage = GetJsInt(obj, "damage", 0),
                Color = GetJsString(obj, "color", "attack"),
                IconPath = iconPath,
                Rarity = GetJsString(obj, "rarity", "Common"),
                Weight = weight,
                Pools = pools,
                EvolutionFrom = evoFrom,
                ModDirectory = modDir
            });
        }

        private void RegisterCrawlerFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var id = idVal.AsString();
            if (string.IsNullOrEmpty(id)) return;

            var triggerWhen = "";
            var triggerEffect = "";
            var oncePerTurn = false;
            var trigVal = obj.Get("trigger");
            if (trigVal is ObjectInstance trigObj)
            {
                triggerWhen = GetJsString(trigObj, "when", "");
                triggerEffect = GetJsString(trigObj, "effect", "");
                oncePerTurn = GetJsBool(trigObj, "oncePerTurn", false);
            }

            var baseHealth = 0;
            var baseMana = 0;
            var baseMight = 0;
            var statsVal = obj.Get("baseStats");
            if (statsVal is ObjectInstance statsObj)
            {
                baseHealth = GetJsInt(statsObj, "health", 0);
                baseMana = GetJsInt(statsObj, "mana", 0);
                baseMight = GetJsInt(statsObj, "might", 0);
            }

            var iconPath = GetJsString(obj, "icon", "");
            if (string.IsNullOrEmpty(iconPath)) iconPath = GetJsString(obj, "iconPath", "");

            CrawlerBridge.Register(new CustomCrawlerDef
            {
                Id = id,
                Name = GetJsString(obj, "name", id),
                Text = GetJsString(obj, "text", ""),
                Colors = GetJsStringArray(obj, "colors"),
                TriggerWhen = triggerWhen,
                TriggerEffect = triggerEffect,
                OncePerTurn = oncePerTurn,
                BaseHealth = baseHealth,
                BaseMana = baseMana,
                BaseMight = baseMight,
                IconPath = iconPath,
                ModDirectory = modDir
            });
        }

        private void RegisterRelicFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var id = idVal.AsString();
            if (string.IsNullOrEmpty(id)) return;

            var options = new List<CustomRelicOption>();
            var optsVal = obj.Get("options");
            if (optsVal.IsArray())
            {
                foreach (var elem in optsVal.AsArray())
                {
                    if (elem is not ObjectInstance optObj) continue;
                    var key = GetJsString(optObj, "key", "");
                    if (string.IsNullOrEmpty(key)) continue;
                    var opt = new CustomRelicOption
                    {
                        Key = key,
                        Label = GetJsString(optObj, "label", key),
                        Kind = GetJsString(optObj, "kind", "toggle"),
                        Default = JsToOptionString(optObj.Get("default")),
                        Description = GetJsString(optObj, "description", "")
                    };
                    var choicesVal = optObj.Get("choices");
                    if (choicesVal.IsArray())
                    {
                        foreach (var c in choicesVal.AsArray())
                        {
                            opt.Choices.Add(JsToOptionString(c));
                        }
                    }
                    var minVal = optObj.Get("min");
                    if (minVal.IsNumber()) opt.Min = (float)minVal.AsNumber();
                    var maxVal = optObj.Get("max");
                    if (maxVal.IsNumber()) opt.Max = (float)maxVal.AsNumber();
                    options.Add(opt);
                }
            }

            var defaultOptions = new Dictionary<string, string>();
            var defOptsVal = obj.Get("defaultOptions");
            if (defOptsVal is ObjectInstance defOptsObj)
            {
                foreach (var k in JsObjectKeys(engine, defOptsVal))
                {
                    if (string.IsNullOrEmpty(k)) continue;
                    defaultOptions[k] = JsToOptionString(defOptsObj.Get(k));
                }
            }

            var iconPath = GetJsString(obj, "icon", "");
            if (string.IsNullOrEmpty(iconPath)) iconPath = GetJsString(obj, "iconPath", "");

            // Note: RelicDef.effects (CardEffectsConfig) has no C# field yet —
            // native application is pending assembly verification.
            RelicBridge.Register(new CustomRelicDef
            {
                Id = id,
                Name = GetJsString(obj, "name", id),
                Text = GetJsString(obj, "text", ""),
                FoundIn = GetJsString(obj, "foundIn", ""),
                Rarity = GetJsString(obj, "rarity", "Common"),
                IconPath = iconPath,
                Options = options,
                DefaultOptions = defaultOptions,
                UnlocksToggle = GetJsString(obj, "unlocksToggle", ""),
                ModDirectory = modDir
            });
        }

        private void SetRelicOptionFromJs(string relicId, string key, JsValue value)
        {
            if (string.IsNullOrEmpty(relicId) || string.IsNullOrEmpty(key)) return;
            RelicBridge.SetOption(relicId, key, JsToOptionString(value));
            try { FireHook("relicOptionChanged", relicId, key, JsToOptionObject(value)); } catch { }
        }

        private object GetRelicOptionValue(string relicId, string key)
        {
            if (string.IsNullOrEmpty(relicId) || string.IsNullOrEmpty(key)) return "";
            return OptionStringToValue(RelicBridge.GetOption(relicId, key));
        }

        private void RegisterToggleFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            var id = GetJsString(obj, "id", "");
            if (string.IsNullOrEmpty(id)) return;
            var conditions = new RunToggleCondition();
            var condVal = obj.Get("conditions");
            if (condVal is ObjectInstance condObj)
            {
                conditions.Biomes = GetJsStringArray(condObj, "biomes");
                conditions.MinFloor = GetJsInt(condObj, "minFloor", 0);
                conditions.MaxFloor = GetJsInt(condObj, "maxFloor", 0);
                conditions.RequiresRelics = GetJsStringArray(condObj, "requiresRelics");
            }
            RunToggleBridge.Register(new CustomRunToggleDef
            {
                Id = id,
                Name = GetJsString(obj, "name", id),
                Description = GetJsString(obj, "description", ""),
                DefaultEnabled = GetJsBool(obj, "defaultEnabled", false),
                Conditions = conditions,
                UnlocksVia = GetJsString(obj, "unlocksVia", ""),
                ModDirectory = modDir
            });
        }

        private void PatchToggleFromJs(string id, JsValue val, Engine engine)
        {
            if (string.IsNullOrEmpty(id) || val is not ObjectInstance patch) return;
            var def = RunToggleBridge.FindDef(id);
            if (def == null) { LoaderLog.Warn($"[JsRuntime] patch target toggle not found: {id}"); return; }
            var v = patch.Get("name");
            if (v.IsString()) def.Name = v.AsString();
            v = patch.Get("description");
            if (v.IsString()) def.Description = v.AsString();
            v = patch.Get("defaultEnabled");
            if (v.IsBoolean()) def.DefaultEnabled = v.AsBoolean();
            else if (v.IsNumber()) def.DefaultEnabled = v.AsNumber() != 0;
            v = patch.Get("unlocksVia");
            if (v.IsString()) def.UnlocksVia = v.AsString();
            v = patch.Get("conditions");
            if (v is ObjectInstance condObj)
            {
                if (def.Conditions == null) def.Conditions = new RunToggleCondition();
                var bv = condObj.Get("biomes");
                if (bv.IsArray())
                {
                    var list = new List<string>();
                    foreach (var e in bv.AsArray()) { if (e.IsString()) list.Add(e.AsString()); }
                    def.Conditions.Biomes = list;
                }
                var minV = condObj.Get("minFloor");
                if (minV.IsNumber()) def.Conditions.MinFloor = (int)minV.AsNumber();
                var maxV = condObj.Get("maxFloor");
                if (maxV.IsNumber()) def.Conditions.MaxFloor = (int)maxV.AsNumber();
                var rrV = condObj.Get("requiresRelics");
                if (rrV.IsArray())
                {
                    var list = new List<string>();
                    foreach (var e in rrV.AsArray()) { if (e.IsString()) list.Add(e.AsString()); }
                    def.Conditions.RequiresRelics = list;
                }
            }
        }

        private void SetToggleEnabledFromJs(string id, JsValue value)
        {
            if (string.IsNullOrEmpty(id)) return;
            RunToggleBridge.SetEnabled(id, ToggleJsToBool(value));
        }

        private void ToggleOnChangedFromJs(string id, JsValue cb, Engine engine)
        {
            if (string.IsNullOrEmpty(id) || cb == null || cb.IsUndefined() || cb.IsNull()) return;
            var toggleId = id;
            RunToggleBridge.OnChanged += (changedId, on) =>
            {
                if (!string.Equals(changedId, toggleId, StringComparison.OrdinalIgnoreCase)) return;
                try { engine.Invoke(cb, on); }
                catch (Exception ex) { LoaderLog.Warn($"[JsRuntime] toggle onChanged callback error for '{toggleId}': {ex.Message}"); }
            };
        }

        private void CheckToggleLevelEnterFromJs(JsValue biomeVal, JsValue floorVal)
        {
            try { RunToggleBridge.CheckLevelEnter(ToggleJsToString(biomeVal), ToggleJsToInt(floorVal)); }
            catch { }
        }

        private bool IsToggleActiveAtRuntimeFromJs(string id, JsValue biomeVal, JsValue floorVal)
        {
            if (string.IsNullOrEmpty(id)) return false;
            try { return RunToggleBridge.IsActiveAtRuntime(id, ToggleJsToString(biomeVal), ToggleJsToInt(floorVal)); }
            catch { return false; }
        }

        private static string ToggleJsToString(JsValue v)
        {
            if (!HasJsValue(v)) return "";
            if (v.IsString()) return v.AsString();
            if (v.IsNumber()) return v.AsNumber().ToString(CultureInfo.InvariantCulture);
            if (v.IsBoolean()) return v.AsBoolean() ? "true" : "false";
            return "";
        }

        private static int ToggleJsToInt(JsValue v)
        {
            if (!HasJsValue(v)) return 0;
            if (v.IsNumber()) return (int)v.AsNumber();
            if (v.IsString())
            {
                var s = v.AsString();
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pi)) return pi;
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var pd)) return (int)pd;
                return 0;
            }
            if (v.IsBoolean()) return v.AsBoolean() ? 1 : 0;
            return 0;
        }

        private static bool ToggleJsToBool(JsValue v)
        {
            if (!HasJsValue(v)) return false;
            if (v.IsBoolean()) return v.AsBoolean();
            if (v.IsNumber()) return v.AsNumber() != 0;
            if (v.IsString())
            {
                var s = v.AsString();
                return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1";
            }
            return false;
        }

        private void RegisterBiomeFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var id = idVal.AsString();
            if (string.IsNullOrEmpty(id)) return;

            var table = new List<BiomeEncounterEntry>();
            var tableVal = obj.Get("encounterTable");
            if (tableVal.IsArray())
            {
                foreach (var elem in tableVal.AsArray())
                {
                    if (elem is not ObjectInstance entryObj) continue;
                    table.Add(new BiomeEncounterEntry
                    {
                        EnemyId = GetJsString(entryObj, "enemyId", ""),
                        Weight = (float)GetJsDouble(entryObj, "weight", 0),
                        Floors = GetJsIntArray(entryObj, "floors")
                    });
                }
            }

            var modifiers = new Dictionary<string, object>();
            var modsVal = obj.Get("modifiers");
            if (modsVal is ObjectInstance modsObj)
            {
                foreach (var k in JsObjectKeys(engine, modsVal))
                {
                    if (string.IsNullOrEmpty(k)) continue;
                    modifiers[k] = JsToOptionObject(modsObj.Get(k));
                }
            }

            DungeonBridge.RegisterBiome(new CustomBiomeDef
            {
                Id = id,
                Name = GetJsString(obj, "name", id),
                Floors = GetJsInt(obj, "floors", 0),
                Difficulty = GetJsInt(obj, "difficulty", 0),
                Background = GetJsString(obj, "background", ""),
                Music = GetJsString(obj, "music", ""),
                EncounterTable = table,
                BossFloor = GetJsInt(obj, "bossFloor", 0),
                Modifiers = modifiers,
                ModDirectory = modDir
            });
        }

        private void RegisterRoomFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var id = idVal.AsString();
            if (string.IsNullOrEmpty(id)) return;

            var reward = new CustomRoomReward();
            var rewardVal = obj.Get("reward");
            if (rewardVal is ObjectInstance rewardObj)
            {
                reward.Coins = GetJsInt(rewardObj, "coins", 0);
                reward.Xp = GetJsInt(rewardObj, "xp", 0);
                reward.Cards = GetJsStringArray(rewardObj, "cards");
            }

            DungeonBridge.RegisterRoom(new CustomRoomTemplate
            {
                Id = id,
                Weight = GetJsInt(obj, "weight", 100),
                Tags = GetJsStringArray(obj, "tags"),
                BiomeId = GetJsString(obj, "biomeId", ""),
                MinFloor = GetJsInt(obj, "minFloor", 0),
                MaxFloor = GetJsInt(obj, "maxFloor", 0),
                Enemies = GetJsStringArray(obj, "enemies"),
                Reward = reward,
                ModDirectory = modDir
            });
        }

        private void RegisterMapFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var id = idVal.AsString();
            if (string.IsNullOrEmpty(id)) return;

            var rooms = new List<RoomMapDef>();
            var roomsVal = obj.Get("rooms");
            if (roomsVal.IsArray())
            {
                foreach (var elem in roomsVal.AsArray())
                {
                    if (elem is not ObjectInstance roomObj) continue;
                    var roomId = GetJsString(roomObj, "id", "");
                    if (string.IsNullOrEmpty(roomId)) continue;
                    rooms.Add(new RoomMapDef
                    {
                        Id = roomId,
                        Name = GetJsString(roomObj, "name", roomId),
                        Width = GetJsInt(roomObj, "width", 0),
                        Height = GetJsInt(roomObj, "height", 0),
                        Tiles = ParseMapTiles(roomObj.Get("tiles")),
                        Walls = ParseMapWalls(roomObj.Get("walls"), engine),
                        Props = ParseMapProps(roomObj.Get("props")),
                        Lights = ParseMapLights(roomObj.Get("lights")),
                        Exits = ParseMapExits(roomObj.Get("exits")),
                        EncounterTable = ParseMapEncounterTable(roomObj.Get("encounterTable"))
                    });
                }
            }

            var tileMaterials = new Dictionary<string, string>();
            var tmVal = obj.Get("tileMaterials");
            if (tmVal is ObjectInstance tmObj)
            {
                foreach (var k in JsObjectKeys(engine, tmVal))
                {
                    if (string.IsNullOrEmpty(k)) continue;
                    tileMaterials[k] = JsToOptionString(tmObj.Get(k));
                }
            }

            MapBridge.RegisterMap(new MapPackageDef
            {
                Id = id,
                Name = GetJsString(obj, "name", id),
                BiomeId = GetJsString(obj, "biomeId", ""),
                Rooms = rooms,
                TileMaterials = tileMaterials,
                ModDirectory = modDir
            });
        }

        private static List<string> ParseMapTiles(JsValue v)
        {
            var list = new List<string>();
            if (v == null || v.IsUndefined() || v.IsNull() || !v.IsArray()) return list;
            foreach (var elem in v.AsArray())
            {
                if (elem.IsString()) list.Add(elem.AsString());
                else if (elem.IsNumber()) list.Add(elem.AsNumber().ToString(CultureInfo.InvariantCulture));
                else if (elem.IsBoolean()) list.Add(elem.AsBoolean() ? "true" : "false");
                else if (elem.IsArray())
                {
                    foreach (var inner in elem.AsArray())
                    {
                        if (inner.IsString()) list.Add(inner.AsString());
                        else if (inner.IsNumber()) list.Add(inner.AsNumber().ToString(CultureInfo.InvariantCulture));
                    }
                }
            }
            return list;
        }

        private static WallDef ParseMapWalls(JsValue v, Engine engine)
        {
            if (v == null || v.IsUndefined() || v.IsNull()) return null;
            if (v is ObjectInstance wallsObj)
            {
                var tiles = new List<MapTile>();
                var tilesVal = wallsObj.Get("tiles");
                if (tilesVal.IsArray())
                {
                    foreach (var elem in tilesVal.AsArray())
                    {
                        if (elem is not ObjectInstance tileObj) continue;
                        tiles.Add(new MapTile
                        {
                            X = GetJsInt(tileObj, "x", 0),
                            Y = GetJsInt(tileObj, "y", 0),
                            Tile = GetJsString(tileObj, "tile", ""),
                            Walkable = GetJsBool(tileObj, "walkable", true)
                        });
                    }
                }
                return new WallDef
                {
                    Tiles = tiles,
                    Material = GetJsString(wallsObj, "material", "")
                };
            }
            return null;
        }

        private static List<PropPlacement> ParseMapProps(JsValue v)
        {
            var list = new List<PropPlacement>();
            if (v == null || v.IsUndefined() || v.IsNull() || !v.IsArray()) return list;
            foreach (var elem in v.AsArray())
            {
                if (elem is not ObjectInstance propObj) continue;
                var pid = GetJsString(propObj, "id", "");
                if (string.IsNullOrEmpty(pid)) continue;
                var drops = GetJsStringArray(propObj, "drops");
                var interact = GetJsString(propObj, "interact", "");
                if (string.IsNullOrEmpty(interact) && GetJsBool(propObj, "interactable", false)) interact = "interact";
                list.Add(new PropPlacement
                {
                    Id = pid,
                    Type = GetJsString(propObj, "type", ""),
                    X = GetJsInt(propObj, "x", 0),
                    Y = GetJsInt(propObj, "y", 0),
                    Destructible = GetJsBool(propObj, "destructible", false),
                    Hp = GetJsInt(propObj, "hp", 0),
                    Drops = drops,
                    Interact = interact
                });
            }
            return list;
        }

        private static List<LightSourceDef> ParseMapLights(JsValue v)
        {
            var list = new List<LightSourceDef>();
            if (v == null || v.IsUndefined() || v.IsNull() || !v.IsArray()) return list;
            foreach (var elem in v.AsArray())
            {
                if (elem is not ObjectInstance lightObj) continue;
                var lid = GetJsString(lightObj, "id", "");
                if (string.IsNullOrEmpty(lid)) continue;
                list.Add(new LightSourceDef
                {
                    Id = lid,
                    X = GetJsInt(lightObj, "x", 0),
                    Y = GetJsInt(lightObj, "y", 0),
                    Radius = (float)GetJsDouble(lightObj, "radius", 0),
                    Color = GetJsString(lightObj, "color", ""),
                    Flicker = GetJsBool(lightObj, "flicker", false)
                });
            }
            return list;
        }

        private static List<MapExit> ParseMapExits(JsValue v)
        {
            var list = new List<MapExit>();
            if (v == null || v.IsUndefined() || v.IsNull() || !v.IsArray()) return list;
            foreach (var elem in v.AsArray())
            {
                if (elem is not ObjectInstance exitObj) continue;
                var eid = GetJsString(exitObj, "id", "");
                if (string.IsNullOrEmpty(eid)) continue;
                var toRoom = GetJsString(exitObj, "toRoom", "");
                if (string.IsNullOrEmpty(toRoom)) toRoom = GetJsString(exitObj, "targetRoom", "");
                var dir = GetJsString(exitObj, "direction", "");
                if (string.IsNullOrEmpty(dir)) dir = GetJsString(exitObj, "targetExit", "");
                if (string.IsNullOrEmpty(dir)) dir = GetJsString(exitObj, "toExit", "");
                list.Add(new MapExit
                {
                    Id = eid,
                    X = GetJsInt(exitObj, "x", 0),
                    Y = GetJsInt(exitObj, "y", 0),
                    ToRoom = toRoom,
                    Direction = dir
                });
            }
            return list;
        }

        private static List<MapEncounterEntry> ParseMapEncounterTable(JsValue v)
        {
            var list = new List<MapEncounterEntry>();
            if (v == null || v.IsUndefined() || v.IsNull() || !v.IsArray()) return list;
            foreach (var elem in v.AsArray())
            {
                if (elem is not ObjectInstance entryObj) continue;
                var enemyId = GetJsString(entryObj, "enemyId", "");
                if (string.IsNullOrEmpty(enemyId)) continue;
                list.Add(new MapEncounterEntry
                {
                    EnemyId = enemyId,
                    Weight = (float)GetJsDouble(entryObj, "weight", 0)
                });
            }
            return list;
        }

        private void RegisterGemFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var id = idVal.AsString();
            if (string.IsNullOrEmpty(id)) return;

            GemBridge.Register(new CustomGemDef
            {
                Id = id,
                Name = GetJsString(obj, "name", id),
                Rarity = GetJsString(obj, "rarity", "Common"),
                Text = GetJsString(obj, "text", ""),
                Tags = GetJsStringArray(obj, "tags"),
                ChestTiers = GetJsIntArray(obj, "chestTiers"),
                ModDirectory = modDir
            });
        }

        private void RegisterEvolutionFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            var primary = GetJsString(obj, "primary", "");
            var result = GetJsString(obj, "result", "");
            if (string.IsNullOrEmpty(primary) || string.IsNullOrEmpty(result)) return;

            // secondary: single id or an array of alternative ids.
            var secondary = new List<string>();
            var secVal = obj.Get("secondary");
            if (secVal.IsString()) secondary.Add(secVal.AsString());
            else if (secVal.IsArray())
            {
                foreach (var s in secVal.AsArray())
                {
                    if (s.IsString()) secondary.Add(s.AsString());
                }
            }

            EvolutionBridge.Register(new CustomEvolutionRecipe
            {
                Primary = primary,
                Secondary = secondary,
                Result = result,
                ModDirectory = modDir
            });
        }

        private void RegisterSettingFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            // VcSettings: `id` wins; `key` is a back-compat alias.
            var id = GetJsString(obj, "id", "");
            if (string.IsNullOrEmpty(id)) id = GetJsString(obj, "key", "");
            if (string.IsNullOrEmpty(id)) return;

            var def = new CustomSettingDef
            {
                Id = id,
                Key = GetJsString(obj, "key", ""),
                Label = GetJsString(obj, "label", id),
                Tab = GetJsString(obj, "tab", ""),
                Kind = GetJsString(obj, "kind", "toggle"),
                Default = JsToOptionObject(obj.Get("default")),
                Description = GetJsString(obj, "description", ""),
                ModDirectory = modDir
            };
            var choicesVal = obj.Get("choices");
            if (choicesVal.IsArray())
            {
                foreach (var c in choicesVal.AsArray())
                {
                    def.Choices.Add(JsToOptionObject(c));
                }
            }
            var minVal = obj.Get("min");
            if (minVal.IsNumber()) { def.Min = minVal.AsNumber(); def.HasMin = true; }
            var maxVal = obj.Get("max");
            if (maxVal.IsNumber()) { def.Max = maxVal.AsNumber(); def.HasMax = true; }
            SettingBridge.Register(def);
        }

        private void SetSettingFromJs(string id, JsValue value)
        {
            if (string.IsNullOrEmpty(id)) return;
            SettingBridge.Set(id, JsToOptionObject(value));
        }

        private object GetSettingValue(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return SettingBridge.Get(id);
        }

        private void SetInverseFromJs(JsValue value)
        {
            var enabled = false;
            if (HasJsValue(value))
            {
                if (value.IsBoolean()) enabled = value.AsBoolean();
                else if (value.IsNumber()) enabled = value.AsNumber() != 0;
                else if (value.IsString())
                {
                    var s = value.AsString();
                    enabled = string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1";
                }
            }
            // InverseToggle fires OnToggled on change, which notifies stored JS
            // onToggle listeners + the inverseToggled hook (see static ctor).
            InverseToggle.SetEnabled(enabled);
            lock (_runModeState) { _runModeState["inverse"] = InverseToggle.IsEnabled(); }
        }

        private static string[] ListRunModeIds()
        {
            var ids = new List<string> { "inverse" };
            lock (_runModeState)
            {
                foreach (var k in _runModeState.Keys)
                {
                    if (!string.Equals(k, "inverse", StringComparison.OrdinalIgnoreCase) && !ids.Contains(k))
                        ids.Add(k);
                }
            }
            return ids.ToArray();
        }

        private static bool IsRunModeActive(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (string.Equals(id, "inverse", StringComparison.OrdinalIgnoreCase)) return InverseToggle.IsEnabled();
            lock (_runModeState) { return _runModeState.TryGetValue(id, out var v) && v; }
        }

        private void SetRunModeActive(string id, bool enabled)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (string.Equals(id, "inverse", StringComparison.OrdinalIgnoreCase))
            {
                InverseToggle.SetEnabled(enabled);
                lock (_runModeState) { _runModeState["inverse"] = InverseToggle.IsEnabled(); }
                return;
            }
            lock (_runModeState) { _runModeState[id] = enabled; }
            LoaderLog.Info($"[JsRuntime] run mode '{id}' {(enabled ? "activated" : "deactivated")}");
        }

        private static JsValue BuildRunFlags(Engine engine)
        {
            var o = engine.Evaluate("({})").AsObject();
            o.Set("inverse", JsValue.FromObject(engine, InverseToggle.IsEnabled()));
            lock (_runModeState)
            {
                foreach (var kv in _runModeState)
                {
                    if (string.Equals(kv.Key, "inverse", StringComparison.OrdinalIgnoreCase)) continue;
                    o.Set(kv.Key, JsValue.FromObject(engine, kv.Value));
                }
            }
            return o;
        }

        // ── patch entry points (JS partial objects applied onto live defs) ──
        private void PatchCardFromJs(string id, JsValue val)
        {
            if (string.IsNullOrEmpty(id) || val is not ObjectInstance patch) return;
            var def = CardBridge.RegisteredDefs.Find(x => x.Id == id);
            if (def == null) { LoaderLog.Warn($"[JsRuntime] patch target card not found: {id}"); return; }
            ApplyCardPatch(def, patch);
        }

        private static void ApplyCardPatch(CustomCardDef def, ObjectInstance patch)
        {
            var v = patch.Get("name");
            if (v.IsString()) def.Name = v.AsString();
            v = patch.Get("text");
            if (v.IsString()) def.Text = v.AsString();
            v = patch.Get("color");
            if (v.IsString()) def.Color = v.AsString();
            v = patch.Get("rarity");
            if (v.IsString()) def.Rarity = v.AsString();
            v = patch.Get("mana");
            if (v.IsNumber()) def.Mana = (int)v.AsNumber();
            else if (v.IsString() && v.AsString() == "W") def.Mana = 0;
            v = patch.Get("damage");
            if (v.IsNumber()) def.Damage = (int)v.AsNumber();
            v = patch.Get("armor");
            if (v.IsNumber()) def.Armor = (int)v.AsNumber();
            v = patch.Get("gemSlots");
            if (v.IsNumber()) def.GemSlots = Math.Clamp((int)v.AsNumber(), 0, 3);
            v = patch.Get("weight");
            if (v.IsNumber()) def.Weight = (int)v.AsNumber();
            v = patch.Get("retain");
            if (v.IsBoolean()) def.Retain = v.AsBoolean();
            v = patch.Get("freeToPlay");
            if (v.IsBoolean()) def.FreeToPlay = v.AsBoolean();
            v = patch.Get("icon");
            if (v.IsString()) def.IconPath = v.AsString();
            else
            {
                v = patch.Get("iconPath");
                if (v.IsString()) def.IconPath = v.AsString();
                else
                {
                    v = patch.Get("sprite");
                    if (v.IsString()) def.IconPath = v.AsString();
                }
            }
            v = patch.Get("body");
            if (v.IsString()) def.BodyPath = v.AsString();
            else
            {
                v = patch.Get("bodyPath");
                if (v.IsString()) def.BodyPath = v.AsString();
                else
                {
                    v = patch.Get("background");
                    if (v.IsString()) def.BodyPath = v.AsString();
                }
            }
            v = patch.Get("keywords");
            if (v.IsArray())
            {
                def.Keywords = new List<string>();
                foreach (var k in v.AsArray())
                {
                    if (k.IsString()) def.Keywords.Add(k.AsString());
                }
            }
            var dropVal = patch.Get("drop");
            if (dropVal is ObjectInstance dropObj)
            {
                var dw = dropObj.Get("weight");
                if (dw.IsNumber()) def.Weight = (int)dw.AsNumber();
                var poolsVal = dropObj.Get("pools");
                if (poolsVal.IsArray())
                {
                    def.Pools = new List<string>();
                    foreach (var elem in poolsVal.AsArray())
                    {
                        if (elem.IsString()) def.Pools.Add(elem.AsString());
                    }
                }
            }
            var targetVal = patch.Get("target");
            if (targetVal is ObjectInstance targetObj)
            {
                if (def.Target == null) def.Target = new CustomTargetConfig();
                var scope = targetObj.Get("scope");
                if (scope.IsString()) def.Target.Scope = scope.AsString();
                var count = targetObj.Get("count");
                if (count.IsNumber()) def.Target.Count = (int)count.AsNumber();
                else if (count.IsString() && count.AsString().Equals("all", StringComparison.OrdinalIgnoreCase)) def.Target.Count = -1;
                var row = targetObj.Get("row");
                if (row.IsNumber()) def.Target.Row = (int)row.AsNumber();
                else if (row.IsString())
                {
                    var rStr = row.AsString().ToLowerInvariant();
                    if (rStr == "front") def.Target.Row = 0;
                    else if (rStr == "back") def.Target.Row = -2;
                    else if (rStr == "all") def.Target.Row = -3;
                }
                var col = targetObj.Get("column");
                if (col.IsNumber()) def.Target.Column = (int)col.AsNumber();
                var pierce = targetObj.Get("pierce");
                if (pierce.IsBoolean()) def.Target.Pierce = pierce.AsBoolean();
                else if (pierce.IsNumber()) def.Target.Pierce = pierce.AsNumber() > 0;
                var rand = targetObj.Get("random");
                if (rand.IsBoolean()) def.Target.Random = rand.AsBoolean();
            }
            var fxVal = patch.Get("effects");
            if (fxVal is ObjectInstance fxObj)
            {
                if (def.Effects == null) def.Effects = new CustomEffectsConfig();
                ApplyPatchedEffects(def.Effects, fxObj);
            }
        }

        private static void ApplyPatchedEffects(CustomEffectsConfig effects, ObjectInstance fxObj)
        {
            var v = fxObj.Get("freeze");
            if (v.IsNumber()) effects.Freeze = (int)v.AsNumber();
            v = fxObj.Get("knockback");
            if (v.IsBoolean()) effects.Knockback = v.AsBoolean();
            else if (v.IsNumber()) effects.Knockback = v.AsNumber() > 0;
            v = fxObj.Get("damage");
            if (v.IsNumber()) effects.Damage = (int)v.AsNumber();
            v = fxObj.Get("armor");
            if (v.IsNumber()) effects.Armor = (int)v.AsNumber();
            v = fxObj.Get("heal");
            if (v.IsNumber()) effects.Heal = (int)v.AsNumber();
            v = fxObj.Get("mana");
            if (v.IsNumber()) effects.Mana = (int)v.AsNumber();
            v = fxObj.Get("draw");
            if (v.IsNumber()) effects.Draw = (int)v.AsNumber();
            v = fxObj.Get("might");
            if (v.IsNumber()) effects.Might = (int)v.AsNumber();
            v = fxObj.Get("minecart");
            if (v.IsBoolean()) effects.Minecart = v.AsBoolean();
            else if (v is ObjectInstance cartObj)
            {
                effects.Minecart = true;
                var cartDmg = cartObj.Get("damage");
                if (cartDmg.IsNumber()) effects.MinecartDamage = (int)cartDmg.AsNumber();
            }
            v = fxObj.Get("disarm");
            if (v.IsBoolean()) effects.Disarm = v.AsBoolean();
            else if (v.IsNumber()) effects.Disarm = v.AsNumber() > 0;
            v = fxObj.Get("erase");
            if (v.IsBoolean()) effects.Erase = v.AsBoolean();
            else if (v.IsNumber()) effects.Erase = v.AsNumber() > 0;
            v = fxObj.Get("coins");
            if (v.IsNumber()) effects.Coins = (int)v.AsNumber();
            v = fxObj.Get("xp");
            if (v.IsNumber()) effects.Xp = (int)v.AsNumber();
            v = fxObj.Get("combo");
            if (v.IsNumber()) effects.Combo = (int)v.AsNumber();
            v = fxObj.Get("reverseCombo");
            if (v.IsBoolean()) effects.ReverseCombo = v.AsBoolean();
            v = fxObj.Get("sound");
            if (v.IsString()) effects.Sound = v.AsString();
        }

        private void PatchWeaponFromJs(string id, JsValue val)
        {
            if (string.IsNullOrEmpty(id) || val is not ObjectInstance patch) return;
            var def = WeaponBridge.FindDef(id);
            if (def == null) { LoaderLog.Warn($"[JsRuntime] patch target weapon not found: {id}"); return; }
            var v = patch.Get("name");
            if (v.IsString()) def.Name = v.AsString();
            v = patch.Get("text");
            if (v.IsString()) def.Text = v.AsString();
            v = patch.Get("damage");
            if (v.IsNumber()) def.Damage = (int)v.AsNumber();
            v = patch.Get("color");
            if (v.IsString()) def.Color = v.AsString();
            v = patch.Get("icon");
            if (v.IsString()) def.IconPath = v.AsString();
            else
            {
                v = patch.Get("iconPath");
                if (v.IsString()) def.IconPath = v.AsString();
            }
            v = patch.Get("rarity");
            if (v.IsString()) def.Rarity = v.AsString();
            v = patch.Get("weight");
            if (v.IsNumber()) def.Weight = (int)v.AsNumber();
            var dropVal = patch.Get("drop");
            if (dropVal is ObjectInstance dropObj)
            {
                var dw = dropObj.Get("weight");
                if (dw.IsNumber()) def.Weight = (int)dw.AsNumber();
                var poolsVal = dropObj.Get("pools");
                if (poolsVal.IsArray())
                {
                    def.Pools = new List<string>();
                    foreach (var elem in poolsVal.AsArray())
                    {
                        if (elem.IsString()) def.Pools.Add(elem.AsString());
                    }
                }
            }
            v = patch.Get("evolutionFrom");
            if (v.IsString()) def.EvolutionFrom = v.AsString();
            else if (v is ObjectInstance evoObj)
            {
                var primary = GetJsString(evoObj, "primary", "");
                def.EvolutionFrom = string.IsNullOrEmpty(primary) ? GetJsString(evoObj, "result", def.EvolutionFrom) : primary;
            }
        }

        private void PatchCrawlerFromJs(string id, JsValue val)
        {
            if (string.IsNullOrEmpty(id) || val is not ObjectInstance patch) return;
            var def = CrawlerBridge.FindDef(id);
            if (def == null) { LoaderLog.Warn($"[JsRuntime] patch target crawler not found: {id}"); return; }
            var v = patch.Get("name");
            if (v.IsString()) def.Name = v.AsString();
            v = patch.Get("text");
            if (v.IsString()) def.Text = v.AsString();
            v = patch.Get("colors");
            if (v.IsArray())
            {
                def.Colors = new List<string>();
                foreach (var c in v.AsArray())
                {
                    if (c.IsString()) def.Colors.Add(c.AsString());
                }
            }
            var trigVal = patch.Get("trigger");
            if (trigVal is ObjectInstance trigObj)
            {
                var when = trigObj.Get("when");
                if (when.IsString()) def.TriggerWhen = when.AsString();
                var effect = trigObj.Get("effect");
                if (effect.IsString()) def.TriggerEffect = effect.AsString();
                var once = trigObj.Get("oncePerTurn");
                if (once.IsBoolean()) def.OncePerTurn = once.AsBoolean();
            }
            var statsVal = patch.Get("baseStats");
            if (statsVal is ObjectInstance statsObj)
            {
                var h = statsObj.Get("health");
                if (h.IsNumber()) def.BaseHealth = (int)h.AsNumber();
                var m = statsObj.Get("mana");
                if (m.IsNumber()) def.BaseMana = (int)m.AsNumber();
                var mi = statsObj.Get("might");
                if (mi.IsNumber()) def.BaseMight = (int)mi.AsNumber();
            }
            v = patch.Get("icon");
            if (v.IsString()) def.IconPath = v.AsString();
            else
            {
                v = patch.Get("iconPath");
                if (v.IsString()) def.IconPath = v.AsString();
            }
        }

        private void PatchRelicFromJs(string id, JsValue val, Engine engine)
        {
            if (string.IsNullOrEmpty(id) || val is not ObjectInstance patch) return;
            var def = RelicBridge.GetDef(id);
            if (def == null) { LoaderLog.Warn($"[JsRuntime] patch target relic not found: {id}"); return; }
            var v = patch.Get("name");
            if (v.IsString()) def.Name = v.AsString();
            v = patch.Get("text");
            if (v.IsString()) def.Text = v.AsString();
            v = patch.Get("foundIn");
            if (v.IsString()) def.FoundIn = v.AsString();
            v = patch.Get("rarity");
            if (v.IsString()) def.Rarity = v.AsString();
            v = patch.Get("icon");
            if (v.IsString()) def.IconPath = v.AsString();
            else
            {
                v = patch.Get("iconPath");
                if (v.IsString()) def.IconPath = v.AsString();
            }
            v = patch.Get("unlocksToggle");
            if (v.IsString()) def.UnlocksToggle = v.AsString();
            v = patch.Get("options");
            if (v.IsArray())
            {
                def.Options = new List<CustomRelicOption>();
                foreach (var elem in v.AsArray())
                {
                    if (elem is not ObjectInstance optObj) continue;
                    var key = GetJsString(optObj, "key", "");
                    if (string.IsNullOrEmpty(key)) continue;
                    var opt = new CustomRelicOption
                    {
                        Key = key,
                        Label = GetJsString(optObj, "label", key),
                        Kind = GetJsString(optObj, "kind", "toggle"),
                        Default = JsToOptionString(optObj.Get("default")),
                        Description = GetJsString(optObj, "description", "")
                    };
                    var choicesVal = optObj.Get("choices");
                    if (choicesVal.IsArray())
                    {
                        foreach (var c in choicesVal.AsArray())
                        {
                            opt.Choices.Add(JsToOptionString(c));
                        }
                    }
                    var minVal = optObj.Get("min");
                    if (minVal.IsNumber()) opt.Min = (float)minVal.AsNumber();
                    var maxVal = optObj.Get("max");
                    if (maxVal.IsNumber()) opt.Max = (float)maxVal.AsNumber();
                    def.Options.Add(opt);
                }
            }
            v = patch.Get("defaultOptions");
            if (v is ObjectInstance defOptsObj)
            {
                if (def.DefaultOptions == null) def.DefaultOptions = new Dictionary<string, string>();
                foreach (var k in JsObjectKeys(engine, v))
                    {
                        if (string.IsNullOrEmpty(k)) continue;
                        var sv = JsToOptionString(defOptsObj.Get(k));
                        def.DefaultOptions[k] = sv;
                        RelicBridge.SetOption(id, k, sv);
                    }
            }
        }

        private void PatchEnemyFromJs(string id, JsValue val)
        {
            if (string.IsNullOrEmpty(id) || val is not ObjectInstance patch) return;
            var def = EnemyBridge.GetDef(id);
            if (def == null) { LoaderLog.Warn($"[JsRuntime] patch target enemy not found: {id}"); return; }
            var v = patch.Get("name");
            if (v.IsString()) def.Name = v.AsString();
            v = patch.Get("health");
            if (v.IsNumber()) def.Health = (int)v.AsNumber();
            v = patch.Get("xp");
            if (v.IsNumber()) def.Xp = (int)v.AsNumber();
            v = patch.Get("boss");
            if (v.IsBoolean()) def.Boss = v.AsBoolean();
            v = patch.Get("maxHit");
            if (v.IsNumber()) def.MaxHit = (int)v.AsNumber();
            v = patch.Get("maxArmor");
            if (v.IsNumber()) def.MaxArmor = (int)v.AsNumber();
            v = patch.Get("spritePath");
            if (v.IsString()) def.SpritePath = v.AsString();
            v = patch.Get("frames");
            if (v.IsNumber()) def.Frames = (int)v.AsNumber();
            v = patch.Get("fps");
            if (v.IsNumber()) def.Fps = (int)v.AsNumber();
        }

        private void PatchBiomeFromJs(string id, JsValue val, Engine engine)
        {
            if (string.IsNullOrEmpty(id) || val is not ObjectInstance patch) return;
            var def = DungeonBridge.Biomes.Find(x => x.Id == id);
            if (def == null) { LoaderLog.Warn($"[JsRuntime] patch target biome not found: {id}"); return; }
            var v = patch.Get("name");
            if (v.IsString()) def.Name = v.AsString();
            v = patch.Get("floors");
            if (v.IsNumber()) def.Floors = (int)v.AsNumber();
            v = patch.Get("difficulty");
            if (v.IsNumber()) def.Difficulty = (int)v.AsNumber();
            v = patch.Get("background");
            if (v.IsString()) def.Background = v.AsString();
            v = patch.Get("music");
            if (v.IsString()) def.Music = v.AsString();
            v = patch.Get("bossFloor");
            if (v.IsNumber()) def.BossFloor = (int)v.AsNumber();
            v = patch.Get("encounterTable");
            if (v.IsArray())
            {
                def.EncounterTable = new List<BiomeEncounterEntry>();
                foreach (var elem in v.AsArray())
                {
                    if (elem is not ObjectInstance entryObj) continue;
                    def.EncounterTable.Add(new BiomeEncounterEntry
                    {
                        EnemyId = GetJsString(entryObj, "enemyId", ""),
                        Weight = (float)GetJsDouble(entryObj, "weight", 0),
                        Floors = GetJsIntArray(entryObj, "floors")
                    });
                }
            }
            v = patch.Get("modifiers");
            if (v is ObjectInstance modsObj)
            {
                if (def.Modifiers == null) def.Modifiers = new Dictionary<string, object>();
                foreach (var k in JsObjectKeys(engine, v))
                    {
                        if (string.IsNullOrEmpty(k)) continue;
                        def.Modifiers[k] = JsToOptionObject(modsObj.Get(k));
                    }
            }
        }

        private void PatchRoomFromJs(string id, JsValue val)
        {
            if (string.IsNullOrEmpty(id) || val is not ObjectInstance patch) return;
            var def = DungeonBridge.Rooms.Find(x => x.Id == id);
            if (def == null) { LoaderLog.Warn($"[JsRuntime] patch target room not found: {id}"); return; }
            var v = patch.Get("weight");
            if (v.IsNumber()) def.Weight = (int)v.AsNumber();
            v = patch.Get("tags");
            if (v.IsArray())
            {
                def.Tags = new List<string>();
                foreach (var t in v.AsArray())
                {
                    if (t.IsString()) def.Tags.Add(t.AsString());
                }
            }
            v = patch.Get("biomeId");
            if (v.IsString()) def.BiomeId = v.AsString();
            v = patch.Get("minFloor");
            if (v.IsNumber()) def.MinFloor = (int)v.AsNumber();
            v = patch.Get("maxFloor");
            if (v.IsNumber()) def.MaxFloor = (int)v.AsNumber();
            v = patch.Get("enemies");
            if (v.IsArray())
            {
                def.Enemies = new List<string>();
                foreach (var e in v.AsArray())
                {
                    if (e.IsString()) def.Enemies.Add(e.AsString());
                }
            }
            var rewardVal = patch.Get("reward");
            if (rewardVal is ObjectInstance rewardObj)
            {
                if (def.Reward == null) def.Reward = new CustomRoomReward();
                var coins = rewardObj.Get("coins");
                if (coins.IsNumber()) def.Reward.Coins = (int)coins.AsNumber();
                var xp = rewardObj.Get("xp");
                if (xp.IsNumber()) def.Reward.Xp = (int)xp.AsNumber();
                var cards = rewardObj.Get("cards");
                if (cards.IsArray())
                {
                    def.Reward.Cards = new List<string>();
                    foreach (var c in cards.AsArray())
                    {
                        if (c.IsString()) def.Reward.Cards.Add(c.AsString());
                    }
                }
            }
        }

        private void PatchMapFromJs(string id, JsValue val, Engine engine)
        {
            if (string.IsNullOrEmpty(id) || val is not ObjectInstance patch) return;
            var def = MapBridge.FindMap(id);
            if (def == null) { LoaderLog.Warn($"[JsRuntime] patch target map not found: {id}"); return; }
            var v = patch.Get("name");
            if (v.IsString()) def.Name = v.AsString();
            v = patch.Get("biomeId");
            if (v.IsString()) def.BiomeId = v.AsString();
            v = patch.Get("rooms");
            if (v.IsArray())
            {
                var rooms = new List<RoomMapDef>();
                foreach (var elem in v.AsArray())
                {
                    if (elem is not ObjectInstance roomObj) continue;
                    var roomId = GetJsString(roomObj, "id", "");
                    if (string.IsNullOrEmpty(roomId)) continue;
                    rooms.Add(new RoomMapDef
                    {
                        Id = roomId,
                        Name = GetJsString(roomObj, "name", roomId),
                        Width = GetJsInt(roomObj, "width", 0),
                        Height = GetJsInt(roomObj, "height", 0),
                        Tiles = ParseMapTiles(roomObj.Get("tiles")),
                        Walls = ParseMapWalls(roomObj.Get("walls"), engine),
                        Props = ParseMapProps(roomObj.Get("props")),
                        Lights = ParseMapLights(roomObj.Get("lights")),
                        Exits = ParseMapExits(roomObj.Get("exits")),
                        EncounterTable = ParseMapEncounterTable(roomObj.Get("encounterTable"))
                    });
                }
                def.Rooms = rooms;
            }
            v = patch.Get("tileMaterials");
            if (v is ObjectInstance tmObj)
            {
                if (def.TileMaterials == null) def.TileMaterials = new Dictionary<string, string>();
                foreach (var k in JsObjectKeys(engine, v))
                {
                    if (string.IsNullOrEmpty(k)) continue;
                    def.TileMaterials[k] = JsToOptionString(tmObj.Get(k));
                }
            }
        }

        private void PatchGemFromJs(string id, JsValue val)
        {
            if (string.IsNullOrEmpty(id) || val is not ObjectInstance patch) return;
            var def = GemBridge.FindDef(id);
            if (def == null) { LoaderLog.Warn($"[JsRuntime] patch target gem not found: {id}"); return; }
            var v = patch.Get("name");
            if (v.IsString()) def.Name = v.AsString();
            v = patch.Get("rarity");
            if (v.IsString()) def.Rarity = v.AsString();
            v = patch.Get("text");
            if (v.IsString()) def.Text = v.AsString();
            v = patch.Get("tags");
            if (v.IsArray())
            {
                def.Tags = new List<string>();
                foreach (var t in v.AsArray())
                {
                    if (t.IsString()) def.Tags.Add(t.AsString());
                }
            }
            v = patch.Get("chestTiers");
            if (v.IsArray())
            {
                def.ChestTiers = new List<int>();
                foreach (var t in v.AsArray())
                {
                    if (t.IsNumber()) def.ChestTiers.Add((int)t.AsNumber());
                }
            }
        }

        private void PatchArcanaFromJs(string id, JsValue val, Engine engine)
        {
            if (string.IsNullOrEmpty(id) || val is not ObjectInstance patch) return;
            var def = ArcanaBridge.FindDef(id);
            if (def == null) { LoaderLog.Warn($"[JsRuntime] patch target arcana not found: {id}"); return; }
            var v = patch.Get("name");
            if (v.IsString()) def.Name = v.AsString();
            v = patch.Get("text");
            if (v.IsString()) def.Text = v.AsString();
            v = patch.Get("description");
            if (v.IsString()) def.Description = v.AsString();
            v = patch.Get("icon");
            if (v.IsString()) def.IconPath = v.AsString();
            else
            {
                v = patch.Get("iconPath");
                if (v.IsString()) def.IconPath = v.AsString();
            }
            v = patch.Get("rarity");
            if (v.IsString()) def.Rarity = v.AsString();
            v = patch.Get("unlockId");
            if (v.IsString()) def.UnlockId = v.AsString();
            v = patch.Get("modifiers");
            if (v is ObjectInstance modsObj)
            {
                if (def.Modifiers == null) def.Modifiers = new Dictionary<string, string>();
                foreach (var k in JsObjectKeys(engine, v))
                {
                    if (string.IsNullOrEmpty(k)) continue;
                    def.Modifiers[k] = JsToOptionString(modsObj.Get(k));
                }
            }
            v = patch.Get("hooks");
            if (v.IsArray())
            {
                var list = new List<string>();
                foreach (var e in v.AsArray())
                {
                    if (e.IsString()) list.Add(e.AsString());
                }
                def.Hooks = list;
            }
        }

        private void FallbackParseCards(string code, string defaultModId, string modDir)
        {
            try
            {
                var idMatches = System.Text.RegularExpressions.Regex.Matches(code, @"id\s*:\s*""([^""]+)""");
                foreach (System.Text.RegularExpressions.Match idM in idMatches)
                {
                    var id = idM.Groups[1].Value;
                    if (string.IsNullOrEmpty(id) || id == defaultModId || !id.Contains(":") || CardBridge.RegisteredDefs.Exists(x => x.Id == id)) continue;

                    var idx = idM.Index;
                    // Route non-card defs (weapons/crawlers/relics/biomes/rooms/gems/maps)
                    // to their own bridges so Jint failure still surfaces them
                    // instead of misregistering them as cards.
                    var route = ClassifyFallbackRoute(code, idx);
                    if (route != FallbackRoute.Card)
                    {
                        FallbackRegisterRoutedDef(code, idx, id, route, modDir);
                        continue;
                    }
                    var snippet = code.Substring(Math.Max(0, idx - 50), Math.Min(900, code.Length - Math.Max(0, idx - 50)));

                    var nameM = System.Text.RegularExpressions.Regex.Match(snippet, @"name\s*:\s*""([^""]+)""");
                    var textM = System.Text.RegularExpressions.Regex.Match(snippet, @"text\s*:\s*""([^""]+)""");
                    var colorM = System.Text.RegularExpressions.Regex.Match(snippet, @"color\s*:\s*""([^""]+)""");
                    var manaM = System.Text.RegularExpressions.Regex.Match(snippet, @"mana\s*:\s*(\d+)");
                    var damageM = System.Text.RegularExpressions.Regex.Match(snippet, @"damage\s*:\s*(\d+)");
                    var armorM = System.Text.RegularExpressions.Regex.Match(snippet, @"armor\s*:\s*(\d+)");
                    var gemSlotsM = System.Text.RegularExpressions.Regex.Match(snippet, @"gemSlots\s*:\s*(\d+)");
                    var weightM = System.Text.RegularExpressions.Regex.Match(snippet, @"weight\s*:\s*(\d+)");
                    var rarityM = System.Text.RegularExpressions.Regex.Match(snippet, @"rarity\s*:\s*""([^""]+)""");

                    var target = new CustomTargetConfig();
                    var scopeM = System.Text.RegularExpressions.Regex.Match(snippet, @"scope\s*:\s*""([^""]+)""");
                    if (scopeM.Success) target.Scope = scopeM.Groups[1].Value;
                    var countM = System.Text.RegularExpressions.Regex.Match(snippet, @"count\s*:\s*(\d+)");
                    if (countM.Success) target.Count = int.Parse(countM.Groups[1].Value);

                    var effects = new CustomEffectsConfig();
                    var freezeM = System.Text.RegularExpressions.Regex.Match(snippet, @"freeze\s*:\s*(\d+)");
                    if (freezeM.Success) effects.Freeze = int.Parse(freezeM.Groups[1].Value);
                    if (snippet.Contains("knockback: true") || snippet.Contains("Knockback")) effects.Knockback = true;
                    if (snippet.Contains("disarm: true") || snippet.Contains("Disarm")) effects.Disarm = true;
                    if (snippet.Contains("erase: true") || snippet.Contains("Erase")) effects.Erase = true;
                    if (snippet.Contains("minecart: true") || snippet.Contains("minecart")) effects.Minecart = true;
                    var healM = System.Text.RegularExpressions.Regex.Match(snippet, @"heal\s*:\s*(\d+)");
                    if (healM.Success) effects.Heal = int.Parse(healM.Groups[1].Value);
                    var coinsM = System.Text.RegularExpressions.Regex.Match(snippet, @"coins\s*:\s*(\d+)");
                    if (coinsM.Success) effects.Coins = int.Parse(coinsM.Groups[1].Value);
                    var xpM = System.Text.RegularExpressions.Regex.Match(snippet, @"xp\s*:\s*(\d+)");
                    if (xpM.Success) effects.Xp = int.Parse(xpM.Groups[1].Value);
                    var comboM = System.Text.RegularExpressions.Regex.Match(snippet, @"combo\s*:\s*(\d+)");
                    if (comboM.Success) effects.Combo = int.Parse(comboM.Groups[1].Value);
                    if (snippet.Contains("reverseCombo: true") || snippet.Contains("reverseCombo:true")) effects.ReverseCombo = true;

                    var iconM = System.Text.RegularExpressions.Regex.Match(snippet, @"(?:icon|iconPath|sprite)\s*:\s*""([^""]+)""");
                    var bodyM = System.Text.RegularExpressions.Regex.Match(snippet, @"(?:body|bodyPath|background)\s*:\s*""([^""]+)""");
                    var def = new CustomCardDef
                    {
                        Id = id,
                        Name = nameM.Success ? nameM.Groups[1].Value : id,
                        Text = textM.Success ? textM.Groups[1].Value : "",
                        Color = colorM.Success ? colorM.Groups[1].Value : "attack",
                        Mana = manaM.Success ? int.Parse(manaM.Groups[1].Value) : 1,
                        Damage = damageM.Success ? int.Parse(damageM.Groups[1].Value) : 0,
                        Armor = armorM.Success ? int.Parse(armorM.Groups[1].Value) : 0,
                        GemSlots = gemSlotsM.Success ? Math.Clamp(int.Parse(gemSlotsM.Groups[1].Value), 0, 3) : 1,
                        Retain = snippet.Contains("retain: true"),
                        FreeToPlay = snippet.Contains("freeToPlay: true"),
                        IconPath = iconM.Success ? iconM.Groups[1].Value : "",
                        BodyPath = bodyM.Success ? bodyM.Groups[1].Value : "",
                        ModDirectory = modDir,
                        Rarity = rarityM.Success ? rarityM.Groups[1].Value : "Common",
                        Weight = weightM.Success ? int.Parse(weightM.Groups[1].Value) : 100,
                        Pools = new List<string> { "levelUp", "chestT1" },
                        Target = target,
                        Effects = effects
                    };

                    CardBridge.RegisterCard(def);
                }

                FallbackParseLinkedIds(code, modDir);
                FallbackParseEvolutions(code, modDir);
            }
            catch { }
        }

        private enum FallbackRoute { Card, Weapon, Crawler, Relic, Biome, Room, Gem, Map }

        /// Decide which registry a fallback `id: "..."` belongs to by looking
        /// at the nearest `register...(` call before it. Bare objects with no
        /// recognizable registration context keep the historical card behavior.
        private static FallbackRoute ClassifyFallbackRoute(string code, int idIndex)
        {
            try
            {
                var start = Math.Max(0, idIndex - 300);
                var prefix = code.Substring(start, idIndex - start);
                var regIdx = prefix.LastIndexOf("register", StringComparison.OrdinalIgnoreCase);
                var ctx = regIdx >= 0 ? prefix.Substring(regIdx) : "";
                if (ctx.IndexOf("weapon", StringComparison.OrdinalIgnoreCase) >= 0) return FallbackRoute.Weapon;
                if (ctx.IndexOf("crawl", StringComparison.OrdinalIgnoreCase) >= 0) return FallbackRoute.Crawler;
                if (ctx.IndexOf("relic", StringComparison.OrdinalIgnoreCase) >= 0) return FallbackRoute.Relic;
                if (ctx.IndexOf("biome", StringComparison.OrdinalIgnoreCase) >= 0 || ctx.IndexOf("dungeon", StringComparison.OrdinalIgnoreCase) >= 0) return FallbackRoute.Biome;
                if (ctx.IndexOf("room", StringComparison.OrdinalIgnoreCase) >= 0) return FallbackRoute.Room;
                if (ctx.IndexOf("gem", StringComparison.OrdinalIgnoreCase) >= 0) return FallbackRoute.Gem;
                if (ctx.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0) return FallbackRoute.Map;
            }
            catch { }
            return FallbackRoute.Card;
        }

        private static void FallbackRegisterRoutedDef(string code, int idIndex, string id, FallbackRoute route, string modDir)
        {
            try
            {
                var start = Math.Max(0, idIndex - 50);
                var snippet = code.Substring(start, Math.Min(900, code.Length - start));
                var nameM = System.Text.RegularExpressions.Regex.Match(snippet, @"name\s*:\s*""([^""]+)""");
                var textM = System.Text.RegularExpressions.Regex.Match(snippet, @"text\s*:\s*""([^""]+)""");
                var name = nameM.Success ? nameM.Groups[1].Value : id;
                var text = textM.Success ? textM.Groups[1].Value : "";
                switch (route)
                {
                    case FallbackRoute.Weapon:
                        if (WeaponBridge.FindDef(id) != null) return;
                        var dmgM = System.Text.RegularExpressions.Regex.Match(snippet, @"damage\s*:\s*(\d+)");
                        var rarityM = System.Text.RegularExpressions.Regex.Match(snippet, @"rarity\s*:\s*""([^""]+)""");
                        var weightM = System.Text.RegularExpressions.Regex.Match(snippet, @"weight\s*:\s*(\d+)");
                        var colorM = System.Text.RegularExpressions.Regex.Match(snippet, @"color\s*:\s*""([^""]+)""");
                        WeaponBridge.RegisterWeapon(new CustomWeaponDef
                        {
                            Id = id,
                            Name = name,
                            Text = text,
                            Damage = dmgM.Success ? int.Parse(dmgM.Groups[1].Value) : 0,
                            Color = colorM.Success ? colorM.Groups[1].Value : "attack",
                            Rarity = rarityM.Success ? rarityM.Groups[1].Value : "Common",
                            Weight = weightM.Success ? int.Parse(weightM.Groups[1].Value) : 100,
                            ModDirectory = modDir
                        });
                        break;
                    case FallbackRoute.Crawler:
                        if (CrawlerBridge.FindDef(id) != null) return;
                        CrawlerBridge.Register(new CustomCrawlerDef { Id = id, Name = name, Text = text, ModDirectory = modDir });
                        break;
                    case FallbackRoute.Relic:
                        if (RelicBridge.GetDef(id) != null) return;
                        var foundM = System.Text.RegularExpressions.Regex.Match(snippet, @"foundIn\s*:\s*""([^""]+)""");
                        var toggleM = System.Text.RegularExpressions.Regex.Match(snippet, @"unlocksToggle\s*:\s*""([^""]+)""");
                        var relicRarityM = System.Text.RegularExpressions.Regex.Match(snippet, @"rarity\s*:\s*""([^""]+)""");
                        RelicBridge.Register(new CustomRelicDef
                        {
                            Id = id,
                            Name = name,
                            Text = text,
                            FoundIn = foundM.Success ? foundM.Groups[1].Value : "",
                            Rarity = relicRarityM.Success ? relicRarityM.Groups[1].Value : "Common",
                            UnlocksToggle = toggleM.Success ? toggleM.Groups[1].Value : "",
                            ModDirectory = modDir
                        });
                        break;
                    case FallbackRoute.Biome:
                        if (DungeonBridge.Biomes.Exists(x => x.Id == id)) return;
                        var floorsM = System.Text.RegularExpressions.Regex.Match(snippet, @"floors\s*:\s*(\d+)");
                        var diffM = System.Text.RegularExpressions.Regex.Match(snippet, @"difficulty\s*:\s*(\d+)");
                        DungeonBridge.RegisterBiome(new CustomBiomeDef
                        {
                            Id = id,
                            Name = name,
                            Floors = floorsM.Success ? int.Parse(floorsM.Groups[1].Value) : 0,
                            Difficulty = diffM.Success ? int.Parse(diffM.Groups[1].Value) : 0,
                            ModDirectory = modDir
                        });
                        break;
                    case FallbackRoute.Room:
                        if (DungeonBridge.Rooms.Exists(x => x.Id == id)) return;
                        DungeonBridge.RegisterRoom(new CustomRoomTemplate { Id = id, ModDirectory = modDir });
                        break;
                    case FallbackRoute.Gem:
                        if (GemBridge.FindDef(id) != null) return;
                        var gemRarityM = System.Text.RegularExpressions.Regex.Match(snippet, @"rarity\s*:\s*""([^""]+)""");
                        GemBridge.Register(new CustomGemDef
                        {
                            Id = id,
                            Name = name,
                            Rarity = gemRarityM.Success ? gemRarityM.Groups[1].Value : "Common",
                            Text = text,
                            ModDirectory = modDir
                        });
                        break;
                    case FallbackRoute.Map:
                        if (MapBridge.FindMap(id) != null) return;
                        var mapBiomeM = System.Text.RegularExpressions.Regex.Match(snippet, @"biomeId\s*:\s*""([^""]+)""");
                        MapBridge.RegisterMap(new MapPackageDef
                        {
                            Id = id,
                            Name = name,
                            BiomeId = mapBiomeM.Success ? mapBiomeM.Groups[1].Value : "",
                            ModDirectory = modDir
                        });
                        break;
                }
            }
            catch { }
        }

        /// Surface cross-link ids (CardDef.weaponId/crawlerId, biome refs, ...)
        /// as minimal defs so they are visible even when Jint execution failed.
        private static void FallbackParseLinkedIds(string code, string modDir)
        {
            try
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(code, @"\b(weaponId|crawlerId|relicId|biomeId|mapId|roomId)\s*:\s*""([^""]+)""");
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    var kind = m.Groups[1].Value;
                    var id = m.Groups[2].Value;
                    if (string.IsNullOrEmpty(id) || !id.Contains(":")) continue;
                    switch (kind)
                    {
                        case "weaponId":
                            if (WeaponBridge.FindDef(id) == null)
                                WeaponBridge.RegisterWeapon(new CustomWeaponDef { Id = id, Name = id, ModDirectory = modDir });
                            break;
                        case "crawlerId":
                            if (CrawlerBridge.FindDef(id) == null)
                                CrawlerBridge.Register(new CustomCrawlerDef { Id = id, Name = id, ModDirectory = modDir });
                            break;
                        case "relicId":
                            if (RelicBridge.GetDef(id) == null)
                                RelicBridge.Register(new CustomRelicDef { Id = id, Name = id, ModDirectory = modDir });
                            break;
                        case "biomeId":
                            if (!DungeonBridge.Biomes.Exists(x => x.Id == id))
                                DungeonBridge.RegisterBiome(new CustomBiomeDef { Id = id, Name = id, ModDirectory = modDir });
                            break;
                        case "mapId":
                            if (MapBridge.FindMap(id) == null)
                                MapBridge.RegisterMap(new MapPackageDef { Id = id, Name = id, ModDirectory = modDir });
                            break;
                        case "roomId":
                            if (!DungeonBridge.Rooms.Exists(x => x.Id == id))
                                DungeonBridge.RegisterRoom(new CustomRoomTemplate { Id = id, ModDirectory = modDir });
                            break;
                    }
                }
            }
            catch { }
        }

        /// Surface evolution recipes (primary/secondary/result have no `id:`
        /// field, so the main loop never sees them).
        private static void FallbackParseEvolutions(string code, string modDir)
        {
            try
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(code, @"primary\s*:\s*""([^""]+)""");
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    var primary = m.Groups[1].Value;
                    if (string.IsNullOrEmpty(primary)) continue;
                    var snippet = code.Substring(m.Index, Math.Min(400, code.Length - m.Index));
                    var secM = System.Text.RegularExpressions.Regex.Match(snippet, @"secondary\s*:\s*(\[[^\]]*\]|""([^""]+)"")");
                    var resM = System.Text.RegularExpressions.Regex.Match(snippet, @"result\s*:\s*""([^""]+)""");
                    if (!resM.Success) continue;
                    var secondary = new List<string>();
                    if (secM.Success)
                    {
                        var raw = secM.Groups[1].Value;
                        foreach (System.Text.RegularExpressions.Match sm in System.Text.RegularExpressions.Regex.Matches(raw, @"""([^""]+)"""))
                            secondary.Add(sm.Groups[1].Value);
                    }
                    EvolutionBridge.Register(new CustomEvolutionRecipe
                    {
                        Primary = primary,
                        Secondary = secondary,
                        Result = resM.Groups[1].Value,
                        ModDirectory = modDir
                    });
                }
            }
            catch { }
        }

        private void RegisterEnemyFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var id = idVal.AsString();
            if (string.IsNullOrEmpty(id)) return;

            var def = new CustomEnemyDef { Id = id, ModDirectory = modDir };
            var nameVal = obj.Get("name");
            if (nameVal.IsString()) def.Name = nameVal.AsString();

            var healthVal = obj.Get("health");
            if (healthVal.IsNumber()) def.Health = (int)healthVal.AsNumber();

            var xpVal = obj.Get("xp");
            if (xpVal.IsNumber()) def.Xp = (int)xpVal.AsNumber();

            var bossVal = obj.Get("boss");
            if (bossVal.IsBoolean()) def.Boss = bossVal.AsBoolean();

            var maxHitVal = obj.Get("maxHit");
            if (maxHitVal.IsNumber()) def.MaxHit = (int)maxHitVal.AsNumber();

            var maxArmorVal = obj.Get("maxArmor");
            if (maxArmorVal.IsNumber()) def.MaxArmor = (int)maxArmorVal.AsNumber();

            var spritePathVal = obj.Get("spritePath");
            if (spritePathVal.IsString()) def.SpritePath = spritePathVal.AsString();

            var framesVal = obj.Get("frames");
            if (framesVal.IsNumber()) def.Frames = (int)framesVal.AsNumber();

            var fpsVal = obj.Get("fps");
            if (fpsVal.IsNumber()) def.Fps = (int)fpsVal.AsNumber();

            EnemyBridge.RegisterEnemy(def);
        }

        private void RegisterAudioFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var pathVal = obj.Get("path");
            if (!pathVal.IsString()) return;

            var def = new AudioDef
            {
                Id = idVal.AsString(),
                Path = pathVal.AsString(),
                ModDirectory = modDir
            };
            AudioBridge.RegisterAudio(def);
        }
    }

    internal static class ModRegistry
    {
        public static List<string> LoadedIds { get; } = new();
        public static List<(string Id, string? Rarity, int? Weight, List<string>? Pools)> RegisteredCards { get; } = new();
        public static void NoteLoaded(string id) { if (!LoadedIds.Contains(id)) LoadedIds.Add(id); }
        public static void NoteCard(string id, string? rarity, int? weight, List<string>? pools) => RegisteredCards.Add((id, rarity, weight, pools));
    }

    internal static class LoaderLog
    {
        public static void Info(string msg) => Console.WriteLine($"[PancakeModLoader] {msg}");
        public static void Warn(string msg) => Console.WriteLine($"[PancakeModLoader:WARN] {msg}");
        public static void Error(string msg) => Console.Error.WriteLine($"[PancakeModLoader:ERR] {msg}");
    }
}
