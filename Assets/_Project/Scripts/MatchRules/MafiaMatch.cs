using System;
using System.Collections.Generic;
using System.Linq;

namespace MafiaNight.MatchRules
{
    /// <summary>
    /// One mafia match from role assignment to the winner. Pure rules, no timers and no networking:
    /// the host feeds it player actions and calls <see cref="Advance"/> when a phase ends (timer, or everybody finished).
    /// Flow: RoleReveal, then per round Night, Dawn, Day, Voting, Verdict, until GameOver.
    /// </summary>
    public sealed class MafiaMatch
    {
        private readonly List<MatchPlayer> players = new List<MatchPlayer>();
        private readonly IRandom rng;

        // night
        private readonly Dictionary<string, string> mafiaVotes = new Dictionary<string, string>();   // mafia id -> victim id
        private string doctorTarget;
        private bool sheriffDone;
        private string silenceTarget;
        private string snipeTarget;
        private bool snipeDecided;                 // the sniper chose a target or chose to keep his bullet tonight
        // the Avenger's moment (after a dawn that killed him, or a verdict that removed him)
        private string avengerId;
        private string avengeTarget;
        private Phase avengeReturn = Phase.Day;    // where the match goes when he is done: Day (after dawn) or Night (after the verdict)
        // day / voting
        private readonly HashSet<string> skipVotes = new HashSet<string>();
        private readonly Dictionary<string, string> votes = new Dictionary<string, string>();       // voter id -> target id (null = abstain)

        public MatchConfig Config { get; }
        public Phase Phase { get; private set; } = Phase.Lobby;
        public int Round { get; private set; }
        public Winner Winner { get; private set; }
        public IReadOnlyList<MatchPlayer> Players => players;
        public NightResult LastNight { get; private set; }
        public SheriffReport LastSheriff { get; private set; }
        public VerdictResult LastVerdict { get; private set; }
        /// <summary>What the doctor learned from his last night (private to him).</summary>
        public DoctorReport LastDoctor { get; private set; }
        /// <summary>The Avenger's choice once it is made (null while he is still deciding, or when no Avenger left).</summary>
        public AvengeResult LastAvenge { get; private set; }
        /// <summary>The player the silencer silenced for the current day (null: nobody). He can vote, not speak or write.</summary>
        public string SilencedId { get; private set; }
        /// <summary>The Avenger who is deciding right now (only in <see cref="Phase.Avenge"/>).</summary>
        public string AvengerId => Phase == Phase.Avenge ? avengerId : null;
        /// <summary>Who the Avenger has picked so far (he may change it until the confirmation time ends).</summary>
        public string AvengePick => Phase == Phase.Avenge ? avengeTarget : null;

        public float PhaseSeconds => Config.Duration(Phase);

        /// <summary>The current night's mafia votes (mafia id -> victim id), for showing the mafia what their team picked.</summary>
        public IReadOnlyDictionary<string, string> MafiaVotes => mafiaVotes;
        /// <summary>The current votes (voter id -> target id, null = abstain), for the live tally.</summary>
        public IReadOnlyDictionary<string, string> CurrentVotes => votes;

        public MafiaMatch(MatchConfig config, IEnumerable<KeyValuePair<string, string>> idAndName, IRandom random = null)
        {
            Config = config ?? new MatchConfig();
            rng = random ?? new SystemRandom();
            foreach (var p in idAndName) players.Add(new MatchPlayer { Id = p.Key, Name = p.Value });
        }

        // ---------------------------------------------------------------- setup

        public static bool CanStart(int playerCount, MatchConfig config)
        {
            config = config ?? new MatchConfig();
            return playerCount >= config.MinPlayers && playerCount <= config.MaxPlayers;
        }

