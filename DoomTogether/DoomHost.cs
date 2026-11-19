using System;
using System.IO;
using ManagedDoom;
using ManagedDoom.UserInput;
using UnityEngine;
using EventType = ManagedDoom.EventType;

namespace DoomTogether
{
    // One running game of DOOM: Managed Doom's engine with this mod's video, sound, music and
    // keyboard. DOOM's world runs 35 tics a second; with FpsScale 2 the picture is drawn twice a
    // tic, half a tic apart (Managed Doom's frame interpolation). See Managed Doom's SilkDoom.
    internal sealed class DoomHost : IDisposable
    {
        private const string ConfigFile = "managed-doom.cfg";
        // At most this many steps to catch up in one Unity frame, so a long hitch doesn't make
        // DOOM run a burst of tics.
        private const int MaxStepsPerFrame = 8;

        private readonly Config _config;
        private readonly string _configPath;
        private GameContent _content;
        private UnityVideo _video;
        private UnitySound _sound;
        private UnityMusic _music;
        private readonly UnityUserInput _input;
        private Doom _doom;

        private readonly int _fpsScale;
        private int _frameCount = -1;
        private double _time;
        private bool _frozen;

        public Texture2D Texture => _video?.Texture;
        // DOOM's own picture size, e.g. 640 x 400 (shown at 4:3 like on a CRT).
        public int Width => _video != null ? _video.Width : 320;
        public int Height => _video != null ? _video.Height : 200;
        public bool Finished { get; private set; }
        public string QuitMessage => _doom?.QuitMessage;

        // wadPath: the IWAD to play. host: the object the sound sources go on.
        public DoomHost(string wadPath, GameObject host, bool music, Func<float> master)
        {
            try
            {
                string dataFolder = Settings.DataFolder();
                Directory.CreateDirectory(dataFolder);
                _configPath = Path.Combine(dataFolder, ConfigFile);
                _config = File.Exists(_configPath) ? new Config(_configPath) : new Config();
                // Shown in a window, never full screen, and always at DOOM's sharper resolution.
                _config.video_fullscreen = false;
                _config.video_fpsscale = Mathf.Clamp(_config.video_fpsscale, 1, 4);

                // Saved games are kept per WAD, since a save only loads in the game it was made in.
                string saves = Path.Combine(dataFolder, "saves", Path.GetFileNameWithoutExtension(wadPath).ToLowerInvariant());
                Directory.CreateDirectory(saves);
                ConfigUtilities.DataDirectory = saves;

                var args = new CommandLineArgs(new[] { "-iwad", wadPath });
                // Undoes the last game's DEHACKED patch before this WAD's is applied.
                DoomInfoReset.BeforeNewGame();
                _content = new GameContent(args);
                _video = new UnityVideo(_config, _content);
                try
                {
                    _sound = new UnitySound(_config, _content, host, master);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"DOOM's sound effects couldn't start, so it plays without them: {e}");
                    _sound = null;
                }
                if (music)
                {
                    try
                    {
                        _music = new UnityMusic(_config, _content, host, master);
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning($"DOOM's music couldn't start, so it plays without it: {e}");
                        _music = null;
                    }
                }
                _input = new UnityUserInput(_config);
                _doom = new Doom(args, _config, _content, _video, _sound, _music, _input);
                _fpsScale = _config.video_fpsscale;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        // Runs DOOM for this frame's time and draws a new picture when anything moved.
        public void Tick(float deltaTime)
        {
            if (_doom == null || Finished || _frozen)
            {
                return;
            }

            double step = 1.0 / (35 * _fpsScale);
            _time += deltaTime;
            int steps = 0;
            while (_time >= step && steps < MaxStepsPerFrame)
            {
                _time -= step;
                steps++;
                _frameCount++;
                if (_frameCount % _fpsScale == 0 && _doom.Update() == UpdateResult.Completed)
                {
                    Finished = true;   // Quit from DOOM's menu
                    return;
                }
            }
            if (steps == MaxStepsPerFrame)
            {
                _time = 0;
            }
            if (steps > 0)
            {
                Fixed frameFrac = Fixed.FromInt(_frameCount % _fpsScale + 1) / _fpsScale;
                _video.Render(_doom, frameFrac);
            }
        }

        // Draws the first picture before anything has run, so the window isn't blank.
        public void RenderNow()
        {
            if (_doom != null && _video != null)
            {
                _video.Render(_doom, Fixed.One);
            }
        }

        public void Key(DoomKey key, bool down)
        {
            if (_doom == null)
            {
                return;
            }
            _input.SetPressed(key, down);
            _doom.PostEvent(new DoomEvent(down ? EventType.KeyDown : EventType.KeyUp, key));
        }

        // Stops (or restarts) DOOM where it is, sound included. Keys held when it stops are let go.
        public void Freeze(bool frozen)
        {
            if (frozen == _frozen)
            {
                return;
            }
            _frozen = frozen;
            _time = 0;
            if (frozen)
            {
                ReleaseKeys();
            }
            _sound?.Freeze(frozen);
            _music?.Freeze(frozen);
        }

        public void ReleaseKeys()
        {
            if (_doom == null)
            {
                return;
            }
            foreach (DoomKey key in _input.Held())
            {
                _doom.PostEvent(new DoomEvent(EventType.KeyUp, key));
            }
            _input.ReleaseAll();
        }

        public void Dispose()
        {
            _doom = null;
            if (_config != null && _configPath != null)
            {
                _config.Save(_configPath);
            }
            _music?.Dispose();
            _music = null;
            _sound?.Dispose();
            _sound = null;
            _video?.Dispose();
            _video = null;
            _content?.Dispose();
            _content = null;
        }
    }
}
