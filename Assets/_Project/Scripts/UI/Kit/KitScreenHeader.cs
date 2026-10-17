using System;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Slim header for full-screen pages (store, chat, settings): back button, title + subtitle and an optional coin ticket.
    /// These pages hide the player bar and the bottom navigation to leave the space to the content.
    /// </summary>
    public sealed class KitScreenHeader : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private CoinPill coins;

        public event Action OnBack;

        private void Awake() { if (backButton != null) backButton.onClick.AddListener(() => OnBack?.Invoke()); }

        private void Start()
        {
            if (!Application.isPlaying || coins == null) return;
            var store = FindAnyObjectByType<KitStoreScreen>();
            if (store == null) return;
            coins.SetValue(store.Coins, false);
            store.OnCoinsChanged += c => coins.SetValue(c, true);
        }

        public void SetCoins(int value, bool animate = false) { if (coins != null) coins.SetValue(value, animate); }
    }
}
