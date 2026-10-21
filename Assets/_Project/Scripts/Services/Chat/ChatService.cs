using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MafiaNight.Core;
using PlayFab;
using UnityEngine;
using UnityEngine.Networking;

namespace MafiaNight.Services.Chat
{
    /// <summary>
    /// Text chat on our own server (Server/rewards-api, "chat" part): the public square, direct messages and groups.
    /// It keeps one long-poll request open to hear new messages, and keeps the lists in memory for the screens.
    /// Create it with <see cref="Ensure"/>; it lives across scenes.
    /// </summary>
    public sealed class ChatService : MonoBehaviour
    {
        private const string Tag = "Chat";
        public const string Public = "public";
        private const int KeepPerChannel = 200;
        private const int PollWaitSeconds = 20;

        public static ChatService Instance { get; private set; }

        private readonly Dictionary<string, List<ChatMsg>> messages = new Dictionary<string, List<ChatMsg>>();
        private readonly List<ChatConv> conversations = new List<ChatConv>();
        private readonly HashSet<string> blocked = new HashSet<string>();
        private int cursor;
        private bool loadedHistory;
        private string openChannel = "";
        private int lastUnread = -1;

        public string MyPf => PlayFabSettings.staticPlayer?.PlayFabId ?? "";
        public IReadOnlyList<ChatConv> Conversations => conversations;
        public bool Connected { get; private set; }

        /// <summary>A message arrived (or was sent by this player).</summary>
        public event Action<ChatMsg> OnMessage;
        /// <summary>The list of conversations changed (a new one, new unread counts, online marks).</summary>
        public event Action OnConversationsChanged;
        /// <summary>Total unread direct and group messages.</summary>
        public event Action<int> OnUnreadChanged;

