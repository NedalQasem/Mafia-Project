using System.IO;
using System.Text;
using MafiaNight.Data.Player;
using MafiaNight.Data.Store;
using UnityEditor;
using UnityEngine;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// Creates Resources/Config/StoreCatalog.asset from the mockup's items (once), and exports the PlayFab catalog JSON.
    /// After the asset exists, designers edit it in the Inspector: new tabs, items, ads and rank sets need no code.
    /// </summary>
    public static class StoreCatalogBuilder
    {
        public const string AssetPath = "Assets/_Project/Resources/Config/StoreCatalog.asset";

        [MenuItem("Mafia/Store/Create Catalog (if missing)")]
        public static void CreateIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<StoreCatalog>(AssetPath) != null) { Debug.Log("StoreCatalog already exists - not touched."); return; }
            Build();
        }

        [MenuItem("Mafia/Store/Reset Catalog To Mockup Defaults")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var cat = AssetDatabase.LoadAssetAtPath<StoreCatalog>(AssetPath);
            if (cat == null) { cat = ScriptableObject.CreateInstance<StoreCatalog>(); AssetDatabase.CreateAsset(cat, AssetPath); }
            cat.categories.Clear(); cat.items.Clear(); cat.ads.Clear(); cat.rankSets.Clear();

            // ---- tabs, in display order
            Cat(cat, "rank_sets", StoreCategoryKind.RankSets, "حصري الرتب", "حصري الرتب", "كل رتبة لها طقم خاص، يُكسب بالترقية ولا يُشترى", 0, 3, false);
            Cat(cat, "frames", StoreCategoryKind.Items, "الإطارات", "إطارات الصورة", "ترافقك في كل غرفة", 1, 3, true);
            Cat(cat, "room_bg", StoreCategoryKind.Items, "خلفية الروم", "خلفية الروم", "شكل المكان الذي تجلسون فيه", 2, 3, true);
            Cat(cat, "chat_bg", StoreCategoryKind.Items, "خلفية الدردشة", "خلفيات الدردشة", "لون مجلسك", 3, 3, true);
            Cat(cat, "bubbles", StoreCategoryKind.Items, "فقاعات الدردشة", "فقاعات الدردشة", "شكل رسائلك", 4, 2, true);
            Cat(cat, "entry", StoreCategoryKind.Items, "فقاعات الدخول", "فقاعات الدخول", "ما يظهر للجميع عند دخولك الروم", 5, 2, true);
            Cat(cat, "gifts", StoreCategoryKind.Items, "الهدايا", "هدايا", "أرسلها للاعبين بالغرفة", 6, 3, false);
            AddCardCategories(cat);
            Cat(cat, "titles", StoreCategoryKind.Items, "الألقاب", "الألقاب", "تظهر تحت اسمك", 8, 3, true);
            Cat(cat, "coins", StoreCategoryKind.Items, "الكوينز", "شحن الكوينز", "للهدايا والزينة فقط", 9, 2, false);

            // ---- frames
            Item(cat, "frames", "frame_copper", "حلقة نحاس", StoreArtKind.Frame, 1, 250, ItemRarity.Common, "إطار نحاسي بسيط ونظيف.", own: true);
            Item(cat, "frames", "frame_barbed", "سلك شائك", StoreArtKind.Frame, 2, 400, ItemRarity.Rare, "للّي لا يريد أن يقترب منه أحد.");
            Item(cat, "frames", "frame_rope", "حبل مضفور", StoreArtKind.Frame, 3, 400, ItemRarity.Rare, "حبل قديم من ميناء الحارة.");
            Item(cat, "frames", "frame_royal_gold", "ذهب ملكي", StoreArtKind.Frame, 4, 1500, ItemRarity.Legendary, "ذهب خالص بتوهّج دائم.", lockRank: 4);
            Item(cat, "frames", "frame_ember", "الجمر", StoreArtKind.Frame, 5, 1200, ItemRarity.Epic, "حلقة نارية تتوهّج وتخفت.", isNew: true);
            Item(cat, "frames", "frame_turquoise", "فيروز", StoreArtKind.Frame, 6, 900, ItemRarity.Epic, "لمعان فيروزي بارد.");

            // ---- room backgrounds
            Item(cat, "room_bg", "room_backroom", "الغرفة الخلفية", StoreArtKind.RoomThumb, 0, 0, ItemRarity.Common, "الغرفة الافتراضية: مصباح واحد وطاولة.", own: true, tint: 0x6a4a38);
            Item(cat, "room_bg", "room_cafe", "مقهى القهوة", StoreArtKind.RoomThumb, 0, 900, ItemRarity.Rare, "طاولتكم في زاوية مقهى قديم.", tint: 0xa8703a);
            Item(cat, "room_bg", "room_rooftop", "سطح المبنى", StoreArtKind.RoomThumb, 0, 1200, ItemRarity.Epic, "سماء مفتوحة وأضواء المدينة تحتكم.", tint: 0x3a4a7a);
            Item(cat, "room_bg", "room_port", "الميناء", StoreArtKind.RoomThumb, 0, 1500, ItemRarity.Epic, "ماء أسود وصناديق ورياح باردة.", lockRank: 4, tint: 0x2a5a7a);
            Item(cat, "room_bg", "room_cellar", "القبو", StoreArtKind.RoomThumb, 0, 2200, ItemRarity.Legendary, "جدران طوب وضوء خافت.", lockRank: 6, tint: 0x8a3a2a);

            // ---- chat backgrounds
            Item(cat, "chat_bg", "chat_backroom", "الغرفة الخلفية", StoreArtKind.ChatBackground, 0, 600, ItemRarity.Rare, "خلفية دردشة بمصباح دافئ.", tint: 0x6a4a38);
            Item(cat, "chat_bg", "chat_rain", "رصيف المطر", StoreArtKind.ChatBackground, 0, 600, ItemRarity.Rare, "مطر ليلي على رصيف قديم.", tint: 0x3a5068);
            Item(cat, "chat_bg", "chat_hideout", "المخبأ", StoreArtKind.ChatBackground, 0, 450, ItemRarity.Common, "جدار طوب وظلال.", own: true, tint: 0x8a4a3a);
            Item(cat, "chat_bg", "chat_brass_hall", "مجلس النحاس", StoreArtKind.ChatBackground, 0, 1800, ItemRarity.Legendary, "مجلس فخم بإضاءة نحاسية.", lockRank: 5, tint: 0xb8883a);
            Item(cat, "chat_bg", "chat_port", "الميناء", StoreArtKind.ChatBackground, 0, 900, ItemRarity.Epic, "سفن ومياه سوداء بالليل.", isNew: true, tint: 0x2a5a7a);
            Item(cat, "chat_bg", "chat_balcony", "الشرفة", StoreArtKind.ChatBackground, 0, 900, ItemRarity.Epic, "قمر فوق مدينة نائمة.", tint: 0x6a3a6a);

            // ---- chat bubbles
            Item(cat, "bubbles", "bubble_leather", "جلد مخيّط", StoreArtKind.ChatBubble, 0, 0, ItemRarity.Common, "فقاعة جلد بخياطة منقّطة.", own: true);
            Item(cat, "bubbles", "bubble_note", "ورقة ملاحظة", StoreArtKind.ChatBubble, 1, 400, ItemRarity.Rare, "رسائلك تبدو مكتوبة على ورقة.");
            Item(cat, "bubbles", "bubble_ticket", "تذكرة ممزقة", StoreArtKind.ChatBubble, 2, 500, ItemRarity.Rare, "حافة ممزقة وثقبان على الجانبين.");
            Item(cat, "bubbles", "bubble_typewriter", "آلة كاتبة", StoreArtKind.ChatBubble, 3, 600, ItemRarity.Rare, "حروف كأنها طُبعت للتو.");
            Item(cat, "bubbles", "bubble_brass", "لوحة نحاس", StoreArtKind.ChatBubble, 4, 800, ItemRarity.Epic, "كل رسالة لوحة نحاس محفورة.");
            Item(cat, "bubbles", "bubble_velvet", "مخمل قرمزي", StoreArtKind.ChatBubble, 5, 1400, ItemRarity.Legendary, "مخمل قرمزي بحافة نحاسية.", lockRank: 5);

            // ---- entry strips
            Item(cat, "entry", "entry_quiet", "دخول هادئ", StoreArtKind.EntryStrip, 0, 0, ItemRarity.Common, "شريط ورقي بسيط باسمك.", own: true);
            Item(cat, "entry", "entry_bullet", "ثقب الرصاصة", StoreArtKind.EntryStrip, 1, 700, ItemRarity.Rare, "شريط فولاذ تخترقه رصاصة.");
            Item(cat, "entry", "entry_red_carpet", "السجادة الحمراء", StoreArtKind.EntryStrip, 2, 900, ItemRarity.Epic, "سجادة قرمزية تُفرش لك.");
            Item(cat, "entry", "entry_headlights", "المصابيح", StoreArtKind.EntryStrip, 3, 1200, ItemRarity.Epic, "مصباحا سيارة يكشفان اسمك.", isNew: true);
            Item(cat, "entry", "entry_door_shadow", "ظل الباب", StoreArtKind.EntryStrip, 4, 1500, ItemRarity.Legendary, "ظلّك على الباب وضوء من الداخل.", lockRank: 6);

            // ---- gifts (used up when sent, so never "owned")
            Item(cat, "gifts", "gift_pocket_watch", "ساعة الجيب", StoreArtKind.Gift, 0, 300, ItemRarity.Common, "هدية للّي ينتظر دوره.");
            Item(cat, "gifts", "gift_golden_bullet", "رصاصة ذهبية", StoreArtKind.Gift, 1, 500, ItemRarity.Rare, "للّي أصاب الهدف.");
            Item(cat, "gifts", "gift_dallah", "الدلّة", StoreArtKind.Gift, 2, 200, ItemRarity.Common, "قهوة على حساب الطاولة.");
            Item(cat, "gifts", "gift_key", "مفتاح الحارة", StoreArtKind.Gift, 3, 350, ItemRarity.Rare, "أنت من العائلة.");
            Item(cat, "gifts", "gift_rose", "وردة حمراء", StoreArtKind.Gift, 4, 800, ItemRarity.Epic, "بلا كلام.");
            Item(cat, "gifts", "gift_crown", "التاج", StoreArtKind.Gift, 5, 5000, ItemRarity.Legendary, "هدية الكبار فقط.", lockRank: 7);

            // ---- role cards: frame, background, motion and back (Docs/mockups/role-cards.html, section 3)
            AddCardItems(cat);

            // ---- titles
            Item(cat, "titles", "title_boss", "كبير الحارة", StoreArtKind.Title, 0, 900, ItemRarity.Legendary, "لقب بلوحة نحاس.", lockRank: 4, title: "كبير الحارة");
            Item(cat, "titles", "title_word", "صاحب كلمة", StoreArtKind.Title, 1, 300, ItemRarity.Common, "لقب بلوحة جلد.", own: true, title: "صاحب كلمة");
            Item(cat, "titles", "title_suspect", "مشبوه", StoreArtKind.Title, 2, 300, ItemRarity.Common, "لقب بورقة مدبّسة.", title: "مشبوه");
            Item(cat, "titles", "title_professor", "الأستاذ", StoreArtKind.Title, 3, 1100, ItemRarity.Epic, "لقب بإطار نحاسي على مخمل.", title: "الأستاذ");

            // ---- coin packs (real money, IAP comes later)
            Pack(cat, "pack_small", "حفنة كوينز", 1, ItemRarity.Common, "٠٫٩٩ $", 500, "٥٠٠ كوينز للهدايا والزينة فقط.");
            Pack(cat, "pack_bag", "كيس كوينز", 2, ItemRarity.Rare, "١٫٩٩ $", 1200, "١٬٢٠٠ كوينز للهدايا والزينة فقط.");
            Pack(cat, "pack_chest", "صندوق كوينز", 3, ItemRarity.Epic, "٤٫٩٩ $", 3500, "٣٬٥٠٠ كوينز للهدايا والزينة فقط.");
            Pack(cat, "pack_vault", "خزنة كوينز", 4, ItemRarity.Legendary, "١١٫٩٩ $", 10000, "١٠٬٠٠٠ كوينز للهدايا والزينة فقط.");

            // ---- newspaper ads
            cat.ads.Add(new StoreAdDef { id = "ad_ember", mast = "عرض الليلة فقط", headline = "إطار الجمر\nبنصف الثمن تقريباً", body = "حلقة نارية تتوهج حول صورتك.", stamp = "−٣٠٪", itemId = "frame_ember", offerPrice = 840, countdownSeconds = 2 * 3600 + 14 * 60 + 33 });
            cat.ads.Add(new StoreAdDef { id = "ad_chat_port", mast = "وصل حديثاً", headline = "خلفية الميناء\nسفن وليل أسود", body = "غيّر لون مجلسك بخلفية جديدة.", stamp = "جديد", itemId = "chat_port" });
            cat.ads.Add(new StoreAdDef { id = "ad_golden_bullet", mast = "هدية الأسبوع", headline = "رصاصة ذهبية\nلمن أصاب الهدف", body = "أرسلها للاعب بالغرفة.", itemId = "gift_golden_bullet" });
            cat.ads.Add(new StoreAdDef { id = "ad_chest", mast = "عرض الكوينز", headline = "صندوق كوينز\nبسعر الحفنة", body = "٣٬٥٠٠ كوينز للهدايا والزينة فقط.", stamp = "+٣٠٪", itemId = "pack_chest" });

            // ---- rank sets
            foreach (var r in RankSets.Defaults)
                cat.rankSets.Add(new RankSetDef { rankNameAr = r.name, m1 = r.m1, m2 = r.m2, e = r.e, itemNames = (string[])r.items.Clone() });

            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            Debug.Log($"StoreCatalog written: {cat.categories.Count} tabs, {cat.items.Count} items, {cat.ads.Count} ads, {cat.rankSets.Count} rank sets");
        }

        // ------------------------------------------------------------------------------------------------------------ role cards

        /// <summary>The four shelves of the role cards (the mockup's tabs): frames, backgrounds, motions, backs. Every one equips one item at a time.</summary>
        private static void AddCardCategories(StoreCatalog cat)
        {
            Cat(cat, "card_frames", StoreCategoryKind.Items, "إطارات الكروت", "إطارات الكروت", "حول كرت دورك، وكل اللاعبين بيشوفوه", 7, 3, true);
            Cat(cat, "card_bgs", StoreCategoryKind.Items, "خلفيات الكروت", "خلفيات الكروت", "خلف صورة دورك على الكرت", 7, 3, true);
            Cat(cat, "card_motions", StoreCategoryKind.Items, "حركات الكروت", "حركات الكروت", "حركة خفيفة فوق الكرت", 7, 3, true);
            Cat(cat, "card_backs", StoreCategoryKind.Items, "ظهر الكرت", "ظهور الكروت", "اللي بيشوفه الكل وكرتك مقلوب (دورك سري)", 7, 3, true);
        }

        /// <summary>The items of the card shelves with the prices and rarities of the approved mockup (Coins for the rare ones, Gems for the epic ones; the first of each is free and worn from the start).</summary>
        private static void AddCardItems(StoreCatalog cat)
        {
            // id, shelf, name, art, layer, coins, gems, rarity, description
            Layer(cat, "card_frames", "cframe_brass", "نحاسي", StoreArtKind.CardFrame, "brass", 0, 0, ItemRarity.Common, "إطار نحاسي بسيط، مجاني للكل.", own: true);
            Layer(cat, "card_frames", "cframe_silver", "فضي", StoreArtKind.CardFrame, "silver", 3500, 0, ItemRarity.Rare, "إطار فضي لامع.");
            Layer(cat, "card_frames", "cframe_gold", "ذهبي", StoreArtKind.CardFrame, "gold", 6000, 0, ItemRarity.Rare, "إطار ذهبي ملكي.");
            Layer(cat, "card_frames", "cframe_black", "أسود", StoreArtKind.CardFrame, "black", 0, 160, ItemRarity.Epic, "إطار أسود بحواف ذهبية.");

            Layer(cat, "card_bgs", "cbg_leather", "جلد", StoreArtKind.CardBackground, "leather", 0, 0, ItemRarity.Common, "خلفية جلد، مجانية للكل.", own: true);
            Layer(cat, "card_bgs", "cbg_velvet", "قطيفة قرمزية", StoreArtKind.CardBackground, "velvet", 4000, 0, ItemRarity.Rare, "قطيفة حمراء غامقة.");
            Layer(cat, "card_bgs", "cbg_steel", "فولاذ", StoreArtKind.CardBackground, "steel", 3000, 0, ItemRarity.Rare, "فولاذ بارد.");
            Layer(cat, "card_bgs", "cbg_carbon", "كربون", StoreArtKind.CardBackground, "carbon", 0, 90, ItemRarity.Epic, "أسود كربوني.");
            Layer(cat, "card_bgs", "cbg_night", "ليل", StoreArtKind.CardBackground, "night", 0, 110, ItemRarity.Epic, "ليل أزرق عميق.");
            Layer(cat, "card_bgs", "cbg_fire", "نار", StoreArtKind.CardBackground, "fire", 0, 180, ItemRarity.Epic, "لهب من تحت.");

            Layer(cat, "card_motions", "cmo_none", "بدون", StoreArtKind.CardMotion, "none", 0, 0, ItemRarity.Common, "كرت ثابت، مجاني للكل.", own: true);
            Layer(cat, "card_motions", "cmo_shine", "لمعان", StoreArtKind.CardMotion, "shine", 5000, 0, ItemRarity.Rare, "لمعة تعدّي على الكرت.");
            Layer(cat, "card_motions", "cmo_smoke", "دخان", StoreArtKind.CardMotion, "smoke", 0, 120, ItemRarity.Epic, "دخان خفيف.");
            Layer(cat, "card_motions", "cmo_embers", "جمر متطاير", StoreArtKind.CardMotion, "embers", 0, 200, ItemRarity.Epic, "جمر يطير فوق الكرت.");

            Layer(cat, "card_backs", "cback_diamond", "ألماسة", StoreArtKind.CardBack, "diamond", 0, 0, ItemRarity.Common, "ظهر الكرت الأساسي، مجاني للكل.", own: true);
            Layer(cat, "card_backs", "cback_lattice", "مشبّك", StoreArtKind.CardBack, "lattice", 3000, 0, ItemRarity.Rare, "نقش مشبّك دقيق.");
            Layer(cat, "card_backs", "cback_rose", "وردة", StoreArtKind.CardBack, "rose", 4500, 0, ItemRarity.Rare, "وردة بنقش ذهبي.");
            Layer(cat, "card_backs", "cback_pistols", "مسدسان متقاطعان", StoreArtKind.CardBack, "pistols", 0, 100, ItemRarity.Epic, "مسدسان متقاطعان.");
            Layer(cat, "card_backs", "cback_skull", "جمجمة بقبعة", StoreArtKind.CardBack, "skull", 0, 140, ItemRarity.Epic, "جمجمة بقبعة مافيا.");
        }

        private static void Layer(StoreCatalog c, string shelf, string id, string name, StoreArtKind art, string layer, int coins, int gems, ItemRarity r, string desc, bool own = false)
            => c.items.Add(new StoreItemDef { id = id, categoryId = shelf, nameAr = name, descAr = desc, artKind = art, layerId = layer, price = coins, gemPrice = gems, rarity = r, ownedByDefault = own });

        /// <summary>Puts the four card shelves into the existing catalog asset without touching anything else (and drops the old "table cards" shelf they replace).</summary>
        [MenuItem("Mafia/Store/Add Role Card Shelves")]
        public static void AddCardShelves()
        {
            var cat = AssetDatabase.LoadAssetAtPath<StoreCatalog>(AssetPath);
            if (cat == null) { Debug.LogError("No StoreCatalog asset yet."); return; }
            cat.categories.RemoveAll(c => c.id == "cards" || c.id.StartsWith("card_"));
            cat.items.RemoveAll(i => i.categoryId == "cards" || i.categoryId.StartsWith("card_"));
            int at = cat.categories.FindIndex(c => c.id == "gifts");
            var tmp = ScriptableObject.CreateInstance<StoreCatalog>();
            AddCardCategories(tmp); AddCardItems(tmp);
            cat.categories.InsertRange(at >= 0 ? at + 1 : cat.categories.Count, tmp.categories);
            cat.items.AddRange(tmp.items);
            Object.DestroyImmediate(tmp);
            EditorUtility.SetDirty(cat); AssetDatabase.SaveAssets();
            Debug.Log($"Role card shelves added: {cat.categories.Count} tabs, {cat.items.Count} items");
        }

        private static Color Hex(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 1f);

        private static void Cat(StoreCatalog c, string id, StoreCategoryKind kind, string tab, string head, string hint, int icon, int perRow, bool equippable)
            => c.categories.Add(new StoreCategoryDef { id = id, kind = kind, tabName = tab, headTitle = head, hint = hint, builtInIcon = icon, perRow = perRow, equippable = equippable });

        private static void Item(StoreCatalog c, string cat, string id, string name, StoreArtKind art, int variant, int price, ItemRarity r, string desc,
            bool own = false, bool isNew = false, int lockRank = 0, int tint = 0xFFFFFF, string title = null)
            => c.items.Add(new StoreItemDef
            {
                id = id, categoryId = cat, nameAr = name, descAr = desc, artKind = art, variant = variant, price = price, rarity = r,
                ownedByDefault = own, isNew = isNew, lockRank = lockRank, tint = Hex(tint), titleText = title,
            });

        private static void Pack(StoreCatalog c, string id, string name, int variant, ItemRarity r, string priceText, int coins, string desc)
            => c.items.Add(new StoreItemDef
            {
                id = id, categoryId = "coins", nameAr = name, descAr = desc, artKind = StoreArtKind.CoinPack, variant = variant, rarity = r,
                priceText = priceText, coinsGranted = coins, productId = id,
            });

        // ------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Writes a PlayFab legacy catalog file (Game Manager → Economy → Catalogs → Upload). Items everybody owns from the start
        /// and real-money packs are left out: they are not bought with Coins.
        /// </summary>
        [MenuItem("Mafia/Store/Export PlayFab Catalog JSON")]
        public static void ExportPlayFab()
        {
            var cat = AssetDatabase.LoadAssetAtPath<StoreCatalog>(AssetPath);
            if (cat == null) { Debug.LogError("No StoreCatalog asset yet - run Mafia/Store/Create Catalog first."); return; }
            var sb = new StringBuilder();
            sb.Append("{\n  \"CatalogVersion\": \"Main\",\n  \"Catalog\": [");
            bool first = true;
            foreach (var it in cat.items)
            {
                if (it.IsPack || it.ownedByDefault) continue;
                var def = cat.GetCategory(it.categoryId);
                bool consumable = def != null && !def.equippable;
                if (!first) sb.Append(",");
                first = false;
                sb.Append("\n    {");
                sb.Append($"\"ItemId\": {Q(it.id)}, \"ItemClass\": {Q(it.categoryId)}, \"CatalogVersion\": \"Main\", ");
                sb.Append($"\"DisplayName\": {Q(it.nameAr)}, \"Description\": {Q(it.descAr)}, ");
                sb.Append(it.IsGem ? $"\"VirtualCurrencyPrices\": {{\"GM\": {it.gemPrice}}}, \"RealCurrencyPrices\": {{}}, " : $"\"VirtualCurrencyPrices\": {{\"CO\": {it.price}}}, \"RealCurrencyPrices\": {{}}, ");
                sb.Append($"\"Tags\": [{Q(it.categoryId)}, {Q(it.rarity.ToString().ToLowerInvariant())}], \"CustomData\": {Q("{\"lockRank\":" + it.lockRank + "}")}, ");
                sb.Append(consumable ? "\"IsStackable\": true, \"Consumable\": {\"UsageCount\": 1}, " : "\"IsStackable\": false, ");
                sb.Append("\"IsTradable\": false, \"IsLimitedEdition\": false, \"InitialLimitedEditionCount\": 0, \"CanBecomeCharacter\": false}");
            }
            sb.Append("\n  ]\n}\n");
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "playfab-catalog"));
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "catalog.json");
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("PlayFab catalog written to " + file);
            EditorUtility.RevealInFinder(file);
        }

        private static string Q(string s)
        {
            if (s == null) return "\"\"";
            var b = new StringBuilder("\"");
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    default: b.Append(ch); break;
                }
            }
            return b.Append('"').ToString();
        }
    }
}
