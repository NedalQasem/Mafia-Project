using System;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    public enum RoomType { Public, Private, Vip }

    [Serializable]
    public class RoomInfo
    {
        public string id = "";             // EOS lobby id (empty for mock rooms)
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
        public bool official;              // kept open by the game's team (the Mafia Office room): drawn by KitOfficialCard, pinned first
        public string sub = "";            // official rooms: the line under the name
        public int adminsOnline;           // official rooms: how many of the team are inside
        public bool pulse;                 // official rooms: the card breathes (a new player should see it)
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
        [SerializeField] private GameObject shineClip;
        [SerializeField] private RectTransform shine;
        [SerializeField] private Image liveDot;
        private float phase;

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
            var ofLabel = countLabel.transform.parent != null ? countLabel.transform.parent.Find("Of") : null;
            if (ofLabel != null) { var of = ofLabel.GetComponent<ArabicText>(); if (of != null) of.Text = "من " + CoinPill.Format(info.capacity); }

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
            if (shineClip != null) shineClip.SetActive(info.type == RoomType.Vip);
            phase = UnityEngine.Random.value * 5f;   // cards do not all pulse in step
        }

        // HTML rooms.html: the speaker's seat dot pulses (1.6s), the red live dot blinks (1.2s), the VIP card gets a gold sheen every 5s
        private void Update()
        {
            if (!Application.isPlaying || Info == null) return;
            float t = Time.unscaledTime + phase;
            var th = KitTheme.Current;
            int s = Info.speakingSeat;
            if (s >= 0 && s < seatDots.Length && s < Info.count && seatDots[s] != null && th != null)
            {
                float k = 0.5f + 0.5f * Mathf.Cos(t / 1.6f * Mathf.PI * 2f);   // 1 -> 0 -> 1
                seatDots[s].transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.12f, k);
                seatDots[s].color = Color.Lerp(th.teal * 0.7f, th.teal, k);
            }
            if (liveDot != null && live != null && live.activeSelf)
            {
                var c = liveDot.color; c.a = (t % 1.2f) < 0.6f ? 1f : 0.2f; liveDot.color = c;
            }
            if (shine != null && shineClip != null && shineClip.activeSelf)
            {
                float p = (t % 5f) / 5f, x;
                if (p < 0.6f) x = -1.2f;
                else { float q = (p - 0.6f) / 0.4f; float e = q < .5f ? 2f * q * q : 1f - Mathf.Pow(-2f * q + 2f, 2f) / 2f; x = Mathf.Lerp(-1.2f, 1.2f, e); }
                shine.anchoredPosition = new Vector2(x * 1000f, 0f);
            }
        }
    }
}