        /// <summary>How many of each role a match of this size gets (for the room's "roles" line and for assignment).</summary>
        public static Dictionary<Role, int> RoleCounts(int playerCount, MatchConfig config)
        {
            config = config ?? new MatchConfig();
            int mafia = Math.Max(1, playerCount / Math.Max(1, config.PlayersPerMafia));
            int left = playerCount - mafia;
            int silencer = config.HasSilencer && mafia >= 2 ? 1 : 0; mafia -= silencer;      // the silencer is one of the mafia, never the only one
            // The owner's order of appearance as the table grows: the mafia and all their roles, then the sheriff, the doctor, the Avenger, the sniper.
            int sheriff = config.HasSheriff && left > 1 ? 1 : 0; left -= sheriff;
            int doctor = config.HasDoctor && left > 1 ? 1 : 0; left -= doctor;
            int avenger = config.HasAvenger && left >= 3 ? 1 : 0; left -= avenger;           // a special citizen only while the town keeps at least two plain ones
            int sniper = config.HasSniper && left >= 3 ? 1 : 0; left -= sniper;
            return new Dictionary<Role, int>
            {
                { Role.Mafia, mafia }, { Role.Silencer, silencer }, { Role.Doctor, doctor }, { Role.Sheriff, sheriff },
                { Role.Sniper, sniper }, { Role.Avenger, avenger }, { Role.Civilian, left }
            };
        }

        /// <summary>Shuffles the players and hands out the roles. Returns false when the player count is not allowed.</summary>
        public bool Start()
        {
            if (Phase != Phase.Lobby || !CanStart(players.Count, Config)) return false;
            var order = Shuffled(players);
            var counts = RoleCounts(players.Count, Config);
            int i = 0;
            foreach (var role in new[] { Role.Mafia, Role.Silencer, Role.Sheriff, Role.Doctor, Role.Avenger, Role.Sniper, Role.Civilian })
                for (int k = 0; k < counts[role]; k++) order[i++].Role = role;
            Phase = Phase.RoleReveal;
            return true;
        }

        private List<MatchPlayer> Shuffled(List<MatchPlayer> src)
        {
            var list = new List<MatchPlayer>(src);
            for (int n = list.Count - 1; n > 0; n--) { int k = rng.Next(n + 1); var t = list[k]; list[k] = list[n]; list[n] = t; }
            return list;
        }

        // ---------------------------------------------------------------- queries

        public MatchPlayer Get(string id) => players.FirstOrDefault(p => p.Id == id);
        public Role? RoleOf(string id) => Get(id)?.Role;
        public IEnumerable<MatchPlayer> InPlay => players.Where(p => p.InPlay);
        public int InPlayCount => players.Count(p => p.InPlay);
        public IEnumerable<MatchPlayer> MafiaTeam => players.Where(p => p.IsMafia);

        private MatchPlayer Actor(string id, Phase phase, Role? role, out ActionResult error)
        {
            error = ActionResult.Ok;
            if (Phase != phase) { error = ActionResult.WrongPhase; return null; }
            var p = Get(id);
            if (p == null) { error = ActionResult.NotAPlayer; return null; }
            if (!p.InPlay) { error = ActionResult.DeadActor; return null; }
            if (role.HasValue && p.Role != role.Value) { error = ActionResult.WrongRole; return null; }
            return p;
        }

        // ---------------------------------------------------------------- night

        /// <summary>A mafia member picks (or changes) the night's victim. The mafia cannot pick one of their own.</summary>
        public ActionResult MafiaVote(string mafiaId, string targetId)
        {
            var a = Actor(mafiaId, Phase.Night, null, out var err); if (a == null) return err;
            if (!a.IsMafia) return ActionResult.WrongRole;           // the mafia team: the killers and the silencer
            var t = Get(targetId);
            if (t == null || !t.InPlay || t.Id == a.Id) return ActionResult.InvalidTarget;
            if (t.IsMafia) return ActionResult.MafiaTeammate;
            mafiaVotes[a.Id] = t.Id;
            return ActionResult.Ok;
        }

        /// <summary>
        /// Who decides the night's victim: the assassin mafia (owner's rule). The silencer only advises (his pick is shown to his mates but not counted), unless
        /// no assassin is left in the game: then the kill would never happen, so his pick counts.
        /// </summary>
        public bool IsKillDecider(MatchPlayer p)
        {
            if (p == null || !p.InPlay) return false;
            if (p.Role == Role.Mafia) return true;
            return p.Role == Role.Silencer && !players.Any(x => x.InPlay && x.Role == Role.Mafia);
        }

