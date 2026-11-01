using UnityEngine;

namespace MafiaNight.Data.Player
{
    /// <summary>
    /// Single source of truth for rank thresholds, rank colors and rank-point rewards.
    /// NOTE: once match results are validated server-side (CloudScript), the server must use the same values.
    /// </summary>
    public static class RankRules
    {
        public const int WinPoints = 50;
        public const int LosePoints = -20;
        public const int MvpBonus = 10;

        // Minimum points for each rank, indexed by (int)PlayerRank
        private static readonly int[] Thresholds = { 0, 1000, 2000, 3000, 4000, 5000, 6000 };

        public static PlayerRank GetRankFromPoints(int points)
        {
            for (int i = Thresholds.Length - 1; i > 0; i--)
            {
                if (points >= Thresholds[i]) return (PlayerRank)i;
            }
            return PlayerRank.Bronze;
        }

        /// <summary>Points still needed to reach the next rank (0 at max rank).</summary>
        public static int GetPointsToNextRank(int points)
        {
            int next = (int)GetRankFromPoints(points) + 1;
            return next < Thresholds.Length ? Thresholds[next] - points : 0;
        }

        public static int GetMatchPoints(bool won, bool isMvp) =>
            won ? WinPoints + (isMvp ? MvpBonus : 0) : LosePoints;

        public static Color GetRankColor(PlayerRank rank)
        {
            switch (rank)
            {
                case PlayerRank.Bronze: return new Color(0.8f, 0.5f, 0.2f);
                case PlayerRank.Silver: return new Color(0.75f, 0.75f, 0.75f);
                case PlayerRank.Gold: return new Color(1f, 0.84f, 0f);
                case PlayerRank.Platinum: return new Color(0.3f, 0.8f, 0.8f);
                case PlayerRank.Diamond: return new Color(0.7f, 0.9f, 1f);
                case PlayerRank.Master: return new Color(0.9f, 0.3f, 0.9f);
                case PlayerRank.Grandmaster: return new Color(1f, 0.2f, 0.2f);
                default: return Color.white;
            }
        }
    }
}
