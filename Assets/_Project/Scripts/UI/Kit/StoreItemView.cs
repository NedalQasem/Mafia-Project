using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>One item standing on a shelf: art, name, hanging price priceTag, rarity glow and state marks.</summary>
    public class StoreItemView : MonoBehaviour
    {
        [SerializeField] private RectTransform artRoot;
        [SerializeField] private Image glow;
        [SerializeField] private ArabicText nameLabel, priceLabel, lockLabel;
        [SerializeField] private GameObject priceTag, ownedStamp, newFlag, lockBadge;
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup group;

        public StoreItem Item { get; private set; }
        public event Action<StoreItem> OnClick;

        private Color baseGlow; private bool pulse; private float phase;

        private void Awake() { if (button != null) button.onClick.AddListener(() => OnClick?.Invoke(Item)); phase = UnityEngine.Random.value * 2.4f; var sw = GetComponentInChildren<KitSwing>(true); if (sw != null) sw.Randomize(); }

        private void Update()
        {
            if (!pulse || glow == null || !Application.isPlaying) return;
            float k = 0.75f + 0.25f * Mathf.Cos((Time.unscaledTime + phase) / 2.4f * Mathf.PI * 2f);   // opacity 1 -> .5 -> 1
            var c = baseGlow; c.a *= k; glow.color = c;
        }

        public void Bind(StoreItem it, int playerRank)
        {
            Item = it;
            var th = KitTheme.Current;
            StoreArt.Create(it, artRoot, th);
            nameLabel.Text = it.name;
            bool locked = it.lockRank > playerRank;
            priceTag.SetActive(!it.owned && !locked);
            priceLabel.Text = string.IsNullOrEmpty(it.priceText) ? CoinPill.Format(it.price) : it.priceText;
            ownedStamp.SetActive(it.owned);
            if (it.owned)
            {
                // worn items say so in gold; the others carry the teal "yours" stamp
                Color gold = new Color32(0xF5, 0xD0, 0x70, 255);
                var stampText = ownedStamp.GetComponentInChildren<ArabicText>(true);
                if (stampText != null) { stampText.Text = it.equipped ? "مفعّل" : "ملكك"; stampText.GetComponent<TMPro.TMP_Text>().color = it.equipped ? gold : th.teal; }
                var stampImg = ownedStamp.GetComponent<Image>();
                if (stampImg != null) stampImg.color = it.equipped ? gold : th.teal;
            }
            newFlag.SetActive(it.isNew && !it.owned);
            lockBadge.SetActive(locked);
            if (locked) lockLabel.Text = "رتبة " + KitTheme.RankNames[Mathf.Clamp(it.lockRank - 1, 0, 7)];
            // HTML .r-c/.r-r/.r-e/.r-l colours; the legendary glow also pulses (see Update)
            switch (it.rarity)
            {
                case Rarity.Rare: baseGlow = new Color32(0x4F, 0xA5, 0x8B, 0xAA); break;
                case Rarity.Epic: baseGlow = new Color32(0xC0, 0x32, 0x3A, 0xCC); break;
                case Rarity.Legendary: baseGlow = new Color32(0xF5, 0xD0, 0x70, 0xDD); break;
                default: baseGlow = new Color32(0xD9, 0xD0, 0xBD, 0x55); break;
            }
            glow.color = baseGlow; pulse = it.rarity == Rarity.Legendary;
            if (group != null) group.alpha = locked ? 0.45f : 1f;
        }
    }
}
