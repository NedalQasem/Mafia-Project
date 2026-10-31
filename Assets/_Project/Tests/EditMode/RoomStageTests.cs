using System.Linq;
using MafiaNight.MatchRules.Stage;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    public class RoomStageTests
    {
        private static RoomStage Stage(MicMode mode = MicMode.Free, int seats = 4) => new RoomStage(id => id == "host" || id == "admin", seats, mode);

        [Test]
        public void A_member_sits_on_the_first_free_seat_and_stands_again()
        {
            var s = Stage();
            Assert.IsTrue(s.Sit("a"));
            Assert.AreEqual(0, s.SeatOf("a"));
            Assert.IsTrue(s.Sit("b"));
            Assert.AreEqual(1, s.SeatOf("b"));
            Assert.IsTrue(s.Stand("a"));
            Assert.AreEqual(RoomStage.NoSeat, s.SeatOf("a"));
            Assert.IsFalse(s.Stand("a"));
        }

        [Test]
        public void Nobody_takes_a_seat_somebody_else_holds()
        {
            var s = Stage();
            Assert.IsTrue(s.Sit("a", 2));
            Assert.IsFalse(s.Sit("b", 2));
            Assert.AreEqual("a", s.SeatHolder(2));
        }

        [Test]
        public void Sitting_somewhere_else_frees_the_old_seat()
        {
            var s = Stage();
            s.Sit("a", 0);
            Assert.IsTrue(s.Sit("a", 3));
            Assert.IsNull(s.SeatHolder(0));
            Assert.AreEqual(3, s.SeatOf("a"));
        }

        [Test]
        public void Seat_numbers_outside_the_room_are_refused()
        {
            var s = Stage();
            Assert.IsFalse(s.Sit("a", 4));
            Assert.IsFalse(s.Sit("a", 99));
            Assert.IsFalse(s.Sit("", 0));
        }

        [Test]
        public void When_every_seat_is_taken_the_next_member_gets_none()
        {
            var s = Stage(seats: 2);
            Assert.IsTrue(s.Sit("a")); Assert.IsTrue(s.Sit("b"));
            Assert.IsFalse(s.Sit("c"));
        }

        [Test]
        public void The_big_seat_is_for_staff_and_holds_one()
        {
            var s = Stage();
            Assert.IsFalse(s.Sit("a", RoomStage.Head));
            Assert.IsTrue(s.Sit("host", RoomStage.Head));
            Assert.IsFalse(s.Sit("admin", RoomStage.Head));
            Assert.AreEqual("host", s.HeadHolder);
            Assert.AreEqual(RoomStage.Head, s.SeatOf("host"));
        }

        [Test]
        public void A_locked_seat_refuses_members_but_not_staff()
        {
            var s = Stage();
            Assert.IsTrue(s.Lock("host", 0, true));
            Assert.IsFalse(s.Sit("a", 0));
            Assert.IsTrue(s.Sit("a"));                 // the next free seat is 1
            Assert.AreEqual(1, s.SeatOf("a"));
            Assert.IsTrue(s.Sit("admin", 0));
        }

        [Test]
        public void Locking_an_occupied_seat_stands_an_ordinary_member()
        {
            var s = Stage();
            s.Sit("a", 1);
            s.Lock("host", 1, true);
            Assert.IsNull(s.SeatHolder(1));
        }

        [Test]
        public void Only_staff_lock_and_configure()
        {
            var s = Stage();
            Assert.IsFalse(s.Lock("a", 0, true));
            Assert.IsFalse(s.Configure("a", 6, MicMode.Request));
            Assert.IsTrue(s.Configure("host", 6, MicMode.Request));
            Assert.AreEqual(6, s.SeatCount);
            Assert.AreEqual(MicMode.Request, s.Mode);
        }

        [Test]
        public void In_a_request_room_members_wait_for_staff()
        {
            var s = Stage(MicMode.Request);
            Assert.IsFalse(s.Sit("a"));
            Assert.IsTrue(s.Hand("a", true));
            Assert.AreEqual(1, s.QueuePosition("a"));
            Assert.IsTrue(s.Bring("host", "a"));
            Assert.AreEqual(0, s.SeatOf("a"));
            Assert.IsFalse(s.HandRaised("a"));         // bringing him up lowers his hand
        }

        [Test]
        public void The_queue_keeps_the_order_hands_went_up()
        {
            var s = Stage(MicMode.Request);
            s.Hand("a", true); s.Hand("b", true); s.Hand("c", true);
            s.Hand("a", true);                         // twice does not queue him twice
            Assert.AreEqual("a,b,c", string.Join(",", s.Queue));
            s.Hand("b", false);
            Assert.AreEqual("a,c", string.Join(",", s.Queue));
            Assert.AreEqual(2, s.QueuePosition("c"));
        }

        [Test]
        public void A_seated_member_has_no_hand()
        {
            var s = Stage();
            s.Sit("a");
            Assert.IsFalse(s.Hand("a", true));
        }

        [Test]
        public void Members_cannot_bring_or_take_down_others()
        {
            var s = Stage();
            s.Sit("a", 0);
            Assert.IsFalse(s.Bring("b", "c"));
            Assert.IsFalse(s.Down("b", "a"));
            Assert.AreEqual(0, s.SeatOf("a"));
        }

        [Test]
        public void Staff_take_a_member_down_but_not_another_staff_member()
        {
            var s = Stage();
            s.Sit("a", 0); s.Sit("admin", 1);
            Assert.IsTrue(s.Down("host", "a"));
            Assert.IsFalse(s.Down("host", "admin"));
            Assert.IsTrue(s.Down("admin", "admin"));   // stepping down by himself
        }

        [Test]
        public void Several_members_speak_at_the_same_time()
        {
            var s = Stage(MicMode.Request, 8);
            foreach (var m in new[] { "a", "b", "c" }) { s.Hand(m, true); Assert.IsTrue(s.Bring("host", m)); }
            Assert.AreEqual(3, Enumerable.Range(0, 8).Count(i => s.SeatHolder(i) != null));
        }

        [Test]
        public void Fewer_seats_empty_the_extra_ones()
        {
            var s = Stage(seats: 8);
            s.Sit("a", 6);
            s.Configure("host", 4, MicMode.Free);
            Assert.AreEqual(RoomStage.NoSeat, s.SeatOf("a"));
        }

        [Test]
        public void A_member_who_leaves_loses_his_seat_and_hand()
        {
            var s = Stage();
            s.Sit("a"); s.Hand("b", true);
            s.Remove("a"); s.Remove("b");
            Assert.IsFalse(s.IsSeated("a"));
            Assert.AreEqual(0, s.Queue.Count);
        }

        [Test]
        public void Keep_drops_everybody_who_is_not_in_the_room()
        {
            var s = Stage();
            s.Sit("host", RoomStage.Head); s.Sit("a"); s.Sit("b"); s.Hand("c", true);
            s.Keep(new[] { "host", "b" });
            Assert.AreEqual("host", s.HeadHolder);
            Assert.IsFalse(s.IsSeated("a"));
            Assert.IsTrue(s.IsSeated("b"));
            Assert.AreEqual(0, s.Queue.Count);
        }

        [Test]
        public void The_version_grows_with_every_change_only()
        {
            var s = Stage();
            int v = s.Version;
            s.Sit("a");
            Assert.Greater(s.Version, v);
            v = s.Version;
            s.Sit("a");                                // already there: nothing changes
            Assert.AreEqual(v, s.Version);
            Assert.IsFalse(s.Sit("b", 0));
            Assert.AreEqual(v, s.Version);
        }

        [Test]
        public void The_exported_stage_is_read_back_the_same()
        {
            var s = Stage(MicMode.Request, 6);
            s.Sit("host", RoomStage.Head); s.Bring("host", "a"); s.Bring("host", "b", 3); s.Lock("host", 5, true); s.Hand("c", true); s.Hand("d", true);
            var t = Stage();
            Assert.IsTrue(t.Import(s.Export()));
            Assert.AreEqual(6, t.SeatCount);
            Assert.AreEqual(MicMode.Request, t.Mode);
            Assert.AreEqual("host", t.HeadHolder);
            Assert.AreEqual("a", t.SeatHolder(0));
            Assert.AreEqual("b", t.SeatHolder(3));
            Assert.IsTrue(t.IsLocked(5));
            Assert.AreEqual("c,d", string.Join(",", t.Queue));
            Assert.AreEqual(s.Version, t.Version);
        }

        [Test]
        public void A_broken_message_changes_nothing()
        {
            var s = Stage();
            s.Sit("a");
            Assert.IsFalse(s.Import(new[] { "1", "x" }));
            Assert.IsFalse(s.Import(new[] { "1", "99", "0", "", "", "", "" }));
            Assert.IsFalse(s.Import(new[] { "1", "4", "0", "", "0000", "a,b", "" }));      // two seats listed for four
            Assert.AreEqual(0, s.SeatOf("a"));
        }
    }
}
