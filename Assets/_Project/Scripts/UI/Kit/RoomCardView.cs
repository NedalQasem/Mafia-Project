using System;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    public enum RoomType { Public, Private, Vip }

    [Serializable]
    public class RoomInfo
    {
        public string name = "غرفة";
        public string owner = "أبو سالم";
        public int ownerRank = 2;
        public int ownerLevel = 7;
        public int count = 1;
        public int capacity = 16;
        public int speakingSeat = -1;      // index of the seat that is talking now, -1 none
        public RoomType type = RoomType.Public;
        public bool mafiaMode;             // a mafia round is running inside the room
        public bool isNew;
        public string code = "";           // private rooms
        public bool IsFull => count >= capacity;
    }

    /// <summary>One room in the rooms list. A single prefab; <see cref="Bind"/> switches the look for public / private / VIP and its states.</summary>
    public class RoomCardView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private AvatarFrame avatar;
        [SerializeField] private ArabicText nameLabel, ownerLabel, countLabel, chipLabel, liveLabel, codeLabel, joinLabel, vipLabel;
        [SerializeField] private Image[] seatDots = new Image[16];
        [SerializeField] private GameObject live, codeBox, vipTab, fullStamp, newTag;
        [SerializeField] private Button joinButton;
        [SerializeField] private Image joinImage;
        [SerializeField] private CanvasGroup group;

        public event Action<RoomInfo> OnJoin;
        public RoomInfo Info { get; private set; }

        private void Awake()
        {
            if (joinButton != null) joinButton.onClick.AddListener(() => { if (Info != null) OnJoin?.Invoke(Info); });
        }

        public void Bind(RoomInfo info)
        {
            Info = info;
            var th = KitTheme.Current;
            if (th == null) return;

            background.sprite = info.type == RoomType.Private ? th.steelPlate : info.type == RoomType.Vip ? th.vipFrame : th.leatherDark;
            avatar.Set(info.ownerRank);
            nameLabel.Text = info.name;
            ownerLabel.Text = info.owner + " · المستوى " + CoinPill.Format(info.ownerLevel);
            countLabel.Text = CoinPill.Format(info.count);

            for (int i = 0; i < seatDots.Length; i++)
            {
                var d = seatDots[i]; if (d == null) continue;
                Color c;
                if (i == info.speakingSeat && i < info.count) c = th.teal;
                else if (i < info.count) c = th.ivory;
                else c = new Color(0.1f, 0.09f, 0.08f, 1f);
                d.color = c;
                d.gameObject.SetActive(i < info.capacity);
            }

            chipLabel.Text = info.mafiaMode ? "مباراة مافيا" : "سهرة";
            live.SetActive(info.speakingSeat >= 0 && !info.IsFull);
            codeBox.SetActive(info.type == RoomType.Private && !string.IsNullOrEmpty(info.code));
            codeLabel.Text = info.code;
            vipTab.SetActive(info.type == RoomType.Vip);
            newTag.SetActive(info.isNew);
            fullStamp.SetActive(info.IsFull);

            bool watch = info.mafiaMode && !info.IsFull;
            joinLabel.Text = info.IsFull ? "ممتلئة" : info.type == RoomType.Private ? "ادخل بالكود" : watch ? "تفرّج" : "انضم";
            joinButton.interactable = !info.IsFull;
            if (joinImage != null) joinImage.sprite = info.IsFull || watch ? th.leatherDark : (info.type == RoomType.Vip ? th.brassPlate : th.leatherRed);
            joinLabel.GetComponent<TMPro.TMP_Text>().color = info.type == RoomType.Vip && !watch && !info.IsFull ? th.brassText : (info.IsFull ? th.ivoryDim : th.ivory);
            if (group != null) group.alpha = info.IsFull ? 0.62f : 1f;
        }
    }
}
