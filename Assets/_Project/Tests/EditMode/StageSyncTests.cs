using System.Collections.Generic;
using System.Linq;
using MafiaNight.MatchRules.Net;
using MafiaNight.MatchRules.Stage;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    public class StageSyncTests
    {
        /// <summary>Devices in one room that reach each other through bytes, like the real transport.</summary>
        private sealed class Room
        {
            public string Host = "h";
            public readonly Dictionary<string, StageSync> Devices = new Dictionary<string, StageSync>();
            public readonly HashSet<string> Admins = new HashSet<string>();
            public readonly List<string> Log = new List<string>();

            public StageSync Join(string id, int seats = 8, MicMode mode = MicMode.Free)
            {
                var s = new StageSync(id, () => Host, () => Devices.Keys.ToList(),
                    (to, msg) => { Log.Add(id + ">" + to + ":" + msg.Type); if (Devices.TryGetValue(to, out var d)) d.Receive(id, WireMessage.FromBytes(msg.ToBytes())); },
                    Admins.Contains, seats, mode);
                Devices[id] = s;
                foreach (var other in Devices.Keys.ToList()) if (other != id) Devices[other].MemberJoined(id);
                return s;
            }

            public void Leave(string id)
            {
                Devices.Remove(id);
                foreach (var d in Devices.Values.ToList()) d.MemberLeft(id);
            }
        }

        private static string Seats(StageSync s) => string.Join(",", Enumerable.Range(0, s.Stage.SeatCount).Select(i => s.Stage.SeatHolder(i) ?? "-"));

        [Test]
        public void A_member_who_sits_is_seen_on_every_device()
        {
            var r = new Room(); var host = r.Join("h"); var a = r.Join("a"); var b = r.Join("b");
            a.Sit();
            Assert.AreEqual("a,-,-,-,-,-,-,-", Seats(host));
            Assert.AreEqual(Seats(host), Seats(a));
            Assert.AreEqual(Seats(host), Seats(b));
        }

        [Test]
        public void Who_is_asking_comes_from_the_network_not_from_the_message()
        {
            var r = new Room(); r.Join("h"); var a = r.Join("a"); var b = r.Join("b");
            b.Stage.Import(new[] { "0", "8", "0", "", "00000000", ",,,,,,,", "" });
            // b pretends to be the host and to bring a member up: the host hears it from b, so it is a plain member's request
            r.Devices["h"].Receive("b", new MafiaNight.MatchRules.Net.WireMessage(Msg.StageAct, "bring", "a", "0"));
            Assert.AreEqual("-,-,-,-,-,-,-,-", Seats(r.Devices["h"]));
            Assert.IsNotNull(a);
        }

        [Test]
        public void A_member_cannot_make_himself_the_host_of_the_stage()
        {
            var r = new Room(); var host = r.Join("h"); var a = r.Join("a");
            host.Sit(RoomStage.Head);
            a.Receive("a", new MafiaNight.MatchRules.Net.WireMessage(Msg.Stage, "99", "8", "0", "a", "00000000", ",,,,,,,", ""));   // a "stage" from a member, not from the host
            Assert.AreEqual("h", host.Stage.HeadHolder);
            Assert.AreEqual("h", a.Stage.HeadHolder);
        }

        [Test]
        public void A_member_who_joins_late_gets_the_stage_at_once()
        {
            var r = new Room(); var host = r.Join("h"); var a = r.Join("a");
            a.Sit(2);
            host.Lock(5, true);
            var late = r.Join("late");
            Assert.AreEqual(Seats(host), Seats(late));
            Assert.IsTrue(late.Stage.IsLocked(5));
        }

        [Test]
        public void A_member_who_leaves_loses_his_seat_everywhere()
        {
            var r = new Room(); var host = r.Join("h"); var a = r.Join("a"); var b = r.Join("b");
            a.Sit(); b.Sit();
            r.Leave("a");
            Assert.IsFalse(host.Stage.IsSeated("a"));
            Assert.IsFalse(b.Stage.IsSeated("a"));
            Assert.IsTrue(b.Stage.IsSeated("b"));
        }

        [Test]
        public void In_a_request_room_the_host_brings_a_member_from_the_queue()
        {
            var r = new Room(); var host = r.Join("h", 8, MicMode.Request); var a = r.Join("a", 8, MicMode.Request);
            a.Sit();
            Assert.IsFalse(a.Stage.IsSeated("a"));
            a.Hand(true);
            Assert.AreEqual(1, host.Stage.QueuePosition("a"));
            Assert.AreEqual(1, a.Stage.QueuePosition("a"));
            host.Bring("a");
            Assert.AreEqual(0, a.Stage.SeatOf("a"));
            Assert.IsFalse(a.Stage.HandRaised("a"));
        }

        [Test]
        public void An_admin_of_an_official_room_runs_the_stage_while_the_host_is_a_bot()
        {
            var r = new Room { Host = "bot" }; r.Admins.Add("adm");
            r.Join("bot", 8, MicMode.Request); var adm = r.Join("adm", 8, MicMode.Request); var p = r.Join("p", 8, MicMode.Request);
            adm.Sit(RoomStage.Head);
            p.Hand(true);
            adm.Bring("p", 3);
            Assert.AreEqual(3, p.Stage.SeatOf("p"));
            Assert.AreEqual("adm", p.Stage.HeadHolder);
            p.Down("adm");                                // a member cannot take the admin down
            Assert.AreEqual("adm", p.Stage.HeadHolder);
        }

        [Test]
        public void The_host_can_take_a_member_down_and_change_the_room()
        {
            var r = new Room(); var host = r.Join("h"); var a = r.Join("a"); var b = r.Join("b");
            a.Sit(); b.Sit();
            host.Down("a");
            host.Configure(4, MicMode.Request);
            Assert.IsFalse(b.Stage.IsSeated("a"));
            Assert.AreEqual(4, b.Stage.SeatCount);
            Assert.AreEqual(MicMode.Request, a.Stage.Mode);
        }

        [Test]
        public void A_new_host_drops_the_people_who_left_and_tells_everybody()
        {
            var r = new Room(); var host = r.Join("h"); var a = r.Join("a"); var b = r.Join("b");
            a.Sit(); b.Sit();
            r.Host = "a";                                  // the old host is lost without saying goodbye
            r.Devices.Remove("h");
            a.HostChanged();
            Assert.IsTrue(b.Stage.IsSeated("a"));
            Assert.IsFalse(b.Stage.IsSeated("h"));
            b.Sit(RoomStage.Head);                         // b is a plain member now
            Assert.IsNull(a.Stage.HeadHolder);
            a.Sit(RoomStage.Head);                         // the new host is staff
            Assert.AreEqual("a", b.Stage.HeadHolder);
        }

        [Test]
        public void Nothing_is_sent_when_a_request_changes_nothing()
        {
            var r = new Room(); var host = r.Join("h"); var a = r.Join("a");
            a.Sit();
            r.Log.Clear();
            a.Sit();                                       // already seated: the host hears it and sends nothing back
            Assert.IsFalse(r.Log.Any(l => l.EndsWith(":stage")));
            Assert.IsNotNull(host);
        }

        [Test]
        public void Messages_that_are_not_for_the_stage_are_ignored()
        {
            var r = new Room(); var host = r.Join("h");
            host.Receive("a", new MafiaNight.MatchRules.Net.WireMessage(Msg.Chat, "hello"));
            host.Receive("a", new MafiaNight.MatchRules.Net.WireMessage(Msg.StageAct, "nonsense", "x", "y"));
            host.Receive("a", null);
            Assert.AreEqual(0, host.Stage.Version);
        }
    }
}
