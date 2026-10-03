using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Jint;
using Jint.Native;

namespace PancakeModLoader
{
    /// Persist per-run state (toggle enabled-states + relic options) as JSON
    /// to a mod-dir-adjacent file. Save/Load use only the public bridge
    /// surface (RunToggleBridge IsEnabled/SetEnabled/ListIds, RelicBridge
    /// SetOption/GetOption/ListIds); WireHooks injects persistence callbacks
    /// into JsRuntime's hook table so FireHook("beforeSave") saves and
    /// FireHook("afterLoad") loads. All failures are logged and contained.
    public static class RunStateStore
    {
        private static readonly object _lock = new object();
        private static string _defaultPath;
        private static bool _wired;

        private sealed class RunStatePayload
        {
            public Dictionary<string, bool> Toggles { get; set; } = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> RelicOptions { get; set; } = new Dictionary<string, string>();
        }

        public static string DefaultPath
        {
            get
            {
                lock (_lock)
                {
                    if (string.IsNullOrEmpty(_defaultPath))
                        _defaultPath = ResolveDefaultPath();
                    return _defaultPath;
                }
            }
        }

        /// Mod-dir-adjacent file: sibling of the first registered mod dir
        /// (<modDir>/../run-state.json), else CWD/run-state.json.
        private static string ResolveDefaultPath()
        {
            try
            {
                string modDir = null;
                var toggle = RunToggleBridge.RegisteredDefs.FirstOrDefault();
                if (toggle != null && !string.IsNullOrEmpty(toggle.ModDirectory))
                    modDir = toggle.ModDirectory;
                else
                {
                    var relic = RelicBridge.RegisteredDefs.FirstOrDefault();
                    if (relic != null && !string.IsNullOrEmpty(relic.ModDirectory))
                        modDir = relic.ModDirectory;
                }
                if (!string.IsNullOrEmpty(modDir))
                {
                    var trimmed = modDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string baseDir = null;
                    try { baseDir = Directory.GetParent(trimmed)?.FullName; } catch { }
                    if (string.IsNullOrEmpty(baseDir)) baseDir = trimmed;
                    return Path.Combine(baseDir, "run-state.json");
                }
            }
            catch { }
            try { return Path.Combine(Directory.GetCurrentDirectory(), "run-state.json"); }
            catch { return "run-state.json"; }
        }

        private static string EffectivePath(string path)
        {
            if (!string.IsNullOrEmpty(path)) return path;
            lock (_lock)
            {
                if (string.IsNullOrEmpty(_defaultPath))
                    _defaultPath = ResolveDefaultPath();
                return _defaultPath;
            }
        }

        public static void Save(string path = null)
        {
            var target = EffectivePath(path);
            if (!string.IsNullOrEmpty(target))
            {
                lock (_lock) { _defaultPath = target; }
            }
            try
            {
                var payload = new RunStatePayload();
                try
                {
                    foreach (var id in RunToggleBridge.ListIds() ?? new string[0])
                    {
                        if (string.IsNullOrEmpty(id)) continue;
                        try { payload.Toggles[id] = RunToggleBridge.IsEnabled(id); }
                        catch { }
                    }
                }
                catch { }
                try
                {
                    foreach (var kv in CollectRelicOptions())
                        payload.RelicOptions[kv.Key] = kv.Value;
                }
                catch { }
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                try
                {
                    var dir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }
                catch { }
                File.WriteAllText(target, json);
                LoaderLog.Info($"[RunStateStore] Saved run state to {target} ({payload.Toggles.Count} toggles, {payload.RelicOptions.Count} relic options)");
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[RunStateStore] Save failed for '{target}': {ex.Message}");
            }
        }

        public static void Load(string path = null)
        {
            var target = EffectivePath(path);
            if (!string.IsNullOrEmpty(target))
            {
                lock (_lock) { _defaultPath = target; }
            }
            try
            {
                if (!File.Exists(target))
                {
                    LoaderLog.Info($"[RunStateStore] No run-state file at {target}; skipping load.");
                    return;
                }
                var json = File.ReadAllText(target);
                var payload = JsonSerializer.Deserialize<RunStatePayload>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (payload == null) return;
                int toggles = 0, options = 0;
                if (payload.Toggles != null)
                {
                    foreach (var kv in payload.Toggles)
                    {
                        if (string.IsNullOrEmpty(kv.Key)) continue;
                        try { RunToggleBridge.SetEnabled(kv.Key, kv.Value); toggles++; }
                        catch { }
                    }
                }
                if (payload.RelicOptions != null)
                {
                    foreach (var kv in payload.RelicOptions)
                    {
                        if (string.IsNullOrEmpty(kv.Key)) continue;
                        try
                        {
                            var sep = kv.Key.IndexOf(':');
                            if (sep <= 0 || sep >= kv.Key.Length - 1) continue;
                            var relicId = kv.Key.Substring(0, sep);
                            var key = kv.Key.Substring(sep + 1);
                            RelicBridge.SetOption(relicId, key, kv.Value ?? "");
                            options++;
                        }
                        catch { }
                    }
                }
                LoaderLog.Info($"[RunStateStore] Loaded run state from {target} ({toggles} toggles, {options} relic options)");
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[RunStateStore] Load failed for '{target}': {ex.Message}");
            }
        }

