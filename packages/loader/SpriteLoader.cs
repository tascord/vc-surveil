using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PancakeModLoader
{
    public static class SpriteLoader
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSpanWrapper
        {
            public IntPtr begin;
            public int length;
            public int _pad;
        }

        private delegate bool LoadImageNativeDelegate(IntPtr tex, IntPtr data, bool markNonReadable);
        private static LoadImageNativeDelegate _loadImageNative;
        private static bool _delegateResolved;

        private static void EnsureNativeDelegate()
        {
            if (_delegateResolved) return;
            try
            {
                _loadImageNative = Il2CppInterop.Runtime.IL2CPP.ResolveICall<LoadImageNativeDelegate>("UnityEngine.ImageConversion::LoadImage_Injected");
                _delegateResolved = true;
                LoaderLog.Info("[SpriteLoader] Successfully resolved 'UnityEngine.ImageConversion::LoadImage_Injected' icall.");
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[SpriteLoader] Failed to resolve LoadImage_Injected icall: {ex}");
                _delegateResolved = true;
            }
        }

        private static readonly Dictionary<string, Sprite> _spriteCache = new(StringComparer.OrdinalIgnoreCase);

        private static string WineToLinuxPath(string winePath)
        {
            if (string.IsNullOrEmpty(winePath)) return null;
            string p = winePath.Replace('\\', '/');
            if (p.StartsWith("S:/", StringComparison.OrdinalIgnoreCase))
            {
                return "/home/flora/.local/share/Steam/" + p.Substring(3);
            }
            if (p.StartsWith("C:/users/", StringComparison.OrdinalIgnoreCase))
            {
                return "/home/flora/.vc-mods/" + Path.GetFileName(winePath);
            }
            return null;
        }

        public static Sprite LoadSprite(string pathOrName, string modDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(pathOrName)) return null;

            if (_spriteCache.TryGetValue(pathOrName, out var cached) && cached != null)
            {
                return cached;
            }

            string cleanRel = pathOrName.Replace('\\', '/').TrimStart('/');
            string fileName = Path.GetFileName(cleanRel);

            var candidates = new List<string>
            {
                pathOrName,
                cleanRel
            };

            if (!string.IsNullOrEmpty(modDirectory))
            {
                candidates.Add(Path.Combine(modDirectory, cleanRel.Replace('/', Path.DirectorySeparatorChar)));
                candidates.Add(Path.Combine(modDirectory, cleanRel));
                candidates.Add(Path.Combine(modDirectory, "assets", fileName));
                candidates.Add(Path.Combine(modDirectory, fileName));

                string linuxPath = WineToLinuxPath(modDirectory);
                if (!string.IsNullOrEmpty(linuxPath))
                {
                    candidates.Add(Path.Combine(linuxPath, cleanRel));
                    candidates.Add(Path.Combine(linuxPath, "assets", fileName));
                    candidates.Add(Path.Combine(linuxPath, fileName));
                }
            }

            // Fixed known game & user mod directory locations
            candidates.Add(Path.Combine("/home/flora/.local/share/Steam/steamapps/common/Vampire Crawlers/Mods/demo-mod/assets", fileName));
            candidates.Add(Path.Combine("/home/flora/.local/share/Steam/steamapps/common/Vampire Crawlers/Mods/demo-mod", cleanRel));
            candidates.Add(Path.Combine("/home/flora/.vc-mods/demo-mod/assets", fileName));
            candidates.Add(Path.Combine("/home/flora/.vc-mods/demo-mod", cleanRel));

            string fullPath = null;
            foreach (var c in candidates)
            {
                try
                {
                    if (!string.IsNullOrEmpty(c) && File.Exists(c))
                    {
                        fullPath = c;
                        break;
                    }
                }
                catch {}
            }

            if (fullPath == null)
            {
                LoaderLog.Info($"[SpriteLoader] Sprite file not found: '{pathOrName}' (modDir: '{modDirectory}'). Checked {candidates.Count} candidate locations.");
                return null;
            }

            try
            {
                EnsureNativeDelegate();
                if (_loadImageNative == null)
                {
                    LoaderLog.Error("[SpriteLoader] LoadImage native delegate is null; aborting sprite load.");
                    return null;
                }

                byte[] rawBytes = File.ReadAllBytes(fullPath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.hideFlags = HideFlags.DontUnloadUnusedAsset | HideFlags.HideAndDontSave;

                IntPtr texPtr;
                try
                {
                    texPtr = UnityEngine.Object.MarshalledUnityObject.MarshalNotNull(tex);
                }
                catch
                {
                    texPtr = Il2CppInterop.Runtime.IL2CPP.Il2CppObjectBaseToPtrNotNull(tex);
                }

                bool loaded = false;
                unsafe
                {
                    fixed (byte* pBytes = rawBytes)
                    {
                        var span = new NativeSpanWrapper
                        {
                            begin = (IntPtr)pBytes,
                            length = rawBytes.Length,
                            _pad = 0
                        };
                        loaded = _loadImageNative(texPtr, (IntPtr)(&span), false);
                    }
                }

                if (!loaded)
                {
                    LoaderLog.Warn($"[SpriteLoader] LoadImage_Injected native call returned false for: {fullPath}");
                    return null;
                }

                var rect = new Rect(0f, 0f, (float)tex.width, (float)tex.height);
                var pivot = new Vector2(0.5f, 0.5f);
                var sprite = Sprite.Create(tex, rect, pivot, 100f);
                sprite.name = Path.GetFileNameWithoutExtension(fullPath);
                sprite.hideFlags = HideFlags.DontUnloadUnusedAsset | HideFlags.HideAndDontSave;

                _spriteCache[pathOrName] = sprite;
                _spriteCache[sprite.name] = sprite;
                _spriteCache[cleanRel] = sprite;
                _spriteCache[fileName] = sprite;

                LoaderLog.Info($"[SpriteLoader] Loaded sprite '{sprite.name}' ({tex.width}x{tex.height}) from '{fullPath}' ✓");
                return sprite;
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[SpriteLoader] Error loading sprite from {fullPath}: {ex}");
                return null;
            }
        }
    }
}
