using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace SessionManager
{
    internal sealed class JoinRequest
    {
        public CSteamID Id;
        // As the player sent it, ready for TextMeshPro.
        public string Name;
        // UTC.
        public DateTime Asked;
        // The name came with the request (not looked up on Steam).
        public bool Named;
    }

    internal enum Answer
    {
        Wait,
        Accept,
        Reject,
    }

    // Requests to join a session with "ask to join" on. The asking player's game sends the host's game
    // "JOIN-<name>" over Steam, and the host's game queues them and shows one at a time in a popup
    // (Accept or Reject). This shows the whole queue, answers any request in it, and answers by itself
    // when a rule says so. See reference/Assembly-CSharp/LobbyRequestManager.cs and UIManager.cs
    // (OpenRequestUI, AcceptLobbyRequest, RejectLobbyRequest).
    internal static class JoinRequests
    {
        private const string JoinPrefix = "JOIN-";

        private static readonly AccessTools.FieldRef<LobbyRequestManager, List<CSteamID>> Pending =
            AccessTools.FieldRefAccess<LobbyRequestManager, List<CSteamID>>("_pendingRequester");
        private static readonly AccessTools.FieldRef<LobbyRequestManager, List<string>> Messages =
            AccessTools.FieldRefAccess<LobbyRequestManager, List<string>>("_requestMessage");
        // The one shown in the game's popup.
        private static readonly AccessTools.FieldRef<LobbyRequestManager, CSteamID> Current =
            AccessTools.FieldRefAccess<LobbyRequestManager, CSteamID>("_currentRequester");
        private static readonly AccessTools.FieldRef<UIManager, GameObject> Popup =
            AccessTools.FieldRefAccess<UIManager, GameObject>("_requestUI");

        private static readonly Dictionary<ulong, JoinRequest> Known = new Dictionary<ulong, JoinRequest>();
        private static readonly List<CSteamID> Ids = new List<CSteamID>();
        private static readonly List<CSteamID> RuleIds = new List<CSteamID>();

        // Decides what to do with each waiting request, every frame. Set by the window while you host;
        // null leaves every request to you.
        public static Func<CSteamID, Answer> Rule;

        // A request answered by a rule: the player's name and the answer.
        public static event Action<string, Answer> Answered;

        // Everyone waiting, the one in the game's popup first.
        public static void Waiting(List<JoinRequest> requests)
        {
            requests.Clear();
            LobbyRequestManager manager = MonoSingleton<LobbyRequestManager>.I;
            if (manager == null)
            {
                return;
            }
            WaitingIds(manager, Ids);
            foreach (CSteamID id in Ids)
            {
                requests.Add(Remember(id, null));
            }
            // Forget the ones that are no longer waiting.
            if (Known.Count > requests.Count)
            {
                var keep = new HashSet<ulong>();
                foreach (CSteamID id in Ids)
                {
                    keep.Add(id.m_SteamID);
                }
                var gone = new List<ulong>();
                foreach (ulong key in Known.Keys)
                {
                    if (!keep.Contains(key))
                    {
                        gone.Add(key);
                    }
                }
                foreach (ulong key in gone)
                {
                    Known.Remove(key);
                }
            }
        }

        public static bool Accept(CSteamID id)
        {
            LobbyRequestManager manager = MonoSingleton<LobbyRequestManager>.I;
            return manager != null && Accept(manager, id);
        }

        // "Host decision": the asking player sees that the host turned them down.
        public static bool Reject(CSteamID id)
        {
            LobbyRequestManager manager = MonoSingleton<LobbyRequestManager>.I;
            return manager != null && Reject(manager, id);
        }

        // ---- Called by the patches ----

        // The game is about to show this player's request in its popup. The popup's name is cut short
        // at the first dash, but the game takes the request out of its queue only after this, so the
        // whole message is still at the front of it.
        public static void PopupOpening(LobbyRequestManager manager, string name)
        {
            CSteamID id = Current(manager);
            if (id == CSteamID.Nil)
            {
                return;
            }
            List<CSteamID> pending = Pending(manager);
            List<string> messages = Messages(manager);
            string full = pending != null && messages != null && pending.Count > 0 && messages.Count > 0 && pending[0] == id
                ? NameIn(messages[0])
                : null;
            if (full != null)
            {
                Remember(id, full);
            }
            else if (!(Known.TryGetValue(id.m_SteamID, out JoinRequest known) && known.Named))
            {
                Remember(id, name);
            }
        }

        // After the game's LobbyRequestManager.Update, which reads new requests and moves the next
        // one into its popup.
        public static void AfterUpdate(LobbyRequestManager manager)
        {
            List<CSteamID> pending = Pending(manager);
            if (Current(manager) == CSteamID.Nil && (pending == null || pending.Count == 0))
            {
                return;
            }
            List<string> messages = Messages(manager);
            if (pending != null && messages != null)
            {
                for (int i = 0; i < pending.Count && i < messages.Count; i++)
                {
                    Remember(pending[i], NameIn(messages[i]));
                }
            }

            Func<CSteamID, Answer> rule = Rule;
            if (rule == null)
            {
                return;
            }
            // A copy, since answering takes requests out of the game's queue.
            WaitingIds(manager, RuleIds);
            foreach (CSteamID id in RuleIds)
            {
                Answer answer = rule(id);
                if (answer == Answer.Wait)
                {
                    continue;
                }
                string name = Remember(id, null).Name;
                bool done = answer == Answer.Accept ? Accept(manager, id) : Reject(manager, id);
                if (done)
                {
                    Answered?.Invoke(name, answer);
                }
            }
        }

        // ---- Inside ----

        private static void WaitingIds(LobbyRequestManager manager, List<CSteamID> ids)
        {
            ids.Clear();
            CSteamID current = Current(manager);
            if (current != CSteamID.Nil)
            {
                ids.Add(current);
            }
            List<CSteamID> pending = Pending(manager);
            if (pending != null)
            {
                foreach (CSteamID id in pending)
                {
                    if (id != CSteamID.Nil && !ids.Contains(id))
                    {
                        ids.Add(id);
                    }
                }
            }
        }

        private static JoinRequest Remember(CSteamID id, string name)
        {
            if (!Known.TryGetValue(id.m_SteamID, out JoinRequest request))
            {
                request = new JoinRequest { Id = id, Asked = DateTime.UtcNow };
                Known[id.m_SteamID] = request;
            }
            if (!string.IsNullOrWhiteSpace(name))
            {
                request.Name = Game.Styled(Game.Filter(name));
                request.Named = true;
            }
            else if (request.Name == null)
            {
                request.Name = SteamManager.Initialized ? Game.Styled(SteamFriends.GetFriendPersonaName(id)) : "Player";
            }
            return request;
        }

        // "JOIN-<name>". The game splits it on '-', which cuts names with a dash in them short.
        private static string NameIn(string message)
        {
            return message != null && message.StartsWith(JoinPrefix, StringComparison.Ordinal)
                ? message.Substring(JoinPrefix.Length)
                : null;
        }

        private static bool Accept(LobbyRequestManager manager, CSteamID id)
        {
            bool done;
            if (Current(manager) == id)
            {
                // What the popup's Accept button does.
                manager.AcceptRequester();
                ClosePopup();
                done = true;
            }
            else
            {
                done = Unqueue(manager, id, "ACCEPT");
            }
            if (done)
            {
                Known.Remove(id.m_SteamID);
                Log($"Accepted a join request from {id}");
            }
            return done;
        }

        private static bool Reject(LobbyRequestManager manager, CSteamID id)
        {
            bool done;
            if (Current(manager) == id)
            {
                // What the popup's Reject button does.
                manager.RejectRequester(RejectReason.HostDecision);
                ClosePopup();
                done = true;
            }
            else
            {
                done = Unqueue(manager, id, $"REJECT {RejectReason.HostDecision}");
            }
            if (done)
            {
                Known.Remove(id.m_SteamID);
                Log($"Turned down a join request from {id}");
            }
            return done;
        }

        // Answers a request that's still waiting behind the popup: sends the same message the game's
        // AcceptRequester / RejectRequester would, and takes it out of the game's queue.
        private static bool Unqueue(LobbyRequestManager manager, CSteamID id, string text)
        {
            List<CSteamID> pending = Pending(manager);
            int i = pending != null ? pending.IndexOf(id) : -1;
            if (i < 0)
            {
                return false;
            }
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            SteamNetworking.SendP2PPacket(id, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, 1);
            pending.RemoveAt(i);
            List<string> messages = Messages(manager);
            if (messages != null && i < messages.Count)
            {
                messages.RemoveAt(i);
            }
            return true;
        }

        private static void ClosePopup()
        {
            UIManager ui = MonoSingleton<UIManager>.I;
            GameObject popup = ui != null ? Popup(ui) : null;
            if (popup != null)
            {
                popup.SetActive(false);
            }
        }

        private static void Log(string text)
        {
            if (Plugin.Settings.DebugLog.Value)
            {
                Plugin.Log.LogInfo(text);
            }
        }
    }
}
