using System.Linq;
using System.Text;
using MafiaNight.MatchRules.Net;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    public class PacketFramerTests
    {
        private static byte[] Bytes(int n) { var b = new byte[n]; for (int i = 0; i < n; i++) b[i] = (byte)(i * 7 + 3); return b; }

        private static byte[] RoundTrip(byte[] msg, int max = PacketFramer.MaxPacket)
        {
            var re = new PacketFramer.Reassembler();
            byte[] result = null;
            foreach (var p in PacketFramer.Split(msg, max)) { Assert.LessOrEqual(p.Length, max); result = re.Push("a", p, p.Length) ?? result; }
            return result;
        }

        [Test]
        public void A_short_message_is_one_packet()
        {
            var msg = Bytes(200);
            Assert.AreEqual(1, PacketFramer.Split(msg).Count);
            CollectionAssert.AreEqual(msg, RoundTrip(msg));
        }

        [Test]
        public void A_message_of_exactly_one_packet_stays_whole()
        {
            var msg = Bytes(PacketFramer.MaxPacket - 1);
            Assert.AreEqual(1, PacketFramer.Split(msg).Count);
            CollectionAssert.AreEqual(msg, RoundTrip(msg));
        }

        [Test]
        public void A_long_message_is_split_and_comes_back_identical()
        {
            foreach (int size in new[] { 1170, 1500, 2339, 2340, 2341, 5000, 20000 })
            {
                var msg = Bytes(size);
                Assert.Greater(PacketFramer.Split(msg).Count, 1, "size " + size);
                CollectionAssert.AreEqual(msg, RoundTrip(msg), "size " + size);
            }
        }

        [Test]
        public void Arabic_text_survives_the_split()
        {
            var text = string.Concat(Enumerable.Repeat("أبو سالم وأم سعيد ", 400));
            var msg = Encoding.UTF8.GetBytes(text);
            Assert.AreEqual(text, Encoding.UTF8.GetString(RoundTrip(msg, 300)));
        }

        [Test]
        public void Two_senders_can_interleave()
        {
            var a = Bytes(3000); var b = Bytes(2500).Select(x => (byte)(x ^ 0x55)).ToArray();
            var pa = PacketFramer.Split(a); var pb = PacketFramer.Split(b);
            var re = new PacketFramer.Reassembler();
            byte[] ra = null, rb = null;
            for (int i = 0; i < System.Math.Max(pa.Count, pb.Count); i++)
            {
                if (i < pa.Count) ra = re.Push("a", pa[i], pa[i].Length) ?? ra;
                if (i < pb.Count) rb = re.Push("b", pb[i], pb[i].Length) ?? rb;
            }
            CollectionAssert.AreEqual(a, ra); CollectionAssert.AreEqual(b, rb);
        }

        [Test]
        public void Pieces_without_a_start_are_ignored()
        {
            var packets = PacketFramer.Split(Bytes(4000));
            var re = new PacketFramer.Reassembler();
            for (int i = 1; i < packets.Count; i++) Assert.IsNull(re.Push("a", packets[i], packets[i].Length));
        }

        [Test]
        public void A_new_message_after_a_lost_ending_still_works()
        {
            var broken = PacketFramer.Split(Bytes(4000));
            var re = new PacketFramer.Reassembler();
            for (int i = 0; i < broken.Count - 1; i++) re.Push("a", broken[i], broken[i].Length);   // the last piece never arrives
            var next = Bytes(100);
            var p = PacketFramer.Split(next)[0];
            CollectionAssert.AreEqual(next, re.Push("a", p, p.Length));
        }

        [Test]
        public void An_endless_message_is_cut_off()
        {
            var re = new PacketFramer.Reassembler();
            var first = new byte[1170]; first[0] = 1;
            re.Push("a", first, first.Length);
            var mid = new byte[1170]; mid[0] = 2;
            for (int i = 0; i < 100; i++) re.Push("a", mid, mid.Length);   // far beyond the 64 KB limit
            var last = new byte[10]; last[0] = 3;
            Assert.IsNull(re.Push("a", last, last.Length));
        }
    }
}
