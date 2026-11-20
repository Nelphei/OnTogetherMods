using System;
using System.Text;
using Steamworks;
using UnityEngine;

namespace CardTable
{
    // How Card Table's games talk to each other over Steam.
    //
    // Lobby chat, to everyone in the session with Card Table: which Go Fish tables there are. The
    // game never sends or reads lobby chat (its chat box goes through PurrNet), so players without
    // the mod never see these, the same as Emote Sync, Desk Swarm and Buddy Up (each with its own tag).
    //
    // Steam P2P, to one player: joining, asking, and what each player may see, so a player's cards
    // only ever go to them. On its own channel: the game uses channels 0 and 1 for join requests
    // (see reference/Assembly-CSharp/LobbyRequestManager.cs) and never reads any other.
    internal sealed class CardNet : ICardNet
    {
        private const string Tag = "OTCT1";
        private const int Channel = 4207;
        private const int MaxPacketsPerFrame = 64;

        private Callback<LobbyChatMsg_t> _chatMsg;
        private Callback<LobbyChatUpdate_t> _chatUpdate;
        private Callback<P2PSessionRequest_t> _sessionRequest;
        private Callback<P2PSessionConnectFail_t> _connectFail;
        private readonly byte[] _chatBuffer = new byte[4096];
        private byte[] _packet = new byte[8192];
        private float _nextPoll;
        private CSteamID _lobby = CSteamID.Nil;
        private ulong _me;

        // Lobby chat from another player: sender and the words after the tag.
        public event Action<ulong, string[]> Broadcasted;

        // A P2P message from another player.
        public event Action<ulong, string[]> Direct;

        public event Action<ulong> MemberLeft;

        // You joined or left a session; true if you were in one before.
        public event Action<bool> LobbyChanged;

        private static bool DebugLog => Plugin.Settings.DebugLog.Value;

        public ulong Me
        {
            get
            {
                if (_me == 0)
                {
                    try
                    {
                        _me = SteamUser.GetSteamID().m_SteamID;
                    }
                    catch (Exception)
                    {
                        // Steam isn't up yet.
                    }
                }
                // Never 0, which means a computer player at the table.
                return _me != 0 ? _me : 1;
            }
        }

        public bool InLobby => _lobby != CSteamID.Nil;

        public void Update()
        {
            Poll();
            if (InLobby)
            {
                ReadPackets();
            }
        }

        public void Broadcast(string message)
        {
            if (!InLobby)
            {
                return;
            }
            byte[] bytes = Encoding.ASCII.GetBytes(Tag + " " + message);
            if (!SteamMatchmaking.SendLobbyChatMsg(_lobby, bytes, bytes.Length))
            {
                Plugin.Log.LogWarning($"Steam didn't send \"{message}\" to the session.");
            }
            else if (DebugLog)
            {
                Plugin.Log.LogInfo($"Sent to all: {message}");
            }
        }

        public void SendTo(ulong player, string message)
        {
            if (!InLobby || player == 0)
            {
                return;
            }
            byte[] bytes = Encoding.ASCII.GetBytes(Tag + " " + message);
            if (!SteamNetworking.SendP2PPacket(new CSteamID(player), bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, Channel))
            {
                Plugin.Log.LogWarning($"Steam didn't send \"{message}\" to {NameOf(player)}.");
            }
            else if (DebugLog)
            {
                Plugin.Log.LogInfo($"Sent to {NameOf(player)}: {message}");
            }
        }

        public static string NameOf(ulong id)
        {
            try
            {
                return SteamFriends.GetFriendPersonaName(new CSteamID(id));
            }
            catch (Exception)
            {
                return id.ToString();
            }
        }

