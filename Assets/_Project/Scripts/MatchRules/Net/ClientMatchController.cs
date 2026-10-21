using System;
using System.Collections.Generic;
using System.Linq;

namespace MafiaNight.MatchRules.Net
{
    /// <summary>The finished match as one device reports it to the rewards server.</summary>
    public sealed class MatchReport
    {
        public string MatchId, Winner, MyRole;
        public List<string> PlayFabIds;
        public bool Survived;
        public int Rounds, DurationSec;
    }

    /// <summary>A player as the screens see him. <see cref="Role"/> is only filled for what this player may know.</summary>
    public sealed class SeatView
    {
        public string Id, Name;
        public bool Alive, Left;
        public Role? Role;
        public bool IsMe, IsMate;
    }

    /// <summary>
    /// What one player's device knows about the match. It only applies messages from the host and sends "act" requests back, so the
    /// screens (the frames of Docs/mockups/match.html) can read this and nothing else. Plain C#, no Unity types.
    /// </summary>
    public sealed class ClientMatchController
    {
        private readonly string myId;
        private readonly Action<WireMessage> toHost;
        private readonly List<SeatView> seats = new List<SeatView>();
        private readonly Dictionary<string, Role> known = new Dictionary<string, Role>();

        public Phase Phase { get; private set; } = Phase.Lobby;
        public int Round { get; private set; }
        public float SecondsLeft { get; private set; }
        public Role? MyRole { get; private set; }
        public IReadOnlyList<SeatView> Seats => seats;
        public Winner Winner { get; private set; }
        /// <summary>Id of the running match (the same on every device; the rewards server counts reports per match). Empty for practice.</summary>
        public string MatchId { get; private set; } = "";
        /// <summary>Player id -> PlayFab id of everybody who started the match.</summary>
        public IReadOnlyDictionary<string, string> Roster => roster;
        public DateTime StartedUtc { get; private set; }
        public double DurationSeconds { get; private set; }
        private readonly Dictionary<string, string> roster = new Dictionary<string, string>();

        public string DawnVictimId { get; private set; }
        public string DawnDiedId { get; private set; }
        public Role? DawnDiedRole { get; private set; }
        public bool DawnSaved { get; private set; }

        public string VerdictRemovedId { get; private set; }
        public Role? VerdictRemovedRole { get; private set; }
        public int VerdictVotes { get; private set; }
        public int VerdictAbstained { get; private set; }

        /// <summary>The sheriff's answers, newest last (target id, is mafia).</summary>
        public List<KeyValuePair<string, bool>> SheriffReports { get; } = new List<KeyValuePair<string, bool>>();
        /// <summary>Mafia only: what each mafia member picked tonight (member id, victim id).</summary>
        public Dictionary<string, string> MafiaVotes { get; } = new Dictionary<string, string>();
        /// <summary>Live vote count (target id, votes).</summary>
        public Dictionary<string, int> Tally { get; } = new Dictionary<string, int>();
        public int SkipCount { get; private set; }
        public int SkipNeeded { get; private set; }
        public string LastResultKind { get; private set; }
        public ActionResult LastResult { get; private set; }

        public event Action Changed;
        public event Action<Phase> PhaseChanged;
        /// <summary>The match is over and the winner and every role are known (raised once per match).</summary>
        public event Action Finished;
        /// <summary>The match was cancelled (the host was lost and nobody could continue it). Argument: the reason in Arabic.</summary>
        public event Action<string> Aborted;

        public ClientMatchController(string myId, Action<WireMessage> toHost) { this.myId = myId; this.toHost = toHost; }

        public bool IAmAlive => seats.FirstOrDefault(s => s.IsMe)?.Alive == true;
        public bool IAmMafia => MyRole == Role.Mafia;
        public SeatView Seat(string id) => seats.FirstOrDefault(s => s.Id == id);

        // ---------------------------------------------------------------- from the host

