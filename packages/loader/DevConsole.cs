using System;
using System.Collections.Generic;
using System.IO;

namespace PancakeModLoader
{
    public class DevConsole
    {
        private readonly List<string> _history = new();
        private readonly List<string> _log = new();
        private readonly Dictionary<string, ConsoleCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
        private string _input = "";
        private bool _open;
        private int _historyIndex = -1;

        public bool IsOpen => _open;
        public IReadOnlyList<string> Lines => _log;

        public DevConsole()
        {
            Register(new ConsoleCommand("help", "List commands", "help", (args, c) =>
            {
                c.WriteLine("Commands:");
                foreach (var kv in _commands) c.WriteLine($"  {kv.Key} - {kv.Value.Description} {(kv.Value.Usage ?? "")}");
                c.WriteLine("  Press Ctrl+Tab to toggle, Enter to run, Esc to close, Up/Down for history");
            }));
            Register(new ConsoleCommand("clear", "Clear console", "clear", (args, c) => c.Clear()));
            Register(new ConsoleCommand("mods", "List loaded mods", "mods", (args, c) =>
            {
                c.WriteLine($"Mods: {string.Join(", ", ModRegistry.LoadedIds)}");
            }));
            Register(new ConsoleCommand("give", "Add card to hand — give <cardId> [count]", "give <cardId> [count]", (args, c) =>
            {
                if (args.Length == 0) { c.WriteLine("usage: give <cardId> [count] — e.g. give demo-mod:test_strike 3"); return; }
                var id = args[0];
                var n = 1;
                if (args.Length > 1) int.TryParse(args[1], out n);

                var card = CardBridge.FindCard(id);
                if (card == null)
                {
                    c.WriteLine($"card '{id}' not found. Type 'cards' to see available cards.");
                    return;
                }

                var player = UnityEngine.Object.FindObjectOfType<Nosebleed.Pancake.Models.PlayerModel>();
                if (player == null)
                {
                    c.WriteLine("PlayerModel not active (are you in a run/encounter?)");
                    return;
                }

                for (int i = 0; i < n; i++)
                {
                    player.AddCardConfigToHand(card, false);
                }
                c.WriteLine($"Added {n}x {card.Name} to hand!");
            }));
            Register(new ConsoleCommand("spawn", "Spawn enemy — spawn <enemyId> [row] [col]", "spawn <enemyId> [row] [col]", (args, c) => {
                if (args.Length == 0) {
                    c.WriteLine("Usage: spawn <enemyId> [row] [col]");
                    return;
                }
                string enemyId = args[0];
                int row = args.Length > 1 && int.TryParse(args[1], out int r) ? r : 0;
                int col = args.Length > 2 && int.TryParse(args[2], out int colVal) ? colVal : 0;
                EnemyBridge.SpawnEnemy(enemyId, row, col);
                c.WriteLine($"[spawn] Spawning {enemyId} at {row}, {col}");
            }));
            Register(new ConsoleCommand("heal", "Heal player — heal [amount]", "heal [amount]", (args, c) =>
            {
                var amt = 20;
                if (args.Length > 0) int.TryParse(args[0], out amt);
                var player = UnityEngine.Object.FindObjectOfType<Nosebleed.Pancake.Models.PlayerModel>();
                if (player != null) { player.AttachHealthRestoredListener(); c.WriteLine($"Healed player {amt} HP!"); }
                else c.WriteLine("Player not in run");
            }));
            Register(new ConsoleCommand("cards", "List registered mod cards", "cards", (args, c) =>
            {
                c.WriteLine($"Registered mod cards ({CardBridge.RegisteredDefs.Count}):");
                foreach (var d in CardBridge.RegisteredDefs)
                {
                    c.WriteLine($"  {d.Id} - '{d.Name}' ({d.Mana} mana, {d.Color}, {d.Rarity}, {d.Weight} weight)");
                }
            }));
            Register(new ConsoleCommand("toggle", "Generic run toggle — toggle <id> [on|off|status|toggle]", "toggle <id> [on|off|status|toggle]", (args, c) =>
            {
                if (args.Length == 0)
                {
                    var ids = RunToggleBridge.ListIds();
                    c.WriteLine(ids.Length == 0 ? "toggles: (none registered)" : $"toggles: {string.Join(", ", ids)}");
                    c.WriteLine("usage: toggle <id> [on|off|status|toggle]");
                    return;
                }
                var id = args[0];
                var sub = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
                if (RunToggleBridge.FindDef(id) == null)
                {
                    c.WriteLine($"unknown toggle: {id} — known: {string.Join(", ", RunToggleBridge.ListIds())}");
                    return;
                }
                switch (sub)
                {
                    case "on":
                        RunToggleBridge.SetEnabled(id, true);
                        break;
                    case "off":
                        RunToggleBridge.SetEnabled(id, false);
                        break;
                    case "toggle":
                        RunToggleBridge.Toggle(id);
                        break;
                    case "status":
                        break;
                    default:
                        c.WriteLine("usage: toggle <id> [on|off|status|toggle]");
                        break;
                }
                var unlocked = RunToggleBridge.IsUnlocked(id);
                var enabled = RunToggleBridge.IsEnabled(id);
                c.WriteLine(unlocked ? $"{id}: {(enabled ? "enabled" : "disabled")} (unlocked)" : $"{id}: locked (requires relic)");
            }));
            Register(new ConsoleCommand("inverse", "Inverse run-mode toggle — inverse [on|off|status|toggle] (alias for toggle inverse)", "inverse [on|off|status|toggle]", (args, c) =>
            {
                var sub = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
                var id = "inverse";
                switch (sub)
                {
                    case "on":
                        RunToggleBridge.SetEnabled(id, true);
                        break;
                    case "off":
                        RunToggleBridge.SetEnabled(id, false);
                        break;
                    case "toggle":
                        RunToggleBridge.Toggle(id);
                        break;
                    case "status":
                        break;
                    default:
                        c.WriteLine("usage: inverse [on|off|status|toggle]");
                        break;
                }
                var unlocked = RunToggleBridge.IsUnlocked(id);
                var enabled = RunToggleBridge.IsEnabled(id);
                c.WriteLine(unlocked ? $"{id}: {(enabled ? "enabled" : "disabled")} (unlocked)" : $"{id}: locked (requires relic with unlocksToggle=inverse)");
            }));
            /*
            Register(new ConsoleCommand("arcanas", "List registered mod arcanas", "arcanas", (args, c) =>
            {
                c.WriteLine($"Registered mod arcanas ({ArcanaBridge.RegisteredDefs.Count}):");
                foreach (var d in ArcanaBridge.RegisteredDefs)
                {
                    c.WriteLine($"  {d.Id} - '{d.Name}'");
                }
            }));
            Register(new ConsoleCommand("give_arcana", "Give arcana to player — give_arcana <id>", "give_arcana <id>", (args, c) =>
            {
                if (args.Length == 0) { c.WriteLine("usage: give_arcana <id>"); return; }
                var id = args[0];
                var arcana = ArcanaBridge.FindArcana(id);
                if (arcana == null)
                {
                    c.WriteLine($"arcana '{id}' not found. Type 'arcanas' to see available arcanas.");
                    return;
                }

                var player = UnityEngine.Object.FindObjectOfType<Nosebleed.Pancake.Models.PlayerModel>();
                if (player == null)
                {
                    c.WriteLine("PlayerModel not active (are you in a run/encounter?)");
                    return;
                }

                try
                {
                    var m = HarmonyLib.AccessTools.Method(typeof(Nosebleed.Pancake.Models.PlayerModel), "AddArcana") 
                         ?? HarmonyLib.AccessTools.Method(typeof(Nosebleed.Pancake.Models.PlayerModel), "AddArcanaConfig");
                    if (m != null)
                    {
                        m.Invoke(player, new object[] { arcana, false });
                        c.WriteLine($"Added arcana {arcana.name}!");
                    }
                    else
                    {
                        c.WriteLine("Method AddArcana not found on PlayerModel.");
                    }
                }
                catch (Exception ex)
                {
                    c.WriteLine($"Failed to give arcana: {ex.Message}");
                }
            }));
            */
        }

