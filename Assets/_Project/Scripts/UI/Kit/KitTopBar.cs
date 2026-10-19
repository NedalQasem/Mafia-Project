using System;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Slim top bar: a paper tag with the player's avatar, level, name, title and XP line, plus the coin ticket.
    /// Tapping the tag raises <see cref="OnProfileClicked"/> (the hanging profile card opens from it).
    /// </summary>
    public class KitTopBar : MonoBehaviour
    {
        [SerializeField] private Button profileButton;
        [SerializeField] private AvatarFrame avatar;
        [SerializeField] private ArabicText nameLabel;
        [SerializeField] private ArabicText titleLabel;
        [SerializeField] private ArabicText levelLabel;
        [SerializeField] private RectTransform xpFill;
        [SerializeField] private Image xpRing;      // radial-filled ring around the medal
        [SerializeField] private CoinPill coins;

        public event Action OnProfileClicked;

        private void Awake()
        {
            if (profileButton != null) profileButton.onClick.AddListener(() => OnProfileClicked?.Invoke());
        }

        public void SetPlayer(string playerName, string title, int level, int rankIndex, float xp01, int coinValue, bool animateCoins = false, Sprite frame = null)
        {
            if (nameLabel != null) nameLabel.Text = playerName;
            if (titleLabel != null) titleLabel.Text = title;
            if (levelLabel != null) levelLabel.Text = "م" + CoinPill.Format(level);
            if (avatar != null) { avatar.Set(rankIndex); avatar.SetFrame(frame); }
            LayoutXpRing(frame != null);
            SetXp(xp01);
            if (coins != null) coins.SetValue(coinValue, animateCoins);
        }

        /// <summary>
        /// Without a store frame the XP ring sits behind the avatar, inside the rank bezel. With one, the picture fills the frame,
        /// so the ring moves onto the picture's rim (above it) in a brighter colour and stays visible.
        /// </summary>
        private void LayoutXpRing(bool framed)
        {
            if (xpRing == null) return;
            var medal = xpRing.transform.parent;
            var track = medal.Find("XpTrack") as RectTransform;
            var ringRt = (RectTransform)xpRing.transform;
            float size = framed ? 175f : 152f;
            ringRt.sizeDelta = new Vector2(size, size);
            if (track != null) track.sizeDelta = new Vector2(size, size);
            var level = medal.Find("Level");
            if (framed)
            {
                if (track != null) track.SetAsLastSibling();
                ringRt.SetAsLastSibling();
                if (level != null) level.SetAsLastSibling();
                xpRing.color = new Color32(0xF5, 0xD0, 0x70, 255);
                if (track != null) { var ti = track.GetComponent<Image>(); if (ti != null) ti.color = new Color(1f, 1f, 1f, 0.22f); }
            }
            else
            {
                if (track != null) track.SetSiblingIndex(1);
                ringRt.SetSiblingIndex(2);
                xpRing.color = new Color32(0xD2, 0xAD, 0x60, 255);
                if (track != null) { var ti = track.GetComponent<Image>(); if (ti != null) ti.color = new Color(1f, 1f, 1f, 0.12f); }
            }
        }

        public void SetXp(float xp01)
        {
            xp01 = Mathf.Clamp01(xp01);
            if (xpRing != null) xpRing.fillAmount = xp01;
            if (xpFill == null) return;
            xpFill.anchorMin = new Vector2(1f - xp01, 0f);   // fills from the right (RTL)
            xpFill.anchorMax = new Vector2(1f, 1f);
            xpFill.offsetMin = xpFill.offsetMax = Vector2.zero;
        }
    }
}
