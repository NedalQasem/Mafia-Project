using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The bag (matches Docs/mockups/bag.html): everything the player bought that stays (frames, titles, backgrounds, bubbles, entries,
    /// table cards), inside an open briefcase. On top what he wears now, then a chip per kind, then the kinds as velvet compartments,
    /// three to a row; the worn one carries a "worn" tag. A tap opens the item's sheet (wear / take off). With nothing bought the bag
    /// says so and shows the free items. The app fills it (BagFlow); sizes are the mockup's CSS px times K.
    /// </summary>
    public class KitBagScreen : MonoBehaviour
    {
        public class Kind { public string id, name, hint; }

        public const float K = 1080f / 390f;

        [Header("Top")]
        [SerializeField] private TMP_Text sub;
        [SerializeField] private TMP_Text count;
        [SerializeField] private RectTransform countTag;
        [Header("What he wears")]
        [SerializeField] private LayoutElement look;
        [SerializeField] private RectTransform lookArt;
        [SerializeField] private TMP_Text lookName;
        [SerializeField] private RectTransform bitsRoot;
        [SerializeField] private GameObject bitTemplate;
        [Header("Kinds")]
        [SerializeField] private RectTransform chipsContent;
        [SerializeField] private GameObject chipTemplate;
        [SerializeField] private Sprite chipOn, chipOff;
        [Header("Compartments")]
        [SerializeField] private RectTransform list;              // the scroll content; look and chips are its first two children
        [SerializeField] private GameObject sectionTemplate, gridTemplate, cellTemplate, emptyKindTemplate, emptyBag;
        [SerializeField] private Sprite cellOn, cellOff;
        [Header("Item sheet")]
        [SerializeField] private GameObject sheetLayer;
        [SerializeField] private Button dim;
        [SerializeField] private RectTransform stageArt;
        [SerializeField] private TMP_Text sheetName, sheetRarity, sheetKind, sheetDesc, sheetPrice;
        [SerializeField] private RectTransform rarityPill;
        [SerializeField] private Button wearButton, offButton;
        [SerializeField] private GameObject wornLine;
        [SerializeField] private Button storeButton;

        /// <summary>Wear this item (it replaces the worn one of its kind).</summary>
        public event Action<StoreItem> OnWear;
        /// <summary>Take off what is worn in this kind.</summary>
        public event Action<string> OnTakeOff;
        /// <summary>The player wants the store (the empty bag, or a kind he has nothing of).</summary>
        public event Action OnStore;

        private readonly List<GameObject> made = new List<GameObject>();
        private readonly List<GameObject> chips = new List<GameObject>();
        private List<Kind> kinds = new List<Kind>();
        private List<StoreItem> items = new List<StoreItem>();
        private string tab = "all";
        private StoreItem open;
        private string playerName = "";
        private bool emptyMode;

        private static readonly string[] RarityNames = { "عادي", "نادر", "مميّز", "أسطوري" };
        private static readonly Color[] RarityColors = { new Color32(0xD9, 0xD0, 0xBD, 0x88), new Color32(0x4F, 0xA5, 0x8B, 0xFF), new Color32(0xE0, 0x46, 0x4F, 0xFF), new Color32(0xF5, 0xD0, 0x70, 0xFF) };

        private void Awake()
        {
            foreach (var t in new[] { bitTemplate, chipTemplate, sectionTemplate, gridTemplate, cellTemplate, emptyKindTemplate }) if (t != null) t.SetActive(false);
            if (dim != null) dim.onClick.AddListener(CloseSheet);
            if (wearButton != null) wearButton.onClick.AddListener(() => { if (open != null) OnWear?.Invoke(open); });
            if (offButton != null) offButton.onClick.AddListener(() => { if (open != null) OnTakeOff?.Invoke(open.categoryId); });
            if (storeButton != null) storeButton.onClick.AddListener(() => OnStore?.Invoke());
            if (sheetLayer != null) sheetLayer.SetActive(false);
        }

        /// <summary>Draws the bag. owned = the items the player has (bought or free), with equipped set; empty = he bought nothing yet.</summary>
        public void Show(string name, List<Kind> kindList, List<StoreItem> owned, bool empty)
        {
            playerName = name ?? ""; kinds = kindList ?? new List<Kind>(); items = owned ?? new List<StoreItem>(); emptyMode = empty;
            if (tab != "all" && kinds.Find(k => k.id == tab) == null) tab = "all";
            Redraw();
            if (open != null) { open = items.Find(i => i.id == open.id); if (open != null) FillSheet(); else CloseSheet(); }
        }

        private void Redraw()
        {
            foreach (var g in made) if (g != null) Destroy(g);
            made.Clear();

            sub.GetComponent<ArabicText>().Text = emptyMode ? "لسا ما اشتريت شي" : "أغراضك اللي اشتريتها، البس اللي بدك ياه";
            SetText(count, Things(items.Count), 0f);
            countTag.sizeDelta = new Vector2(Width(count) + 36f * K, countTag.sizeDelta.y);   // padding 10 + the sprite's 8, the tag grows with its text

            DrawLook();
            DrawChips();
            if (emptyBag != null) emptyBag.SetActive(emptyMode);
            if (emptyMode) { AddSection("إلك من الأول", "مجاناً"); AddGrid(items); return; }
            foreach (var k in kinds)
            {
                if (tab != "all" && tab != k.id) continue;
                var mine = items.FindAll(i => i.categoryId == k.id);
                if (tab == "all" && mine.Count == 0) continue;
                AddSection(k.name, Ar(mine.Count) + " · " + k.hint);
                if (mine.Count > 0) AddGrid(mine);
                else
                {
                    var e = Make(emptyKindTemplate);
                    SetText(e.transform.Find("Title").GetComponent<TMP_Text>(), "ما عندك " + k.name + " بعد", 300f);
                    var b = e.GetComponentInChildren<Button>(true); if (b != null) b.onClick.AddListener(() => OnStore?.Invoke());
                }
            }
        }

        // ---------------------------------------------------------------- what he wears

        private void DrawLook()
        {
            var frame = items.Find(i => i.equipped && i.art == "ring");
            if (frame != null) DrawArt(lookArt, frame, 0.78f);
            else
            {   // no frame worn: the plain picture
                ClearKids(lookArt);
                var av = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
                var ar = (RectTransform)av.transform; ar.SetParent(lookArt, false); ar.anchorMin = Vector2.zero; ar.anchorMax = Vector2.one; ar.offsetMin = ar.offsetMax = Vector2.zero;
                var im = av.GetComponent<Image>(); im.sprite = KitTheme.Current != null ? KitTheme.Current.avatarDefault : null; im.raycastTarget = false;
            }
            SetText(lookName, playerName, 256f);
            for (int i = bitsRoot.childCount - 1; i >= 0; i--) if (bitsRoot.GetChild(i).gameObject != bitTemplate) Destroy(bitsRoot.GetChild(i).gameObject);
            var worn = items.FindAll(i => i.equipped && i.art != "ring");
            var names = worn.ConvertAll(i => i.name);
            if (names.Count == 0) names.Add(frame != null ? "ما لابس شي غير الإطار" : "ما لابس شي بعد");
            // pills flow right to left and wrap inside 256px (gap 4, rows of 20 + 4)
            float x = 0f, y = 0f; int rows = 1;
            foreach (var n in names)
            {
                var b = Instantiate(bitTemplate, bitsRoot); b.SetActive(true);
                var t = b.GetComponentInChildren<TMP_Text>();
                SetText(t, n, 256f - 14f);
                float w = Mathf.Min(256f, Width(t) / K + 14f);
                if (x > 0f && x + w > 256f) { x = 0f; y += 24f; rows++; }
                var r = (RectTransform)b.transform;
                r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f);
                r.sizeDelta = new Vector2(w * K, 20f * K); r.anchoredPosition = new Vector2(-x * K, -y * K);
                x += w + 4f;
            }
            // the card hugs its text: label 19 + 4 + name 21 + 4 + the pills, padding 12 above and below; never shorter than the 66px frame
            float mid = 19f + 4f + 21.11f + 4f + rows * 20f + (rows - 1) * 4f;
            look.preferredHeight = (Mathf.Max(66f, mid) + 24f) * K;
            var mr = (RectTransform)lookName.transform.parent;
            mr.sizeDelta = new Vector2(mr.sizeDelta.x, mid * K);
        }

        // ---------------------------------------------------------------- chips

        private void DrawChips()
        {
            foreach (var c in chips) if (c != null) Destroy(c);
            chips.Clear();
            AddChip("all", "الكل", items.Count);
            foreach (var k in kinds) AddChip(k.id, k.name, items.FindAll(i => i.categoryId == k.id).Count);
            LayoutRebuilder.ForceRebuildLayoutImmediate(chipsContent);
        }

        private void AddChip(string id, string name, int n)
        {
            var c = Instantiate(chipTemplate, chipsContent); c.SetActive(true); chips.Add(c);
            bool on = tab == id;
            c.GetComponent<Image>().sprite = on ? chipOn : chipOff;
            var t = c.transform.Find("Text").GetComponent<TMP_Text>();
            var nt = c.transform.Find("N/Text").GetComponent<TMP_Text>();
            SetText(t, name, 0f); SetText(nt, Ar(n), 0f);
            t.color = on ? new Color32(0x2A, 0x1A, 0x08, 255) : new Color32(0xCD, 0xBF, 0x9F, 255);
            nt.color = on ? new Color32(0x2A, 0x1A, 0x08, 255) : new Color32(0xA9, 0x8F, 0x62, 255);
            var ni = c.transform.Find("N").GetComponent<Image>(); ni.color = on ? new Color32(0x2A, 0x1A, 0x08, 0x55) : new Color32(0, 0, 0, 0xAA);
            // padding 6 12, gap 6; the count pill is 18 high and at least 18 wide (padding 0 5)
            float tw = Width(t) / K, nw = Mathf.Max(18f, Width(nt) / K + 10f);
            ((RectTransform)t.transform).sizeDelta = new Vector2(tw * K + 2f, ((RectTransform)t.transform).sizeDelta.y);
            var nr = (RectTransform)ni.transform; nr.sizeDelta = new Vector2(nw * K, nr.sizeDelta.y);
            var le = c.GetComponent<LayoutElement>(); le.preferredWidth = (12f + tw + 6f + nw + 12f) * K;
            var g = c.GetComponent<CanvasGroup>(); g.alpha = n == 0 && id != "all" ? Mathf.Pow(0.45f, 2.2f) : 1f;   // Linear colour space
            c.GetComponent<Button>().onClick.AddListener(() => { tab = id; Redraw(); });
        }

        // ---------------------------------------------------------------- compartments

        private void AddSection(string title, string small)
        {
            var s = Make(sectionTemplate);
            var t = s.transform.Find("Title").GetComponent<TMP_Text>(); SetText(t, title, 200f);
            var sm = s.transform.Find("Small").GetComponent<TMP_Text>(); SetText(sm, small, 362f - 16f - Width(t) / K - 12f);
            var tr = (RectTransform)t.transform; tr.sizeDelta = new Vector2(Width(t) + 4f, tr.sizeDelta.y);
            var smr = (RectTransform)sm.transform; smr.sizeDelta = new Vector2(Width(sm) + 4f, smr.sizeDelta.y);
            smr.anchoredPosition = new Vector2(-(Width(t) / K + 8f) * K, smr.anchoredPosition.y);
            var line = (RectTransform)s.transform.Find("Line");
            float left = 362f - (Width(t) / K + 8f + Width(sm) / K + 8f);
            line.sizeDelta = new Vector2(Mathf.Max(0f, left) * K, line.sizeDelta.y);
        }

        private void AddGrid(List<StoreItem> list)
        {
            var g = Make(gridTemplate);
            foreach (var it in list)
            {
                var c = Instantiate(cellTemplate, g.transform); c.SetActive(true);
                var bg = c.transform.Find("Bg").GetComponent<Image>();
                bg.sprite = it.equipped ? cellOn : cellOff;
                var br = (RectTransform)bg.transform; float pad = (it.equipped ? 10f : 6f) * K;     // the worn one's glow reaches further
                br.offsetMin = new Vector2(-pad, -pad); br.offsetMax = new Vector2(pad, pad);
                var art = (RectTransform)c.transform.Find("Art");
                DrawArt(art, it, 1f);
                SetText(c.transform.Find("Name").GetComponent<TMP_Text>(), it.name, 104f);
                c.transform.Find("Rar").GetComponent<Image>().color = RarityColors[Mathf.Clamp((int)it.rarity, 0, 3)];
                c.transform.Find("Tag").gameObject.SetActive(it.equipped);
                var item = it;
                c.GetComponent<Button>().onClick.AddListener(() => OpenSheet(item));
            }
        }

        private GameObject Make(GameObject template)
        {
            var g = Instantiate(template, list); g.SetActive(true); made.Add(g);
            return g;
        }

        // ---------------------------------------------------------------- the item sheet

        private void OpenSheet(StoreItem it)
        {
            open = it;
            sheetLayer.SetActive(true);      // first, so the stage has its real size when the picture is fitted into it
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)sheetName.transform.parent);
            FillSheet();
        }

        public void CloseSheet() { open = null; if (sheetLayer != null) sheetLayer.SetActive(false); }

        private void FillSheet()
        {
            DrawArt(stageArt, open, 1.5f);
            SetText(sheetName, open.name, 354f);
            SetText(sheetRarity, RarityNames[Mathf.Clamp((int)open.rarity, 0, 3)], 0f);
            rarityPill.GetComponent<LayoutElement>().preferredWidth = Width(sheetRarity) + 16f * K;   // padding 1 8; the row lays it out
            var k = kinds.Find(x => x.id == open.categoryId);
            SetText(sheetKind, k != null ? k.name + " · " + k.hint : "", 354f - 50f);
            sheetDesc.GetComponent<ArabicText>().Text = string.IsNullOrEmpty(open.desc) ? "" : open.desc;
            SetText(sheetPrice, open.price > 0 ? "اشتريته من المتجر بـ " + CoinPill.Format(open.price) + " كوينز" : "إلك من الأول", 354f);
            wearButton.gameObject.SetActive(!open.equipped);
            offButton.gameObject.SetActive(open.equipped);
            wornLine.SetActive(open.equipped);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)sheetName.transform.parent);
        }

        // ---------------------------------------------------------------- helpers

        // natural size of each picture StoreArt draws (canvas units), so it can be fitted in a box
        private static Vector2 ArtSize(StoreItem it)
        {
            if (it.sprite != null) return new Vector2(220, 220);
            switch (it.art)
            {
                case "ring": return new Vector2(205, 205);      // the ring without its glow
                case "thumb": return new Vector2(150, 188);
                case "card": return new Vector2(140, 196);
                case "plate": return new Vector2(270, 100);
                case "bubble": return new Vector2(278, 164);
                case "entry": return new Vector2(290, 92);
                default: return new Vector2(220, 220);
            }
        }

        /// <summary>Draws an item's picture into a box: StoreArt builds it at its own size inside a holder that is scaled to fit (at most max).</summary>
        private static void DrawArt(RectTransform box, StoreItem it, float max)
        {
            ClearKids(box);
            var h = new GameObject("Holder", typeof(RectTransform)).GetComponent<RectTransform>();
            h.SetParent(box, false); h.anchorMin = h.anchorMax = h.pivot = new Vector2(0.5f, 0.5f); h.anchoredPosition = Vector2.zero;
            var nat = ArtSize(it); var r = box.rect.size;
            float s = Mathf.Min(max, r.x / nat.x, r.y / nat.y);
            h.localScale = new Vector3(s, s, 1f); h.sizeDelta = r / s;
            StoreArt.Create(it, h, KitTheme.Current);
        }

        private static void ClearKids(RectTransform r) { for (int i = r.childCount - 1; i >= 0; i--) Destroy(r.GetChild(i).gameObject); }

        private static float Width(TMP_Text t) { t.ForceMeshUpdate(); return t.preferredWidth; }

        /// <summary>Sets a line of text; when maxCss > 0 and it is wider, cuts it with an ellipsis measured on the real font
        /// (ArabicText reverses the line, so TMP's own ellipsis would cut the wrong end). Lalezar draws "…" as a dot: it gets "...".</summary>
        private static void SetText(TMP_Text t, string s, float maxCss)
        {
            var a = t.GetComponent<ArabicText>();
            s = s ?? "";
            a.Text = s;
            if (maxCss <= 0f || Width(t) <= maxCss * K) return;
            string dots = t.font != null && t.font.name.Contains("Lalezar") ? "..." : "…";
            int lo = 0, hi = s.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                a.Text = s.Substring(0, mid).TrimEnd() + dots;
                if (Width(t) <= maxCss * K) lo = mid; else hi = mid - 1;
            }
            a.Text = s.Substring(0, lo).TrimEnd() + dots;
        }

        private static string Ar(int n) => CoinPill.Format(n);

        /// <summary>Arabic counts the noun by the number: غرض واحد، غرضين، ٣-١٠ أغراض، ١١+ غرض.</summary>
        public static string Things(int n) => n == 1 ? "غرض واحد" : n == 2 ? "غرضين" : Ar(n) + (n >= 3 && n <= 10 ? " أغراض" : " غرض");
    }
}