        /// <summary>The silencer picks who may not speak or write tomorrow (he can still vote). Not one of his own.</summary>
        public ActionResult Silence(string silencerId, string targetId)
        {
            var a = Actor(silencerId, Phase.Night, Role.Silencer, out var err); if (a == null) return err;
            var t = Get(targetId);
            if (t == null || !t.InPlay || t.Id == a.Id) return ActionResult.InvalidTarget;
            if (t.IsMafia) return ActionResult.MafiaTeammate;
            silenceTarget = t.Id;
            return ActionResult.Ok;
        }

        /// <summary>
        /// The sniper fires his only bullet at somebody, or keeps it tonight (<paramref name="targetId"/> null or empty). The doctor cannot stop it.
        /// The target dies; if he was a citizen the sniper dies with him. Nobody is told who shot.
        /// </summary>
        public ActionResult Snipe(string sniperId, string targetId)
        {
            var a = Actor(sniperId, Phase.Night, Role.Sniper, out var err); if (a == null) return err;
            if (a.ShotsUsed >= RoleDefs.Of(Role.Sniper).Uses) return ActionResult.AbilityUsed;
            if (string.IsNullOrEmpty(targetId)) { snipeTarget = null; snipeDecided = true; return ActionResult.Ok; }
            var t = Get(targetId);
            if (t == null || !t.InPlay || t.Id == a.Id) return ActionResult.InvalidTarget;
            snipeTarget = t.Id; snipeDecided = true;
            return ActionResult.Ok;
        }

        /// <summary>
        /// The Avenger (already out of the game) picks the player who leaves with him, in the open. A null target takes the pick back. The host lets the pick stand
        /// once it has not changed for <see cref="MatchConfig.AvengeConfirmSeconds"/>. He may not skip: with no pick when the time ends, a random living player goes with him.
        /// </summary>
        public ActionResult AvengerPick(string actorId, string targetId)
        {
            if (Phase != Phase.Avenge) return ActionResult.WrongPhase;
            if (Get(actorId) == null) return ActionResult.NotAPlayer;
            if (actorId != avengerId) return ActionResult.WrongRole;
            if (string.IsNullOrEmpty(targetId)) { avengeTarget = null; return ActionResult.Ok; }
            var t = Get(targetId);
            if (t == null || !t.InPlay) return ActionResult.InvalidTarget;
            avengeTarget = t.Id;
            return ActionResult.Ok;
        }

        /// <summary>The doctor picks (or changes) who is protected tonight; himself only <see cref="MatchConfig.DoctorSelfSaves"/> times.</summary>
        public ActionResult DoctorSave(string doctorId, string targetId)
        {
            var a = Actor(doctorId, Phase.Night, Role.Doctor, out var err); if (a == null) return err;
            var t = Get(targetId);
            if (t == null || !t.InPlay) return ActionResult.InvalidTarget;
            if (t.Id == a.Id && a.SelfSavesUsed >= Config.DoctorSelfSaves) return ActionResult.SelfSaveUsed;
            doctorTarget = t.Id;
            return ActionResult.Ok;
        }

        /// <summary>The sheriff checks one player per night and gets the answer straight away.</summary>
        public ActionResult SheriffCheck(string sheriffId, string targetId, out SheriffReport report)
        {
            report = null;
            var a = Actor(sheriffId, Phase.Night, Role.Sheriff, out var err); if (a == null) return err;
            if (sheriffDone) return ActionResult.AlreadyDone;
            var t = Get(targetId);
            if (t == null || !t.InPlay || t.Id == a.Id) return ActionResult.InvalidTarget;
            sheriffDone = true;
            report = LastSheriff = new SheriffReport { SheriffId = a.Id, TargetId = t.Id, IsMafia = t.IsMafia };
            return ActionResult.Ok;
        }

        /// <summary>Everybody who acts at night has acted, so the host may end the night early.</summary>
        public bool NightActionsComplete()
        {
            if (Phase != Phase.Night) return false;
            foreach (var m in players.Where(p => p.InPlay && IsKillDecider(p))) if (!mafiaVotes.ContainsKey(m.Id)) return false;     // the silencer only advises: the assassins decide
            if (players.Any(p => p.InPlay && p.Role == Role.Doctor) && doctorTarget == null) return false;
            if (players.Any(p => p.InPlay && p.Role == Role.Sheriff) && !sheriffDone) return false;
            if (players.Any(p => p.InPlay && p.Role == Role.Silencer) && silenceTarget == null) return false;
            if (players.Any(p => p.InPlay && p.Role == Role.Sniper && p.ShotsUsed < RoleDefs.Of(Role.Sniper).Uses) && !snipeDecided) return false;   // shoot, or say "not tonight"
            return true;
        }