        public static ChatService Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("ChatService");
            DontDestroyOnLoad(go);
            return go.AddComponent<ChatService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start() { if (Instance == this) StartCoroutine(PollLoop()); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private static string Base => (GameConstants.ApiUrl ?? "").TrimEnd('/');
        private static string Ticket => PlayFabSettings.staticPlayer?.ClientSessionTicket;

        public int UnreadTotal => conversations.Sum(c => c.Unread);

        public IReadOnlyList<ChatMsg> Messages(string channel) =>
            messages.TryGetValue(channel, out var l) ? (IReadOnlyList<ChatMsg>)l : new ChatMsg[0];

        public bool IsBlocked(string pf) => blocked.Contains(pf);

        /// <summary>The channel of a direct conversation with this player (the same rule as the server's).</summary>
        public string DirectChannel(string otherPf)
        {
            string a = MyPf, b = otherPf;
            return string.CompareOrdinal(a, b) <= 0 ? "dm:" + a + ":" + b : "dm:" + b + ":" + a;
        }

        /// <summary>Finds the direct conversation with a player, making an empty one in the list if it does not exist yet.</summary>
        public ChatConv EnsureDirect(string otherPf, string name, int rank)
        {
            string ch = DirectChannel(otherPf);
            var c = conversations.FirstOrDefault(x => x.Channel == ch);
            if (c != null) return c;
            c = new ChatConv { Channel = ch, OtherPf = otherPf, Name = name, Rank = rank };
            conversations.Insert(0, c);
            OnConversationsChanged?.Invoke();
            return c;
        }

        // ---------------------------------------------------------------- the polling loop

        private IEnumerator PollLoop()
        {
            int failures = 0;
            while (true)
            {
                if (string.IsNullOrEmpty(Ticket) || string.IsNullOrEmpty(Base)) { Connected = false; yield return new WaitForSecondsRealtime(2f); continue; }
                string url = Base + "/v1/chat/poll?since=" + cursor + "&wait=" + PollWaitSeconds + (loadedHistory ? "" : "&history=1");
                using (var req = UnityWebRequest.Get(url))
                {
                    req.SetRequestHeader("X-Authorization", Ticket);
                    req.timeout = PollWaitSeconds + 15;
                    yield return req.SendWebRequest();
                    PollDto dto = null;
                    if (req.result == UnityWebRequest.Result.Success) { try { dto = JsonUtility.FromJson<PollDto>(req.downloadHandler.text); } catch { /* not json */ } }
                    if (dto != null && dto.ok)
                    {
                        failures = 0; Connected = true;
                        bool first = !loadedHistory;
                        loadedHistory = true;
                        cursor = Mathf.Max(cursor, dto.cursor);
                        if (dto.messages != null) foreach (var m in dto.messages) Receive(m, !first);
                        if (first || (dto.messages != null && dto.messages.Count > 0)) yield return RefreshInboxRoutine();
                        continue;
                    }
                    Connected = false;
                    failures++;
                    if (req.responseCode == 401) { yield return new WaitForSecondsRealtime(15f); continue; }
                }
                yield return new WaitForSecondsRealtime(Mathf.Min(30f, Mathf.Pow(2f, Mathf.Min(failures, 5))));
            }
        }

        private void Receive(MsgDto d, bool announce)
        {
            if (d == null) return;
            var list = ListFor(d.ch);
            if (list.Any(x => x.Id == d.id)) return;                 // already shown (our own message comes back through the poll)
            var m = new ChatMsg { Id = d.id, Channel = d.ch, FromPf = d.from, Name = d.name, Text = d.text, Rank = d.rank, Ts = d.ts, System = d.sys == 1, Mine = d.from == MyPf };
            Add(list, m);
            if (d.ch != Public && !m.Mine && !m.System && d.ch != openChannel)
            {
                var c = conversations.FirstOrDefault(x => x.Channel == d.ch);
                if (c != null) { c.Unread++; c.Last = m; }
            }
            OnMessage?.Invoke(m);
            if (announce) RaiseUnread();
        }

        private List<ChatMsg> ListFor(string ch)
        {
            if (!messages.TryGetValue(ch, out var l)) messages[ch] = l = new List<ChatMsg>();
            return l;
        }

        private static void Add(List<ChatMsg> list, ChatMsg m)
        {
            int i = list.Count;
            while (i > 0 && list[i - 1].Id > m.Id && m.Id > 0) i--;
            list.Insert(i, m);
            if (list.Count > KeepPerChannel) list.RemoveRange(0, list.Count - KeepPerChannel);
        }

        private void RaiseUnread()
        {
            int total = UnreadTotal;
            if (total == lastUnread) return;
            lastUnread = total;
            OnUnreadChanged?.Invoke(total);
        }

        // ---------------------------------------------------------------- the list of conversations

        public void RefreshInbox() { StartCoroutine(RefreshInboxRoutine()); }

        private IEnumerator RefreshInboxRoutine()
        {
            if (string.IsNullOrEmpty(Ticket) || string.IsNullOrEmpty(Base)) yield break;
            using (var req = UnityWebRequest.Get(Base + "/v1/chat/inbox"))
            {
                req.SetRequestHeader("X-Authorization", Ticket); req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) yield break;
                InboxDto dto = null; try { dto = JsonUtility.FromJson<InboxDto>(req.downloadHandler.text); } catch { }
                if (dto == null || !dto.ok || dto.conversations == null) yield break;
                var fresh = new List<ChatConv>();
                foreach (var d in dto.conversations)
                {
                    var old = conversations.FirstOrDefault(x => x.Channel == d.ch);
                    var c = new ChatConv
                    {
                        Channel = d.ch, IsGroup = d.kind == "group", Name = d.name, OtherPf = d.other, Rank = d.rank, Online = d.online, Owner = d.owner,
                        Unread = d.ch == openChannel ? 0 : d.unread,
                        Last = d.last != null && d.last.id > 0 ? new ChatMsg { Id = d.last.id, Channel = d.last.ch, FromPf = d.last.from, Name = d.last.name, Text = d.last.text, Ts = d.last.ts, System = d.last.sys == 1, Mine = d.last.from == MyPf } : (old != null ? old.Last : null),
                    };
                    if (d.members != null) foreach (var m in d.members) c.Members.Add(new KeyValuePair<string, string>(m.pf, m.name));
                    fresh.Add(c);
                    if (!messages.ContainsKey(d.ch) && c.Last != null) ListFor(d.ch);
                }
                // conversations opened locally but without messages yet stay in the list
                foreach (var old in conversations) if (!old.IsGroup && fresh.All(f => f.Channel != old.Channel)) fresh.Add(old);
                conversations.Clear(); conversations.AddRange(fresh);
                OnConversationsChanged?.Invoke();
                RaiseUnread();
            }
            yield return LoadBlocked();
        }

        private IEnumerator LoadBlocked()
        {
            using (var req = UnityWebRequest.Get(Base + "/v1/chat/blocked"))
            {
                req.SetRequestHeader("X-Authorization", Ticket); req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) yield break;
                BlockedDto dto = null; try { dto = JsonUtility.FromJson<BlockedDto>(req.downloadHandler.text); } catch { }
                if (dto == null || !dto.ok || dto.blocked == null) yield break;
                blocked.Clear(); foreach (var b in dto.blocked) blocked.Add(b);
            }
        }

        /// <summary>The screen opened a conversation: its messages count as read.</summary>
        public void Open(string channel)
        {
            openChannel = channel ?? "";
            if (string.IsNullOrEmpty(channel) || channel == Public) return;
            var c = conversations.FirstOrDefault(x => x.Channel == channel);
            if (c != null && c.Unread > 0) { c.Unread = 0; OnConversationsChanged?.Invoke(); RaiseUnread(); }
            var list = Messages(channel);
            int upTo = list.Count > 0 ? list[list.Count - 1].Id : 0;
            if (upTo > 0) StartCoroutine(Post("/v1/chat/read", "{\"channel\":\"" + Esc(channel) + "\",\"upTo\":" + upTo + "}", null));
        }

