using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.Services.Chat;
using MafiaNight.Services.Rooms;
using PlayFab;
using UnityEngine;
using UnityEngine.Networking;

namespace MafiaNight.Services.Friends
{
    /// <summary>A friend, or somebody who asked to be one.</summary>
    public sealed class Friend
    {
        public string Pf, Num, Name;
        public int Rank, Level;
        public bool Online;
        public RoomRef Room;            // the room he sits in right now (null when none, or offline)
        public long Seen;               // last time he was online, unix ms
        public long At;                 // requests: when it was sent
    }

    public sealed class LookupResult { public bool Found, Self, Friend, Pending; public string Pf, Num, Name, Error; public int Level; }

    [Serializable] internal sealed class FriendDto { public string pf, num, name; public int rank, level; public bool online; public RoomDto room; public long seen, at; }
    [Serializable] internal sealed class FriendsDto { public bool ok; public FriendDto me; public int max; public List<FriendDto> friends, incoming, outgoing; }
    [Serializable] internal sealed class LookupDto { public bool ok, found, self, friend, pending; public string pf, num, name, error; public int level; }
    [Serializable] internal sealed class OkDto { public bool ok, accepted, pending; public string error; public int retryMs; }

    /// <summary>
    /// The family of the player on our server: his id number, friends, requests, who sits in which room, and invitations.
    /// It tells the server where this player sits every half minute (so friends can see it) and hears news through the chat poll.
    /// Create it with <see cref="Ensure"/>; it lives across scenes.
    /// </summary>
    public sealed class FriendsService : MonoBehaviour
    {
        private const string Tag = "Friends";
        private const float PresenceEvery = 30f;

        public static FriendsService Instance { get; private set; }

        public Friend Me { get; private set; }
        public int Max { get; private set; } = 12;
        public List<Friend> Friends { get; } = new List<Friend>();
        public List<Friend> Incoming { get; } = new List<Friend>();
        public List<Friend> Outgoing { get; } = new List<Friend>();
        public bool Loaded { get; private set; }

        /// <summary>The lists changed.</summary>
        public event Action OnChanged;
        /// <summary>Somebody invited this player to a room.</summary>
        public event Action<ServerEvent> OnInvite;
        /// <summary>A friend request arrived or was accepted (for a toast).</summary>
        public event Action<ServerEvent> OnNews;

        private IRoomService rooms;
        private string lastPresence = "?";
        private float nextPresenceAt;
        private ChatService chat;

        public static FriendsService Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("FriendsService");
            DontDestroyOnLoad(go);
            return go.AddComponent<FriendsService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (Instance != this) return;
            ServiceLocator.TryGet(out rooms);
            chat = ChatService.Ensure();
            chat.OnServerEvent += OnEvent;
            if (rooms != null) { rooms.OnRoomChanged += () => nextPresenceAt = 0f; rooms.OnRoomJoined += _ => nextPresenceAt = 0f; rooms.OnRoomLeft += _ => nextPresenceAt = 0f; }
        }

        private void OnDestroy()
        {
            if (chat != null) chat.OnServerEvent -= OnEvent;
            if (Instance == this) Instance = null;
        }

        private static string Base => (GameConstants.ApiUrl ?? "").TrimEnd('/');
        private static string Ticket => PlayFabSettings.staticPlayer?.ClientSessionTicket;
        private bool Ready => !string.IsNullOrEmpty(Ticket) && !string.IsNullOrEmpty(Base);

        private void Update()
        {
            if (Time.unscaledTime < nextPresenceAt || !Ready) return;
            nextPresenceAt = Time.unscaledTime + PresenceEvery;
            StartCoroutine(ReportPresence());
        }

        // ---------------------------------------------------------------- news

        private void OnEvent(ServerEvent e)
        {
            switch (e.Type)
            {
                case "invite": OnInvite?.Invoke(e); break;
                case "friend_request":
                case "friend_accepted":
                    OnNews?.Invoke(e);
                    Refresh();
                    break;
            }
        }

