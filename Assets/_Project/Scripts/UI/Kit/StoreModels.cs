using System;
using System.Collections.Generic;
using MafiaNight.Data.Player;
using MafiaNight.Data.Store;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    public enum Rarity { Common, Rare, Epic, Legendary }

    /// <summary>What the store screens show for one catalog item (a view of <see cref="StoreItemDef"/> plus the state of this player).</summary>
    [Serializable]
    public class StoreItem
    {
        public string id;                 // the catalog id (also the PlayFab ItemId)
        public string categoryId;
        public string name, desc;
        public int price;
        public string priceText;          // real-money packs
        public Rarity rarity;
        public bool owned, isNew;
        public bool equipped;             // worn right now (only meaningful when owned)
        public bool equippable;           // wearing it replaces the previous item of its category
        public int lockRank;              // 1-based rank needed, 0 = none
        public string art;                // ring | thumb | gift | card | plate | pack | bubble | entry | rankthumb
        public int idx;
        public Color c1 = Color.white;
        public string text;
        public Sprite sprite;             // replaces the built-in drawing when set
        public string storeId;            // PlayFab store holding an offer price (ads)
    }

    /// <summary>Reads the store catalog (Resources/Config/StoreCatalog) and turns it into the models the screens draw.</summary>
    public static class StoreData
    {
        public static StoreCatalog Catalog => StoreCatalog.Current;

        public const string RankSetsCategory = "rank_sets";

        /// <summary>Tabs in display order (hidden ones left out).</summary>
        public static List<StoreCategoryDef> Categories()
        {
            var list = new List<StoreCategoryDef>();
            var c = Catalog;
            if (c == null) return list;
            foreach (var cat in c.categories) if (!cat.hidden) list.Add(cat);
            return list;
        }

        public static Rarity ToRarity(ItemRarity r) => (Rarity)(int)r;

        public static string ArtOf(StoreArtKind k)
        {
            switch (k)
            {
                case StoreArtKind.Frame: return "ring";
                case StoreArtKind.RoomThumb:
                case StoreArtKind.ChatBackground: return "thumb";
                case StoreArtKind.ChatBubble: return "bubble";
                case StoreArtKind.EntryStrip: return "entry";
                case StoreArtKind.Gift: return "gift";
                case StoreArtKind.CardBack: return "card";
                case StoreArtKind.Title: return "plate";
                default: return "pack";
            }
        }

        public static StoreItem From(StoreItemDef d, bool consumable = false) => new StoreItem
        {
            id = d.id, categoryId = d.categoryId, name = d.nameAr, desc = d.descAr,
            price = d.price, priceText = d.priceText, rarity = ToRarity(d.rarity),
            owned = d.ownedByDefault, isNew = d.isNew, lockRank = d.lockRank,
            art = ArtOf(d.artKind), idx = d.variant, c1 = d.tint, text = string.IsNullOrEmpty(d.titleText) ? d.nameAr : d.titleText,
            sprite = d.sprite, equippable = !consumable && d.artKind != StoreArtKind.Gift && d.artKind != StoreArtKind.CoinPack,
        };

        /// <summary>The items of one category. The rank-exclusive tab gathers every rank-locked item.</summary>
        public static List<StoreItem> Items(string categoryId)
        {
            var list = new List<StoreItem>();
            var c = Catalog;
            if (c == null) return list;
            var cat = c.GetCategory(categoryId);
            bool consumable = cat != null && !cat.equippable;
            foreach (var d in c.items)
                if (d != null && d.categoryId == categoryId) list.Add(From(d, consumable));
            return list;
        }

        public static StoreItem Find(string itemId)
        {
            var d = Catalog != null ? Catalog.GetItem(itemId) : null;
            if (d == null) return null;
            var cat = Catalog.GetCategory(d.categoryId);
            return From(d, cat != null && !cat.equippable);
        }
    }

    /// <summary>Builds the picture of a store item (frame, thumbnail, gift, card, plate, bubbles...) inside a container.</summary>
    public static class StoreArt
    {
        public static ArabicText MakeText(Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Text", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
            var a = go.AddComponent<ArabicText>(); a.Text = text;
            var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return a;
        }

        private static Image Add(Transform parent, Sprite s, Vector2 pos, Vector2 size, bool sliced = false, float mul = 1f)
        {
            var go = new GameObject("Img", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var i = go.AddComponent<Image>(); i.sprite = s; i.raycastTarget = false;
            i.type = sliced ? Image.Type.Sliced : Image.Type.Simple; i.pixelsPerUnitMultiplier = mul; i.preserveAspect = !sliced;
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
            return i;
        }

        public static void Create(StoreItem it, RectTransform parent, KitTheme th)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
            if (it.sprite != null)
            {
                // an authored picture replaces the built-in drawing
                Add(parent, it.sprite, Vector2.zero, new Vector2(220, 220)).color = it.c1;
                return;
            }
            switch (it.art)
            {
                case "ring":
                    // HTML .fr: 76 px circle, 6 px ring; the texture adds the shadow / glow around it (320 / 256)
                    Add(parent, th.avatarDefault, Vector2.zero, new Vector2(177, 177));
                    Add(parent, th.storeFrames[Mathf.Clamp(it.idx - 1, 0, 5)], Vector2.zero, new Vector2(262, 262));
                    break;
                case "thumb":
                    Add(parent, th.thumbFill, Vector2.zero, new Vector2(150, 188)).color = it.c1;
                    var bar = Add(parent, th.dotWhite, new Vector2(0, -28), new Vector2(86, 22)); bar.color = new Color(1, 1, 1, 0.16f);
                    Add(parent, th.thumbBorder, Vector2.zero, new Vector2(150, 188));
                    break;
                case "gift":
                    Sprite[] g = { th.giftWatch, th.bulletOn, th.propDallah, th.giftKey, th.giftRose, th.giftCrown };
                    Add(parent, g[Mathf.Clamp(it.idx, 0, 5)], Vector2.zero, it.idx == 1 ? new Vector2(80, 200) : new Vector2(200, 200));
                    break;
                case "card":
                    Add(parent, th.cardBack, Vector2.zero, new Vector2(140, 196)).color = it.c1;
                    break;
                case "plate":
                    Sprite[] p = { th.brassPlate, th.leatherDark, th.paper, th.vipFrame };
                    Add(parent, p[Mathf.Clamp(it.idx, 0, 3)], Vector2.zero, new Vector2(270, 100), true, 2.2f);
                    var tc = it.idx == 0 ? th.brassText : it.idx == 2 ? th.paperInk : (it.idx == 3 ? th.brassLight : th.leatherText);
                    var pt = MakeText(parent, it.text, th.fontCalligraphy, 36, tc);
                    break;
                case "rankthumb":
                    RankArt.Build(parent, RankSets.All[it.idx / 10 - 1], it.idx % 10, it.lockRank > 0, 1.9f);
                    break;
                case "pack":
                    for (int i = 0; i < Mathf.Clamp(it.idx, 1, 4); i++) Add(parent, th.coin, new Vector2((i % 2 == 0 ? -10 : 12), -70 + i * 38), new Vector2(110, 110));
                    break;
                case "bubble":
                    Sprite[] b = { th.leatherDark, th.paper, th.paperTicket, th.paper, th.brassPlate, th.vipFrame };
                    int k = Mathf.Clamp(it.idx, 0, 5);
                    var b1 = Add(parent, b[k], new Vector2(-34, 38), new Vector2(210, 78), true, 2.2f);
                    var b2 = Add(parent, k == 0 ? th.leatherRed : b[k], new Vector2(34, -48), new Vector2(190, 78), true, 2.2f);
                    var dark = k == 0 || k == 5; var col = k == 4 ? th.brassText : (dark ? th.leatherText : th.paperInk);
                    var t1 = MakeText(b1.transform, "السلام عليكم", th.fontBody, 26, col);
                    var t2 = MakeText(b2.transform, "أهلاً، تفضّل", th.fontBody, 26, k == 0 ? th.ivory : col);
                    break;
                case "entry":
                    Sprite[] e = { th.paper, th.steelPlate, th.leatherRed, th.leatherDark, th.leatherDark };
                    int ek = Mathf.Clamp(it.idx, 0, 4);
                    var strip = Add(parent, e[ek], Vector2.zero, new Vector2(290, 92), true, 2.4f);
                    Add(strip.transform, th.avatarDefault, new Vector2(100, 0), new Vector2(48, 48));
                    var et = MakeText(strip.transform, "أبو سالم دخل الروم", th.fontBody, 22, ek == 0 ? th.paperInk : th.leatherText);
                    et.GetComponent<RectTransform>().offsetMax = new Vector2(-70, 0);
                    break;
            }
        }
    }
}
