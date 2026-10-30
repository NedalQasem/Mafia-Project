using System;
using System.Collections;
using System.Collections.Generic;
using MafiaNight.Core;
using MafiaNight.Data.Store;
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
        private KitMailBoard mailBoard;
        private EventsBoard events;
        private MailBoxData mailData;
        private float mailAt;
        private bool mailLoading, mailClaiming;
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
            if (mailBoard != null) Destroy(mailBoard.gameObject);
        }

        private void Update()
        {
            if (menu != null) menu.SetSuppressed(rooms != null && rooms.InRoom);
            if (menu != null && !mailLoading && Time.unscaledTime >= mailAt) { mailLoading = true; StartCoroutine(RefreshMail()); }
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
            menu.OnOpenMail += () => { if (mailBoard == null) StartCoroutine(OpenMail()); };
            BlockedOffers.Changed += OnBlockedChanged;
            return true;
        }

        // ---------------------------------------------------------------- the mail
        private IEnumerator RefreshMail()
        {
            yield return MailService.Fetch(m => mailData = m ?? mailData);
            mailAt = Time.unscaledTime + 120f;
            mailLoading = false;
            if (menu != null) menu.SetMail(mailData != null ? mailData.Unread : 0);
        }

        private IEnumerator OpenMail()
        {
            var th = KitTheme.Current;
            var canvas = InviteFlow.FindCanvas();
            if (th == null || th.mailBoardPrefab == null || canvas == null) yield break;
            yield return MailService.Fetch(m => mailData = m ?? mailData);
            if (mailBoard != null) yield break;
            if (mailData == null) mailData = new MailBoxData();
            var go = Instantiate(th.mailBoardPrefab, canvas, false);
            go.transform.SetAsLastSibling();
            mailBoard = go.GetComponent<KitMailBoard>();
            mailBoard.OnClaim += c => { if (!mailClaiming) StartCoroutine(ClaimMail(c)); };
            mailBoard.OnClaimAll += () => { if (!mailClaiming) StartCoroutine(ClaimAllMail()); };
            mailBoard.OnRedeem += code => StartCoroutine(RedeemCode(code));
            mailBoard.OnClosed += () => { mailBoard = null; mailAt = 0f; };      // the number on the icon is refreshed right after
            mailBoard.Show(MailCards());
            if (menu != null) menu.SetMail(mailData.Unread);
        }

        private List<KitMailBoard.Card> MailCards()
        {
            var list = new List<KitMailBoard.Card>();
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var catalog = StoreData.Catalog;
            foreach (var m in mailData.Items)
            {
                var names = new string[m.Items.Length];
                for (int i = 0; i < names.Length; i++) { var def = catalog != null ? catalog.GetItem(m.Items[i]) : null; names[i] = def != null && !string.IsNullOrEmpty(def.nameAr) ? def.nameAr : m.Items[i]; }
                list.Add(new KitMailBoard.Card
                {
                    id = m.Id, title = m.Title, body = m.Body, coins = m.Coins, gems = m.Gems, items = names, claimed = m.Claimed > 0,
                    when = Ago(now - m.Ts), expiry = m.Exp > 0 && m.Claimed == 0 ? "تنتهي بعد " + Span(m.Exp - now) : "",
                });
            }
            return list;
        }

        private static string Ago(long ms)
        {
            long s = Math.Max(0, ms / 1000);
            if (s < 60) return "قبل لحظات";
            if (s < 3600) return "قبل " + CoinPill.Format((int)(s / 60)) + " د";
            if (s < 86400) return "قبل " + CoinPill.Format((int)(s / 3600)) + " س";
            return "قبل " + CoinPill.Format((int)(s / 86400)) + " يوم";
        }

        private static string Span(long ms)
        {
            long s = Math.Max(0, ms / 1000);
            if (s < 3600) return CoinPill.Format((int)Math.Max(1, s / 60)) + " دقيقة";
            if (s < 86400) return CoinPill.Format((int)(s / 3600)) + " ساعة";
            return CoinPill.Format((int)(s / 86400)) + " يوم";
        }

        private IEnumerator ClaimMail(KitMailBoard.Card c)
        {
            mailClaiming = true;
            MailClaimResult r = null;
            yield return MailService.Claim(c.id, x => r = x);
            mailClaiming = false;
            bool ok = r != null && r.Ok;
            if (ok)
            {
                foreach (var m in mailData.Items) if (m.Id == c.id) m.Claimed = 1;
                mailData.Unread = Math.Max(0, mailData.Unread - 1);
                if ((r.Coins > 0 || r.Gems > 0) && ServiceLocator.TryGet(out ICurrencyService wallet)) wallet.FetchCurrencies();
            }
            if (mailBoard != null) mailBoard.ClaimDone(c.id, ok, ok ? (r.Coins > 0 || r.Gems > 0 || r.Items.Length > 0 ? "انضاف الرصيد لحسابك" : "تمام") : "ما زبطت، جرّب بعد شوي");
            if (menu != null) menu.SetMail(mailData.Unread);
        }

        private IEnumerator RedeemCode(string code)
        {
            MailClaimResult r = null;
            yield return MailService.Redeem(code, x => r = x);
            if (mailBoard == null) yield break;
            if (r != null && r.Ok)
            {
                yield return MailService.Fetch(m => mailData = m ?? mailData);       // the package is now a telegram in the list
                if (mailBoard != null) { mailBoard.Refresh(MailCards()); mailBoard.CodeResult(true, "الكود صح! نزل البكج على بريدك."); }
                if (menu != null && mailData != null) menu.SetMail(mailData.Unread);
                yield break;
            }
            string why = r == null ? "network" : r.Error;
            mailBoard.CodeResult(false, why == "already_used" ? "استخدمت هالكود قبل." : why == "locked" ? "محاولات كثيرة، جرّب بعد شوي." : why == "bad_code" ? "الكود غير صحيح أو انتهى." : "ما زبطت، جرّب بعد شوي.");
        }

        private IEnumerator ClaimAllMail()
        {
            mailClaiming = true;
            MailClaimResult r = null;
            yield return MailService.ClaimAll(x => r = x);
            mailClaiming = false;
            yield return MailService.Fetch(m => mailData = m ?? mailData);
            if (r != null && r.Ok && (r.Coins > 0 || r.Gems > 0) && ServiceLocator.TryGet(out ICurrencyService wallet)) wallet.FetchCurrencies();
            if (mailBoard != null)
            {
                mailBoard.Refresh(MailCards());
                mailBoard.Toast(r != null && r.Ok ? (r.Claimed > 0 ? "استلمت " + CoinPill.Format(r.Claimed) + " برقيات" : "ما في شي للاستلام") : "ما زبطت، جرّب بعد شوي");
            }
            if (menu != null) menu.SetMail(mailData != null ? mailData.Unread : 0);
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
            board.OnRank += c => StartCoroutine(OpenRanking(c));
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
                    hasRank = e.HasRanking, rankDistributed = e.RankDistributed, rankMetric = e.RankMetric, rankMine = e.RankMine, rankValue = e.RankValue,
                });
            return cards;
        }

        // the ranking sheet of one event: fetched when the player presses "التصنيف"
        private IEnumerator OpenRanking(KitEventsBoard.Card c)
        {
            EventRanking r = null;
            yield return EventsService.Ranking(c.id, x => r = x);
            if (board == null) yield break;
            if (r == null) { board.Toast("ما زبطت، جرّب بعد شوي"); yield break; }
            var d = new KitEventsBoard.RankData { title = c.title, metric = MetricLabel(r.Metric), distributed = r.Distributed, endsText = r.EndsInSeconds > 0 ? "ينتهي بعد " + Span(r.EndsInSeconds * 1000L) : "انتهى" };
            foreach (var row in r.Rows) d.rows.Add((row.Rank, row.Name, row.Value, row.Me));
            if (r.MineRank > 10) d.rows.Add((r.MineRank, "أنت", r.MineValue, true));        // not in the top ten: my own line below the gap
            var catalog = StoreData.Catalog;
            foreach (var p in r.Prizes)
            {
                var names = new List<string>();
                foreach (var id in p.Items) { var def = catalog != null ? catalog.GetItem(id) : null; names.Add(def != null && !string.IsNullOrEmpty(def.nameAr) ? def.nameAr : id); }
                if (p.Coins > 0 && p.Gems > 0) names.Insert(0, CoinPill.Format(p.Gems) + " جوهرة");
                d.prizes.Add((p.From == p.To ? CoinPill.Format(p.From) : CoinPill.Format(p.From) + "-" + CoinPill.Format(p.To), p.Coins, p.Gems, string.Join("، ", names)));
            }
            board.ShowRanking(d);
        }

        private static string MetricLabel(string m) => m == "games" ? "المباريات" : m == "points" ? "نقاط الرتبة" : m == "secs" ? "الدقائق" : "الانتصارات";

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