        // ---------------------------------------------------------------- where this player sits

        private IEnumerator ReportPresence()
        {
            string json = "{\"room\":null}";
            var room = rooms != null && rooms.InRoom ? rooms.CurrentRoom : null;
            if (room != null)
                json = "{\"room\":{\"id\":\"" + JsonText.Esc(room.Id) + "\",\"name\":\"" + JsonText.Esc(room.Name) + "\",\"count\":" + room.Count + ",\"cap\":" + room.Capacity
                     + ",\"kind\":\"" + (room.Kind == RoomKind.Private ? "private" : room.Kind == RoomKind.Vip ? "vip" : "public") + "\",\"locked\":" + (room.Locked ? "true" : "false")
                     + ",\"code\":\"" + JsonText.Esc(room.Kind == RoomKind.Private ? room.Code : "") + "\",\"inMatch\":" + (room.InMatch ? "true" : "false") + "}}";
            lastPresence = json;
            yield return Post("/v1/presence", json, null);
        }

        // ---------------------------------------------------------------- lists

        public void Refresh(Action<bool> done = null) { if (isActiveAndEnabled) StartCoroutine(RefreshRoutine(done)); else done?.Invoke(false); }

        private static Friend Map(FriendDto d)
        {
            var f = new Friend { Pf = d.pf, Num = d.num, Name = d.name, Rank = d.rank, Level = d.level, Online = d.online, Seen = d.seen, At = d.at };
            if (d.room != null && !string.IsNullOrEmpty(d.room.id)) f.Room = new RoomRef { Id = d.room.id, Name = d.room.name, Kind = d.room.kind, Code = d.room.code, Count = d.room.count, Cap = d.room.cap, Locked = d.room.locked, InMatch = d.room.inMatch };
            return f;
        }

        private IEnumerator RefreshRoutine(Action<bool> done)
        {
            if (!Ready) { done?.Invoke(false); yield break; }
            using (var req = UnityWebRequest.Get(Base + "/v1/friends"))
            {
                req.SetRequestHeader("X-Authorization", Ticket); req.timeout = 15;
                yield return req.SendWebRequest();
                FriendsDto d = null;
                if (req.result == UnityWebRequest.Result.Success) { try { d = JsonUtility.FromJson<FriendsDto>(req.downloadHandler.text); } catch { } }
                if (d == null || !d.ok) { done?.Invoke(false); yield break; }
                Me = d.me != null ? Map(d.me) : Me; Max = d.max > 0 ? d.max : Max;
                Friends.Clear(); Incoming.Clear(); Outgoing.Clear();
                if (d.friends != null) Friends.AddRange(d.friends.Select(Map));
                if (d.incoming != null) Incoming.AddRange(d.incoming.Select(Map));
                if (d.outgoing != null) Outgoing.AddRange(d.outgoing.Select(Map));
                Loaded = true;
                OnChanged?.Invoke();
                done?.Invoke(true);
            }
        }

        // ---------------------------------------------------------------- actions

        public void Lookup(string num, Action<LookupResult> done)
        {
            StartCoroutine(Post("/v1/friends/lookup", "{\"num\":\"" + JsonText.Esc(num) + "\"}", (code, json) =>
            {
                LookupDto d = null; try { d = JsonUtility.FromJson<LookupDto>(json); } catch { }
                if (d == null) { done?.Invoke(new LookupResult { Error = "failed" }); return; }
                done?.Invoke(new LookupResult { Found = d.ok && d.found, Self = d.self, Friend = d.friend, Pending = d.pending, Pf = d.pf, Num = d.num, Name = d.name, Level = d.level, Error = d.ok ? null : d.error });
            }));
        }

        /// <summary>Asks somebody (by his id number) to become a friend. Error is a short code from the server, or null.</summary>
        public void SendRequest(string num, Action<string, bool> done)
        {
            StartCoroutine(Post("/v1/friends/request", "{\"num\":\"" + JsonText.Esc(num) + "\"}", (code, json) =>
            {
                OkDto d = null; try { d = JsonUtility.FromJson<OkDto>(json); } catch { }
                bool accepted = d != null && d.ok && d.accepted;
                done?.Invoke(d != null && d.ok ? null : (d != null && !string.IsNullOrEmpty(d.error) ? d.error : "failed"), accepted);
                Refresh();
            }));
        }

