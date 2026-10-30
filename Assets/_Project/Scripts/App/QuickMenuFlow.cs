using System.Collections;
using System.Collections.Generic;
using MafiaNight.Core;
using MafiaNight.Services.Config;
using MafiaNight.Services.Economy;
using MafiaNight.Services.Rooms;
using MafiaNight.Services.Scenes;
using MafiaNight.UI.Kit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MafiaNight.App
{
    /// <summary>
    /// Puts the quick menu (<see cref="KitQuickMenu"/>) on the home screen and keeps it fed: the offers (the same ones the launch banner shows,
    /// without the ones the player blocked) and the events (<see cref="KitEventsBoard"/>, from <see cref="EventsService"/>). The offers icon
    /// reopens them after the banner was closed; blocking hides an offer until it ends, with an undo. The events icon opens the board, where a
    /// finished challenge is claimed (the server pays). The menu hides itself while the player is in a room.
    /// </summary>
    public sealed class QuickMenuFlow : MonoBehaviour
    {
        private const float Settle = 1.0f, RefetchEvery = 300f;

        private IRoomService rooms;
        private ISceneLoader scenes;
        private KitQuickMenu menu;
        private List<StoreAdCarousel.Ad> all = new List<StoreAdCarousel.Ad>();
        private KitEventsBoard board;
        private EventsBoard events;
        private float refetchAt;
        private bool loading, claiming;

        private void Start()
        {
            if (SceneManager.GetActiveScene().name != AppScenes.Home) { Destroy(this); return; }
            ServiceLocator.TryGet(out rooms);
            ServiceLocator.TryGet(out scenes);
            refetchAt = Time.unscaledTime + Settle;
        }

        private void OnDestroy()
        {
            BlockedOffers.Changed -= OnBlockedChanged;
            if (menu != null) Destroy(menu.gameObject);
            if (board != null) Destroy(board.gameObject);
        }

        private void Update()
        {
            if (menu != null) menu.SetSuppressed(rooms != null && rooms.InRoom);
            if (loading || Time.unscaledTime < refetchAt) return;
            loading = true;
            StartCoroutine(Load());
        }

        private IEnumerator Load()
        {
            List<StoreAdCarousel.Ad> ads = null;
            yield return LaunchBannerFlow.LoadAds(r => ads = r);
            all = ads ?? new List<StoreAdCarousel.Ad>();
            yield return EventsService.Fetch(b => events = b ?? events);
            refetchAt = Time.unscaledTime + RefetchEvery;
            loading = false;
            if (menu == null && !Create()) { refetchAt = float.MaxValue; yield break; }
            Feed();
        }

        private bool Create()
        {
            var th = KitTheme.Current;
            var canvas = InviteFlow.FindCanvas();
            if (th == null || th.quickMenuPrefab == null || canvas == null) return false;
            var go = Instantiate(th.quickMenuPrefab, canvas, false);
            go.transform.SetAsLastSibling();
            menu = go.GetComponent<KitQuickMenu>();
            menu.OnBlock += id => { foreach (var ad in all) if (ad.id == id) { BlockedOffers.Block(ad); break; } };
            menu.OnUndo += BlockedOffers.Unblock;
            menu.OnBuyOffer += id => { KitStoreScreen.PendingAdId = id; Open(AppScenes.Store); };
            menu.OnOpenPaper += () => LaunchBannerFlow.ShowNow(BlockedOffers.Filter(all));
            menu.OnOpenEvents += () => { if (board == null) StartCoroutine(OpenEvents()); };
            BlockedOffers.Changed += OnBlockedChanged;
            return true;
        }

        // ---------------------------------------------------------------- the events board
        private IEnumerator OpenEvents()
        {
            var th = KitTheme.Current;
            var canvas = InviteFlow.FindCanvas();
            if (th == null || th.eventsBoardPrefab == null || canvas == null) yield break;
            yield return EventsService.Fetch(b => events = b ?? events);      // fresh numbers every time it opens
            if (board != null) yield break;
            if (events == null) events = new EventsBoard();      // no answer from the server: the board opens empty
            var go = Instantiate(th.eventsBoardPrefab, canvas, false);
            go.transform.SetAsLastSibling();
            board = go.GetComponent<KitEventsBoard>();
            board.OnAction += OnEventAction;
            board.OnClosed += () => board = null;
            board.Show(Cards(events.Live, 0), Cards(events.Soon, 1), Cards(events.Done, 2));
            if (menu != null) menu.SetEvents(events.Live.Count);
        }

        private static List<KitEventsBoard.Card> Cards(List<GameEvent> list, int state)
        {
            var cards = new List<KitEventsBoard.Card>();
            foreach (var e in list)
                cards.Add(new KitEventsBoard.Card
                {
                    id = e.Id, kicker = e.Kicker, title = e.Title, body = e.Body, kind = (int)e.Kind == 1 ? 1 : (int)e.Kind == 2 ? 2 : 0, state = state,
                    multiplier = e.Multiplier, goal = e.Goal, progress = e.Progress, reward = e.RewardCoins, countsGames = e.CountsGames,
                    claimed = e.Claimed, claimable = e.Claimable, secret = e.Secret, seconds = state == 1 ? e.StartsInSeconds : e.EndsInSeconds,
                });
            return cards;
        }

        private void OnEventAction(KitEventsBoard.Card c)
        {
            if (c.kind == 1) { if (board != null) board.Close(); return; }       // a bonus: go and play (the rooms are this screen)
            if (c.kind == 2 && c.claimable && !claiming) StartCoroutine(Claim(c));
        }

        private IEnumerator Claim(KitEventsBoard.Card c)
        {
            claiming = true;
            int coins = 0;
            yield return EventsService.Claim(c.id, r => coins = r);
            claiming = false;
            if (coins > 0 && ServiceLocator.TryGet(out ICurrencyService wallet)) wallet.FetchCurrencies();
            if (board != null) board.ClaimDone(c.id, coins);
        }

        private void Open(string scene)
        {
            if (scenes != null) scenes.LoadScene(scene); else SceneManager.LoadScene(scene);
        }

        private void Feed()
        {
            menu.SetOffers(BlockedOffers.Filter(all));
            menu.SetEvents(events != null ? events.Live.Count : 0);
        }

        private void OnBlockedChanged(string adId, bool blocked)
        {
            if (menu == null) return;
            Feed();
            if (blocked) menu.ShowUndo(adId, "ما رح يظهر لك هالعرض بعد هلأ");
        }
    }
}
