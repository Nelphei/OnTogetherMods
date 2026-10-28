using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HarmonyLib;
using OnTogetherMods.Shared;
using UnityEngine;

namespace SongPlayer
{
    // The song list. It appears while you sit at an instrument and goes away when you stand up.
    // It sits in the bottom window stack (Shared/PanelStack.cs) and is drawn with Unity's
    // immediate-mode GUI, laid out at 1080p and scaled to the screen.
    internal sealed class SongWindow : MonoBehaviour
    {
        private const string PanelId = "SongPlayer";
        private const float Width = 360f;
        private const float Pad = 10f;
        private const float HeaderHeight = 24f;
        private const float RowHeight = 30f;
        private const float Gap = 4f;
        private const float ProgressHeight = 22f;
        private const float StatusHeight = 22f;
        private const float ScrollbarWidth = 16f;
        private const float StatusSeconds = 5f;

        private static readonly AccessTools.FieldRef<MusicManager, MusicInstrumentController> CurrentInstrument =
            AccessTools.FieldRefAccess<MusicManager, MusicInstrumentController>("_currentInstrument");

        private static SongWindow _instance;

        // Set by the patch when you sit at an instrument.
        internal static MusicManager Music;

        // Read by the patches. True while the mouse is over the window, so clicks don't reach the world.
        internal static bool IsMouseOver { get; private set; }

        private static Settings S => Plugin.Settings;

        private readonly Performer _performer = new Performer();
        private readonly List<string> _files = new List<string>();
        private bool _active;
        private InstrumentType _instrument;

        private Song _song;
        private string _songPath;
        private int _partIndex;

        private Vector2 _scroll;
        private string _status;
        private float _statusUntil;

        // Where the window was last drawn, in screen pixels from the top-left.
        private Rect _screenRect;
        private int _drawnFrame = -100;
        private ClickBlocker _blocker;

        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _instrumentStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _songStyle;
        private GUIStyle _playingStyle;
        private GUIStyle _emptyStyle;
        private GUIStyle _timeStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _tipStyle;

        private void Awake()
        {
            _instance = this;
            _blocker = new ClickBlocker("SongPlayer ClickBlocker");
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsMouseOver = false;
            }
            _blocker?.Destroy();
        }

