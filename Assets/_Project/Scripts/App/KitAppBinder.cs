using MafiaNight.Core;
using MafiaNight.Data.Player;
using MafiaNight.Services.Economy;
using MafiaNight.Services.Profile;
using MafiaNight.Services.Scenes;
using MafiaNight.UI.Kit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MafiaNight.App
{
    /// <summary>
    /// Glue between the UI-kit screens and the real services. It is created automatically in any scene that has
    /// the kit bars, so the screens keep working as stand-alone prototypes when the services are missing
    /// (the scene was opened without going through the Bootstrap).
    /// Rooms, chat and leaderboards still show demo data: they have no backend service yet.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class KitAppBinder : MonoBehaviour
    {
        private const string Tag = "AppBinder";

        private IPlayerProfileService profile;
        private ICurrencyService currency;
        private ISceneLoader sceneLoader;
        private KitTopBar topBar;
        private KitNavBar navBar;
        private KitProfileCard card;
        private KitStoreScreen store;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        private static void Attach()
        {
            if ((FindAnyObjectByType<KitTopBar>() == null && FindAnyObjectByType<KitLeaderboardsScreen>() == null) || FindAnyObjectByType<KitAppBinder>() != null) return;
            new GameObject("KitAppBinder").AddComponent<KitAppBinder>();
        }

        private void Start()
        {
            topBar = FindAnyObjectByType<KitTopBar>();
            navBar = FindAnyObjectByType<KitNavBar>();
            if (!ServiceLocator.TryGet(out profile) || !ServiceLocator.TryGet(out currency))
            {
                Log.Info("Services not available - screen stays in demo mode", Tag);
                Destroy(gameObject);
                return;
            }
            ServiceLocator.TryGet(out sceneLoader);
            card = FindAnyObjectByType<KitProfileCard>(FindObjectsInactive.Include);
            store = FindAnyObjectByType<KitStoreScreen>();

            profile.OnProfileLoaded += OnProfile;
            profile.OnProfileUpdated += OnProfile;
            currency.OnCoinsChanged += OnCoins;
            if (navBar != null) navBar.OnTabSelected += OnTab;
            if (store != null) WireStore();
            var lb = FindAnyObjectByType<KitLeaderboardsScreen>();
            if (lb != null) lb.OnBack += GoHome;

            Refresh();
        }

        private void OnDestroy()
        {
            if (profile != null) { profile.OnProfileLoaded -= OnProfile; profile.OnProfileUpdated -= OnProfile; }
            if (currency != null) currency.OnCoinsChanged -= OnCoins;
            if (navBar != null) navBar.OnTabSelected -= OnTab;
        }

        private void OnProfile(PlayerProfile _) => Refresh();

        private void OnCoins(int coins)
        {
            if (store != null) store.Coins = coins;
            if (topBar != null) Refresh(true);
        }

        private void GoHome()
        {
            if (sceneLoader != null && SceneManager.GetActiveScene().name != AppScenes.Home) sceneLoader.LoadScene(AppScenes.Home);
        }

        private void OnTab(int index)
        {
            if (sceneLoader == null || index < 0 || index >= AppScenes.ByTab.Length) return;
            string target = AppScenes.ByTab[index];
            if (SceneManager.GetActiveScene().name == target) return;
            sceneLoader.LoadScene(target);
        }

        private void Refresh(bool animateCoins = false)
        {
            var p = profile.LocalProfile;
            if (p == null || topBar == null) return;

            var step = RankLadder.FromPoints(p.RankPoints);
            string title = KitTheme.RankNames[Mathf.Clamp(step.RankIndex, 0, KitTheme.RankNames.Length - 1)];
            string name = string.IsNullOrEmpty(p.DisplayName) ? "لاعب" : p.DisplayName;
            float xp01 = p.GetLevelProgress();

            topBar.SetPlayer(name, title, p.Level, step.RankIndex, xp01, currency.Coins, animateCoins);
            if (card != null)
            {
                var s = p.Statistics ?? new PlayerStatistics();
                card.SetPlayer(name, title, p.Level, step.RankIndex, xp01, p.CurrentXP, p.XPToNextLevel, s.GamesPlayed, s.GamesWon);
                card.SetRoles(s.CitizenGames, s.MafiaGames, 0, 0);
            }
            if (store != null) store.PlayerRank = step.RankIndex + 1;
        }

        private void WireStore()
        {
            store.Coins = currency.Coins;
            store.OwnedResolver = item => profile.LocalProfile?.Inventory != null && profile.LocalProfile.Inventory.OwnsStoreItem(item.id);
            store.PurchaseHandler = Purchase;
        }

        private void Purchase(StoreItem item, System.Action<bool, string> done)
        {
            var p = profile.LocalProfile;
            if (p == null) { done(false, "تعذّر تحميل ملفك. حاول لاحقاً."); return; }
            if (item.lockRank > RankLadder.FromPoints(p.RankPoints).RankIndex + 1) { done(false, "هذا الغرض مقفل لرتبتك."); return; }
            if (p.Inventory != null && p.Inventory.OwnsStoreItem(item.id)) { done(true, "تملكه مسبقاً"); return; }
            if (!currency.HasEnoughCoins(item.price)) { done(false, "رصيدك لا يكفي."); return; }

            // TODO(security): purchases must be validated server-side (CloudScript) before going live.
            currency.DeductCoins(item.price, () =>
            {
                if (p.Inventory == null) p.Inventory = new PlayerInventory();
                p.Inventory.AddStoreItem(item.id);
                profile.SaveProfile();
                done(true, "أُضيف إلى مقتنياتك");
            }, err =>
            {
                Log.Warning("Purchase failed: " + err, Tag);
                done(false, "تعذّر الشراء، حاول لاحقاً.");
            });
        }
    }
}
