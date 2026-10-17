using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>The store: ad carousel, category drawers, shelves, and the buy sheet.</summary>
    public class KitStoreScreen : MonoBehaviour
    {
        [SerializeField] private StoreAdCarousel carousel;
        [SerializeField] private RectTransform catsRoot, shelvesRoot;
        [SerializeField] private GameObject catPrefab, shelfPrefab, itemPrefab, headerPrefab;
        // buy sheet
        [SerializeField] private CanvasGroup dim;
        [SerializeField] private RectTransform sheet, preview;
        [SerializeField] private ArabicText sheetName, sheetDesc, sheetChip, sheetPrice, sheetMsg;
        [SerializeField] private Button sheetBuy, sheetClose;
        [SerializeField] private Image sheetBuyImage, sheetChipBg;
        [SerializeField] private int playerRank = 3;

        public event Action<int> OnCoinsChanged;
        public int Coins { get; set; } = 1250;

        /// <summary>Set by the app layer: true when the player owns the item. Without it a local demo set is used.</summary>
        public Func<StoreItem, bool> OwnedResolver;
        /// <summary>Set by the app layer: performs the real purchase and reports (success, message). Without it the demo purchase runs.</summary>
        public Action<StoreItem, Action<bool, string>> PurchaseHandler;
        /// <summary>Player rank (1-based) used for rank-locked items.</summary>
        public int PlayerRank { get => playerRank; set { playerRank = value; if (Application.isPlaying && catButtons.Count > 0) Select(category); } }

        private static readonly HashSet<string> demoOwned = new HashSet<string>();
        private bool buying;

        private int category;
        private StoreItem open;
        private readonly List<Button> catButtons = new List<Button>();
        private Coroutine sheetAnim;

        private void Awake()
        {
            if (sheetClose != null) sheetClose.onClick.AddListener(CloseSheet);
            if (sheetBuy != null) sheetBuy.onClick.AddListener(Buy);
            var d = dim != null ? dim.GetComponent<Button>() : null; if (d != null) d.onClick.AddListener(CloseSheet);
        }

        private void Start()
        {
            if (Application.isPlaying) Build();
        }

        public void Build()
        {
            var th = KitTheme.Current;
            // categories
            for (int i = catsRoot.childCount - 1; i >= 0; i--) DestroyAny(catsRoot.GetChild(i).gameObject);
            catButtons.Clear();
            for (int i = 0; i < StoreData.CategoryNames.Length; i++)
            {
                int idx = i;
                var go = Instantiate(catPrefab, catsRoot);
                go.GetComponentInChildren<ArabicText>().Text = StoreData.CategoryNames[i];
                var b = go.GetComponent<Button>(); b.onClick.AddListener(() => Select(idx));
                catButtons.Add(b);
            }
            // ads
            carousel.SetAds(new List<StoreAdCarousel.Ad>
            {
                new StoreAdCarousel.Ad { mast = "عرض الليلة فقط", headline = "إطار الجمر\nبنصف الثمن تقريباً", body = "حلقة نارية تتوهج حول صورتك.", stamp = "−٣٠٪", price = "٨٤٠  ‏", item = StoreData.Items(0)[4], countdown = true },
                new StoreAdCarousel.Ad { mast = "وصل حديثاً", headline = "خلفية الميناء\nسفن وليل أسود", body = "غيّر لون مجلسك بخلفية جديدة.", stamp = "جديد", price = "٩٠٠", item = StoreData.Items(2)[4] },
                new StoreAdCarousel.Ad { mast = "هدية الأسبوع", headline = "رصاصة ذهبية\nلمن أصاب الهدف", body = "أرسلها للاعب بالغرفة.", price = "٥٠٠", item = StoreData.Items(5)[1] },
                new StoreAdCarousel.Ad { mast = "عرض الكوينز", headline = "صندوق كوينز\nبسعر الحفنة", body = "٣٬٥٠٠ كوينز للهدايا والزينة فقط.", stamp = "+٣٠٪", price = "٤٫٩٩ $", item = StoreData.Items(8)[2] },
            });
            carousel.OnBuy += item => OpenSheet(item);
            Select(category);
        }

        private static void DestroyAny(GameObject g) { if (Application.isPlaying) Destroy(g); else DestroyImmediate(g); }

        // slide the category row so the chosen drawer is in view (HTML scrolls the tab strip too)
        private System.Collections.IEnumerator ScrollTabsTo(int idx)
        {
            yield return null;
            if (idx < 0 || idx >= catButtons.Count) yield break;
            var sr = catsRoot.GetComponentInParent<ScrollRect>(); if (sr == null) yield break;
            var vp = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
            float vw = vp.rect.width, cw = catsRoot.rect.width;
            if (cw <= vw) yield break;
            var bt = (RectTransform)catButtons[idx].transform;
            float bx = catsRoot.InverseTransformPoint(bt.position).x;
            float target = Mathf.Clamp(-vw * 0.5f - bx, 0f, cw - vw);
            float from = catsRoot.anchoredPosition.x;
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                float e = 1f - Mathf.Pow(1f - t / 0.35f, 3f);
                catsRoot.anchoredPosition = new Vector2(Mathf.Lerp(from, target, e), catsRoot.anchoredPosition.y);
                yield return null;
            }
            catsRoot.anchoredPosition = new Vector2(target, catsRoot.anchoredPosition.y);
        }

        private Coroutine tabScroll;

        public void Select(int cat)
        {
            category = cat;
            if (Application.isPlaying && isActiveAndEnabled) { if (tabScroll != null) StopCoroutine(tabScroll); tabScroll = StartCoroutine(ScrollTabsTo(cat)); }
            var th = KitTheme.Current;
            for (int i = 0; i < catButtons.Count; i++)
            {
                var img = catButtons[i].GetComponent<Image>();
                bool on = i == cat;
                img.sprite = on ? th.brassPlate : th.leatherDark;
                var tx = catButtons[i].GetComponentInChildren<TMPro.TMP_Text>();
                tx.color = on ? th.brassText : th.ivoryDim;
                ((RectTransform)catButtons[i].transform).anchoredPosition = new Vector2(((RectTransform)catButtons[i].transform).anchoredPosition.x, 0);
                catButtons[i].transform.localScale = Vector3.one * (on ? 1.08f : 1f);
            }
            for (int i = shelvesRoot.childCount - 1; i >= 0; i--) DestroyAny(shelvesRoot.GetChild(i).gameObject);
            var header = Instantiate(headerPrefab, shelvesRoot);
            header.GetComponentInChildren<ArabicText>().Text = StoreData.CategoryNames[cat] + "  ·  " + StoreData.CategoryHints[cat];
            var items = StoreData.Items(cat);
            foreach (var it in items) if (!it.owned && IsOwned(it)) it.owned = true;
            int per = cat == 3 || cat == 4 || cat == 8 ? 2 : 3;
            int rows = Mathf.CeilToInt(items.Count / (float)per);
            float total = 90f + rows * 470f;
            var le = shelvesRoot.GetComponent<LayoutElement>(); if (le != null) le.preferredHeight = total;
            shelvesRoot.sizeDelta = new Vector2(1080f, total);
            for (int r = 0; r * per < items.Count; r++)
            {
                var shelf = Instantiate(shelfPrefab, shelvesRoot);
                var row = shelf.transform.Find("Row");
                for (int k = 0; k < per && r * per + k < items.Count; k++)
                {
                    var go = Instantiate(itemPrefab, row);
                    var v = go.GetComponent<StoreItemView>();
                    v.Bind(items[r * per + k], playerRank);
                    v.OnClick += OpenSheet;
                    KitEnterAnim.Play(go, k * 0.08f + r * 0.12f, 0f, 0.5f, 0.4f, -38f);   // HTML .slot: rises 14px and fades in, staggered
                }
            }
        }

        public void OpenSheet(StoreItem item)
        {
            var th = KitTheme.Current;
            open = item;
            if (!item.owned && IsOwned(item)) item.owned = true;
            StoreArt.Create(item, preview, th);
            sheetName.Text = item.name; sheetDesc.Text = item.desc;
            sheetChip.Text = item.rarity == Rarity.Common ? "عادي" : item.rarity == Rarity.Rare ? "نادر" : item.rarity == Rarity.Epic ? "ملحمي" : "أسطوري";
            // HTML .chip c-l / c-e / c-r: outline and text tinted by rarity
            Color32 outline, txt;
            switch (item.rarity)
            {
                case Rarity.Legendary: outline = new Color32(0xB9, 0x90, 0x48, 255); txt = new Color32(0xF5, 0xD0, 0x70, 255); break;
                case Rarity.Epic: outline = new Color32(0x8A, 0x1C, 0x24, 255); txt = new Color32(0xE6, 0xB3, 0xB6, 255); break;
                case Rarity.Rare: outline = new Color32(0x2D, 0x7A, 0x60, 255); txt = new Color32(0x8F, 0xE0, 0xC6, 255); break;
                default: outline = new Color32(0x4A, 0x44, 0x3E, 255); txt = new Color32(0xD9, 0xD0, 0xBD, 255); break;
            }
            if (sheetChipBg != null) sheetChipBg.color = outline;
            sheetChip.GetComponent<TMPro.TMP_Text>().color = txt;
            bool locked = item.lockRank > playerRank;
            sheetMsg.Text = locked ? "يفتح عند رتبة " + KitTheme.RankNames[item.lockRank - 1] : "";
            sheetPrice.Text = item.owned ? "استخدمه" : locked ? "مقفل" : (string.IsNullOrEmpty(item.priceText) ? CoinPill.Format(item.price) : item.priceText);
            sheetBuy.interactable = !locked;
            var coinIcon = sheetBuy.transform.Find("Coin"); if (coinIcon != null) coinIcon.gameObject.SetActive(!item.owned && !locked && string.IsNullOrEmpty(item.priceText));
            var buyLabel = sheetPrice.GetComponent<RectTransform>(); buyLabel.offsetMax = new Vector2(coinIcon != null && coinIcon.gameObject.activeSelf ? -60f : -6f, buyLabel.offsetMax.y);
            sheetBuyImage.color = locked ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
            gameObject.SetActive(true);
            if (sheetAnim != null) StopCoroutine(sheetAnim);
            sheetAnim = StartCoroutine(Slide(true));
        }

        public void CloseSheet() { if (sheetAnim != null) StopCoroutine(sheetAnim); sheetAnim = StartCoroutine(Slide(false)); }

        private IEnumerator Slide(bool show)
        {
            dim.blocksRaycasts = show;
            float from = sheet.anchoredPosition.y, to = show ? 0f : -(sheet.rect.height + 80f);
            float a0 = dim.alpha, a1 = show ? 1f : 0f;
            for (float t = 0; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.45f); float e = 1f - Mathf.Pow(1f - k, 3f);
                sheet.anchoredPosition = new Vector2(0, Mathf.Lerp(from, to, e));
                dim.alpha = Mathf.Lerp(a0, a1, e);
                yield return null;
            }
            sheet.anchoredPosition = new Vector2(0, to); dim.alpha = a1;
        }

        private bool IsOwned(StoreItem it) => OwnedResolver != null ? OwnedResolver(it) : demoOwned.Contains(it.id);

        private void Buy()
        {
            if (open == null || buying) return;
            if (open.owned) { sheetPrice.Text = "تم"; CloseSheet(); return; }
            if (!string.IsNullOrEmpty(open.priceText))
            {
                if (PurchaseHandler != null) { sheetMsg.Text = "الشراء بالمال الحقيقي غير متاح بعد."; return; }
                sheetPrice.Text = "تم الشراء"; CloseSheet(); return;
            }
            if (Coins < open.price) { sheetMsg.Text = "رصيدك لا يكفي. اشحن كوينز من قسم الكوينز."; return; }
            if (PurchaseHandler != null)
            {
                buying = true;
                var item = open;
                PurchaseHandler(item, (ok, msg) =>
                {
                    buying = false;
                    if (this == null) return;
                    sheetMsg.Text = msg ?? "";
                    if (!ok) return;
                    item.owned = true;
                    Invoke(nameof(AfterBuy), 0.9f);
                });
                return;
            }
            Coins -= open.price; open.owned = true; demoOwned.Add(open.id);
            OnCoinsChanged?.Invoke(Coins);
            sheetMsg.Text = "أُضيف إلى مقتنياتك";
            Invoke(nameof(AfterBuy), 0.9f);
        }

        private void AfterBuy() { CloseSheet(); Select(category); }
    }
}
