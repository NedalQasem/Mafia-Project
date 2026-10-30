using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>Rotating newspaper offers at the top of the store (auto turns every few seconds, swipe to change).</summary>
    public class StoreAdCarousel : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
    {
        [SerializeField] private RectTransform pagesRoot;
        [SerializeField] private RectTransform dotsRoot;
        [SerializeField] private GameObject pagePrefab;
        [SerializeField] private GameObject dotPrefab;
        [SerializeField] private float period = 5.5f;
        [SerializeField] private float pageTilt = -1f;      // degrees each page lies crooked (the store tilts its ads; the launch banner tilts its whole sheet instead)
        [SerializeField] private RectTransform progress;   // thin red bar above the ad that runs out before the next page

        public event Action<StoreItem> OnBuy;
        /// <summary>The page's "see it in the store" link (only pages that have one, like the launch banner's).</summary>
        public event Action OnMore;

        [Serializable]
        public class Ad { public string id, mast, headline, body, stamp, price, was; public StoreItem item; public bool countdown; public int countdownSeconds; }

        public int Current => current;
        public int Count => pages.Count;

        private readonly List<CanvasGroup> pages = new List<CanvasGroup>();
        private readonly List<Image> shades = new List<Image>();
        private readonly List<Image> dots = new List<Image>();
        private readonly List<ArabicText> countdowns = new List<ArabicText>();
        private int current;
        private float timer, dragStart;
        private bool dragging;
        private float secondsLeft = 2 * 3600 + 14 * 60 + 33;
        private Coroutine turn;

        public void SetAds(IList<Ad> ads)
        {
            var th = KitTheme.Current;
            for (int i = pagesRoot.childCount - 1; i >= 0; i--) Destroy(pagesRoot.GetChild(i).gameObject);
            for (int i = dotsRoot.childCount - 1; i >= 0; i--) Destroy(dotsRoot.GetChild(i).gameObject);
            pages.Clear(); shades.Clear(); dots.Clear(); countdowns.Clear();
            for (int i = 0; i < ads.Count; i++)
            {
                var ad = ads[i];
                var go = Instantiate(pagePrefab, pagesRoot);
                // pages turn around their right edge, like a newspaper (HTML: transform-origin 100% 50%, rotate -1deg)
                var prt = (RectTransform)go.transform; prt.pivot = new Vector2(1f, 0.5f); prt.anchoredPosition = prt.anchorMin.x == prt.anchorMax.x ? new Vector2(prt.rect.width * 0.5f, 0f) : Vector2.zero;   // stretched pages already fit their parent
                prt.localRotation = Quaternion.Euler(0f, 0f, pageTilt);
                var cg = go.GetComponent<CanvasGroup>(); cg.alpha = i == 0 ? 1f : 0f; cg.blocksRaycasts = i == 0;
                shades.Add(go.transform.Find("Shade").GetComponent<Image>());
                go.transform.Find("Mast/Right").GetComponent<ArabicText>().Text = ad.mast;
                go.transform.Find("Headline").GetComponent<ArabicText>().Text = ad.headline;
                go.transform.Find("Body").GetComponent<ArabicText>().Text = ad.body;
                var stamp = go.transform.Find("Stamp");
                stamp.gameObject.SetActive(!string.IsNullOrEmpty(ad.stamp));
                if (stamp.gameObject.activeSelf) stamp.GetComponentInChildren<ArabicText>().Text = ad.stamp;
                var art = (RectTransform)go.transform.Find("Art");
                StoreArt.Create(ad.item, art, th);
                var cd = go.transform.Find("Countdown");
                cd.gameObject.SetActive(ad.countdown);
                if (ad.countdown) { countdowns.Add(cd.Find("Value").GetComponent<ArabicText>()); secondsLeft = ad.countdownSeconds; }
                // optional parts of the bigger launch-banner page: the normal price crossed out, and a link to the store
                var was = go.transform.Find("Was");
                if (was != null)
                {
                    was.gameObject.SetActive(!string.IsNullOrEmpty(ad.was));
                    if (!string.IsNullOrEmpty(ad.was))
                    {
                        var wv = was.Find("Value").GetComponent<ArabicText>(); wv.Text = ad.was;
                        var wt = wv.GetComponent<TMPro.TMP_Text>(); wt.ForceMeshUpdate();
                        var strike = wv.transform.Find("Strike") as RectTransform;
                        if (strike != null) strike.sizeDelta = new Vector2(wt.preferredWidth + 8f, strike.sizeDelta.y);
                    }
                }
                var more = go.transform.Find("More");
                if (more != null) more.GetComponent<Button>().onClick.AddListener(() => OnMore?.Invoke());
                var buy = go.transform.Find("Buy");
                buy.GetComponentInChildren<ArabicText>().Text = ad.price;
                var item = ad.item;
                buy.GetComponent<Button>().onClick.AddListener(() => OnBuy?.Invoke(item));
                pages.Add(cg);
                var d = Instantiate(dotPrefab, dotsRoot).GetComponent<Image>();
                dots.Add(d);
            }
            current = 0; timer = 0; Paint();
        }

        private void Paint() { for (int i = 0; i < dots.Count; i++) { dots[i].color = i == current ? new Color(0.88f, 0.32f, 0.35f) : new Color(0.3f, 0.26f, 0.2f); dots[i].transform.localScale = Vector3.one * (i == current ? 1.3f : 1f); } }

        public void Go(int index)
        {
            if (pages.Count < 2 || turn != null) return;
            int from = current;
            bool wrapForward = from == pages.Count - 1 && ((index % pages.Count) + pages.Count) % pages.Count == 0;
            index = (index + pages.Count) % pages.Count;
            if (index == current) return;
            int dir = (index > from || wrapForward) ? 1 : -1;
            if (index == pages.Count - 1 && from == 0) dir = -1;   // stepping back from the first page
            turn = StartCoroutine(Turn(from, index, dir));
        }

        // CSS cubic-bezier(.5, 0, .3, 1)
        private static float Bezier(float x)
        {
            const float x1 = .5f, y1 = 0f, x2 = .3f, y2 = 1f;
            float t = x;
            for (int i = 0; i < 6; i++)
            {
                float cx = 3 * x1 * t * (1 - t) * (1 - t) + 3 * x2 * t * t * (1 - t) + t * t * t - x;
                float dx = 3 * x1 * (1 - t) * (1 - t) + 6 * (x2 - x1) * t * (1 - t) + 3 * (1 - x2) * t * t;
                if (Mathf.Abs(dx) < 1e-5f) break;
                t = Mathf.Clamp01(t - cx / dx);
            }
            return 3 * y1 * t * (1 - t) * (1 - t) + 3 * y2 * t * t * (1 - t) + t * t * t;
        }

        // HTML pageturn: the old page swings away around its right edge (-118 / +118 deg), darkening and fading,
        // while the next page already lies underneath. 0.9 s, ease (.5, 0, .3, 1).
        private IEnumerator Turn(int from, int to, int dir)
        {
            const float dur = 0.9f;
            var a = pages[from]; var b = pages[to];
            b.transform.SetSiblingIndex(0); a.transform.SetAsLastSibling();
            b.alpha = 1f; b.blocksRaycasts = true; a.blocksRaycasts = false;
            b.transform.localRotation = Quaternion.Euler(0f, 0f, pageTilt); shades[to].color = new Color(0, 0, 0, 0);
            current = to; Paint();                                    // dots follow at once, as in the mockup
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Bezier(t / dur);
                float angle = -118f * dir * k;
                a.transform.localRotation = Quaternion.Euler(0f, angle, pageTilt);
                float bright = k < 0.6f ? Mathf.Lerp(1f, 0.7f, k / 0.6f) : Mathf.Lerp(0.7f, 0.4f, (k - 0.6f) / 0.4f);
                shades[from].color = new Color(0, 0, 0, 1f - bright);
                a.alpha = Mathf.Abs(angle) >= 90f ? 0f : 1f - k;   // backface hidden once it is edge-on
                yield return null;
            }
            a.alpha = 0f; shades[from].color = new Color(0, 0, 0, 0);
            a.transform.localRotation = Quaternion.Euler(0f, 0f, pageTilt);
            timer = 0f; turn = null;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            secondsLeft -= Time.unscaledDeltaTime;
            int s = Mathf.Max(0, (int)secondsLeft);
            string txt = CoinPill.Format(s / 3600).PadLeft(2, '٠') + ":" + CoinPill.Format(s % 3600 / 60).PadLeft(2, '٠') + ":" + CoinPill.Format(s % 60).PadLeft(2, '٠');
            foreach (var c in countdowns) c.Text = txt;
            if (progress != null) { progress.anchorMin = new Vector2(Mathf.Clamp01(timer / period), 0f); progress.anchorMax = Vector2.one; }
            if (dragging || pages.Count < 2) return;
            timer += Time.unscaledDeltaTime;
            if (timer > period) Go(current + 1);
        }

        public void OnBeginDrag(PointerEventData e) { dragging = true; dragStart = e.position.x; }
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e)
        {
            dragging = false;
            float dx = e.position.x - dragStart;
            if (Mathf.Abs(dx) > 60f) Go(current + (dx < 0 ? 1 : -1));
        }
    }
}
