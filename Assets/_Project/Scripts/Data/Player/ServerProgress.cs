using System;

namespace MafiaNight.Data.Player
{
    /// <summary>
    /// Level, XP, rank points and game counts as the rewards server writes them (PlayFab read-only user data, key "Progress").
    /// The game only reads this; the player cannot change it. Field names match Server/rewards-api/src/rewards.js.
    /// </summary>
    [Serializable]
    public class ServerProgress
    {
        public int level = 1, xp, xpToNext = 150, rankPoints;
        public int games, wins, losses, mafiaGames, mafiaWins, citizenGames, citizenWins;
        public string lastReward;
    }
}
