using System;
using System.Collections.Generic;
using System.Linq;

namespace PancakeModLoader
{
    /// Declarative weapon store.
    /// Native weapon pool hook pending assembly anchor — defs are kept for
    /// `vc.weapons` round-trips; no native pool injection is wired yet.
    public class CustomWeaponDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Text { get; set; } = "";
        public int Damage { get; set; } = 0;
        public string Color { get; set; } = "attack";
        public string IconPath { get; set; } = "";
        public string Rarity { get; set; } = "Common";
        public int Weight { get; set; } = 100;
        public List<string> Pools { get; set; } = new();
        public string EvolutionFrom { get; set; } = "";
        public string ModDirectory { get; set; } = "";
    }

    public static class WeaponBridge
    {
        public static List<CustomWeaponDef> RegisteredDefs { get; } = new();

        public static void RegisterWeapon(CustomWeaponDef def)
        {
            RegisteredDefs.RemoveAll(x => x.Id == def.Id);
            RegisteredDefs.Add(def);
            LoaderLog.Info($"[WeaponBridge] Registered weapon: {def.Id} ('{def.Name}') — declarative, native pool hook pending assembly anchor");
        }

        public static void Register(CustomWeaponDef def) => RegisterWeapon(def);

        public static void PatchWeapon(string id, Action<CustomWeaponDef> apply)
        {
            var def = RegisteredDefs.Find(x => x.Id == id);
            if (def == null) { LoaderLog.Warn($"[WeaponBridge] Patch target not found: {id}"); return; }
            apply(def);
        }

        public static void Patch(string id, Action<CustomWeaponDef> apply) => PatchWeapon(id, apply);

        public static CustomWeaponDef FindDef(string id) => RegisteredDefs.Find(x => x.Id == id);

        public static CustomWeaponDef GetDef(string id) => FindDef(id);

        public static string[] ListIds() => RegisteredDefs.Select(x => x.Id).ToArray();
    }
}
