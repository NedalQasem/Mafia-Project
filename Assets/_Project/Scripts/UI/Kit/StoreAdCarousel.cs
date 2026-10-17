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

        public event Action<StoreItem> OnBuy;

        [Serializable]
        public class Ad { public string mast, headline, body, stamp, price; public StoreItem item; public bool countdown; }

        private readonly List<CanvasGroup> pages = new List<CanvasGroup>();
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
            pages.Clear(); dots.Clear(); countdowns.Clear();
            for (int i = 0; i < ads.Count; i++)
            {
                var ad = ads[i];
                var go = Instantiate(pagePrefab, pagesRoot);
                var cg = go.GetComponent<CanvasGroup>(); cg.alpha = i == 0 ? 1f : 0f; cg.blocksRaycasts = i == 0;
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
                if (ad.countdown) countdowns.Add(cd.Find("Value").GetComponent<ArabicText>());
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

        private void Paint() { for (int i = 0; i < dots.Count; i++) dots[i].color = i == current ? new Color(0.88f, 0.32f, 0.35f) : new Color(0.3f, 0.26f, 0.2f); }

        public void Go(int index)
        {
            if (pages.Count < 2 || turn != null) return;
            index = (index + pages.Count) % pages.Count;
            if (index == current) return;
            turn = StartCoroutine(Turn(current, index));
        }

        private IEnumerator Turn(int from, int to)
        {
            const float dur = 0.8f;
            var a = pages[from]; var b = pages[to];
            b.transform.SetAsLastSibling(); b.alpha = 0f; b.blocksRaycasts = true; a.blocksRaycasts = false;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur; float e = k * k * (3f - 2f * k);
                a.alpha = 1f - e; b.alpha = e;
                a.transform.localRotation = Quaternion.Euler(0, 0, -1.5f * e);
                a.transform.localScale = Vector3.one * (1f - 0.05f * e);
                b.transform.localRotation = Quaternion.Euler(0, 0, 1.2f * (1f - e));
                b.transform.localScale = Vector3.one * (0.95f + 0.05f * e);
                yield return null;
            }
            a.alpha = 0; b.alpha = 1;
            a.transform.localRotation = Quaternion.identity; a.transform.localScale = Vector3.one;
            b.transform.localRotation = Quaternion.identity; b.transform.localScale = Vector3.one;
            current = to; Paint(); timer = 0; turn = null;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            secondsLeft -= Time.unscaledDeltaTime;
            int s = Mathf.Max(0, (int)secondsLeft);
            string txt = CoinPill.Format(s / 3600).PadLeft(2, '٠') + ":" + CoinPill.Format(s % 3600 / 60).PadLeft(2, '٠') + ":" + CoinPill.Format(s % 60).PadLeft(2, '٠');
            foreach (var c in countdowns) c.Text = txt;
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
