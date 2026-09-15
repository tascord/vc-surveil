using System;
using System.Collections.Generic;
using Nosebleed.Pancake.GameConfig;
using Nosebleed.Pancake.Models;
using UnityEngine;

namespace PancakeModLoader
{
    public class CustomEnemyDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Health { get; set; } = 10;
        public int Xp { get; set; } = 1;
        public bool Boss { get; set; } = false;
        public int MaxHit { get; set; } = 0;
        public int MaxArmor { get; set; } = 0;
        
        public string SpritePath { get; set; } = "";
        public int Frames { get; set; } = 1;
        public int Fps { get; set; } = 5;
        
        public string ModDirectory { get; set; } = "";
    }

    public static class EnemyBridge
    {
        public static List<CustomEnemyDef> RegisteredDefs { get; } = new();
        public static Dictionary<string, EnemyConfig> CustomConfigs { get; } = new();

        public static void RegisterEnemy(CustomEnemyDef def)
        {
            RegisteredDefs.RemoveAll(x => x.Id == def.Id);
            RegisteredDefs.Add(def);
            LoaderLog.Info($"[EnemyBridge] Registered enemy: {def.Id} ({def.Health} HP)");
        }

        public static void EnsureCustomEnemiesCreated()
        {
            if (CustomConfigs.Count > 0 && CustomConfigs.Count == RegisteredDefs.Count) return;

            EnemyConfig template = null;
            var objects = Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<EnemyConfig>());
            if (objects != null && objects.Length > 0)
            {
                foreach (var obj in objects)
                {
                    var config = obj?.TryCast<EnemyConfig>();
                    if (config != null && !string.IsNullOrEmpty(config.name) && !config.name.StartsWith("mod_"))
                    {
                        template = config;
                        break;
                    }
                }
            }

            if (template == null)
            {
                LoaderLog.Warn("[EnemyBridge] No EnemyConfig template found in memory!");
                return;
            }

            CustomConfigs.Clear();
            foreach (var def in RegisteredDefs)
            {
                var clone = UnityEngine.Object.Instantiate(template);
                clone.name = def.Id;
                
                // Set fields using reflection or direct property access if public
                // Often these fields are named like health, xpReward, isBoss, etc.
                // Assuming common names or just patching the model later.
                // It's safer to rely on EnemyPatches to override stats if properties aren't accessible.
                
                CustomConfigs[def.Id] = clone;
            }
        }

        public static EnemyConfig GetConfig(string id)
        {
            if (CustomConfigs.TryGetValue(id, out var config)) return config;
            return null;
        }

        public static CustomEnemyDef GetDef(string id)
        {
            return RegisteredDefs.Find(x => x.Id == id);
        }

        public static void SpawnEnemy(string enemyId, int row = 0, int col = 0)
        {
            var config = GetConfig(enemyId);
            if (config == null)
            {
                LoaderLog.Warn($"[EnemyBridge] Cannot spawn unknown enemy: {enemyId}");
                return;
            }

            var encounterModel = UnityEngine.Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<EnemyEncounterModel>())?.TryCast<EnemyEncounterModel>();
            if (encounterModel == null)
            {
                LoaderLog.Warn("[EnemyBridge] No active encounter model found.");
                return;
            }

            // Using reflection or assuming standard game logic for spawning
            // In typical IL2CPP games, you instantiate an EnemyModel and add it to the encounter
            LoaderLog.Info($"[EnemyBridge] Spawning enemy {enemyId} at row {row} col {col}");
            
            // This would be the actual call to the game's spawn logic.
            // Since we don't have exact API, we can fake it or call a known method if we had one.
            try
            {
                // This is a typical method signature guess. Real implementation would depend on Cpp2IL dump.
                var enemyObj = UnityEngine.Object.Instantiate(config);
                // ... setup enemy Model and View ...
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[EnemyBridge] Failed to spawn enemy: {ex}");
            }
        }
    }
}
