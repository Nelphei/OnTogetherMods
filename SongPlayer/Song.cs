using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MidiPlayerTK;
using MPTK.NAudio.Midi;

namespace SongPlayer
{
    // One note from the file. Times are in milliseconds from the first note of the song.
    internal struct SongNote
    {
        public float Start;
        public float End;
        public int Pitch;
        public int Channel;
    }

    // A set of notes the player can choose to play, like "Melody" or "Piano".
    internal sealed class SongPart
    {
        public string Label;
        public List<SongNote> Notes;
    }

    // A MIDI file read with the game's own MIDI library (Maestro, in MidiPlayer.Run.dll).
    internal sealed class Song
    {
        // MIDI channel 10 (9 counting from 0) is always drums.
        public const int DrumChannel = 9;

        // Shorter notes are stretched to this, so every note is heard.
        private const float MinNoteMs = 60f;

        // Notes that start this close together count as one chord when picking the melody.
        private const float ChordMs = 30f;

        public string Name;
        public float LengthMs;

        // Parts for the piano, guitar, keytar and mic: all parts, the melody, then each instrument
        // in the file. Empty when the file has no notes outside the drum channel.
        public List<SongPart> MelodicParts = new List<SongPart>();

        // Notes for the drum kit, or null when the file has none.
        public SongPart DrumPart;

        // Returns null and sets error when the file can't be read.
        public static Song Load(string path, out string error)
        {
            error = null;
            var midi = new MidiLoad();
            if (!midi.MPTK_Load(File.ReadAllBytes(path)) || midi.MPTK_MidiEvents == null)
            {
                error = "isn't a MIDI file the game can read";
                return null;
            }

            var notes = new List<SongNote>();
            var trackOf = new Dictionary<int, long>();
            var trackNames = new Dictionary<long, string>();
            var programs = new Dictionary<int, int>();
            foreach (MPTKEvent e in midi.MPTK_MidiEvents)
            {
                if (e.Command == MPTKCommand.NoteOn && e.Velocity > 0)
                {
                    notes.Add(new SongNote
                    {
                        Start = e.RealTime,
                        End = e.RealTime + Math.Max(e.Duration, MinNoteMs),
                        Pitch = e.Value,
                        Channel = e.Channel,
                    });
                    if (!trackOf.ContainsKey(e.Channel))
                    {
                        trackOf[e.Channel] = e.Track;
                    }
                }
                else if (e.Command == MPTKCommand.MetaEvent && e.Meta == MPTKMeta.SequenceTrackName &&
                         !string.IsNullOrWhiteSpace(e.Info) && !trackNames.ContainsKey(e.Track))
                {
                    trackNames[e.Track] = e.Info.Trim();
                }
                else if (e.Command == MPTKCommand.PatchChange && !programs.ContainsKey(e.Channel))
                {
                    programs[e.Channel] = e.Value;
                }
            }
            if (notes.Count == 0)
            {
                error = "has no notes";
                return null;
            }

            // Skip any silence before the first note.
            notes.Sort((a, b) => a.Start.CompareTo(b.Start));
            float first = notes[0].Start;
            float last = 0f;
            for (int i = 0; i < notes.Count; i++)
            {
                SongNote n = notes[i];
                n.Start -= first;
                n.End -= first;
                notes[i] = n;
                last = Math.Max(last, n.End);
            }

            var song = new Song
            {
                Name = Path.GetFileNameWithoutExtension(path),
                LengthMs = last,
            };

            List<SongNote> drums = notes.Where(n => n.Channel == DrumChannel).ToList();
            if (drums.Count > 0)
            {
                song.DrumPart = new SongPart { Label = "Drums", Notes = drums };
            }

            List<SongNote> melodic = notes.Where(n => n.Channel != DrumChannel).ToList();
            if (melodic.Count > 0)
            {
                song.MelodicParts.Add(new SongPart { Label = "All parts", Notes = melodic });
                song.MelodicParts.Add(new SongPart { Label = "Melody only", Notes = Melody(melodic) });

                List<int> channels = melodic.Select(n => n.Channel).Distinct().OrderBy(c => c).ToList();
                if (channels.Count > 1)
                {
                    foreach (int channel in channels)
                    {
                        song.MelodicParts.Add(new SongPart
                        {
                            Label = PartName(channel, trackOf, trackNames, programs),
                            Notes = melodic.Where(n => n.Channel == channel).ToList(),
                        });
                    }
                }
            }
            return song;
        }

        // The top note of each chord, with each note cut off when the next one starts.
        // Most tunes carry the melody on top, so this turns a full arrangement into a clean tune.
        private static List<SongNote> Melody(List<SongNote> notes)
        {
            var tops = new List<SongNote>();
            int i = 0;
            while (i < notes.Count)
            {
                SongNote top = notes[i];
                int j = i + 1;
                while (j < notes.Count && notes[j].Start - notes[i].Start <= ChordMs)
                {
                    if (notes[j].Pitch > top.Pitch)
                    {
                        top = notes[j];
                    }
                    j++;
                }
                tops.Add(top);
                i = j;
            }
            for (int k = 0; k + 1 < tops.Count; k++)
            {
                SongNote n = tops[k];
                n.End = Math.Max(n.Start + MinNoteMs, Math.Min(n.End, tops[k + 1].Start));
                tops[k] = n;
            }
            return tops;
        }

        // The track's name if the file has one, then the General MIDI instrument, then the channel.
        private static string PartName(int channel, Dictionary<int, long> trackOf,
            Dictionary<long, string> trackNames, Dictionary<int, int> programs)
        {
            if (trackOf.TryGetValue(channel, out long track) && trackNames.TryGetValue(track, out string name))
            {
                return name;
            }
            if (programs.TryGetValue(channel, out int program))
            {
                try
                {
                    return PatchChangeEvent.GetPatchName(program);
                }
                catch (Exception)
                {
                    // Fall through to the channel number.
                }
            }
            return "Channel " + (channel + 1);
        }
    }
}
