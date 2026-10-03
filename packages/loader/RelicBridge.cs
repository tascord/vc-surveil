using System;
using System.Collections.Generic;
using System.Linq;

namespace PancakeModLoader
{
    public class CustomRelicOption
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Kind { get; set; } = "toggle";
        public string Default { get; set; } = "";
        public List<string> Choices { get; set; } = new();
        public float Min { get; set; } = 0f;
        public float Max { get; set; } = 0f;
        public string Description { get; set; } = "";
    }

    public class CustomRelicDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Text { get; set; } = "";
        public string FoundIn { get; set; } = "";
        public string Rarity { get; set; } = "Common";
        public string IconPath { get; set; } = "";
        public List<CustomRelicOption> Options { get; set; } = new();
        public Dictionary<string, string> DefaultOptions { get; set; } = new();
        public string UnlocksToggle { get; set; } = "";
        public string ModDirectory { get; set; } = "";
    }

    public static class RelicBridge
    {
        public static List<CustomRelicDef> RegisteredDefs { get; } = new();
        public static HashSet<string> OwnedRelics { get; } = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, string> OptionState { get; } = new();

        // In-memory per-run state only. Persistence via beforeSave/afterLoad is pending.

        private static string OptionKey(string relicId, string key) => $"{relicId}:{key}";

        public static void Register(CustomRelicDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id))
            {
                LoaderLog.Warn("[RelicBridge] Register called with null/empty id; ignoring.");
                return;
            }
            RegisteredDefs.RemoveAll(x => string.Equals(x.Id, def.Id, StringComparison.OrdinalIgnoreCase));
            RegisteredDefs.Add(def);
            SeedDefaults(def);
            LoaderLog.Info($"[RelicBridge] Registered relic: {def.Id} ('{def.Name}') FoundIn='{def.FoundIn}' Rarity='{def.Rarity}' UnlocksToggle='{def.UnlocksToggle}'");
        }

        public static void RegisterRelic(CustomRelicDef def) => Register(def);

        public static void Patch(string id, Action<CustomRelicDef> apply)
        {
            var def = RegisteredDefs.Find(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (def == null) { LoaderLog.Warn($"[RelicBridge] Patch target not found: {id}"); return; }
            apply(def);
            SeedDefaults(def);
        }

        public static void PatchRelic(string id, Action<CustomRelicDef> apply) => Patch(id, apply);

        public static CustomRelicDef GetDef(string id) =>
            RegisteredDefs.Find(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));

        public static string[] ListIds() => RegisteredDefs.Select(x => x.Id).ToArray();

        public static void SetOption(string relicId, string key, string value)
        {
            if (string.IsNullOrEmpty(relicId) || string.IsNullOrEmpty(key)) return;
            OptionState[OptionKey(relicId, key)] = value ?? "";
        }

        public static string GetOption(string relicId, string key)
        {
            if (string.IsNullOrEmpty(relicId) || string.IsNullOrEmpty(key)) return "";
            if (OptionState.TryGetValue(OptionKey(relicId, key), out var v)) return v;
            var def = GetDef(relicId);
            if (def == null) return "";
            if (def.DefaultOptions != null && def.DefaultOptions.TryGetValue(key, out var d)) return d;
            var opt = def.Options?.Find(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
            return opt?.Default ?? "";
        }

        public static void NoteOwned(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (OwnedRelics.Add(id))
                LoaderLog.Info($"[RelicBridge] Relic owned: {id}");
        }

        public static bool IsOwned(string id) => !string.IsNullOrEmpty(id) && OwnedRelics.Contains(id);

        private static void SeedDefaults(CustomRelicDef def)
        {
            if (def.Options != null)
            {
                foreach (var opt in def.Options)
                {
                    if (opt == null || string.IsNullOrEmpty(opt.Key)) continue;
                    var k = OptionKey(def.Id, opt.Key);
                    if (!OptionState.ContainsKey(k))
                        OptionState[k] = opt.Default ?? "";
                }
            }
            if (def.DefaultOptions != null)
            {
                foreach (var kv in def.DefaultOptions)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    OptionState[OptionKey(def.Id, kv.Key)] = kv.Value ?? "";
                }
            }
        }
    }
}
