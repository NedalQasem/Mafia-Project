using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.MatchRules.Net;

namespace MafiaNight.MatchRules.Stage
{
    /// <summary>A made-up player (a bot) the host lists in its room: it has no device, so the host speaks for it and every member shows it like a person.</summary>
    public sealed class VirtualMember
    {
        public string Id = "", Name = "";
        public int Rank, Level;
    }

    /// <summary>
    /// Keeps the mic seats of a room the same on every device. The room's host is the authority: a member sends what he wants to the host
    /// (who he is comes from the network, never from the message), the host checks it with <see cref="RoomStage"/> and sends the whole stage
    /// to everybody. A member who joins gets it at once; when the host changes, the new host cleans the stage of people who are gone and sends it.
    /// Pure: the network is the <c>send</c> delegate, so the host, the headless room bot and the tests use the same code.
    /// </summary>
    public sealed class StageSync
    {
        private readonly string me;
        private readonly Func<string> hostId;
        private readonly Func<IEnumerable<string>> members;
        private readonly Action<string, WireMessage> send;

        private readonly List<VirtualMember> virtuals = new List<VirtualMember>();

        public RoomStage Stage { get; }
        /// <summary>The made-up players the host listed (bots): they may sit on seats like members but no message is ever sent to them.</summary>
        public IReadOnlyList<VirtualMember> Virtuals => virtuals;
        /// <summary>The stage changed (here or on the host's word): redraw the seats and apply the mic.</summary>
        public event Action Changed;

        public bool IAmHost => hostId() == me;

        /// <param name="me">this device's id in the room</param>
        /// <param name="hostId">the id of the room's current host</param>
        /// <param name="members">everybody in the room, this device included</param>
        /// <param name="send">sends a message to one member</param>
        /// <param name="isAdmin">extra staff besides the host (the admins of an official room); may be null</param>
        public StageSync(string me, Func<string> hostId, Func<IEnumerable<string>> members, Action<string, WireMessage> send, Func<string, bool> isAdmin = null, int seats = 8, MicMode mode = MicMode.Free)
        {
            this.me = me; this.hostId = hostId; this.members = members; this.send = send;
            Stage = new RoomStage(id => id == hostId() || (isAdmin != null && isAdmin(id)), seats, mode);
        }

        // ------------------------------------------------------------------ what this device asks for

        public void Sit(int seat = RoomStage.NoSeat) => Act("sit", seat.ToString());
        public void Stand() => Act("stand");
        public void Hand(bool raised) => Act("hand", raised ? "1" : "0");
        public void Bring(string target, int seat = RoomStage.NoSeat) => Act("bring", target, seat.ToString());
        public void Down(string target) => Act("down", target);
        public void Lock(int seat, bool on) => Act("lock", seat.ToString(), on ? "1" : "0");
        public void Configure(int seats, MicMode mode) => Act("cfg", seats.ToString(), ((int)mode).ToString());

        private void Act(string kind, string a = "", string b = "")
        {
            if (IAmHost) { int v = Stage.Version; Apply(me, kind, a, b); if (Stage.Version != v) Publish(); return; }
            string host = hostId();
            if (!string.IsNullOrEmpty(host)) send(host, new WireMessage(Msg.StageAct, kind, a, b));
        }

        private bool Apply(string actor, string kind, string a, string b)
        {
            switch (kind)
            {
                case "sit": return int.TryParse(a, out int seat) && Stage.Sit(actor, seat);
                case "stand": return Stage.Stand(actor);
                case "hand": return Stage.Hand(actor, a == "1");
                case "bring": return int.TryParse(b, out int to) && Stage.Bring(actor, a, to);
                case "down": return Stage.Down(actor, a);
                case "lock": return int.TryParse(a, out int s) && Stage.Lock(actor, s, b == "1");
                case "cfg": return int.TryParse(a, out int n) && int.TryParse(b, out int m) && m >= 0 && m <= 1 && Stage.Configure(actor, n, (MicMode)m);
                default: return false;
            }
        }

        // ------------------------------------------------------------------ the host's made-up players

        /// <summary>Host side: the bots of the room are now exactly these. Those who went lose their seat and hand; everybody gets the new list.</summary>
        public void SetVirtuals(IEnumerable<VirtualMember> list)
        {
            if (!IAmHost) return;
            var next = list.ToList();
            if (next.Count == virtuals.Count && next.Zip(virtuals, (a, b) => a.Id == b.Id && a.Name == b.Name && a.Rank == b.Rank && a.Level == b.Level).All(x => x)) return;
            var gone = virtuals.Where(v => next.All(n => n.Id != v.Id)).Select(v => v.Id).ToList();
            virtuals.Clear(); virtuals.AddRange(next);
            int ver = Stage.Version;
            foreach (var id in gone) Stage.Remove(id);
            var msg = RosterMessage();
            foreach (var id in members()) if (id != me) send(id, msg);
            if (Stage.Version != ver) Publish(); else Changed?.Invoke();
        }

        private WireMessage RosterMessage()
        {
            var a = new List<string>();
            foreach (var v in virtuals) { a.Add(WireMessage.Clean(v.Id)); a.Add(WireMessage.Clean(v.Name)); a.Add(v.Rank.ToString()); a.Add(v.Level.ToString()); }
            return new WireMessage(Msg.Roster, a.ToArray());
        }

        // ------------------------------------------------------------------ what arrives

        /// <summary>A message from the room (only the stage messages are handled here; others are ignored).</summary>
        public void Receive(string sender, WireMessage m)
        {
            if (m == null) return;
            if (m.Type == Msg.StageAct)
            {
                if (!IAmHost) return;
                int v = Stage.Version;
                Apply(sender, m.Arg(0), m.Arg(1), m.Arg(2));
                if (Stage.Version != v) Publish();
            }
            else if (m.Type == Msg.Roster)
            {
                if (IAmHost || sender != hostId()) return;
                virtuals.Clear();
                for (int i = 0; i + 3 < m.Args.Length; i += 4)
                    if (m.Arg(i).Length > 0) virtuals.Add(new VirtualMember { Id = m.Arg(i), Name = m.Arg(i + 1), Rank = m.Int(i + 2), Level = m.Int(i + 3) });
                Changed?.Invoke();
            }
            else if (m.Type == Msg.Stage)
            {
                if (IAmHost || sender != hostId()) return;       // only the room's host may say how the stage is
                if (Stage.Import(m.Args)) Changed?.Invoke();
            }
        }

        /// <summary>Host side: a member entered the room, he gets the stage as it is.</summary>
        public void MemberJoined(string id)
        {
            if (!IAmHost || id == me) return;
            send(id, new WireMessage(Msg.Stage, Stage.Export()));
            if (virtuals.Count > 0) send(id, RosterMessage());
        }

        /// <summary>Host side: a member left the room, his seat and hand go.</summary>
        public void MemberLeft(string id)
        {
            if (!IAmHost) return;
            int v = Stage.Version;
            Stage.Remove(id);
            if (Stage.Version != v) Publish();
        }

        /// <summary>This device may have just become the host (or the room changed): clean the stage of people who are gone and tell everybody.</summary>
        public void HostChanged()
        {
            if (!IAmHost) return;
            Stage.Keep(members().ToList());
            Publish();
        }

        private void Publish()
        {
            var msg = new WireMessage(Msg.Stage, Stage.Export());
            foreach (var id in members()) if (id != me) send(id, msg);
            Changed?.Invoke();
        }
    }
}
