using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.MatchRules;
using MafiaNight.Services.Auth;
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
    public sealed class MatchRunner : MonoBehaviour
    {
        private const string Tag = "MatchRunner";

        public static MatchRunner Instance { get; private set; }
        public MatchSession Session { get; private set; }

        private IRoomService rooms;
        private IEosPlatform eos;
        private ISceneLoader scenes;
        private IVoiceService voice;
        private string lastVoicePlan = "";
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
            Session.Client.Changed += ApplyVoice;
            lastVoicePlan = "";
            known.Clear();
            foreach (var m in rooms.Members) known.Add(m.Puid);
        }

        private void Close()
        {
            if (Session == null) return;
            Session.Client.PhaseChanged -= OnPhase;
            Session.Client.Changed -= ApplyVoice;
            Session.Dispose();
            voice?.ClearGameVoice();
            Session = null;
        }

        /// <summary>The first state message of a new match arrives: show the table.</summary>
        private void OnPhase(Phase p)
        {
            if (p != Phase.RoleReveal) return;
            if (SceneManager.GetActiveScene().name == GameConstants.SCENE_MATCH) return;
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
            var plan = MatchVoiceRules.For(c.Phase, c.MyRole, c.IAmAlive, mates);
            string sig = plan.MicAllowed + "|" + (plan.HearOnly == null ? "*" : string.Join(",", plan.HearOnly.OrderBy(x => x)));
            if (sig == lastVoicePlan) return;
            lastVoicePlan = sig;
            voice.SetGameVoice(plan.MicAllowed, plan.HearOnly);
            Log.Info("Match voice: " + sig, Tag);
        }

        private void OnRoomChanged()
        {
            if (Session == null) return;
            var now = new System.Collections.Generic.HashSet<string>();
            foreach (var m in rooms.Members) now.Add(m.Puid);
            foreach (var id in known) if (!now.Contains(id)) Session.MemberLeft(id);
            foreach (var id in now) if (!known.Contains(id)) Session.MemberJoined(id);
            known = now;
        }

        private void Update() { Session?.Tick(Time.unscaledDeltaTime); }
    }
}
