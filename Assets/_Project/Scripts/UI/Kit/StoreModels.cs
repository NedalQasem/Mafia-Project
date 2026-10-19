using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    public enum Rarity { Common, Rare, Epic, Legendary }

    [Serializable]
    public class StoreItem
    {
        public string id;                 // stable store ID, e.g. "c0_2" (category_index)
        public string name, desc;
        public int price;
        public string priceText;          // real-money packs
        public Rarity rarity;
        public bool owned, isNew;
        public int lockRank;              // 1-based rank needed, 0 = none
        public string art;                // ring | thumb | gift | card | plate | pack | bubble | entry
        public int idx;
        public Color c1 = Color.white;
        public string text;
    }

    public static class StoreData
    {
        public static readonly string[] CategoryNames = { "الإطارات", "خلفية الروم", "خلفية الدردشة", "فقاعات الدردشة", "فقاعات الدخول", "الهدايا", "أوراق الطاولة", "الألقاب", "الكوينز", "حصري الرتب" };
        /// <summary>Order of the drawers in the tab row (the rank-exclusive one comes first) and the icon of each category id.</summary>
        public static readonly int[] TabOrder = { 9, 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        public static readonly int[] IconOf = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 };
        /// <summary>Shelf heading text per category (HTML HEAD): the title is bold gold, the hint small and grey.</summary>
        public static readonly string[] HeadTitles = { "إطارات الصورة", "خلفية الروم", "خلفيات الدردشة", "فقاعات الدردشة", "فقاعات الدخول", "هدايا", "أوراق الطاولة", "الألقاب", "شحن الكوينز", "حصري الرتب" };
        public static readonly string[] CategoryHints = { "ترافقك في كل غرفة", "شكل المكان الذي تجلسون فيه", "لون مجلسك", "شكل رسائلك", "ما يظهر للجميع عند دخولك الروم", "أرسلها للاعبين بالغرفة", "شكل ظهر الورق", "تظهر تحت اسمك", "للهدايا والزينة فقط", "كل رتبة لها طقم خاص، يُكسب بالترقية ولا يُشترى" };

        private static StoreItem I(string name, string art, int idx, int price, Rarity r, string desc, bool owned = false, bool isNew = false, int lockRank = 0, Color? c = null, string text = null)
            => new StoreItem { name = name, art = art, idx = idx, price = price, rarity = r, desc = desc, owned = owned, isNew = isNew, lockRank = lockRank, c1 = c ?? Color.white, text = text };

        private static Color H(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 1f);

        public static List<StoreItem> Items(int category)
        {
            if (category == 9)
            {
                var all = new List<StoreItem>();
                for (int c = 0; c < 8; c++) foreach (var it in Items(c)) if (it.lockRank > 0) all.Add(it);
                all.Sort((a, b) => a.lockRank.CompareTo(b.lockRank));
                return all;
            }
            var list = Raw(category);
            for (int i = 0; i < list.Count; i++) list[i].id = "c" + category + "_" + i;
            return list;
        }

        private static List<StoreItem> Raw(int category)
        {
            switch (category)
            {
                case 0: return new List<StoreItem> {
                    I("حلقة نحاس", "ring", 1, 250, Rarity.Common, "إطار نحاسي بسيط ونظيف.", owned: true),
                    I("سلك شائك", "ring", 2, 400, Rarity.Rare, "للّي لا يريد أن يقترب منه أحد."),
                    I("حبل مضفور", "ring", 3, 400, Rarity.Rare, "حبل قديم من ميناء الحارة."),
                    I("ذهب ملكي", "ring", 4, 1500, Rarity.Legendary, "ذهب خالص بتوهّج دائم.", lockRank: 4),
                    I("الجمر", "ring", 5, 1200, Rarity.Epic, "حلقة نارية تتوهّج وتخفت.", isNew: true),
                    I("فيروز", "ring", 6, 900, Rarity.Epic, "لمعان فيروزي بارد.") };
                case 1: return new List<StoreItem> {
                    I("الغرفة الخلفية", "thumb", 0, 0, Rarity.Common, "الغرفة الافتراضية: مصباح واحد وطاولة.", owned: true, c: H(0x6a4a38)),
                    I("مقهى القهوة", "thumb", 0, 900, Rarity.Rare, "طاولتكم في زاوية مقهى قديم.", c: H(0xa8703a)),
                    I("سطح المبنى", "thumb", 0, 1200, Rarity.Epic, "سماء مفتوحة وأضواء المدينة تحتكم.", c: H(0x3a4a7a)),
                    I("الميناء", "thumb", 0, 1500, Rarity.Epic, "ماء أسود وصناديق ورياح باردة.", lockRank: 4, c: H(0x2a5a7a)),
                    I("القبو", "thumb", 0, 2200, Rarity.Legendary, "جدران طوب وضوء خافت.", lockRank: 6, c: H(0x8a3a2a)) };
                case 2: return new List<StoreItem> {
                    I("الغرفة الخلفية", "thumb", 0, 600, Rarity.Rare, "خلفية دردشة بمصباح دافئ.", c: H(0x6a4a38)),
                    I("رصيف المطر", "thumb", 0, 600, Rarity.Rare, "مطر ليلي على رصيف قديم.", c: H(0x3a5068)),
                    I("المخبأ", "thumb", 0, 450, Rarity.Common, "جدار طوب وظلال.", owned: true, c: H(0x8a4a3a)),
                    I("مجلس النحاس", "thumb", 0, 1800, Rarity.Legendary, "مجلس فخم بإضاءة نحاسية.", lockRank: 5, c: H(0xb8883a)),
                    I("الميناء", "thumb", 0, 900, Rarity.Epic, "سفن ومياه سوداء بالليل.", isNew: true, c: H(0x2a5a7a)),
                    I("الشرفة", "thumb", 0, 900, Rarity.Epic, "قمر فوق مدينة نائمة.", c: H(0x6a3a6a)) };
                case 3: return new List<StoreItem> {
                    I("جلد مخيّط", "bubble", 0, 0, Rarity.Common, "فقاعة جلد بخياطة منقّطة.", owned: true),
                    I("ورقة ملاحظة", "bubble", 1, 400, Rarity.Rare, "رسائلك تبدو مكتوبة على ورقة."),
                    I("تذكرة ممزقة", "bubble", 2, 500, Rarity.Rare, "حافة ممزقة وثقبان على الجانبين."),
                    I("آلة كاتبة", "bubble", 3, 600, Rarity.Rare, "حروف كأنها طُبعت للتو."),
                    I("لوحة نحاس", "bubble", 4, 800, Rarity.Epic, "كل رسالة لوحة نحاس محفورة."),
                    I("مخمل قرمزي", "bubble", 5, 1400, Rarity.Legendary, "مخمل قرمزي بحافة نحاسية.", lockRank: 5) };
                case 4: return new List<StoreItem> {
                    I("دخول هادئ", "entry", 0, 0, Rarity.Common, "شريط ورقي بسيط باسمك.", owned: true),
                    I("ثقب الرصاصة", "entry", 1, 700, Rarity.Rare, "شريط فولاذ تخترقه رصاصة."),
                    I("السجادة الحمراء", "entry", 2, 900, Rarity.Epic, "سجادة قرمزية تُفرش لك."),
                    I("المصابيح", "entry", 3, 1200, Rarity.Epic, "مصباحا سيارة يكشفان اسمك.", isNew: true),
                    I("ظل الباب", "entry", 4, 1500, Rarity.Legendary, "ظلّك على الباب وضوء من الداخل.", lockRank: 6) };
                case 5: return new List<StoreItem> {
                    I("ساعة الجيب", "gift", 0, 300, Rarity.Common, "هدية للّي ينتظر دوره."),
                    I("رصاصة ذهبية", "gift", 1, 500, Rarity.Rare, "للّي أصاب الهدف."),
                    I("الدلّة", "gift", 2, 200, Rarity.Common, "قهوة على حساب الطاولة."),
                    I("مفتاح الحارة", "gift", 3, 350, Rarity.Rare, "أنت من العائلة."),
                    I("وردة حمراء", "gift", 4, 800, Rarity.Epic, "بلا كلام."),
                    I("التاج", "gift", 5, 5000, Rarity.Legendary, "هدية الكبار فقط.", lockRank: 7) };
                case 6: return new List<StoreItem> {
                    I("ظهر قرمزي", "card", 0, 350, Rarity.Common, "ورق بظهر قرمزي مزخرف.", owned: true, c: H(0xc0323a)),
                    I("ليل أزرق", "card", 0, 500, Rarity.Rare, "خطوط رفيعة على أزرق داكن.", c: H(0x3a5a8a)),
                    I("أخضر النادي", "card", 0, 700, Rarity.Epic, "أخضر جوخ بلمسة نحاس.", c: H(0x2f7a52)),
                    I("فحم", "card", 0, 700, Rarity.Epic, "أسود كالليل.", isNew: true, c: H(0x3a3a3c)) };
                case 7: return new List<StoreItem> {
                    I("كبير الحارة", "plate", 0, 900, Rarity.Legendary, "لقب بلوحة نحاس.", lockRank: 4, text: "كبير الحارة"),
                    I("صاحب كلمة", "plate", 1, 300, Rarity.Common, "لقب بلوحة جلد.", owned: true, text: "صاحب كلمة"),
                    I("مشبوه", "plate", 2, 300, Rarity.Common, "لقب بورقة مدبّسة.", text: "مشبوه"),
                    I("الأستاذ", "plate", 3, 1100, Rarity.Epic, "لقب بإطار نحاسي على مخمل.", text: "الأستاذ") };
                default: return new List<StoreItem> {
                    new StoreItem { name = "حفنة كوينز", art = "pack", idx = 1, rarity = Rarity.Common, priceText = "٠٫٩٩ $", desc = "٥٠٠ كوينز للهدايا والزينة فقط." },
                    new StoreItem { name = "كيس كوينز", art = "pack", idx = 2, rarity = Rarity.Rare, priceText = "١٫٩٩ $", desc = "١٬٢٠٠ كوينز للهدايا والزينة فقط." },
                    new StoreItem { name = "صندوق كوينز", art = "pack", idx = 3, rarity = Rarity.Epic, priceText = "٤٫٩٩ $", desc = "٣٬٥٠٠ كوينز للهدايا والزينة فقط." },
                    new StoreItem { name = "خزنة كوينز", art = "pack", idx = 4, rarity = Rarity.Legendary, priceText = "١١٫٩٩ $", desc = "١٠٬٠٠٠ كوينز للهدايا والزينة فقط." } };
            }
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
