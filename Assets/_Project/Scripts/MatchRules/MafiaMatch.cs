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

        public float PhaseSeconds => Config.Duration(Phase);

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
            int doctor = config.HasDoctor && left > 1 ? 1 : 0; left -= doctor;
            int sheriff = config.HasSheriff && left > 1 ? 1 : 0; left -= sheriff;
            return new Dictionary<Role, int> { { Role.Mafia, mafia }, { Role.Doctor, doctor }, { Role.Sheriff, sheriff }, { Role.Civilian, left } };
        }

        /// <summary>Shuffles the players and hands out the roles. Returns false when the player count is not allowed.</summary>
        public bool Start()
        {
            if (Phase != Phase.Lobby || !CanStart(players.Count, Config)) return false;
            var order = Shuffled(players);
            var counts = RoleCounts(players.Count, Config);
            int i = 0;
            foreach (var role in new[] { Role.Mafia, Role.Doctor, Role.Sheriff, Role.Civilian })
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
            var a = Actor(mafiaId, Phase.Night, Role.Mafia, out var err); if (a == null) return err;
            var t = Get(targetId);
            if (t == null || !t.InPlay || t.Id == a.Id) return ActionResult.InvalidTarget;
            if (t.IsMafia) return ActionResult.MafiaTeammate;
            mafiaVotes[a.Id] = t.Id;
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
            foreach (var m in players.Where(p => p.InPlay && p.IsMafia)) if (!mafiaVotes.ContainsKey(m.Id)) return false;
            if (players.Any(p => p.InPlay && p.Role == Role.Doctor) && doctorTarget == null) return false;
            if (players.Any(p => p.InPlay && p.Role == Role.Sheriff) && !sheriffDone) return false;
            return true;
        }

        private NightResult ResolveNight()
        {
            var res = new NightResult();
            // most votes wins, a tie is broken at random
            var tally = mafiaVotes.Values.Where(v => Get(v)?.InPlay == true).GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());
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
                    Winner = CheckWinner();
                    Phase = Phase.Dawn;
                    break;
                case Phase.Dawn:
                    if (Winner != Winner.None) Phase = Phase.GameOver;
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
                    if (Winner != Winner.None) Phase = Phase.GameOver; else BeginNight();
                    break;
            }
        }

        private void BeginNight()
        {
            Round++;
            mafiaVotes.Clear(); doctorTarget = null; sheriffDone = false;
            LastNight = null; LastSheriff = null;
            Phase = Phase.Night;
        }

        /// <summary>A player left for good (closed the app, lost connection too long). He counts as dead and may end the match.</summary>
        public void PlayerLeft(string id)
        {
            var p = Get(id); if (p == null || p.Left) return;
            p.Left = true; p.Alive = false;
            if (Phase != Phase.Lobby && Phase != Phase.GameOver && Winner == Winner.None)
            {
                var w = CheckWinner();
                if (w != Winner.None) { Winner = w; if (Phase == Phase.Night || Phase == Phase.Day || Phase == Phase.Voting) Phase = Phase.GameOver; }
            }
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
    }
}
