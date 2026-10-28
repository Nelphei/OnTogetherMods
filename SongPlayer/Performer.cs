using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SongPlayer
{
    // Plays a list of notes on the instrument you're sitting at, by pressing the game's own 13 keys
    // (one octave, C to C) the way your keyboard does. See reference/Assembly-CSharp/MusicManager.cs.
    // PlayNoteMaster and StopNoteMaster light up the on-screen keys and send each note to everyone,
    // so other players hear the song without the mod.
    //
    // Each instrument plays one octave at a time, starting at its "octave shift" (a MIDI note
    // number). Before a song starts we move to the octave that fits the most notes, the same as
    // clicking the octave arrows (other players hear the change too). Notes that still don't fit
    // are moved up or down by whole octaves until they do.
    internal sealed class Performer
    {
        public const int KeyCount = 13;

        private static readonly AccessTools.FieldRef<MusicManager, List<int>> OctaveShifts =
            AccessTools.FieldRefAccess<MusicManager, List<int>>("_octaveShifts");
        private static readonly AccessTools.FieldRef<MusicManager, List<int>> MinOctaves =
            AccessTools.FieldRefAccess<MusicManager, List<int>>("_minOctaves");
        private static readonly AccessTools.FieldRef<MusicManager, List<int>> MaxOctaves =
            AccessTools.FieldRefAccess<MusicManager, List<int>>("_maxOctaves");

        // Drum sounds the kit might not have, and the closest ones to use instead, in order.
        // The numbers are General MIDI drum notes.
        private static readonly Dictionary<int, int[]> DrumStandIns = new Dictionary<int, int[]>
        {
            { 35, new[] { 36 } },                 // kick
            { 36, new[] { 35 } },
            { 37, new[] { 38, 40 } },             // side stick
            { 38, new[] { 40 } },                 // snare
            { 39, new[] { 38, 40 } },             // clap
            { 40, new[] { 38 } },
            { 41, new[] { 43, 45 } },             // toms, low to high
            { 43, new[] { 41, 45 } },
            { 45, new[] { 47, 43 } },
            { 47, new[] { 45, 48 } },
            { 48, new[] { 47, 50 } },
            { 50, new[] { 48, 47 } },
            { 42, new[] { 44, 46 } },             // hi-hats
            { 44, new[] { 42, 46 } },
            { 46, new[] { 42, 44 } },
            { 49, new[] { 57, 46, 42 } },         // crash cymbals
            { 57, new[] { 49, 46, 42 } },
            { 52, new[] { 49, 57, 46 } },         // china
            { 55, new[] { 49, 57, 46 } },         // splash
            { 51, new[] { 59, 53, 42 } },         // ride cymbals
            { 53, new[] { 51, 59, 42 } },
            { 59, new[] { 51, 53, 42 } },
        };

        private readonly float[] _heldUntil = new float[KeyCount];
        private MusicManager _music;
        private InstrumentType _instrument;
        private List<SongNote> _notes;
        private int _next;
        private double _startTime;
        private int _lowest;

        public Performer()
        {
            ClearKeys();
        }

        public bool IsPlaying => _notes != null;

        // How far into the song we are, in milliseconds.
        public float PositionMs => IsPlaying ? (float)((Time.realtimeSinceStartupAsDouble - _startTime) * 1000.0) : 0f;

        public void Play(MusicManager music, InstrumentType instrument, List<SongNote> notes, float fromMs)
        {
            Stop();
            _music = music;
            _instrument = instrument;
            // Done before the notes are set, so if it fails nothing is left half-started.
            _lowest = instrument == InstrumentType.Drum ? CurrentShift() : MoveToBestOctave(notes);

            _next = 0;
            while (_next < notes.Count && notes[_next].Start < fromMs)
            {
                _next++;
            }
            _notes = notes;
            _startTime = Time.realtimeSinceStartupAsDouble - fromMs / 1000.0;
        }

        // Lets go of every key we're holding.
        public void Stop()
        {
            if (_music != null)
            {
                for (int key = 0; key < KeyCount; key++)
                {
                    if (_heldUntil[key] >= 0f)
                    {
                        _music.StopNoteMaster(key, _instrument);
                    }
                }
            }
            Forget();
        }

        // Clears our state without touching the instrument. Used after the game has already let go
        // of every key itself (you stood up from the instrument).
        public void Forget()
        {
            ClearKeys();
            _notes = null;
            _music = null;
        }

        // Call once a frame. Returns false when the song has finished.
        public bool Tick(int maxNotesAtOnce)
        {
            if (!IsPlaying)
            {
                return false;
            }
            float now = PositionMs;
            // Follow the octave arrows if the player clicks them during the song.
            _lowest = CurrentShift();

            for (int key = 0; key < KeyCount; key++)
            {
                if (_heldUntil[key] >= 0f && _heldUntil[key] <= now)
                {
                    _music.StopNoteMaster(key, _instrument);
                    _heldUntil[key] = -1f;
                }
            }

            while (_next < _notes.Count && _notes[_next].Start <= now)
            {
                SongNote note = _notes[_next++];
                // After a long frame (loading, alt-tab) skip notes that have already ended,
                // instead of playing them all at once.
                if (note.End <= now)
                {
                    continue;
                }
                int key = KeyFor(note.Pitch);
                if (key < 0)
                {
                    continue;
                }
                if (_heldUntil[key] >= 0f)
                {
                    // The key is already down: lift it and press it again. Pressing a key twice
                    // without lifting it would leave the first note ringing forever.
                    _music.StopNoteMaster(key, _instrument);
                }
                else if (HeldCount() >= maxNotesAtOnce)
                {
                    continue;
                }
                _music.PlayNoteMaster(key, _instrument);
                _heldUntil[key] = note.End;
            }

            if (_next >= _notes.Count && HeldCount() == 0)
            {
                Forget();
                return false;
            }
            return true;
        }

        private int KeyFor(int pitch)
        {
            if (_instrument == InstrumentType.Drum)
            {
                int key = pitch - _lowest;
                if (key >= 0 && key < KeyCount)
                {
                    return key;
                }
                if (DrumStandIns.TryGetValue(pitch, out int[] standIns))
                {
                    foreach (int standIn in standIns)
                    {
                        key = standIn - _lowest;
                        if (key >= 0 && key < KeyCount)
                        {
                            return key;
                        }
                    }
                }
                return -1;
            }

            int k = pitch - _lowest;
            while (k < 0)
            {
                k += 12;
            }
            while (k >= KeyCount)
            {
                k -= 12;
            }
            return k;
        }

        // Shifts the instrument to the octave that fits the most notes, and returns its lowest note.
        private int MoveToBestOctave(List<SongNote> notes)
        {
            int type = (int)_instrument;
            List<int> shifts = OctaveShifts(_music);
            int current = shifts[type];
            int min = MinOctaves(_music)[type];
            int max = MaxOctaves(_music)[type];

            // Only octaves the arrows can reach from here.
            int best = current;
            int bestCount = Fits(notes, current);
            for (int lowest = current - 12; lowest >= min; lowest -= 12)
            {
                Consider(lowest);
            }
            for (int lowest = current + 12; lowest <= max; lowest += 12)
            {
                Consider(lowest);
            }

            // ShiftOctave*Master are the game's octave arrows: they move one octave and tell everyone.
            for (int i = 0; i < 10 && shifts[type] < best; i++)
            {
                _music.ShiftOctaveUpMaster();
            }
            for (int i = 0; i < 10 && shifts[type] > best; i++)
            {
                _music.ShiftOctaveDownMaster();
            }
            return CurrentShift();

            void Consider(int lowest)
            {
                int count = Fits(notes, lowest);
                // On a tie, keep the octave closer to where the player had it.
                if (count > bestCount || (count == bestCount && Mathf.Abs(lowest - current) < Mathf.Abs(best - current)))
                {
                    best = lowest;
                    bestCount = count;
                }
            }
        }

        private int CurrentShift()
        {
            return OctaveShifts(_music)[(int)_instrument];
        }

        private static int Fits(List<SongNote> notes, int lowest)
        {
            int count = 0;
            foreach (SongNote note in notes)
            {
                if (note.Pitch >= lowest && note.Pitch < lowest + KeyCount)
                {
                    count++;
                }
            }
            return count;
        }

        private int HeldCount()
        {
            int count = 0;
            for (int key = 0; key < KeyCount; key++)
            {
                if (_heldUntil[key] >= 0f)
                {
                    count++;
                }
            }
            return count;
        }

        private void ClearKeys()
        {
            for (int key = 0; key < KeyCount; key++)
            {
                _heldUntil[key] = -1f;
            }
        }
    }
}
