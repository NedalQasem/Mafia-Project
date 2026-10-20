using System;
using System.Collections.Generic;

namespace MafiaNight.MatchRules.Net
{
    /// <summary>
    /// EOS peer-to-peer packets are at most 1170 bytes, but a state message of 16 players or a match snapshot is bigger. Every packet
    /// starts with one frame byte: whole message, or the first / middle / last piece of a long one. The network delivers a peer's packets
    /// in order, so the pieces are just glued back together. Plain C#, tested without a network.
    /// </summary>
    public static class PacketFramer
    {
        public const int MaxPacket = 1170;
        public const int MaxMessage = 64 * 1024;       // longer messages are dropped (a broken or hostile sender)

        private const byte Whole = 0, First = 1, Middle = 2, Last = 3;

        public static List<byte[]> Split(byte[] message, int maxPacket = MaxPacket)
        {
            var packets = new List<byte[]>();
            int room = maxPacket - 1;
            if (message.Length <= room) { packets.Add(Frame(Whole, message, 0, message.Length)); return packets; }
            for (int pos = 0; pos < message.Length; pos += room)
            {
                int n = Math.Min(room, message.Length - pos);
                byte kind = pos == 0 ? First : pos + n >= message.Length ? Last : Middle;
                packets.Add(Frame(kind, message, pos, n));
            }
            return packets;
        }

        private static byte[] Frame(byte kind, byte[] src, int offset, int count)
        {
            var p = new byte[count + 1];
            p[0] = kind;
            Buffer.BlockCopy(src, offset, p, 1, count);
            return p;
        }

        /// <summary>Glues the pieces of each sender back into messages.</summary>
        public sealed class Reassembler
        {
            private readonly Dictionary<string, List<byte>> partial = new Dictionary<string, List<byte>>();

            /// <summary>Returns the complete message when <paramref name="packet"/> finishes one, otherwise null.</summary>
            public byte[] Push(string sender, byte[] packet, int length)
            {
                if (packet == null || length < 1) return null;
                byte kind = packet[0];
                int count = length - 1;
                switch (kind)
                {
                    case Whole:
                        partial.Remove(sender);
                        var whole = new byte[count]; Buffer.BlockCopy(packet, 1, whole, 0, count);
                        return whole;
                    case First:
                        partial[sender] = new List<byte>(count * 3);
                        Append(sender, packet, count);
                        return null;
                    case Middle:
                        Append(sender, packet, count);
                        return null;
                    case Last:
                        if (!Append(sender, packet, count)) return null;
                        var all = partial[sender].ToArray();
                        partial.Remove(sender);
                        return all;
                    default:
                        return null;
                }
            }

            private bool Append(string sender, byte[] packet, int count)
            {
                if (!partial.TryGetValue(sender, out var buf)) return false;          // the start was lost: ignore the rest
                if (buf.Count + count > MaxMessage) { partial.Remove(sender); return false; }
                for (int i = 0; i < count; i++) buf.Add(packet[1 + i]);
                return true;
            }

            public void Forget(string sender) { partial.Remove(sender); }
        }
    }
}
