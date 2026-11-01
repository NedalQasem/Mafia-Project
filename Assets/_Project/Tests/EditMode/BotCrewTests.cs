using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.MatchRules.Net;
using MafiaNight.MatchRules.Stage;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    public class BotCrewTests
    {
        private DateTime clock = new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc);
        private readonly List<string> said = new List<string>();
        private readonly Dictionary<string, StageSync> devices = new Dictionary<string, StageSync>();
        private int real;

        [SetUp]
        public void Reset() { clock = new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc); said.Clear(); devices.Clear(); real = 0; }

        private StageSync Host(int seats = 8, MicMode mode = MicMode.Request)
        {
            var host = new StageSync("keeper", () => "keeper", () => devices.Keys.ToList(),
                (to, m) => { if (devices.TryGetValue(to, out var d)) d.Receive("keeper", WireMessage.FromBytes(m.ToBytes())); }, null, seats, mode);
            devices["keeper"] = host;
            return host;
        }

        private StageSync Member(string id, int seats = 8, MicMode mode = MicMode.Request)
        {
            var m = new StageSync(id, () => "keeper", () => devices.Keys.ToList(),
                (to, msg) => { if (devices.TryGetValue(to, out var d)) d.Receive(id, WireMessage.FromBytes(msg.ToBytes())); }, null, seats, mode);
            devices[id] = m;
            devices["keeper"].MemberJoined(id);
            return m;
        }

        private static List<BotProfile> Profiles(int n) => Enumerable.Range(1, n).Select(i => new BotProfile { Id = "b" + i, Name = "بوت" + i, Level = 5 + i, Rank = 1, Style = "chatty", Lines = new List<string> { "أهلين", "تمام", "شو الأخبار" } }).ToList();

        private BotCrew Crew(StageSync host, int seed = 1) => new BotCrew(host, () => clock, new Random(seed), (who, text) => said.Add(who.Id + ":" + text), () => real);

        private void Run(BotCrew crew, int seconds) { for (int i = 0; i < seconds; i++) { clock = clock.AddSeconds(1); crew.Tick(); } }

        [Test]
        public void Bots_come_in_one_by_one_until_the_wanted_number()
        {
            var host = Host(); var crew = Crew(host);
            crew.Configure(Profiles(10), 5, true, true);
            Run(crew, 1);
            Assert.AreEqual(1, crew.Count);
            Run(crew, 120);
            Assert.AreEqual(5, crew.Count);
            Assert.AreEqual(5, host.Virtuals.Count);
        }

        [Test]
        public void Every_member_sees_the_bots_the_host_lists()
        {
            var host = Host(); var crew = Crew(host);
            var me = Member("p1");
            crew.Configure(Profiles(4), 3, false, false);
            Run(crew, 60);
            Assert.AreEqual(3, me.Virtuals.Count);
            var late = Member("p2");        // somebody entering later gets the list at once
            Assert.AreEqual(3, late.Virtuals.Count);
            Assert.AreEqual("بوت1", host.Virtuals.First(v => v.Id == "bot:b1").Name);
        }

        [Test]
        public void Bots_take_at_most_half_of_the_seats_and_never_the_big_one()
        {
            var host = Host(8); var crew = Crew(host);
            crew.Configure(Profiles(12), 12, true, false);
            Run(crew, 300);
            int seated = crew.Ids.Count(id => host.Stage.IsSeated(id));
            Assert.LessOrEqual(seated, 4);
            Assert.Greater(seated, 0);
            Assert.IsNull(host.Stage.HeadHolder);
        }

        [Test]
        public void A_bot_steps_down_when_a_real_person_waits_and_no_seat_is_free()
        {
            var host = Host(2); var crew = Crew(host);
            var p = Member("p1", 2);
            crew.Configure(Profiles(6), 6, true, false);
            Run(crew, 200);
            Assert.AreEqual(1, crew.Ids.Count(id => host.Stage.IsSeated(id)));    // half of two seats
            host.Stage.Hand("p1", true);                                           // he raises his hand
            host.Stage.Bring("keeper", "bot:b1");                                  // (make sure a bot is on the second seat)
            Run(crew, 20);
            Assert.GreaterOrEqual(Enumerable.Range(0, 2).Count(i => host.Stage.SeatHolder(i) == null), 1);
        }

        [Test]
        public void Bots_that_are_not_wanted_any_more_leave_with_their_seats()
        {
            var host = Host(); var crew = Crew(host);
            crew.Configure(Profiles(6), 6, true, false);
            Run(crew, 200);
            crew.Configure(Profiles(6), 2, true, false);
            Run(crew, 100);
            Assert.AreEqual(2, crew.Count);
            Assert.AreEqual(2, host.Virtuals.Count);
            Assert.LessOrEqual(Enumerable.Range(0, 8).Count(i => host.Stage.SeatHolder(i) != null), 1);
        }

        [Test]
        public void A_bot_the_board_took_away_goes_at_once()
        {
            var host = Host(); var crew = Crew(host);
            crew.Configure(Profiles(3), 3, false, false);
            Run(crew, 60);
            crew.Configure(Profiles(3).Where(p => p.Id != "b2"), 3, false, false);
            Assert.IsFalse(crew.IsMine("bot:b2"));
        }

        [Test]
        public void Chatty_bots_write_and_a_real_person_gets_a_greeting()
        {
            var host = Host(); var crew = Crew(host);
            real = 1;
            crew.Configure(Profiles(3), 3, false, true);
            Run(crew, 600);
            Assert.Greater(said.Count, 3);
            said.Clear();
            crew.Greet();
            Run(crew, 12);
            Assert.IsTrue(said.Any(s => new[] { "أهلين", "يا هلا فيك", "مرحبا", "نورت", "هلا والله", "أهلا وسهلا" }.Any(g => s.EndsWith(":" + g))));
        }

        [Test]
        public void Nobody_talks_when_chat_is_off()
        {
            var host = Host(); var crew = Crew(host);
            crew.Configure(Profiles(3), 3, false, false);
            Run(crew, 600);
            Assert.AreEqual(0, said.Count);
        }
    }
}
