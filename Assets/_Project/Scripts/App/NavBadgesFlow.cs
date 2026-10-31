using MafiaNight.Services.Chat;
using MafiaNight.Services.Friends;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Fills the bottom bar with what is real: the family tab shows unread chat messages plus friend requests (and each in its menu), the tag over the rooms tab
    /// shows how many players are online, and the ranks tab has nothing to announce (its dot stays hidden).
    /// </summary>
    public sealed class NavBadgesFlow : MonoBehaviour
    {
        private const int FamilyTab = 1, RanksTab = 3;

        private const float Every = 1f;           // the family screens build one bar per frame, some a moment after the scene loaded
        private float next;
        private ChatService chat;
        private FriendsService friends;

        private void Start()
        {
            if (FindAnyObjectByType<KitNavBar>(FindObjectsInactive.Include) == null) { Destroy(this); return; }
            chat = ChatService.Ensure();
            friends = FriendsService.Ensure();
            chat.OnUnreadChanged += OnUnread;
            friends.OnChanged += Refresh;
            friends.OnOnlineChanged += Refresh;
            Refresh();
            friends.Refresh();                       // the requests may have arrived while this screen was not open
        }

        private void OnDestroy()
        {
            if (chat != null) chat.OnUnreadChanged -= OnUnread;
            if (friends != null) { friends.OnChanged -= Refresh; friends.OnOnlineChanged -= Refresh; }
        }

        private void OnUnread(int _) => Refresh();

        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + Every;
            Refresh();
        }

        private void Refresh()
        {
            int family = chat.UnreadTotal + friends.Incoming.Count;
            foreach (var nav in FindObjectsByType<KitNavBar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                nav.SetBadge(FamilyTab, family);
                nav.SetPlaceBadge(FamilyTab, 0, chat.UnreadTotal);          // in the family tab's menu: the chats ...
                nav.SetPlaceBadge(FamilyTab, 1, friends.Incoming.Count);    // ... and the friend requests
                nav.SetBadge(RanksTab, 0);
                nav.SetPlaceBadge(RanksTab, 0, 0);
                nav.SetOnline(friends.OnlineCount);
            }
        }
    }
}
