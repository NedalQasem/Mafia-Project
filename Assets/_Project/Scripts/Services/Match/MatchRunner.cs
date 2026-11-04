using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.MatchRules;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Economy;
using MafiaNight.Services.Profile;
using MafiaNight.Services.Rooms;
using MafiaNight.Services.Scenes;
using MafiaNight.Services.Voice;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MafiaNight.Services.Match
{
    /// <summary>
    /// Keeps one <see cref="MatchSession"/> alive while the player sits in a room (opened on joining, closed on leaving) and ticks it,
    /// across scene changes. When the host starts a match, every player's device loads the match screens. Screens reach the session
    /// through <see cref="Session"/>. Create it with <see cref="Ensure"/>.
    /// </summary>
    /// <summary>One line of the room's text chat.</summary>
    public sealed class RoomChatLine { public string Puid, PlayFabId, Name, Text; public int Rank; public bool Mine; public long Ts; public ChatChannel Channel; }

    public sealed class MatchRunner : MonoBehaviour
    {
        private const string Tag = "MatchRunner";

        public static MatchRunner Instance { get; private set; }
        public MatchSession Session { get; private set; }
        private string endReason = "";
        private Coroutine reporting;

        /// <summary>What the rewards server paid for the last match (None until a match ends).</summary>
        public RewardResult Reward { get; private set; } = new RewardResult();
        public event System.Action RewardChanged;

        private readonly System.Collections.Generic.List<RoomChatLine> roomChat = new System.Collections.Generic.List<RoomChatLine>();
        private float lastRoomChatAt = -10f;
        /// <summary>What this player's chat box may do right now (the channel, or why it is locked): the match rules, see <see cref="MatchChat"/>.</summary>
        public ChatPlan ChatPlanNow
        {
            get
            {
                var c = Session?.Client;
                if (c == null) return MatchChat.For(Phase.Lobby, null, false, false);
                return MatchChat.For(c.Phase, c.MyRole, c.IAmAlive || c.IAmTheAvenger, c.IAmSilenced);
            }
        }

        /// <summary>The lines this player can read now, on the channel his box is on (the room's screens show these).</summary>
        public System.Collections.Generic.IReadOnlyList<RoomChatLine> RoomChatHistory => ChatLines(ChatPlanNow.Channel);
        /// <summary>A line on the channel this player's box is on arrived.</summary>
        public event System.Action<RoomChatLine> RoomChatReceived;
        /// <summary>Any line was kept (on any channel), or the match moved to another channel: the table screen redraws its chat.</summary>
        public event System.Action ChatChanged;

        public System.Collections.Generic.List<RoomChatLine> ChatLines(ChatChannel channel)
        {
            var list = new System.Collections.Generic.List<RoomChatLine>();
            foreach (var l in roomChat) if (l.Channel == channel) list.Add(l);
            return list;
        }

        /// <summary>Writes a line on the channel the match allows. Returns an Arabic reason when it is refused, otherwise null.</summary>
        public string SendRoomChat(string text)
        {
            if (Session == null || rooms == null || !rooms.InRoom) return "لست بغرفة.";
            if (rooms.CurrentRoom != null && !rooms.CurrentRoom.ChatEnabled) return "الدردشة مقفلة بهالغرفة.";
            var plan = ChatPlanNow;
            if (plan.Locked) return plan.LockedText;
            text = CleanChat(text);
            if (string.IsNullOrEmpty(text)) return "";
            if (Time.unscaledTime - lastRoomChatAt < 1f) return "مهلاً، رسائلك سريعة.";
            lastRoomChatAt = Time.unscaledTime;
            Session.SendChat(text, plan.Channel);
            var me = System.Linq.Enumerable.FirstOrDefault(rooms.Members, m => m.IsMe);
            AddRoomLine(new RoomChatLine { Puid = me?.Puid, PlayFabId = me?.PlayFabId, Name = me?.Name ?? "", Rank = me?.Rank ?? 0, Text = text, Mine = true, Ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Channel = plan.Channel });
            return null;
        }

        private static string CleanChat(string s)
        {
            if (s == null) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s) sb.Append(ch < ' ' ? ' ' : ch);
            string t = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
            return t.Length > 300 ? t.Substring(0, 300) : t;
        }

        private void OnRoomChat(string senderPuid, string text, ChatChannel channel)
        {
            if (rooms == null || !rooms.InRoom || (rooms.CurrentRoom != null && !rooms.CurrentRoom.ChatEnabled)) return;
            var from = System.Linq.Enumerable.FirstOrDefault(rooms.Members, m => m.Puid == senderPuid);
            if (from == null && Session != null)
            {   // a bot the host put into the room: the host speaks for it
                var bot = System.Linq.Enumerable.FirstOrDefault(Session.Stage.Virtuals, v => v.Id == senderPuid);
                if (bot != null) from = new MafiaNight.Data.Rooms.RoomMember { Puid = bot.Id, PlayFabId = "", Name = bot.Name, Rank = bot.Rank, Level = bot.Level };
            }
            if (from == null || from.IsMe) return;                     // only people in this room, and never an echo of ourselves
            text = CleanChat(text);
            if (string.IsNullOrEmpty(text)) return;
            AddRoomLine(new RoomChatLine { Puid = from.Puid, PlayFabId = from.PlayFabId, Name = from.Name, Rank = from.Rank, Text = text, Ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Channel = channel });
        }

        private void AddRoomLine(RoomChatLine line)
        {
            roomChat.Add(line);
            if (roomChat.Count > 200) roomChat.RemoveAt(0);
            if (line.Channel == ChatPlanNow.Channel) RoomChatReceived?.Invoke(line);
            ChatChanged?.Invoke();
        }

        /// <summary>Why the last match was cancelled (shown once by the room screen), or empty.</summary>
        public string TakeEndReason() { var r = endReason; endReason = ""; return r; }

        /// <summary>The voice device of the room (the table's mic button reads and switches it).</summary>
        public IVoiceService Voice => voice;
        private bool micSaved, micBefore;

        /// <summary>A match is on: everybody's own mic switch goes on (the room opens it only for those on a mic seat), and it goes back to what it was when the match is over.</summary>
        private void MatchMicOn() { if (voice == null || micSaved) return; micSaved = true; micBefore = voice.MicOn; voice.SetMic(true); }
        private void MatchMicRestore() { if (voice == null || !micSaved) return; micSaved = false; voice.SetMic(micBefore); }

        private IRoomService rooms;
        private IEosPlatform eos;
        private ISceneLoader scenes;
        private IVoiceService voice;
        private string lastVoicePlan = "";
        private Phase prevPhase = Phase.Lobby;                 // the phase before the one that just arrived: tells a new match from the next night of the same one
        private MatchState lastState = MatchState.Idle;

        /// <summary>Where this player stands against the room's match (one value for every screen; see <see cref="MatchStates"/>).</summary>
        public MatchState State => Session == null ? MatchState.Idle : MatchStates.Of(Session.Client.Phase, Session.Client.IAmAlive);
        /// <summary>The state changed (a match began or ended, the player died, ...).</summary>
        public event System.Action StateChanged;

        /// <summary>The table screens can be opened now: a match is running (as a player or as a spectator).</summary>
        public bool CanOpenTable => State == MatchState.Playing || State == MatchState.Watching;

        /// <summary>The player asks for the table (the room screen's match button): the screens open and stay until he leaves them.</summary>
        public void OpenTable()
        {
            if (!CanOpenTable || SceneManager.GetActiveScene().name == GameConstants.SCENE_MATCH) return;
            Log.Info("Opening the table by the player's choice (" + State + ")", Tag);
            if (scenes != null) scenes.LoadScene(GameConstants.SCENE_MATCH); else SceneManager.LoadScene(GameConstants.SCENE_MATCH);
        }

        private void OnClientChanged()
        {
            ApplyVoice();
            ChatChanged?.Invoke();
            var now = State;
            if (now == lastState) return;
            Log.Info("Match state: " + lastState + " -> " + now, Tag);
            lastState = now;
            StateChanged?.Invoke();
        }
        private System.Collections.Generic.HashSet<string> known = new System.Collections.Generic.HashSet<string>();
        private bool started;

        /// <summary>Makes the runner if it does not exist yet; it survives scene changes.</summary>
        public static MatchRunner Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("MatchRunner");
            DontDestroyOnLoad(go);
            return go.AddComponent<MatchRunner>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (Instance != this) return;
            if (!ServiceLocator.TryGet(out rooms) || !ServiceLocator.TryGet(out eos)) { Destroy(gameObject); return; }
            ServiceLocator.TryGet(out scenes);
            ServiceLocator.TryGet(out voice);
            rooms.OnRoomJoined += OnJoined;
            rooms.OnRoomLeft += OnLeft;
            rooms.OnRoomChanged += OnRoomChanged;
            started = true;
            if (rooms.InRoom) Open();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (!started) return;
            rooms.OnRoomJoined -= OnJoined;
            rooms.OnRoomLeft -= OnLeft;
            rooms.OnRoomChanged -= OnRoomChanged;
            Close();
        }

        private void OnJoined(RoomListing _) { Open(); }
        private void OnLeft(string _) { Close(); }

        private void Open()
        {
            if (Session != null) return;
            Session = new MatchSession(eos, rooms);
            Session.Client.PhaseChanged += OnPhase;
            Session.Client.Changed += OnClientChanged;
            prevPhase = Phase.Lobby; lastState = MatchState.Idle;
            Session.Cancelled += OnCancelled;
            Session.RoomChat += OnRoomChat;
            roomChat.Clear();
            Session.Client.Finished += OnMatchFinished;
            lastVoicePlan = "";
            known.Clear();
            foreach (var m in rooms.Members) known.Add(m.Puid);
        }

        private void Close()
        {
            if (Session == null) return;
            Session.Client.PhaseChanged -= OnPhase;
            Session.Client.Changed -= OnClientChanged;
            Session.Cancelled -= OnCancelled;
            Session.RoomChat -= OnRoomChat;
            roomChat.Clear();
            Session.Client.Finished -= OnMatchFinished;
            Session.Dispose();
            MatchMicRestore();
            voice?.ClearGameVoice();
            Session = null;
        }

        /// <summary>The match ended: every player's device reports it to the rewards server, which pays once most players agree.</summary>
        private void OnMatchFinished()
        {
            MatchMicRestore();
            LastMatchStore.Clear();
            var report = Session?.Client.BuildReport();
            Reward = new RewardResult();
            if (report == null) { RewardChanged?.Invoke(); return; }
            if (reporting != null) StopCoroutine(reporting);
            reporting = StartCoroutine(RewardsReporter.Run(report, OnRewardProgress));
        }

        private void OnRewardProgress(RewardResult r)
        {
            Reward = r;
            RewardChanged?.Invoke();
            if (r.Status != RewardStatus.Granted) return;
            // the server changed the coins and the progress: read them again
            if (ServiceLocator.TryGet(out ICurrencyService currency)) currency.FetchCurrencies();
            if (ServiceLocator.TryGet(out IPlayerProfileService profile)) profile.RefreshProfile();
        }

        private void OnCancelled(string reason)
        {
            MatchMicRestore();
            LastMatchStore.Clear();
            prevPhase = Phase.Lobby;
            endReason = reason ?? "";
            voice?.ClearGameVoice();
            if (SceneManager.GetActiveScene().name != GameConstants.SCENE_MATCH) return;
            if (scenes != null) scenes.LoadScene(GameConstants.SCENE_HOME); else SceneManager.LoadScene(GameConstants.SCENE_HOME);
        }

        /// <summary>The first state message of a new match arrives: show the table.</summary>
        private void OnPhase(Phase p)
        {
            var before = prevPhase; prevPhase = p;
            if (p == Phase.Lobby || p == Phase.GameOver) return;
            MatchMicOn();
            if (Session != null && Session.Client.MyRole.HasValue) LastMatchStore.Save(rooms?.CurrentRoom);     // so he can come back if the app or the network drops
            if (SceneManager.GetActiveScene().name == GameConstants.SCENE_MATCH) return;
            // the table opens by itself only when a match begins; a player who went back to the room is not dragged to it by every night and day (he opens it with the match button)
            if (!MatchStates.OpensTable(before, p)) { Log.Info("Match phase " + p + ": the player is in the room, the table stays closed (match button opens it)", Tag); return; }
            Log.Info("Match started: loading the table screens", Tag);
            if (scenes != null) scenes.LoadScene(GameConstants.SCENE_MATCH);
            else SceneManager.LoadScene(GameConstants.SCENE_MATCH);
        }

        /// <summary>The match decides who may talk and whom we hear (see <see cref="MatchVoiceRules"/>); only changes are sent to EOS.</summary>
        private void ApplyVoice()
        {
            if (voice == null || Session == null) return;
            var c = Session.Client;
            var mates = c.Seats.Where(x => x.IsMate && x.Alive && !x.Left).Select(x => x.Id).ToList();
            var living = c.Seats.Where(x => x.Alive && !x.Left).Select(x => x.Id).ToList();
            var me = rooms.Members.FirstOrDefault(m => m.IsMe);
            var plan = MatchVoiceRules.For(c.Phase, c.MyRole, c.IAmAlive, mates, living, rooms.Members.Select(m => m.Puid).ToList(), me != null ? me.Puid : "", c.IAmSilenced, c.Phase == Phase.Avenge ? c.AvengerId : null);
            string sig = plan.MicAllowed + "|" + (plan.HearOnly == null ? "*" : string.Join(",", plan.HearOnly.OrderBy(x => x)));
            if (sig == lastVoicePlan) return;
            lastVoicePlan = sig;
            voice.SetGameVoice(plan.MicAllowed, plan.HearOnly);
            Log.Info("Match voice (" + State + ", " + c.Phase + "): mic " + (plan.MicAllowed ? "open" : "closed") + ", hears " + (plan.HearOnly == null ? "everybody" : plan.HearOnly.Count + " people"), Tag);
        }

        private void OnRoomChanged()
        {
            if (Session == null) return;
            var now = new System.Collections.Generic.HashSet<string>();
            foreach (var m in rooms.Members) now.Add(m.Puid);
            foreach (var id in known) if (!now.Contains(id)) Session.MemberLeft(id);
            foreach (var id in now) if (!known.Contains(id)) Session.MemberJoined(id);
            known = now;
            ApplyVoice();            // somebody came or went: who is a player and who a spectator may have changed
        }

        private void Update() { Session?.Tick(Time.unscaledDeltaTime); }
    }
}
