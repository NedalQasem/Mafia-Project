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
    /// The voice rules of a match, as pure logic: night is silent except the mafia who talk to each other, the dead only listen
    /// (and hear nothing at night). Each device applies its own plan, so a modified client could ignore it; the server-side check comes later.
    /// </summary>
    public static class MatchVoiceRules
    {
        public static VoicePlan For(Phase phase, Role? role, bool alive, IEnumerable<string> mafiaMateIds)
        {
            var plan = new VoicePlan();
            if (phase == Phase.Lobby || phase == Phase.RoleReveal || phase == Phase.GameOver) return plan;

            bool night = phase == Phase.Night;
            if (!alive)
            {
                plan.MicAllowed = false;
                if (night) plan.HearOnly = new HashSet<string>();
                return plan;
            }
            if (night)
            {
                if (role == Role.Mafia) plan.HearOnly = new HashSet<string>(mafiaMateIds ?? Enumerable.Empty<string>());
                else { plan.MicAllowed = false; plan.HearOnly = new HashSet<string>(); }
            }
            return plan;
        }
    }
}