        public void Close() { openChannel = ""; }

        // ---------------------------------------------------------------- sending and actions

        public void Send(string channel, string text, Action<ChatSendResult, int> done)
        {
            string body = "{\"channel\":\"" + Esc(channel) + "\",\"text\":\"" + Esc(text) + "\"}";
            StartCoroutine(Post("/v1/chat/send", body, (code, json) =>
            {
                SendDto d = null; try { d = JsonUtility.FromJson<SendDto>(json); } catch { }
                if (d == null) { done?.Invoke(ChatSendResult.Failed, 0); return; }
                if (d.ok)
                {
                    string ch = string.IsNullOrEmpty(d.ch) ? channel : d.ch;
                    var list = ListFor(ch);
                    if (d.id == 0 || !list.Any(x => x.Id == d.id))
                    {
                        var me = new ChatMsg { Id = d.id == 0 ? -(int)(DateTime.UtcNow.Ticks % 1000000000) : d.id, Channel = ch, FromPf = MyPf, Name = "me", Text = d.text, Ts = d.ts > 0 ? d.ts : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Mine = true };
                        Add(list, me);
                        OnMessage?.Invoke(me);
                    }
                    if (ch != Public) TouchConversation(ch);
                    done?.Invoke(ChatSendResult.Ok, 0);
                    return;
                }
                done?.Invoke(d.error == "empty" ? ChatSendResult.Empty : d.error == "slow" ? ChatSendResult.Slow : d.error == "muted" ? ChatSendResult.Muted
                    : d.error == "you_blocked" ? ChatSendResult.YouBlocked : d.error == "not_member" ? ChatSendResult.NotMember : ChatSendResult.Failed, d.retryMs);
            }));
        }

        private void TouchConversation(string ch)
        {
            var c = conversations.FirstOrDefault(x => x.Channel == ch);
            var list = Messages(ch);
            if (c != null && list.Count > 0) { c.Last = list[list.Count - 1]; OnConversationsChanged?.Invoke(); }
            else RefreshInbox();
        }

        public void CreateGroup(string name, IEnumerable<string> memberPfs, Action<string> done)
        {
            var sb = new StringBuilder("{\"name\":\"" + Esc(name) + "\",\"members\":[");
            bool first = true; foreach (var m in memberPfs) { if (!first) sb.Append(','); first = false; sb.Append('"').Append(Esc(m)).Append('"'); }
            sb.Append("]}");
            StartCoroutine(Post("/v1/chat/group/create", sb.ToString(), (code, json) =>
            {
                GroupDto d = null; try { d = JsonUtility.FromJson<GroupDto>(json); } catch { }
                if (d != null && d.ok) { RefreshInbox(); done?.Invoke(d.ch); } else done?.Invoke(null);
            }));
        }

        public void LeaveGroup(string channel, Action<bool> done)
        {
            string id = channel.StartsWith("grp:") ? channel.Substring(4) : channel;
            StartCoroutine(Post("/v1/chat/group/leave", "{\"id\":\"" + Esc(id) + "\"}", (code, json) =>
            {
                bool ok = code == 200;
                if (ok) { conversations.RemoveAll(c => c.Channel == channel); messages.Remove(channel); OnConversationsChanged?.Invoke(); RaiseUnread(); }
                done?.Invoke(ok);
            }));
        }

        public void Report(ChatMsg m, string reason, Action<bool> done)
        {
            string body = "{\"channel\":\"" + Esc(m.Channel) + "\",\"messageId\":" + m.Id + ",\"reason\":\"" + Esc(reason ?? "") + "\"}";
            StartCoroutine(Post("/v1/chat/report", body, (code, json) => done?.Invoke(code == 200)));
        }

        public void Block(string pf, bool on, Action<bool> done)
        {
            StartCoroutine(Post("/v1/chat/block", "{\"player\":\"" + Esc(pf) + "\",\"on\":" + (on ? "true" : "false") + "}", (code, json) =>
            {
                bool ok = code == 200;
                if (ok) { if (on) blocked.Add(pf); else blocked.Remove(pf); }
                done?.Invoke(ok);
            }));
        }

        private IEnumerator Post(string path, string json, Action<long, string> done)
        {
            if (string.IsNullOrEmpty(Ticket) || string.IsNullOrEmpty(Base)) { done?.Invoke(0, ""); yield break; }
            using (var req = new UnityWebRequest(Base + path, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Authorization", Ticket);
                req.timeout = 15;
                yield return req.SendWebRequest();
                done?.Invoke(req.responseCode, req.downloadHandler.text);
            }
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append(' '); break;
                    default: if (ch < ' ') sb.Append(' '); else sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }
    }
}
