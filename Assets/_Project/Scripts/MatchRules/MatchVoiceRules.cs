using System.Collections.Generic;
using System.Linq;

namespace MafiaNight.MatchRules
{
    /// <summary>Who may talk and whom a player hears at a moment of the match.</summary>
    public sealed class VoicePlan
    {
        /// <summary>The microphone may send.</summary>
        public bool MicAllowed = true;
        /// <summary>Only these players are heard; null = everybody.</summary>
        public HashSet<string> HearOnly;
    }

    /// <summary>
    /// The voice rules of a match, as pure logic. There are two groups in the room while a match runs: the living players and the spectators
    /// (the dead, people who came in after the start). The players hear only the living players, so a spectator can talk as much as he likes and no player hears him;
    /// the spectators talk among themselves and hear the players' discussion by day (not the mafia's talk at night).
    /// Night: the town is silent and hears nothing, the mafia talk to each other.
    /// Each device applies its own plan, so a modified client could ignore it; the server-side check comes later.
    /// </summary>
    public static class MatchVoiceRules
    {
        /// <param name="alivePlayerIds">the players still in the match (ids as the room knows them), this one included when alive</param>
        /// <param name="memberIds">everybody in the room, this one included</param>
        /// <param name="myId">this device's id</param>
        /// <param name="silenced">the silencer silenced this player for the day: he hears everything but his mic stays shut (he can still vote)</param>
        /// <param name="avengerId">in the Avenge phase, the Avenger who is deciding: he is out of the game, but he talks with the players and they hear him</param>
        public static VoicePlan For(Phase phase, Role? role, bool alive, IEnumerable<string> mafiaMateIds, IEnumerable<string> alivePlayerIds, IEnumerable<string> memberIds, string myId, bool silenced = false, string avengerId = null)
        {
            var plan = new VoicePlan();
            if (phase == Phase.Lobby || phase == Phase.RoleReveal || phase == Phase.GameOver) return plan;      // before and after the match everybody talks together

            var players = new HashSet<string>(alivePlayerIds ?? Enumerable.Empty<string>());
            if (phase == Phase.Avenge && !string.IsNullOrEmpty(avengerId))
            {
                players.Add(avengerId);                  // the players hear him, and he is one of them for this talk
                if (myId == avengerId) alive = true;
            }
            bool night = phase == Phase.Night;

            if (!alive)
            {
                // a spectator: free to talk (to the other spectators), hears the players' discussion by day, and only the spectators at night
                if (night)
                {
                    var spectators = new HashSet<string>((memberIds ?? Enumerable.Empty<string>()).Where(id => id != myId && !players.Contains(id)));
                    plan.HearOnly = spectators;
                }
                return plan;
            }

            if (night)
            {
                if (role == Role.Mafia) plan.HearOnly = new HashSet<string>(mafiaMateIds ?? Enumerable.Empty<string>());
                else { plan.MicAllowed = false; plan.HearOnly = new HashSet<string>(); }
                return plan;
            }

            players.Remove(myId);
            plan.HearOnly = players;          // by day a player hears the living players only: the spectators are a separate group
            if (silenced) plan.MicAllowed = false;
            return plan;
        }
    }
}
