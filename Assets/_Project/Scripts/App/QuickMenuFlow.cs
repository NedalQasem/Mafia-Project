using System.Collections;
using System.Collections.Generic;
using MafiaNight.Core;
using MafiaNight.Services.Rooms;
using MafiaNight.Services.Scenes;
using MafiaNight.UI.Kit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MafiaNight.App
{
    /// <summary>
    /// Puts the quick menu (<see cref="KitQuickMenu"/>) on the home screen and keeps it fed: the offers (the same ones the launch banner shows,
    /// without the ones the player blocked) and, later, the events. The offers icon reopens them after the banner was closed; blocking
    /// hides an offer until it ends, with an undo. The menu hides itself while the player is in a room.
    /// </summary>
    public sealed class QuickMenuFlow : MonoBehaviour
    {
        private const float Settle = 1.0f, RefetchEvery = 300f;

        private IRoomService rooms;
        private ISceneLoader scenes;
        private KitQuickMenu menu;
        private List<StoreAdCarousel.Ad> all = new List<StoreAdCarousel.Ad>();
        private float refetchAt;
        private bool loading;

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
            BlockedOffers.Changed += OnBlockedChanged;
            return true;
        }

        private void Open(string scene)
        {
            if (scenes != null) scenes.LoadScene(scene); else SceneManager.LoadScene(scene);
        }

        private void Feed() => menu.SetOffers(BlockedOffers.Filter(all));

        private void OnBlockedChanged(string adId, bool blocked)
        {
            if (menu == null) return;
            Feed();
            if (blocked) menu.ShowUndo(adId, "ما رح يظهر لك هالعرض بعد هلأ");
        }
    }
}
