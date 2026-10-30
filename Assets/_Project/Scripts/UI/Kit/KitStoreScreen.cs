using System;
using System.Collections;
using System.Collections.Generic;
using MafiaNight.Data.Store;
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
        [SerializeField] private GameObject catPrefab, shelfPrefab, itemPrefab, headerPrefab, rankCardPrefab;
        // buy sheet
        [SerializeField] private CanvasGroup dim;
        [SerializeField] private RectTransform sheet, preview;
        [SerializeField] private ArabicText sheetName, sheetDesc, sheetChip, sheetPrice, sheetMsg;
        [SerializeField] private Button sheetBuy, sheetClose;
        [SerializeField] private Image sheetBuyImage, sheetChipBg;
        [SerializeField] private int playerRank = 3;

        /// <summary>The launch banner's offer the player pressed "buy" on: the store opens that ad's buy sheet when it builds (then this is cleared).</summary>
        public static string PendingAdId;

        public event Action<int> OnCoinsChanged;
        public int Coins { get; set; } = 1250;

        /// <summary>Set by the app layer: true when the player owns the item. Without it a local demo set is used.</summary>
        public Func<StoreItem, bool> OwnedResolver;
        /// <summary>Set by the app layer: true when the item is worn right now.</summary>
        public Func<StoreItem, bool> EquippedResolver;
        /// <summary>Set by the app layer: wears an owned item. Without it a local demo set is used.</summary>
        public Action<StoreItem> EquipHandler;
        /// <summary>Set by the app layer: performs the real purchase and reports (success, message). Without it the demo purchase runs.</summary>
        public Action<StoreItem, Action<bool, string>> PurchaseHandler;
        /// <summary>Player rank (1-based) used for rank-locked items.</summary>
        public int PlayerRank { get => playerRank; set { if (playerRank == value) return; playerRank = value; if (Application.isPlaying && catButtons.Count > 0) Select(category); } }

        private static readonly HashSet<string> demoOwned = new HashSet<string>();
        private static readonly Dictionary<string, string> demoEquipped = new Dictionary<string, string>();
        private bool buying;

        private List<StoreCategoryDef> cats = new List<StoreCategoryDef>();
        private string category;
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
            // categories (tabs) come from the catalog
            for (int i = catsRoot.childCount - 1; i >= 0; i--) DestroyAny(catsRoot.GetChild(i).gameObject);
            catButtons.Clear();
            cats = StoreData.Categories();
            for (int i = 0; i < cats.Count; i++)
            {
                var cat = cats[i];
                var go = Instantiate(catPrefab, catsRoot);
                var label = go.transform.Find("Body/Label").GetComponent<ArabicText>();
                label.Text = cat.tabName;
                go.transform.Find("Body/Icon").GetComponent<Image>().sprite = cat.icon != null ? cat.icon : th.catIcons[Mathf.Clamp(cat.builtInIcon, 0, th.catIcons.Length - 1)];
                var tmp = label.GetComponent<TMPro.TMP_Text>(); tmp.ForceMeshUpdate();
                float w = Mathf.Max(150f, tmp.preferredWidth + 62f);   // HTML .cat: the label sets the width, padding 12px
                go.GetComponent<LayoutElement>().preferredWidth = w; ((RectTransform)go.transform).sizeDelta = new Vector2(w, 150f);
                var b = go.GetComponent<Button>(); string id = cat.id; b.onClick.AddListener(() => Select(id));
                catButtons.Add(b);
            }
            if (string.IsNullOrEmpty(category) || cats.Find(c => c.id == category) == null)
            {
                var first = cats.Find(c => c.kind == StoreCategoryKind.Items);
                category = first != null ? first.id : (cats.Count > 0 ? cats[0].id : null);
            }
            // ads (newspaper pages) come from the catalog too
            var ads = new List<StoreAdCarousel.Ad>();
            var adItems = new Dictionary<string, StoreItem>();
            var catalog = StoreData.Catalog;
            if (catalog != null)
                foreach (var def in catalog.ads)
                {
                    if (!def.enabled) continue;
                    var item = StoreData.Find(def.itemId);
                    if (item == null) continue;
                    if (def.offerPrice > 0 && def.offerPrice != item.price) { item.price = def.offerPrice; item.storeId = def.offerStoreId; }
                    string price = !string.IsNullOrEmpty(item.priceText) ? item.priceText : CoinPill.Format(item.price);
                    adItems[def.id] = item;
                    ads.Add(new StoreAdCarousel.Ad { id = def.id, mast = def.mast, headline = def.headline, body = def.body, stamp = def.stamp, price = price, item = item, countdown = def.countdownSeconds > 0, countdownSeconds = def.countdownSeconds });
                }
            carousel.gameObject.SetActive(ads.Count > 0);
            carousel.SetAds(ads);
            carousel.OnBuy -= OnAdBuy; carousel.OnBuy += OnAdBuy;
            Select(category);
            if (!string.IsNullOrEmpty(PendingAdId))
            {
                string want = PendingAdId; PendingAdId = null;
                if (Application.isPlaying && adItems.TryGetValue(want, out var wanted)) OnAdBuy(wanted);
            }
        }

        private void OnAdBuy(StoreItem item) { Resolve(item); OpenSheet(item); }

        private static void DestroyAny(GameObject g) { if (Application.isPlaying) Destroy(g); else DestroyImmediate(g); }

        // slide the category row so the chosen drawer is in view (HTML scrolls the tab strip too)
        private System.Collections.IEnumerator ScrollTabsTo(string catId)
        {
            yield return null;
            int idx = cats.FindIndex(c => c.id == catId);
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

        public void Select(string cat)
        {
            category = cat;
            var def = cats.Find(c => c.id == cat);
            if (def == null) return;
            if (Application.isPlaying && isActiveAndEnabled) { if (tabScroll != null) StopCoroutine(tabScroll); tabScroll = StartCoroutine(ScrollTabsTo(cat)); }
            var th = KitTheme.Current;
            for (int i = 0; i < catButtons.Count; i++)
            {
                bool on = cats[i].id == cat;
                var body = (RectTransform)catButtons[i].transform.Find("Body");
                body.GetComponent<Image>().sprite = on ? th.catOn : th.catOff;
                body.anchoredPosition = new Vector2(0f, on ? 11f : 0f);                // HTML .cat.on: lifts 4px
                var tx = body.Find("Label").GetComponent<TMPro.TMP_Text>();
                tx.color = on ? new Color32(0x2A, 0x1A, 0x08, 255) : new Color32(0x8F, 0x87, 0x77, 255);
                var icon = body.Find("Icon").GetComponent<Image>();
                icon.color = on ? Color.white : new Color(0.55f, 0.52f, 0.5f, 0.85f);   // grayscale + dim when not picked
                body.Find("Pointer").gameObject.SetActive(on);
            }
            for (int i = shelvesRoot.childCount - 1; i >= 0; i--) DestroyAny(shelvesRoot.GetChild(i).gameObject);
            var header = Instantiate(headerPrefab, shelvesRoot);
            var title = header.transform.Find("Label").GetComponent<ArabicText>(); var hint = header.transform.Find("Hint").GetComponent<ArabicText>();
            title.Text = def.headTitle; hint.Text = def.hint;
            var tt = title.GetComponent<TMPro.TMP_Text>(); var ht = hint.GetComponent<TMPro.TMP_Text>(); tt.ForceMeshUpdate(); ht.ForceMeshUpdate();
            float hx = -44f - tt.preferredWidth - 28f, lx = hx - ht.preferredWidth - 28f;
            ((RectTransform)hint.transform).anchoredPosition = new Vector2(hx, -6f);
            var line = (RectTransform)header.transform.Find("Line"); line.anchoredPosition = new Vector2(lx, -4f); line.sizeDelta = new Vector2(Mathf.Max(0f, 1080f + lx - 44f), 3f);
            if (def.kind == StoreCategoryKind.RankSets) { ShowRanks(); return; }
            var items = StoreData.Items(cat);
            foreach (var it in items) Resolve(it);
            int per = Mathf.Max(1, def.perRow);
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

        private void ShowRanks()
        {
            int total = 90 + RankSets.All.Length * 508;
            var le = shelvesRoot.GetComponent<LayoutElement>(); if (le != null) le.preferredHeight = total;
            shelvesRoot.sizeDelta = new Vector2(1080f, total);
            for (int i = 0; i < RankSets.All.Length; i++)
            {
                var go = Instantiate(rankCardPrefab, shelvesRoot);
                var v = go.GetComponent<RankSetCardView>();
                v.Bind(i, playerRank);
                v.OnItem += OpenRankItem;
                KitEnterAnim.Play(go, i * 0.07f, 0f, 0.5f, 0.4f, -38f);
            }
        }

        private void OpenRankItem(int rankIndex, int type)
        {
            var r = RankSets.All[rankIndex]; int n = rankIndex + 1; bool lk = n > playerRank;
            string[] rar = { "c", "c", "r", "r", "e", "e", "l", "l" };
            var it = new StoreItem
            {
                id = "rk" + rankIndex + "_" + type, name = r.items[type], art = "rankthumb", idx = n * 10 + type,
                rarity = rar[rankIndex] == "c" ? Rarity.Common : rar[rankIndex] == "r" ? Rarity.Rare : rar[rankIndex] == "e" ? Rarity.Epic : Rarity.Legendary,
                desc = lk ? "يُفتح عند الوصول إلى رتبة " + r.name + ". لا يُشترى بالكوينز." : "مفتوح لك بالترقية. استخدمه متى شئت.",
                owned = !lk, lockRank = lk ? n : 0, equippable = false,
            };
            OpenSheet(it);
        }

        public void OpenSheet(StoreItem item)
        {
            var th = KitTheme.Current;
            open = item;
            Resolve(item);
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
            bool locked = !item.owned && item.lockRank > playerRank;
            sheetMsg.Text = locked ? "يفتح عند رتبة " + KitTheme.RankNames[item.lockRank - 1] : "";
            sheetPrice.Text = item.owned ? (!item.equippable ? (item.id.StartsWith("rk") ? "مفتوح لك" : "تم") : item.equipped ? "مفعّل" : "استخدمه") : locked ? "مقفل" : (string.IsNullOrEmpty(item.priceText) ? CoinPill.Format(item.price) : item.priceText);
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

        private bool IsEquipped(StoreItem it) => EquippedResolver != null ? EquippedResolver(it) : (demoEquipped.TryGetValue(it.categoryId ?? "", out var cur) && cur == it.id);

        /// <summary>Fills the owned / worn state of an item from the app layer (or the demo set).</summary>
        private void Resolve(StoreItem it)
        {
            if (!it.equippable) { it.owned = false; it.equipped = false; return; }   // gifts are used up, never "owned"
            if (!it.owned && IsOwned(it)) it.owned = true;
            it.equipped = it.owned && IsEquipped(it);
        }

        /// <summary>Re-reads owned / worn state and redraws the open shelf (the app layer calls it after outside changes).</summary>
        public void Refresh() { if (Application.isPlaying && catButtons.Count > 0 && !string.IsNullOrEmpty(category)) Select(category); }

        private void Buy()
        {
            if (open == null || buying) return;
            if (open.owned)
            {
                if (open.equippable && !open.equipped)
                {
                    if (EquipHandler != null) EquipHandler(open);
                    else demoEquipped[open.categoryId ?? ""] = open.id;
                    sheetPrice.Text = "تم التفعيل";
                    Invoke(nameof(AfterBuy), 0.5f);
                    return;
                }
                CloseSheet(); return;
            }
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
                    if (item.equippable) item.owned = true;
                    Invoke(nameof(AfterBuy), 0.9f);
                });
                return;
            }
            Coins -= open.price; if (open.equippable) { open.owned = true; demoOwned.Add(open.id); }
            OnCoinsChanged?.Invoke(Coins);
            sheetMsg.Text = "أُضيف إلى مقتنياتك";
            Invoke(nameof(AfterBuy), 0.9f);
        }

        private void AfterBuy() { CloseSheet(); Select(category); }
    }
}
