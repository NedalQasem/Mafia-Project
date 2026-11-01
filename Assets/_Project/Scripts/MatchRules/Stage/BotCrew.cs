using System;
using System.Collections.Generic;
using System.Linq;

namespace MafiaNight.MatchRules.Stage
{
    /// <summary>A made-up player the admin board keeps (name, level, how talkative, the lines it says).</summary>
    public sealed class BotProfile
    {
        public string Id = "", Name = "", Style = "normal";
        public int Level = 1, Rank;
        public List<string> Lines = new List<string>();
    }

    /// <summary>
    /// The bots of one room, run by the room's host (the room bot for an official room): they come in one by one, sit on a few mic seats and give them back
    /// when real people wait for the mic, and say something now and then. Pure: time, randomness and the outside world (the chat) are passed in,
    /// so the room bot and the tests use the same code. A bot is never a real member: the host lists it through <see cref="StageSync.SetVirtuals"/>.
    /// </summary>
    public sealed class BotCrew
    {
        public const string IdPrefix = "bot:";

        private static readonly string[] Greetings = { "أهلين", "يا هلا فيك", "مرحبا", "نورت", "هلا والله", "أهلا وسهلا" };
        private static readonly string[] Fallback = { "مساء الخير", "شو الأخبار؟", "تمام", "يلا نبلّش", "أنا معكم" };

        private sealed class Live { public BotProfile P; public DateTime NextSay; public string Last = ""; }

        private readonly StageSync stage;
        private readonly Func<DateTime> now;
        private readonly Random rng;
        private readonly Action<VirtualMember, string> say;
        private readonly Func<int> realCount;
        private readonly List<Live> live = new List<Live>();
        private List<BotProfile> pool = new List<BotProfile>();
        private int wanted;
        private bool sit, chat;
        private DateTime nextJoin, nextLeave, nextSeat, nextRotate;

        /// <param name="say">a bot says a line in the room's chat</param>
        /// <param name="realCount">how many real people are in the room now</param>
        public BotCrew(StageSync stage, Func<DateTime> now, Random rng, Action<VirtualMember, string> say, Func<int> realCount)
        {
            this.stage = stage; this.now = now; this.rng = rng; this.say = say; this.realCount = realCount;
        }

        public static string IdOf(BotProfile p) => IdPrefix + p.Id;
        public int Count => live.Count;
        public IEnumerable<string> Ids => live.Select(l => IdOf(l.P));
        public bool IsMine(string id) => live.Any(l => IdOf(l.P) == id);

        /// <summary>What the board wants for this room: how many bots, chosen from <paramref name="available"/> (profiles no other room uses), sitting and chatting or not.</summary>
        public void Configure(IEnumerable<BotProfile> available, int wantedCount, bool sitOnMic, bool chatting)
        {
            pool = available.ToList(); wanted = Math.Max(0, wantedCount); sit = sitOnMic; chat = chatting;
            // a profile the board deleted or gave to another room goes at once; its data may also have changed (name, lines)
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var fresh = pool.FirstOrDefault(p => p.Id == live[i].P.Id);
                if (fresh == null) live.RemoveAt(i); else live[i].P = fresh;
            }
        }

        private TimeSpan Seconds(double lo, double hi) => TimeSpan.FromSeconds(lo + rng.NextDouble() * (hi - lo));

        private TimeSpan SayGap(BotProfile p)
        {
            double lo, hi;
            switch (p.Style) { case "quiet": lo = 240; hi = 600; break; case "chatty": lo = 25; hi = 90; break; default: lo = 90; hi = 240; break; }
            double k = realCount() > 0 ? 1.0 : 3.0;      // alone in the room, they keep the chat alive slowly
            return Seconds(lo * k, hi * k);
        }

        /// <summary>Call about once a second.</summary>
        public void Tick()
        {
            if (!stage.IAmHost) return;
            var t = now();
            Membership(t);
            stage.SetVirtuals(live.Select(l => new VirtualMember { Id = IdOf(l.P), Name = l.P.Name, Rank = l.P.Rank, Level = l.P.Level }));
            Seats(t);
            if (chat) Talk(t);
            Deliver();
        }

