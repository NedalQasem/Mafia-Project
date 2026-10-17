using System.Collections.Generic;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>Prototype wiring: rooms list -> voice room, with mock data.</summary>
    public class KitRoomsDemo : MonoBehaviour
    {
        [SerializeField] private KitTopBar topBar;
        [SerializeField] private KitNavBar navBar;
        [SerializeField] private KitProfileCard card;
        [SerializeField] private KitRoomsScreen rooms;
        [SerializeField] private KitVoiceRoom voiceRoom;

        public static List<RoomInfo> MockRooms() => new List<RoomInfo>
        {
            new RoomInfo { name = "سهرة الشباب", owner = "أبو سالم", ownerRank = 2, ownerLevel = 7, count = 9, speakingSeat = 4 },
            new RoomInfo { name = "مجلس الكبار", owner = "الحج حسن", ownerRank = 6, ownerLevel = 26, count = 14, speakingSeat = 1, type = RoomType.Vip },
            new RoomInfo { name = "غرفة الأصحاب", owner = "لينا", ownerRank = 1, ownerLevel = 3, count = 4, type = RoomType.Private, code = "K7Q2", mafiaMode = true },
            new RoomInfo { name = "ليلة المافيا", owner = "سارة", ownerRank = 2, ownerLevel = 9, count = 10, mafiaMode = true, speakingSeat = 2 },
            new RoomInfo { name = "أول سهرة", owner = "مازن", ownerRank = 2, ownerLevel = 5, count = 1, isNew = true },
            new RoomInfo { name = "ديوانية العيلة", owner = "البيك", ownerRank = 5, ownerLevel = 12, count = 16 },
        };

        public static List<SeatInfo> MockSeats() => new List<SeatInfo>
        {
            new SeatInfo { name = "أبو سالم", rank = 2, host = true, ready = true },
            new SeatInfo { name = "أم سعيد", rank = 4, ready = true },
            new SeatInfo { name = "البيك", rank = 5 },
            new SeatInfo { name = "أبو فارس", rank = 2, ready = true },
            new SeatInfo { name = "لينا", rank = 3, muted = true },
            new SeatInfo { name = "الحج حسن", rank = 6, ready = true },
            new SeatInfo { name = "سارة", rank = 1 },
            new SeatInfo(), new SeatInfo(), new SeatInfo(),
        };

        private void Start()
        {
            topBar.SetPlayer("أبو سالم", "صاحب كلمة", 7, 2, 0.62f, 1250);
            card.SetPlayer("أبو سالم", "صاحب كلمة", 7, 2, 0.62f, 620, 1000, 34, 12);
            card.SetRoles(15, 5, 7, 7);
            topBar.OnProfileClicked += card.Show;
            navBar.SnapToSelected();
            rooms.SetRooms(MockRooms());
            rooms.OnJoinRoom += r => voiceRoom.Open(r.name, CoinPill.Format(r.count) + " من ١٦" + (r.type == RoomType.Private ? " · " + r.code : ""), MockSeats());
            rooms.OnQuickJoin += () => voiceRoom.Open("سهرة الشباب", "٩ من ١٦", MockSeats());
            voiceRoom.OnBack += () => voiceRoom.gameObject.SetActive(false);
        }
    }
}
