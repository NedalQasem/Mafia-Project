using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The offers banner that opens over the home screen when the game starts (Docs/mockups/launch-banner.html): a page of the newspaper
    /// "جريدة الليل" with up to three offers that turn like the ads of the store, a brass close button, and "don't show again today".
    /// The pages are the store's <see cref="StoreAdCarousel"/> with a bigger page. Create it from the prefab, then <see cref="Show"/>.
    /// </summary>
    public sealed class KitLaunchBanner : MonoBehaviour
    {
        [SerializeField] private StoreAdCarousel carousel;
        [SerializeField] private RectTransform sheet, closeButtonRect, below;
        [SerializeField] private CanvasGroup scrim, cone, belowGroup;
        [SerializeField] private Button closeButton, scrimButton, skipButton, prevButton, nextButton;
        [SerializeField] private GameObject skipTick, dotsRoot, progressTrack;
        [SerializeField] private ArabicText countLabel;

        /// <summary>The player pressed "buy" on an offer: the ad's id and its item.</summary>
        public event Action<string, StoreItem> OnBuy;
        /// <summary>The player pressed "see it in the store".</summary>
        public event Action OnMore;
        /// <summary>The player blocked the offer on the page (its ad id); the app hides it and the banner goes away.</summary>
        public event Action<string> OnBlock;
        /// <summary>The banner is gone; the argument is true when "don't show it again today" was ticked.</summary>
        public event Action<bool> OnClosed;

        private readonly List<StoreAdCarousel.Ad> shown = new List<StoreAdCarousel.Ad>();
        private bool dontShowToday, closing;

        private void Awake()
        {
            KitFitGroup.Wrap((RectTransform)transform, sheet, closeButtonRect, below);       // keeps the page, its close button and the strip below together on short screens
            closeButton.onClick.AddListener(Close);
            scrimButton.onClick.AddListener(Close);
            skipButton.onClick.AddListener(() => { dontShowToday = !dontShowToday; skipTick.SetActive(dontShowToday); });
            prevButton.onClick.AddListener(() => carousel.Go(carousel.Current - 1));
            nextButton.onClick.AddListener(() => carousel.Go(carousel.Current + 1));
            carousel.OnBuy += item => { var ad = shown.Find(a => a.item != null && item != null && a.item.id == item.id); OnBuy?.Invoke(ad != null ? ad.id : null, item); };
            carousel.OnMore += () => OnMore?.Invoke();
            carousel.OnBlock += ad => OnBlock?.Invoke(ad.id);
        }

        public void Show(IList<StoreAdCarousel.Ad> ads)
        {
            shown.Clear(); shown.AddRange(ads);
            carousel.SetAds(ads);
            bool many = ads.Count > 1;
            prevButton.gameObject.SetActive(many); nextButton.gameObject.SetActive(many);
            dotsRoot.SetActive(many); progressTrack.SetActive(many);
            countLabel.Text = many ? CoinPill.Format(ads.Count) + " عروض" : "عرض واحد";
            skipTick.SetActive(false);
            StartCoroutine(Enter());
        }

        // HTML: scrim fades in 0.35 s, the light cone 0.8 s, the page rises 0.55 s from 40 px below (a little crooked), the rest follows
        private IEnumerator Enter()
        {
            sheet.gameObject.SetActive(true);
            scrim.alpha = 0f; cone.alpha = 0f; belowGroup.alpha = 0f;
            float restY = sheet.anchoredPosition.y;
            var closeCg = closeButtonRect.GetComponent<CanvasGroup>();
            float closeRest = closeButtonRect.anchoredPosition.y;
            sheet.gameObject.GetComponent<CanvasGroup>().alpha = 0f;
            closeCg.alpha = 0f;
            for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
            {
                scrim.alpha = Mathf.Clamp01(t / 0.35f);
                cone.alpha = Mathf.Clamp01((t - 0.1f) / 0.8f);
                float k = Ease(Mathf.Clamp01(t / 0.55f));
                sheet.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(t / 0.3f);
                sheet.anchoredPosition = new Vector2(sheet.anchoredPosition.x, restY - 110f * (1f - k));
                sheet.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, k);
                sheet.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-2f, 1f, k));
                float kc = Ease(Mathf.Clamp01((t - 0.15f) / 0.55f));
                closeCg.alpha = kc; closeButtonRect.anchoredPosition = new Vector2(closeButtonRect.anchoredPosition.x, closeRest - 110f * (1f - kc));
                belowGroup.alpha = Mathf.Clamp01((t - 0.3f) / 0.6f);
                yield return null;
            }
            scrim.alpha = 1f; cone.alpha = 1f; belowGroup.alpha = 1f; closeCg.alpha = 1f;
            sheet.GetComponent<CanvasGroup>().alpha = 1f;
            sheet.anchoredPosition = new Vector2(sheet.anchoredPosition.x, restY); sheet.localScale = Vector3.one; sheet.localRotation = Quaternion.Euler(0f, 0f, 1f);
            closeButtonRect.anchoredPosition = new Vector2(closeButtonRect.anchoredPosition.x, closeRest);
        }

        private static float Ease(float x) => 1f - Mathf.Pow(1f - x, 3f);

        /// <summary>Closes it (also what the brass button and the dark area around the page do).</summary>
        public void Close()
        {
            if (closing) return;
            closing = true;
            StartCoroutine(Leave());
        }

        private IEnumerator Leave()
        {
            var sheetCg = sheet.GetComponent<CanvasGroup>();
            float y = sheet.anchoredPosition.y;
            var closeCg = closeButtonRect.GetComponent<CanvasGroup>();
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.3f;
                scrim.alpha = 1f - k; cone.alpha = 1f - k; belowGroup.alpha = 1f - k; closeCg.alpha = 1f - k; sheetCg.alpha = 1f - k;
                sheet.anchoredPosition = new Vector2(sheet.anchoredPosition.x, y - 83f * k);
                sheet.localScale = Vector3.one * Mathf.Lerp(1f, 0.96f, k);
                yield return null;
            }
            OnClosed?.Invoke(dontShowToday);
            Destroy(gameObject);
        }
    }
}
