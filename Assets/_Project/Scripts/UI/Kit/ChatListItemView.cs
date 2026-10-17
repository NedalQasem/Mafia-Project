using System;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>A conversation in the Direct / Groups lists.</summary>
    public class ChatListItemView : MonoBehaviour
    {
        [SerializeField] private AvatarFrame avatar;
        [SerializeField] private GameObject groupIcon;
        [SerializeField] private ArabicText nameLabel, lastLabel, timeLabel, rankLabel, badgeLabel;
        [SerializeField] private GameObject badge;
        [SerializeField] private Button button;

        public ChatConversation Conv { get; private set; }
        public event Action<ChatConversation> OnClick;

        private void Awake() { if (button != null) button.onClick.AddListener(() => OnClick?.Invoke(Conv)); }

        public void Bind(ChatConversation c)
        {
            Conv = c;
            bool group = c.kind == ChatKind.Group;
            avatar.gameObject.SetActive(!group); groupIcon.SetActive(group);
            if (!group) avatar.Set(c.rank, null, c.online);
            nameLabel.Text = c.name; lastLabel.Text = c.last; timeLabel.Text = c.time;
            rankLabel.gameObject.SetActive(!group);
            if (!group) rankLabel.Text = KitTheme.RankNames[Mathf.Clamp(c.rank, 0, 7)];
            badge.SetActive(c.unread > 0);
            if (c.unread > 0) badgeLabel.Text = CoinPill.Format(c.unread);
        }
    }
}
