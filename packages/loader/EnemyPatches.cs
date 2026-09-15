using HarmonyLib;
using Nosebleed.Pancake.GameConfig;
using Nosebleed.Pancake.Models;
using Nosebleed.Pancake.View;
using UnityEngine;
using System.IO;
using System.Linq;

namespace PancakeModLoader
{
    [HarmonyPatch(typeof(EnemyView))]
    public static class EnemyPatches
    {
        [HarmonyPatch(nameof(EnemyView.Awake))]
        [HarmonyPrefix]
        public static void AwakePrefix(EnemyView __instance)
        {
            var config = __instance.EnemyModel?.EnemyConfig;
            if (config == null || string.IsNullOrEmpty(config.name)) return;
            var def = EnemyBridge.GetDef(config.name);
            if (def != null)
            {
                LoaderLog.Info($"[EnemyPatches] Awake for {def.Id}");
                
                // Set up custom sprite animator
                if (!string.IsNullOrEmpty(def.SpritePath))
                {
                    string fullPath = Path.Combine(def.ModDirectory, def.SpritePath);
                    var animator = __instance.gameObject.GetComponent<ModSpriteAnimator>() ?? __instance.gameObject.AddComponent<ModSpriteAnimator>();
                    animator.Initialize(fullPath, def.Frames, def.Fps);
                }
            }
        }
    }
}
