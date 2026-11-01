using System;
using System.Collections.Generic;

namespace MafiaNight.UI.Kit
{
    [Serializable]
    public class LeaderEntry
    {
        public string name;
        public int rank;       // 0..7
        public int value;
        public int delta;      // movement since yesterday / last week
        public bool me;
    }

    /// <summary>What the leaderboards screen shows for one period and category (filled by the app from the server).</summary>
    public class LeaderBoardView
    {
        public List<LeaderEntry> list = new List<LeaderEntry>();   // best first, at most twelve
        public LeaderEntry me;                                    // this player (value 0 when not on the board)
        public int mePosition;                                    // 0 = not ranked yet
        public int need;                                          // missing amount to enter the first ten (0 = in)
        public float fill;                                        // 0..1 progress towards the tenth place
        public int resetInSeconds;
        public bool failed;                                       // the server could not be reached
        public string prizeText;                                  // what the first places win when the period is over (null: no prizes)
    }

    /// <summary>One plaque or record card of the hall of fame; Name == null means nobody holds it yet.</summary>
    public class LeaderWallItem { public string name; public string value; }

    public class LeaderWallView
    {
        public LeaderWallItem godfather;                          // value = points
        public LeaderWallItem[] records = new LeaderWallItem[6];  // streak, games, hours, mafia wins, sheriff wins, survived
    }

    /// <summary>Demo leaderboards (daily / weekly, eight categories), used only when no app is attached to the screen (the editor preview).</summary>
    public static class LeaderboardData
    {
        public static readonly string[] Categories = { "النقاط", "الفوز", "أفضل مافيا", "أفضل محقق", "الأكثر سهراً", "الأكثر مباريات", "الناجي", "سلسلة الفوز" };
        public static readonly string[] Units = { "نقطة", "فوز", "فوز كمافيا", "فوز كمحقق", "دقيقة", "مباراة", "نجاة", "فوز متتالٍ" };
        private static readonly int[,] Base = { { 3200, 18400 }, { 14, 96 }, { 7, 42 }, { 11, 63 }, { 9, 58 }, { 26, 171 }, { 48, 340 }, { 8, 17 } };
        private static readonly string[] Names = { "أبو فارس", "أم سعيد", "البيك", "لينا", "الحج حسن", "سارة", "مازن", "أبو خليل", "ريما", "الدكتور", "نور", "أبو جاسم", "هدى", "الأستاذ", "سمير", "جود" };
        private static readonly int[] Ranks = { 7, 6, 6, 5, 5, 4, 4, 3, 3, 3, 2, 2, 2, 1, 1, 1 };

        public static List<LeaderEntry> Make(int period, int cat)
        {
            var rnd = new Random(cat * 31 + period * 7 + 5);
            var order = new List<int>();
            for (int i = 0; i < Names.Length; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
            var list = new List<LeaderEntry>();
            for (int i = 0; i < 12; i++)
            {
                int idx = order[i];
                list.Add(new LeaderEntry
                {
                    name = Names[idx], rank = Ranks[idx],
                    value = Math.Max(1, (int)(Base[cat, period] * (1.0 - i * 0.07) * (0.94 + rnd.NextDouble() * 0.08))),
                    delta = (int)Math.Round((rnd.NextDouble() - 0.45) * 7)
                });
            }
            return list;
        }

        public static LeaderEntry Me(int period, int cat) =>
            new LeaderEntry { name = "أبو سالم (أنت)", rank = 2, value = (int)(Base[cat, period] * 0.18), delta = 3, me = true };
    }
}
