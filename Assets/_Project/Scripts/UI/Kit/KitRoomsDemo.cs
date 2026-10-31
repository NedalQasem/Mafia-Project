using System.Collections.Generic;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>Prototype wiring of the rooms scene (the bars and the list with mock data when the real services are not running).</summary>
    public class KitRoomsDemo : MonoBehaviour
    {
        [SerializeField] private KitTopBar topBar;
        [SerializeField] private KitNavBar navBar;
        [SerializeField] private KitProfileCard card;
        [SerializeField] private KitRoomsScreen rooms;

        public static List<RoomInfo> MockRooms() => new List<RoomInfo>
        {
            new RoomInfo { name = "سهرة الشباب", owner = "أبو سالم", ownerRank = 2, ownerLevel = 7, count = 9, speakingSeat = 4 },
            new RoomInfo { name = "مجلس الكبار", owner = "الحج حسن", ownerRank = 6, ownerLevel = 26, count = 14, speakingSeat = 1, type = RoomType.Vip },
            new RoomInfo { name = "غرفة الأصحاب", owner = "لينا", ownerRank = 1, ownerLevel = 3, count = 4, type = RoomType.Private, code = "K7Q2", mafiaMode = true },
            new RoomInfo { name = "ليلة المافيا", owner = "سارة", ownerRank = 2, ownerLevel = 9, count = 10, mafiaMode = true, speakingSeat = 2 },
            new RoomInfo { name = "أول سهرة", owner = "مازن", ownerRank = 2, ownerLevel = 5, count = 1, isNew = true },
            new RoomInfo { name = "ديوانية العيلة", owner = "البيك", ownerRank = 5, ownerLevel = 12, count = 16 },
        };

        private void Start()
        {
            if (KitLiveMode.Active)
            {
                // real services drive this screen (see KitAppBinder / RoomsFlow): only the bars are wired here
                topBar.OnProfileClicked += card.Show;
                navBar.SnapToSelected();
                return;
            }
            topBar.SetPlayer("أبو سالم", "صاحب كلمة", 7, 2, 0.62f, 1250);
            card.SetPlayer("أبو سالم", "صاحب كلمة", 7, 2, 0.62f, 620, 1000, 34, 12);
            card.SetRoles(15, 5, 7, 7);
            topBar.OnProfileClicked += card.Show;
            navBar.SnapToSelected();
            rooms.SetRooms(MockRooms());
        }
    }
}
