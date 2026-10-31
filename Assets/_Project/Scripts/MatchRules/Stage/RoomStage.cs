using System;
using System.Collections.Generic;
using System.Linq;

namespace MafiaNight.MatchRules.Stage
{
    /// <summary>How a room hands out its mic seats.</summary>
    public enum MicMode
    {
        /// <summary>Any member sits on any free, unlocked seat and speaks.</summary>
        Free,
        /// <summary>A member raises a hand; only the host / staff put him on a seat.</summary>
        Request,
    }

    /// <summary>
    /// The mic seats of a voice room (the screen of Docs/mockups/office-room.html): one big seat for the host or staff, a few ordinary seats,
    /// who sits on them, which are locked, and the queue of raised hands. Pure rules, no Unity and no network, so the host and the headless
    /// room bot run the same code and tests cover it. Every action names its actor; <c>isStaff</c> says who may run the room (the host, the admins of an official room).
    /// </summary>
    public sealed class RoomStage
    {
        public const int MinSeats = 2;
        public const int MaxSeats = 12;
        /// <summary>The big seat (staff only).</summary>
        public const int Head = -1;
        public const int NoSeat = -2;

        private readonly Func<string, bool> isStaff;
        private readonly string[] seats = new string[MaxSeats];
        private readonly bool[] locked = new bool[MaxSeats];
        private readonly List<string> queue = new List<string>();
        private string head;

        public int SeatCount { get; private set; }
        public MicMode Mode { get; private set; }
        /// <summary>Raised on every change of the stage, so the host knows to broadcast it.</summary>
        public int Version { get; private set; }
        public string HeadHolder => head;
        public IReadOnlyList<string> Queue => queue;

        public RoomStage(Func<string, bool> isStaff, int seatCount = 8, MicMode mode = MicMode.Free)
        {
            this.isStaff = isStaff ?? (_ => false);
            SeatCount = Math.Max(MinSeats, Math.Min(MaxSeats, seatCount));
            Mode = mode;
        }

        public string SeatHolder(int index) => index >= 0 && index < SeatCount ? seats[index] : null;
        public bool IsLocked(int index) => index >= 0 && index < SeatCount && locked[index];

        /// <summary>Where this member sits: <see cref="Head"/>, a seat index, or <see cref="NoSeat"/>.</summary>
        public int SeatOf(string id)
        {
            if (id == null) return NoSeat;
            if (head == id) return Head;
            for (int i = 0; i < SeatCount; i++) if (seats[i] == id) return i;
            return NoSeat;
        }

        public bool IsSeated(string id) => SeatOf(id) != NoSeat;
        public bool HandRaised(string id) => queue.Contains(id);
        /// <summary>Position in the queue, 1 for the first; 0 when not waiting.</summary>
        public int QueuePosition(string id) { int i = queue.IndexOf(id); return i < 0 ? 0 : i + 1; }

        private void Changed() => Version++;

        private void Clear(string id)
        {
            if (head == id) head = null;
            for (int i = 0; i < MaxSeats; i++) if (seats[i] == id) seats[i] = null;
            queue.Remove(id);
        }

        private int FirstFree()
        {
            for (int i = 0; i < SeatCount; i++) if (seats[i] == null && !locked[i]) return i;
            return NoSeat;
        }

        // ------------------------------------------------------------------ what a member does

        /// <summary>A member takes a seat himself. <paramref name="seat"/> = <see cref="NoSeat"/> picks the first free one; <see cref="Head"/> is for staff.</summary>
        public bool Sit(string actor, int seat = NoSeat)
        {
            if (string.IsNullOrEmpty(actor)) return false;
            bool staff = isStaff(actor);
            if (Mode == MicMode.Request && !staff) return false;
            if (seat == Head)
            {
                if (!staff || (head != null && head != actor)) return false;
                Clear(actor); head = actor; Changed(); return true;
            }
            if (seat == NoSeat && IsSeated(actor)) return true;          // already sitting: "sit" changes nothing
            if (seat == NoSeat) seat = FirstFree();
            if (seat < 0 || seat >= SeatCount) return false;
            if (seats[seat] != null && seats[seat] != actor) return false;
            if (locked[seat] && !staff) return false;
            if (seats[seat] == actor) return true;
            Clear(actor); seats[seat] = actor; Changed(); return true;
        }

        /// <summary>A member leaves his seat.</summary>
        public bool Stand(string actor)
        {
            if (!IsSeated(actor)) return false;
            Clear(actor); Changed(); return true;
        }

