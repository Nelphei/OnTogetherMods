using System;
using System.IO;
using ManagedDoom;
using ManagedDoom.Audio;
using MeltySynth;
using UnityEngine;

namespace DoomTogether
{
    // DOOM's music: the WAD's MUS (or MIDI) tracks played on MeltySynth, a SoundFont synthesizer,
    // with the TimGM6mb sound bank that sits beside the DLL. The synthesizer runs on Unity's audio
    // thread (OnAudioFilterRead). Ported from Managed Doom's SilkMusic (GPLv2).
    internal sealed class UnityMusic : IMusic, IDisposable
    {
        public const string SoundFontFile = "TimGM6mb.sf2";

        // Loading the sound bank takes a moment, so it's kept between games.
        private static SoundFont _soundFont;

        private readonly Config _config;
        private readonly Wad _wad;
        private readonly int _sampleRate;
        private GameObject _object;
        private MusicFilter _filter;
        private Bgm _current = Bgm.NONE;

        // master: the mod's own volume (0-1). Read on the audio thread, so keep it cheap.
        public UnityMusic(Config config, GameContent content, GameObject host, Func<float> master)
        {
            _config = config;
            _wad = content.Wad;
            config.audio_musicvolume = Mathf.Clamp(config.audio_musicvolume, 0, MaxVolume);

            if (_soundFont == null)
            {
                _soundFont = new SoundFont(Path.Combine(Plugin.ModFolder, SoundFontFile));
            }

            _sampleRate = AudioSettings.outputSampleRate;
            var settings = new SynthesizerSettings(_sampleRate)
            {
                BlockSize = MusDecoder.BlockLength(_sampleRate),
                EnableReverbAndChorus = config.audio_musiceffect,
            };
            var synthesizer = new Synthesizer(_soundFont, settings);

            // OnAudioFilterRead only runs while the AudioSource beside it plays, so it plays a
            // looping second of silence that the filter writes the music over.
            _object = new GameObject("DoomTogether Music");
            _object.hideFlags = HideFlags.HideAndDontSave;
            _object.transform.SetParent(host.transform, false);
            AudioSource source = _object.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.clip = AudioClip.Create("DoomTogether Silence", _sampleRate, 2, _sampleRate, false);
            source.clip.hideFlags = HideFlags.HideAndDontSave;
            _filter = _object.AddComponent<MusicFilter>();
            _filter.Setup(synthesizer, config, MaxVolume, master);
            source.Play();
        }

