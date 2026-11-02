using System.Linq;
using System.Text;
using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.Services.Match;
using MafiaNight.Services.Net;
using MafiaNight.Services.Rooms;
using MafiaNight.Services.Voice;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Test builds only: writes what happens in a room to the test journal (<see cref="GameJournal"/>) in plain words (who came, who left, who is host, the voice, the mic, the seat),
    /// and checks every couple of seconds that the state makes sense; anything that does not is written as an ANOMALY line, which is where reading a problem report starts.
    /// </summary>
    public sealed class HealthWatch : MonoBehaviour
    {
        private const string Tag = "Health";
        private const float Every = 2f;

        public static HealthWatch Instance { get; private set; }

        private IRoomService rooms;
        private IVoiceService audio;
        private float next;
        private string lastState = "";
        private float roomNoVoiceSince = -1f, voiceNoRoomSince = -1f, noHostSince = -1f, notMemberSince = -1f;
        private bool flaggedNoVoice, flaggedVoiceNoRoom, flaggedNoHost, flaggedNotMember, flaggedDuplicate;
        private string lastMembers = "";

        public static HealthWatch Ensure()
        {
            if (!GameJournal.Enabled) return null;
            if (Instance != null) return Instance;
            var go = new GameObject("HealthWatch");
            DontDestroyOnLoad(go);
            return go.AddComponent<HealthWatch>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            NetworkWatch.Changed += OnNetwork;
            GameJournal.OnTap += OnTap;
        }

        private void OnDestroy()
        {
            NetworkWatch.Changed -= OnNetwork;
            GameJournal.OnTap -= OnTap;
            if (rooms != null) { rooms.OnRoomJoined -= OnJoined; rooms.OnRoomLeft -= OnLeft; rooms.OnHostChanged -= OnHost; }
            if (audio != null) audio.OnConnectionChanged -= OnVoice;
            if (Instance == this) Instance = null;
        }

        private void OnNetwork(bool online) => GameJournal.Event("Network", online ? "connection is back" : "connection lost (system says " + Application.internetReachability + ")");

        // ---- a tap on a seat or the mic that changes nothing: the player pressed and the room did not answer (the host did not answer, the message never arrived...)
        private string seatTapState;
        private float seatTapAt = -1f;
        private string seatTapWhat = "";

        private void OnTap(string what)
        {
            if (rooms == null || rooms.CurrentRoom == null || !(what.StartsWith("Seat ") || what.StartsWith("Mic "))) return;
            if (seatTapAt >= 0f) return;            // one watch at a time: a burst of taps is judged by the first
            seatTapAt = Time.unscaledTime; seatTapWhat = what; seatTapState = StageState();
        }

        private string StageState()
        {
            var runner = MatchRunner.Instance;
            var stage = runner != null && runner.Session != null ? runner.Session.Stage : null;
            var me = rooms != null ? rooms.Members.FirstOrDefault(m => m.IsMe) : null;
            if (stage == null || me == null) return "no stage";
            return (stage.Stage.IsSeated(me.Puid) ? "seated" : "listening") + "/" + (stage.Stage.HandRaised(me.Puid) ? "hand up" : "hand down") + "/" + (audio != null && audio.MicOn ? "mic on" : "mic off");
        }

        private void CheckSeatTap()
        {
            if (seatTapAt < 0f || Time.unscaledTime - seatTapAt < 5f) return;
            if (rooms.CurrentRoom != null && StageState() == seatTapState)
                GameJournal.Anomaly(Tag, "tapped \"" + seatTapWhat + "\" and nothing changed for 5 s (" + seatTapState + "; host " + (rooms.Members.FirstOrDefault(m => m.IsHost)?.Name ?? "none") + (rooms.IAmHost ? ", I am the host" : "") + ")");
            seatTapAt = -1f;
        }

        private void Bind()
        {
            if (rooms == null && ServiceLocator.TryGet(out IRoomService r))
            {
                rooms = r;
                rooms.OnRoomJoined += OnJoined; rooms.OnRoomLeft += OnLeft; rooms.OnHostChanged += OnHost;
            }
            if (audio == null && ServiceLocator.TryGet(out IVoiceService a))
            {
                audio = a;
                audio.OnConnectionChanged += OnVoice;
            }
        }

        private void OnJoined(RoomListing room) => GameJournal.Event("Room", "joined \"" + room.Name + "\" (" + room.Kind + ", " + room.Mode + (string.IsNullOrEmpty(room.OfficialId) ? "" : ", official " + room.OfficialId) + ") · " + room.Count + "/" + room.Capacity + " · mic seats " + room.MicSeats + (room.MicByRequest ? " by request" : " free"));
        private void OnLeft(string reason) => GameJournal.Event("Room", string.IsNullOrEmpty(reason) ? "left the room (by choice, or switching rooms)" : "LOST the room: " + reason);
        private void OnHost(string text) => GameJournal.Event("Room", "host change: " + text);
        private void OnVoice(bool connected) => GameJournal.Event("Voice", (connected ? "connected" : "DISCONNECTED") + " · " + ShortStats(audio?.LastStatistics));

        /// <summary>EOS hands over its connection statistics as a long JSON text: only the fact that it is the EOS path is kept.</summary>
        private static string ShortStats(string s) => string.IsNullOrEmpty(s) ? "" : s.StartsWith("[") || s.StartsWith("{") ? "EOS" : s;

        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + Every;
            Bind();
            if (rooms == null) return;
            CheckSeatTap();
            Watch();
        }

        private void Watch()
        {
            var room = rooms.CurrentRoom;
            bool voiceUp = audio != null && audio.IsConnected;
            float now = Time.unscaledTime;
            var runner = MatchRunner.Instance;
            var stage = runner != null && runner.Session != null ? runner.Session.Stage : null;
            var me = rooms.Members.FirstOrDefault(m => m.IsMe);
            bool seated = stage != null && me != null && stage.Stage.IsSeated(me.Puid);

            // a line only when something changed, so the journal reads like a story
            var sb = new StringBuilder();
            if (room == null) sb.Append("no room");
            else
            {
                sb.Append("room \"").Append(room.Name).Append("\" members ").Append(rooms.Members.Count).Append(" host ").Append(rooms.Members.FirstOrDefault(m => m.IsHost)?.Name ?? "none");
                sb.Append(" · me ").Append(me != null ? (seated ? "on a seat" : "listening") : "not in the member list");
                sb.Append(" · mic ").Append(audio != null && audio.MicOn ? "on" : "off");
            }
            sb.Append(" · voice ").Append(voiceUp ? "up" : "down").Append(" · network ").Append(NetworkWatch.Online ? "ok" : "LOST");
            string state = sb.ToString();
            if (state != lastState) { GameJournal.Event("State", state); lastState = state; }

            if (room != null)
            {
                string members = string.Join(", ", rooms.Members.Select(m => (m.Name ?? "?") + (m.IsHost ? "(host)" : "") + (m.IsMe ? "(me)" : "") + (m.IsKeeper ? "(bot)" : "")));
                if (members != lastMembers) { GameJournal.Event("Members", members); lastMembers = members; }
            }
            else lastMembers = "";

            // ---- does it make sense?
            Check(room != null && !voiceUp && !(audio != null && audio.LastStatistics != null && audio.LastStatistics.Contains("connecting")), ref roomNoVoiceSince, ref flaggedNoVoice, 25f, now,
                "in a room but the voice has not been connected" + (audio != null ? " (" + audio.LastStatistics + ")" : " (no voice service)"));
            Check(room == null && voiceUp, ref voiceNoRoomSince, ref flaggedVoiceNoRoom, 10f, now, "the voice is connected but the player is in no room (" + audio?.LastStatistics + ")");
            Check(room != null && rooms.Members.Count > 0 && !rooms.Members.Any(m => m.IsHost) && !(room.OfficialId != null && room.OfficialId.Length > 0), ref noHostSince, ref flaggedNoHost, 10f, now, "the room has members but none of them is the host");
            Check(room != null && me == null, ref notMemberSince, ref flaggedNotMember, 6f, now, "the player is in a room but is not in its member list");
            if (room != null && rooms.Members.GroupBy(m => m.Puid).Any(g => g.Count() > 1)) { if (!flaggedDuplicate) { flaggedDuplicate = true; GameJournal.Anomaly(Tag, "the same player appears twice in the member list"); } }
            else flaggedDuplicate = false;
        }

        /// <summary>Raises one anomaly line when a bad condition lasts <paramref name="seconds"/>, and again only after it went away and came back.</summary>
        private static void Check(bool bad, ref float since, ref bool flagged, float seconds, float now, string text)
        {
            if (!bad) { since = -1f; flagged = false; return; }
            if (since < 0f) since = now;
            if (!flagged && now - since >= seconds) { flagged = true; GameJournal.Anomaly(Tag, text + " — for " + (now - since).ToString("0") + " s"); }
        }
    }
}
