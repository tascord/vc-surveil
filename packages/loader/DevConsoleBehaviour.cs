using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PancakeModLoader
{
    public class DevConsoleBehaviour : MonoBehaviour
    {
        public static GameObject HostObject { get; private set; }

        public DevConsoleBehaviour(IntPtr ptr) : base(ptr) { }

        public static void EnsureHostExists()
        {
            try
            {
                if (HostObject == null)
                {
                    HostObject = new GameObject("VCSurveilDevConsoleHost");
                    HostObject.hideFlags = HideFlags.HideAndDontSave;
                    UnityEngine.Object.DontDestroyOnLoad(HostObject);
                    HostObject.AddComponent<DevConsoleBehaviour>();
                    LoaderLog.Info("[DevConsole] VCSurveilDevConsoleHost created with DevConsoleBehaviour ✓");
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[DevConsole] Failed to ensure host: {ex}");
            }
        }

        public void Awake()
        {
            LoaderLog.Info("[DevConsole] DevConsoleBehaviour Awake ✓");
        }

        public void Start()
        {
            LoaderLog.Info("[DevConsole] DevConsoleBehaviour Start ✓");
        }

        public void Update()
        {
            CheckToggleInput();
            CheckInverseToggleInput();
        }

        // ── Inverse toggle widget (M7-T2): controller-navigable button with accessible name,
        // reuses DevConsole overlay patterns (GUI.depth, Box/Label/Button with Rect, Screen metrics).
        // Accessible name/label for keyboard focus + screen readers (IMGUI focusable via Tab).
        public const string InverseControlName = "Inverse mode toggle";
        // Bindable gamepad confirm button (legacy KeyCode; remap by assigning this field).
        // New Input System Gamepad.buttonSouth is always also checked (standard confirm).
        public static KeyCode InverseGamepadConfirmKey = KeyCode.Joystick1Button0;
        public static bool EnableInverseGamepadShortcut = true;

        private static bool _loggedKeyboard = false;

        public static void CheckToggleInput()
        {
            try
            {
                if (!_loggedKeyboard)
                {
                    _loggedKeyboard = true;
                    var kbName = Keyboard.current != null ? Keyboard.current.name : "null";
                    LoaderLog.Info($"[DevConsole] Keyboard device state: Keyboard.current = '{kbName}'");
                }

                bool togglePressed = false;
                bool escPressed = false;

                // 1. New Input System (Unity.InputSystem)
                try
                {
                    var kb = Keyboard.current;
                    if (kb != null)
                    {
                        bool ctrl = (kb.leftCtrlKey != null && kb.leftCtrlKey.isPressed) ||
                                    (kb.rightCtrlKey != null && kb.rightCtrlKey.isPressed);
                        if (ctrl && kb.tabKey != null && kb.tabKey.wasPressedThisFrame)
                        {
                            togglePressed = true;
                        }
                        else if (kb.backquoteKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame)
                        {
                            togglePressed = true;
                        }

                        if (kb.escapeKey != null && kb.escapeKey.wasPressedThisFrame)
                        {
                            escPressed = true;
                        }
                    }
                }
                catch { }

                // 2. Fallback to Legacy Input System
                if (!togglePressed)
                {
                    try
                    {
                        bool ctrlLegacy = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                        if (ctrlLegacy && Input.GetKeyDown(KeyCode.Tab))
                        {
                            togglePressed = true;
                        }
                        else if (Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.F1))
                        {
                            togglePressed = true;
                        }

                        if (Input.GetKeyDown(KeyCode.Escape))
                        {
                            escPressed = true;
                        }
                    }
                    catch { }
                }

                if (togglePressed)
                {
                    BepInExPlugin.Instance?.Console?.Toggle();
                    LoaderLog.Info($"[DevConsole] Toggled console via keyboard (Ctrl+Tab). IsOpen = {BepInExPlugin.Instance?.Console?.IsOpen}");
                }
                else if (escPressed && BepInExPlugin.Instance?.Console?.IsOpen == true)
                {
                    BepInExPlugin.Instance?.Console?.Close();
                    LoaderLog.Info("[DevConsole] Console closed via Escape");
                }
            }
            catch { }
        }

        public static bool WasInverseConfirmPressed()
        {
            // New Input System gamepad confirm (standard controller-confirm, bindable via Input System bindings).
            try
            {
                var pad = Gamepad.current;
                if (pad != null && pad.buttonSouth != null && pad.buttonSouth.wasPressedThisFrame)
                    return true;
            }
            catch { }
            // Legacy fallback, bindable via InverseGamepadConfirmKey.
            try
            {
                if (Input.GetKeyDown(InverseGamepadConfirmKey) || Input.GetKeyDown(KeyCode.JoystickButton0))
                    return true;
            }
            catch { }
            return false;
        }

        public static void CheckInverseToggleInput()
        {
            if (!EnableInverseGamepadShortcut) return;
            try
            {
                if (BepInExPlugin.Instance?.Console?.IsOpen != true) return;
                if (!InverseToggle.IsUnlocked()) return;
                if (WasInverseConfirmPressed())
                {
                    InverseToggle.Toggle();
                    LoaderLog.Info($"[DevConsole] Inverse toggled via gamepad ({InverseToggle.StatusString()})");
                }
            }
            catch { }
        }

        private static void DrawInverseToggleWidget()
        {
            try
            {
                // Reuse DevConsole overlay patterns: centered bar below console panel, Box/Label/Button with Rect.
                var width = System.Math.Min(800f, Screen.width - 32f);
                var height = System.Math.Min(480f, Screen.height - 80f);
                var panelX = (Screen.width - width) / 2f;
                var panelY = 40f;
                float barY = panelY + height + 6f;
                if (barY + 30f > Screen.height - 4f) barY = System.Math.Max(4f, Screen.height - 34f);
                var barRect = new Rect(panelX, barY, width, 30f);

                GUI.Box(barRect, "");
                // Accessible visible label (screen-reader / keyboard context).
                GUI.Label(new Rect(barRect.x + 10f, barRect.y + 5f, 160f, 20f), InverseControlName);

                bool unlocked = InverseToggle.IsUnlocked();
                string status = InverseToggle.StatusString();

                // Keyboard-focusable: SetNextControlName gives Tab focus + accessible name.
                // Standard IMGUI focus means Enter/Space (keyboard) or controller-confirm (submit)
                // activates the focused button without extra code.
                GUI.SetNextControlName(InverseControlName);
                bool prevEnabled = GUI.enabled;
                // Only enabled/clickable when unlocked (grays out + ignores clicks while locked).
                GUI.enabled = unlocked;
                var content = new GUIContent(status, InverseControlName);
                if (GUI.Button(new Rect(barRect.x + 180f, barRect.y + 3f, barRect.width - 190f, 24f), content))
                {
                    if (unlocked)
                    {
                        InverseToggle.Toggle();
                        LoaderLog.Info($"[DevConsole] Inverse toggled via overlay button ({InverseToggle.StatusString()})");
                    }
                }
                GUI.enabled = prevEnabled;
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[DevConsole] Inverse widget error: {ex.Message}");
            }
        }

        public void OnGUI()
        {
            try
            {
                GUI.depth = -10000;

                // 1. Raw IMGUI keyboard event detection (works across ALL Unity input modes & OS backends)
                var currentEvent = Event.current;
                if (currentEvent != null && currentEvent.isKey && (currentEvent.type == EventType.KeyDown || currentEvent.rawType == EventType.KeyDown))
                {
                    if (currentEvent.control && currentEvent.keyCode == KeyCode.Tab)
                    {
                        BepInExPlugin.Instance?.Console?.Toggle();
                        LoaderLog.Info($"[DevConsole] Toggled console via Ctrl+Tab. IsOpen = {BepInExPlugin.Instance?.Console?.IsOpen}");
                        currentEvent.Use();
                        return;
                    }
                    else if (currentEvent.keyCode == KeyCode.BackQuote || currentEvent.keyCode == KeyCode.F1 || currentEvent.character == '`' || currentEvent.character == '~')
                    {
                        BepInExPlugin.Instance?.Console?.Toggle();
                        LoaderLog.Info($"[DevConsole] Toggled console via key '{currentEvent.keyCode}'. IsOpen = {BepInExPlugin.Instance?.Console?.IsOpen}");
                        currentEvent.Use();
                        return;
                    }
                    else if (currentEvent.keyCode == KeyCode.Escape && BepInExPlugin.Instance?.Console?.IsOpen == true)
                    {
                        BepInExPlugin.Instance?.Console?.Close();
                        currentEvent.Use();
                        return;
                    }
                }

                if (BepInExPlugin.Instance?.Console?.IsOpen != true)
                {
                    return;
                }

                // Inverse toggle widget drawn BEFORE console so IMGUI keyboard focus
                // (Tab/Enter/Space) reaches the button before DevConsole consumes all keys.
                // Rect is below the console panel (no visual overlap), reusing overlay patterns.
                DrawInverseToggleWidget();

                BepInExPlugin.Instance?.Console?.OnGUI();
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[DevConsole] OnGUI error: {ex}");
            }
        }
    }
}
