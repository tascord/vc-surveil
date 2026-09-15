using System;
using System.Collections.Generic;
using System.IO;

namespace PancakeModLoader
{
    // Scaffold plugin for CI — compiled when BepInEx not present.
    // Real entrypoint when game present is BepInExPlugin (Plugin.BepInEx.cs).
    public class Plugin
    {
        public const string ModsFolderName = "Mods";
        private readonly string _gameDir;
        private readonly ModDiscovery _discovery;
        private readonly JsRuntime _js;
        public readonly DevConsole Console = new();

        private int _modsCount;
        private List<string> _modIds = new();

        public Plugin(string? gameDir = null)
        {
            _gameDir = gameDir ?? Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".";
            var surveilHome = Environment.GetEnvironmentVariable("VC_MODS_DIR")
                ?? Environment.GetEnvironmentVariable("SURVEIL_MODS_DIR")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vc-mods");
            _discovery = new ModDiscovery(Path.Combine(surveilHome), Path.Combine(_gameDir, ModsFolderName));
            _js = new JsRuntime();
        }

        public void Awake()
        {
            LoaderLog.Info($"Awake — gameDir={_gameDir}");
            var envMods = Environment.GetEnvironmentVariable("VC_MODS_DIR") ?? Environment.GetEnvironmentVariable("SURVEIL_MODS_DIR");
            if (!string.IsNullOrEmpty(envMods)) LoaderLog.Info($"VC_MODS_DIR={envMods}");
            var mods = _discovery.Discover();
            _modsCount = mods.Count;
            _modIds = mods.ConvertAll(m => m.Manifest.Id);
            LoaderLog.Info($"Found {mods.Count} mod(s) — use ` for console");
            foreach (var mod in mods)
            {
                try
                {
                    _js.LoadMod(mod);
                    ModRegistry.NoteLoaded(mod.Manifest.Id);
                    LoaderLog.Info($"Loaded mod {mod.Manifest.Id} v{mod.Manifest.Version}");
                    Console.WriteLine($"loaded {mod.Manifest.Id}@{mod.Manifest.Version}");
                }
                catch (Exception ex)
                {
                    LoaderLog.Error($"Failed {mod.Manifest.Id}: {ex}");
                    Console.WriteLine($"failed {mod.Manifest.Id}: {ex.Message}");
                }
            }
            Console.WriteLine($"VC Surveil ready — {mods.Count} mod(s). Press ` or F1 for console. Try 'help'.");
        }

        public void Update() => Console.Update();

        public void OnGUI()
        {
            try
            {
                var guiType = Type.GetType("UnityEngine.GUI, UnityEngine.CoreModule");
                var guilayoutType = Type.GetType("UnityEngine.GUILayout, UnityEngine.CoreModule");
                if (guiType != null && guilayoutType != null)
                {
                    var rectType = Type.GetType("UnityEngine.Rect, UnityEngine.CoreModule");
                    var rect = Activator.CreateInstance(rectType!, 8f, 8f, 520f, 48f);
                    var beginArea = guilayoutType.GetMethod("BeginArea", new[] { rectType! });
                    var endArea = guilayoutType.GetMethod("EndArea", Type.EmptyTypes);
                    var label = guilayoutType.GetMethod("Label", new[] { typeof(string) });
                    beginArea?.Invoke(null, new[] { rect });
                    label?.Invoke(null, new object[] { $"VC Surveil: {_modsCount} mod(s) — {string.Join(", ", _modIds)}  |  ` for console" });
                    endArea?.Invoke(null, null);
                }
            }
            catch { }
            Console.OnGUI();
        }
    }
}
