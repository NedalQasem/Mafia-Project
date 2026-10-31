using System;
using System.Collections.Generic;
using System.Collections;
using MafiaNight.Core;
using MafiaNight.Data.Store;
using MafiaNight.Services.Config;
using MafiaNight.Services.Rooms;
using MafiaNight.Services.Scenes;
using MafiaNight.UI.Kit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MafiaNight.App
{
    /// <summary>
    /// Opens the offers banner (<see cref="KitLaunchBanner"/>) over the home screen once per start of the game. Never on the very first start
    /// (the tutorial comes first), never in a room or a match, never without offers; "don't show it again today" keeps it away for the rest
    /// of the day unless a new offer appears. "Buy" opens the store on that offer's buy sheet.
    /// </summary>
    public sealed class LaunchBannerFlow : MonoBehaviour
    {
        private const float Wait = 1.4f;                       // let the home screen settle after the loading screen
        private const string SeenKey = "mafia.launch.seen", HideDayKey = "mafia.launch.hideDay", SigKey = "mafia.launch.sig";
        private const int MaxOffers = 3;

        private static bool shownThisRun;

        private IRoomService rooms;
        private float showAt;

        private void Start()
        {
            if (shownThisRun || SceneManager.GetActiveScene().name != AppScenes.Home) { Destroy(this); return; }
            ServiceLocator.TryGet(out rooms);
            showAt = Time.unscaledTime + Wait;
        }

        private void Update()
        {
            if (Time.unscaledTime < showAt) return;
            showAt = float.MaxValue;
            StartCoroutine(Run());
        }

        private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

        private IEnumerator Run()
        {
            if (rooms != null && rooms.InRoom) { Destroy(this); yield break; }
            var mr = MafiaNight.Services.Match.MatchRunner.Instance;
            if (mr != null && mr.Session != null && mr.Session.Client.Phase != MafiaNight.MatchRules.Phase.Lobby && mr.Session.Client.Phase != MafiaNight.MatchRules.Phase.GameOver) { Destroy(this); yield break; }
            if (!PlayerPrefs.HasKey(SeenKey)) { PlayerPrefs.SetInt(SeenKey, 1); PlayerPrefs.Save(); Destroy(this); yield break; }     // first start ever

            List<StoreAdCarousel.Ad> ads = null;
            yield return LoadAds(r => ads = r);
            ads = BlockedOffers.Filter(ads);                // what the player blocked stays out
            if (ads.Count == 0) { Destroy(this); yield break; }
            string sig = string.Join(",", ads.ConvertAll(a => a.id));
            bool hiddenToday = PlayerPrefs.GetString(HideDayKey, "") == Today && PlayerPrefs.GetString(SigKey, "") == sig;
            if (hiddenToday) { Destroy(this); yield break; }

            shownThisRun = ShowNow(ads, hide =>
            {
                PlayerPrefs.SetString(SigKey, sig);
                PlayerPrefs.SetString(HideDayKey, hide ? Today : "");
                PlayerPrefs.Save();
            });
            Destroy(this);
        }

        /// <summary>
        /// The offers of the moment: the server decides what is promoted (its config); without an answer or without offers the game's own
        /// store ads are used. Blocked offers are still in the list (see <see cref="BlockedOffers.Filter"/>).
        /// </summary>
        public static IEnumerator LoadAds(Action<List<StoreAdCarousel.Ad>> done)
        {
            List<ServerOffer> fromServer = null;
            yield return OffersService.Fetch(r => fromServer = r);
            var ads = fromServer != null && fromServer.Count > 0 ? Resolve(fromServer) : new List<StoreAdCarousel.Ad>();
            if (ads.Count == 0) ads = Offers();
            done(ads);
        }

        /// <summary>Opens the banner over the current screen. Returns false when the kit prefab or a canvas is missing.</summary>
        public static bool ShowNow(IList<StoreAdCarousel.Ad> ads, Action<bool> closed = null)
        {
            var th = KitTheme.Current;
            var canvas = InviteFlow.FindCanvas();
            if (th == null || th.launchBannerPrefab == null || canvas == null || ads == null || ads.Count == 0) return false;
            ServiceLocator.TryGet(out ISceneLoader scenes);
            var go = Instantiate(th.launchBannerPrefab, canvas, false);
            go.transform.SetAsLastSibling();
            var banner = go.GetComponent<KitLaunchBanner>();
            void OpenStore() { banner.Close(); if (scenes != null) scenes.LoadScene(AppScenes.Store); else SceneManager.LoadScene(AppScenes.Store); }
            banner.OnBuy += (adId, item) => { KitStoreScreen.PendingAdId = adId; OpenStore(); };
            banner.OnMore += OpenStore;
            banner.OnBlock += adId =>
            {
                foreach (var ad in ads) if (ad.id == adId) { BlockedOffers.Block(ad); break; }
                banner.Close();
            };
            if (closed != null) banner.OnClosed += closed;
            banner.Show(ads);
            return true;
        }

        private static StoreAdCarousel.Ad MakeAd(StoreAdDef def, ServerOffer over)
        {
            string itemId = !string.IsNullOrEmpty(def != null ? def.itemId : null) ? def.itemId : over != null ? over.ItemId : null;
            if (over != null && !string.IsNullOrEmpty(over.ItemId) && def == null) itemId = over.ItemId;
            var item = StoreData.Find(itemId);
            if (item == null) return null;
            string was = null;
            if (def != null && def.offerPrice > 0 && def.offerPrice != item.price) { was = CoinPill.Format(item.price); item.price = def.offerPrice; item.storeId = def.offerStoreId; }
            string price = !string.IsNullOrEmpty(item.priceText) ? item.priceText : CoinPill.Format(item.price);
            string Pick(string server, string own) => !string.IsNullOrEmpty(server) ? server : own;
            int seconds = over != null && over.EndsInSeconds > 0 ? over.EndsInSeconds : (over == null && def != null ? def.countdownSeconds : 0);
            return new StoreAdCarousel.Ad
            {
                id = def != null ? def.id : "item_" + itemId, imageId = over != null ? over.Image : null, price = price, was = was, item = item, countdown = seconds > 0, countdownSeconds = seconds,
                mast = Pick(over?.Mast, def != null ? def.mast : ""), headline = Pick(over?.Headline, def != null ? def.headline : item.name), body = Pick(over?.Body, def != null ? def.body : item.desc), stamp = Pick(over?.Stamp, def != null ? def.stamp : ""),
            };
        }

        /// <summary>The server's offers, each tied to an ad of the store catalog (adId) or to a store item (itemId); texts it sends replace the game's.</summary>
        private static List<StoreAdCarousel.Ad> Resolve(List<ServerOffer> offers)
        {
            var list = new List<StoreAdCarousel.Ad>();
            var catalog = StoreData.Catalog;
            foreach (var o in offers)
            {
                var def = catalog != null && !string.IsNullOrEmpty(o.AdId) ? catalog.ads.Find(a => a.id == o.AdId) : null;
                var ad = MakeAd(def, o);
                if (ad != null) list.Add(ad);
                if (list.Count == MaxOffers) break;
            }
            return list;
        }

        /// <summary>The game's own offers: the first enabled ads of the store catalog (the same pages as the store's newspaper ads), at most three.</summary>
        private static List<StoreAdCarousel.Ad> Offers()
        {
            var list = new List<StoreAdCarousel.Ad>();
            var catalog = StoreData.Catalog;
            if (catalog == null) return list;
            foreach (var def in catalog.ads)
            {
                if (!def.enabled) continue;
                var ad = MakeAd(def, null);
                if (ad != null) list.Add(ad);
                if (list.Count == MaxOffers) break;
            }
            return list;
        }
    }
}
