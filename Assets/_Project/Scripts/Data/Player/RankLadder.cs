using UnityEngine;

namespace MafiaNight.Data.Player
{
    /// <summary>
    /// The 8 ranks x 3 tiers ladder shown in the UI. Derived from RankPoints, so it needs no new save data.
    /// (The old 7-value <see cref="PlayerRank"/> enum is kept for existing code.)
    /// </summary>
    public static class RankLadder
    {
        public const int RankCount = 8;
        public const int TiersPerRank = 3;
        public const int PointsPerTier = 250;
        public const int MaxPoints = RankCount * TiersPerRank * PointsPerTier;   // 6000

        public struct Step
        {
            public int RankIndex;      // 0-based, 0 = first rank
            public int Tier;           // 1..3
            public float Progress01;   // progress inside the current tier
            public int PointsIntoTier;
            public int PointsToNext;   // 0 at the top
        }

        public static Step FromPoints(int points)
        {
            points = Mathf.Max(0, points);
            int step = Mathf.Min(RankCount * TiersPerRank - 1, points / PointsPerTier);
            bool top = points >= MaxPoints;
            int into = top ? PointsPerTier : points - step * PointsPerTier;
            return new Step
            {
                RankIndex = step / TiersPerRank,
                Tier = step % TiersPerRank + 1,
                Progress01 = top ? 1f : (float)into / PointsPerTier,
                PointsIntoTier = into,
                PointsToNext = top ? 0 : PointsPerTier - into,
            };
        }
    }
}
