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
        private KitQuickNotice notice;
        private bool offersSeenOnce;

        private void Start()
        {
            if (SceneManager.GetActiveScene().name != AppScenes.Home) { Destroy(this); return; }
            ServiceLocator.TryGet(out rooms);
            ServiceLocator.TryGet(out scenes);
            KitImages.Loader = MailService.LoadImage;
            refetchAt = Time.unscaledTime + Settle;
        }

        private void OnEnable() { chatService = MafiaNight.Services.Chat.ChatService.Ensure(); chatService.OnServerEvent += OnServerEvent; }
        private void OnDisable() { if (chatService != null) chatService.OnServerEvent -= OnServerEvent; }

        // the server announces new mail on the chat poll the moment the office sends it: read the mail now instead of at the next two-minute check
        private MafiaNight.Services.Chat.ChatService chatService;
        private void OnServerEvent(MafiaNight.Services.Chat.ServerEvent e) { if (e != null && e.Type == "mail") mailAt = 0f; }

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
            NoticeEvents(); NoticeOffers();
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
            if (th.quickNoticePrefab != null)
            {
                notice = Instantiate(th.quickNoticePrefab).GetComponent<KitQuickNotice>();
                notice.Attach(menu);
                notice.CanShow = Free;
                notice.IsSeen = Seen;
                notice.Shown = MarkSeen;
            }
            return true;
        }

        // ---------------------------------------------------------------- the mail
        // the number on the icon is checked every two minutes; while the mail is open, every 20 seconds, so a new telegram shows up in the list
        private const float MailEvery = 120f, MailOpenEvery = 20f;

        private IEnumerator RefreshMail()
        {
            string before = MailSignature();
            yield return MailService.Fetch(m => mailData = m ?? mailData);
            mailAt = Time.unscaledTime + (mailBoard != null ? MailOpenEvery : MailEvery);
            mailLoading = false;
            if (menu != null) menu.SetMail(mailData != null ? mailData.Unread : 0);
            if (mailBoard != null && !mailClaiming && MailSignature() != before) mailBoard.Refresh(MailCards());
            if (mailBoard == null) NoticeMail(); else SeeMail();
            TryLetter();
        }

        // ---------------------------------------------------------------- private messages (note / warning)
        // A message from the office lands over the screen as a letter the moment it is read from the server (the mail check runs at once when the
        // server announces new mail). Pressing its button tells the server it was read. Each message pops up once per run, even if telling the server failed.
        private readonly HashSet<string> lettersShown = new HashSet<string>();
        private bool letterOpen;

        private void TryLetter()
        {
            if (letterOpen || mailData == null || (rooms != null && rooms.InRoom)) return;
            if (FindAnyObjectByType<KitRewardPopup>() != null || FindAnyObjectByType<KitLaunchBanner>() != null || FindAnyObjectByType<KitDirectLetter>() != null) return;
            MailItem pick = null;
            foreach (var m in mailData.Items)
            {
                if (!m.IsMessage || m.Claimed > 0 || lettersShown.Contains(m.Id)) continue;
                if (pick == null || (m.Kind == "warn" && pick.Kind != "warn") || (m.Kind == pick.Kind && m.Ts < pick.Ts)) pick = m;
            }
            if (pick != null) ShowLetter(pick.Id);
        }

        private void ShowLetter(string id)
        {
            var th = KitTheme.Current;
            var canvas = InviteFlow.FindCanvas();
            MailItem m = null;
            if (mailData != null) foreach (var x in mailData.Items) if (x.Id == id) m = x;
            if (m == null || th == null || th.directLetterPrefab == null || canvas == null) return;
            lettersShown.Add(id);
            var go = Instantiate(th.directLetterPrefab, canvas, false);
            go.transform.SetAsLastSibling();
            var letter = go.GetComponent<KitDirectLetter>();
            letterOpen = true;
            letter.OnDismissed += () => { letterOpen = false; StartCoroutine(MarkRead(id)); };
            letter.Show(new KitDirectLetter.Data { type = m.Kind == "warn" ? 2 : 1, title = m.Title, body = m.Body, imageId = m.ImageId, must = m.Must, when = m.Claimed > 0 ? Ago(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - m.Ts) : "هلأ" });
        }

        private IEnumerator MarkRead(string id)
        {
            bool ok = false;
            yield return MailService.Read(id, r => ok = r);
            if (mailData == null) yield break;
            foreach (var m in mailData.Items)
                if (m.Id == id && m.Claimed == 0) { m.Claimed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); mailData.Unread = Math.Max(0, mailData.Unread - 1); }
            if (menu != null) menu.SetMail(mailData.Unread);
            if (mailBoard != null) mailBoard.Refresh(MailCards());
            if (!ok) mailAt = Time.unscaledTime + 30f;          // the server did not get it: the next check shows the message as unread again
        }

        /// <summary>What the list shows (ids and collected marks): the open board is rebuilt only when this changes.</summary>
        private string MailSignature()
        {
            if (mailData == null || mailData.Items == null) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var m in mailData.Items) sb.Append(m.Id).Append(m.Claimed > 0 ? '+' : '-');
            return sb.ToString();
        }

        // back from another app: the mail, the coins and the name may have changed meanwhile (the admin board, a gift)
        private void OnApplicationFocus(bool focus) { if (focus) mailAt = 0f; }

        private IEnumerator OpenMail()
        {
            var th = KitTheme.Current;
            var canvas = InviteFlow.FindCanvas();
            if (th == null || th.mailBoardPrefab == null || canvas == null) yield break;
            yield return MailService.Fetch(m => mailData = m ?? mailData);
            if (mailBoard != null) yield break;
            if (mailData == null) mailData = new MailBoxData();
            SeeMail();
            var go = Instantiate(th.mailBoardPrefab, canvas, false);
            go.transform.SetAsLastSibling();
            mailBoard = go.GetComponent<KitMailBoard>();
            mailBoard.OnClaim += c => { if (c.type != 0) { if (!letterOpen) ShowLetter(c.id); } else if (!mailClaiming) StartCoroutine(ClaimMail(c)); };
            mailBoard.OnClaimAll += () => { if (!mailClaiming) StartCoroutine(ClaimAllMail()); };
            mailBoard.OnRedeem += code => StartCoroutine(RedeemCode(code));
            mailBoard.OnClosed += () => { mailBoard = null; mailAt = 0f; };      // the number on the icon is refreshed right after
            mailBoard.Show(MailCards());
            mailAt = Time.unscaledTime + MailOpenEvery;
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
                    type = m.Kind == "warn" ? 2 : m.Kind == "note" ? 1 : 0, imageId = m.ImageId, must = m.Must,
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
                // the loot briefcase reads the wallet again when the player collects; without it (nothing to show) the wallet is read here
                bool shown = RewardFlow.Show("من بريد الحارة", c.title, RewardFlow.FromPack(r.Coins, r.Gems, r.Items));
                if (!shown && (r.Coins > 0 || r.Gems > 0) && ServiceLocator.TryGet(out ICurrencyService wallet)) wallet.FetchCurrencies();
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
            bool shown = r != null && r.Ok && r.Claimed > 0 && RewardFlow.Show("البريد", "استلمت " + CoinPill.Format(r.Claimed) + " برقيات", RewardFlow.FromPack(r.Coins, r.Gems, r.Items));
            if (!shown && r != null && r.Ok && (r.Coins > 0 || r.Gems > 0) && ServiceLocator.TryGet(out ICurrencyService wallet)) wallet.FetchCurrencies();
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
            SeeEvents();
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
            bool shown = coins > 0 && RewardFlow.Show("جائزة من أخبار الحارة", c.title, RewardFlow.FromPack(coins, 0, null));
            if (!shown && coins > 0 && ServiceLocator.TryGet(out ICurrencyService wallet)) wallet.FetchCurrencies();
            if (board != null) board.ClaimDone(c.id, coins);
        }

        private void Open(string scene)
        {
            if (scenes != null) scenes.LoadScene(scene); else SceneManager.LoadScene(scene);
        }

        // ---------------------------------------------------------------- the notices that slide out of the tab
        // Every piece of news has a key; once noticed (or seen on its own board) it is kept on the device and never shown again.
        private const string SeenPref = "mafia.quick.seen";
        private const int KeepSeen = 300;
        private List<string> seenList;
        private HashSet<string> seenSet;
        private float freeCheckAt; private bool freeCache;
        private readonly HashSet<string> queued = new HashSet<string>();      // waiting in the notice's queue (seen only once it really comes out)

        private bool Fresh(string key) => !Seen(key) && !queued.Contains(key);

        private bool Seen(string key)
        {
            if (seenSet == null) { seenList = new List<string>(PlayerPrefs.GetString(SeenPref, "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)); seenSet = new HashSet<string>(seenList); }
            return seenSet.Contains(key);
        }

        private void MarkSeen(string key)
        {
            if (Seen(key)) return;
            seenSet.Add(key); seenList.Add(key);
            if (seenList.Count > KeepSeen) { seenSet.Remove(seenList[0]); seenList.RemoveAt(0); }
            PlayerPrefs.SetString(SeenPref, string.Join("|", seenList));
        }

        /// <summary>No sheet, popup or banner is over the home screen (checked twice a second).</summary>
        private bool Free()
        {
            if (Time.unscaledTime < freeCheckAt) return freeCache;
            freeCheckAt = Time.unscaledTime + .5f;
            freeCache = mailBoard == null && board == null && FindAnyObjectByType<KitLaunchBanner>() == null && FindAnyObjectByType<KitRewardPopup>() == null;
            return freeCache;
        }

        private static string Pack(int coins, int gems, int items)
        {
            var parts = new List<string>();
            if (coins > 0) parts.Add(CoinPill.Format(coins) + " كوينز");
            if (gems > 0) parts.Add(CoinPill.Format(gems) + " جوهرة");
            if (items > 0) parts.Add(items == 1 ? "غرض من المتجر" : CoinPill.Format(items) + " أغراض");
            return string.Join(" + ", parts);
        }

        private void NoticeMail()
        {
            if (notice == null || mailData == null) return;
            var fresh = new List<MailItem>();
            foreach (var m in mailData.Items) if (m.Claimed == 0 && !m.IsMessage && Fresh("ml:" + m.Id)) fresh.Add(m);       // messages come as a letter, not a notice
            if (fresh.Count == 0) return;
            var keys = new string[fresh.Count];
            for (int i = 0; i < keys.Length; i++) { keys[i] = "ml:" + fresh[i].Id; queued.Add(keys[i]); }
            Action open = () => { if (mailBoard == null) StartCoroutine(OpenMail()); };
            if (fresh.Count == 1)
            {
                var m = fresh[0]; string pack = Pack(m.Coins, m.Gems, m.Items.Length);
                notice.Enqueue(KitQuickNotice.Kind.Mail, "برقية جديدة", string.IsNullOrEmpty(pack) ? m.Title : m.Title + " · " + pack, open, keys);
            }
            else notice.Enqueue(KitQuickNotice.Kind.Mail, "وصلك " + CoinPill.Format(fresh.Count) + " برقيات", fresh[0].Title + "، وغيرها", open, keys);
        }

        private void SeeMail() { if (mailData != null) foreach (var m in mailData.Items) MarkSeen("ml:" + m.Id); }

        private void NoticeEvents()
        {
            if (notice == null || events == null) return;
            Action open = () => { if (board == null) StartCoroutine(OpenEvents()); };
            // a prize to collect first, then an event about to end, then the new ones
            foreach (var list in new[] { events.Live, events.Done })
                foreach (var e in list)
                    if (e.Claimable && !e.Claimed && Fresh("rw:" + e.Id))
                    {
                        queued.Add("rw:" + e.Id);
                        notice.Enqueue(KitQuickNotice.Kind.Reward, "جائزتك جاهزة!", e.Title + (e.RewardCoins > 0 ? " · استلم " + CoinPill.Format(e.RewardCoins) + " كوينز" : ""), open, "rw:" + e.Id);
                    }
            foreach (var e in events.Live)
                if (e.EndsInSeconds > 0 && e.EndsInSeconds <= 3600 && (e.HasRanking || ((int)e.Kind == 2 && e.Progress > 0 && !e.Claimed)) && Fresh("end:" + e.Id))
                {
                    queued.Add("end:" + e.Id);
                    notice.Enqueue(KitQuickNotice.Kind.Ending, e.Title + " بتخلص", "باقي " + Span(e.EndsInSeconds * 1000L) + (e.RankMine > 0 ? " · إنت بالمركز " + CoinPill.Format(e.RankMine) : ""), open, "end:" + e.Id);
                }
            int fresh = 0;
            foreach (var e in events.Live)
                if (Fresh("ev:" + e.Id))
                {
                    if (++fresh > 2) { MarkSeen("ev:" + e.Id); continue; }     // a pile of new events at once: two notices are enough
                    queued.Add("ev:" + e.Id);
                    notice.Enqueue(KitQuickNotice.Kind.Event, "حدث جديد: " + e.Title, !string.IsNullOrEmpty(e.Kicker) ? e.Kicker : (e.Body ?? ""), open, "ev:" + e.Id);
                }
        }

        private void SeeEvents()
        {
            if (events == null) return;
            foreach (var e in events.Live) { MarkSeen("ev:" + e.Id); if (e.Claimable) MarkSeen("rw:" + e.Id); }
            foreach (var e in events.Done) if (e.Claimable) MarkSeen("rw:" + e.Id);
        }

        private void NoticeOffers()
        {
            if (notice == null) return;
            var shown = BlockedOffers.Filter(all);
            // the first load of the run: the launch banner shows the offers anyway
            if (!offersSeenOnce) { offersSeenOnce = true; foreach (var ad in shown) MarkSeen("of:" + ad.id); return; }
            foreach (var ad in shown)
                if (Fresh("of:" + ad.id))
                {
                    queued.Add("of:" + ad.id);
                    notice.Enqueue(KitQuickNotice.Kind.Offer, "عرض جديد بالسوق", (ad.headline ?? "").Replace('\n', ' '), () => { if (menu != null) menu.OpenOffers(); }, "of:" + ad.id);
                }
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