        /// <summary>A member who is not seated raises or lowers his hand (only the "request" rooms use the queue, but a free room may too).</summary>
        public bool Hand(string actor, bool raised)
        {
            if (string.IsNullOrEmpty(actor) || IsSeated(actor)) return false;
            if (raised) { if (queue.Contains(actor)) return true; queue.Add(actor); }
            else if (!queue.Remove(actor)) return true;
            Changed(); return true;
        }

        // ------------------------------------------------------------------ what staff do

        /// <summary>Staff put a member on a seat (the next free one when <paramref name="seat"/> is <see cref="NoSeat"/>), also from the queue.</summary>
        public bool Bring(string actor, string target, int seat = NoSeat)
        {
            if (!isStaff(actor) || string.IsNullOrEmpty(target)) return false;
            if (seat == NoSeat) seat = FirstFree();
            if (seat < 0 || seat >= SeatCount || locked[seat]) return false;
            if (seats[seat] == target) return true;
            if (seats[seat] != null) return false;
            Clear(target); seats[seat] = target; Changed(); return true;
        }

        /// <summary>Staff take a member off his seat (another staff member can only step down by himself).</summary>
        public bool Down(string actor, string target)
        {
            if (!isStaff(actor) || !IsSeated(target)) return false;
            if (target != actor && isStaff(target)) return false;
            Clear(target); Changed(); return true;
        }

        /// <summary>Staff lock or unlock a seat; locking an occupied seat stands its member.</summary>
        public bool Lock(string actor, int seat, bool on)
        {
            if (!isStaff(actor) || seat < 0 || seat >= SeatCount) return false;
            if (locked[seat] == on) return true;
            locked[seat] = on;
            if (on && seats[seat] != null && !isStaff(seats[seat])) seats[seat] = null;
            Changed(); return true;
        }

        /// <summary>Staff change the number of seats and the mic mode; seats beyond the new count are emptied.</summary>
        public bool Configure(string actor, int seatCount, MicMode mode)
        {
            if (!isStaff(actor)) return false;
            seatCount = Math.Max(MinSeats, Math.Min(MaxSeats, seatCount));
            if (seatCount == SeatCount && mode == Mode) return true;
            for (int i = seatCount; i < MaxSeats; i++) { seats[i] = null; locked[i] = false; }
            SeatCount = seatCount; Mode = mode;
            Changed(); return true;
        }

        /// <summary>A member left the room.</summary>
        public void Remove(string id)
        {
            if (!IsSeated(id) && !queue.Contains(id)) return;
            Clear(id); Changed();
        }

        /// <summary>Drops everybody who is no longer in the room (after the host changed or somebody was lost).</summary>
        public void Keep(ICollection<string> present)
        {
            bool any = false;
            if (head != null && !present.Contains(head)) { head = null; any = true; }
            for (int i = 0; i < MaxSeats; i++) if (seats[i] != null && !present.Contains(seats[i])) { seats[i] = null; any = true; }
            if (queue.RemoveAll(q => !present.Contains(q)) > 0) any = true;
            if (any) Changed();
        }

        // ------------------------------------------------------------------ on the wire

        /// <summary>The whole stage as message arguments: version, seat count, mode, head, locked mask, seats (comma separated), queue (comma separated).</summary>
        public string[] Export()
        {
            var mask = new System.Text.StringBuilder();
            for (int i = 0; i < SeatCount; i++) mask.Append(locked[i] ? '1' : '0');
            return new[]
            {
                Version.ToString(), SeatCount.ToString(), ((int)Mode).ToString(), head ?? "", mask.ToString(),
                string.Join(",", seats.Take(SeatCount).Select(s => s ?? "")), string.Join(",", queue),
            };
        }

        /// <summary>Replaces this stage with one received from the host. False when the text is malformed (nothing changes then).</summary>
        public bool Import(string[] a)
        {
            if (a == null || a.Length < 7) return false;
            if (!int.TryParse(a[0], out int version) || !int.TryParse(a[1], out int count) || !int.TryParse(a[2], out int mode)) return false;
            if (count < MinSeats || count > MaxSeats || mode < 0 || mode > 1) return false;
            var parts = a[5].Split(',');
            if (parts.Length != count) return false;
            SeatCount = count; Mode = (MicMode)mode; head = a[3].Length == 0 ? null : a[3];
            for (int i = 0; i < MaxSeats; i++)
            {
                seats[i] = i < count && parts[i].Length > 0 ? parts[i] : null;
                locked[i] = i < count && i < a[4].Length && a[4][i] == '1';
            }
            queue.Clear();
            if (a[6].Length > 0) queue.AddRange(a[6].Split(','));
            Version = version;
            return true;
        }
    }
}
