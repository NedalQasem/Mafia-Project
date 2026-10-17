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
        [SerializeField] private CoinPill coins;

        public event Action OnProfileClicked;

        private void Awake()
        {
            if (profileButton != null) profileButton.onClick.AddListener(() => OnProfileClicked?.Invoke());
        }

        public void SetPlayer(string playerName, string title, int level, int rankIndex, float xp01, int coinValue, bool animateCoins = false)
        {
            if (nameLabel != null) nameLabel.Text = playerName;
            if (titleLabel != null) titleLabel.Text = title;
            if (levelLabel != null) levelLabel.Text = CoinPill.Format(level);
            if (avatar != null) avatar.Set(rankIndex);
            SetXp(xp01);
            if (coins != null) coins.SetValue(coinValue, animateCoins);
        }

        public void SetXp(float xp01)
        {
            if (xpFill == null) return;
            xp01 = Mathf.Clamp01(xp01);
            xpFill.anchorMin = new Vector2(1f - xp01, 0f);   // fills from the right (RTL)
            xpFill.anchorMax = new Vector2(1f, 1f);
            xpFill.offsetMin = xpFill.offsetMax = Vector2.zero;
        }
    }
}