        public void Respond(string pf, bool accept, Action<string> done)
        {
            StartCoroutine(Post("/v1/friends/respond", "{\"player\":\"" + JsonText.Esc(pf) + "\",\"accept\":" + (accept ? "true" : "false") + "}", (code, json) =>
            {
                OkDto d = null; try { d = JsonUtility.FromJson<OkDto>(json); } catch { }
                done?.Invoke(d != null && d.ok ? null : (d != null && !string.IsNullOrEmpty(d.error) ? d.error : "failed"));
                Refresh();
            }));
        }

        public void Cancel(string pf, Action<string> done = null) { StartCoroutine(Post("/v1/friends/cancel", "{\"player\":\"" + JsonText.Esc(pf) + "\"}", (c, j) => { done?.Invoke(c == 200 ? null : "failed"); Refresh(); })); }
        public void Remove(string pf, Action<string> done = null) { StartCoroutine(Post("/v1/friends/remove", "{\"player\":\"" + JsonText.Esc(pf) + "\"}", (c, j) => { done?.Invoke(c == 200 ? null : "failed"); Refresh(); })); }

        /// <summary>Invites a friend to the room this player sits in. Error: "no_room", a server code, or null.</summary>
        public void Invite(string friendPf, Action<string> done)
        {
            var room = rooms != null && rooms.InRoom ? rooms.CurrentRoom : null;
            if (room == null) { done?.Invoke("no_room"); return; }
            string json = "{\"friend\":\"" + JsonText.Esc(friendPf) + "\",\"room\":{\"id\":\"" + JsonText.Esc(room.Id) + "\",\"name\":\"" + JsonText.Esc(room.Name) + "\",\"count\":" + room.Count + ",\"cap\":" + room.Capacity
                        + ",\"kind\":\"" + (room.Kind == RoomKind.Private ? "private" : room.Kind == RoomKind.Vip ? "vip" : "public") + "\",\"code\":\"" + JsonText.Esc(room.Kind == RoomKind.Private ? room.Code : "") + "\"}}";
            StartCoroutine(Post("/v1/friends/invite", json, (code, body) =>
            {
                OkDto d = null; try { d = JsonUtility.FromJson<OkDto>(body); } catch { }
                done?.Invoke(d != null && d.ok ? null : (d != null && !string.IsNullOrEmpty(d.error) ? d.error : "failed"));
            }));
        }

        /// <summary>Reports a player (reason: abuse, cheat, spam, other).</summary>
        public void ReportPlayer(string pf, string reason, string note, Action<bool> done)
        {
            StartCoroutine(Post("/v1/report/player", "{\"player\":\"" + JsonText.Esc(pf) + "\",\"reason\":\"" + JsonText.Esc(reason) + "\",\"note\":\"" + JsonText.Esc(note ?? "") + "\"}", (c, j) => done?.Invoke(c == 200)));
        }

        /// <summary>Goes to the room a friend sits in or an invitation points to.</summary>
        public void JoinRoom(RoomRef room, Action<RoomResult> done)
        {
            if (rooms == null || room == null || string.IsNullOrEmpty(room.Id)) { done?.Invoke(RoomResult.Fail(RoomError.NotFound)); return; }
            var listing = new RoomListing { Id = room.Id, Name = room.Name, Count = room.Count, Capacity = room.Cap, Locked = room.Locked, Code = room.Code ?? "", Kind = room.Kind == "private" ? RoomKind.Private : room.Kind == "vip" ? RoomKind.Vip : RoomKind.Public };
            rooms.Join(listing, done);
        }

        private IEnumerator Post(string path, string json, Action<long, string> done)
        {
            if (!Ready) { done?.Invoke(0, ""); yield break; }
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
    }
}