        private DeathInfo Death(MatchPlayer p) { return new DeathInfo { Id = p.Id, Role = Config.RevealRoleOnDeath ? p.Role : (Role?)null }; }

        /// <summary>The night, in a fixed order: silence, protect, the mafia's kill, then the sniper's shot (nothing protects against it).</summary>
        private NightResult ResolveNight()
        {
            var res = new NightResult();
            var shooter = players.FirstOrDefault(p => p.InPlay && p.Role == Role.Sniper && p.ShotsUsed < RoleDefs.Of(Role.Sniper).Uses);   // he fires even if the mafia kill him this same night
            string shotAt = shooter != null && snipeDecided ? snipeTarget : null;
            // most votes wins, a tie is broken at random
            var tally = mafiaVotes.Where(kv => IsKillDecider(Get(kv.Key)) && Get(kv.Value)?.InPlay == true).GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.Count());
            if (tally.Count > 0)
            {
                int top = tally.Values.Max();
                var leaders = tally.Where(kv => kv.Value == top).Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToList();
                res.VictimId = leaders[rng.Next(leaders.Count)];
            }
            var doctor = players.FirstOrDefault(p => p.InPlay && p.Role == Role.Doctor);
            if (doctor != null && doctorTarget == doctor.Id) doctor.SelfSavesUsed++;
            res.Saved = res.VictimId != null && doctorTarget == res.VictimId;
            if (res.VictimId != null && !res.Saved)
            {
                var v = Get(res.VictimId); v.Alive = false;
                res.DiedId = v.Id;
                if (Config.RevealRoleOnDeath) res.DiedRole = v.Role;
            }
            if (doctor != null && doctorTarget != null)
                LastDoctor = new DoctorReport { DoctorId = doctor.Id, TargetId = doctorTarget, Saved = res.Saved };

            if (shotAt != null)
            {
                shooter.ShotsUsed++;
                var target = Get(shotAt);
                if (target != null && target.Id != shooter.Id)
                {
                    if (target.InPlay) { target.Alive = false; res.ShotDeaths.Add(Death(target)); }
                    if (!target.IsMafia && shooter.InPlay) { shooter.Alive = false; res.ShotDeaths.Add(Death(shooter)); }   // a citizen was hit: the sniper goes with him
                }
            }
            if (silenceTarget != null && Get(silenceTarget)?.InPlay == true) res.SilencedId = silenceTarget;
            return res;
        }

        // ---------------------------------------------------------------- day and voting

        /// <summary>A player asks to skip the discussion. Returns true once more than half of the living agree.</summary>
        public bool RequestSkip(string playerId)
        {
            if (Phase != Phase.Day || Get(playerId)?.InPlay != true) return false;
            skipVotes.Add(playerId);
            return SkipReached;
        }

        public int SkipCount => skipVotes.Count(id => Get(id)?.InPlay == true);
        public bool SkipReached => Phase == Phase.Day && SkipCount * 2 > InPlayCount;

        /// <summary>A living player votes for who leaves (targetId null = abstain). Changing the vote is allowed until the vote ends.</summary>
        public ActionResult Vote(string voterId, string targetId)
        {
            var a = Actor(voterId, Phase.Voting, null, out var err); if (a == null) return err;
            if (targetId != null)
            {
                var t = Get(targetId);
                if (t == null || !t.InPlay || t.Id == a.Id) return ActionResult.InvalidTarget;
            }
            votes[a.Id] = targetId;
            return ActionResult.Ok;
        }

        public bool AllVoted() => Phase == Phase.Voting && players.Where(p => p.InPlay).All(p => votes.ContainsKey(p.Id));

