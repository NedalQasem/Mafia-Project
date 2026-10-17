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

        private void Awake() { if (button != null) button.onClick.AddListener(() => OnClick?.Invoke(Item)); }

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
            newFlag.SetActive(it.isNew && !it.owned);
            lockBadge.SetActive(locked);
            if (locked) lockLabel.Text = "رتبة " + KitTheme.RankNames[Mathf.Clamp(it.lockRank - 1, 0, 7)];
            Color rc;
            switch (it.rarity) { case Rarity.Rare: rc = th.teal; break; case Rarity.Epic: rc = new Color(0.85f, 0.2f, 0.25f); break; case Rarity.Legendary: rc = new Color(1f, 0.82f, 0.4f); break; default: rc = th.ivory; break; }
            rc.a = it.rarity == Rarity.Common ? 0.3f : 0.75f; glow.color = rc;
            if (group != null) group.alpha = locked ? 0.45f : 1f;
        }
    }
}
