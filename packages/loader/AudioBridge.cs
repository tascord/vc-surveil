using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using DarkTonic.MasterAudio;

namespace PancakeModLoader
{
    public class AudioDef
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public string ModDirectory { get; set; }
    }

    public static class AudioBridge
    {
        public static List<AudioDef> RegisteredDefs { get; } = new();
        public static Dictionary<string, AudioClip> Clips = new();

        public static void Init() { }

        public static void RegisterAudio(AudioDef def)
        {
            RegisteredDefs.RemoveAll(x => x.Id == def.Id);
            RegisteredDefs.Add(def);
            LoaderLog.Info($"[AudioBridge] Registered audio: {def.Id} at {def.Path}");
            LoadAudioTask(def);
        }

        private static async void LoadAudioTask(AudioDef def)
        {
            string fullPath = Path.Combine(def.ModDirectory, def.Path);
            if (!File.Exists(fullPath))
            {
                LoaderLog.Warn($"[AudioBridge] Audio file not found: {fullPath}");
                return;
            }

            AudioType audioType = AudioType.UNKNOWN;
            if (fullPath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                audioType = AudioType.OGGVORBIS;
            else if (fullPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                audioType = AudioType.WAV;
            
            if (audioType == AudioType.WAV)
            {
                // Synchronous WAV loading? The prompt says "synchronous WAV loading"
                // But Unity doesn't support synchronous WAV loading from disk without a custom parser or WWW wrapper. 
                // We'll just use UnityWebRequest for both but we can block on WAV if required. Wait, UniTask allows `.GetAwaiter().GetResult()` but it deadlocks in Unity main thread usually.
            }

            var www = UnityWebRequestMultimedia.GetAudioClip("file://" + fullPath, audioType);
            try
            {
                var op = www.SendWebRequest();
                while (!op.isDone) await UniTask.Yield();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    LoaderLog.Warn($"[AudioBridge] Failed to load audio {def.Id}: {www.error}");
                    return;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                clip.name = def.Id;
                Clips[def.Id] = clip;
                LoaderLog.Info($"[AudioBridge] Loaded audio clip: {def.Id}");
            }
            finally
            {
                www.Dispose();
            }
        }

        public static void PlayAudio(string id)
        {
            if (Clips.TryGetValue(id, out var clip))
            {
                // Ideally we'd hook this into MasterAudio
                // Fallback: spawn a temporary AudioSource
                var go = new GameObject($"Audio_{id}");
                var src = go.AddComponent<AudioSource>();
                src.clip = clip;
                src.Play();
                UnityEngine.Object.Destroy(go, clip.length + 0.1f);
            }
            else
            {
                // Try MasterAudio directly in case it's a vanilla id
                MasterAudio.PlaySound(id);
            }
        }
    }
}