        public Dictionary<string, int> CurrentTally() =>
            votes.Where(kv => kv.Value != null && Get(kv.Key)?.InPlay == true && Get(kv.Value)?.InPlay == true)
                 .GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.Count());

        private VerdictResult ResolveVote()
        {
            var res = new VerdictResult { Tally = CurrentTally() };
            res.Abstained = InPlayCount - res.Tally.Values.Sum();
            if (res.Tally.Count == 0) return res;
            int top = res.Tally.Values.Max();
            var leaders = res.Tally.Where(kv => kv.Value == top).Select(kv => kv.Key).ToList();
            res.Votes = top;
            if (leaders.Count > 1 && Config.TieRemovesNobody) return res;
            string id = leaders.Count == 1 ? leaders[0] : leaders.OrderBy(x => x, StringComparer.Ordinal).ElementAt(rng.Next(leaders.Count));
            var p = Get(id); p.Alive = false;
            res.RemovedId = id;
            if (Config.RevealRoleOnDeath) res.RemovedRole = p.Role;
            return res;
        }

        // ---------------------------------------------------------------- flow

        /// <summary>Ends the current phase (its timer ran out, or everybody finished) and starts the next one.</summary>
        public void Advance()
        {
            switch (Phase)
            {
                case Phase.RoleReveal: BeginNight(); break;
                case Phase.Night:
                    LastNight = ResolveNight();
                    SilencedId = LastNight.SilencedId;
                    Winner = CheckWinner();
                    Phase = Phase.Dawn;
                    break;
                case Phase.Dawn:
                    if (Winner != Winner.None) Phase = Phase.GameOver;
                    else if (TryBeginAvenge(LastNight.AllDeadIds, Phase.Day)) { }
                    else { skipVotes.Clear(); Phase = Phase.Day; }
                    break;
                case Phase.Day:
                    votes.Clear();
                    Phase = Phase.Voting;
                    break;
                case Phase.Voting:
                    LastVerdict = ResolveVote();
                    Winner = CheckWinner();
                    Phase = Phase.Verdict;
                    break;
                case Phase.Verdict:
                    if (Winner != Winner.None) Phase = Phase.GameOver;
                    else if (LastVerdict?.RemovedId != null && TryBeginAvenge(new[] { LastVerdict.RemovedId }, Phase.Night)) { }
                    else BeginNight();
                    break;
                case Phase.Avenge:
                    LastAvenge = ResolveAvenge();
                    Winner = CheckWinner();
                    if (Winner != Winner.None) Phase = Phase.GameOver;
                    else if (avengeReturn == Phase.Day) { skipVotes.Clear(); Phase = Phase.Day; }
                    else BeginNight();
                    avengerId = null; avengeTarget = null;
                    break;
            }
        }

        /// <summary>When the Avenger is among those who just left the game, the match stops for his moment before it goes on to <paramref name="then"/>.</summary>
        private bool TryBeginAvenge(IEnumerable<string> leftIds, Phase then)
        {
            var avenger = players.FirstOrDefault(p => p.Role == Role.Avenger);
            if (avenger == null || avenger.Left || !leftIds.Contains(avenger.Id)) return false;
            avengerId = avenger.Id; avengeTarget = null; avengeReturn = then; LastAvenge = null;
            Phase = Phase.Avenge;
            return true;
        }

        private AvengeResult ResolveAvenge()
        {
            var res = new AvengeResult { AvengerId = avengerId };
            var t = avengeTarget == null ? null : Get(avengeTarget);
            if ((t == null || !t.InPlay) && Get(avengerId)?.Left == false)      // the owner's rule: he must choose; the time ran out, so chance chooses
            {
                var living = InPlay.ToList();
                t = living.Count > 0 ? living[rng.Next(living.Count)] : null;
            }
            if (t != null && t.InPlay)
            {
                t.Alive = false;
                res.TargetId = t.Id;
                if (Config.RevealRoleOnDeath) res.TargetRole = t.Role;
            }
            return res;
        }

        /// <summary>The Avenger left the match for good while he was deciding: his moment is over (the host ends it at once).</summary>
        public bool AvengerGone => Phase == Phase.Avenge && Get(avengerId)?.Left == true;

        private void BeginNight()
        {
            Round++;
            mafiaVotes.Clear(); doctorTarget = null; sheriffDone = false;
            silenceTarget = null; snipeTarget = null; snipeDecided = false; SilencedId = null;
            LastNight = null; LastSheriff = null; LastDoctor = null;
            Phase = Phase.Night;
        }

        /// <summary>A player left for good (closed the app, lost connection too long). He counts as dead and may end the match.</summary>
        public void PlayerLeft(string id)
        {
            var p = Get(id); if (p == null || p.Left) return;
            p.AliveWhenLeft = p.Alive;
            p.Left = true; p.Alive = false;
            if (Phase != Phase.Lobby && Phase != Phase.GameOver && Winner == Winner.None)
            {
                var w = CheckWinner();
                if (w != Winner.None) { Winner = w; if (Phase == Phase.Night || Phase == Phase.Day || Phase == Phase.Voting || Phase == Phase.Avenge) Phase = Phase.GameOver; }
            }
        }

        /// <summary>
        /// A player who left comes back (same account) while the match still runs: he gets his seat, his role and his life (or death) back.
        /// False when he never played, did not leave, or the match is not running any more.
        /// </summary>
        public bool PlayerReturned(string id)
        {
            var p = Get(id);
            if (p == null || !p.Left || Phase == Phase.Lobby || Phase == Phase.GameOver || Winner != Winner.None) return false;
            p.Left = false; p.Alive = p.AliveWhenLeft;
            return true;
        }

        public Winner CheckWinner()
        {
            int mafia = players.Count(p => p.InPlay && p.IsMafia);
            int others = players.Count(p => p.InPlay && !p.IsMafia);
            if (mafia == 0) return Winner.Town;
            if (mafia >= others) return Winner.Mafia;
            return Winner.None;
        }

        public MatchSummary Summary() => new MatchSummary { Winner = Winner, Rounds = Round, Players = new List<MatchPlayer>(players) };

        // ---------------------------------------------------------------- snapshot (so another device can take over as host)

        private const string SnapshotVersion = "m3";

        /// <summary>Everything the match knows, as plain text pieces. <see cref="Restore"/> builds an identical match from it.</summary>
        public List<string> Export()
        {
            var o = new List<string> { SnapshotVersion, Phase.ToString(), Round.ToString(), Winner.ToString(), doctorTarget ?? "", sheriffDone ? "1" : "0" };
            o.Add(silenceTarget ?? ""); o.Add(snipeTarget ?? ""); o.Add(snipeDecided ? "1" : "0"); o.Add(SilencedId ?? "");
            o.Add(avengerId ?? ""); o.Add(avengeTarget ?? ""); o.Add(avengeReturn.ToString());
            o.Add(players.Count.ToString());
            foreach (var p in players) { o.Add(p.Id); o.Add(p.Name ?? ""); o.Add(p.Role.ToString()); o.Add(p.Alive ? "1" : "0"); o.Add(p.Left ? "1" : "0"); o.Add(p.AliveWhenLeft ? "1" : "0"); o.Add(p.SelfSavesUsed.ToString()); o.Add(p.ShotsUsed.ToString()); }
            o.Add(mafiaVotes.Count.ToString());
            foreach (var kv in mafiaVotes) { o.Add(kv.Key); o.Add(kv.Value); }
            o.Add(skipVotes.Count.ToString());
            foreach (var id in skipVotes) o.Add(id);
            o.Add(votes.Count.ToString());
            foreach (var kv in votes) { o.Add(kv.Key); o.Add(kv.Value ?? ""); }
            if (LastNight == null) o.Add("0");
            else
            {
                o.Add("1"); o.Add(LastNight.VictimId ?? ""); o.Add(LastNight.Saved ? "1" : "0"); o.Add(LastNight.DiedId ?? ""); o.Add(LastNight.DiedRole?.ToString() ?? "");
                o.Add(LastNight.SilencedId ?? ""); o.Add(LastNight.ShotDeaths.Count.ToString());
                foreach (var d in LastNight.ShotDeaths) { o.Add(d.Id); o.Add(d.Role?.ToString() ?? ""); }
            }
            if (LastSheriff == null) o.Add("0");
            else { o.Add("1"); o.Add(LastSheriff.SheriffId); o.Add(LastSheriff.TargetId); o.Add(LastSheriff.IsMafia ? "1" : "0"); }
            if (LastDoctor == null) o.Add("0");
            else { o.Add("1"); o.Add(LastDoctor.DoctorId); o.Add(LastDoctor.TargetId); o.Add(LastDoctor.Saved ? "1" : "0"); }
            if (LastAvenge == null) o.Add("0");
            else { o.Add("1"); o.Add(LastAvenge.AvengerId); o.Add(LastAvenge.TargetId ?? ""); o.Add(LastAvenge.TargetRole?.ToString() ?? ""); }
            if (LastVerdict == null) o.Add("0");
            else
            {
                o.Add("1"); o.Add(LastVerdict.RemovedId ?? ""); o.Add(LastVerdict.RemovedRole?.ToString() ?? ""); o.Add(LastVerdict.Votes.ToString()); o.Add(LastVerdict.Abstained.ToString());
                o.Add(LastVerdict.Tally.Count.ToString());
                foreach (var kv in LastVerdict.Tally) { o.Add(kv.Key); o.Add(kv.Value.ToString()); }
            }
            return o;
        }

        /// <summary>Rebuilds a match from <see cref="Export"/>; null if the data is not a valid snapshot.</summary>
        public static MafiaMatch Restore(MatchConfig config, IList<string> data, IRandom random = null)
        {
            try
            {
                int i = 0;
                string Next() => data[i++];
                int NextInt() => int.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture);
                bool NextBool() => Next() == "1";
                Role? NextRole() { var s = Next(); return s.Length == 0 ? (Role?)null : (Role)Enum.Parse(typeof(Role), s); }
                string NullIfEmpty(string s) => s.Length == 0 ? null : s;

                if (data == null || data.Count < 8 || Next() != SnapshotVersion) return null;
                var m = new MafiaMatch(config, new KeyValuePair<string, string>[0], random);
                m.Phase = (Phase)Enum.Parse(typeof(Phase), Next());
                m.Round = NextInt();
                m.Winner = (Winner)Enum.Parse(typeof(Winner), Next());
                m.doctorTarget = NullIfEmpty(Next());
                m.sheriffDone = NextBool();
                m.silenceTarget = NullIfEmpty(Next()); m.snipeTarget = NullIfEmpty(Next()); m.snipeDecided = NextBool(); m.SilencedId = NullIfEmpty(Next());
                m.avengerId = NullIfEmpty(Next()); m.avengeTarget = NullIfEmpty(Next()); m.avengeReturn = (Phase)Enum.Parse(typeof(Phase), Next());
                int n = NextInt();
                for (int k = 0; k < n; k++)
                    m.players.Add(new MatchPlayer { Id = Next(), Name = Next(), Role = (Role)Enum.Parse(typeof(Role), Next()), Alive = NextBool(), Left = NextBool(), AliveWhenLeft = NextBool(), SelfSavesUsed = NextInt(), ShotsUsed = NextInt() });
                n = NextInt(); for (int k = 0; k < n; k++) { string a = Next(), b = Next(); m.mafiaVotes[a] = b; }
                n = NextInt(); for (int k = 0; k < n; k++) m.skipVotes.Add(Next());
                n = NextInt(); for (int k = 0; k < n; k++) { string a = Next(), b = Next(); m.votes[a] = NullIfEmpty(b); }
                if (NextBool())
                {
                    var night = new NightResult { VictimId = NullIfEmpty(Next()), Saved = NextBool(), DiedId = NullIfEmpty(Next()), DiedRole = NextRole(), SilencedId = NullIfEmpty(Next()) };
                    n = NextInt(); for (int k = 0; k < n; k++) night.ShotDeaths.Add(new DeathInfo { Id = Next(), Role = NextRole() });
                    m.LastNight = night;
                }
                if (NextBool()) m.LastSheriff = new SheriffReport { SheriffId = Next(), TargetId = Next(), IsMafia = NextBool() };
                if (NextBool()) m.LastDoctor = new DoctorReport { DoctorId = Next(), TargetId = Next(), Saved = NextBool() };
                if (NextBool()) m.LastAvenge = new AvengeResult { AvengerId = Next(), TargetId = NullIfEmpty(Next()), TargetRole = NextRole() };
                if (NextBool())
                {
                    var v = new VerdictResult { RemovedId = NullIfEmpty(Next()), RemovedRole = NextRole(), Votes = NextInt(), Abstained = NextInt() };
                    n = NextInt(); for (int k = 0; k < n; k++) { string a = Next(); v.Tally[a] = NextInt(); }
                    m.LastVerdict = v;
                }
                return m;
            }
            catch (Exception) { return null; }
        }
    }
}
