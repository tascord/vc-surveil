using System;

namespace PancakeModLoader
{
    // Inverse run-mode toggle — thin compat layer over RunToggleBridge (M7).
    //
    // VS semantics note (see .opencode/docs/vs-inverse.md): Vampire Survivors
    // has NO in-run Inverse switch — Hyper/Hurry/Inverse/Endless are per-stage
    // checkboxes in Stage Selection, unlocked permanently via Gracia's Mirror.
    // This in-run toggle is a NEW mod capability for Crawlers, not a VS port:
    // mod-level in-memory state gated on relic ownership
    // (RelicDef.unlocksToggle == "inverse"), mirroring Gracia's Mirror gating.
    //
    // UI note: the toggle widget reuses the DevConsoleBehaviour overlay (no new
    // canvas). It must be a controller-navigable button with an accessible name
    // (e.g. "Inverse mode toggle") so gamepad/keyboard focus + screen readers
    // can operate it in-run.
    //
    // Dependencies: System + RunToggleBridge (same namespace) + LoaderLog only.
    // No UnityEngine / Nosebleed game refs, so this compiles without game refs.
    public static class InverseToggle
    {
        private const string ToggleId = "inverse";

        static InverseToggle()
        {
            try
            {
                EnsurePreset();
                RunToggleBridge.OnChanged += OnBridgeChanged;
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[InverseToggle] compat init failed: {ex.Message}");
            }
        }

        public static bool Enabled => IsEnabled();

        public static event Action<bool> OnToggled;

        public static bool IsUnlocked()
        {
            try
            {
                EnsurePreset();
                return RunToggleBridge.IsUnlocked(ToggleId);
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[InverseToggle] IsUnlocked check failed: {ex.Message}");
                return false;
            }
        }

        public static bool IsEnabled()
        {
            try
            {
                EnsurePreset();
                return RunToggleBridge.IsEnabled(ToggleId);
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[InverseToggle] IsEnabled check failed: {ex.Message}");
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                EnsurePreset();
                RunToggleBridge.SetEnabled(ToggleId, enabled);
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[InverseToggle] SetEnabled failed: {ex.Message}");
            }
        }

        public static void Toggle()
        {
            try
            {
                EnsurePreset();
                RunToggleBridge.Toggle(ToggleId);
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[InverseToggle] Toggle failed: {ex.Message}");
            }
        }

        public static string StatusString()
        {
            if (!IsUnlocked()) return "inverse: locked (requires relic with unlocksToggle=inverse)";
            return IsEnabled() ? "inverse: enabled (unlocked)" : "inverse: disabled (unlocked)";
        }

        private static void EnsurePreset()
        {
            try
            {
                if (RunToggleBridge.FindDef(ToggleId) == null)
                {
                    RunToggleBridge.Register(new CustomRunToggleDef
                    {
                        Id = ToggleId,
                        Name = "Inverse",
                        DefaultEnabled = false
                    });
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[InverseToggle] EnsurePreset failed: {ex.Message}");
            }
        }

        private static void OnBridgeChanged(string id, bool enabled)
        {
            if (!string.Equals(id, ToggleId, StringComparison.OrdinalIgnoreCase)) return;
            try { OnToggled?.Invoke(enabled); }
            catch (Exception ex) { LoaderLog.Warn($"[InverseToggle] OnToggled handler error: {ex.Message}"); }
        }
    }
}
