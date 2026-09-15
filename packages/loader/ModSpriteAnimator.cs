using System;
using System.IO;
using UnityEngine;
using Il2CppInterop.Runtime.Injection;

namespace PancakeModLoader
{
    public class ModSpriteAnimator : MonoBehaviour
    {
        public ModSpriteAnimator(IntPtr ptr) : base(ptr) { }

        private Sprite[] _frames;
        private int _fps = 5;
        private float _timer = 0f;
        private int _currentFrame = 0;
        private SpriteRenderer _renderer;

        public static void RegisterClass()
        {
            if (!ClassInjector.IsTypeRegisteredInIl2Cpp<ModSpriteAnimator>())
            {
                ClassInjector.RegisterTypeInIl2Cpp<ModSpriteAnimator>();
            }
        }

        public void Initialize(string imagePath, int frameCount, int fps)
        {
            _fps = fps > 0 ? fps : 5;
            _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_renderer == null) return;

            LoadSpriteSheet(imagePath, frameCount);
        }

        private void LoadSpriteSheet(string path, int frameCount)
        {
            if (!File.Exists(path)) return;

            byte[] data = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            // IL2CPP ImageConversion.LoadImage is usually an extension method or utility
            UnityEngine.ImageConversion.LoadImage(tex, data);
            tex.filterMode = FilterMode.Point; // Pixel art

            _frames = new Sprite[frameCount];
            int width = tex.width / frameCount;
            int height = tex.height;

            for (int i = 0; i < frameCount; i++)
            {
                Rect rect = new Rect(i * width, 0, width, height);
                _frames[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
            }

            if (_frames.Length > 0)
            {
                _renderer.sprite = _frames[0];
            }
        }

        private void Update()
        {
            if (_frames == null || _frames.Length <= 1 || _renderer == null) return;

            _timer += Time.deltaTime;
            float frameTime = 1f / _fps;
            
            if (_timer >= frameTime)
            {
                _timer -= frameTime;
                _currentFrame = (_currentFrame + 1) % _frames.Length;
                _renderer.sprite = _frames[_currentFrame];
            }
        }
    }
}
