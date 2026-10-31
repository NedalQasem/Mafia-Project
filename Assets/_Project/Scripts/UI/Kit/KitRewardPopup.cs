using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    public enum RewardRarity { Common, Rare, Legend }

    /// <summary>One thing the player receives, as the popup shows it (the app maps its data onto this).</summary>
    public sealed class RewardItem
    {
        /// <summary>coins, gems, frame, bubble, or item (anything else from the store).</summary>
        public string kind;
        /// <summary>The store item id (to wear it); empty for coins and gems.</summary>
        public string id;
        public int amount;
        public string name, sub;
        public RewardRarity rarity;
        /// <summary>Optional picture of a store item; without it the built-in drawing of its kind is used.</summary>
        public Sprite art;
        /// <summary>A store item: the card draws it exactly as the store does (frame, room, gift, title...).</summary>
        public StoreItem store;
        public bool IsMoney => kind == "coins" || kind == "gems";
    }

    /// <summary>
    /// "شنطة الغنيمة" (Docs/mockups/reward.html): a leather briefcase drops in, the player taps it, the brass latches pop, the lid opens and the loot
    /// comes out as cards (common bronze, rare crimson, legendary gold with turning rays). "استلم" flies the coins and gems into the wallet pills and
    /// counts them up; a frame can be worn at once ("البسه هلأ"). All positions are mockup pixels of the 390x790 phone, times K, inside the stage.
    /// </summary>
    public class KitRewardPopup : MonoBehaviour
    {
        [SerializeField] private float k = 1080f / 390f;
        [SerializeField] private RectTransform stage;
        [SerializeField] private CanvasGroup dim, spot, src, tapHint, panel, wallet;
        [SerializeField] private RectTransform rays;
        [SerializeField] private Image raysImage;
        [SerializeField] private KitSparks sparks;
        [SerializeField] private ArabicText kicker, title, toast;
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private RectTransform caseRoot, shell, lid, latchA, latchB;
        [SerializeField] private CanvasGroup inside;
        [SerializeField] private Button caseButton, collectButton, wearButton;
        [SerializeField] private RectTransform cardsRoot, cardTemplate, chipsRoot, chipTemplate;
        [SerializeField] private RectTransform coinPill, gemPill;
        [SerializeField] private TextMeshProUGUI coinValue, gemValue;
        [SerializeField] private Sprite coinToken, gemToken;
        [SerializeField] private Sprite cardCommon, cardRare, cardLegend;
        [SerializeField] private Sprite artCoins, artGems, artFrame, artBubble;

        /// <summary>The player pressed "استلم" (false) or "البسه هلأ" (true); the popup then plays the collecting and closes itself.</summary>
        public event Action<bool> OnCollect;
        public event Action OnClosed;

        private static readonly Color Gold = new Color(1f, .84f, .51f), Pale = new Color(.96f, .89f, .66f), Red = new Color(.9f, .35f, .39f), White = new Color(1f, .96f, .85f);
        private readonly List<RectTransform> cards = new List<RectTransform>();
        private IList<RewardItem> items;
        private int coins, gems;
        private bool opened, busy, done;
        private Coroutine idle;
        private Vector2 latchAAt, latchBAt;     // where the builder put the latches (they pop and lift from there)

        private Vector2 P(float x, float y) => new Vector2((x - 195f) * k, (395f - y) * k);     // mockup pixel -> stage point

        private void Awake()
        {
            caseButton.onClick.AddListener(() => { if (!opened && !busy) StartCoroutine(Open()); });
            collectButton.onClick.AddListener(() => Collect(false));
            wearButton.onClick.AddListener(() => Collect(true));
            cardTemplate.gameObject.SetActive(false); chipTemplate.gameObject.SetActive(false);
            latchAAt = latchA.anchoredPosition; latchBAt = latchB.anchoredPosition;
        }

        /// <summary>Shows the briefcase. coinsNow / gemsNow: the wallet before the loot (the pills count up from them).</summary>
        public void Show(string kickerText, string titleText, IList<RewardItem> loot, int coinsNow, int gemsNow)
        {
            items = loot ?? new List<RewardItem>();
            coins = coinsNow; gems = gemsNow;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            KitText.Fit(kicker, kickerText, 34, 20f); KitText.Fit(title, titleText, 26, 34f);       // server text of any length stays on its line
            coinValue.text = CoinPill.Format(coins); gemValue.text = CoinPill.Format(gems);
            StopAllCoroutines();
            StartCoroutine(Enter());
        }

        private RewardRarity Top()
        {
            var r = RewardRarity.Common;
            foreach (var it in items) if (it.rarity > r) r = it.rarity;
            return r;
        }

        // ---------------------------------------------------------------- 1. the briefcase drops in
        private IEnumerator Enter()
        {
            opened = false; done = false; busy = true;
            foreach (var c in cards) if (c != null) Destroy(c.gameObject);
            cards.Clear();
            foreach (Transform c in chipsRoot) if (c != chipTemplate) Destroy(c.gameObject);
            dim.alpha = spot.alpha = src.alpha = tapHint.alpha = panel.alpha = toastGroup.alpha = 0f; wallet.alpha = 0f;
            panel.blocksRaycasts = false; inside.alpha = 0f; raysImage.color = new Color(1, 1, 1, 0);
            caseRoot.anchoredPosition = P(195, 430); caseRoot.localScale = Vector3.one; shell.anchoredPosition = Vector2.zero; shell.localRotation = Quaternion.identity;
            lid.gameObject.SetActive(true); lid.localScale = Vector3.one;
            latchA.localScale = latchB.localScale = Vector3.one;
            latchA.anchoredPosition = latchAAt; latchB.anchoredPosition = latchBAt;
            sparks.Clear(); sparks.Dust(40);

            StartCoroutine(Fade(dim, 1f, .4f)); StartCoroutine(Fade(spot, 1f, .8f, .2f)); StartCoroutine(Fade(src, 1f, .5f, .3f)); StartCoroutine(Fade(wallet, 1f, .4f, .3f));
            // drop: from 520 px above, tilted -8deg, overshoots to +2deg (cubic-bezier .3,1.5,.5,1)
            for (float t = 0; t < .8f; t += Time.unscaledDeltaTime)
            {
                float u = Mathf.Clamp01(t / .8f), e = BackOut(u, 1.9f);
                shell.anchoredPosition = new Vector2(0, (1f - e) * 520f * k);
                shell.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(8f, 0f, e));
                yield return null;
            }
            shell.anchoredPosition = Vector2.zero; shell.localRotation = Quaternion.identity;
            busy = false;
            StartCoroutine(Fade(tapHint, 1f, .4f));
            idle = StartCoroutine(Bob());
        }

        private IEnumerator Bob()
        {
            for (float t = 0; ; t += Time.unscaledDeltaTime)
            {
                float s = (1f - Mathf.Cos(t / 2.6f * Mathf.PI * 2f)) * .5f;
                shell.anchoredPosition = new Vector2(0, 6f * k * s); shell.localRotation = Quaternion.Euler(0, 0, 1f * s);
                yield return null;
            }
        }

        // ---------------------------------------------------------------- 2. tap: latches, lid, light, cards
        private IEnumerator Open()
        {
            opened = true; busy = true;
            if (idle != null) StopCoroutine(idle);
            shell.anchoredPosition = Vector2.zero;
            StartCoroutine(Fade(tapHint, 0f, .3f));
            StartCoroutine(Shake(.45f));
            yield return Wait(.16f); PopLatch(latchA); sparks.Burst(Local(latchA), 8, Pale);
            yield return Wait(.22f); PopLatch(latchB); sparks.Burst(Local(latchB), 8, Pale);
            yield return Wait(.26f);

            var top = Top();
            StartCoroutine(MoveCase());
            StartCoroutine(Scale(lid, new Vector3(1, 0, 1), .3f, () => lid.gameObject.SetActive(false)));
            StartCoroutine(Fade(inside, 1f, .3f));
            if (top != RewardRarity.Common) StartCoroutine(RaysIn(top));
            yield return Wait(.26f);
            Vector2 mouth = P(195, 545);
            sparks.Burst(mouth, top == RewardRarity.Legend ? 90 : 55, new Color(1f, .84f, .51f));
            if (top != RewardRarity.Common) sparks.Burst(mouth, 30, top == RewardRarity.Legend ? White : Red);
            yield return Deal();

            // the summary and the buttons
            foreach (var it in items)
            {
                var chip = Instantiate(chipTemplate, chipsRoot); chip.gameObject.SetActive(true);
                var label = chip.GetComponentInChildren<ArabicText>(true);
                var icon = chip.Find("Icon") as RectTransform;
                if (it.IsMoney) { KitText.Fit(label, "+" + CoinPill.Format(it.amount), 16, 26f); icon.GetComponent<Image>().sprite = it.kind == "coins" ? coinToken : gemToken; }
                else { KitText.Fit(label, it.name, 16, 20f); icon.gameObject.SetActive(false); }
            }
            bool frame = false; foreach (var it in items) if (it.kind == "frame") frame = true;
            wearButton.gameObject.SetActive(frame);
            panel.blocksRaycasts = true;
            StartCoroutine(Fade(panel, 1f, .4f));
            StartCoroutine(Slide(panel.transform as RectTransform, 30f * k, .4f));
            done = true; busy = false;
        }

        private void PopLatch(RectTransform l)
        {
            // rotateX(-70deg) translateY(-4px): seen from the front it shrinks to about a third and lifts a little
            StartCoroutine(Scale(l, new Vector3(1, .34f, 1), .25f, null, 2.4f));
            StartCoroutine(MoveBy(l, new Vector2(0, 4f * k), .25f));
        }

        private IEnumerator MoveCase()
        {
            Vector2 a = caseRoot.anchoredPosition, b = P(195, 572);
            for (float t = 0; t < .7f; t += Time.unscaledDeltaTime)
            {
                float e = BackInOut(Mathf.Clamp01(t / .7f));
                caseRoot.anchoredPosition = Vector2.LerpUnclamped(a, b, e);
                caseRoot.localScale = Vector3.one * Mathf.LerpUnclamped(1f, .74f, e);
                yield return null;
            }
            caseRoot.anchoredPosition = b; caseRoot.localScale = Vector3.one * .74f;
        }

        private IEnumerator RaysIn(RewardRarity top)
        {
            // rare: fewer, redder rays; legendary: full gold rays (the image turns in Update)
            Color to = top == RewardRarity.Legend ? new Color(1f, 1f, 1f, .7f) : new Color(1f, .45f, .45f, .4f);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime) { raysImage.color = Color.Lerp(new Color(to.r, to.g, to.b, 0), to, t); yield return null; }
            raysImage.color = to;
        }

        private List<Vector2> Slots(int n, bool big)
        {
            var list = new List<Vector2>();
            if (big) { list.Add(P(195, 330)); return list; }
            int[] rows = n <= 3 ? new[] { n } : new[] { 2, n - 2 };
            for (int r = 0; r < rows.Length; r++)
                for (int i = 0; i < rows[r]; i++)
                    list.Add(P(195 - (i - (rows[r] - 1) / 2f) * 116f, rows.Length == 1 ? 330 : 258 + r * 156));
            return list;
        }

        private IEnumerator Deal()
        {
            bool big = items.Count == 1;
            var slots = Slots(items.Count, big);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var c = Instantiate(cardTemplate, cardsRoot); c.gameObject.SetActive(true);
                cards.Add(c);
                Fill(c, it);
                var to = slots[i];
                float scale = big ? 150f / 104f : 1f;
                StartCoroutine(Fly(c, P(195, 545), to, scale));
                int idx = i;
                var tap = c.GetComponent<Button>(); if (tap != null) tap.onClick.AddListener(() => StartCoroutine(Spin(cards[idx], to, it)));
                if (it.IsMoney) StartCoroutine(CountUp(c.Find("Amount").GetComponent<TextMeshProUGUI>(), 0, it.amount, .65f, .38f, "+"));
                StartCoroutine(Later(.52f, () => sparks.Burst(to, it.rarity == RewardRarity.Legend ? 26 : 12, it.rarity == RewardRarity.Rare ? Red : Pale)));
                yield return Wait(big ? .3f : .23f);
            }
            yield return Wait(.7f);
        }

        private void Fill(RectTransform c, RewardItem it)
        {
            c.Find("Face").GetComponent<Image>().sprite = it.rarity == RewardRarity.Legend ? cardLegend : it.rarity == RewardRarity.Rare ? cardRare : cardCommon;
            var art = c.Find("Art").GetComponent<Image>();
            art.sprite = it.art != null ? it.art : it.kind == "coins" ? artCoins : it.kind == "gems" ? artGems : it.kind == "frame" ? artFrame : artBubble;
            // a store item is drawn by the store's own code in a 300 x 300 box, shrunk into the 70 px picture of the card
            bool drawn = it.store != null && it.art == null && KitTheme.Current != null;
            art.enabled = !drawn;
            if (drawn)
            {
                var box = new GameObject("StoreArt", typeof(RectTransform)).GetComponent<RectTransform>();
                box.SetParent(c, false); box.SetSiblingIndex(art.transform.GetSiblingIndex() + 1);
                box.anchoredPosition = ((RectTransform)art.transform).anchoredPosition; box.sizeDelta = new Vector2(300, 300);
                box.localScale = Vector3.one * (70f * k / 300f);
                StoreArt.Create(it.store, box, KitTheme.Current);
            }
            var amount = c.Find("Amount").gameObject; amount.SetActive(it.IsMoney);
            amount.GetComponent<TextMeshProUGUI>().text = "+" + CoinPill.Format(0);
            var nm = c.Find("Name"); nm.gameObject.SetActive(!it.IsMoney);
            KitText.Fit(nm.GetComponent<ArabicText>(), it.name, 18, 22f);
            KitText.Fit(nm.Find("Sub").GetComponent<ArabicText>(), it.sub, 22, 16f);
            c.Find("Ribbon").GetComponent<ArabicText>().Text = it.rarity == RewardRarity.Legend ? "أسطوري" : it.rarity == RewardRarity.Rare ? "نادر" : "عادي";
            c.Find("Sheen").gameObject.SetActive(it.rarity == RewardRarity.Legend);
        }

        private IEnumerator Fly(RectTransform c, Vector2 from, Vector2 to, float scale)
        {
            var g = c.GetComponent<CanvasGroup>();
            for (float t = 0; t < .65f; t += Time.unscaledDeltaTime)
            {
                float u = Mathf.Clamp01(t / .65f), e = BackOut(u, 1.4f);
                c.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
                c.localScale = Vector3.one * Mathf.LerpUnclamped(.2f, scale, e);
                c.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(14f, 0f, e));
                g.alpha = Mathf.Clamp01(u / .45f);
                yield return null;
            }
            c.anchoredPosition = to; c.localScale = Vector3.one * scale; c.localRotation = Quaternion.identity; g.alpha = 1f;
        }

        private IEnumerator Spin(RectTransform c, Vector2 at, RewardItem it)
        {
            if (c == null) yield break;
            sparks.Burst(at, 14, Pale);
            float s = c.localScale.y;
            for (float t = 0; t < .7f; t += Time.unscaledDeltaTime)
            {
                float e = BackOut(Mathf.Clamp01(t / .7f), 1.3f);
                if (c == null) yield break;
                c.localScale = new Vector3(s * Mathf.Cos(e * Mathf.PI * 2f), s, 1f);
                yield return null;
            }
            if (c != null) c.localScale = Vector3.one * s;
        }

        // ---------------------------------------------------------------- 3. collect: tokens fly into the pills, the cards go
        private void Collect(bool wear)
        {
            if (!done || busy) return;
            busy = true; done = false;
            panel.blocksRaycasts = false;
            StartCoroutine(Fade(panel, 0f, .25f));
            OnCollect?.Invoke(wear);
            StartCoroutine(CollectRoutine(wear));
        }

        private IEnumerator CollectRoutine(bool wear)
        {
            float longest = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i]; var c = cards[i];
                Vector2 from = c.anchoredPosition;
                float delay = i * .16f;
                if (it.IsMoney)
                {
                    int n = Mathf.Min(12, 4 + Mathf.RoundToInt(Mathf.Log10(Mathf.Max(1, it.amount)) * 2f));
                    var pill = it.kind == "coins" ? coinPill : gemPill;
                    Vector2 to = (Vector2)stage.InverseTransformPoint(pill.TransformPoint(pill.rect.center));
                    for (int j = 0; j < n; j++) StartCoroutine(Token(it.kind == "coins" ? coinToken : gemToken, it.kind == "coins" ? 20f : 16f, from, to, delay + j * .055f));
                    var item = it;
                    StartCoroutine(Later(delay + .72f, () => Arrive(item)));
                    longest = Mathf.Max(longest, delay + n * .055f + .72f);
                }
                else
                {
                    var item = it; var art = c.Find("Art").GetComponent<Image>().sprite;
                    StartCoroutine(Token(art, 40f, from, P(195, 110), delay));
                    StartCoroutine(Later(delay + .72f, () => Toast(item.kind == "frame" && wear ? "انلبس " + item.name : item.name + " بحقيبتك")));
                    longest = Mathf.Max(longest, delay + .72f);
                }
                StartCoroutine(Later(delay + .06f, () => StartCoroutine(Gone(c))));
            }
            yield return Wait(longest + .6f);
            StartCoroutine(Fade(src, 0f, .3f)); StartCoroutine(Fade(spot, 0f, .3f)); StartCoroutine(Fade(wallet, 0f, .3f));
            raysImage.color = new Color(1, 1, 1, 0);
            var caseGroup = caseRoot.GetComponent<CanvasGroup>();
            if (caseGroup != null) StartCoroutine(Fade(caseGroup, 0f, .3f));
            yield return Fade(dim, 0f, .35f);
            sparks.Clear();
            if (caseGroup != null) caseGroup.alpha = 1f;
            busy = false;
            gameObject.SetActive(false);
            OnClosed?.Invoke();
        }

        private void Arrive(RewardItem it)
        {
            bool c = it.kind == "coins";
            int from = c ? coins : gems;
            if (c) coins += it.amount; else gems += it.amount;
            StartCoroutine(CountUp(c ? coinValue : gemValue, from, from + it.amount, .5f, 0f, ""));
            StartCoroutine(Bump(c ? coinPill : gemPill));
        }

        private IEnumerator Token(Sprite s, float size, Vector2 from, Vector2 to, float delay)
        {
            yield return Wait(delay);
            var g = new GameObject("token", typeof(RectTransform)); g.transform.SetParent(stage, false);
            var img = g.AddComponent<Image>(); img.sprite = s; img.raycastTarget = false; img.preserveAspect = true;
            var r = (RectTransform)g.transform; r.sizeDelta = new Vector2(size, size) * k;
            Vector2 a = from + new Vector2(UnityEngine.Random.Range(-20f, 20f), UnityEngine.Random.Range(-15f, 15f)) * k;
            for (float t = 0; t < .7f; t += Time.unscaledDeltaTime)
            {
                float u = Mathf.Clamp01(t / .7f);
                // x eases in; y first rises a little, then dives to the pill (the two cubic-beziers of the mockup)
                float ex = u * u * (1.6f - .6f * u), ey = u < .3f ? -.25f * Mathf.Sin(u / .3f * Mathf.PI * .5f) : Mathf.Lerp(-.25f, 1f, (u - .3f) / .7f);
                r.anchoredPosition = new Vector2(Mathf.Lerp(a.x, to.x, ex), Mathf.LerpUnclamped(a.y, to.y, ey));
                r.localScale = Vector3.one * Mathf.Lerp(1f, .7f, u);
                yield return null;
            }
            Destroy(g);
        }

        private IEnumerator Gone(RectTransform c)
        {
            var g = c.GetComponent<CanvasGroup>(); Vector3 s = c.localScale; Vector2 p = c.anchoredPosition;
            for (float t = 0; t < .45f; t += Time.unscaledDeltaTime)
            {
                float u = Mathf.Clamp01(t / .45f), e = u * u;
                c.localScale = s * Mathf.Lerp(1f, .4f, e); c.anchoredPosition = p + new Vector2(0, 40f * k * e); g.alpha = 1f - e;
                yield return null;
            }
            c.gameObject.SetActive(false);
        }

        private void Toast(string text)
        {
            toast.Text = text;
            StopCoroutine(nameof(ToastRoutine)); StartCoroutine(nameof(ToastRoutine));
        }

        private IEnumerator ToastRoutine()
        {
            yield return Fade(toastGroup, 1f, .3f);
            yield return Wait(1.5f);
            yield return Fade(toastGroup, 0f, .3f);
        }

        // ---------------------------------------------------------------- small tweens (unscaled time, the game may be paused)
        private void Update()
        {
            if (rays != null && raysImage.color.a > 0f) rays.localRotation = Quaternion.Euler(0, 0, -Time.unscaledTime * 9f);     // a turn every 40 s
            foreach (var c in cards)
            {
                if (c == null || !c.gameObject.activeSelf) continue;
                var sheen = c.Find("Sheen/Bar") as RectTransform;
                if (sheen == null || !sheen.gameObject.activeInHierarchy) continue;
                float u = (Time.unscaledTime % 2.6f) / 2.6f, e = Mathf.Clamp01(u / .45f);
                sheen.anchoredPosition = new Vector2(Mathf.Lerp(-110f, 110f, e) * k, 0);
            }
        }

        private static WaitForSecondsRealtime Wait(float s) => new WaitForSecondsRealtime(s);
        private static float BackOut(float u, float s) { u -= 1f; return u * u * ((s + 1f) * u + s) + 1f; }
        private static float BackInOut(float u) { const float s = 1.7f * 1.525f; u *= 2f; if (u < 1f) return .5f * (u * u * ((s + 1f) * u - s)); u -= 2f; return .5f * (u * u * ((s + 1f) * u + s) + 2f); }

        private Vector2 Local(RectTransform r) => stage.InverseTransformPoint(r.TransformPoint(r.rect.center));

        private IEnumerator Later(float s, Action a) { yield return Wait(s); a(); }

        private static IEnumerator Fade(CanvasGroup g, float to, float time, float delay = 0f)
        {
            if (delay > 0f) yield return Wait(delay);
            float from = g.alpha;
            for (float t = 0; t < time; t += Time.unscaledDeltaTime) { g.alpha = Mathf.Lerp(from, to, t / time); yield return null; }
            g.alpha = to;
        }

        private IEnumerator Slide(RectTransform r, float fromBelow, float time)
        {
            Vector2 end = r.anchoredPosition, start = end - new Vector2(0, fromBelow);
            for (float t = 0; t < time; t += Time.unscaledDeltaTime) { r.anchoredPosition = Vector2.Lerp(start, end, 1f - Mathf.Pow(1f - t / time, 3f)); yield return null; }
            r.anchoredPosition = end;
        }

        private static IEnumerator Scale(RectTransform r, Vector3 to, float time, Action after = null, float overshoot = 0f)
        {
            Vector3 from = r.localScale;
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                float u = Mathf.Clamp01(t / time), e = overshoot > 0f ? BackOut(u, overshoot) : 1f - Mathf.Pow(1f - u, 3f);
                r.localScale = Vector3.LerpUnclamped(from, to, e); yield return null;
            }
            r.localScale = to; after?.Invoke();
        }

        private static IEnumerator MoveBy(RectTransform r, Vector2 by, float time)
        {
            Vector2 a = r.anchoredPosition;
            for (float t = 0; t < time; t += Time.unscaledDeltaTime) { r.anchoredPosition = a + by * (1f - Mathf.Pow(1f - t / time, 3f)); yield return null; }
            r.anchoredPosition = a + by;
        }

        private IEnumerator Shake(float time)
        {
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                float u = t / time, a = u < .2f ? -4f * u / .2f : u < .4f ? Mathf.Lerp(-4f, 4f, (u - .2f) / .2f) : u < .6f ? Mathf.Lerp(4f, -3f, (u - .4f) / .2f) : u < .8f ? Mathf.Lerp(-3f, 2f, (u - .6f) / .2f) : Mathf.Lerp(2f, 0f, (u - .8f) / .2f);
                shell.localRotation = Quaternion.Euler(0, 0, -a); yield return null;
            }
            shell.localRotation = Quaternion.identity;
        }

        private IEnumerator Bump(RectTransform r)
        {
            for (float t = 0; t < .35f; t += Time.unscaledDeltaTime) { float u = t / .35f; r.localScale = Vector3.one * (1f + .18f * Mathf.Sin(Mathf.Min(1f, u / .8f) * Mathf.PI)); yield return null; }
            r.localScale = Vector3.one;
        }

        private static IEnumerator CountUp(TextMeshProUGUI label, int from, int to, float time, float delay, string prefix)
        {
            if (delay > 0f) yield return Wait(delay);
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                float e = 1f - Mathf.Pow(1f - t / time, 3f);
                label.text = prefix + CoinPill.Format(Mathf.RoundToInt(Mathf.Lerp(from, to, e)));
                yield return null;
            }
            label.text = prefix + CoinPill.Format(to);
        }
    }
}
