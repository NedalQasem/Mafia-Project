using System;
using System.Collections.Generic;

namespace MafiaNight.MatchRules
{
    // The rules of a mafia match as plain C# (no Unity types): the host runs this, the network layer only moves its inputs and results.
    // Rules are the ones drawn in Docs/mockups/match.html (see Docs/09-integration-plan.md, M4).

    public enum Role { Civilian, Mafia, Doctor, Sheriff, Silencer, Sniper, Avenger }

    public enum Phase
    {
        Lobby,
        RoleReveal,   // everybody looks at their own role card
        Night,        // mafia choose a victim, doctor saves, sheriff checks
        Dawn,         // the morning report: who died (or that nobody did)
        Day,          // discussion by voice
        Voting,       // everybody votes for who leaves
        Verdict,      // the result of the vote
        Avenge,       // the Avenger left (killed or voted out): everybody talks to him, then he takes one player with him
        GameOver
    }

    public enum Winner { None, Town, Mafia }

    public enum ActionResult
    {
        Ok,
        WrongPhase,
        NotAPlayer,
        DeadActor,
        WrongRole,
        InvalidTarget,     // unknown, dead, or the actor himself where that is not allowed
        MafiaTeammate,     // the mafia cannot target one of their own
        SelfSaveUsed,      // the doctor already saved himself once
        AlreadyDone,
        AbilityUsed        // the sniper already fired his only shot
    }

    /// <summary>Everything a rule depends on, so numbers can come from data (Title Data / a ScriptableObject) instead of code.</summary>
    public sealed class MatchConfig
    {
        public int MinPlayers = 4;
        public int MaxPlayers = 16;
        /// <summary>One mafia member per this many players (at least one).</summary>
        public int PlayersPerMafia = 4;
        public bool HasDoctor = true;
        public bool HasSheriff = true;
        public int DoctorSelfSaves = 1;
        /// <summary>Show the role of a player who dies or is voted out.</summary>
        public bool RevealRoleOnDeath = true;
        /// <summary>A tie in the vote (or only abstentions) removes nobody.</summary>
        public bool TieRemovesNobody = true;
        /// <summary>The new roles are off unless a match asks for them (<see cref="WithAllRoles"/>): each needs enough players, see <see cref="MafiaMatch.RoleCounts"/>.</summary>
        public bool HasSilencer, HasSniper, HasAvenger;

        public float RoleRevealSeconds = 8f, NightSeconds = 45f, DawnSeconds = 8f, DaySeconds = 120f, VotingSeconds = 45f, VerdictSeconds = 6f;
        /// <summary>The Avenger's thinking time while everybody talks to him (owner's rule, 30 s).</summary>
        public float AvengeSeconds = 30f;
        /// <summary>After he picks, the pick stays changeable this long (a wrong tap can be undone) and then it is final.</summary>
        public float AvengeConfirmSeconds = 3f;

        /// <summary>The rules of the game as it ships: every role in play.</summary>
        public static MatchConfig WithAllRoles() { return new MatchConfig { HasSilencer = true, HasSniper = true, HasAvenger = true }; }

        public float Duration(Phase p)
        {
            switch (p)
            {
                case Phase.RoleReveal: return RoleRevealSeconds;
                case Phase.Night: return NightSeconds;
                case Phase.Dawn: return DawnSeconds;
                case Phase.Day: return DaySeconds;
                case Phase.Voting: return VotingSeconds;
                case Phase.Verdict: return VerdictSeconds;
                case Phase.Avenge: return AvengeSeconds;
                default: return 0f;
            }
        }
    }

    public sealed class MatchPlayer
    {
        public string Id;
        public string Name;
        public Role Role;
        public bool Alive = true;
        /// <summary>The player left the match (disconnected for good). Counts as dead for the rules.</summary>
        public bool Left;
        /// <summary>Whether he was alive when he left: a player who comes back gets that back (a dead player returns dead).</summary>
        public bool AliveWhenLeft;
        public int SelfSavesUsed;
        /// <summary>Shots the sniper has fired (he has one).</summary>
        public int ShotsUsed;

        public bool InPlay => Alive && !Left;
        public bool IsMafia => Role.IsMafiaTeam();
    }

    /// <summary>Source of randomness, injectable so tests (and replays) are deterministic.</summary>
    public interface IRandom { int Next(int maxExclusive); }

    public sealed class SystemRandom : IRandom
    {
        private readonly Random r;
        public SystemRandom(int? seed = null) { r = seed.HasValue ? new Random(seed.Value) : new Random(); }
        public int Next(int maxExclusive) => r.Next(maxExclusive);
    }

    /// <summary>What the morning report says.</summary>
    public sealed class NightResult
    {
        public string VictimId;      // who the mafia picked (null: they did not pick anybody)
        public bool Saved;           // the doctor saved that player
        public string DiedId;        // who actually died of the mafia's pick (null: nobody)
        public Role? DiedRole;       // revealed when the config says so
        /// <summary>Who the silencer silenced for the coming day (null: nobody, or the silenced player died). Everybody hears it, nobody hears who did it.</summary>
        public string SilencedId;
        /// <summary>Deaths that were not the mafia's pick: the sniper's target, and the sniper himself when the target was a citizen. Who shot is never named.</summary>
        public List<DeathInfo> ShotDeaths = new List<DeathInfo>();
        public bool NobodyDied => DiedId == null && ShotDeaths.Count == 0;
        /// <summary>Everybody who died tonight, the mafia's victim first.</summary>
        public IEnumerable<string> AllDeadIds { get { if (DiedId != null) yield return DiedId; foreach (var d in ShotDeaths) yield return d.Id; } }
    }

    public sealed class DeathInfo
    {
        public string Id;
        public Role? Role;           // revealed when the config says so
    }

    /// <summary>What the doctor learns about his night (private to him): did his protection stop the mafia?</summary>
    public sealed class DoctorReport
    {
        public string DoctorId, TargetId;
        public bool Saved;
    }

    /// <summary>The Avenger's choice, in the open.</summary>
    public sealed class AvengeResult
    {
        public string AvengerId;
        public string TargetId;      // null: he picked nobody
        public Role? TargetRole;
    }

    /// <summary>What the sheriff learns about the player he checked (private to him).</summary>
    public sealed class SheriffReport
    {
        public string SheriffId, TargetId;
        public bool IsMafia;
    }

    public sealed class VerdictResult
    {
        public string RemovedId;     // null: nobody (tie / only abstentions / no votes)
        public Role? RemovedRole;
        public int Votes;            // votes the removed (or the leading) player got
        public int Abstained;
        public Dictionary<string, int> Tally = new Dictionary<string, int>();
        public bool NobodyRemoved => RemovedId == null;
    }

    public sealed class MatchSummary
    {
        public Winner Winner;
        public int Rounds;
        public List<MatchPlayer> Players = new List<MatchPlayer>();
    }
}
