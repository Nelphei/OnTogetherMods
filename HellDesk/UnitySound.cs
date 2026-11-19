using System;
using ManagedDoom;
using ManagedDoom.Audio;
using UnityEngine;

namespace HellDesk
{
    // DOOM's sound effects through Unity AudioSources: 8 channels for sounds in the world, panned
    // left and right by where they come from, and one for menu sounds. Ported from Managed Doom's
    // SilkSound (GPLv2), with OpenAL's 3D position turned into a stereo pan.
    internal sealed class UnitySound : ISound, IDisposable
    {
        private const int ChannelCount = 8;

        private static readonly float FastDecay = (float)Math.Pow(0.5, 1.0 / (35 / 5));
        private static readonly float SlowDecay = (float)Math.Pow(0.5, 1.0 / 35);

        private const float ClipDist = 1200;
        private const float CloseDist = 160;
        private const float Attenuator = ClipDist - CloseDist;

        // How far a sound fully to one side pans (1 = only one ear).
        private const float PanWidth = 0.8f;

        private readonly Config _config;
        private readonly Func<float> _master;

        private AudioClip[] _clips;
        private readonly float[] _amplitudes;
        private readonly DoomRandom _random;

        private AudioSource[] _channels;
        private readonly bool[] _paused;
        private readonly ChannelInfo[] _infos;

        private AudioSource _uiChannel;
        private Sfx _uiReserved;

        private Mobj _listener;
        private float _masterVolumeDecay;
        private float _lastUpdate = -1f;

        // master: the mod's own volume (0-1), read every update.
        public UnitySound(Config config, GameContent content, GameObject host, Func<float> master)
        {
            _config = config;
            _master = master;
            config.audio_soundvolume = Mathf.Clamp(config.audio_soundvolume, 0, MaxVolume);

            _clips = new AudioClip[DoomInfo.SfxNames.Length];
            _amplitudes = new float[DoomInfo.SfxNames.Length];
            if (config.audio_randompitch)
            {
                _random = new DoomRandom();
            }

            for (int i = 0; i < DoomInfo.SfxNames.Length; i++)
            {
                string name = "DS" + DoomInfo.SfxNames[i].ToString().ToUpper();
                if (content.Wad.GetLumpNumber(name) == -1)
                {
                    continue;
                }
                float[] samples = GetSamples(content.Wad, name, out int sampleRate);
                if (samples != null && samples.Length > 0)
                {
                    AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
                    clip.SetData(samples, 0);
                    clip.hideFlags = HideFlags.HideAndDontSave;
                    _clips[i] = clip;
                    _amplitudes[i] = GetAmplitude(samples, sampleRate);
                }
            }

            _channels = new AudioSource[ChannelCount];
            _paused = new bool[ChannelCount];
            _infos = new ChannelInfo[ChannelCount];
            for (int i = 0; i < ChannelCount; i++)
            {
                _channels[i] = NewSource(host);
                _infos[i] = new ChannelInfo();
            }
            _uiChannel = NewSource(host);
            _uiReserved = Sfx.NONE;

            _masterVolumeDecay = (float)config.audio_soundvolume / MaxVolume;
        }

        private static AudioSource NewSource(GameObject host)
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;   // plain stereo, not placed in the game's world
            source.dopplerLevel = 0f;
            return source;
        }