        public void Register(ConsoleCommand cmd) => _commands[cmd.Name] = cmd;
        public void Register(string name, string desc, Action<string[], DevConsole> handler) => Register(new ConsoleCommand(name, desc, name, handler));
        public void Clear() => _log.Clear();
        public void Open()
        {
            if (!_open)
            {
                _open = true;
                SetGameInputEnabled(false);
            }
        }
        public void Close()
        {
            if (_open)
            {
                _open = false;
                SetGameInputEnabled(true);
            }
        }
        public void Toggle()
        {
            if (_open) Close();
            else Open();
        }

        private static void SetGameInputEnabled(bool enabled)
        {
            try
            {
                var im = UnityEngine.Object.FindObjectOfType<Nosebleed.Pancake.InputSystem.InputManager>();
                if (im != null)
                {
                    if (enabled)
                    {
                        im.InputAsset?.Enable();
                        im.InputActionMap?.Enable();
                        try { im.DisableInputAsset(false); } catch {}
                    }
                    else
                    {
                        im.InputAsset?.Disable();
                        im.InputActionMap?.Disable();
                        try { im.DisableInputAsset(true); } catch {}
                    }
                    LoaderLog.Info($"[DevConsole] Game input asset enabled = {enabled}");
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[DevConsole] Failed to set game input: {ex.Message}");
            }
        }

        public void WriteLine(string line)
        {
            _log.Add(line);
            LoaderLog.Info($"[console] {line}");
            if (_log.Count > 400) _log.RemoveRange(0, _log.Count - 400);
        }

        public bool Execute(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            _history.Add(line);
            _historyIndex = _history.Count;
            WriteLine($"> {line}");
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var name = parts[0].TrimStart('/', '!', '`');
            var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();
            if (_commands.TryGetValue(name, out var cmd))
            {
                try { cmd.Handler(args, this); } catch (Exception ex) { WriteLine($"error: {ex.Message}"); }
                return true;
            }
            WriteLine($"unknown command: {name} — try 'help'");
            return false;
        }

        public void Update()
        {
            DevConsoleBehaviour.CheckToggleInput();
        }

        public void OnGUI()
        {
            if (!_open) return;
            try
            {
                UnityEngine.GUI.depth = -10000;

                // Main panel coordinates (centered horizontally)
                var width = System.Math.Min(800f, UnityEngine.Screen.width - 32f);
                var height = System.Math.Min(480f, UnityEngine.Screen.height - 80f);
                var panelX = (UnityEngine.Screen.width - width) / 2f;
                var panelY = 40f;
                var panelRect = new UnityEngine.Rect(panelX, panelY, width, height);

                // Handle keyboard & mouse input for console (consuming all keys while open)
                var ev = UnityEngine.Event.current;
                if (ev != null)
                {
                    if (ev.isKey && (ev.type == UnityEngine.EventType.KeyDown || ev.rawType == UnityEngine.EventType.KeyDown))
                    {
                        if (ev.keyCode == UnityEngine.KeyCode.Return || ev.keyCode == UnityEngine.KeyCode.KeypadEnter)
                        {
                            if (!string.IsNullOrWhiteSpace(_input))
                            {
                                var cmd = _input;
                                _input = "";
                                Execute(cmd);
                            }
                        }
                        else if (ev.keyCode == UnityEngine.KeyCode.Backspace)
                        {
                            if (!string.IsNullOrEmpty(_input))
                            {
                                _input = _input.Substring(0, _input.Length - 1);
                            }
                        }
                        else if (ev.keyCode == UnityEngine.KeyCode.Escape)
                        {
                            Close();
                        }
                        else if (ev.keyCode == UnityEngine.KeyCode.UpArrow)
                        {
                            if (_history.Count > 0 && _historyIndex > 0)
                            {
                                _historyIndex--;
                                _input = _history[_historyIndex];
                            }
                        }
                        else if (ev.keyCode == UnityEngine.KeyCode.DownArrow)
                        {
                            if (_historyIndex < _history.Count - 1)
                            {
                                _historyIndex++;
                                _input = _history[_historyIndex];
                            }
                            else
                            {
                                _historyIndex = _history.Count;
                                _input = "";
                            }
                        }
                        else if (ev.character >= 32 && ev.character <= 126)
                        {
                            _input += ev.character;
                        }

                        // Consume ALL key presses while open so background never receives them
                        ev.Use();
                    }
                    else if (ev.isKey)
                    {
                        // Consume other key events (KeyUp, etc.)
                        ev.Use();
                    }
                }

                // Background & Top header bar using native GUI.Box (guaranteed unstripped)
                UnityEngine.GUI.Box(panelRect, "");
                UnityEngine.GUI.Box(new UnityEngine.Rect(panelRect.x, panelRect.y, panelRect.width, 28f), "");

                // Title & Close Button
                var modsCount = BepInExPlugin.Instance?.LoadedModsCount ?? 0;
                UnityEngine.GUI.Label(new UnityEngine.Rect(panelRect.x + 10f, panelRect.y + 4f, 450f, 22f), $"VC Surveil Dev Console — {modsCount} mod(s) active [Ctrl+Tab to toggle]");
                if (UnityEngine.GUI.Button(new UnityEngine.Rect(panelRect.x + panelRect.width - 92f, panelRect.y + 3f, 86f, 22f), "Close [Esc]"))
                {
                    Close();
                }

                // Log output lines (up to 16 lines)
                float lineY = panelRect.y + 34f;
                int start = System.Math.Max(0, _log.Count - 16);
                for (int i = start; i < _log.Count; i++)
                {
                    UnityEngine.GUI.Label(new UnityEngine.Rect(panelRect.x + 12f, lineY, panelRect.width - 24f, 20f), _log[i]);
                    lineY += 20f;
                }

                // Input bar with background box
                float inputY = panelRect.y + panelRect.height - 66f;
                UnityEngine.GUI.Box(new UnityEngine.Rect(panelRect.x + 8f, inputY - 2f, panelRect.width - 96f, 26f), "");
                UnityEngine.GUI.Label(new UnityEngine.Rect(panelRect.x + 14f, inputY, panelRect.width - 110f, 22f), $"> {_input}_");
                if (UnityEngine.GUI.Button(new UnityEngine.Rect(panelRect.x + panelRect.width - 84f, inputY - 2f, 76f, 26f), "Run [Enter]"))
                {
                    var cmd = _input;
                    _input = "";
                    Execute(cmd);
                }

                // Quick command buttons
                float quickY = panelRect.y + panelRect.height - 34f;
                if (UnityEngine.GUI.Button(new UnityEngine.Rect(panelRect.x + 10f, quickY, 130f, 24f), "give test_strike 1"))
                {
                    Execute("give demo-mod:test_strike 1");
                }
                if (UnityEngine.GUI.Button(new UnityEngine.Rect(panelRect.x + 146f, quickY, 130f, 24f), "give garlic_aura 1"))
                {
                    Execute("give demo-mod:garlic_aura 1");
                }
                if (UnityEngine.GUI.Button(new UnityEngine.Rect(panelRect.x + 282f, quickY, 60f, 24f), "cards"))
                {
                    Execute("cards");
                }
                if (UnityEngine.GUI.Button(new UnityEngine.Rect(panelRect.x + 348f, quickY, 60f, 24f), "help"))
                {
                    Execute("help");
                }
                if (UnityEngine.GUI.Button(new UnityEngine.Rect(panelRect.x + 414f, quickY, 65f, 24f), "heal 50"))
                {
                    Execute("heal 50");
                }
                if (UnityEngine.GUI.Button(new UnityEngine.Rect(panelRect.x + 485f, quickY, 60f, 24f), "clear"))
                {
                    Clear();
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[DevConsole] OnGUI error: {ex}");
            }
        }
    }

    public record ConsoleCommand(string Name, string Description, string Usage, Action<string[], DevConsole> Handler);
}
