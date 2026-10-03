using System;
using System.Collections.Generic;
using System.Linq;

namespace PancakeModLoader
{
    public class RunToggleCondition
    {
        public List<string> Biomes { get; set; } = new();
        public int MinFloor { get; set; } = 0;
        public int MaxFloor { get; set; } = 0;
        public List<string> RequiresRelics { get; set; } = new();
        public Dictionary<string, string> RequiresFlags { get; set; } = new();
    }

    public class CustomRunToggleDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public bool DefaultEnabled { get; set; } = false;
        public RunToggleCondition Conditions { get; set; } = new();
        public string UnlocksVia { get; set; } = "";
        public string ModDirectory { get; set; } = "";
    }

    public static class RunToggleBridge
    {
        public static List<CustomRunToggleDef> RegisteredDefs { get; } = new();

        private static readonly Dictionary<string, bool> _enabledState =
            new(StringComparer.OrdinalIgnoreCase);

        public static event Action<string, bool> OnChanged;

        public static void Register(CustomRunToggleDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id))
            {
                LoaderLog.Warn("[RunToggleBridge] Register called with null/empty id; ignoring.");
                return;
            }
            RegisteredDefs.RemoveAll(x => string.Equals(x.Id, def.Id, StringComparison.OrdinalIgnoreCase));
            RegisteredDefs.Add(def);
            if (!_enabledState.ContainsKey(def.Id))
                _enabledState[def.Id] = def.DefaultEnabled;
            LoaderLog.Info($"[RunToggleBridge] Registered toggle: {def.Id} ('{def.Name}') DefaultEnabled={def.DefaultEnabled} UnlocksVia='{def.UnlocksVia}'");
        }

        public static void Patch(string id, Action<CustomRunToggleDef> apply)
        {
            var def = FindDef(id);
            if (def == null) { LoaderLog.Warn($"[RunToggleBridge] Patch target not found: {id}"); return; }
            apply?.Invoke(def);
        }

        public static CustomRunToggleDef FindDef(string id) =>
            RegisteredDefs.Find(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));

        public static string[] ListIds() => RegisteredDefs.Select(x => x.Id).ToArray();

        public static bool IsUnlocked(string id)
        {
            var def = FindDef(id);
            if (def == null) return false;
            var hasUnlocksVia = !string.IsNullOrEmpty(def.UnlocksVia);
            var requiresRelics = def.Conditions?.RequiresRelics
                ?? new List<string>();
            var hasRequiresRelics = requiresRelics.Any(r => !string.IsNullOrEmpty(r));
            if (!hasUnlocksVia && !hasRequiresRelics) return true;
            try
            {
                if (hasUnlocksVia && RelicBridge.IsOwned(def.UnlocksVia))
                    return true;
                foreach (var relicId in requiresRelics)
                {
                    if (string.IsNullOrEmpty(relicId)) continue;
                    if (RelicBridge.IsOwned(relicId))
                        return true;
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[RunToggleBridge] IsUnlocked check failed for '{id}': {ex.Message}");
            }
            return false;
        }

        public static bool IsEnabled(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (_enabledState.TryGetValue(id, out var v)) return v;
            var def = FindDef(id);
            return def != null && def.DefaultEnabled;
        }

        public static void SetEnabled(string id, bool on)
        {
            var def = FindDef(id);
            if (def == null) { LoaderLog.Warn($"[RunToggleBridge] SetEnabled unknown toggle: {id}"); return; }
            if (!IsUnlocked(id))
            {
                LoaderLog.Warn($"[RunToggleBridge] Locked — toggle '{id}' requires owned relic (UnlocksVia='{def.UnlocksVia}').");
                return;
            }
            if (IsEnabled(id) == on) return;
            _enabledState[def.Id] = on;
            LoaderLog.Info($"[RunToggleBridge] {def.Id} {(on ? "enabled" : "disabled")}");
            try { OnChanged?.Invoke(def.Id, on); }
            catch (Exception ex) { LoaderLog.Warn($"[RunToggleBridge] OnChanged handler error: {ex.Message}"); }
        }

        public static void Toggle(string id)
        {
            SetEnabled(id, !IsEnabled(id));
        }

        public static void CheckLevelEnter(string biomeId, int floor)
        {
            var active = new List<string>();
            foreach (var def in RegisteredDefs)
            {
                try
                {
                    if (IsActiveAtRuntime(def.Id, biomeId, floor))
                        active.Add(def.Id);
                }
                catch (Exception ex)
                {
                    LoaderLog.Warn($"[RunToggleBridge] CheckLevelEnter check failed for '{def.Id}': {ex.Message}");
                }
            }
            LoaderLog.Info($"[RunToggleBridge] LevelEnter biome='{biomeId}' floor={floor} active=[{string.Join(",", active)}]");
        }

        public static bool IsActiveAtRuntime(string id, string biomeId, int floor)
        {
            var def = FindDef(id);
            if (def == null) return false;
            if (!IsEnabled(id)) return false;
            if (!IsUnlocked(id)) return false;
            var cond = def.Conditions;
            if (cond == null) return true;
            if (cond.Biomes != null && cond.Biomes.Count > 0)
            {
                if (string.IsNullOrEmpty(biomeId)) return false;
                if (!cond.Biomes.Any(b => string.Equals(b, biomeId, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }
            if (cond.MinFloor > 0 && floor < cond.MinFloor) return false;
            if (cond.MaxFloor > 0 && floor > cond.MaxFloor) return false;
            return true;
        }
    }
}