        private static Dictionary<string, string> CollectRelicOptions()
        {
            var out_ = new Dictionary<string, string>();
            string[] ids = new string[0];
            try { ids = RelicBridge.ListIds() ?? new string[0]; } catch { }
            foreach (var relicId in ids)
            {
                if (string.IsNullOrEmpty(relicId)) continue;
                var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var def = RelicBridge.GetDef(relicId);
                    if (def != null)
                    {
                        if (def.Options != null)
                        {
                            foreach (var opt in def.Options)
                            {
                                if (opt != null && !string.IsNullOrEmpty(opt.Key))
                                    keys.Add(opt.Key);
                            }
                        }
                        if (def.DefaultOptions != null)
                        {
                            foreach (var k in def.DefaultOptions.Keys)
                            {
                                if (!string.IsNullOrEmpty(k)) keys.Add(k);
                            }
                        }
                    }
                }
                catch { }
                try
                {
                    var prefix = relicId + ":";
                    foreach (var k in RelicBridge.OptionState.Keys)
                    {
                        if (k != null && k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            var optKey = k.Substring(prefix.Length);
                            if (!string.IsNullOrEmpty(optKey)) keys.Add(optKey);
                        }
                    }
                }
                catch { }
                foreach (var key in keys)
                {
                    try { out_[relicId + ":" + key] = RelicBridge.GetOption(relicId, key) ?? ""; }
                    catch { }
                }
            }
            return out_;
        }

        /// Subscribe persistence to the JS hook table so FireHook("beforeSave")
        /// saves and FireHook("afterLoad") loads. Called once from the
        /// JsRuntime static ctor. Uses only proven Jint APIs
        /// (SetValue/Evaluate/Invoke) plus reflection over JsRuntime's private
        /// hook table; no game interop.
        public static void WireHooks(string defaultPath = null)
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(defaultPath))
                    _defaultPath = defaultPath;
                else if (string.IsNullOrEmpty(_defaultPath))
                    _defaultPath = ResolveDefaultPath();
                if (_wired) return;
                _wired = true;
            }
            try
            {
                var field = typeof(JsRuntime).GetField("_hookCallbacks", BindingFlags.NonPublic | BindingFlags.Static);
                if (field == null)
                {
                    LoaderLog.Warn("[RunStateStore] WireHooks: _hookCallbacks field not found; persistence hooks not wired.");
                    return;
                }
                var dictObj = field.GetValue(null);
                if (dictObj == null)
                {
                    LoaderLog.Warn("[RunStateStore] WireHooks: hook table null; persistence hooks not wired.");
                    return;
                }
                var engine = new Engine();
                engine.SetValue("__runStateSave", new Action<JsValue>(v => Save(JsArgToPath(v))));
                engine.SetValue("__runStateLoad", new Action<JsValue>(v => Load(JsArgToPath(v))));
                engine.Evaluate("function __onBeforeSave(p){ __runStateSave(p); }");
                engine.Evaluate("function __onAfterLoad(p){ __runStateLoad(p); }");
                var beforeCb = engine.Evaluate("__onBeforeSave");
                var afterCb = engine.Evaluate("__onAfterLoad");
                lock (dictObj)
                {
                    AddHookCallback(dictObj, "beforeSave", engine, beforeCb);
                    AddHookCallback(dictObj, "afterLoad", engine, afterCb);
                }
                LoaderLog.Info("[RunStateStore] Wired persistence hooks: beforeSave -> Save, afterLoad -> Load");
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[RunStateStore] WireHooks failed: {ex.Message}");
            }
        }

        private static string JsArgToPath(JsValue v)
        {
            try
            {
                if (v != null && v.IsString())
                {
                    var s = v.AsString();
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            catch { }
            lock (_lock)
            {
                if (string.IsNullOrEmpty(_defaultPath))
                    _defaultPath = ResolveDefaultPath();
                return _defaultPath;
            }
        }

        private static void AddHookCallback(object dictObj, string eventName, Engine engine, JsValue callback)
        {
            try
            {
                var dictType = dictObj.GetType();
                var tryGet = dictType.GetMethod("TryGetValue");
                var adder = dictType.GetMethod("Add");
                object listObj = null;
                if (tryGet != null)
                {
                    var args = new object[] { eventName, null };
                    bool found = false;
                    try { found = (bool)tryGet.Invoke(dictObj, args); } catch { }
                    if (found) listObj = args[1];
                }
                else
                {
                    try
                    {
                        var indexer = dictType.GetProperty("Item");
                        listObj = indexer?.GetValue(dictObj, new object[] { eventName });
                    }
                    catch { }
                }
                if (listObj == null)
                {
                    var elemType = typeof(ValueTuple<Engine, JsValue>);
                    var listType = typeof(List<>).MakeGenericType(elemType);
                    listObj = Activator.CreateInstance(listType);
                    try
                    {
                        if (adder != null) adder.Invoke(dictObj, new object[] { eventName, listObj });
                        else dictType.GetProperty("Item")?.SetValue(dictObj, listObj, new object[] { eventName });
                    }
                    catch { }
                }
                var add = listObj.GetType().GetMethod("Add");
                if (add == null) return;
                var tuple = new ValueTuple<Engine, JsValue>(engine, callback);
                add.Invoke(listObj, new object[] { tuple });
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[RunStateStore] AddHookCallback failed for '{eventName}': {ex.Message}");
            }
        }
    }
}
