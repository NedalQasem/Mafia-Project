using MafiaNight.Core;
using MafiaNight.Data.Player;
using MafiaNight.Services.Economy;
using MafiaNight.Services.Profile;
using MafiaNight.Services.Scenes;
using MafiaNight.Services.Store;
using MafiaNight.Data.Store;
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
        private IStoreService storeService;
        private ISceneLoader sceneLoader;
        private KitTopBar topBar;
        private KitNavBar navBar;
        private KitProfileCard card;
        private KitStoreScreen store;
        private KitScreenHeader header;

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
            bool familyScene = SceneManager.GetActiveScene().name == AppScenes.Family;       // the family screens have no top bar of the kit
            if ((FindAnyObjectByType<KitTopBar>() == null && FindAnyObjectByType<KitLeaderboardsScreen>() == null && FindAnyObjectByType<KitScreenHeader>() == null && !familyScene) || FindAnyObjectByType<KitAppBinder>() != null) return;
            new GameObject("KitAppBinder").AddComponent<KitAppBinder>();
        }

        private void Awake()
        {
            // the prototype wirings of the kit scenes (mock rooms, mock seats) step aside while the real services run
            KitLiveMode.Active = ServiceLocator.IsRegistered<IPlayerProfileService>() && ServiceLocator.IsRegistered<ICurrencyService>();
        }

        private void Start()
        {
            topBar = FindAnyObjectByType<KitTopBar>();
            navBar = FindAnyObjectByType<KitNavBar>();
            if (!ServiceLocator.TryGet(out profile) || !ServiceLocator.TryGet(out currency))
            {
                KitLiveMode.Active = false;
                Log.Info("Services not available - screen stays in demo mode", Tag);
                Destroy(gameObject);
                return;
            }
            ServiceLocator.TryGet(out sceneLoader);
            ServiceLocator.TryGet(out storeService);
            card = FindAnyObjectByType<KitProfileCard>(FindObjectsInactive.Include);
            store = FindAnyObjectByType<KitStoreScreen>();

            profile.OnProfileLoaded += OnProfile;
            profile.OnProfileUpdated += OnProfile;
            currency.OnCoinsChanged += OnCoins;
            if (navBar != null) { navBar.OnTabSelected += OnTab; gameObject.AddComponent<NavBadgesFlow>(); }
            if (storeService != null) storeService.OnChanged += OnStoreChanged;
            if (store != null) WireStore();
            if (FindAnyObjectByType<KitRoomsScreen>() != null && ServiceLocator.IsRegistered<MafiaNight.Services.Rooms.IRoomService>())
            {
                gameObject.AddComponent<RoomsFlow>();
                MafiaNight.Services.Match.MatchRunner.Ensure();
            }
            if (FindAnyObjectByType<KitSettingsScreen>() != null) { gameObject.AddComponent<VoiceSettingsFlow>(); gameObject.AddComponent<RecoveryFlow>(); }
            MafiaNight.Services.Chat.ChatService.Ensure();   // listens for messages in every scene (the chat screen reads from it)
            if (FindAnyObjectByType<KitChatScreen>(FindObjectsInactive.Include) != null) gameObject.AddComponent<ChatFlow>();
            MafiaNight.Services.Friends.FriendsService.Ensure();   // reports where the player sits and hears friend news in every scene
            InviteFlow.Ensure();
            ReconnectFlow.Ensure();      // offers the way back into a match the player lost with his connection
            if (SceneManager.GetActiveScene().name == AppScenes.Home) { gameObject.AddComponent<LaunchBannerFlow>(); gameObject.AddComponent<QuickMenuFlow>(); }       // the offers banner at the start of the game, and the quick menu that brings it back
            if (SceneManager.GetActiveScene().name == AppScenes.Family && FindAnyObjectByType<KitMatchView>(FindObjectsInactive.Include) != null) gameObject.AddComponent<FamilyFlow>();
            var lb = FindAnyObjectByType<KitLeaderboardsScreen>();
            if (lb != null) { lb.OnBack += GoHome; gameObject.AddComponent<LeaderboardsFlow>(); }
            header = FindAnyObjectByType<KitScreenHeader>();
            if (header != null) { header.OnBack += GoHome; header.SetCoins(currency.Coins); }

            Refresh();
        }

        private void OnDestroy()
        {
            if (profile != null) { profile.OnProfileLoaded -= OnProfile; profile.OnProfileUpdated -= OnProfile; }
            if (currency != null) currency.OnCoinsChanged -= OnCoins;
            if (navBar != null) navBar.OnTabSelected -= OnTab;
            if (storeService != null) storeService.OnChanged -= OnStoreChanged;
        }

        private void OnStoreChanged() => Refresh();

        private void OnProfile(PlayerProfile _) => Refresh();

        private void OnCoins(int coins)
        {
            if (store != null) store.Coins = coins;
            if (header != null) header.SetCoins(coins, true);
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

            // what the player wears: the frame goes around the avatar, the title replaces the rank under the name
            Sprite frame = null;
            if (storeService != null && storeService.Catalog != null)
            {
                var f = storeService.Catalog.GetItem(storeService.GetEquipped("frames"));
                if (f != null) frame = f.sprite != null ? f.sprite : FrameSprite(f.variant);
                var t = storeService.Catalog.GetItem(storeService.GetEquipped("titles"));
                if (t != null && !string.IsNullOrEmpty(t.titleText)) title = t.titleText;
            }

            topBar.SetPlayer(name, title, p.Level, step.RankIndex, xp01, currency.Coins, animateCoins, frame);
            if (card != null)
            {
                var s = p.Statistics ?? new PlayerStatistics();
                card.SetPlayer(name, title, p.Level, step.RankIndex, xp01, p.CurrentXP, p.XPToNextLevel, s.GamesPlayed, s.GamesWon, frame);
                card.SetRoles(s.CitizenGames, s.MafiaGames, 0, 0);
            }
            if (store != null) store.PlayerRank = step.RankIndex + 1;
        }

        private static Sprite FrameSprite(int variant)
        {
            var th = KitTheme.Current;
            return th != null && th.storeFrames != null && variant >= 1 && variant <= th.storeFrames.Length ? th.storeFrames[variant - 1] : null;
        }

        private void WireStore()
        {
            store.Coins = currency.Coins;
            if (storeService == null) return;   // no store service: the screen keeps its local demo behaviour
            store.OwnedResolver = item => storeService.Owns(item.id);
            store.EquippedResolver = item => storeService.IsEquipped(item.id);
            store.EquipHandler = item => storeService.Equip(item.id);
            store.PurchaseHandler = (item, done) => storeService.Purchase(item.id, item.price, item.storeId, done);
        }
    }
}
