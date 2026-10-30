using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The quick menu of the home screen (Docs/mockups/quick-rail.html): a small brass tab stuck to a screen edge; pressed, the icons drop down
    /// under it (offers, events), and each opens its own small sheet. The player can pick the tab up and park it on either edge at any height;
    /// the place is remembered on the device. Create it from the prefab, then feed it with <see cref="SetOffers"/> and <see cref="SetEvents"/>.
    /// It hides itself while there is nothing to show.
    /// </summary>
    public sealed class KitQuickMenu : MonoBehaviour
    {
        // sizes are the HTML ones times 2.769 (390 px phone -> 1080 canvas)
        private const float TabW = 83f, TabH = 138f, Overlap = 17f, DropPad = 33f, BtnSize = 76f, Gap = 33f, TimeRoom = 17f, SnapTime = 0.28f;
        private const float DefaultTop = 653f;                    // 236 px of the HTML
        private const string SidePref = "mafia.quick.side", YPref = "mafia.quick.y", HintPref = "mafia.quick.hint";

        [SerializeField] private RectTransform menu, tabRt, dropBg, dropItems, chevron, sumDot, offersRt, eventsRt, mailRt, offDot, evDot, mailDot, offTime, offLabel, evLabel, mailLabel, bubble, panel, rowsRoot, toast;
        [SerializeField] private Image tabImage, ringImage, edgeL, edgeR;
        [SerializeField] private Sprite tabSprite, pillSprite;
        [SerializeField] private ArabicText sumText, offDotText, evDotText, mailDotText, offTimeText, toastText;
        [SerializeField] private Button tabButton, offersButton, eventsButton, mailButton, scrimButton, openPaperButton, undoButton, catcher;
        [SerializeField] private CanvasGroup menuGroup, dropItemsGroup, edgesGroup, hintGroup, offLabelGroup, evLabelGroup, mailLabelGroup, bubbleGroup, panelGroup, scrimGroup, toastGroup;
        [SerializeField] private GameObject rowPrefab, emptyState, openPaperGo;

        /// <summary>An offer's block button was pressed (the app decides: it hides the offer and calls <see cref="SetOffers"/> again).</summary>
        public event Action<string> OnBlock;
        /// <summary>The player tapped an offer in the small sheet (wants to buy it).</summary>
        public event Action<string> OnBuyOffer;
        /// <summary>"Undo" after a block.</summary>
        public event Action<string> OnUndo;
        /// <summary>"Open the whole newspaper".</summary>
        public event Action OnOpenPaper;
        /// <summary>The events icon was pressed (the events sheet is the app's to show).</summary>
        public event Action OnOpenEvents;
        /// <summary>The mail icon was pressed (the mail board is the app's to show).</summary>
        public event Action OnOpenMail;

        private sealed class Row { public StoreAdCarousel.Ad ad; public ArabicText sub; public string subPrefix; }

        private readonly List<StoreAdCarousel.Ad> ads = new List<StoreAdCarousel.Ad>();
        private readonly List<Row> rows = new List<Row>();
        private float adsAt;
        private int events, mail;
        private bool right = true, open, panelOpen, dragging, suppressed, placed, nearLeft;
        private float topPx = -1f, topFrac = -1f, openT, hintLeft, snapX = -1f, lastW, lastH, tick, toastLeft;
        private Vector2 grab;
        private string undoId;
        private Coroutine snap;
        private RectTransform Root => (RectTransform)transform;

        private void Awake()
        {
            right = PlayerPrefs.GetInt(SidePref, 1) != 0;
            topFrac = PlayerPrefs.GetFloat(YPref, -1f);
            tabButton.onClick.AddListener(() => SetOpen(!open));
            offersButton.onClick.AddListener(OpenPanel);
            eventsButton.onClick.AddListener(() => { SetOpen(false); OnOpenEvents?.Invoke(); });
            mailButton.onClick.AddListener(() => { SetOpen(false); OnOpenMail?.Invoke(); });
            scrimButton.onClick.AddListener(ClosePanel);
            catcher.onClick.AddListener(() => SetOpen(false));
            openPaperButton.onClick.AddListener(() => { ClosePanel(); OnOpenPaper?.Invoke(); });
            undoButton.onClick.AddListener(() => { var id = undoId; undoId = null; toastLeft = 0f; if (id != null) OnUndo?.Invoke(id); });
            var relay = tabRt.gameObject.GetComponent<KitQuickTab>(); relay.owner = this;
            panelGroup.alpha = 0f; panelGroup.blocksRaycasts = false; scrimGroup.alpha = 0f; scrimGroup.blocksRaycasts = false;
            toastGroup.alpha = 0f; toastGroup.blocksRaycasts = false; edgesGroup.alpha = 0f; hintGroup.alpha = 0f; bubbleGroup.alpha = 0f;
            offLabelGroup.alpha = 0f; evLabelGroup.alpha = 0f; mailLabelGroup.alpha = 0f; dropItemsGroup.alpha = 0f; dropItemsGroup.blocksRaycasts = false;
            catcher.gameObject.SetActive(false); dropBg.gameObject.SetActive(false);
            menuGroup.alpha = 0f; menuGroup.blocksRaycasts = false;
            ApplySide();
        }

        // ---------------------------------------------------------------- what it shows
        /// <summary>The offers of the small sheet (the same ones the newspaper shows), without the ones the player blocked.</summary>
        public void SetOffers(IList<StoreAdCarousel.Ad> list)
        {
            ads.Clear(); if (list != null) ads.AddRange(list);
            adsAt = Time.unscaledTime;
            BuildRows();
            Refresh();
        }

        /// <summary>How many events are on (0 hides the events icon).</summary>
        public void SetEvents(int count) { events = Mathf.Max(0, count); Refresh(); }

        /// <summary>How many telegrams wait in the mail (the red number on the mail icon).</summary>
        public void SetMail(int count) { mail = Mathf.Max(0, count); Refresh(); }

        /// <summary>Hides the whole menu (while the player is in a room or a match).</summary>
        public void SetSuppressed(bool on)
        {
            if (suppressed == on) return;
            suppressed = on;
            if (on) { SetOpen(false); ClosePanel(); }
            Refresh();
        }

        /// <summary>"The offer is hidden" with an undo, over the bottom of the screen for a few seconds.</summary>
        public void ShowUndo(string adId, string text)
        {
            undoId = adId; toastText.Text = text; toastLeft = 4.2f; toastGroup.blocksRaycasts = true;
        }

        private int Total => ads.Count + events + mail;
        private bool Visible => !suppressed;

        private void BuildRows()
        {
            for (int i = rowsRoot.childCount - 1; i >= 0; i--) Destroy(rowsRoot.GetChild(i).gameObject);
            rows.Clear();
            var th = KitTheme.Current;
            foreach (var ad in ads)
            {
                var go = Instantiate(rowPrefab, rowsRoot);
                go.transform.Find("Texts/Title").GetComponent<ArabicText>().Text = (ad.headline ?? "").Replace('\n', ' ');
                var sub = go.transform.Find("Texts/Sub").GetComponent<ArabicText>();
                var row = new Row { ad = ad, sub = sub, subPrefix = ad.countdown ? "ينتهي خلال " : null };
                if (!ad.countdown) sub.Text = !string.IsNullOrEmpty(ad.stamp) ? ad.stamp : (ad.mast ?? "");
                var pr = go.transform.Find("Texts/Price");
                pr.Find("Value").GetComponent<ArabicText>().Text = ad.price;
                bool money = ad.item != null && !string.IsNullOrEmpty(ad.item.priceText);
                pr.Find("Coin").gameObject.SetActive(!money);
                var was = pr.Find("Was");
                was.gameObject.SetActive(!string.IsNullOrEmpty(ad.was));
                if (!string.IsNullOrEmpty(ad.was))
                {
                    var wv = was.Find("Value").GetComponent<ArabicText>(); wv.Text = ad.was;
                    var wt = wv.GetComponent<TMPro.TMP_Text>(); wt.ForceMeshUpdate();
                    var strike = wv.transform.Find("Strike") as RectTransform;
                    if (strike != null) strike.sizeDelta = new Vector2(wt.preferredWidth + 6f, strike.sizeDelta.y);
                }
                var ring = go.transform.Find("Icon/Ring").GetComponent<Image>();
                ring.color = ad.countdown ? new Color32(0xC9, 0x32, 0x3A, 255) : new Color32(0x1D, 0x4A, 0x3C, 255);
                if (ad.item != null && th != null) StoreArt.Create(ad.item, (RectTransform)go.transform.Find("Icon/Art"), th);
                string id = ad.id;
                go.transform.Find("Block").GetComponent<Button>().onClick.AddListener(() => OnBlock?.Invoke(id));
                go.GetComponent<Button>().onClick.AddListener(() => { ClosePanel(); OnBuyOffer?.Invoke(id); });
                rows.Add(row);
            }
            UpdateTimes(true);
        }

        private static string Clock(int s) => CoinPill.Format(s / 3600).PadLeft(2, '٠') + ":" + CoinPill.Format(s % 3600 / 60).PadLeft(2, '٠') + ":" + CoinPill.Format(s % 60).PadLeft(2, '٠');
        private static string Short(int s) => s >= 3600 ? CoinPill.Format(s / 3600).PadLeft(2, '٠') + ":" + CoinPill.Format(s % 3600 / 60).PadLeft(2, '٠') : CoinPill.Format(s / 60).PadLeft(2, '٠') + ":" + CoinPill.Format(s % 60).PadLeft(2, '٠');

        private void UpdateTimes(bool force)
        {
            float gone = Time.unscaledTime - adsAt;
            int soonest = int.MaxValue;
            foreach (var r in rows)
            {
                if (!r.ad.countdown) continue;
                int left = Mathf.Max(0, Mathf.RoundToInt(r.ad.countdownSeconds - gone));
                soonest = Mathf.Min(soonest, left);
                if (force || panelOpen) r.sub.Text = r.subPrefix + Clock(left);
            }
            bool any = soonest != int.MaxValue;
            if (offTime.gameObject.activeSelf != any) { offTime.gameObject.SetActive(any); LayoutDrop(); }
            if (any) offTimeText.Text = Short(soonest);
        }

        /// <summary>Counts, which icons exist, where they stand; hides the whole menu when empty.</summary>
        private void Refresh()
        {
            bool vis = Visible;
            menuGroup.alpha = vis ? 1f : 0f; menuGroup.blocksRaycasts = vis;
            if (!vis) { SetOpen(false); }
            sumText.Text = CoinPill.Format(Total);
            sumDot.gameObject.SetActive(Total > 0);
            offDotText.Text = CoinPill.Format(ads.Count); offDot.gameObject.SetActive(ads.Count > 0);
            evDotText.Text = CoinPill.Format(events); evDot.gameObject.SetActive(events > 0);
            mailDotText.Text = CoinPill.Format(mail); mailDot.gameObject.SetActive(mail > 0);
            mailRt.gameObject.SetActive(true);
            bool offersIcon = ads.Count > 0 || panelOpen;
            offersRt.gameObject.SetActive(offersIcon); eventsRt.gameObject.SetActive(true);       // the events icon is always there, it says so when there are none
            var evText = evLabel.GetComponentInChildren<ArabicText>(true);
            if (evText != null) evText.Text = events > 0 ? "الأحداث" : "لا يوجد أحداث";
            emptyState.SetActive(ads.Count == 0); openPaperGo.SetActive(ads.Count > 0);
            LayoutDrop();
            if (vis && !placed) { /* placed in LateUpdate once the canvas has a size */ }
            if (vis && !PlayerPrefs.HasKey(HintPref)) hintLeft = Mathf.Max(hintLeft, 0f);
        }

        private float dropH;

        private void LayoutDrop()
        {
            float y = DropPad; int n = 0;
            void Put(RectTransform r, bool on, float extra)
            {
                if (!on) return;
                r.anchoredPosition = new Vector2(TabW * 0.5f, -(y + BtnSize * 0.5f));
                y += BtnSize + extra + Gap; n++;
            }
            Put(offersRt, offersRt.gameObject.activeSelf, offTime.gameObject.activeSelf ? TimeRoom : 0f);
            Put(eventsRt, eventsRt.gameObject.activeSelf, 0f);
            Put(mailRt, mailRt.gameObject.activeSelf, 0f);
            dropH = n == 0 ? 0f : y - Gap + DropPad;
            dropItems.sizeDelta = new Vector2(TabW, dropH);
        }

        // ---------------------------------------------------------------- open and close
        private void SetOpen(bool on)
        {
            if (on && (!Visible || dragging)) return;
            if (open == on) return;
            open = on;
            catcher.gameObject.SetActive(on);
            dropBg.gameObject.SetActive(true);
            dropItemsGroup.blocksRaycasts = on;
            hintLeft = on ? 2.6f : 0f;
            if (on) { bubbleGroup.alpha = 0f; PlayerPrefs.SetInt(HintPref, 1); }
        }

        private void OpenPanel()
        {
            SetOpen(false);
            panelOpen = true;
            UpdateTimes(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            float h = panel.rect.height;
            float t =Mathf.Clamp(topPx - 33f, 194f, Mathf.Max(194f, BottomEdge() - 22f - h));
            panel.anchoredPosition = new Vector2(right ? -155f : 155f, -t);
            scrimGroup.blocksRaycasts = true; panelGroup.blocksRaycasts = true;
        }

        private void ClosePanel()
        {
            if (!panelOpen) return;
            panelOpen = false;
            scrimGroup.blocksRaycasts = false; panelGroup.blocksRaycasts = false;
            Refresh();
        }

        // ---------------------------------------------------------------- where it stands
        private RectTransform topBarRt, navBarRt;

        private float TopEdge()          // distance from the top of the screen to the bottom of the top bar, plus air
        {
            if (topBarRt == null) { var b = FindAnyObjectByType<KitTopBar>(); if (b != null) topBarRt = (RectTransform)b.transform; }
            return topBarRt != null ? DistFromTop(topBarRt, false) + 22f : 255f;
        }

        private float BottomEdge()       // distance from the top of the screen to the top of the bottom bar
        {
            if (navBarRt == null) { var b = FindAnyObjectByType<KitNavBar>(); if (b != null) navBarRt = (RectTransform)b.transform; }
            return navBarRt != null ? DistFromTop(navBarRt, true) : Root.rect.height - 216f;
        }

        private float DistFromTop(RectTransform r, bool topEdge)
        {
            var c = new Vector3[4]; r.GetWorldCorners(c);
            var rootRect = Root.rect;
            float y = Root.InverseTransformPoint(topEdge ? c[1] : c[0]).y;       // c[0] bottom-left, c[1] top-left
            return rootRect.yMax - y;
        }

        private void Limits(out float min, out float max)
        {
            min = TopEdge();
            max = Mathf.Max(min, BottomEdge() - 14f - (TabH - Overlap + Mathf.Max(dropH, 2 * BtnSize + 3 * Gap)));
        }

        private void Place()
        {
            var r = Root.rect;
            if (r.width < 10f) return;
            lastW = r.width; lastH = r.height;
            Limits(out float min, out float max);
            topPx = Mathf.Clamp(topFrac >= 0f ? topFrac * r.height : DefaultTop, min, max);
            menu.anchoredPosition = new Vector2(right ? r.width - TabW : 0f, -topPx);
            placed = true;
        }

        private void ApplySide()
        {
            float s = right ? 1f : -1f;
            tabImage.rectTransform.localScale = new Vector3(s, 1f, 1f);
            ringImage.rectTransform.localScale = new Vector3(s, 1f, 1f);
            dropBg.localScale = new Vector3(s, 1f, 1f);
            // the red counts sit on the side away from the edge
            void Corner(RectTransform r, float inX, float yy, float parentW) { r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.anchoredPosition = new Vector2(right ? inX : parentW - inX, yy); }
            Corner(sumDot, 14f, -8f, TabW);
            Corner(offDot, 8f, -8f, BtnSize); Corner(evDot, 8f, -8f, BtnSize);
            // labels: next to the button, away from the edge
            void Side(RectTransform r, float x, bool inButton)
            {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(right ? 1f : 0f, 0.5f);
                r.anchoredPosition = new Vector2(right ? -x : x, 0f);
            }
            Side(offLabel, 66f, true); Side(evLabel, 66f, true);
            bubble.anchorMin = bubble.anchorMax = new Vector2(right ? 0f : 1f, 1f); bubble.pivot = new Vector2(right ? 1f : 0f, 1f);
            bubble.anchoredPosition = new Vector2(right ? -14f : 14f, -26f);
            panel.anchorMin = panel.anchorMax = new Vector2(right ? 1f : 0f, 1f); panel.pivot = new Vector2(right ? 1f : 0f, 1f);
            panel.localRotation = Quaternion.Euler(0f, 0f, right ? -1f : 1f);
        }

        private bool Point(PointerEventData e, out Vector2 p)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, e.position, e.pressEventCamera, out var l)) { p = default; return false; }
            var r = Root.rect;
            p = new Vector2(l.x - r.xMin, r.yMax - l.y);        // from the top-left corner of the screen
            return true;
        }

        internal void BeginDrag(PointerEventData e)
        {
            if (!Point(e, out var p)) return;
            SetOpen(false); ClosePanel();
            if (snap != null) { StopCoroutine(snap); snap = null; }
            dragging = true;
            bubbleGroup.alpha = 0f; PlayerPrefs.SetInt(HintPref, 1);
            grab = new Vector2(menu.anchoredPosition.x - p.x, -menu.anchoredPosition.y - p.y);
            tabImage.sprite = pillSprite; tabRt.localScale = Vector3.one * 1.14f;
        }

        internal void Drag(PointerEventData e)
        {
            if (!dragging || !Point(e, out var p)) return;
            var r = Root.rect; Limits(out float min, out float max);
            float x = Mathf.Clamp(p.x + grab.x, 0f, r.width - TabW), top = Mathf.Clamp(p.y + grab.y, min, max);
            menu.anchoredPosition = new Vector2(x, -top); topPx = top;
            nearLeft = x + TabW * 0.5f < r.width * 0.5f;
        }

        internal void EndDrag(PointerEventData e)
        {
            if (!dragging) return;
            dragging = false;
            right = !nearLeft;
            tabImage.sprite = tabSprite; tabRt.localScale = Vector3.one;
            ApplySide();
            topFrac = topPx / Root.rect.height;
            PlayerPrefs.SetInt(SidePref, right ? 1 : 0); PlayerPrefs.SetFloat(YPref, topFrac); PlayerPrefs.Save();
            snap = StartCoroutine(Snap(Root.rect.width));
        }

        private IEnumerator Snap(float w)
        {
            float from = menu.anchoredPosition.x, to = right ? w - TabW : 0f;
            for (float t = 0f; t < SnapTime; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / SnapTime, 3f);
                menu.anchoredPosition = new Vector2(Mathf.Lerp(from, to, k), menu.anchoredPosition.y);
                yield return null;
            }
            menu.anchoredPosition = new Vector2(to, menu.anchoredPosition.y);
            snap = null;
        }

        /// <summary>Back to the first place (the settings offer this).</summary>
        public void ResetPlace()
        {
            PlayerPrefs.DeleteKey(SidePref); PlayerPrefs.DeleteKey(YPref); PlayerPrefs.DeleteKey(HintPref);
            right = true; topFrac = -1f; ApplySide(); Place();
        }

        // ---------------------------------------------------------------- every frame
        private void Update()
        {
            var r = Root.rect;
            if (!placed || (!dragging && snap == null && (Mathf.Abs(r.width - lastW) > 1f || Mathf.Abs(r.height - lastH) > 1f))) Place();
            float dt = Time.unscaledDeltaTime;

            // the drop-down opens like the HTML one (max-height 0.4 s, icons fade in after it)
            openT = Mathf.MoveTowards(openT, open ? 1f : 0f, dt / 0.4f);
            float k = 1f - Mathf.Pow(1f - openT, 3f);
            dropBg.anchoredPosition = new Vector2(TabW * 0.5f, -(TabH - Overlap));      // its pivot is in the middle, so that it can flip to the other side
            dropBg.sizeDelta = new Vector2(TabW, dropH * k);
            dropItems.anchoredPosition = new Vector2(0f, -(TabH - Overlap));
            dropItemsGroup.alpha = Mathf.Clamp01((openT - 0.35f) / 0.65f);
            if (openT <= 0f && dropBg.gameObject.activeSelf) dropBg.gameObject.SetActive(false);
            chevron.localRotation = Quaternion.Euler(0f, 0f, 180f * k);
            sumDot.GetComponent<CanvasGroup>().alpha = 1f - k;

            // names next to the icons for a moment
            hintLeft -= dt;
            float la = open && hintLeft > 0f && openT > 0.8f ? 1f : 0f;
            float evLa = open && openT > 0.8f && (hintLeft > 0f || events == 0) ? 1f : 0f;   // "no events" stays written while the menu is open
            offLabelGroup.alpha = Mathf.MoveTowards(offLabelGroup.alpha, la, dt * 5f); mailLabelGroup.alpha = Mathf.MoveTowards(mailLabelGroup.alpha, la, dt * 5f); evLabelGroup.alpha = Mathf.MoveTowards(evLabelGroup.alpha, evLa, dt * 5f);

            // the ring that pulses while something is new
            if (Total > 0 && !open && !dragging)
            {
                float p = Time.unscaledTime % 2f / 2f;
                float s = Mathf.Lerp(0.8f, 1.35f, p) * 1.06f;
                ringImage.rectTransform.localScale = new Vector3((right ? 1f : -1f) * s, s, 1f);
                ringImage.color = new Color(0.88f, 0.32f, 0.35f, 0.8f * (1f - p));
            }
            else ringImage.color = Color.clear;

            // edges and the carried look
            float ea = dragging ? 1f : 0f;
            edgesGroup.alpha = Mathf.MoveTowards(edgesGroup.alpha, ea, dt * 5f); hintGroup.alpha = edgesGroup.alpha;
            edgeL.color = Color.Lerp(edgeL.color, dragging && nearLeft ? new Color(0.96f, 0.89f, 0.66f, 0.30f) : new Color(0.73f, 0.56f, 0.28f, 0.12f), dt * 12f);
            edgeR.color = Color.Lerp(edgeR.color, dragging && !nearLeft ? new Color(0.96f, 0.89f, 0.66f, 0.30f) : new Color(0.73f, 0.56f, 0.28f, 0.12f), dt * 12f);

            // the sheet and its scrim
            panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, panelOpen ? 1f : 0f, dt * 4f);
            scrimGroup.alpha = Mathf.MoveTowards(scrimGroup.alpha, panelOpen ? 1f : 0f, dt * 4f);
            float ps = Mathf.Lerp(0.92f, 1f, panelGroup.alpha); panel.localScale = new Vector3(ps, ps, 1f);

            // the undo toast
            if (toastLeft > 0f) toastLeft -= dt;
            toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, toastLeft > 0f ? 1f : 0f, dt * 4f);
            if (toastLeft <= 0f && toastGroup.alpha <= 0f) { toastGroup.blocksRaycasts = false; undoId = null; }

            // the first-run hint beside the tab
            if (Visible && !PlayerPrefs.HasKey(HintPref) && !open && !dragging)
            {
                float t = Time.unscaledTime;
                if (!hintStart.HasValue) hintStart = t + 0.9f;
                float a = t >= hintStart.Value && t < hintStart.Value + 5f ? 1f : 0f;
                bubbleGroup.alpha = Mathf.MoveTowards(bubbleGroup.alpha, a, dt * 3f);
                float nudge = Mathf.Sin(t * 4.5f) * 14f;
                bubble.anchoredPosition = new Vector2((right ? -14f : 14f) + (right ? -nudge : nudge), -26f);
            }

            tick -= dt;
            if (tick <= 0f) { tick = 0.5f; UpdateTimes(false); }
        }

        private float? hintStart;
    }
}
