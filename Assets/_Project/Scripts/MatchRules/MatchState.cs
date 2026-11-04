namespace MafiaNight.MatchRules
{
    /// <summary>Where one player stands against the match of his room. One value, decided in one place, so no screen has to guess.</summary>
    public enum MatchState
    {
        /// <summary>No match is running (the room waits, or a match was cancelled).</summary>
        Idle,
        /// <summary>A match runs and this player is a living player of it.</summary>
        Playing,
        /// <summary>A match runs and this player only watches: he died, came in after the start, or was removed.</summary>
        Watching,
        /// <summary>The match is over and its result is on the table.</summary>
        Finished,
    }

    public static class MatchStates
    {
        public static MatchState Of(Phase phase, bool iAmAlive)
        {
            if (phase == Phase.Lobby) return MatchState.Idle;
            if (phase == Phase.GameOver) return MatchState.Finished;
            return iAmAlive ? MatchState.Playing : MatchState.Watching;
        }

        /// <summary>
        /// The table screens open by themselves only when a match begins (or when a late member first hears of it). Every later change of phase (night, day...)
        /// must not drag a player who went back to the room to the table again: he opens it himself with the match button.
        /// </summary>
        public static bool OpensTable(Phase previous, Phase next)
        {
            bool wasOutside = previous == Phase.Lobby || previous == Phase.GameOver;
            bool nextIsMatch = next != Phase.Lobby && next != Phase.GameOver;
            return wasOutside && nextIsMatch;
        }
    }
}