        // Checks once a second which Steam lobby we're in.
        private void Poll()
        {
            if (Time.unscaledTime < _nextPoll)
            {
                return;
            }
            _nextPoll = Time.unscaledTime + 1f;

            CSteamID lobby = ReadLobby();
            if (lobby == _lobby)
            {
                return;
            }
            bool hadLobby = InLobby;
            _lobby = lobby;
            if (InLobby && _chatMsg == null)
            {
                _chatMsg = Callback<LobbyChatMsg_t>.Create(OnChatMsg);
                _chatUpdate = Callback<LobbyChatUpdate_t>.Create(OnChatUpdate);
                _sessionRequest = Callback<P2PSessionRequest_t>.Create(OnSessionRequest);
                _connectFail = Callback<P2PSessionConnectFail_t>.Create(OnConnectFail);
                // The game does this too, but only once its join-request manager has started.
                SteamNetworking.AllowP2PPacketRelay(true);
            }
            if (DebugLog)
            {
                Plugin.Log.LogInfo(InLobby ? $"In lobby {_lobby}." : "Left the lobby.");
            }
            LobbyChanged?.Invoke(hadLobby);
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

        private bool IsMember(CSteamID player)
        {
            int count = SteamMatchmaking.GetNumLobbyMembers(_lobby);
            for (int i = 0; i < count; i++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(_lobby, i) == player)
                {
                    return true;
                }
            }
            return false;
        }

        private void ReadPackets()
        {
            for (int i = 0; i < MaxPacketsPerFrame && SteamNetworking.IsP2PPacketAvailable(out uint size, Channel); i++)
            {
                if (size > _packet.Length)
                {
                    _packet = new byte[size];
                }
                if (!SteamNetworking.ReadP2PPacket(_packet, size, out uint read, out CSteamID from, Channel))
                {
                    break;
                }
                Handle(from, _packet, (int)read, Direct, "from");
            }
        }

        private void OnChatMsg(LobbyChatMsg_t msg)
        {
            if (msg.m_ulSteamIDLobby != _lobby.m_SteamID)
            {
                return;
            }
            int length = SteamMatchmaking.GetLobbyChatEntry(_lobby, (int)msg.m_iChatID, out CSteamID sender,
                _chatBuffer, _chatBuffer.Length, out EChatEntryType _);
            // Steam echoes our own messages back to us.
            if (length > 0 && sender.m_SteamID != Me)
            {
                Handle(sender, _chatBuffer, length, Broadcasted, "to all from");
            }
        }

        private void Handle(CSteamID sender, byte[] bytes, int length, Action<ulong, string[]> handler, string how)
        {
            string text = Encoding.ASCII.GetString(bytes, 0, length).TrimEnd('\0');
            string[] words = text.Split(' ');
            if (words.Length < 2 || words[0] != Tag)
            {
                return;
            }
            if (DebugLog)
            {
                Plugin.Log.LogInfo($"Got {how} {NameOf(sender.m_SteamID)}: {text.Substring(Tag.Length + 1)}");
            }
            try
            {
                var rest = new string[words.Length - 1];
                Array.Copy(words, 1, rest, 0, rest.Length);
                handler?.Invoke(sender.m_SteamID, rest);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to handle \"{text}\" from {NameOf(sender.m_SteamID)}: {e}");
            }
        }

        private void OnChatUpdate(LobbyChatUpdate_t update)
        {
            const uint gone = (uint)(EChatMemberStateChange.k_EChatMemberStateChangeLeft |
                EChatMemberStateChange.k_EChatMemberStateChangeDisconnected |
                EChatMemberStateChange.k_EChatMemberStateChangeKicked |
                EChatMemberStateChange.k_EChatMemberStateChangeBanned);

            if (update.m_ulSteamIDLobby == _lobby.m_SteamID && (update.m_rgfChatMemberStateChange & gone) != 0)
            {
                MemberLeft?.Invoke(update.m_ulSteamIDUserChanged);
            }
        }

        // Steam asks before the first message from a player gets through. The game accepts everyone
        // (LobbyRequestManager); this only accepts players in your session.
        private void OnSessionRequest(P2PSessionRequest_t request)
        {
            if (InLobby && IsMember(request.m_steamIDRemote))
            {
                SteamNetworking.AcceptP2PSessionWithUser(request.m_steamIDRemote);
            }
        }

        private void OnConnectFail(P2PSessionConnectFail_t fail)
        {
            Plugin.Log.LogWarning($"Couldn't reach {NameOf(fail.m_steamIDRemote.m_SteamID)} through Steam ({(EP2PSessionError)fail.m_eP2PSessionError}).");
        }
    }
}
