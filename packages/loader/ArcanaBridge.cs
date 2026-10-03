using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PancakeModLoader
{
    /// Declarative arcana store (ported from ArcanaBridge.cs.bak).
    /// Native draft-pool / passive-application hook pending assembly anchor —
    /// defs are kept for `vc.arcanas` round-trips; native activation must be
    /// verified against game assemblies before claiming in-game behavior.
    /// Mirrors packages/api/src/index.ts ArcanaDef: rarity / unlockId /
    /// modifiers / hooks are stored verbatim for forward-compat.
    public class CustomArcanaDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Text { get; set; } = "";
        public string Description { get; set; } = "";
        public string IconPath { get; set; } = "";
        public string Rarity { get; set; } = "Common";
        public string UnlockId { get; set; } = "";
        public Dictionary<string, string> Modifiers { get; set; } = new();
        public List<string> Hooks { get; set; } = new();
        public string ModDirectory { get; set; } = "";
    }

    public static class ArcanaBridge
    {
        public static List<CustomArcanaDef> RegisteredDefs { get; } = new();

        /// Runtime-cloned game objects when native wiring succeeds.
        /// Kept as object so this file compiles without game interop;
        /// entries are `global::ArcanaConfig` instances when available.
        public static List<object> CustomArcanaConfigs { get; } = new();

        private static bool _arcanasCreated = false;

        public static void Register(CustomArcanaDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id))
            {
                LoaderLog.Warn("[ArcanaBridge] Register called with null/empty id; ignoring.");
                return;
            }
            RegisteredDefs.RemoveAll(x => string.Equals(x.Id, def.Id, StringComparison.OrdinalIgnoreCase));
            RegisteredDefs.Add(def);
            LoaderLog.Info($"[ArcanaBridge] Registered custom arcana: {def.Id} ('{def.Name}') Rarity='{def.Rarity}' UnlockId='{def.UnlockId}' IconPath='{def.IconPath}'");
        }

        public static void RegisterArcana(CustomArcanaDef def) => Register(def);

        public static void Patch(string id, Action<CustomArcanaDef> apply)
        {
            var def = RegisteredDefs.Find(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (def == null) { LoaderLog.Warn($"[ArcanaBridge] Patch target not found: {id}"); return; }
            try
            {
                apply?.Invoke(def);
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[ArcanaBridge] Patch failed for {id}: {ex.Message}");
            }
        }

        public static void PatchArcana(string id, Action<CustomArcanaDef> apply) => Patch(id, apply);

        public static CustomArcanaDef FindDef(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return RegisteredDefs.Find(x =>
                string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Name, id, StringComparison.OrdinalIgnoreCase));
        }

        /// Lookup by runtime game instance (replaces the .bak's
        /// FindDef(global::ArcanaConfig) without a compile-time game ref).
        /// Matches on the instance's `name` property via reflection.
        public static CustomArcanaDef FindDef(object arcana)
        {
            if (arcana == null) return null;
            string cname = null;
            try
            {
                var prop = arcana.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                cname = prop?.GetValue(arcana) as string;
            }
            catch { }
            if (string.IsNullOrEmpty(cname)) return null;

            string cleanName = cname.Replace("(Clone)", "").Trim();
            return RegisteredDefs.Find(x =>
                string.Equals(x.Id, cleanName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Name, cleanName, StringComparison.OrdinalIgnoreCase) ||
                cleanName.StartsWith(x.Id, StringComparison.OrdinalIgnoreCase) ||
                cleanName.EndsWith(x.Id, StringComparison.OrdinalIgnoreCase) ||
                cleanName.EndsWith(":" + x.Id, StringComparison.OrdinalIgnoreCase) ||
                cleanName.IndexOf(x.Id, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static CustomArcanaDef GetDef(string id) => FindDef(id);

        public static string[] ListIds() => RegisteredDefs.Select(x => x.Id).ToArray();

        public static object FindArcana(string idOrName)
        {
            EnsureCustomArcanasCreated();
            if (string.IsNullOrEmpty(idOrName)) return null;
            foreach (var c in CustomArcanaConfigs)
            {
                if (c == null) continue;
                string cname = null;
                try
                {
                    var prop = c.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                    cname = prop?.GetValue(c) as string;
                }
                catch { }
                if (string.IsNullOrEmpty(cname)) continue;
                if (string.Equals(cname, idOrName, StringComparison.OrdinalIgnoreCase) ||
                    cname.EndsWith(":" + idOrName, StringComparison.OrdinalIgnoreCase))
                {
                    return c;
                }
                var def = FindDef(c);
                if (def != null && string.Equals(def.Name, idOrName, StringComparison.OrdinalIgnoreCase))
                {
                    return c;
                }
            }
            return null;
        }

        /// Attempt native template clone; gracefully degrade to
        /// declarative-only storage when game refs are absent (CI / menu).
        public static void EnsureCustomArcanasCreated()
        {
            if (_arcanasCreated && CustomArcanaConfigs.Count > 0) return;
            if (RegisteredDefs.Count == 0) return;

            try
            {
                var template = FindArcanaTemplate();
                if (template == null)
                {
                    LoaderLog.Warn("[ArcanaBridge] No ArcanaConfig template found in memory! Running declarative-only (register/patch/list work; native draft-pool wiring pending assembly anchor).");
                    return;
                }

                string templateName = SafeObjectName(template);
                LoaderLog.Info($"[ArcanaBridge] Discovered template: '{templateName}'");

                CustomArcanaConfigs.Clear();
                foreach (var def in RegisteredDefs)
                {
                    try
                    {
                        var clone = CloneTemplate(template, def.Id);
                        if (clone == null)
                        {
                            LoaderLog.Warn($"[ArcanaBridge] Clone failed for {def.Id}; keeping declarative-only entry.");
                            continue;
                        }

                        TryResolveIcon(def, clone);

                        CustomArcanaConfigs.Add(clone);
                        LoaderLog.Info($"[ArcanaBridge] Created ArcanaConfig {def.Id} ('{def.Name}') from template '{templateName}'");
                    }
                    catch (Exception ex)
                    {
                        LoaderLog.Error($"[ArcanaBridge] Failed to create arcana {def.Id}: {ex.Message}");
                    }
                }

                if (CustomArcanaConfigs.Count > 0)
                {
                    _arcanasCreated = true;
                    LoaderLog.Info($"[ArcanaBridge] Successfully created {CustomArcanaConfigs.Count} custom arcanas in-game!");
                }
                else
                {
                    LoaderLog.Warn("[ArcanaBridge] Template clone produced no instances; running declarative-only.");
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[ArcanaBridge] Failed to create custom arcanas: {ex.Message}");
            }
        }

        /// Icon resolution via SpriteLoader with try/catch. Uses reflection so
        /// this file compiles without game interop; at runtime it forwards to
        /// PancakeModLoader.SpriteLoader.LoadSprite(iconPath, modDirectory).
        private static void TryResolveIcon(CustomArcanaDef def, object arcanaInstance)
        {
            if (def == null || arcanaInstance == null) return;
            if (string.IsNullOrEmpty(def.IconPath)) return;
            try
            {
                var loaderType = Type.GetType("PancakeModLoader.SpriteLoader, PancakeModLoader")
                    ?? AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => SafeGetType(a, "PancakeModLoader.SpriteLoader"))
                        .FirstOrDefault(t => t != null);
                if (loaderType == null)
                {
                    LoaderLog.Warn("[ArcanaBridge] SpriteLoader not available; skipping icon for " + def.Id);
                    return;
                }
                var method = loaderType.GetMethod("LoadSprite", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    LoaderLog.Warn("[ArcanaBridge] SpriteLoader.LoadSprite not found; skipping icon for " + def.Id);
                    return;
                }
                var sprite = method.Invoke(null, new object[] { def.IconPath, def.ModDirectory });
                if (sprite == null)
                {
                    LoaderLog.Info($"[ArcanaBridge] LoadSprite returned NULL for arcana {def.Id} (iconPath='{def.IconPath}')");
                    return;
                }
                try
                {
                    var spriteProp = arcanaInstance.GetType().GetProperty("sprite", BindingFlags.Public | BindingFlags.Instance);
                    spriteProp?.SetValue(arcanaInstance, sprite);
                    LoaderLog.Info($"[ArcanaBridge] Assigned custom sprite to arcana {def.Id} ✓ (via SpriteLoader)");
                }
                catch (Exception ex)
                {
                    LoaderLog.Warn($"[ArcanaBridge] Could not assign sprite to arcana {def.Id}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[ArcanaBridge] Error loading sprite for arcana {def.Id}: {ex.Message}");
            }
        }

        private static object FindArcanaTemplate()
        {
            try
            {
                var arcanaType = ResolveArcanaConfigType();
                if (arcanaType == null) return null;

                // UnityEngine.Resources.FindObjectsOfTypeAll(Type) via reflection.
                var resourcesType = ResolveUnityType("UnityEngine.Resources");
                if (resourcesType == null) return null;
                var findAll = resourcesType.GetMethod("FindObjectsOfTypeAll", new[] { typeof(Type) });
                if (findAll == null) return null;

                // Il2Cpp type wrapper: Il2CppInterop.Runtime.Il2CppType.Of<T>() via reflection.
                var il2cppTypeOf = ResolveIl2CppTypeOf(arcanaType);
                if (il2cppTypeOf == null) return null;

                var objects = findAll.Invoke(null, new object[] { il2cppTypeOf }) as Array;
                if (objects == null || objects.Length == 0) return null;

                foreach (var obj in objects)
                {
                    if (obj == null) continue;
                    string name = SafeObjectName(obj);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (name.Contains("demo-mod")) continue;
                    if (name.StartsWith("Test", StringComparison.OrdinalIgnoreCase)) continue;
                    return obj;
                }
            }
            catch { }
            return null;
        }

        private static object CloneTemplate(object template, string newName)
        {
            try
            {
                var objectType = ResolveUnityType("UnityEngine.Object");
                if (objectType == null) return null;
                var instantiate = objectType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Instantiate" && m.GetParameters().Length == 1);
                if (instantiate == null) return null;
                var clone = instantiate.Invoke(null, new object[] { template });
                if (clone == null) return null;
                try
                {
                    var nameProp = clone.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                    nameProp?.SetValue(clone, newName);
                }
                catch { }
                try
                {
                    // hideFlags = DontUnloadUnusedAsset | HideAndDontSave when available.
                    var hideProp = clone.GetType().GetProperty("hideFlags", BindingFlags.Public | BindingFlags.Instance);
                    if (hideProp != null && hideProp.PropertyType.IsEnum)
                    {
                        int combined = 0;
                        foreach (var v in Enum.GetValues(hideProp.PropertyType))
                        {
                            string n = v.ToString();
                            if (n == "DontUnloadUnusedAsset" || n == "HideAndDontSave")
                                combined |= Convert.ToInt32(v);
                        }
                        if (combined != 0)
                            hideProp.SetValue(clone, Enum.ToObject(hideProp.PropertyType, combined));
                    }
                }
                catch { }
                return clone;
            }
            catch
            {
                return null;
            }
        }

        private static Type ResolveArcanaConfigType()
        {
            // Prefer the global type; fall back to any ArcanaConfig in loaded assemblies.
            var t = Type.GetType("ArcanaConfig");
            if (t != null) return t;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var found = SafeGetType(a, "ArcanaConfig");
                if (found != null) return found;
                try
                {
                    Type[] types;
                    try { types = a.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(x => x != null).ToArray(); }
                    var match = types.FirstOrDefault(x => x != null && x.Name == "ArcanaConfig");
                    if (match != null) return match;
                }
                catch { }
            }
            return null;
        }

        private static Type ResolveUnityType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var found = SafeGetType(a, fullName);
                if (found != null) return found;
            }
            return null;
        }

        private static object ResolveIl2CppTypeOf(Type arcanaType)
        {
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type il2cppType;
                    try { il2cppType = a.GetType("Il2CppInterop.Runtime.Il2CppType"); }
                    catch { continue; }
                    if (il2cppType == null) continue;
                    var of = il2cppType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "Of" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
                    if (of == null) continue;
                    return of.MakeGenericMethod(arcanaType).Invoke(null, null);
                }
            }
            catch { }
            return null;
        }

        private static Type SafeGetType(Assembly a, string fullName)
        {
            try { return a.GetType(fullName, false); }
            catch { return null; }
        }

        private static string SafeObjectName(object obj)
        {
            try
            {
                var prop = obj.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                return prop?.GetValue(obj) as string;
            }
            catch { return null; }
        }
    }
}