        public void StartMusic(Bgm bgm, bool loop)
        {
            if (bgm == _current || _filter == null)
            {
                return;
            }
            _current = bgm;

            IDecoder decoder = null;
            try
            {
                string lump = "D_" + DoomInfo.BgmNames[(int)bgm].ToString().ToUpper();
                if (_wad.GetLumpNumber(lump) != -1)
                {
                    decoder = ReadData(_wad.ReadLump(lump), loop);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't play DOOM's music track {bgm}: {e.Message}");
            }
            _filter.SetDecoder(decoder);
        }

        private IDecoder ReadData(byte[] data, bool loop)
        {
            if (StartsWith(data, MusDecoder.MusHeader))
            {
                return new MusDecoder(data, loop);
            }
            if (StartsWith(data, MidiDecoder.MidiHeader))
            {
                return new MidiDecoder(data, loop);
            }
            throw new Exception("not MUS or MIDI");
        }

        private static bool StartsWith(byte[] data, byte[] header)
        {
            if (data.Length < header.Length)
            {
                return false;
            }
            for (int i = 0; i < header.Length; i++)
            {
                if (data[i] != header[i])
                {
                    return false;
                }
            }
            return true;
        }

        // The mod freezing DOOM (it lost the keys): the music holds its place.
        public void Freeze(bool frozen)
        {
            if (_filter != null)
            {
                _filter.Frozen = frozen;
            }
        }

        public int MaxVolume => 15;

        public int Volume
        {
            get => _config.audio_musicvolume;
            set => _config.audio_musicvolume = value;
        }

        public void Dispose()
        {
            if (_object != null)
            {
                AudioSource source = _object.GetComponent<AudioSource>();
                if (source != null && source.clip != null)
                {
                    source.Stop();
                    UnityEngine.Object.Destroy(source.clip);
                }
                UnityEngine.Object.Destroy(_object);
                _object = null;
                _filter = null;
            }
        }

        // ---- The audio thread ----

        internal interface IDecoder
        {
            void RenderWaveform(Synthesizer synthesizer, float[] left, float[] right, int count);
        }

        internal sealed class MusicFilter : MonoBehaviour
        {
            private Synthesizer _synthesizer;
            private Config _config;
            private int _maxVolume;
            private Func<float> _master;
            private float[] _left = new float[0];
            private float[] _right = new float[0];

            private volatile IDecoder _current;
            private volatile IDecoder _reserved;
            private volatile bool _changed;
            private volatile float _masterValue = 1f;

            public volatile bool Frozen;

            public void Setup(Synthesizer synthesizer, Config config, int maxVolume, Func<float> master)
            {
                _synthesizer = synthesizer;
                _config = config;
                _maxVolume = maxVolume;
                _master = master;
                _masterValue = master();
            }

            public void SetDecoder(IDecoder decoder)
            {
                _reserved = decoder;
                _changed = true;
            }

            // The mod's volume setting is read here, on the main thread.
            private void Update()
            {
                if (_master != null)
                {
                    _masterValue = _master();
                }
            }

            private void OnAudioFilterRead(float[] data, int channels)
            {
                Synthesizer synthesizer = _synthesizer;
                if (synthesizer == null || channels <= 0)
                {
                    return;
                }
                if (_changed)
                {
                    _changed = false;
                    synthesizer.Reset();
                    _current = _reserved;
                }

                IDecoder decoder = _current;
                if (decoder == null || Frozen)
                {
                    Array.Clear(data, 0, data.Length);
                    return;
                }

                int count = data.Length / channels;
                if (_left.Length < count)
                {
                    _left = new float[count];
                    _right = new float[count];
                }
                try
                {
                    decoder.RenderWaveform(synthesizer, _left, _right, count);
                }
                catch (Exception)
                {
                    _current = null;
                    Array.Clear(data, 0, data.Length);
                    return;
                }

                float a = 2f * _config.audio_musicvolume / _maxVolume * _masterValue;
                int p = 0;
                for (int t = 0; t < count; t++)
                {
                    float l = Mathf.Clamp(a * _left[t], -1f, 1f);
                    float r = Mathf.Clamp(a * _right[t], -1f, 1f);
                    if (channels == 1)
                    {
                        data[p++] = 0.5f * (l + r);
                    }
                    else
                    {
                        data[p++] = l;
                        data[p++] = r;
                        for (int c = 2; c < channels; c++)
                        {
                            data[p++] = 0f;
                        }
                    }
                }
            }
        }

        // DOOM's own music format: MIDI-like events at 140 ticks a second.
        private sealed class MusDecoder : IDecoder
        {
            public static readonly byte[] MusHeader = { (byte)'M', (byte)'U', (byte)'S', 0x1A };

            public static int BlockLength(int sampleRate) => Mathf.Clamp(sampleRate / 140, 8, 1024);

            private readonly byte[] _data;
            private readonly bool _loop;
            private readonly int _scoreStart;

            private readonly MusEvent[] _events;
            private int _eventCount;

            private readonly int[] _lastVolume;
            private int _p;
            private int _delay;

            private int _blockWrote = -1;

            public MusDecoder(byte[] data, bool loop)
            {
                _data = data;
                _loop = loop;
                _scoreStart = BitConverter.ToUInt16(data, 6);

                _events = new MusEvent[128];
                for (int i = 0; i < _events.Length; i++)
                {
                    _events[i] = new MusEvent();
                }
                _lastVolume = new int[16];
                Reset();
            }

            public void RenderWaveform(Synthesizer synthesizer, float[] left, float[] right, int count)
            {
                if (_blockWrote < 0)
                {
                    _blockWrote = synthesizer.BlockSize;
                }
                int wrote = 0;
                while (wrote < count)
                {
                    if (_blockWrote == synthesizer.BlockSize)
                    {
                        ProcessMidiEvents(synthesizer);
                        _blockWrote = 0;
                    }

                    int rem = Math.Min(synthesizer.BlockSize - _blockWrote, count - wrote);
                    synthesizer.Render(left.AsSpan(wrote, rem), right.AsSpan(wrote, rem));
                    _blockWrote += rem;
                    wrote += rem;
                }
            }

            private void ProcessMidiEvents(Synthesizer synthesizer)
            {
                if (_delay > 0)
                {
                    _delay--;
                }
                if (_delay == 0)
                {
                    _delay = ReadSingleEventGroup();
                    SendEvents(synthesizer);
                    if (_delay == -1)
                    {
                        synthesizer.NoteOffAll(false);
                        if (_loop)
                        {
                            Reset();
                        }
                    }
                }
            }

            private void Reset()
            {
                Array.Clear(_lastVolume, 0, _lastVolume.Length);
                _p = _scoreStart;
                _delay = 0;
            }

            private int ReadSingleEventGroup()
            {
                _eventCount = 0;
                while (true)
                {
                    ReadResult result = ReadSingleEvent();
                    if (result == ReadResult.EndOfGroup)
                    {
                        break;
                    }
                    if (result == ReadResult.EndOfFile)
                    {
                        return -1;
                    }
                }

                int time = 0;
                while (true)
                {
                    byte value = _data[_p++];
                    time = time * 128 + (value & 127);
                    if ((value & 128) == 0)
                    {
                        break;
                    }
                }
                return time;
            }

            private ReadResult ReadSingleEvent()
            {
                int channelNumber = _data[_p] & 0xF;
                if (channelNumber == 15)
                {
                    channelNumber = 9;
                }
                else if (channelNumber >= 9)
                {
                    channelNumber++;
                }

                int eventType = (_data[_p] & 0x70) >> 4;
                bool last = (_data[_p] >> 7) != 0;
                _p++;

                if (eventType == 6)   // end of file
                {
                    return ReadResult.EndOfFile;
                }
                if (_eventCount >= _events.Length)
                {
                    throw new Exception("too many MUS events at once");
                }

                MusEvent me = _events[_eventCount++];
                me.Type = eventType;
                me.Channel = channelNumber;
                switch (eventType)
                {
                    case 0: // release note
                        me.Data1 = _data[_p++];
                        me.Data2 = 0;
                        break;

                    case 1: // play note
                        int playNote = _data[_p++];
                        me.Data1 = playNote & 127;
                        if ((playNote & 128) != 0)
                        {
                            me.Data2 = _data[_p++];
                            _lastVolume[channelNumber] = me.Data2;
                        }
                        else
                        {
                            me.Data2 = _lastVolume[channelNumber];
                        }
                        break;

                    case 2: // pitch wheel
                        int pw2 = (_data[_p++] << 7) / 2;
                        me.Data1 = pw2 & 127;
                        me.Data2 = pw2 >> 7;
                        break;

                    case 3: // system event
                        me.Data1 = _data[_p++];
                        me.Data2 = 0;
                        break;

                    case 4: // control change
                        me.Data1 = _data[_p++];
                        me.Data2 = _data[_p++];
                        break;

                    default:
                        throw new Exception("unknown MUS event type " + eventType);
                }
                return last ? ReadResult.EndOfGroup : ReadResult.Ongoing;
            }

            private void SendEvents(Synthesizer synthesizer)
            {
                for (int i = 0; i < _eventCount; i++)
                {
                    MusEvent me = _events[i];
                    switch (me.Type)
                    {
                        case 0:
                            synthesizer.NoteOff(me.Channel, me.Data1);
                            break;
                        case 1:
                            synthesizer.NoteOn(me.Channel, me.Data1, me.Data2);
                            break;
                        case 2:
                            synthesizer.ProcessMidiMessage(me.Channel, 0xE0, me.Data1, me.Data2);
                            break;
                        case 3:
                            if (me.Data1 == 11)
                            {
                                synthesizer.NoteOffAll(me.Channel, false);
                            }
                            else if (me.Data1 == 14)
                            {
                                synthesizer.ResetAllControllers(me.Channel);
                            }
                            break;
                        case 4:
                            switch (me.Data1)
                            {
                                case 0: synthesizer.ProcessMidiMessage(me.Channel, 0xC0, me.Data2, 0); break;    // program change
                                case 1: synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x00, me.Data2); break; // bank select
                                case 2: synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x01, me.Data2); break; // modulation
                                case 3: synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x07, me.Data2); break; // volume
                                case 4: synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x0A, me.Data2); break; // pan
                                case 5: synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x0B, me.Data2); break; // expression
                                case 6: synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x5B, me.Data2); break; // reverb
                                case 7: synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x5D, me.Data2); break; // chorus
                                case 8: synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x40, me.Data2); break; // pedal
                            }
                            break;
                    }
                }
            }

            private sealed class MusEvent
            {
                public int Type;
                public int Channel;
                public int Data1;
                public int Data2;
            }

            private enum ReadResult
            {
                Ongoing,
                EndOfGroup,
                EndOfFile,
            }
        }

        // Some WADs (Freedoom among them) keep their music as standard MIDI files.
        private sealed class MidiDecoder : IDecoder
        {
            public static readonly byte[] MidiHeader = { (byte)'M', (byte)'T', (byte)'h', (byte)'d' };

            private readonly MidiFile _midi;
            private readonly bool _loop;
            private MidiFileSequencer _sequencer;

            public MidiDecoder(byte[] data, bool loop)
            {
                _midi = new MidiFile(new MemoryStream(data));
                _loop = loop;
            }

            public void RenderWaveform(Synthesizer synthesizer, float[] left, float[] right, int count)
            {
                if (_sequencer == null)
                {
                    _sequencer = new MidiFileSequencer(synthesizer);
                    _sequencer.Play(_midi, _loop);
                }
                _sequencer.Render(left.AsSpan(0, count), right.AsSpan(0, count));
            }
        }
    }
}
