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

        /// <summary>Which role is awake right now (null: the night opens, or the pause between two calls). Everybody sees it on the banner in the middle of the table.</summary>
        public NightStep? ActiveStep { get; private set; }
        /// <summary>The calls of this night in order, and the position of the current one (-1: the opening).</summary>
        public List<NightStep> NightSteps { get; } = new List<NightStep>();
        public int NightStepIndex { get; private set; } = -1;
        /// <summary>It is my role's turn: I am alive, my role is the one called, so I may choose. For everybody else the screen is locked.</summary>
        public bool MyTurn => Phase == Phase.Night && IAmAlive && MyRole.HasValue && ActiveStep.HasValue && MafiaMatch.IsAwake(ActiveStep.Value, MyRole.Value);
        /// <summary>The roles of this match and how many of each (public at the start, so a call never gives anybody away).</summary>
        public List<KeyValuePair<Role, int>> RoleCounts { get; } = new List<KeyValuePair<Role, int>>();
        /// <summary>The call of a night step changed (the role that was called, or null when the pause between two calls began). The narrator's director listens to this.</summary>
        public event Action<NightStep?> StepChanged;

        public string DawnVictimId { get; private set; }
        public string DawnDiedId { get; private set; }
        public Role? DawnDiedRole { get; private set; }
        public bool DawnSaved { get; private set; }
        /// <summary>Deaths of the sniper's shot (the target, and the shooter when a citizen was hit): who shot is not told.</summary>
        public List<KeyValuePair<string, Role?>> DawnShotDeaths { get; } = new List<KeyValuePair<string, Role?>>();
        /// <summary>The player the silencer silenced for the day (everybody knows), null if nobody.</summary>
        public string SilencedId { get; private set; }
        public bool IAmSilenced => SilencedId != null && SilencedId == myId && Phase != Phase.Night;
        /// <summary>The doctor's answers, newest last (target id, did the protection stop the mafia).</summary>
        public List<KeyValuePair<string, bool>> DoctorReports { get; } = new List<KeyValuePair<string, bool>>();
        /// <summary>The Avenger's moment: who he is, who he picked so far, and whether the pick is final.</summary>
        public string AvengerId { get; private set; }
        public string AvengeTargetId { get; private set; }
        public Role? AvengeTargetRole { get; private set; }
        public bool AvengeDone { get; private set; }
        public bool IAmTheAvenger => AvengerId != null && AvengerId == myId && Phase == Phase.Avenge;
        /// <summary>The sniper already fired his bullet (this device knows from the host's answer).</summary>
        public bool ShotUsed { get; private set; }

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

        /// <summary>What this device sees of the match, one line at a time (for the test journal).</summary>
        public Action<string> Trace;
        private void T(string line) { Trace?.Invoke(line); }

        public event Action Changed;
        public event Action<Phase> PhaseChanged;
        /// <summary>The host told something that has its own narration, once its data is here: "dawn", "verdict", "avenge.open", "avenge.done", "over" (the narrator's director listens to this).</summary>
        public event Action<string> Moment;
        /// <summary>The match is over and the winner and every role are known (raised once per match).</summary>
        public event Action Finished;
        /// <summary>The match was cancelled (the host was lost and nobody could continue it). Argument: the reason in Arabic.</summary>
        public event Action<string> Aborted;

        public ClientMatchController(string myId, Action<WireMessage> toHost) { this.myId = myId; this.toHost = toHost; }

        public bool IAmAlive => seats.FirstOrDefault(s => s.IsMe)?.Alive == true;
        public bool IAmMafia => MyRole.HasValue && MyRole.Value.IsMafiaTeam();
        public SeatView Seat(string id) => seats.FirstOrDefault(s => s.Id == id);

        // ---------------------------------------------------------------- from the host

        public void Apply(WireMessage m)
        {
            if (m == null) return;
            switch (m.Type)
            {
                case Msg.Start:
                    known.Clear(); SheriffReports.Clear(); Winner = Winner.None; MyRole = null;
                    DoctorReports.Clear(); DawnShotDeaths.Clear(); SilencedId = null; ShotUsed = false; snipeChoice = null;
                    AvengerId = null; AvengeTargetId = null; AvengeTargetRole = null; AvengeDone = false;
                    MatchId = m.Arg(0); roster.Clear(); StartedUtc = DateTime.UtcNow; DurationSeconds = 0;
                    for (int i = 1; i + 1 < m.Args.Length; i += 2) roster[m.Args[i]] = m.Args[i + 1];
                    break;
                case Msg.Role:
                    if (Enum.TryParse(m.Arg(0), out Role r)) { MyRole = r; known[myId] = r; T("my role: " + r + (m.Args.Length > 1 ? " (mafia mates: " + (m.Args.Length - 1) + ")" : "")); }
                    for (int i = 1; i < m.Args.Length; i++)
                    {
                        var mate = m.Args[i].Split('|');                                  // "id" = mafia, "id|Silencer" = the silencer
                        known[mate[0]] = mate.Length > 1 && Enum.TryParse(mate[1], out Role mr) ? mr : Role.Mafia;
                    }
                    RefreshRoles();
                    break;
                case Msg.State: ApplyState(m); break;
                case Msg.Step: ApplyStep(m); return;
                case Msg.Roles:
                    RoleCounts.Clear();
                    for (int i = 0; i + 1 < m.Args.Length; i += 2) if (Enum.TryParse(m.Args[i], out Role rc)) RoleCounts.Add(new KeyValuePair<Role, int>(rc, m.Int(i + 1)));
                    break;
                case Msg.Result:
                    LastResultKind = m.Arg(0);
                    Enum.TryParse(m.Arg(1), out ActionResult res); LastResult = res;
                    T("the host answered my " + LastResultKind + ": " + res);
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
                    T("dawn: victim " + (DawnVictimId ?? "-") + (DawnSaved ? ", saved" : ", died " + (DawnDiedId ?? "-")));
                    DawnDiedRole = Enum.TryParse(m.Arg(3), out Role dr) ? dr : (Role?)null;
                    if (DawnDiedId != null && DawnDiedRole.HasValue) known[DawnDiedId] = DawnDiedRole.Value;
                    SilencedId = Nullable(m.Arg(4));
                    DawnShotDeaths.Clear();
                    for (int i = 5; i + 1 < m.Args.Length; i += 2)
                    {
                        Role? sr = Enum.TryParse(m.Args[i + 1], out Role shotRole) ? shotRole : (Role?)null;
                        DawnShotDeaths.Add(new KeyValuePair<string, Role?>(m.Args[i], sr));
                        if (sr.HasValue) known[m.Args[i]] = sr.Value;
                    }
                    if (MyRole == Role.Sniper && snipeChoice != null) ShotUsed = true;      // the night is over: the bullet I aimed was fired
                    T("dawn: silenced " + (SilencedId ?? "-") + ", shot deaths " + DawnShotDeaths.Count);
                    RefreshRoles();
                    Moment?.Invoke("dawn");
                    break;
                case Msg.DoctorResult:
                    DoctorReports.Add(new KeyValuePair<string, bool>(m.Arg(0), m.Arg(1) == "1"));
                    T("doctor: my protection of " + m.Arg(0) + (m.Arg(1) == "1" ? " saved him" : " was not needed"));
                    break;
                case Msg.Avenge:
                    ApplyAvenge(m);
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
                    T("verdict: " + (VerdictRemovedId ?? "nobody") + " removed, votes " + VerdictVotes);
                    if (VerdictRemovedId != null && VerdictRemovedRole.HasValue) known[VerdictRemovedId] = VerdictRemovedRole.Value;
                    RefreshRoles();
                    Moment?.Invoke("verdict");
                    break;
                case Msg.Abort:
                    Phase = Phase.Lobby; MafiaVotes.Clear(); Tally.Clear();
                    T("match cancelled: " + m.Arg(0));
                    Aborted?.Invoke(m.Arg(0));
                    break;
                case Msg.Over:
                    Enum.TryParse(m.Arg(0), out Winner w); Winner = w;
                    T("match over: " + w + " won; I was " + (MyRole.HasValue ? MyRole.Value.ToString() : "a spectator") + (IAmAlive ? " (alive)" : " (dead)"));
                    DurationSeconds = (DateTime.UtcNow - StartedUtc).TotalSeconds;
                    for (int i = 1; i + 1 < m.Args.Length; i += 2) if (Enum.TryParse(m.Args[i + 1], out Role rr)) known[m.Args[i]] = rr;   // the roles are public now
                    RefreshRoles();
                    Moment?.Invoke("over");
                    Changed?.Invoke();
                    Finished?.Invoke();
                    break;
            }
            Changed?.Invoke();
        }

        private static string Nullable(string s) => string.IsNullOrEmpty(s) ? null : s;

        private void ApplyAvenge(WireMessage m)
        {
            string step = m.Arg(3);
            AvengerId = Nullable(m.Arg(0));
            AvengeTargetId = Nullable(m.Arg(1));
            if (AvengerId != null) known[AvengerId] = Role.Avenger;                 // his card is turned over for everybody
            if (step == "open") { AvengeDone = false; AvengeTargetRole = null; }
            if (step == "done")
            {
                AvengeDone = true;
                AvengeTargetRole = Enum.TryParse(m.Arg(2), out Role tr) ? tr : (Role?)null;
                if (AvengeTargetId != null && AvengeTargetRole.HasValue) known[AvengeTargetId] = AvengeTargetRole.Value;
            }
            T("avenge " + step + ": " + (Seat(AvengerId)?.Name ?? AvengerId) + " -> " + (AvengeTargetId == null ? "nobody" : Seat(AvengeTargetId)?.Name ?? AvengeTargetId));
            RefreshRoles();
            if (step == "open" || step == "done") Moment?.Invoke("avenge." + step);
        }

        private void ApplyStep(WireMessage m)
        {
            NightStep? now = Enum.TryParse(m.Arg(0), out NightStep st) ? st : (NightStep?)null;
            NightSteps.Clear();
            foreach (var part in m.Arg(1).Split(',')) if (Enum.TryParse(part, out NightStep ns)) NightSteps.Add(ns);
            NightStepIndex = m.Int(2, -1);
            bool changed = now != ActiveStep;
            ActiveStep = now;
            T("night: " + (now.HasValue ? now.Value + " is awake" : NightStepIndex < 0 ? "the night opens" : "everybody sleeps") + (MyTurn ? " (my turn)" : ""));
            Changed?.Invoke();
            if (changed) StepChanged?.Invoke(now);
        }

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
                if (Phase == Phase.Night) { MafiaVotes.Clear(); SilencedId = null; snipeChoice = null; ActiveStep = null; NightSteps.Clear(); NightStepIndex = -1; }
                if (Phase == Phase.Voting) { Tally.Clear(); }
                if (Phase == Phase.Day) { SkipCount = 0; }
                T("table: phase " + Phase + " round " + Round + ", I am " + (IAmAlive ? "alive" : "out") + ", " + seats.Count(x => x.Alive && !x.Left) + " alive");
                PhaseChanged?.Invoke(Phase);
            }
        }

        private void RefreshRoles()
        {
            foreach (var s in seats)
            {
                s.Role = known.TryGetValue(s.Id, out var r) ? r : (Role?)null;
                s.IsMate = IAmMafia && s.Role.HasValue && s.Role.Value.IsMafiaTeam() && !s.IsMe;
            }
        }

        /// <summary>
        /// What this device tells the rewards server when the match is over: who played (PlayFab ids), who won, and this player's own role and fate.
        /// Null when there is nothing to report (practice, a spectator, no winner, or too few known PlayFab ids).
        /// </summary>
        /// <summary>The fewest real accounts (PlayFab ids) at the table for a report to be sent: the rewards server's own minimum of players.</summary>
        public const int MinAccountsToReport = 4;

        public MatchReport BuildReport()
        {
            if (Phase != Phase.GameOver || Winner == Winner.None || string.IsNullOrEmpty(MatchId) || !MyRole.HasValue) return null;
            var me = seats.FirstOrDefault(s => s.IsMe);
            if (me == null) return null;
            var ids = new List<string>();
            foreach (var s in seats) if (roster.TryGetValue(s.Id, out var pf) && !string.IsNullOrEmpty(pf)) ids.Add(pf);
            if (ids.Count < MinAccountsToReport) return null;       // bots have no account: a table that is mostly bots earns nothing, and the server would refuse the report anyway
            return new MatchReport { MatchId = MatchId, PlayFabIds = ids, Winner = Winner.ToString(), MyRole = MyRole.Value.ToString(), Survived = me.Alive && !me.Left, Rounds = Math.Max(1, Round), DurationSec = (int)Math.Max(0, DurationSeconds) };
        }

        public void Tick(float dt) { if (SecondsLeft > 0f) SecondsLeft = Math.Max(0f, SecondsLeft - dt); }

        // ---------------------------------------------------------------- to the host

        private string snipeChoice;       // the target I aimed at tonight (null: none yet, or "not tonight")

        private void Act(string kind, string target = "") { T("I ask: " + kind + (string.IsNullOrEmpty(target) ? "" : " on " + (Seat(target)?.Name ?? target))); toHost?.Invoke(new WireMessage(Msg.Act, kind, target ?? "")); }

        public void MafiaPick(string id) => Act("mafia", id);
        /// <summary>The silencer: who may not speak or write tomorrow.</summary>
        public void Silence(string id) => Act("silence", id);
        /// <summary>The sniper: fire his only bullet at <paramref name="id"/>, or pass null to keep it tonight.</summary>
        public void Snipe(string idOrNullToKeepTheBullet) { snipeChoice = string.IsNullOrEmpty(idOrNullToKeepTheBullet) ? null : idOrNullToKeepTheBullet; Act("snipe", idOrNullToKeepTheBullet); }
        /// <summary>The Avenger: who leaves with him; null takes the pick back (it is final a few seconds after the last pick).</summary>
        public void AvengerPick(string idOrNullToTakeItBack) => Act("avenge", idOrNullToTakeItBack);
        public void DoctorSave(string id) => Act("save", id);
        public void SheriffCheck(string id) => Act("check", id);
        public void SkipDiscussion() => Act("skip");
        public void Vote(string idOrNullToAbstain) => Act("vote", idOrNullToAbstain);
        public void Ready() => Act("ready");
    }
}