        // DOOM sounds are 8-bit unsigned samples after an 8-byte header.
        private static float[] GetSamples(Wad wad, string name, out int sampleRate)
        {
            byte[] data = wad.ReadLump(name);
            sampleRate = -1;
            if (data.Length < 8)
            {
                return null;
            }

            sampleRate = BitConverter.ToUInt16(data, 2);
            int sampleCount = BitConverter.ToInt32(data, 4);
            int offset = 8;
            if (ContainsDmxPadding(data))
            {
                offset += 16;
                sampleCount -= 32;
            }
            sampleCount = Math.Min(sampleCount, data.Length - offset);
            if (sampleCount <= 0 || sampleRate <= 0)
            {
                return null;
            }

            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = (data[offset + i] - 128) / 128f;
            }
            return samples;
        }

        // If the first and last 16 samples are the same, the data has pad bytes.
        // https://doomwiki.org/wiki/Sound
        private static bool ContainsDmxPadding(byte[] data)
        {
            int sampleCount = BitConverter.ToInt32(data, 4);
            if (sampleCount < 32 || 8 + sampleCount > data.Length)
            {
                return false;
            }
            byte first = data[8];
            for (int i = 1; i < 16; i++)
            {
                if (data[8 + i] != first)
                {
                    return false;
                }
            }
            byte last = data[8 + sampleCount - 1];
            for (int i = 1; i < 16; i++)
            {
                if (data[8 + sampleCount - i - 1] != last)
                {
                    return false;
                }
            }
            return true;
        }

        private static float GetAmplitude(float[] samples, int sampleRate)
        {
            float max = 0f;
            int count = Math.Min(sampleRate / 5, samples.Length);
            for (int t = 0; t < count; t++)
            {
                max = Math.Max(max, Math.Abs(samples[t]));
            }
            return max;
        }

        public void SetListener(Mobj listener)
        {
            _listener = listener;
        }

        // Called by DOOM once a tic.
        public void Update()
        {
            float now = Time.unscaledTime;
            if (now - _lastUpdate < 0.01f && _lastUpdate >= 0f)
            {
                return;
            }
            _lastUpdate = now;

            for (int i = 0; i < _infos.Length; i++)
            {
                ChannelInfo info = _infos[i];
                AudioSource channel = _channels[i];

                if (info.Playing != Sfx.NONE)
                {
                    if (channel.isPlaying || _paused[i])
                    {
                        info.Priority *= info.Type == SfxType.Diffuse ? SlowDecay : FastDecay;
                        SetParam(channel, info);
                    }
                    else
                    {
                        info.Playing = Sfx.NONE;
                        if (info.Reserved == Sfx.NONE)
                        {
                            info.Source = null;
                        }
                    }
                }

                if (info.Reserved != Sfx.NONE)
                {
                    if (info.Playing != Sfx.NONE)
                    {
                        channel.Stop();
                    }
                    _paused[i] = false;
                    channel.clip = _clips[(int)info.Reserved];
                    SetParam(channel, info);
                    channel.pitch = GetPitch(info.Type, info.Reserved);
                    channel.Play();
                    info.Playing = info.Reserved;
                    info.Reserved = Sfx.NONE;
                }
            }

            if (_uiReserved != Sfx.NONE)
            {
                _uiChannel.Stop();
                _uiChannel.panStereo = 0f;
                _uiChannel.volume = _masterVolumeDecay * _master();
                _uiChannel.clip = _clips[(int)_uiReserved];
                _uiChannel.Play();
                _uiReserved = Sfx.NONE;
            }
        }

        public void StartSound(Sfx sfx)
        {
            if (_clips[(int)sfx] == null)
            {
                return;
            }
            _uiReserved = sfx;
        }

        public void StartSound(Mobj mobj, Sfx sfx, SfxType type)
        {
            StartSound(mobj, sfx, type, 100);
        }

        public void StartSound(Mobj mobj, Sfx sfx, SfxType type, int volume)
        {
            if (_clips[(int)sfx] == null || _listener == null)
            {
                return;
            }

            float x = (mobj.X - _listener.X).ToFloat();
            float y = (mobj.Y - _listener.Y).ToFloat();
            float dist = MathF.Sqrt(x * x + y * y);

            float priority = type == SfxType.Diffuse ? volume : _amplitudes[(int)sfx] * GetDistanceDecay(dist) * volume;

            for (int i = 0; i < _infos.Length; i++)
            {
                ChannelInfo info = _infos[i];
                if (info.Source == mobj && info.Type == type)
                {
                    info.Reserved = sfx;
                    info.Priority = priority;
                    info.Volume = volume;
                    return;
                }
            }

            for (int i = 0; i < _infos.Length; i++)
            {
                ChannelInfo info = _infos[i];
                if (info.Reserved == Sfx.NONE && info.Playing == Sfx.NONE)
                {
                    info.Reserved = sfx;
                    info.Priority = priority;
                    info.Source = mobj;
                    info.Type = type;
                    info.Volume = volume;
                    return;
                }
            }

            float minPriority = float.MaxValue;
            int minChannel = -1;
            for (int i = 0; i < _infos.Length; i++)
            {
                if (_infos[i].Priority < minPriority)
                {
                    minPriority = _infos[i].Priority;
                    minChannel = i;
                }
            }
            if (minChannel >= 0 && priority >= minPriority)
            {
                ChannelInfo info = _infos[minChannel];
                info.Reserved = sfx;
                info.Priority = priority;
                info.Source = mobj;
                info.Type = type;
                info.Volume = volume;
            }
        }

        public void StopSound(Mobj mobj)
        {
            for (int i = 0; i < _infos.Length; i++)
            {
                ChannelInfo info = _infos[i];
                if (info.Source == mobj)
                {
                    info.LastX = info.Source.X;
                    info.LastY = info.Source.Y;
                    info.Source = null;
                    info.Volume /= 5;
                }
            }
        }

        public void Reset()
        {
            _random?.Clear();
            for (int i = 0; i < _infos.Length; i++)
            {
                _channels[i].Stop();
                _paused[i] = false;
                _infos[i].Clear();
            }
            _listener = null;
        }

        // DOOM's own pause (the Pause key, or its menu).
        public void Pause()
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                AudioSource channel = _channels[i];
                if (channel.isPlaying && channel.clip != null && channel.clip.length - channel.time > 0.2f)
                {
                    channel.Pause();
                    _paused[i] = true;
                }
            }
        }

        public void Resume()
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                if (_paused[i])
                {
                    _channels[i].UnPause();
                    _paused[i] = false;
                }
            }
        }

        // The mod freezing DOOM (it lost the keys): everything stops where it is.
        public void Freeze(bool frozen)
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                if (frozen)
                {
                    _channels[i].Pause();
                }
                else if (!_paused[i])   // one DOOM paused itself stays paused
                {
                    _channels[i].UnPause();
                }
            }
            if (frozen)
            {
                _uiChannel.Pause();
            }
            else
            {
                _uiChannel.UnPause();
            }
        }

        private void SetParam(AudioSource source, ChannelInfo info)
        {
            float master = _master();
            if (info.Type == SfxType.Diffuse || _listener == null)
            {
                source.panStereo = 0f;
                source.volume = 0.01f * _masterVolumeDecay * info.Volume * master;
                return;
            }

            Fixed sourceX = info.Source == null ? info.LastX : info.Source.X;
            Fixed sourceY = info.Source == null ? info.LastY : info.Source.Y;
            float x = (sourceX - _listener.X).ToFloat();
            float y = (sourceY - _listener.Y).ToFloat();

            if (Math.Abs(x) < 16 && Math.Abs(y) < 16)
            {
                source.panStereo = 0f;
                source.volume = 0.01f * _masterVolumeDecay * info.Volume * master;
            }
            else
            {
                float dist = MathF.Sqrt(x * x + y * y);
                float angle = MathF.Atan2(y, x) - (float)_listener.Angle.ToRadian();
                // OpenAL's listener faces -z with +x to the right; SilkSound puts the sound at
                // (-sin(angle), 0, -cos(angle)).
                source.panStereo = Mathf.Clamp(-MathF.Sin(angle) * PanWidth, -1f, 1f);
                source.volume = 0.01f * _masterVolumeDecay * GetDistanceDecay(dist) * info.Volume * master;
            }
        }

        private static float GetDistanceDecay(float dist)
        {
            return dist < CloseDist ? 1f : Math.Max((ClipDist - dist) / Attenuator, 0f);
        }

        private float GetPitch(SfxType type, Sfx sfx)
        {
            if (_random == null || sfx == Sfx.ITEMUP || sfx == Sfx.TINK || sfx == Sfx.RADIO)
            {
                return 1f;
            }
            return type == SfxType.Voice
                ? 1f + 0.075f * (_random.Next() - 128) / 128
                : 1f + 0.025f * (_random.Next() - 128) / 128;
        }

        public int MaxVolume => 15;

        public int Volume
        {
            get => _config.audio_soundvolume;
            set
            {
                _config.audio_soundvolume = value;
                _masterVolumeDecay = (float)_config.audio_soundvolume / MaxVolume;
            }
        }

        public void Dispose()
        {
            if (_channels != null)
            {
                foreach (AudioSource channel in _channels)
                {
                    if (channel != null)
                    {
                        channel.Stop();
                        UnityEngine.Object.Destroy(channel);
                    }
                }
                _channels = null;
            }
            if (_uiChannel != null)
            {
                _uiChannel.Stop();
                UnityEngine.Object.Destroy(_uiChannel);
                _uiChannel = null;
            }
            if (_clips != null)
            {
                foreach (AudioClip clip in _clips)
                {
                    if (clip != null)
                    {
                        UnityEngine.Object.Destroy(clip);
                    }
                }
                _clips = null;
            }
        }

        private sealed class ChannelInfo
        {
            public Sfx Reserved;
            public Sfx Playing;
            public float Priority;

            public Mobj Source;
            public SfxType Type;
            public int Volume;
            public Fixed LastX;
            public Fixed LastY;

            public void Clear()
            {
                Reserved = Sfx.NONE;
                Playing = Sfx.NONE;
                Priority = 0;
                Source = null;
                Type = 0;
                Volume = 0;
                LastX = Fixed.Zero;
                LastY = Fixed.Zero;
            }
        }
    }
}
