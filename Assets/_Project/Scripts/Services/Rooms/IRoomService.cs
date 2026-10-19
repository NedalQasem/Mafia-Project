using System;
using System.Collections.Generic;
using MafiaNight.Data.Rooms;

namespace MafiaNight.Services.Rooms
{
    public enum RoomError
    {
        None,
        NotReady,        // EOS is not logged in yet
        BadName,
        LevelRequired,   // public rooms need a higher level
        VipRequired,
        NotFound,        // no room with this code / it closed
        Full,
        NoRooms,         // quick join found nothing
        Busy,            // too many requests per minute
        Failed,
    }

    public sealed class RoomResult
    {
        public bool Ok => Error == RoomError.None;
        public RoomError Error;
        public string Detail;                    // technical text for the log
        public RoomListing Room;                 // create / join
        public IReadOnlyList<RoomListing> Rooms; // list

        public static RoomResult Success(RoomListing room = null, IReadOnlyList<RoomListing> rooms = null) => new RoomResult { Room = room, Rooms = rooms };
        public static RoomResult Fail(RoomError error, string detail = null) => new RoomResult { Error = error, Detail = detail };

        /// <summary>Arabic text for the player.</summary>
        public static string Message(RoomError error, int publicMinLevel = 5)
        {
            switch (error)
            {
                case RoomError.None: return "";
                case RoomError.NotReady: return "لسا ما اتصلنا بالخادم. انتظر لحظة وجرّب.";
                case RoomError.BadName: return "اكتب اسماً للغرفة.";
                case RoomError.LevelRequired: return "الغرف العامة تفتح عند المستوى " + publicMinLevel + ". افتح غرفة خاصة بالكود.";
                case RoomError.VipRequired: return "غرف VIP تحتاج عضوية VIP.";
                case RoomError.NotFound: return "ما لقينا هالغرفة. تأكد من الكود أو يمكن انسكرت.";
                case RoomError.Full: return "الغرفة ممتلئة.";
                case RoomError.NoRooms: return "ما في غرف فيها مكان الآن. افتح طاولتك!";
                case RoomError.Busy: return "كثرت الطلبات. استنى شوي وجرّب.";
                default: return "صار خطأ، حاول مرة ثانية.";
            }
        }
    }

    /// <summary>
    /// Rooms on EOS Lobbies. One room per player: creating or joining a room leaves the previous one
    /// (the previous room is closed when the player owns it).
    /// </summary>
    public interface IRoomService
    {
        RoomListing CurrentRoom { get; }
        bool InRoom { get; }
        /// <summary>The players in the current room (host first).</summary>
        IReadOnlyList<RoomMember> Members { get; }

        /// <summary>The player entered a room (created or joined).</summary>
        event Action<RoomListing> OnRoomJoined;
        /// <summary>The player left, was kicked, or the room closed. Argument = Arabic reason or empty.</summary>
        event Action<string> OnRoomLeft;
        /// <summary>The room's members or settings changed.</summary>
        event Action OnRoomChanged;

        /// <summary>Public / VIP rooms, newest data from EOS (private rooms are not listed).</summary>
        void RefreshList(Action<RoomResult> done);
        void Create(RoomSettings settings, Action<RoomResult> done);
        void Join(RoomListing room, Action<RoomResult> done);
        void JoinByCode(string code, Action<RoomResult> done);
        /// <summary>Joins the fullest public room that still has a seat.</summary>
        void QuickJoin(Action<RoomResult> done);
        void Leave(Action done = null);
        /// <summary>Host only.</summary>
        void Kick(string puid, Action<bool> done = null);
    }
}
