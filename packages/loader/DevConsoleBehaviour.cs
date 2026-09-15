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
        }

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

                BepInExPlugin.Instance?.Console?.OnGUI();
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[DevConsole] OnGUI error: {ex}");
            }
        }
    }
}