        private void Update()
        {
            try
            {
                bool atInstrument = Music != null && Music.IsActive;
                if (atInstrument && !_active)
                {
                    SitDown();
                }
                else if (!atInstrument && _active)
                {
                    StandUp();
                }

                if (_active && _performer.IsPlaying && !_performer.Tick(S.MaxNotesAtOnce.Value))
                {
                    Status("Finished " + _song.Name);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                _performer.Forget();
                Status("Something went wrong. See the BepInEx log.");
            }
            UpdateMouseOver();
        }

        private void SitDown()
        {
            MusicInstrumentController instrument = CurrentInstrument(Music);
            if (instrument == null)
            {
                return;
            }
            _active = true;
            _instrument = instrument.musicInstrumentType;
            _partIndex = 0;
            _status = null;
            RefreshList();
        }

        private void StandUp()
        {
            // Standing up already lets go of every key (MusicManager.DropInstrument).
            _active = false;
            _performer.Forget();
        }

        private void UpdateMouseOver()
        {
            bool shown = _active && Time.frameCount - _drawnFrame <= 2;
            IsMouseOver = shown && ClickBlocker.MouseIn(_screenRect);
            _blocker?.Update(shown, _screenRect);
        }

        // ---- Actions ----

        private void RefreshList()
        {
            _files.Clear();
            string folder = S.Folder();
            try
            {
                if (Directory.Exists(folder))
                {
                    _files.AddRange(Directory.GetFiles(folder)
                        .Where(f => f.EndsWith(".mid", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".midi", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase));
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                Status("Couldn't read the songs folder. See the BepInEx log.");
            }
        }

        private void OpenFolder()
        {
            string folder = S.Folder();
            try
            {
                Directory.CreateDirectory(folder);
                if (Application.platform == RuntimePlatform.WindowsPlayer)
                {
                    Process.Start("explorer.exe", "\"" + folder + "\"");
                }
                else
                {
                    // Opens Finder on a Mac.
                    Application.OpenURL(new Uri(folder).AbsoluteUri);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                Status("Couldn't open " + folder);
            }
        }

        private void PlayFile(string path)
        {
            if (path != _songPath || _song == null)
            {
                Song song;
                string error;
                try
                {
                    song = Song.Load(path, out error);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError(e);
                    song = null;
                    error = "couldn't be opened";
                }
                if (song == null)
                {
                    Status(Path.GetFileNameWithoutExtension(path) + " " + error);
                    return;
                }
                _song = song;
                _songPath = path;
                _partIndex = 0;
            }
            StartPart(0f);
        }

        private void StartPart(float fromMs)
        {
            List<SongPart> parts = Parts();
            if (parts.Count == 0)
            {
                _performer.Stop();
                Status(_instrument == InstrumentType.Drum
                    ? _song.Name + " has no drums. Try another instrument."
                    : _song.Name + " only has drums. Try the drum kit.");
                return;
            }
            _partIndex = Mathf.Clamp(_partIndex, 0, parts.Count - 1);
            _performer.Play(Music, _instrument, parts[_partIndex].Notes, fromMs);
            _status = null;
        }

        private void NextPart()
        {
            List<SongPart> parts = Parts();
            if (parts.Count < 2)
            {
                return;
            }
            _partIndex = (_partIndex + 1) % parts.Count;
            if (_performer.IsPlaying)
            {
                StartPart(_performer.PositionMs);
            }
        }

        private List<SongPart> Parts()
        {
            if (_song == null)
            {
                return new List<SongPart>();
            }
            if (_instrument == InstrumentType.Drum)
            {
                return _song.DrumPart != null ? new List<SongPart> { _song.DrumPart } : new List<SongPart>();
            }
            return _song.MelodicParts;
        }

        private void Status(string text)
        {
            _status = text;
            _statusUntil = Time.unscaledTime + StatusSeconds;
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            // Hidden while Photo Mode takes a photo.
            if (!_active || PanelStack.Hidden)
            {
                return;
            }
            if (_boxStyle == null)
            {
                MakeStyles();
            }

            Matrix4x4 previous = PanelStack.Begin(out _);
            try
            {
                DrawWindow();
            }
            catch (Exception e) when (!(e is ExitGUIException))
            {
                Plugin.Log.LogError(e);
            }
            finally
            {
                PanelStack.End(previous);
            }
        }

        private void DrawWindow()
        {
            int rows = Mathf.Clamp(_files.Count, 2, S.VisibleRows.Value);
            float listHeight = rows * RowHeight + (rows - 1) * Gap;
            float height = Pad + HeaderHeight + Gap + listHeight + 2 * Gap + RowHeight + Gap +
                           ProgressHeight + Gap + RowHeight + Gap + StatusHeight + Pad;

            Rect panel = PanelStack.Place(PanelId, PanelStack.SongPlayerOrder, new Vector2(Width, height));
            float scale = Screen.height / 1080f;
            _screenRect = new Rect(panel.x * scale, panel.y * scale, panel.width * scale, panel.height * scale);
            _drawnFrame = Time.frameCount;

            GUI.Box(panel, GUIContent.none, _boxStyle);
            float x = panel.x + Pad;
            float w = panel.width - 2 * Pad;
            float y = panel.y + Pad;

            GUI.Label(new Rect(x, y, w, HeaderHeight), "<b>SONG PLAYER</b>", _titleStyle);
            GUI.Label(new Rect(x, y, w, HeaderHeight), InstrumentName(_instrument), _instrumentStyle);
            y += HeaderHeight + Gap;

            DrawList(new Rect(x, y, w, listHeight));
            y += listHeight + 2 * Gap;

            DrawControls(new Rect(x, y, w, RowHeight));
            y += RowHeight + Gap;

            DrawProgress(new Rect(x, y, w, ProgressHeight));
            y += ProgressHeight + Gap;

            DrawFolderRow(new Rect(x, y, w, RowHeight));
            y += RowHeight + Gap;

            DrawStatus(new Rect(x, y, w, StatusHeight));
        }

        private void DrawList(Rect area)
        {
            if (_files.Count == 0)
            {
                GUI.Label(area, "No songs yet. Put .mid files in the songs folder, then click Refresh.", _emptyStyle);
                return;
            }

            float contentHeight = _files.Count * (RowHeight + Gap) - Gap;
            bool scrolls = contentHeight > area.height + 0.5f;
            float rowWidth = scrolls ? area.width - ScrollbarWidth - Gap : area.width;

            // Clicks are acted on after the loop, so the list doesn't change while it's being drawn.
            int play = -1;
            _scroll = GUI.BeginScrollView(area, _scroll, new Rect(0f, 0f, rowWidth, contentHeight), false, false);
            for (int i = 0; i < _files.Count; i++)
            {
                var row = new Rect(0f, i * (RowHeight + Gap), rowWidth, RowHeight);
                bool playing = _performer.IsPlaying && _files[i] == _songPath;
                string name = Path.GetFileNameWithoutExtension(_files[i]);
                if (GUI.Button(row, playing ? "► " + name : name, playing ? _playingStyle : _songStyle))
                {
                    play = i;
                }
            }
            GUI.EndScrollView();

            if (play >= 0)
            {
                PlayFile(_files[play]);
            }
        }

        private void DrawControls(Rect row)
        {
            const float stopWidth = 70f;
            List<SongPart> parts = Parts();
            string label = parts.Count > 0
                ? "Part: " + parts[Mathf.Clamp(_partIndex, 0, parts.Count - 1)].Label
                : "Part: none";

            GUI.enabled = parts.Count > 1;
            if (GUI.Button(new Rect(row.x, row.y, row.width - stopWidth - Gap, row.height), label, _buttonStyle))
            {
                NextPart();
            }
            GUI.enabled = _performer.IsPlaying;
            if (GUI.Button(new Rect(row.xMax - stopWidth, row.y, stopWidth, row.height), "Stop", _buttonStyle))
            {
                _performer.Stop();
            }
            GUI.enabled = true;
        }

        private void DrawProgress(Rect row)
        {
            if (!_performer.IsPlaying || _song == null)
            {
                GUI.Label(row, _files.Count > 0 ? "Click a song to play it" : "", _tipStyle);
                return;
            }

            const float timeWidth = 100f;
            float position = Mathf.Min(_performer.PositionMs, _song.LengthMs);
            var bar = new Rect(row.x, row.y + row.height / 2f - 3f, row.width - timeWidth - Gap, 6f);
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color32(0x7C, 0xD9, 0x92, 0xFF);
            float fraction = _song.LengthMs > 0f ? position / _song.LengthMs : 0f;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), Texture2D.whiteTexture);
            GUI.color = old;

            GUI.Label(new Rect(row.xMax - timeWidth, row.y, timeWidth, row.height),
                Clock(position) + " / " + Clock(_song.LengthMs), _timeStyle);
        }

        private void DrawFolderRow(Rect row)
        {
            float half = (row.width - Gap) / 2f;
            if (GUI.Button(new Rect(row.x, row.y, half, row.height), "Open songs folder", _buttonStyle))
            {
                OpenFolder();
            }
            if (GUI.Button(new Rect(row.xMax - half, row.y, half, row.height), "Refresh", _buttonStyle))
            {
                RefreshList();
                Status(_files.Count == 1 ? "Found 1 song" : "Found " + _files.Count + " songs");
            }
        }

        private void DrawStatus(Rect row)
        {
            if (_status != null && Time.unscaledTime < _statusUntil)
            {
                GUI.Label(row, _status, _statusStyle);
            }
            else if (_performer.IsPlaying)
            {
                GUI.Label(row, "Everyone nearby hears it. Space or Esc stands up.", _tipStyle);
            }
        }

        private static string InstrumentName(InstrumentType type)
        {
            return type == InstrumentType.Drum ? "Drums" : type.ToString();
        }

        private static string Clock(float ms)
        {
            int seconds = Mathf.FloorToInt(ms / 1000f);
            return (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }

        private void MakeStyles()
        {
            var dim = new Color(1f, 1f, 1f, 0.65f);

            _boxStyle = PanelStack.MakeStyle(TextAnchor.UpperLeft);
            _titleStyle = Label(15, Color.white, TextAnchor.MiddleLeft);
            _titleStyle.richText = true;
            _instrumentStyle = Label(13, dim, TextAnchor.MiddleRight);
            _tipStyle = Label(14, dim, TextAnchor.MiddleLeft);
            _timeStyle = Label(14, dim, TextAnchor.MiddleRight);
            _statusStyle = Label(14, new Color32(0xFF, 0xD9, 0x66, 0xFF), TextAnchor.MiddleLeft);
            _emptyStyle = Label(14, dim, TextAnchor.MiddleLeft);
            _emptyStyle.wordWrap = true;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                padding = new RectOffset(6, 6, 2, 2),
                clipping = TextClipping.Clip,
                richText = false,
            };
            // File names are shown as they are, never read as formatting tags.
            _songStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 6, 2, 2),
                richText = false,
                clipping = TextClipping.Clip,
            };
            _playingStyle = new GUIStyle(_songStyle);
            _playingStyle.normal.textColor = new Color32(0x7C, 0xD9, 0x92, 0xFF);
            _playingStyle.hover.textColor = _playingStyle.normal.textColor;
        }

        private static GUIStyle Label(int size, Color color, TextAnchor alignment)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = alignment,
                richText = false,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            style.normal.textColor = color;
            return style;
        }
    }
}
