using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace PancakeModLoader
{
    // This is the real BepInEx entrypoint — compiled when game BepInEx is present
    [BepInPlugin("com.vc.surveil.loader", "PancakeModLoader", "0.1.0")]
    public class BepInExPlugin : BasePlugin
    {
        public static BepInExPlugin Instance { get; private set; }
        public const string ModsFolderName = "Mods";
        private string _gameDir = "";
        private ModDiscovery _discovery = null!;
        private JsRuntime _js = null!;
        public readonly DevConsole Console = new();
        private int _modsCount;
        private List<string> _modIds = new();

        public int LoadedModsCount => _modsCount;
        public IReadOnlyList<string> LoadedModIds => _modIds;

        public override void Load()
        {
            Instance = this;
            _gameDir = Path.GetDirectoryName(typeof(BepInExPlugin).Assembly.Location) ?? "/home/flora/.local/share/Steam/steamapps/common/Vampire Crawlers";
            var g = _gameDir;
            try { g = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(BepInExPlugin).Assembly.Location)!, "..", "..")); } catch {}

            var surveilHome = Environment.GetEnvironmentVariable("VC_MODS_DIR")
                ?? Environment.GetEnvironmentVariable("SURVEIL_MODS_DIR")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vc-mods");

            _discovery = new ModDiscovery(Path.Combine(surveilHome), Path.Combine(g, ModsFolderName));
            _js = new JsRuntime(Console);
            CardBridge.Init(Console);

            Log.LogInfo($"PancakeModLoader Load — gameDir={g} primary={surveilHome}");

            // Hook unhandled exceptions for crash tracking
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    var ex = args.ExceptionObject as Exception;
                    string msg = ex != null ? ex.ToString() : args.ExceptionObject?.ToString() ?? "Unknown unhandled exception";
                    Log.LogFatal($"[CRASH] Unhandled Exception (isTerminating={args.IsTerminating}): {msg}");
                    try
                    {
                        var crashPath = Path.Combine(surveilHome, "last-crash.log");
                        File.WriteAllText(crashPath, $"Timestamp: {DateTime.UtcNow:O}\nType: UnhandledException\nIsTerminating: {args.IsTerminating}\nDetails:\n{msg}\n");
                    }
                    catch {}
                };
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not attach crash handlers: {ex.Message}");
            }

            // Apply Harmony patches (card reward pool, starting deck, on-play effects)
            try
            {
                var harmony = new Harmony("com.vc.surveil.loader");
                harmony.PatchAll(typeof(BepInExPlugin).Assembly);
                Log.LogInfo("Harmony patches applied successfully ✓");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to apply Harmony patches: {ex}");
            }

            // Register Unity MonoBehaviour for in-game Dev Console & HUD
            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<DevConsoleBehaviour>();
                ModSpriteAnimator.RegisterClass();
                DevConsoleBehaviour.EnsureHostExists();
                Log.LogInfo("DevConsoleBehaviour registered and attached in-game ✓");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to register DevConsoleBehaviour: {ex}");
            }

            var mods = _discovery.Discover();
            _modsCount = mods.Count;
            _modIds = mods.ConvertAll(m => m.Manifest.Id);
            Log.LogInfo($"Found {mods.Count} mod(s)");
            if (mods.Count == 0) Log.LogWarning("No mods found in ~/.vc-mods or game/Mods");

            foreach (var mod in mods)
            {
                try
                {
                    _js.LoadMod(mod);
                    ModRegistry.NoteLoaded(mod.Manifest.Id);
                    Log.LogInfo($"Loaded mod {mod.Manifest.Id} v{mod.Manifest.Version}");
                    Console.WriteLine($"loaded {mod.Manifest.Id}@{mod.Manifest.Version}");
                }
                catch (Exception ex)
                {
                    Log.LogError($"Failed {mod.Manifest.Id}: {ex}");
                    Console.WriteLine($"failed {mod.Manifest.Id}: {ex.Message}");
                }
            }

            Log.LogMessage($"VC Surveil: {_modsCount} mod(s) ready! Press Ctrl+Tab in-game for Dev Console.");
        }
    }
}
