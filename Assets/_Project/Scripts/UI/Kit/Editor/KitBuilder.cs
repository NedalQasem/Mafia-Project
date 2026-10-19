using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// Builds the KitTheme asset, the reusable UI prefabs and the UIKitTest scene.
    /// Re-runnable: it overwrites what it created. Menu: Mafia Night / UI Kit / Build Kit + Test Scene.
    /// </summary>
    public static partial class KitBuilder
    {
        private const string Art = "Assets/_Project/Art/UI/Kit/";
        private const string ThemePath = "Assets/_Project/Resources/Kit/KitTheme.asset";
        private const string PrefabDir = "Assets/_Project/Prefabs/UIKit/";
        private const string ScenePath = "Assets/_Project/Scenes/UIKitTest.unity";
        private const string RoomsScenePath = "Assets/_Project/Scenes/UIKitRooms.unity";
        private const string ShellScenePath = "Assets/_Project/Scenes/UIKitShell.unity";

        [MenuItem("Mafia Night/UI Kit/Build Kit + Test Scene")]
        public static void BuildAll()
        {
            KitProceduralTextures.Generate();
            KitProceduralTextures2.Generate();
            KitProceduralTextures3.Generate();
            KitProceduralTextures4.Generate();
            KitProceduralTextures5.Generate();
            KitProceduralTextures6.Generate();
            KitProceduralTextures7.Generate();
            KitProceduralTextures8.Generate();
            KitProceduralTextures9.Generate();
            KitProceduralTextures10.Generate();
            KitProceduralTextures.ConfigureSprites();
            var theme = BuildTheme();
            BuildPrefabs(theme);
            BuildTopBar(theme);
            BuildProfileCard(theme);
            BuildRoomPrefabs(theme);
            BuildCreateRoomPrefabs(theme);
            BuildStore(theme);
            BuildLeaderboards(theme);
            BuildChat(theme);
            BuildSettings(theme);
            BuildLoading(theme);
            BuildScene(theme);
            BuildShellScene(theme);
            BuildRoomsScene(theme);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIKit] built theme, prefabs and test scene");
        }

        // ------------------------------------------------------------------ theme
        private static Sprite Spr(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + n + ".png");

        private static TMP_FontAsset FindFont(string contains)
        {
            foreach (var g in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p).ToLower().Contains(contains.ToLower()))
                    return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
            }
            return null;
        }

        public static KitTheme BuildTheme()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
            var th = AssetDatabase.LoadAssetAtPath<KitTheme>(ThemePath);
            if (th == null) { th = ScriptableObject.CreateInstance<KitTheme>(); AssetDatabase.CreateAsset(th, ThemePath); }
            th.leatherRed = Spr("leather_red"); th.leatherDark = Spr("leather_dark"); th.brassPlate = Spr("brass_plate");
            th.paper = Spr("paper"); th.paperTicket = Spr("paper_ticket"); th.slotDark = Spr("slot_dark");
            th.navBarTile = Spr("navbar_tile"); th.navShadow = Spr("nav_shadow"); th.navSeal = Spr("nav_seal"); th.navSealGlow = Spr("nav_seal_glow"); th.navSpot = Spr("nav_spot"); th.livePill = Spr("live_pill"); th.badgeDot = Spr("badge_dot");
            th.plank = Spr("plank"); th.priceTag = Spr("price_tag"); th.thumbFill = Spr("thumb_fill"); th.thumbBorder = Spr("thumb_border"); th.cardBack = Spr("card_back"); th.glowFloor = Spr("glow_floor"); th.giftWatch = Spr("gift_watch"); th.giftKey = Spr("gift_key"); th.giftRose = Spr("gift_rose"); th.giftCrown = Spr("gift_crown");
            th.knobFace = Spr("knob_face"); th.dialTicks = Spr("dial_ticks"); th.chalkboard = Spr("chalkboard"); th.wallTile = Spr("wall_tile"); th.goldFrame = Spr("gold_frame"); th.iconGift = Spr("icon_gift"); th.iconSend = Spr("icon_send"); th.triUp = Spr("tri_up");
            th.steelPlate = Spr("steel_plate"); th.vipFrame = Spr("vip_frame"); th.tableTop = Spr("table_top"); th.chair = Spr("chair"); th.propCup = Spr("prop_cup"); th.propAshtray = Spr("prop_ashtray"); th.propDeck = Spr("prop_deck"); th.propDallah = Spr("prop_dallah"); th.iconMic = Spr("icon_mic"); th.iconMicOff = Spr("icon_mic_off"); th.hostStar = Spr("host_star"); th.seatDashed = Spr("seat_dashed"); th.dotWhite = Spr("dot_white"); th.talkGlow = Spr("talk_glow");
            th.dotsTile = Spr("dots_tile"); th.avatarDefault = Spr("avatar_default"); th.ribbonRed = Spr("ribbon_red"); th.strapRed = Spr("strap_red"); th.clipMetal = Spr("clip_metal"); th.levelSeal = Spr("level_seal"); th.barcodeTile = Spr("barcode_tile"); th.stampFrame = Spr("stamp_frame");
            th.storeFrames = new Sprite[6]; for (int i = 0; i < 6; i++) th.storeFrames[i] = Spr("frame_f" + (i + 1)); th.catOn = Spr("cat_on"); th.catOff = Spr("cat_off"); { string[] ci = { "rank", "frame", "room", "bg", "bubble", "entry", "gift", "table", "title", "coin" }; th.catIcons = new Sprite[ci.Length]; for (int i = 0; i < ci.Length; i++) th.catIcons[i] = Spr("cat_" + ci[i]); } th.sheetBg = Spr("sheet_bg"); th.sheetHandle = Spr("sheet_handle"); th.btnGold = Spr("btn_gold"); th.btnDark = Spr("btn_dark"); th.chipSheet = Spr("chip_sheet"); th.togOff = Spr("tog_off"); th.togOn = Spr("tog_on"); th.togKnob = Spr("tog_knob"); th.rarGlow = Spr("rar_glow"); th.itemCone = Spr("item_cone"); th.shineBand = Spr("shine_band"); th.resetPill = Spr("reset_pill"); th.wallBg = Spr("wall_bg"); th.wallLines = Spr("wall_lines"); th.wallVignette = Spr("wall_vignette"); th.fedSil = Spr("fed_sil"); th.picBg = Spr("pic_bg"); th.plaqueGold = Spr("plaque_gold"); th.lampCap = Spr("lamp_cap"); th.lampCone = Spr("lamp_cone"); th.hairGrad = Spr("hair_grad"); th.recCard = Spr("rec_card"); th.mineBar = Spr("mine_bar"); th.barRed = Spr("bar_red"); th.dashH = Spr("dash_h"); th.doorD1 = Spr("door_d1"); th.doorD2 = Spr("door_d2"); th.doorD3 = Spr("door_d3"); th.doorRing = Spr("door_ring"); th.boardDay = Spr("board_day"); th.chipOff = Spr("chip_pill"); th.chipOn = Spr("chip_pill_on"); th.rowBg = Spr("row_bg"); th.iconDay = Spr("icon_day"); th.iconWeek = Spr("icon_week"); th.iconWall = Spr("icon_wall"); th.xpRing = Spr("xp_ring"); th.medalDisc = Spr("medal_disc"); th.coin = Spr("coin"); th.bulletOn = Spr("bullet_on"); th.bulletOff = Spr("bullet_off");
            th.iconWhatsapp = Spr("icon_whatsapp"); th.iconLink = Spr("icon_link"); th.iconCopy = Spr("icon_copy"); th.segOn = Spr("seg_on"); th.segBar = Spr("seg_bar"); th.typeOn = Spr("type_on"); th.typeOff = Spr("type_off"); th.iconGlobe = Spr("icon_globe"); th.iconKey = Spr("icon_key"); th.iconCrown = Spr("icon_crown"); th.iconLock = Spr("icon_lock");
            for (int i = 0; i < 8; i++) { th.ringByRank[i] = Spr("ring_r" + (i + 1)); th.medalByRank[i] = Spr("medal_r" + (i + 1)); }
            th.navIcons = new[] { Spr("nav_shop"), Spr("nav_family"), Spr("nav_rooms"), Spr("nav_ranks"), Spr("nav_settings") };
            th.fontBody = FindFont("cairo") ?? th.fontBody;
            th.fontHeading = FindFont("cairo") ?? th.fontHeading;
            th.fontCalligraphy = FindFont("amiri") ?? FindFont("cairo") ?? th.fontCalligraphy;
            EditorUtility.SetDirty(th);
            return th;
        }

        // ------------------------------------------------------------------ helpers
        private static TMP_FontAsset headFont;

        private static GameObject Go(string name, Transform parent)
        {
            var g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            return g;
        }

        private static RectTransform Rt(GameObject g) => (RectTransform)g.transform;

        private static void Stretch(RectTransform r, float l = 0, float b = 0, float rr = 0, float t = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t);
        }

        private static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        private static Image Img(GameObject g, Sprite s, bool sliced = false, float ppuMul = 1f, bool raycast = false)
        {
            var i = g.AddComponent<Image>();
            i.sprite = s; i.raycastTarget = raycast;
            i.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            i.pixelsPerUnitMultiplier = ppuMul;
            i.preserveAspect = !sliced;
            return i;
        }

        private static TextMeshProUGUI Txt(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool wrap = false)
        {
            var g = Go(name, parent);
            var t = g.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if (font != null && font == headFont) t.fontStyle = FontStyles.Bold;
            t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
            var a = g.AddComponent<ArabicText>();
            a.Wrap = wrap;
            a.Text = text;
            return t;
        }

        private static void SetRef(Object comp, string field, Object value)
        {
            var so = new SerializedObject(comp);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[UIKit] missing field " + field + " on " + comp.GetType().Name); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Save(GameObject root, string name)
        {
            Directory.CreateDirectory(PrefabDir);
            var path = PrefabDir + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ prefabs
        private static GameObject MakeButton(string name, Sprite sprite, Color textColor, string label, TMP_FontAsset font, float textSize = 56)
        {
            var root = new GameObject(name, typeof(RectTransform));
            Rt(root).sizeDelta = new Vector2(480, 168);
            var face = Go("Face", root.transform);
            Stretch(Rt(face));
            var img = Img(face, sprite, true, 1f, true);
            var btn = root.AddComponent<Button>();
            btn.targetGraphic = img; btn.transition = Selectable.Transition.None;
            var lbl = Txt(face.transform, "Label", label, font, textSize, textColor);
            Stretch(Rt(lbl.gameObject), 24, 30, 24, 14);
            var fx = root.AddComponent<KitButtonFx>();
            SetRef(fx, "face", Rt(face));
            return root;
        }

        private static GameObject MakeLabelPlate(string name, Sprite sprite, Color textColor, string label, TMP_FontAsset font, float mul = 2.4f)
        {
            var root = new GameObject(name, typeof(RectTransform));
            Rt(root).sizeDelta = new Vector2(360, 96);
            Img(root, sprite, true, mul);
            var lbl = Txt(root.transform, "Label", label, font, 40, textColor);
            Stretch(Rt(lbl.gameObject), 16, 14, 16, 8);
            return root;
        }

        private static void BuildPrefabs(KitTheme th)
        {
            var body = th.fontBody; var head = th.fontHeading; var calli = th.fontCalligraphy; headFont = head;
            Save(MakeButton("KitButton_Primary", th.leatherRed, th.ivory, "ادخل الطاولة", head), "KitButton_Primary");
            Save(MakeButton("KitButton_Secondary", th.leatherDark, th.leatherText, "طاولتي", head), "KitButton_Secondary");
            Save(MakeButton("KitButton_Brass", th.brassPlate, th.brassText, "ابدأ السهرة", head, 62), "KitButton_Brass");

            Save(MakeLabelPlate("NameLabel_Leather", th.leatherDark, th.leatherText, "أبو سالم", body), "NameLabel_Leather");
            Save(MakeLabelPlate("NameLabel_Brass", th.brassPlate, th.brassText, "الحج حسن", body), "NameLabel_Brass");
            Save(MakeLabelPlate("NameLabel_Paper", th.paper, th.paperInk, "أم سعيد", calli, 2.4f), "NameLabel_Paper");

            // plaque / panel with a brass title
            {
                var root = new GameObject("KitPlaque", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(900, 520);
                Img(root, th.leatherDark, true, 1f, true);
                var title = MakeLabelPlate("Title", th.brassPlate, th.brassText, "مجلس الكبار", head, 2.2f);
                title.transform.SetParent(root.transform, false);
                Place(Rt(title), new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(520, 110));
                var body2 = Txt(root.transform, "Body", "هنا يوضع محتوى النافذة أو القائمة. النص العربي يلتف تلقائياً ويبقى ترتيب الكلمات صحيحاً.", body, 44, th.leatherText, TextAlignmentOptions.TopRight, true);
                Stretch(Rt(body2.gameObject), 70, 70, 70, 120);
                Save(root, "KitPlaque");
            }

            // coin pill
            {
                var root = new GameObject("CoinPill", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(360, 100);
                Img(root, th.slotDark, true, 1.6f);
                var coin = Go("Coin", root.transform);
                Place(Rt(coin), new Vector2(1, 0.5f), new Vector2(-62, 0), new Vector2(76, 76));
                Img(coin, th.coin);
                var val = Txt(root.transform, "Value", "١٬٢٥٠", head, 48, th.brassLight, TextAlignmentOptions.Center);
                Stretch(Rt(val.gameObject), 24, 8, 120, 8);
                var pill = root.AddComponent<CoinPill>();
                SetRef(pill, "label", val.GetComponent<ArabicText>());
                SetRef(pill, "coinIcon", Rt(coin));
                Save(root, "CoinPill");
            }

            // rank medal + pips
            {
                var root = new GameObject("RankMedal", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(240, 380);
                var m = Go("Medal", root.transform);
                Place(Rt(m), new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(240, 240));
                var mi = Img(m, th.medalByRank[3]);
                var tray = Go("Tray", root.transform);
                Place(Rt(tray), new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(190, 118));
                Img(tray, th.slotDark, true, 2.6f);
                var view = root.AddComponent<RankMedalView>();
                var so = new SerializedObject(view);
                so.FindProperty("medal").objectReferenceValue = mi;
                var arr = so.FindProperty("pips"); arr.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    var p = Go("Pip" + (i + 1), tray.transform);
                    Place(Rt(p), new Vector2(0.5f, 0.5f), new Vector2((1 - i) * 52, 0), new Vector2(40, 96));
                    var pi = Img(p, i < 1 ? th.bulletOn : th.bulletOff);
                    arr.GetArrayElementAtIndex(i).objectReferenceValue = pi;
                }
                so.FindProperty("rank").intValue = 3; so.FindProperty("filledPips").intValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                Save(root, "RankMedal");
            }

            // avatar frame
            {
                var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                var root = new GameObject("AvatarFrame", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(180, 180);
                var av = Go("Avatar", root.transform);
                Stretch(Rt(av), 20, 20, 20, 20);
                var ai = Img(av, th.avatarDefault != null ? th.avatarDefault : knob);
                var mask = av.AddComponent<Mask>(); mask.showMaskGraphic = true;
                var ring = Go("Ring", root.transform);
                Stretch(Rt(ring));
                var ri = Img(ring, th.ringByRank[2]);
                var dot = Go("OnlineDot", root.transform);
                Place(Rt(dot), new Vector2(1, 0), new Vector2(-26, 26), new Vector2(34, 34));
                var di = Img(dot, knob); di.color = new Color(0.26f, 0.75f, 0.54f, 1f);
                var fr = root.AddComponent<AvatarFrame>();
                SetRef(fr, "ring", ri); SetRef(fr, "avatar", ai); SetRef(fr, "onlineDot", dot);
                Save(root, "AvatarFrame");
            }

            // bottom nav bar (matches Docs/mockups/footer.html)
            {
                var root = new GameObject("KitNavBar", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(1080, 330);

                var shadow = Go("Shadow", root.transform);
                var sr = Rt(shadow); sr.anchorMin = new Vector2(0, 0); sr.anchorMax = new Vector2(1, 0); sr.pivot = new Vector2(0.5f, 0);
                sr.offsetMin = new Vector2(0, 210); sr.offsetMax = new Vector2(0, 300);
                Img(shadow, th.navShadow).preserveAspect = false;

                var bar = Go("Bar", root.transform);
                var br = Rt(bar); br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0); br.pivot = new Vector2(0.5f, 0);
                br.offsetMin = Vector2.zero; br.offsetMax = new Vector2(0, 210);
                var bi = bar.AddComponent<Image>(); bi.sprite = th.navBarTile; bi.type = Image.Type.Tiled; bi.raycastTarget = true;

                var spot = Go("Spotlight", root.transform);
                Place(Rt(spot), new Vector2(0.5f, 0f), new Vector2(0, 0), new Vector2(300, 230));
                Rt(spot).pivot = new Vector2(0.5f, 0f);
                Img(spot, th.navSpot).preserveAspect = false;

                var nav = root.AddComponent<KitNavBar>();
                string[] labels = { "السوق", "العائلة", "الغرف", "الرتب", "الإعدادات" };
                var so = new SerializedObject(nav);
                var tabs = so.FindProperty("tabs"); tabs.arraySize = 5;
                for (int i = 0; i < 5; i++)
                {
                    var tab = Go("Tab_" + labels[i], root.transform);
                    float x = (2 - i) * 216f;
                    Place(Rt(tab), new Vector2(0.5f, 0f), new Vector2(x, 0), new Vector2(216, 210));
                    Rt(tab).pivot = new Vector2(0.5f, 0f);
                    var tabImg = tab.AddComponent<Image>(); tabImg.color = Color.clear; tabImg.raycastTarget = true;
                    var b = tab.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = tabImg;

                    RectTransform iconRt; Image iconImg; Image glowImg = null; bool seal = i == 2;
                    if (seal)
                    {
                        var glow = Go("Glow", tab.transform);
                        Place(Rt(glow), new Vector2(0.5f, 0f), new Vector2(0, 179), new Vector2(380, 380));
                        glowImg = Img(glow, th.navSealGlow); glowImg.color = new Color(1, 1, 1, 0);
                        var sealGo = Go("Seal", tab.transform);
                        Place(Rt(sealGo), new Vector2(0.5f, 0f), new Vector2(0, 82), new Vector2(194, 194));
                        Rt(sealGo).pivot = new Vector2(0.5f, 0f);
                        iconImg = Img(sealGo, th.navSeal); iconRt = Rt(sealGo);
                        var inner = Go("Icon", sealGo.transform);
                        Place(Rt(inner), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130, 130));
                        Img(inner, th.navIcons[2]);

                        var pill = Go("LivePill", tab.transform);
                        Place(Rt(pill), new Vector2(0.5f, 0f), new Vector2(120, 268), new Vector2(176, 56));
                        Img(pill, th.livePill, true, 1f);
                        var dot = Go("Dot", pill.transform);
                        Place(Rt(dot), new Vector2(0f, 0.5f), new Vector2(34, 0), new Vector2(36, 36));
                        Img(dot, th.badgeDot);
                        var cnt = Txt(pill.transform, "Count", "١٬٢٣٤", th.fontHeading, 30, th.brassLight);
                        Stretch(Rt(cnt.gameObject), 58, 4, 14, 4);
                    }
                    else
                    {
                        var icon = Go("Icon", tab.transform);
                        Place(Rt(icon), new Vector2(0.5f, 0f), new Vector2(0, 118), new Vector2(100, 100));
                        iconImg = Img(icon, th.navIcons[i]); iconRt = Rt(icon);
                        if (i == 1)
                        {
                            var bd = Go("Badge", tab.transform);
                            Place(Rt(bd), new Vector2(0.5f, 0f), new Vector2(46, 170), new Vector2(54, 54));
                            Img(bd, th.badgeDot);
                            var n = Txt(bd.transform, "N", "٣", th.fontHeading, 26, Color.white);
                            Stretch(Rt(n.gameObject), 0, 4, 0, 0);
                        }
                        if (i == 3)
                        {
                            var bd = Go("Dot", tab.transform);
                            Place(Rt(bd), new Vector2(0.5f, 0f), new Vector2(44, 172), new Vector2(40, 40));
                            Img(bd, th.badgeDot);
                        }
                    }
                    var lb = Txt(tab.transform, "Label", labels[i], th.fontHeading, 30, th.ivoryDim);
                    Place(Rt(lb.gameObject), new Vector2(0.5f, 0f), new Vector2(0, seal ? 26 : 30), new Vector2(210, 50));
                    Rt(lb.gameObject).pivot = new Vector2(0.5f, 0f);

                    var el = tabs.GetArrayElementAtIndex(i);
                    el.FindPropertyRelative("button").objectReferenceValue = b;
                    el.FindPropertyRelative("icon").objectReferenceValue = iconRt;
                    el.FindPropertyRelative("iconImage").objectReferenceValue = iconImg;
                    el.FindPropertyRelative("label").objectReferenceValue = lb.GetComponent<ArabicText>();
                    el.FindPropertyRelative("isSeal").boolValue = seal;
                    el.FindPropertyRelative("glow").objectReferenceValue = glowImg;
                }
                so.FindProperty("spotlight").objectReferenceValue = Rt(spot);
                so.ApplyModifiedPropertiesWithoutUndo();
                nav.CaptureRest();
                nav.SnapToSelected();
                EditorUtility.SetDirty(nav);
                Save(root, "KitNavBar");
            }
        }


        // ------------------------------------------------------------------ top bar + profile card
        private static GameObject LoadPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + name + ".prefab");

        private static void PlaceTop(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
        }

        private static void BuildTopBar(KitTheme th)
        {
            var arrow = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
            var paperInkSoft = new Color32(0x6A, 0x5A, 0x3A, 255);
            var root = new GameObject("KitTopBar", typeof(RectTransform));
            var rr = Rt(root); rr.anchorMin = new Vector2(0, 1); rr.anchorMax = new Vector2(1, 1); rr.pivot = new Vector2(0.5f, 1); rr.sizeDelta = new Vector2(0, 270);

            // coin ticket (left, slightly tilted like the mockup)
            var ticket = Go("Ticket", root.transform);
            Place(Rt(ticket), new Vector2(0, 1), new Vector2(32 + 150, -150), new Vector2(300, 118));
            ticket.transform.localRotation = Quaternion.Euler(0, 0, -2f);
            Img(ticket, th.paperTicket, true, 1.6f);
            var coin = Go("Coin", ticket.transform);
            Place(Rt(coin), new Vector2(1, 0.5f), new Vector2(-52, 0), new Vector2(60, 60));
            Img(coin, th.coin);
            var val = Txt(ticket.transform, "Value", "١٬٢٥٠", th.fontHeading, 50, th.paperInk, TextAlignmentOptions.Center);
            Stretch(Rt(val.gameObject), 22, 8, 96, 14);
            var pill = ticket.AddComponent<CoinPill>();
            SetRef(pill, "label", val.GetComponent<ArabicText>());
            SetRef(pill, "coinIcon", Rt(coin));

            // paper tag (right) = the profile button; the avatar sits in a medal whose ring fills with XP
            var tag = Go("Tag", root.transform);
            Place(Rt(tag), new Vector2(1, 1), new Vector2(-(32 + 350), -150), new Vector2(700, 150));
            var tagImg = Img(tag, th.paper, true, 1f, true);
            var btn = tag.AddComponent<Button>(); btn.targetGraphic = tagImg; btn.transition = Selectable.Transition.None;
            var fx = tag.AddComponent<KitButtonFx>(); SetRef(fx, "face", Rt(tag));

            var ribbon = Go("Ribbon", tag.transform);
            Place(Rt(ribbon), new Vector2(1, 1), new Vector2(-250, 6), new Vector2(56, 44));
            Img(ribbon, th.ribbonRed);

            var medal = Go("Medal", tag.transform);
            // layers, inside out: dark disc, XP ring, avatar picture, and on top the store frame (the outer ring of the medal)
            Place(Rt(medal), new Vector2(1, 0.5f), new Vector2(-104, 0), new Vector2(200, 200));
            var discGo = Go("Disc", medal.transform); Place(Rt(discGo), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(158, 158));
            var disc = Img(discGo, th.medalDisc); disc.raycastTarget = false;
            var track = Go("XpTrack", medal.transform); Place(Rt(track), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(152, 152));
            var ti2 = Img(track, th.xpRing); ti2.color = new Color(1f, 1f, 1f, 0.12f); ti2.raycastTarget = false;
            var ringGo = Go("XpRing", medal.transform); Place(Rt(ringGo), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(152, 152));
            var ringImg = Img(ringGo, th.xpRing); ringImg.color = new Color32(0xD2, 0xAD, 0x60, 255); ringImg.raycastTarget = false;
            ringImg.type = Image.Type.Filled; ringImg.fillMethod = Image.FillMethod.Radial360; ringImg.fillOrigin = (int)Image.Origin360.Top; ringImg.fillClockwise = true; ringImg.fillAmount = 0.62f;
            var avGo = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("AvatarFrame"), medal.transform);
            avGo.name = "Avatar";
            Place(Rt(avGo), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 200));
            var avPic = avGo.transform.Find("Avatar");
            if (avPic != null) { var ar = (RectTransform)avPic; ar.offsetMin = new Vector2(48, 48); ar.offsetMax = new Vector2(-48, -48); }
            var lvl = Go("Level", medal.transform);
            Place(Rt(lvl), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(84, 84));
            Img(lvl, th.levelSeal);
            var lvlTxt = Txt(lvl.transform, "N", "م٧", th.fontHeading, 38, th.brassText);
            Stretch(Rt(lvlTxt.gameObject), 0, 3, 0, 0);

            var nm = Txt(tag.transform, "Name", "أبو سالم", th.fontCalligraphy, 62, th.paperInk, TextAlignmentOptions.MidlineRight);
            Place(Rt(nm.gameObject), new Vector2(1, 0.5f), new Vector2(-390, 20), new Vector2(340, 72));
            var ti = Txt(tag.transform, "Title", "صاحب كلمة", th.fontBody, 30, paperInkSoft, TextAlignmentOptions.MidlineRight);
            Place(Rt(ti.gameObject), new Vector2(1, 0.5f), new Vector2(-390, -30), new Vector2(340, 42));

            var chev = Go("Chevron", tag.transform);
            Place(Rt(chev), new Vector2(0, 0.5f), new Vector2(54, 0), new Vector2(40, 40));
            var ci = Img(chev, arrow); ci.color = new Color32(0x8A, 0x1C, 0x24, 255);

            var bar = root.AddComponent<KitTopBar>();
            SetRef(bar, "profileButton", btn);
            SetRef(bar, "avatar", avGo.GetComponent<AvatarFrame>());
            SetRef(bar, "nameLabel", nm.GetComponent<ArabicText>());
            SetRef(bar, "titleLabel", ti.GetComponent<ArabicText>());
            SetRef(bar, "levelLabel", lvlTxt.GetComponent<ArabicText>());
            SetRef(bar, "xpRing", ringImg);
            SetRef(bar, "coins", pill);
            Save(root, "KitTopBar");
        }

        private static void BuildProfileCard(KitTheme th)
        {
            var ink = th.paperInk; var soft = new Color32(0x6A, 0x5A, 0x3A, 255);
            var crim = new Color32(0x8A, 0x1C, 0x24, 255); var green = new Color32(0x1D, 0x4A, 0x3C, 255);
            var root = new GameObject("KitProfileCard", typeof(RectTransform));
            Stretch(Rt(root));
            var group = root.AddComponent<CanvasGroup>();

            var dim = Go("Dim", root.transform); Stretch(Rt(dim));
            var di = dim.AddComponent<Image>(); di.color = new Color(0, 0, 0, 0.84f);
            var dimBtn = dim.AddComponent<Button>(); dimBtn.targetGraphic = di; dimBtn.transition = Selectable.Transition.None;

            var hang = Go("Hang", root.transform);
            PlaceTop(Rt(hang), 0, 0, 700, 1320);

            var strap = Go("Strap", hang.transform); PlaceTop(Rt(strap), 0, 0, 130, 200); Img(strap, th.strapRed);
            var cardGo = Go("Card", hang.transform); PlaceTop(Rt(cardGo), 0, -190, 700, 1000);
            Img(cardGo, th.paper, true, 1f, true);
            var clip = Go("Clip", hang.transform); PlaceTop(Rt(clip), 0, -160, 120, 54); Img(clip, th.clipMetal);

            var front = Go("Front", cardGo.transform); Stretch(Rt(front));
            var slot = Go("Slot", front.transform); PlaceTop(Rt(slot), 0, -54, 150, 30); Img(slot, th.slotDark, true, 3f);

            var hdrR = Txt(front.transform, "HeaderR", "هوية العائلة", th.fontBody, 28, crim, TextAlignmentOptions.MidlineRight);
            PlaceTop(Rt(hdrR.gameObject), 200, -96, 300, 40);
            var num = Txt(front.transform, "CardNo", "رقم ٠٤٢", th.fontBody, 28, crim, TextAlignmentOptions.MidlineLeft);
            PlaceTop(Rt(num.gameObject), -200, -96, 300, 40);
            var line = Go("HeaderLine", front.transform); PlaceTop(Rt(line), 0, -140, 604, 4);
            var lineI = line.AddComponent<Image>(); lineI.color = new Color(0.11f, 0.09f, 0.06f, 0.4f); lineI.raycastTarget = false;

            var avGo = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("AvatarFrame"), front.transform);
            avGo.name = "Avatar"; PlaceTop(Rt(avGo), 215, -170, 230, 230);

            var nm = Txt(front.transform, "Name", "أبو سالم", th.fontCalligraphy, 92, ink, TextAlignmentOptions.MidlineRight);
            PlaceTop(Rt(nm.gameObject), -100, -185, 380, 110);
            var ti = Txt(front.transform, "Title", "صاحب كلمة", th.fontBody, 38, soft, TextAlignmentOptions.MidlineRight);
            PlaceTop(Rt(ti.gameObject), -100, -300, 380, 52);
            var mem = Txt(front.transform, "Member", "عضو منذ شهر", th.fontBody, 30, soft, TextAlignmentOptions.MidlineRight);
            PlaceTop(Rt(mem.gameObject), -100, -352, 380, 44);

            var lvl = Txt(front.transform, "Level", "م٧", th.fontHeading, 40, ink, TextAlignmentOptions.MidlineRight);
            PlaceTop(Rt(lvl.gameObject), 250, -432, 130, 56);
            var xpBg = Go("XpBg", front.transform); PlaceTop(Rt(xpBg), 20, -450, 380, 20);
            var xbi = xpBg.AddComponent<Image>(); xbi.color = new Color(0.11f, 0.09f, 0.06f, 0.22f); xbi.raycastTarget = false;
            var fill = Go("Fill", xpBg.transform); var fr = Rt(fill);
            fr.anchorMin = new Vector2(0.38f, 0); fr.anchorMax = new Vector2(1, 1); fr.offsetMin = fr.offsetMax = Vector2.zero;
            var fi = fill.AddComponent<Image>(); fi.color = crim; fi.raycastTarget = false;
            var xpT = Txt(front.transform, "XpText", "٦٢٠/١٠٠٠", th.fontBody, 28, ink, TextAlignmentOptions.MidlineLeft);
            PlaceTop(Rt(xpT.gameObject), -240, -432, 190, 56);

            var bc = Go("Barcode", front.transform); PlaceTop(Rt(bc), 0, -520, 604, 46);
            var bci = bc.AddComponent<Image>(); bci.sprite = th.barcodeTile; bci.type = Image.Type.Tiled; bci.raycastTarget = false;

            string[] statLabels = { "جولة", "فوز", "نسبة الفوز" };
            var statNums = new ArabicText[3];
            for (int i = 0; i < 3; i++)
            {
                float x = (1 - i) * 205f;
                var box = Go("Stat" + i, front.transform); PlaceTop(Rt(box), x, -600, 190, 156);
                var bi = Img(box, th.stampFrame, true, 1f); bi.color = new Color(0.11f, 0.09f, 0.06f, 0.55f);
                var n = Txt(box.transform, "Number", "٠", th.fontHeading, 70, ink);
                Place(Rt(n.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(180, 84));
                statNums[i] = n.GetComponent<ArabicText>();
                var l = Txt(box.transform, "Label", statLabels[i], th.fontBody, 26, soft);
                Place(Rt(l.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, -42), new Vector2(180, 40));
            }

            string[] stamps = { "مافيا ×٥", "محقق ×٧" };
            for (int i = 0; i < 2; i++)
            {
                var st = Go("Stamp" + i, front.transform);
                PlaceTop(Rt(st), i == 0 ? 130 : -130, -790, 230, 78);
                st.transform.localRotation = Quaternion.Euler(0, 0, i == 0 ? -5f : 4f);
                var si = Img(st, th.stampFrame, true, 1f); si.color = i == 0 ? (Color)crim : green;
                var t = Txt(st.transform, "Text", stamps[i], th.fontCalligraphy, 44, i == 0 ? crim : green);
                Stretch(Rt(t.gameObject), 6, 6, 6, 6);
            }

            string[] acts = { "تعديل البطاقة", "الإطارات" };
            for (int i = 0; i < 2; i++)
            {
                var b = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("KitButton_Secondary"), front.transform);
                b.name = "Action" + i;
                Place(Rt(b), new Vector2(0.5f, 1f), new Vector2(i == 0 ? 160 : -160, -925), new Vector2(480, 168));
                b.transform.localScale = Vector3.one * 0.6f;
                b.GetComponentInChildren<ArabicText>().Text = acts[i];
            }

            
            // ---- back side: roles record, medals, signature
            var back = Go("Back", cardGo.transform); Stretch(Rt(back)); back.SetActive(false);
            var bs = Go("Slot", back.transform); PlaceTop(Rt(bs), 0, -54, 150, 30); Img(bs, th.slotDark, true, 3f);
            var bh1 = Txt(back.transform, "HeaderR", "السجل", th.fontBody, 28, crim, TextAlignmentOptions.MidlineRight); PlaceTop(Rt(bh1.gameObject), 200, -96, 300, 40);
            var bh2 = Txt(back.transform, "HeaderL", "خلف البطاقة", th.fontBody, 28, crim, TextAlignmentOptions.MidlineLeft); PlaceTop(Rt(bh2.gameObject), -200, -96, 300, 40);
            var bl = Go("HeaderLine", back.transform); PlaceTop(Rt(bl), 0, -140, 604, 4);
            var bli = bl.AddComponent<Image>(); bli.color = new Color(0.11f, 0.09f, 0.06f, 0.4f); bli.raycastTarget = false;
            var t1 = Txt(back.transform, "RolesTitle", "الأدوار", th.fontCalligraphy, 60, ink, TextAlignmentOptions.MidlineRight); PlaceTop(Rt(t1.gameObject), 190, -170, 320, 80);
            string[] roleNames = { "مواطن", "مافيا", "محقق", "طبيب" };
            var roleVals = new ArabicText[4];
            for (int i = 0; i < 4; i++)
            {
                float y = -270 - i * 82;
                var rn = Txt(back.transform, "Role" + i, roleNames[i], th.fontBody, 40, ink, TextAlignmentOptions.MidlineRight); PlaceTop(Rt(rn.gameObject), 215, y, 240, 62);
                var rv = Txt(back.transform, "RoleVal" + i, "٠", th.fontHeading, 46, ink, TextAlignmentOptions.MidlineLeft); PlaceTop(Rt(rv.gameObject), -240, y, 120, 62);
                roleVals[i] = rv.GetComponent<ArabicText>();
                var dl = Go("Dots" + i, back.transform); PlaceTop(Rt(dl), -10, y - 30, 330, 6);
                var dli = dl.AddComponent<Image>(); dli.sprite = th.dotsTile; dli.type = Image.Type.Tiled; dli.raycastTarget = false;
            }
            var t2 = Txt(back.transform, "MedalsTitle", "الأوسمة", th.fontCalligraphy, 60, ink, TextAlignmentOptions.MidlineRight); PlaceTop(Rt(t2.gameObject), 190, -620, 320, 80);
            string[] medalNames = { "أول فوز", "ضيف دائم", "١٠ سهرات" };
            for (int i = 0; i < 3; i++)
            {
                var st = Go("Medal" + i, back.transform); PlaceTop(Rt(st), (1 - i) * 205, -725, 190, 78);
                st.transform.localRotation = Quaternion.Euler(0, 0, (i - 1) * -5f);
                var si = Img(st, th.stampFrame, true, 1f); si.color = i == 1 ? (Color)green : crim;
                var mt = Txt(st.transform, "Text", medalNames[i], th.fontCalligraphy, 40, i == 1 ? green : crim); Stretch(Rt(mt.gameObject), 4, 4, 4, 4);
            }
            var sline = Go("SignLine", back.transform); PlaceTop(Rt(sline), 120, -905, 340, 4);
            var sli = sline.AddComponent<Image>(); sli.color = ink; sli.raycastTarget = false;
            var sig = Txt(back.transform, "Signature", "أبو سالم", th.fontCalligraphy, 76, new Color32(0x2A, 0x22, 0x17, 255), TextAlignmentOptions.MidlineRight);
            PlaceTop(Rt(sig.gameObject), 120, -830, 340, 90); sig.transform.localRotation = Quaternion.Euler(0, 0, 3f);

            // flip button under the card
            var fb = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("KitButton_Secondary"), hang.transform);
            fb.name = "FlipButton";
            Place(Rt(fb), new Vector2(0.5f, 1f), new Vector2(0, -1240), new Vector2(480, 168));
            fb.transform.localScale = Vector3.one * 0.5f;
            fb.GetComponentInChildren<ArabicText>().Text = "اقلب البطاقة";

            var card = root.AddComponent<KitProfileCard>();
            SetRef(card, "hang", Rt(hang)); SetRef(card, "group", group); SetRef(card, "dimButton", dimBtn);
            SetRef(card, "avatar", avGo.GetComponent<AvatarFrame>());
            SetRef(card, "nameLabel", nm.GetComponent<ArabicText>()); SetRef(card, "titleLabel", ti.GetComponent<ArabicText>());
            SetRef(card, "levelLabel", lvl.GetComponent<ArabicText>()); SetRef(card, "xpText", xpT.GetComponent<ArabicText>());
            SetRef(card, "memberLabel", mem.GetComponent<ArabicText>()); SetRef(card, "cardNumber", num.GetComponent<ArabicText>());
            SetRef(card, "xpFill", fr);
            SetRef(card, "front", front); SetRef(card, "back", back); SetRef(card, "cardRect", Rt(cardGo)); SetRef(card, "flipButton", fb.GetComponent<Button>());
            { var so2 = new SerializedObject(card); var ra = so2.FindProperty("roleValues"); ra.arraySize = 4; for (int i = 0; i < 4; i++) ra.GetArrayElementAtIndex(i).objectReferenceValue = roleVals[i]; so2.ApplyModifiedPropertiesWithoutUndo(); }
            var so = new SerializedObject(card);
            var arr = so.FindProperty("statNumbers"); arr.arraySize = 3;
            for (int i = 0; i < 3; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = statNums[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root, "KitProfileCard");
        }

        private static void BuildShellScene(KitTheme th)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.04f, 0.03f, 0.03f); cam.orthographic = true;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 2400); scaler.matchWidthOrHeight = 0f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>();
            var ism = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (ism != null) es.AddComponent(ism); else es.AddComponent<StandaloneInputModule>();

            var bg = Go("Background", canvasGo.transform); Stretch(Rt(bg));
            var bgi = bg.AddComponent<Image>(); bgi.color = new Color(0.075f, 0.055f, 0.045f); bgi.raycastTarget = false;

            var ph = Txt(canvasGo.transform, "Placeholder", "الغرف", th.fontCalligraphy, 130, th.brassLight);
            Place(Rt(ph.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(900, 200));
            var sub = Txt(canvasGo.transform, "PlaceholderSub", "هنا تبنى الشاشة. اضغط على الشريط العلوي لتفتح البطاقة", th.fontBody, 38, th.ivoryDim, TextAlignmentOptions.Center, true);
            Place(Rt(sub.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(800, 120));

            var top = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("KitTopBar"), canvasGo.transform);
            var tr = Rt(top); tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(0.5f, 1); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(0, 270);

            var nav = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("KitNavBar"), canvasGo.transform);
            var nr = Rt(nav); nr.anchorMin = new Vector2(0, 0); nr.anchorMax = new Vector2(1, 0); nr.pivot = new Vector2(0.5f, 0); nr.anchoredPosition = Vector2.zero; nr.sizeDelta = new Vector2(0, 330);

            var card = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("KitProfileCard"), canvasGo.transform);
            Stretch(Rt(card));
            card.SetActive(false);   // opens from the top bar

            var demo = canvasGo.AddComponent<KitShellDemo>();
            SetRef(demo, "topBar", top.GetComponent<KitTopBar>());
            SetRef(demo, "navBar", nav.GetComponent<KitNavBar>());
            SetRef(demo, "card", card.GetComponent<KitProfileCard>());
            SetRef(demo, "placeholder", ph.GetComponent<ArabicText>());

            EditorSceneManager.SaveScene(scene, ShellScenePath);
        }


        // ------------------------------------------------------------------ rooms list + voice room
        private static GameObject InstChild(string prefab, Transform parent, string name)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(prefab), parent); g.name = name; return g;
        }

        private static void BuildRoomPrefabs(KitTheme th)
        {
            var dotSprite = th.dotWhite;
            var ivoryTxt = th.leatherText;

            // ---------- room card
            {
                var root = new GameObject("RoomCard", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(1000, 340);
                var bgImg = Img(root, th.leatherDark, true, 1f, false);
                var cg = root.AddComponent<CanvasGroup>();
                var le = root.AddComponent<LayoutElement>(); le.preferredWidth = 1000; le.preferredHeight = 340;

                var av = InstChild("AvatarFrame", root.transform, "Avatar");
                Place(Rt(av), new Vector2(0.5f, 0.5f), new Vector2(392, 62), new Vector2(130, 130));
                var nm = Txt(root.transform, "Name", "سهرة الشباب", th.fontCalligraphy, 60, ivoryTxt, TextAlignmentOptions.MidlineRight);
                Place(Rt(nm.gameObject), new Vector2(0.5f, 0.5f), new Vector2(112, 92), new Vector2(400, 76));
                var ow = Txt(root.transform, "Owner", "أبو سالم · المستوى ٧", th.fontBody, 28, th.ivoryDim, TextAlignmentOptions.MidlineRight);
                Place(Rt(ow.gameObject), new Vector2(0.5f, 0.5f), new Vector2(112, 40), new Vector2(400, 40));

                var cbox = Go("CountBox", root.transform);
                Place(Rt(cbox), new Vector2(0.5f, 0.5f), new Vector2(-395, 78), new Vector2(150, 104));
                Img(cbox, th.slotDark, true, 1.8f);
                var cn = Txt(cbox.transform, "Count", "٩", th.fontHeading, 56, th.brassLight);
                Place(Rt(cn.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(140, 66));
                var cof = Txt(cbox.transform, "Of", "من ١٦", th.fontBody, 22, th.ivoryDim);
                Place(Rt(cof.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(140, 30));

                var dots = new Image[16];
                for (int i = 0; i < 16; i++)
                {
                    var d = Go("Seat" + i, root.transform);
                    Place(Rt(d), new Vector2(0.5f, 0.5f), new Vector2(446 - i * 41f, -14), new Vector2(28, 28));
                    dots[i] = Img(d, dotSprite);
                }

                var chip = Go("Chip", root.transform);
                Place(Rt(chip), new Vector2(0.5f, 0.5f), new Vector2(380, -92), new Vector2(190, 56));
                Img(chip, th.slotDark, true, 2.2f);
                var chipT = Txt(chip.transform, "Label", "سهرة", th.fontBody, 28, th.leatherText);
                Stretch(Rt(chipT.gameObject), 4, 4, 4, 8);

                var live = Go("Live", root.transform);
                Place(Rt(live), new Vector2(0.5f, 0.5f), new Vector2(120, -92), new Vector2(300, 56));
                var ld = Go("Dot", live.transform); Place(Rt(ld), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(22, 22));
                var ldi = Img(ld, dotSprite); ldi.color = new Color32(0xE0, 0x34, 0x3F, 255);
                var lt = Txt(live.transform, "Label", "يتكلّم أحدهم", th.fontBody, 28, new Color32(0xE6, 0xB3, 0xB6, 255), TextAlignmentOptions.MidlineRight);
                Stretch(Rt(lt.gameObject), 0, 0, 44, 0);

                var codeBox = Go("CodeBox", root.transform);
                Place(Rt(codeBox), new Vector2(0.5f, 0.5f), new Vector2(-40, -92), new Vector2(190, 56));
                Img(codeBox, th.slotDark, true, 2.2f);
                var codeT = Txt(codeBox.transform, "Code", "K7Q2", th.fontHeading, 32, new Color32(0xFF, 0xB3, 0x5A, 255));
                Stretch(Rt(codeT.gameObject), 4, 4, 4, 8);

                var join = InstChild("KitButton_Primary", root.transform, "Join");
                Place(Rt(join), new Vector2(0.5f, 0.5f), new Vector2(-326, -98), new Vector2(480, 168));
                join.transform.localScale = Vector3.one * 0.6f;
                var joinFace = join.transform.Find("Face").GetComponent<Image>();
                var joinLbl = join.GetComponentInChildren<ArabicText>();

                var vip = Go("VipTab", root.transform);
                Place(Rt(vip), new Vector2(0.5f, 0.5f), new Vector2(0, 166), new Vector2(170, 52));
                Img(vip, th.brassPlate, true, 3f);
                var vipT = Txt(vip.transform, "Label", "VIP", th.fontHeading, 28, th.brassText);
                Stretch(Rt(vipT.gameObject), 0, 4, 0, 6);

                var full = Go("FullStamp", root.transform);
                Place(Rt(full), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(330, 110));
                full.transform.localRotation = Quaternion.Euler(0, 0, 9f);
                var fi = Img(full, th.stampFrame, true, 1f); fi.color = new Color32(0xC0, 0x32, 0x3A, 255);
                var ft = Txt(full.transform, "Label", "ممتلئة", th.fontCalligraphy, 64, new Color32(0xC0, 0x32, 0x3A, 255));
                Stretch(Rt(ft.gameObject), 6, 6, 6, 6);

                var nw = Go("NewTag", root.transform);
                Place(Rt(nw), new Vector2(0.5f, 0.5f), new Vector2(330, 166), new Vector2(130, 44));
                var nwi = nw.AddComponent<Image>(); nwi.color = th.teal; nwi.raycastTarget = false;
                var nwt = Txt(nw.transform, "Label", "جديدة", th.fontHeading, 26, new Color32(0x06, 0x23, 0x1A, 255));
                Stretch(Rt(nwt.gameObject), 0, 2, 0, 2);

                var shineClip = Go("ShineClip", root.transform); Stretch(Rt(shineClip), 14, 14, 14, 14); shineClip.AddComponent<RectMask2D>(); shineClip.transform.SetSiblingIndex(0);
                var band = Go("Shine", shineClip.transform); var bandR = Rt(band); bandR.anchorMin = bandR.anchorMax = new Vector2(0.5f, 0.5f); bandR.sizeDelta = new Vector2(1000, 340);
                var bandImg = Img(band, th.shineBand); bandImg.preserveAspect = false; bandImg.raycastTarget = false;
                shineClip.SetActive(false);
                var view = root.AddComponent<RoomCardView>();
                SetRef(view, "shineClip", shineClip); SetRef(view, "shine", bandR); SetRef(view, "liveDot", ldi);
                SetRef(view, "background", bgImg); SetRef(view, "avatar", av.GetComponent<AvatarFrame>());
                SetRef(view, "nameLabel", nm.GetComponent<ArabicText>()); SetRef(view, "ownerLabel", ow.GetComponent<ArabicText>());
                SetRef(view, "countLabel", cn.GetComponent<ArabicText>()); SetRef(view, "chipLabel", chipT.GetComponent<ArabicText>());
                SetRef(view, "liveLabel", lt.GetComponent<ArabicText>()); SetRef(view, "codeLabel", codeT.GetComponent<ArabicText>());
                SetRef(view, "joinLabel", joinLbl); SetRef(view, "vipLabel", vipT.GetComponent<ArabicText>());
                SetRef(view, "live", live); SetRef(view, "codeBox", codeBox); SetRef(view, "vipTab", vip);
                SetRef(view, "fullStamp", full); SetRef(view, "newTag", nw);
                SetRef(view, "joinButton", join.GetComponent<Button>()); SetRef(view, "joinImage", joinFace); SetRef(view, "group", cg);
                var so = new SerializedObject(view); var arr = so.FindProperty("seatDots"); arr.arraySize = 16;
                for (int i = 0; i < 16; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = dots[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                Save(root, "RoomCard");
            }

            // ---------- seat around the table
            {
                var root = new GameObject("Seat", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(210, 260);
                var chair = Go("Chair", root.transform);
                Place(Rt(chair), new Vector2(0.5f, 0.5f), new Vector2(0, 28), new Vector2(210, 158));
                Img(chair, th.chair);
                var glow = Go("TalkGlow", root.transform);
                Place(Rt(glow), new Vector2(0.5f, 0.5f), new Vector2(0, 28), new Vector2(230, 230));
                var gi = Img(glow, th.talkGlow); gi.color = new Color(1, 1, 1, 0);
                var dash = Go("Empty", root.transform);
                Place(Rt(dash), new Vector2(0.5f, 0.5f), new Vector2(0, 28), new Vector2(140, 140));
                var di = Img(dash, th.seatDashed);
                var av = InstChild("AvatarFrame", root.transform, "Avatar");
                Place(Rt(av), new Vector2(0.5f, 0.5f), new Vector2(0, 28), new Vector2(136, 136));
                var plate = Go("NamePlate", root.transform);
                Place(Rt(plate), new Vector2(0.5f, 0.5f), new Vector2(0, -52), new Vector2(200, 56));
                Img(plate, th.leatherDark, true, 2.4f);
                var nmT = Txt(plate.transform, "Name", "أبو سالم", th.fontBody, 28, th.leatherText);
                Stretch(Rt(nmT.gameObject), 6, 8, 6, 10);
                var host = Go("HostBadge", root.transform);
                Place(Rt(host), new Vector2(0.5f, 0.5f), new Vector2(-56, 88), new Vector2(46, 46)); Img(host, th.hostStar);
                var mute = Go("MutedDot", root.transform);
                Place(Rt(mute), new Vector2(0.5f, 0.5f), new Vector2(56, 84), new Vector2(46, 46)); Img(mute, th.badgeDot);
                var ready = Go("ReadyDot", root.transform);
                Place(Rt(ready), new Vector2(0.5f, 0.5f), new Vector2(56, 84), new Vector2(40, 40));
                var rdi = Img(ready, th.badgeDot); rdi.color = new Color(0.55f, 1f, 0.8f, 1f);
                var sv = root.AddComponent<SeatView>();
                SetRef(sv, "chair", Rt(chair)); SetRef(sv, "avatar", av.GetComponent<AvatarFrame>()); SetRef(sv, "dashed", di);
                SetRef(sv, "nameLabel", nmT.GetComponent<ArabicText>()); SetRef(sv, "namePlate", plate);
                SetRef(sv, "hostBadge", host); SetRef(sv, "mutedDot", mute); SetRef(sv, "readyDot", ready); SetRef(sv, "talkGlow", gi);
                Save(root, "Seat");
            }
        }

        private static void BuildRoomsScene(KitTheme th)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.04f, 0.03f, 0.03f); cam.orthographic = true;
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 2400); scaler.matchWidthOrHeight = 0f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>();
            var ism = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (ism != null) es.AddComponent(ism); else es.AddComponent<StandaloneInputModule>();

            var bg = Go("Background", canvasGo.transform); Stretch(Rt(bg));
            var bgi = bg.AddComponent<Image>(); bgi.color = new Color(0.075f, 0.055f, 0.045f); bgi.raycastTarget = false;
            canvasGo = MakeSafeRoot(canvasGo, true);   // everything below is built inside the safe area

            // ---- rooms screen (between top bar and nav bar)
            var screen = Go("RoomsScreen", canvasGo.transform); Stretch(Rt(screen));
            var quick = InstChild("KitButton_Primary", screen.transform, "QuickJoin");
            var qr = Rt(quick); qr.anchorMin = qr.anchorMax = new Vector2(0.5f, 1f); qr.pivot = new Vector2(0.5f, 1f); qr.anchoredPosition = new Vector2(-170, -290); qr.sizeDelta = new Vector2(620, 172);
            quick.GetComponentInChildren<ArabicText>().Text = "انضم سريع";
            var create = InstChild("KitButton_Secondary", screen.transform, "CreateRoom");
            var cr = Rt(create); cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 1f); cr.pivot = new Vector2(0.5f, 1f); cr.anchoredPosition = new Vector2(350, -290); cr.sizeDelta = new Vector2(330, 172);
            create.GetComponentInChildren<ArabicText>().Text = "افتح طاولتك";
            create.GetComponentInChildren<TextMeshProUGUI>().fontSize = 44;

            var scrollGo = Go("Scroll", screen.transform);
            var sr = Rt(scrollGo); sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = new Vector2(0, 330); sr.offsetMax = new Vector2(0, -500);
            var scroll = scrollGo.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 40f;
            var vp = Go("Viewport", scrollGo.transform); Stretch(Rt(vp)); vp.AddComponent<RectMask2D>();
            var vpi = vp.AddComponent<Image>(); vpi.color = Color.clear; vpi.raycastTarget = true;
            var content = Go("Content", vp.transform);
            var cRt = Rt(content); cRt.anchorMin = new Vector2(0, 1); cRt.anchorMax = new Vector2(1, 1); cRt.pivot = new Vector2(0.5f, 1); cRt.anchoredPosition = Vector2.zero; cRt.sizeDelta = new Vector2(0, 0);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 34; vlg.padding = new RectOffset(0, 0, 24, 60); vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = false; vlg.childControlHeight = false; vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            var csf = content.AddComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = Rt(vp); scroll.content = cRt;

            var rs = screen.AddComponent<KitRoomsScreen>();
            SetRef(rs, "content", cRt); SetRef(rs, "roomCardPrefab", LoadPrefab("RoomCard"));
            SetRef(rs, "quickJoinButton", quick.GetComponent<Button>()); SetRef(rs, "createButton", create.GetComponent<Button>());
            rs.SetRoomsImmediate(KitRoomsDemo.MockRooms());
            LayoutRebuilder.ForceRebuildLayoutImmediate(cRt);

            var top = InstChild("KitTopBar", canvasGo.transform, "KitTopBar");
            var tr = Rt(top); tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(0.5f, 1); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(0, 270);
            var nav = InstChild("KitNavBar", canvasGo.transform, "KitNavBar");
            var nr = Rt(nav); nr.anchorMin = new Vector2(0, 0); nr.anchorMax = new Vector2(1, 0); nr.pivot = new Vector2(0.5f, 0); nr.anchoredPosition = Vector2.zero; nr.sizeDelta = new Vector2(0, 330);
            var card = InstChild("KitProfileCard", canvasGo.transform, "KitProfileCard"); Stretch(Rt(card)); card.SetActive(false);

            // ---- voice room (full screen, hidden until a room is joined)
            var vr = Go("VoiceRoom", canvasGo.transform); Stretch(Rt(vr));
            var vbg = Go("Bg", vr.transform); Stretch(Rt(vbg));
            var vbgi = vbg.AddComponent<Image>(); vbgi.color = new Color(0.05f, 0.035f, 0.03f);
            var warm = Go("Warm", vr.transform); Place(Rt(warm), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(1500, 1500));
            var wi = Img(warm, th.navSealGlow); wi.color = new Color(1f, 0.85f, 0.55f, 0.16f); wi.raycastTarget = false;
            var table = Go("Table", vr.transform); Place(Rt(table), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(840, 1215)); Img(table, th.tableTop).raycastTarget = false;

            void Prop(Sprite sp, Vector2 pos, Vector2 size, float rot, string name)
            {
                var p = Go(name, vr.transform); Place(Rt(p), new Vector2(0.5f, 0.5f), pos, size);
                p.transform.localRotation = Quaternion.Euler(0, 0, rot); Img(p, sp).raycastTarget = false;
            }
            Prop(th.propDeck, new Vector2(20, -20), new Vector2(112, 148), -10f, "Deck");
            Prop(th.propDallah, new Vector2(-190, 150), new Vector2(150, 210), 0f, "Dallah");
            Prop(th.propCup, new Vector2(-95, 70), new Vector2(76, 76), 0f, "Cup1");
            Prop(th.propCup, new Vector2(-215, 55), new Vector2(76, 76), 0f, "Cup2");
            Prop(th.propAshtray, new Vector2(185, -90), new Vector2(104, 104), 0f, "Ashtray");

            var seatsRoot = Go("Seats", vr.transform); Place(Rt(seatsRoot), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(10, 10));

            // top: back, plaque, invite
            var back = InstChild("KitButton_Secondary", vr.transform, "Back");
            var br = Rt(back); br.anchorMin = br.anchorMax = new Vector2(1, 1); br.pivot = new Vector2(1, 1); br.anchoredPosition = new Vector2(-32, -78); br.sizeDelta = new Vector2(260, 130);
            back.GetComponentInChildren<ArabicText>().Text = "رجوع"; back.GetComponentInChildren<TextMeshProUGUI>().fontSize = 44;
            var invite = InstChild("KitButton_Secondary", vr.transform, "Invite");
            var ir = Rt(invite); ir.anchorMin = ir.anchorMax = new Vector2(0, 1); ir.pivot = new Vector2(0, 1); ir.anchoredPosition = new Vector2(32, -78); ir.sizeDelta = new Vector2(260, 130);
            invite.GetComponentInChildren<ArabicText>().Text = "دعوة"; invite.GetComponentInChildren<TextMeshProUGUI>().fontSize = 44;
            var plaque = Go("Plaque", vr.transform); Place(Rt(plaque), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(480, 150));
            Img(plaque, th.leatherDark, true, 1.3f);
            var rname = Txt(plaque.transform, "Name", "سهرة الشباب", th.fontCalligraphy, 58, th.leatherText);
            Place(Rt(rname.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(440, 76));
            var rsub = Txt(plaque.transform, "Sub", "٩ من ١٦", th.fontBody, 28, th.brass);
            Place(Rt(rsub.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(440, 40));

            // bottom: speaker line, mic, ready
            var spk = Txt(vr.transform, "Speaker", "يتكلّم الآن: أم سعيد", th.fontBody, 34, th.ivoryDim);
            Place(Rt(spk.gameObject), new Vector2(0.5f, 0f), new Vector2(0, 360), new Vector2(800, 54));
            var mic = Go("Mic", vr.transform); Place(Rt(mic), new Vector2(0.5f, 0f), new Vector2(330, 170), new Vector2(210, 210));
            var micSeal = Img(mic, th.navSeal, false, 1f, true);
            var micBtn = mic.AddComponent<Button>(); micBtn.targetGraphic = micSeal; micBtn.transition = Selectable.Transition.None;
            var micFx = mic.AddComponent<KitButtonFx>(); SetRef(micFx, "face", Rt(mic));
            var micIcon = Go("Icon", mic.transform); Place(Rt(micIcon), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112, 112));
            var micIconImg = Img(micIcon, th.iconMic);
            var ready = InstChild("KitButton_Primary", vr.transform, "Ready");
            var rr = Rt(ready); rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0f); rr.pivot = new Vector2(0.5f, 0.5f); rr.anchoredPosition = new Vector2(-110, 170); rr.sizeDelta = new Vector2(560, 180);
            ready.GetComponentInChildren<ArabicText>().Text = "جاهز";

            var room = vr.AddComponent<KitVoiceRoom>();
            SetRef(room, "seatsRoot", Rt(seatsRoot)); SetRef(room, "seatPrefab", LoadPrefab("Seat"));
            SetRef(room, "roomName", rname.GetComponent<ArabicText>()); SetRef(room, "roomSub", rsub.GetComponent<ArabicText>()); SetRef(room, "speakerLabel", spk.GetComponent<ArabicText>());
            SetRef(room, "backButton", back.GetComponent<Button>()); SetRef(room, "inviteButton", invite.GetComponent<Button>());
            SetRef(room, "micButton", micBtn); SetRef(room, "readyButton", ready.GetComponent<Button>());
            SetRef(room, "micIcon", micIconImg); SetRef(room, "micSeal", micSeal);
            room.BuildSeatsImmediate(KitRoomsDemo.MockSeats());
            vr.SetActive(false);

            AddRoomsFlowUi(canvasGo, th, screen, rs);

            var demo = canvasGo.AddComponent<KitRoomsDemo>();
            SetRef(demo, "topBar", top.GetComponent<KitTopBar>()); SetRef(demo, "navBar", nav.GetComponent<KitNavBar>());
            SetRef(demo, "card", card.GetComponent<KitProfileCard>()); SetRef(demo, "rooms", rs); SetRef(demo, "voiceRoom", room);

            EditorSceneManager.SaveScene(scene, RoomsScenePath);
        }

        // ------------------------------------------------------------------ test scene
        private static void BuildScene(KitTheme th)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.04f, 0.03f, 0.03f);
            cam.orthographic = true;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 2400); scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>();
            var ism = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (ism != null) es.AddComponent(ism); else es.AddComponent<StandaloneInputModule>();

            var bg = Go("Background", canvasGo.transform); Stretch(Rt(bg));
            var bgi = bg.AddComponent<Image>(); bgi.color = new Color(0.075f, 0.055f, 0.045f); bgi.raycastTarget = false;

            var root = Rt(Go("Content", canvasGo.transform)); Stretch(root);
            var titleT = Txt(root, "Title", "مكتبة الواجهة", th.fontCalligraphy, 92, th.brassLight);
            Place(Rt(titleT.gameObject), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(900, 130));

            GameObject Inst(string prefab, Vector2 pos, string name = null)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + prefab + ".prefab");
                var o = (GameObject)PrefabUtility.InstantiatePrefab(p, root);
                var r = Rt(o); r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.anchoredPosition = pos;
                if (name != null) o.name = name;
                return o;
            }
            void Cap(string text, float y)
            {
                var c = Txt(root, "Cap", text, th.fontBody, 34, th.ivoryDim, TextAlignmentOptions.Right);
                Place(Rt(c.gameObject), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(960, 50));
            }

            Cap("الأزرار", -270);
            Inst("KitButton_Primary", new Vector2(-270, -390));
            Inst("KitButton_Secondary", new Vector2(270, -390));
            Inst("KitButton_Brass", new Vector2(0, -590));

            Cap("لوحات الأسماء والكوينز", -700);
            Inst("NameLabel_Leather", new Vector2(330, -790));
            Inst("NameLabel_Brass", new Vector2(0, -790));
            Inst("NameLabel_Paper", new Vector2(-330, -790));
            Inst("CoinPill", new Vector2(0, -920));

            Cap("الرتب والصور", -950);
            for (int i = 0; i < 4; i++)
            {
                var m = Inst("RankMedal", new Vector2(345 - i * 230, -1190));
                m.GetComponent<RankMedalView>().Set(i, i % 3 + 1);
            }
            for (int i = 0; i < 4; i++)
            {
                var m = Inst("RankMedal", new Vector2(345 - i * 230, -1560));
                m.GetComponent<RankMedalView>().Set(i + 4, (i + 1) % 4);
            }
            for (int i = 0; i < 3; i++)
            {
                var a = Inst("AvatarFrame", new Vector2(300 - i * 300, -1830)); a.transform.localScale = Vector3.one * 0.8f;
                a.GetComponent<AvatarFrame>().Set(i * 3, null, i != 1);
            }
            var pl = Inst("KitPlaque", new Vector2(0, -2025)); pl.transform.localScale = Vector3.one * 0.44f;

            var nav = Inst("KitNavBar", Vector2.zero);
            var nr = Rt(nav); nr.anchorMin = new Vector2(0f, 0f); nr.anchorMax = new Vector2(1f, 0f); nr.pivot = new Vector2(0.5f, 0f); nr.anchoredPosition = Vector2.zero; nr.sizeDelta = new Vector2(0f, 330f);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