        private void Membership(DateTime t)
        {
            if (live.Count < wanted && t >= nextJoin)
            {
                var free = pool.Where(p => live.All(l => l.P.Id != p.Id)).ToList();
                if (free.Count > 0)
                {
                    var p = free[rng.Next(free.Count)];
                    live.Add(new Live { P = p, NextSay = t + Seconds(20, 90) });
                    nextJoin = t + Seconds(2, 8);
                }
            }
            else if (live.Count > wanted && t >= nextLeave)
            {
                // the ones not on a seat leave first
                var order = live.OrderBy(l => stage.Stage.IsSeated(IdOf(l.P)) ? 1 : 0).ThenBy(_ => rng.Next()).ToList();
                var gone = order[0];
                live.Remove(gone);
                nextLeave = t + Seconds(2, 6);
            }
        }

        private void Seats(DateTime t)
        {
            var st = stage.Stage;
            var seated = live.Where(l => st.IsSeated(IdOf(l.P))).ToList();
            if (!sit) { foreach (var l in seated) stage.Down(IdOf(l.P)); return; }

            int target = Math.Min(st.SeatCount / 2, (live.Count + 1) / 2);
            // real people waiting for the mic: a bot steps down so the staff can bring them
            if (st.Queue.Count > 0 && seated.Count > 0 && FreeSeats() < st.Queue.Count) { stage.Down(IdOf(seated[rng.Next(seated.Count)].P)); return; }
            if (seated.Count > target) { stage.Down(IdOf(seated[rng.Next(seated.Count)].P)); return; }
            if (seated.Count < target && st.Queue.Count == 0 && t >= nextSeat)
            {
                var idle = live.Where(l => !st.IsSeated(IdOf(l.P))).ToList();
                if (idle.Count > 0 && FreeSeats() > 0) { stage.Bring(IdOf(idle[rng.Next(idle.Count)].P)); nextSeat = t + Seconds(4, 12); }
                return;
            }
            if (t >= nextRotate)
            {
                nextRotate = t + Seconds(90, 240);
                if (seated.Count > 0 && seated.Count >= target) stage.Down(IdOf(seated[rng.Next(seated.Count)].P));   // the next tick brings another one
            }
        }

        private int FreeSeats()
        {
            var st = stage.Stage; int n = 0;
            for (int i = 0; i < st.SeatCount; i++) if (st.SeatHolder(i) == null && !st.IsLocked(i)) n++;
            return n;
        }

        private void Talk(DateTime t)
        {
            foreach (var l in live)
            {
                if (t < l.NextSay) continue;
                l.NextSay = t + SayGap(l.P);
                var lines = l.P.Lines.Count > 0 ? l.P.Lines : new List<string>(Fallback);
                string line = lines[rng.Next(lines.Count)];
                if (lines.Count > 1 && line == l.Last) line = lines[(lines.IndexOf(line) + 1) % lines.Count];
                l.Last = line;
                say(new VirtualMember { Id = IdOf(l.P), Name = l.P.Name, Rank = l.P.Rank, Level = l.P.Level }, line);
            }
        }

        /// <summary>A real person came in: one bot welcomes him in a few seconds.</summary>
        public void Greet()
        {
            if (!chat || live.Count == 0) return;
            var l = live[rng.Next(live.Count)];
            var p = l.P;
            var who = new VirtualMember { Id = IdOf(p), Name = p.Name, Rank = p.Rank, Level = p.Level };
            l.NextSay = now() + SayGap(p);
            greeting = (who, now() + Seconds(3, 8), Greetings[rng.Next(Greetings.Length)]);
        }

        private (VirtualMember who, DateTime at, string text)? greeting;

        /// <summary>Says a greeting whose time has come (<see cref="Tick"/> calls it).</summary>
        private void Deliver()
        {
            if (greeting is { } g && now() >= g.at && IsMine(g.who.Id)) { greeting = null; say(g.who, g.text); }
            else if (greeting is { } h && now() >= h.at) greeting = null;
        }
    }
}
