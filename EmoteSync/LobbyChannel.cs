using System;
using System.Text;
using Steamworks;
using UnityEngine;

namespace EmoteSync
{
    // Talks to other modded players through Steam lobby chat. The game never sends or reads lobby
    // chat (its chat box goes through PurrNet), so players without the mod never see these messages.
    internal sealed class LobbyChannel
    {
        private const string Tag = "OTES1";

        private Callback<LobbyChatMsg_t> _chatMsg;
        private Callback<LobbyChatUpdate_t> _chatUpdate;
        private readonly byte[] _buffer = new byte[512];
        private float _nextPoll;

        public CSteamID Lobby { get; private set; } = CSteamID.Nil;

        public CSteamID Me { get; private set; } = CSteamID.Nil;

        public bool InLobby => Lobby != CSteamID.Nil;

        // Sender and the words after the tag, e.g. ["GO", "3", "51234"].
        public event Action<CSteamID, string[]> MessageReceived;

        public event Action<CSteamID> MemberLeft;

        public event Action LobbyChanged;

        // Checks once a second which Steam lobby we're in.
        public void Poll()
        {
            if (Time.unscaledTime < _nextPoll)
            {
                return;
            }
            _nextPoll = Time.unscaledTime + 1f;

            CSteamID lobby = ReadLobby();
            if (lobby == Lobby)
            {
                return;
            }

            Lobby = lobby;
            if (InLobby)
            {
                Me = SteamUser.GetSteamID();
                if (_chatMsg == null)
                {
                    _chatMsg = Callback<LobbyChatMsg_t>.Create(OnChatMsg);
                    _chatUpdate = Callback<LobbyChatUpdate_t>.Create(OnChatUpdate);
                }
            }
            Plugin.Log.LogInfo(InLobby ? $"In lobby {Lobby}." : "Left the lobby.");
            LobbyChanged?.Invoke();
        }

        public bool Send(string message)
        {
            if (!InLobby)
            {
                return false;
            }
            byte[] bytes = Encoding.ASCII.GetBytes(Tag + " " + message);
            bool sent = SteamMatchmaking.SendLobbyChatMsg(Lobby, bytes, bytes.Length);
            if (!sent)
            {
                Plugin.Log.LogWarning($"Steam didn't send \"{message}\".");
            }
            return sent;
        }

        public static string NameOf(CSteamID id)
        {
            try
            {
                return SteamFriends.GetFriendPersonaName(id);
            }
            catch (Exception)
            {
                return id.ToString();
            }
        }

        private static CSteamID ReadLobby()
        {
            try
            {
                // MonoSingleton.I searches the scene when the instance is missing, which is why
                // this only runs once a second.
                MultiplayerManager mm = MonoSingleton<MultiplayerManager>.I;
                if (mm == null || !ulong.TryParse(mm.LobbyCode, out ulong id) || id == 0)
                {
                    return CSteamID.Nil;
                }
                var lobby = new CSteamID(id);
                // Steam reports 0 members for a lobby we aren't in.
                return SteamMatchmaking.GetNumLobbyMembers(lobby) > 0 ? lobby : CSteamID.Nil;
            }
            catch (Exception)
            {
                // No lobby object yet, or Steam isn't running.
                return CSteamID.Nil;
            }
        }

        private void OnChatMsg(LobbyChatMsg_t msg)
        {
            if (msg.m_ulSteamIDLobby != Lobby.m_SteamID)
            {
                return;
            }

            int length = SteamMatchmaking.GetLobbyChatEntry(Lobby, (int)msg.m_iChatID, out CSteamID sender,
                _buffer, _buffer.Length, out EChatEntryType _);
            // Steam echoes our own messages back to us.
            if (length <= 0 || sender == Me)
            {
                return;
            }

            string text = Encoding.ASCII.GetString(_buffer, 0, length).TrimEnd('\0');
            string[] words = text.Split(' ');
            if (words.Length < 2 || words[0] != Tag)
            {
                return;
            }

            try
            {
                MessageReceived?.Invoke(sender, words.AsSpan(1).ToArray());
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to handle \"{text}\" from {NameOf(sender)}: {e}");
            }
        }

        private void OnChatUpdate(LobbyChatUpdate_t update)
        {
            const uint gone = (uint)(EChatMemberStateChange.k_EChatMemberStateChangeLeft |
                EChatMemberStateChange.k_EChatMemberStateChangeDisconnected |
                EChatMemberStateChange.k_EChatMemberStateChangeKicked |
                EChatMemberStateChange.k_EChatMemberStateChangeBanned);

            if (update.m_ulSteamIDLobby == Lobby.m_SteamID && (update.m_rgfChatMemberStateChange & gone) != 0)
            {
                MemberLeft?.Invoke(new CSteamID(update.m_ulSteamIDUserChanged));
            }
        }
    }
}
