using System;
using System.Collections.Generic;
using System.Text;
using OnTogetherMods.Shared;
using Steamworks;
using TMPro;
using UnityEngine;

namespace ChatRange
{
    // Lists the players your local chat messages reach, in a small box just above the chat box
    // (or beside it, if the chat box is dragged to the top of the screen). Shown while the chat
    // box is open on its Local tab.
    internal sealed class RangeDisplay : MonoBehaviour
    {
        private const float RefreshEvery = 0.2f;   // seconds between distance checks
        private const float Gap = 8f;              // between the chat box and this box, in 1080p units
        private const float Margin = 10f;          // from the edges of the screen
        private const string Dim = "<color=#FFFFFFAA>";

        private static Settings S => Plugin.Settings;

        // Drawn with TextMeshPro, so names keep the styling they have on name tags.
        private readonly TextBox _box = new TextBox("ChatRange", TextAlignmentOptions.TopLeft);
        private readonly List<string> _names = new List<string>();
        private readonly StringBuilder _builder = new StringBuilder();

        private TextChannelManager _channel;
        private UIManager _ui;
        private PlayerPanelController _panel;
        private float _nextLookup;
        private float _nextRefresh;

        private bool _show;
        private bool _broken;
        private string _text;
        private Rect _chat;          // the chat box on screen, in 1080p units from the top-left
        private Rect _loggedChat;

        private void OnDestroy()
        {
            _box.Destroy();
        }

        // After Update, so a chat box being dragged this frame is already where it's going.
        private void LateUpdate()
        {
            _show = false;
            try
            {
                if (!_broken && S.Enabled.Value && FindGame() && !_ui.IsMessagePanelHidden
                    && (_channel.Islocal || S.ShowOnGlobalTab.Value) && Game.ChatBounds(_ui, out _chat))
                {
                    _show = true;
                    LogChat();
                    if (_text == null || Time.unscaledTime >= _nextRefresh)
                    {
                        _nextRefresh = Time.unscaledTime + RefreshEvery;
                        _text = BuildText();
                    }
                }
            }
            catch (Exception e)
            {
                // Most likely a game update renamed something. Say so once, then stay out of the
                // way until the game restarts.
                Plugin.Log.LogError($"Stopped showing the list: {e}");
                _broken = true;
            }
            _box.LateUpdate();
        }

        private void OnGUI()
        {
            // Hidden while Photo Mode takes a photo.
            if (!_show || _text == null || PanelStack.Hidden)
            {
                return;
            }
            Vector2 size = _box.Measure(_text);
            _box.Draw(Place(size), _text);
        }

        // Just above the chat box, lined up with its left edge, so the list grows upwards as
        // names are added. If there's no room above, beside it on the right instead.
        private Rect Place(Vector2 size)
        {
            float x = _chat.xMin;
            float y = _chat.yMin - Gap - size.y;
            if (y < Margin)
            {
                x = _chat.xMax + Gap;
                y = _chat.yMin;
            }
            x = Mathf.Clamp(x, Margin, Mathf.Max(Margin, PanelStack.Width - Margin - size.x));
            y = Mathf.Clamp(y, Margin, Mathf.Max(Margin, 1080f - Margin - size.y));
            return new Rect(x, y, size.x, size.y);
        }

        private string BuildText()
        {
            Vector3 me = _channel.MainPlayer.position;
            float rangeSquared = Game.LocalRange * Game.LocalRange;
            List<PlayerController> players = _panel.PlayerControllers;

            _names.Clear();
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController player = players[i];
                if (player == null || player == _channel.MainPlayerController || player.transform == _channel.MainPlayer)
                {
                    continue;
                }
                // The same check the other player's game makes when your message arrives.
                if (Vector3.SqrMagnitude(player.transform.position - me) <= rangeSquared)
                {
                    _names.Add(NameOf(i, player));
                }
            }

            if (_names.Count == 0)
            {
                return Dim + "Nobody in local chat range</color>";
            }
            _names.Sort((a, b) => string.Compare(PlayerNames.WithoutTags(a), PlayerNames.WithoutTags(b),
                StringComparison.OrdinalIgnoreCase));

            _builder.Clear();
            _builder.Append("<size=15>").Append(Dim).Append("In local chat range</color></size>");
            int shown = Math.Min(_names.Count, S.MaxNames.Value);
            for (int i = 0; i < shown; i++)
            {
                _builder.Append('\n').Append(_names[i]);
            }
            if (_names.Count > shown)
            {
                _builder.Append('\n').Append(Dim).Append("and ").Append(_names.Count - shown).Append(" more</color>");
            }
            return _builder.ToString();
        }

        // The name on their name tag, colours and all. Falls back to their Steam name, which the
        // game keeps next to the name tag (PlayerPanelController.PlayerSteamIDs).
        private string NameOf(int index, PlayerController player)
        {
            string name = PlayerNames.Of(player);
            if (name != null)
            {
                return name;
            }
            List<string> ids = _panel.PlayerSteamIDs;
            if (ids != null && index < ids.Count && ulong.TryParse(ids[index], out ulong id))
            {
                try
                {
                    string steamName = SteamFriends.GetFriendPersonaName(new CSteamID(id));
                    if (!string.IsNullOrWhiteSpace(steamName))
                    {
                        return PlayerNames.Plain(steamName);
                    }
                }
                catch (Exception)
                {
                    // Steam isn't running.
                }
            }
            return Dim + "someone</color>";
        }

        // The game objects this needs only exist in a lobby. Their .I lookups search the whole
        // scene when the object is missing (as on the main menu), so that's tried once a second.
        private bool FindGame()
        {
            if (_channel == null || _ui == null || _panel == null)
            {
                if (Time.unscaledTime < _nextLookup)
                {
                    return false;
                }
                _nextLookup = Time.unscaledTime + 1f;
                _channel = NetworkSingleton<TextChannelManager>.I;
                _ui = MonoSingleton<UIManager>.I;
                _panel = NetworkSingleton<PlayerPanelController>.I;
                if (_channel == null || _ui == null || _panel == null)
                {
                    return false;
                }
            }
            return _channel.MainPlayer != null && _panel.PlayerControllers != null;
        }

        private void LogChat()
        {
            if (!S.DebugLog.Value || (Mathf.Abs(_chat.x - _loggedChat.x) < 1f && Mathf.Abs(_chat.y - _loggedChat.y) < 1f
                && Mathf.Abs(_chat.width - _loggedChat.width) < 1f && Mathf.Abs(_chat.height - _loggedChat.height) < 1f))
            {
                return;
            }
            _loggedChat = _chat;
            Plugin.Log.LogInfo($"Chat box at x {_chat.x:0}, y {_chat.y:0}, {_chat.width:0} x {_chat.height:0} " +
                $"(1080p units from the top-left, screen {Screen.width} x {Screen.height}).");
        }
    }
}
