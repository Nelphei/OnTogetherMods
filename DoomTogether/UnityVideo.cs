using System;
using ManagedDoom;
using ManagedDoom.Video;
using UnityEngine;
using Renderer = ManagedDoom.Video.Renderer;

namespace DoomTogether
{
    // DOOM's picture, as a Unity texture. Managed Doom's renderer writes 32-bit colours column by
    // column (x * height + y), so the texture is the picture turned on its side: Renderer.Height
    // texels wide and Renderer.Width tall, with DOOM's x going up the texture. DoomWindow turns it
    // back when drawing. Ported from Managed Doom's SilkVideo.
    internal sealed class UnityVideo : IVideo, IDisposable
    {
        private readonly Renderer _renderer;
        private byte[] _buffer;

        public Texture2D Texture { get; private set; }

        public UnityVideo(Config config, GameContent content)
        {
            _renderer = new Renderer(config, content);
            _buffer = new byte[4 * _renderer.Width * _renderer.Height];
            Texture = new Texture2D(_renderer.Height, _renderer.Width, TextureFormat.RGBA32, false, false)
            {
                name = "DoomTogether Screen",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        // DOOM's own size, e.g. 640 x 400.
        public int Width => _renderer.Width;
        public int Height => _renderer.Height;

        public void Render(Doom doom, Fixed frameFrac)
        {
            _renderer.Render(doom, _buffer, frameFrac);
            Texture.SetPixelData(_buffer, 0);
            Texture.Apply(false, false);
        }

        public void InitializeWipe()
        {
            _renderer.InitializeWipe();
        }

        // Only decides whether DOOM wants the mouse grabbed, which this mod never does.
        public bool HasFocus()
        {
            return false;
        }

        public int MaxWindowSize => _renderer.MaxWindowSize;

        public int WindowSize
        {
            get => _renderer.WindowSize;
            set => _renderer.WindowSize = value;
        }

        public bool DisplayMessage
        {
            get => _renderer.DisplayMessage;
            set => _renderer.DisplayMessage = value;
        }

        public int MaxGammaCorrectionLevel => _renderer.MaxGammaCorrectionLevel;

        public int GammaCorrectionLevel
        {
            get => _renderer.GammaCorrectionLevel;
            set => _renderer.GammaCorrectionLevel = value;
        }

        public int WipeBandCount => _renderer.WipeBandCount;
        public int WipeHeight => _renderer.WipeHeight;

        public void Dispose()
        {
            _buffer = null;
            if (Texture != null)
            {
                UnityEngine.Object.Destroy(Texture);
                Texture = null;
            }
        }
    }
}
