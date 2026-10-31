using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// The bottom bar, idea B of Docs/mockups/navbar.html (the case file). The pieces are exported from the mockup by
    /// Docs/mockups/export-navbar-b.js into Art/UI/Nav, and every position below is the mockup's measurement (layout.json there)
    /// in CSS px on a 390px phone, origin at the phone's bottom-left; one CSS px is NK canvas units.
    /// </summary>
    public static partial class KitBuilder
    {
        private const string NavArt = "Assets/_Project/Art/UI/Nav/";
        private const float NK = 1080f / 390f;

        [MenuItem("Mafia Night/UI Kit/Build Nav Bar")]
        public static void BuildNavBarMenu()
        {
            var th = AssetDatabase.LoadAssetAtPath<KitTheme>(ThemePath);
            if (th == null) { Debug.LogError("[UIKit] no theme, run the full UI kit build first"); return; }
            BuildNavBar(th);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIKit] built the bottom bar");
        }

        /// <summary>Imports a nav sprite: texels per CSS px as exported, optional horizontal slice ends (CSS px, padding included).</summary>
        private static Sprite NavSpr(string name, float texPerCss = 0f, float sliceCss = 0f)
        {
            string path = NavArt + name + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true; ti.filterMode = FilterMode.Bilinear; ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                if (texPerCss > 0f) ti.spritePixelsPerUnit = 100f * texPerCss / NK;      // so a sliced border keeps its CSS size
                if (sliceCss > 0f) { float b = sliceCss * texPerCss; ti.spriteBorder = new Vector4(b, 0f, b, 0f); }
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Places a rect given in CSS px (x, y from the parent's bottom-left, w, h); pivot in the middle.</summary>
        private static RectTransform NavRect(GameObject g, float x, float y, float w, float h)
        {
            var r = Rt(g);
            r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2((x + w / 2f) * NK, (y + h / 2f) * NK);
            r.sizeDelta = new Vector2(w * NK, h * NK);
            return r;
        }

        private static Image NavImg(Transform parent, string name, Sprite s, float x, float y, float w, float h, bool sliced = false)
        {
            var g = Go(name, parent); NavRect(g, x, y, w, h);
            var i = g.AddComponent<Image>(); i.sprite = s; i.raycastTarget = false; i.preserveAspect = false;
            if (sliced) { i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = 1f; }
            return i;
        }

        private static TextMeshProUGUI NavTxt(Transform parent, string name, string text, TMP_FontAsset font, float cssSize, string hex, TextAlignmentOptions align, float x, float y, float w, float h, bool bold)
        {
            var t = Txt(parent, name, text, font, cssSize * NK, ParseColor(hex, Color.white), align);
            NavRect(t.gameObject, x, y, w, h);
            t.fontStyle = FontStyles.Normal;
            if (bold) { var mat = EnsureBoldMaterial(font); if (mat != null) t.fontSharedMaterial = mat; }
            t.overflowMode = TextOverflowModes.Overflow; t.enableWordWrapping = false;
            return t;
        }

        private static CanvasGroup Group(GameObject g) { var c = g.AddComponent<CanvasGroup>(); c.blocksRaycasts = false; c.interactable = false; return c; }

        /// <summary>A red count badge (17px plus its ring, sliced so it widens) with its number, centred at (cx, cy) CSS px.</summary>
        private static GameObject NavCount(Transform parent, string name, float cx, float cy, TMP_FontAsset lalezar, Sprite cnt, string sample)
        {
            var im = NavImg(parent, name, cnt, cx - 12.5f, cy - 12.5f, 25f, 25f, true);
            var n = Txt(im.transform, "N", sample, lalezar, 10.56f * NK, Color.white);
            Stretch(Rt(n.gameObject), 0, 0, 0, 2f);
            n.enableWordWrapping = false; n.overflowMode = TextOverflowModes.Overflow;
            return im.gameObject;
        }

        public static void BuildNavBar(KitTheme th)
        {
            AssetDatabase.Refresh();
            Sprite leather = NavSpr("nb_leather"), paper = NavSpr("nb_paper"), tagSpr = NavSpr("nb_tag", 6f, 12f), cnt = NavSpr("nb_cnt", 6f, 12f), dot = NavSpr("nb_dot");
            Sprite panel = NavSpr("nm_panel"), arrow = NavSpr("nm_arrow"), cur = NavSpr("nm_cur");
            TMP_FontAsset cairo = th.fontBody, lalezar = th.fontLalezar != null ? th.fontLalezar : th.fontHeading;

            var root = new GameObject("KitNavBar", typeof(RectTransform));
            Rt(root).sizeDelta = new Vector2(1080, 330);

            // the dim over the page while a menu is open: under the bar, over everything above it; a tap on it closes the menu
            var scrimGo = Go("Scrim", root.transform); NavRect(scrimGo, 0f, 0f, 390f, 2400f);
            var si = scrimGo.AddComponent<Image>(); si.color = new Color(0f, 0f, 0f, 0.91f); si.raycastTarget = true;   // #000a in the browser, in linear light
            var sb = scrimGo.AddComponent<Button>(); sb.transition = Selectable.Transition.None; sb.targetGraphic = si;
            var scrim = scrimGo.AddComponent<CanvasGroup>();

            // the leather strip (74px with the stitch, the shadow above it)
            var bar = NavImg(root.transform, "Bar", leather, 0f, 0f, 390f, 126f);
            bar.raycastTarget = true;   // taps on the strip between tabs don't fall through to the page

            var nav = root.AddComponent<KitNavBar>();
            var so = new SerializedObject(nav);
            var tabsP = so.FindProperty("tabs"); tabsP.arraySize = 5;

            string[] keys = { "market", "family", "rooms", "ranks", "settings" };
            string[] names = { "السوق", "العائلة", "الغرف", "الرتب", "الإعدادات" };
            string[] icons = { "market", "family", "rooms", "ranks", "safe" };
            float[] tabX = { 310.81f, 234.61f, 158.41f, 82.2f, 6.02f };
            float[] labelW = { 27f, 26f, 27f, 25f, 43f };      // Aref label images (labels.json)
            const float TabW = 73.2f, IconY = 37.34f, LabelOffY = 22.67f, LabelOnCy = 35f;

            for (int i = 0; i < 5; i++)
            {
                var tab = Go("Tab_" + keys[i], root.transform); NavRect(tab, tabX[i], 0f, TabW, 94f);
                var hit = tab.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
                var btn = tab.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.targetGraphic = hit;

                var paperImg = NavImg(tab.transform, "Paper", paper, 0f, 0f, TabW, 94f);
                var pr = Rt(paperImg.gameObject); pr.pivot = new Vector2(0.5f, 0f); pr.anchoredPosition = new Vector2(TabW / 2f * NK, 0f);
                var paperG = Group(paperImg.gameObject);

                var content = Go("Content", tab.transform); NavRect(content, 0f, 0f, TabW, 94f);
                var off = NavImg(content.transform, "IconOff", NavSpr("ni_" + icons[i] + "_off"), TabW / 2f - 21f, IconY - 4f, 42f, 42f);
                var on = NavImg(content.transform, "IconOn", NavSpr("ni_" + icons[i]), TabW / 2f - 21f, IconY - 4f, 42f, 42f);
                var offG = Group(off.gameObject); var onG = Group(on.gameObject);
                if (i == 1) { var b = NavCount(content.transform, "Badge", 285.7f - tabX[1], 67.5f, lalezar, cnt, "٣"); b.SetActive(false); }
                if (i == 3) { var d = NavImg(content.transform, "Dot", dot, 132.3f - tabX[3] - 7.5f, 68.5f - 7.5f, 15f, 15f); d.gameObject.SetActive(false); }

                var lb = NavTxt(tab.transform, "Label", names[i], cairo, 10.56f, "#968b78", TextAlignmentOptions.Center, 0f, LabelOffY - 2f, TabW, 16.67f, true);
                var lon = NavImg(tab.transform, "LabelOn", NavSpr("nl_" + keys[i]), TabW / 2f - (labelW[i] + 8f) / 2f, LabelOnCy - 12f, labelW[i] + 8f, 24f);
                lon.color = ParseColor("#2a1a0e", Color.black);
                var lonG = Group(lon.gameObject);

                RectTransform tagRt = null;
                if (i == 2)
                {   // the paper tag with the players online, on the tab's top edge (tilted 3 degrees)
                    var tg = NavImg(tab.transform, "Tag", tagSpr, TabW / 2f - 42.3f, 84.5f - 16.5f, 84.6f, 33f, true);
                    tagRt = Rt(tg.gameObject); tagRt.localEulerAngles = new Vector3(0, 0, 3f);
                    var tx = NavTxt(tg.transform, "Count", "١٬٢٣٤ متصل", cairo, 9.28f, "#5a1016", TextAlignmentOptions.Center, 8f, 6f, 62f, 21f, true);
                    var dt = NavImg(tg.transform, "Live", dot, 67.5f, 13f, 7f, 7f);
                    dt.color = ParseColor("#8a1c24", Color.red);
                    tg.gameObject.SetActive(false);
                }

                var el = tabsP.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("button").objectReferenceValue = btn;
                el.FindPropertyRelative("content").objectReferenceValue = Rt(content);
                el.FindPropertyRelative("iconOn").objectReferenceValue = onG;
                el.FindPropertyRelative("iconOff").objectReferenceValue = offG;
                el.FindPropertyRelative("label").objectReferenceValue = lb;
                el.FindPropertyRelative("labelOn").objectReferenceValue = lonG;
                el.FindPropertyRelative("paper").objectReferenceValue = paperG;
                el.FindPropertyRelative("tag").objectReferenceValue = tagRt;
            }

            // the menus over market, family and ranks (250 x 124.72, 14px above the bar, kept 8px inside the phone; the arrow points at the tab)
            var menusP = so.FindProperty("menus"); menusP.arraySize = 3;
            int[] menuTab = { 0, 1, 3 };
            float[] menuX = { 132f, 132f, 8f }, arrowX = { 215.41f, 139.21f, 110.8f };
            string[,] titles = { { "المتجر", "الحقيبة" }, { "الدردشة", "العائلة" }, { "التصنيفات", "رتبتي" } };
            string[,] subs = { { "كوينز وجواهر وعروض", "الأغراض اللي اشتريتها" }, { "الخاص والمجموعات والساحة", "الأصدقاء والطلبات" }, { "الأقوى اليوم والأسبوع", "من الصبي للدون" } };
            string[,] micons = { { "market", "case" }, { "phone", "family" }, { "trophy", "medal" } };
            bool[,] soon = { { false, false }, { false, false }, { false, true } };    // the rank ladder is not built yet
            const float MW = 250f, MH = 124.72f, ItemW = 236f, ItemH = 53.86f;
            for (int m = 0; m < 3; m++)
            {
                var mg = Go("Menu_" + keys[menuTab[m]], root.transform);
                var mr = NavRect(mg, menuX[m], 108f, MW, MH);
                mr.pivot = new Vector2(arrowX[m] / MW, 0f);                      // it grows out of the arrow
                mr.anchoredPosition = new Vector2((menuX[m] + arrowX[m]) * NK, 108f * NK);
                var mgG = mg.AddComponent<CanvasGroup>();
                NavImg(mg.transform, "Panel", panel, -40f, -40f, 330f, 204.72f);
                NavImg(mg.transform, "Arrow", arrow, arrowX[m] - 15f, -15.5f, 30f, 30f);

                var mel = menusP.GetArrayElementAtIndex(m);
                mel.FindPropertyRelative("tab").intValue = menuTab[m];
                mel.FindPropertyRelative("root").objectReferenceValue = mr;
                mel.FindPropertyRelative("group").objectReferenceValue = mgG;
                var placesP = mel.FindPropertyRelative("places"); placesP.arraySize = 2;
                for (int p = 0; p < 2; p++)
                {
                    float iy = p == 0 ? 63.86f : 7f;
                    var item = Go("Place" + p, mg.transform); NavRect(item, 7f, iy, ItemW, ItemH);
                    var ih = item.AddComponent<Image>(); ih.color = Color.clear; ih.raycastTarget = true;
                    var ib = item.AddComponent<Button>(); ib.transition = Selectable.Transition.None; ib.targetGraphic = ih;
                    var curImg = NavImg(item.transform, "Current", cur, -2f, -2f, 240f, 57.86f);
                    curImg.gameObject.SetActive(false);
                    NavImg(item.transform, "Icon", NavSpr("nm_" + micons[m, p]), 189f - 4f, 7.94f - 4f, 46f, 46f);

                    var text = Go("Text", item.transform);
                    var trt = Rt(text); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f);
                    trt.offsetMin = new Vector2(9f * NK, 0f); trt.offsetMax = new Vector2(-(ItemW - 179f) * NK, 0f);
                    var title = NavTxt(text.transform, "Title", titles[m, p], lalezar, 17.28f, "#f0dca0", TextAlignmentOptions.Right, 0f, 27f, 170f, 19.86f, false);
                    var sub = NavTxt(text.transform, "Sub", subs[m, p], cairo, 10.56f, "#a1957f", TextAlignmentOptions.Right, 0f, 7f, 170f, 20f, true);
                    foreach (var tt in new[] { title, sub })
                    {   // fill the text column, whatever its width
                        var r = Rt(tt.gameObject); float y = r.anchoredPosition.y, h = r.sizeDelta.y;
                        r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f); r.sizeDelta = new Vector2(0f, h); r.anchoredPosition = new Vector2(0f, y);
                        tt.overflowMode = TextOverflowModes.Ellipsis;
                    }
                    title.overflowMode = TextOverflowModes.Overflow;   // a Lalezar line is taller than its box: with Ellipsis TMP draws nothing

                    GameObject badge = null, dotGo = null; TMP_Text num = null;
                    if (m == 1) { badge = NavCount(item.transform, "Badge", 17.5f, 26.93f, lalezar, cnt, "٢"); num = badge.transform.Find("N").GetComponent<TMP_Text>(); badge.SetActive(false); }
                    if (m == 2 && p == 0) { dotGo = NavImg(item.transform, "Dot", dot, 13.5f - 7.5f, 26.93f - 7.5f, 15f, 15f).gameObject; dotGo.SetActive(false); }
                    if (soon[m, p])
                    {
                        var s = NavTxt(item.transform, "Soon", "قريباً", cairo, 9.6f, "#c9a050", TextAlignmentOptions.Left, 9f, 17f, 40f, 20f, true);
                        s.gameObject.SetActive(true);
                        trt.offsetMin = new Vector2(48f * NK, 0f);
                    }

                    var pel = placesP.GetArrayElementAtIndex(p);
                    pel.FindPropertyRelative("button").objectReferenceValue = ib;
                    pel.FindPropertyRelative("current").objectReferenceValue = curImg.gameObject;
                    pel.FindPropertyRelative("text").objectReferenceValue = trt;
                    pel.FindPropertyRelative("textLeft").floatValue = (soon[m, p] ? 48f : 9f) * NK;
                    pel.FindPropertyRelative("textLeftWithBadge").floatValue = (m == 2 ? 26f : soon[m, p] ? 48f : 36f) * NK;
                    pel.FindPropertyRelative("badge").objectReferenceValue = badge;
                    pel.FindPropertyRelative("badgeNumber").objectReferenceValue = num;
                    pel.FindPropertyRelative("dot").objectReferenceValue = dotGo;
                    pel.FindPropertyRelative("soon").boolValue = soon[m, p];
                }
                mg.SetActive(false);
            }

            so.FindProperty("scrim").objectReferenceValue = scrim;
            so.FindProperty("rise").floatValue = (44.67f - 37.34f) * NK;
            so.FindProperty("tagRise").floatValue = 12f * NK;
            so.ApplyModifiedPropertiesWithoutUndo();
            scrimGo.SetActive(false);
            nav.CaptureRest();
            nav.SnapToSelected();
            EditorUtility.SetDirty(nav);
            Save(root, "KitNavBar");
        }
    }
}