        public void Apply(WireMessage m)
        {
            if (m == null) return;
            switch (m.Type)
            {
                case Msg.Start:
                    known.Clear(); SheriffReports.Clear(); Winner = Winner.None; MyRole = null;
                    MatchId = m.Arg(0); roster.Clear(); StartedUtc = DateTime.UtcNow; DurationSeconds = 0;
                    for (int i = 1; i + 1 < m.Args.Length; i += 2) roster[m.Args[i]] = m.Args[i + 1];
                    break;
                case Msg.Role:
                    if (Enum.TryParse(m.Arg(0), out Role r)) { MyRole = r; known[myId] = r; }
                    for (int i = 1; i < m.Args.Length; i++) known[m.Args[i]] = Role.Mafia;
                    RefreshRoles();
                    break;
                case Msg.State: ApplyState(m); break;
                case Msg.Result:
                    LastResultKind = m.Arg(0);
                    Enum.TryParse(m.Arg(1), out ActionResult res); LastResult = res;
                    break;
                case Msg.MafiaVotes:
                    MafiaVotes.Clear();
                    for (int i = 0; i + 1 < m.Args.Length; i += 2) MafiaVotes[m.Args[i]] = m.Args[i + 1];
                    break;
                case Msg.Sheriff:
                    SheriffReports.Add(new KeyValuePair<string, bool>(m.Arg(0), m.Arg(1) == "1"));
                    if (m.Arg(1) == "1") known[m.Arg(0)] = Role.Mafia;
                    RefreshRoles();
                    break;
                case Msg.Dawn:
                    DawnVictimId = Nullable(m.Arg(0)); DawnSaved = m.Arg(1) == "1"; DawnDiedId = Nullable(m.Arg(2));
                    DawnDiedRole = Enum.TryParse(m.Arg(3), out Role dr) ? dr : (Role?)null;
                    if (DawnDiedId != null && DawnDiedRole.HasValue) known[DawnDiedId] = DawnDiedRole.Value;
                    RefreshRoles();
                    break;
                case Msg.Reveal:
                    for (int i = 0; i + 1 < m.Args.Length; i += 2) if (Enum.TryParse(m.Args[i + 1], out Role rv)) known[m.Args[i]] = rv;
                    RefreshRoles();
                    break;
                case Msg.Skip: SkipCount = m.Int(0); SkipNeeded = m.Int(1); break;
                case Msg.Tally:
                    Tally.Clear();
                    for (int i = 0; i + 1 < m.Args.Length; i += 2) Tally[m.Args[i]] = m.Int(i + 1);
                    break;
                case Msg.Verdict:
                    VerdictRemovedId = Nullable(m.Arg(0));
                    VerdictRemovedRole = Enum.TryParse(m.Arg(1), out Role vr) ? vr : (Role?)null;
                    VerdictVotes = m.Int(2); VerdictAbstained = m.Int(3);
                    if (VerdictRemovedId != null && VerdictRemovedRole.HasValue) known[VerdictRemovedId] = VerdictRemovedRole.Value;
                    RefreshRoles();
                    break;
                case Msg.Abort:
                    Phase = Phase.Lobby; MafiaVotes.Clear(); Tally.Clear();
                    Aborted?.Invoke(m.Arg(0));
                    break;
                case Msg.Over:
                    Enum.TryParse(m.Arg(0), out Winner w); Winner = w;
                    DurationSeconds = (DateTime.UtcNow - StartedUtc).TotalSeconds;
                    for (int i = 1; i + 1 < m.Args.Length; i += 2) if (Enum.TryParse(m.Args[i + 1], out Role rr)) known[m.Args[i]] = rr;   // the roles are public now
                    RefreshRoles();
                    Changed?.Invoke();
                    Finished?.Invoke();
                    break;
            }
            Changed?.Invoke();
        }

        private static string Nullable(string s) => string.IsNullOrEmpty(s) ? null : s;

        private void ApplyState(WireMessage m)
        {
            var oldPhase = Phase;
            Enum.TryParse(m.Arg(0), out Phase p); Phase = p;
            Round = m.Int(1); SecondsLeft = m.Float(2);
            seats.Clear();
            for (int i = 3; i + 3 < m.Args.Length; i += 4)
                seats.Add(new SeatView { Id = m.Args[i], Name = m.Args[i + 1], Alive = m.Args[i + 2] == "1", Left = m.Args[i + 3] == "1", IsMe = m.Args[i] == myId });
            RefreshRoles();
            if (Phase != oldPhase)
            {
                if (Phase == Phase.Night) { MafiaVotes.Clear(); }
                if (Phase == Phase.Voting) { Tally.Clear(); }
                if (Phase == Phase.Day) { SkipCount = 0; }
                PhaseChanged?.Invoke(Phase);
            }
        }

        private void RefreshRoles()
        {
            foreach (var s in seats)
            {
                s.Role = known.TryGetValue(s.Id, out var r) ? r : (Role?)null;
                s.IsMate = IAmMafia && s.Role == Role.Mafia && !s.IsMe;
            }
        }

        /// <summary>
        /// What this device tells the rewards server when the match is over: who played (PlayFab ids), who won, and this player's own role and fate.
        /// Null when there is nothing to report (practice, a spectator, no winner, or too few known PlayFab ids).
        /// </summary>
        public MatchReport BuildReport()
        {
            if (Phase != Phase.GameOver || Winner == Winner.None || string.IsNullOrEmpty(MatchId) || !MyRole.HasValue) return null;
            var me = seats.FirstOrDefault(s => s.IsMe);
            if (me == null) return null;
            var ids = new List<string>();
            foreach (var s in seats) if (roster.TryGetValue(s.Id, out var pf) && !string.IsNullOrEmpty(pf)) ids.Add(pf);
            return new MatchReport { MatchId = MatchId, PlayFabIds = ids, Winner = Winner.ToString(), MyRole = MyRole.Value.ToString(), Survived = me.Alive && !me.Left, Rounds = Math.Max(1, Round), DurationSec = (int)Math.Max(0, DurationSeconds) };
        }

        public void Tick(float dt) { if (SecondsLeft > 0f) SecondsLeft = Math.Max(0f, SecondsLeft - dt); }

        // ---------------------------------------------------------------- to the host

        private void Act(string kind, string target = "") => toHost?.Invoke(new WireMessage(Msg.Act, kind, target ?? ""));

        public void MafiaPick(string id) => Act("mafia", id);
        public void DoctorSave(string id) => Act("save", id);
        public void SheriffCheck(string id) => Act("check", id);
        public void SkipDiscussion() => Act("skip");
        public void Vote(string idOrNullToAbstain) => Act("vote", idOrNullToAbstain);
        public void Ready() => Act("ready");
    }
}
