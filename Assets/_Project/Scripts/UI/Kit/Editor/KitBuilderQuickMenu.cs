using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static MafiaNight.UI.Kit.Editor.KitProceduralTextures;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// The quick menu of Docs/mockups/quick-rail.html as prefabs: "QuickMenu" (the tab on the edge, the icons that drop down, the offers sheet,
    /// the undo toast, the edge guides shown while it is carried) and "QuickOfferRow" (one offer of the sheet). Sizes are the HTML ones times
    /// 2.769 (390 px phone -> 1080 canvas). Its small sprites are drawn here (brass tab, drop-down, round buttons, line icons).
    /// </summary>
    public static partial class KitBuilder
    {
        private static readonly string[] QmNames = { "qm_tab", "qm_pill", "qm_ring", "qm_drop", "qm_btn", "qm_badge", "qm_ic_offers", "qm_ic_events", "qm_ic_block", "qm_ic_chev" };

        [MenuItem("Mafia Night/UI Kit/Build Quick Menu")]
        public static void BuildQuickMenuMenu()
        {
            var th = BuildTheme();
            headFont = th.fontHeading;
            EnsureQuickMenuSprites(true);
            BuildQuickMenu(th);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIKit] built the quick menu prefabs");
        }

        /// <summary>Draws the quick menu sprites when they are missing (or always when forced) and sets their import settings.</summary>
        internal static void EnsureQuickMenuSprites(bool force = false)
        {
            bool missing = force;
            foreach (var n in QmNames) if (!File.Exists(Art + n + ".png")) missing = true;
            if (missing) { Directory.CreateDirectory(Art); DrawQuickMenuSprites(); AssetDatabase.Refresh(); }
            foreach (var n in QmNames)
            {
                var imp = AssetImporter.GetAtPath(Art + n + ".png") as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.filterMode = FilterMode.Bilinear; imp.spritePixelsPerUnit = 100;
                imp.wrapMode = TextureWrapMode.Clamp; imp.textureCompression = TextureImporterCompression.Uncompressed;
                var s = new TextureImporterSettings(); imp.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect;
                s.spriteBorder = n == "qm_drop" ? new Vector4(0, 110, 0, 10) : Vector4.zero;      // left, bottom, right, top: the drop-down grows in height only
                imp.SetTextureSettings(s);
                imp.SaveAndReimport();
            }
        }

        private static float QmSeg(float x, float y, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(x, y); var a = new Vector2(ax, ay); var ab = new Vector2(bx, by) - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static void DrawQuickMenuSprites()
        {
            // the brass of the CSS: radial-gradient(circle at 35% 28%, #f5e2a8, #b99048 55%, #6e4b1c)
            Color Brass(float x, float y, float w, float h)
            {
                var c0 = new Vector2(.35f * w, .28f * h);
                float t = Vector2.Distance(new Vector2(x, y), c0) / Vector2.Distance(c0, new Vector2(w, h));
                return t < .55f ? Color.Lerp(C(0xf5e2a8), C(0xb99048), t / .55f) : Color.Lerp(C(0xb99048), C(0x6e4b1c), Mathf.Clamp01((t - .55f) / .45f));
            }
            Color White(bool on) => on ? Color.white : Color.clear;

            // the edge tab: a half capsule, flat on the screen edge (drawn for the right edge, flipped for the left)
            Make("qm_tab", 166, 276, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 152f, 138f, 152f, 138f, 138f);
                if (d > 0f) return Color.clear;
                if (d > -6f) return Color.black;
                var c = Brass(x, y, 166, 276);
                if (d > -11f && y < 60f) c = Over(C(0xffffff, .5f), c);
                return c;
            });
            // the same while it is carried: a full capsule
            Make("qm_pill", 166, 276, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 83f, 138f, 83f, 138f, 83f);
                if (d > 0f) return Color.clear;
                if (d > -6f) return Color.black;
                var c = Brass(x, y, 166, 276);
                if (d > -11f && y < 60f) c = Over(C(0xffffff, .5f), c);
                return c;
            });
            // the pulse ring (white, tinted red by the script)
            Make("qm_ring", 166, 276, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 152f, 138f, 152f, 138f, 138f);
                return White(d <= 0f && d > -10f);
            });
            // the drop-down under the tab: leather, brass inner line, rounded on the lower outer corner only
            Make("qm_drop", 166, 300, (x, y) =>
            {
                float ins = (x < 100f && y > 200f) ? 100f - Vector2.Distance(new Vector2(x, y), new Vector2(100f, 200f)) : Mathf.Min(x, 300f - y);
                if (ins < 0f) return Color.clear;
                if (ins < 5f) return Color.black;
                if (ins < 10f) return C(0x6e5224);
                var c = Color.Lerp(C(0x2d2019), C(0x140d0a), y / 300f);
                float n = (Hash((int)x, (int)y) - .5f) * .06f;
                return new Color(c.r + n, c.g + n * .8f, c.b + n * .6f, 1f);
            });
            // the round dark buttons inside it
            Make("qm_btn", 188, 188, (x, y) =>
            {
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(94f, 94f));
                if (r > 94f) return Color.clear;
                if (r > 89f) return Color.black;
                var c0 = new Vector2(.35f * 188f, .28f * 188f);
                float t = Vector2.Distance(new Vector2(x, y), c0) / Vector2.Distance(c0, new Vector2(188f, 188f));
                var c = Color.Lerp(C(0x4a382c), C(0x1d130e), Mathf.Clamp01(t * 1.6f));
                if (r > 82f && y < 94f) c = Over(C(0xffffff, .14f), c);
                return c;
            });
            // the red count
            Make("qm_badge", 64, 64, (x, y) =>
            {
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f));
                if (r > 32f) return Color.clear;
                if (r > 28.5f) return Color.black;
                float t = Vector2.Distance(new Vector2(x, y), new Vector2(22.4f, 19.2f)) / Vector2.Distance(new Vector2(22.4f, 19.2f), new Vector2(64f, 64f));
                return Color.Lerp(C(0xe0525a), C(0x8a1c24), Mathf.Clamp01(t));
            });
            // line icons (white, tinted where they are used)
            Make("qm_ic_offers", 96, 96, (x, y) => White(
                Mathf.Abs(RoundRectSdf(x, y, 46f, 48f, 26f, 34f, 8f)) < 4f ||
                QmSeg(x, y, 34, 34, 58, 34) < 3.5f || QmSeg(x, y, 34, 48, 58, 48) < 3.5f || QmSeg(x, y, 34, 62, 50, 62) < 3.5f));
            Make("qm_ic_events", 96, 96, (x, y) => White(
                Mathf.Abs(RoundRectSdf(x, y, 48f, 52f, 32f, 29f, 8f)) < 4f ||
                QmSeg(x, y, 16, 40, 80, 40) < 4f || QmSeg(x, y, 36, 16, 36, 30) < 4f || QmSeg(x, y, 60, 16, 60, 30) < 4f ||
                QmSeg(x, y, 36, 62, 46, 72) < 4f || QmSeg(x, y, 46, 72, 62, 52) < 4f));
            Make("qm_ic_block", 96, 96, (x, y) => White(
                Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(48f, 48f)) - 36f) < 4f || QmSeg(x, y, 23, 23, 73, 73) < 4f));
            Make("qm_ic_chev", 96, 96, (x, y) => White(QmSeg(x, y, 24, 36, 48, 60) < 5.5f || QmSeg(x, y, 48, 60, 72, 36) < 5.5f));
        }

        private static void QmSetRefs(Object comp, params (string, Object)[] pairs) { foreach (var (f, v) in pairs) SetRef(comp, f, v); }

        private static void BuildQuickMenu(KitTheme th)
        {
            var ink = (Color)th.paperInk; var crimDark = (Color)th.crimson;
            var lalezar = th.fontLalezar != null ? th.fontLalezar : th.fontHeading; var cairo = th.fontBody;
            var soft = new Color32(0x4A, 0x3C, 0x24, 255);
            var cream = new Color32(0xE6, 0xD9, 0xBD, 255);
            var tabS = Spr("qm_tab"); var pillS = Spr("qm_pill"); var ringS = Spr("qm_ring"); var dropS = Spr("qm_drop"); var btnS = Spr("qm_btn"); var badgeS = Spr("qm_badge");
            var icOffers = Spr("qm_ic_offers"); var icEvents = Spr("qm_ic_events"); var icBlock = Spr("qm_ic_block"); var icChev = Spr("qm_ic_chev");

            GameObject Pic(string n, Transform parent, Sprite s, float w, float h, Color tint)
            {
                var g = Go(n, parent); var i = g.AddComponent<Image>(); i.sprite = s; i.color = tint; i.raycastTarget = false; i.preserveAspect = true;
                Rt(g).sizeDelta = new Vector2(w, h); return g;
            }
            void NoWrap(TextMeshProUGUI t) { t.overflowMode = TextOverflowModes.Overflow; t.textWrappingMode = TextWrappingModes.NoWrap; }
            LayoutElement Le(GameObject g, float pw = -1, float ph = -1, float fw = -1) { var l = g.AddComponent<LayoutElement>(); if (pw >= 0) l.preferredWidth = pw; if (ph >= 0) l.preferredHeight = ph; if (fw >= 0) l.flexibleWidth = fw; return l; }
            ArabicText Badge(Transform parent, out GameObject root, string text, float size)
            {
                var b = Pic("Dot", parent, badgeS, size, size, Color.white); root = b;
                var t = Txt(b.transform, "Count", text, lalezar, 32, Color.white, TextAlignmentOptions.Center); NoWrap(t); Stretch(Rt(t.gameObject));
                return t.GetComponent<ArabicText>();
            }

            // ---------------------------------------------------------------- one offer of the sheet
            {
                var row = new GameObject("QuickOfferRow", typeof(RectTransform));
                Rt(row).sizeDelta = new Vector2(764, 190);
                var rimg = row.AddComponent<Image>(); rimg.color = Color.clear; rimg.raycastTarget = true;
                var rbtn = row.AddComponent<Button>(); rbtn.targetGraphic = rimg; rbtn.transition = Selectable.Transition.None;
                Le(row, -1, 190);
                var h = row.AddComponent<HorizontalLayoutGroup>(); h.spacing = 25; h.padding = new RectOffset(0, 0, 12, 12); h.childAlignment = TextAnchor.MiddleCenter;
                h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;

                // the block button (left in the picture, as in the HTML)
                var blk = Go("Block", row.transform); var bi = blk.AddComponent<Image>(); bi.sprite = th.dotWhite; bi.color = new Color(ink.r, ink.g, ink.b, 0.09f);
                var bb = blk.AddComponent<Button>(); bb.targetGraphic = bi; bb.transition = Selectable.Transition.None; Le(blk, 72, 72); Le(blk).flexibleHeight = 0;
                Pic("Icon", blk.transform, icBlock, 44, 44, new Color32(0x6A, 0x5A, 0x3A, 255)); Place(Rt(blk.transform.Find("Icon").gameObject), new Vector2(.5f, .5f), Vector2.zero, new Vector2(44, 44));

                // texts
                var texts = Go("Texts", row.transform); Le(texts, -1, -1, 1);
                var vl = texts.AddComponent<VerticalLayoutGroup>(); vl.spacing = 4; vl.childAlignment = TextAnchor.MiddleRight; vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
                var title = Txt(texts.transform, "Title", "عنوان العرض", lalezar, 44, ink, TextAlignmentOptions.MidlineRight); NoWrap(title); Le(title.gameObject, -1, 58);
                var sub = Txt(texts.transform, "Sub", "ينتهي خلال ٠٢:١٤:٣٣", cairo, 27, soft, TextAlignmentOptions.MidlineRight); sub.fontStyle = FontStyles.Bold; NoWrap(sub); Le(sub.gameObject, -1, 40);
                var pr = Go("Price", texts.transform); Le(pr, -1, 56);
                var ph = pr.AddComponent<HorizontalLayoutGroup>(); ph.spacing = 12; ph.childAlignment = TextAnchor.MiddleRight; ph.childControlWidth = true; ph.childControlHeight = true; ph.childForceExpandWidth = false; ph.childForceExpandHeight = false;
                var was = Go("Was", pr.transform); var wh = was.AddComponent<HorizontalLayoutGroup>(); wh.childAlignment = TextAnchor.MiddleCenter; wh.childControlWidth = true; wh.childControlHeight = true; wh.childForceExpandWidth = false; wh.childForceExpandHeight = false;
                var wv = Txt(was.transform, "Value", "١٬٢٠٠", lalezar, 34, new Color(ink.r, ink.g, ink.b, 0.6f)); NoWrap(wv);
                var strike = Bar(wv.transform, "Strike", crimDark, 80, 4); { var sr = Rt(strike.gameObject); sr.anchorMin = sr.anchorMax = new Vector2(.5f, .5f); sr.anchoredPosition = Vector2.zero; }
                var pv = Txt(pr.transform, "Value", "٨٤٠", lalezar, 42, ink); NoWrap(pv);
                var coin = Go("Coin", pr.transform); var ci = Img(coin, th.coin); ci.raycastTarget = false; Le(coin, 38, 38);

                // the picture: a dark disc with a ring (red for a timed offer, green otherwise); the store's own art sits inside, smaller
                var icon = Go("Icon", row.transform); Le(icon, 150, 150); Le(icon).flexibleHeight = 0;
                var ring = Go("Ring", icon.transform); Stretch(Rt(ring)); var ri = Img(ring, th.dotWhite); ri.color = new Color32(0xC9, 0x32, 0x3A, 255); ri.preserveAspect = false;
                var inner = Go("Inner", icon.transform); Stretch(Rt(inner), 9, 9, 9, 9); var ii = Img(inner, th.dotWhite); ii.color = new Color32(0x14, 0x11, 0x0F, 255); ii.preserveAspect = false;
                var art = Go("Art", icon.transform); var ar = Rt(art); ar.anchorMin = ar.anchorMax = new Vector2(.5f, .5f); ar.sizeDelta = Vector2.zero; art.transform.localScale = Vector3.one * 0.55f;

                // dashed line under the row
                var line = Bar(row.transform, "Line", new Color(ink.r, ink.g, ink.b, 0.25f), 764, 3); line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                { var lr = Rt(line.gameObject); lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 0); lr.pivot = new Vector2(.5f, 0); lr.anchoredPosition = Vector2.zero; lr.sizeDelta = new Vector2(0, 3); }
                Save(row, "QuickOfferRow");
            }

            // ---------------------------------------------------------------- the menu
            var root = new GameObject("QuickMenu", typeof(RectTransform)); Stretch(Rt(root));
            var qm = root.AddComponent<KitQuickMenu>();

            // taps outside the open menu close it
            var catcher = Go("Catcher", root.transform); Stretch(Rt(catcher)); var cImg = catcher.AddComponent<Image>(); cImg.color = Color.clear;
            var cBtn = catcher.AddComponent<Button>(); cBtn.targetGraphic = cImg; cBtn.transition = Selectable.Transition.None;

            // the two edges that light up while the tab is carried
            var edges = Go("Edges", root.transform); Stretch(Rt(edges)); var edgesCg = edges.AddComponent<CanvasGroup>(); edgesCg.blocksRaycasts = false; edgesCg.interactable = false;
            Image Edge(string n, bool left)
            {
                var e = Go(n, edges.transform); var r = Rt(e);
                r.anchorMin = new Vector2(left ? 0 : 1, 0); r.anchorMax = new Vector2(left ? 0 : 1, 1); r.pivot = new Vector2(left ? 0 : 1, .5f);
                r.offsetMin = new Vector2(left ? 0 : -72, 249); r.offsetMax = new Vector2(left ? 72 : 0, -194);
                var img = e.AddComponent<Image>(); img.color = new Color(.73f, .56f, .28f, .12f); img.raycastTarget = false;
                var ln = Bar(e.transform, "Line", new Color(.73f, .56f, .28f, .55f), 6, 100); var lr = Rt(ln.gameObject);
                lr.anchorMin = new Vector2(left ? 1 : 0, 0); lr.anchorMax = new Vector2(left ? 1 : 0, 1); lr.pivot = new Vector2(left ? 1 : 0, .5f); lr.sizeDelta = new Vector2(6, 0); lr.anchoredPosition = Vector2.zero;
                return img;
            }
            var edgeLi = Edge("EdgeL", true); var edgeRi = Edge("EdgeR", false);
            var hint = Go("MoveHint", root.transform); var hr = Rt(hint); hr.anchorMin = hr.anchorMax = new Vector2(.5f, 1f); hr.pivot = new Vector2(.5f, 1f); hr.anchoredPosition = new Vector2(0, -255); hr.sizeDelta = new Vector2(720, 84);
            Img(hint, th.leatherDark, true, 2.4f); var hintCg = hint.AddComponent<CanvasGroup>(); hintCg.blocksRaycasts = false;
            { var t = Txt(hint.transform, "Label", "فلّته على أي حافة وبيلزق فيها", cairo, 32, th.leatherText); t.fontStyle = FontStyles.Bold; NoWrap(t); Stretch(Rt(t.gameObject), 16, 10, 16, 6); }

            // the menu itself (position is set by the script)
            var menu = Go("Menu", root.transform); var mr = Rt(menu); mr.anchorMin = mr.anchorMax = new Vector2(0, 1); mr.pivot = new Vector2(0, 1); mr.sizeDelta = new Vector2(83, 138);
            var menuCg = menu.AddComponent<CanvasGroup>();

            var dropBg = Go("DropBg", menu.transform); var dbr = Rt(dropBg); dbr.anchorMin = dbr.anchorMax = new Vector2(0, 1); dbr.pivot = new Vector2(.5f, 1); dbr.anchoredPosition = new Vector2(41.5f, -121); dbr.sizeDelta = new Vector2(83, 0);
            Img(dropBg, dropS, true, 2f).preserveAspect = false;
            var dropItems = Go("DropItems", menu.transform); var dir = Rt(dropItems); dir.anchorMin = dir.anchorMax = new Vector2(0, 1); dir.pivot = new Vector2(0, 1); dir.anchoredPosition = new Vector2(0, -121); dir.sizeDelta = new Vector2(83, 304);
            var dropCg = dropItems.AddComponent<CanvasGroup>();

            GameObject QuickButton(string n, Sprite icon, string label, string dotText, out GameObject dot, out ArabicText dotLabel, out RectTransform labelRt, out CanvasGroup labelCg, out Button button)
            {
                var b = Go(n, dropItems.transform); var r = Rt(b); r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(.5f, .5f); r.sizeDelta = new Vector2(76, 76);
                var bi = b.AddComponent<Image>(); bi.sprite = btnS; bi.raycastTarget = true; button = b.AddComponent<Button>(); button.targetGraphic = bi; button.transition = Selectable.Transition.None;
                var fx = b.AddComponent<KitButtonFx>(); SetRef(fx, "face", r);
                var ic = Pic("Icon", b.transform, icon, 44, 44, cream); Place(Rt(ic), new Vector2(.5f, .5f), Vector2.zero, new Vector2(44, 44));
                dotLabel = Badge(b.transform, out dot, dotText, 44);
                var lb = Go("Label", b.transform); labelRt = Rt(lb); labelRt.sizeDelta = new Vector2(190, 56); labelCg = lb.AddComponent<CanvasGroup>(); labelCg.blocksRaycasts = false;
                Img(lb, th.leatherDark, true, 2.4f);
                var lt = Txt(lb.transform, "Text", label, cairo, 29, th.leatherText); lt.fontStyle = FontStyles.Bold; NoWrap(lt); Stretch(Rt(lt.gameObject), 10, 8, 10, 4);
                return b;
            }
            var offers = QuickButton("Offers", icOffers, "العروض", "٣", out var offDot, out var offDotText, out var offLabel, out var offLabelCg, out var offersBtn);
            var events = QuickButton("Events", icEvents, "الأحداث", "١", out var evDot, out var evDotText, out var evLabel, out var evLabelCg, out var eventsBtn);
            // the countdown pill under the offers icon
            var time = Go("Time", offers.transform); var tmr = Rt(time); tmr.anchorMin = tmr.anchorMax = new Vector2(.5f, 0); tmr.pivot = new Vector2(.5f, .5f); tmr.anchoredPosition = new Vector2(0, -7); tmr.sizeDelta = new Vector2(80, 34);
            Img(time, th.slotDark, true, 2.6f).raycastTarget = false;
            var timeText = Txt(time.transform, "Text", "٠٢:١٤", lalezar, 24, th.brassLight); NoWrap(timeText); Stretch(Rt(timeText.gameObject), 4, 0, 4, 0);

            // the tab (pivot in the middle: it grows a little while it is carried)
            var tab = Go("Tab", menu.transform); var tr = Rt(tab); tr.anchorMin = tr.anchorMax = new Vector2(0, 1); tr.pivot = new Vector2(.5f, .5f); tr.anchoredPosition = new Vector2(41.5f, -69); tr.sizeDelta = new Vector2(83, 138);
            var face = Go("Face", tab.transform); Stretch(Rt(face)); var faceImg = face.AddComponent<Image>(); faceImg.sprite = tabS; faceImg.raycastTarget = true; faceImg.preserveAspect = false;
            var ringGo = Go("Ring", tab.transform); Stretch(Rt(ringGo)); var ringImg = ringGo.AddComponent<Image>(); ringImg.sprite = ringS; ringImg.raycastTarget = false; ringImg.color = Color.clear; ringImg.preserveAspect = false;
            var tabBtn = tab.AddComponent<Button>(); tabBtn.targetGraphic = faceImg; tabBtn.transition = Selectable.Transition.None;
            tab.AddComponent<KitQuickTab>();
            var chev = Pic("Chevron", tab.transform, icChev, 56, 56, new Color32(0x2A, 0x1A, 0x08, 255)); Place(Rt(chev), new Vector2(.5f, .5f), Vector2.zero, new Vector2(56, 56));
            var sumText = Badge(tab.transform, out var sumDot, "٤", 50); sumDot.AddComponent<CanvasGroup>().blocksRaycasts = false;

            // first-run hint
            var bubble = Go("Bubble", menu.transform); var bur = Rt(bubble); bur.sizeDelta = new Vector2(520, 84);
            Img(bubble, th.leatherDark, true, 2.4f); var bubbleCg = bubble.AddComponent<CanvasGroup>(); bubbleCg.blocksRaycasts = false;
            { var t = Txt(bubble.transform, "Label", "اسحبني وحطني وين ما بدك", cairo, 31, th.brassLight); t.fontStyle = FontStyles.Bold; NoWrap(t); Stretch(Rt(t.gameObject), 16, 10, 16, 6); }

            // ---------------------------------------------------------------- the offers sheet
            var scrim = Go("Scrim", root.transform); Stretch(Rt(scrim)); var sImg = scrim.AddComponent<Image>(); sImg.color = new Color(0.02f, 0.012f, 0.008f, 0.67f);
            var sBtn = scrim.AddComponent<Button>(); sBtn.targetGraphic = sImg; sBtn.transition = Selectable.Transition.None; var scrimCg = scrim.AddComponent<CanvasGroup>();

            var panel = Go("OffersPanel", root.transform); var pr0 = Rt(panel); pr0.anchorMin = pr0.anchorMax = new Vector2(1, 1); pr0.pivot = new Vector2(1, 1); pr0.sizeDelta = new Vector2(830, 0);
            Img(panel, th.paper, true, 1f, true);
            var panelCg = panel.AddComponent<CanvasGroup>();
            var panelV = panel.AddComponent<VerticalLayoutGroup>(); panelV.padding = new RectOffset(35, 35, 30, 40); panelV.spacing = 8; panelV.childAlignment = TextAnchor.UpperCenter;
            panelV.childControlWidth = true; panelV.childControlHeight = true; panelV.childForceExpandWidth = true; panelV.childForceExpandHeight = false;
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var r1 = Bar(panel.transform, "Rule1", ink, 760, 5); Le(r1.gameObject, -1, 5);
            var pt = Txt(panel.transform, "Title", "عروض الليلة", lalezar, 60, ink); NoWrap(pt); Le(pt.gameObject, -1, 80);
            var ps = Txt(panel.transform, "Sub", "اضغط على العرض لتشتريه، أو احظره لو ما بدك إياه", cairo, 26, crimDark); ps.fontStyle = FontStyles.Bold; NoWrap(ps); Le(ps.gameObject, -1, 38);
            var r2 = Bar(panel.transform, "Rule2", ink, 760, 5); Le(r2.gameObject, -1, 5);
            var rowsGo = Go("Rows", panel.transform); var rv = rowsGo.AddComponent<VerticalLayoutGroup>(); rv.spacing = 0; rv.childControlWidth = true; rv.childControlHeight = true; rv.childForceExpandWidth = true; rv.childForceExpandHeight = false;
            var empty = Txt(panel.transform, "Empty", "ما في عروض هلأ. بتظهر هون لما ينزل عرض جديد.", cairo, 34, soft, TextAlignmentOptions.Center, true); empty.fontStyle = FontStyles.Bold; Le(empty.gameObject, -1, 150);
            var openPaper = MakeButton("OpenPaper", th.leatherRed, new Color32(0xF0, 0xDC, 0xD8, 255), "افتح الجريدة كاملة", cairo, 40);
            openPaper.transform.SetParent(panel.transform, false); Le(openPaper, -1, 122);
            var tip = Txt(panel.transform, "Tip", "الحظر بيخفي هالعرض عنك لحد ما ينتهي، وما بيأثر على باقي العروض.", cairo, 24, soft, TextAlignmentOptions.Top, true); tip.fontStyle = FontStyles.Bold; Le(tip.gameObject, -1, 72);

            // ---------------------------------------------------------------- the undo toast
            var toast = Go("Toast", root.transform); var tor = Rt(toast); tor.anchorMin = tor.anchorMax = new Vector2(.5f, 0); tor.pivot = new Vector2(.5f, 0); tor.anchoredPosition = new Vector2(0, 268); tor.sizeDelta = new Vector2(900, 108);
            Img(toast, th.leatherDark, true, 2.4f, true); var toastCg = toast.AddComponent<CanvasGroup>();
            var th0 = toast.AddComponent<HorizontalLayoutGroup>(); th0.spacing = 40; th0.childAlignment = TextAnchor.MiddleCenter; th0.padding = new RectOffset(44, 44, 14, 20);
            th0.childControlWidth = true; th0.childControlHeight = true; th0.childForceExpandWidth = false; th0.childForceExpandHeight = false;
            var undo = Go("Undo", toast.transform); var ui = undo.AddComponent<Image>(); ui.color = Color.clear; var ub = undo.AddComponent<Button>(); ub.targetGraphic = ui; ub.transition = Selectable.Transition.None; Le(undo);
            { var t = Txt(undo.transform, "Label", "تراجع", cairo, 36, th.brassLight); t.fontStyle = FontStyles.Bold | FontStyles.Underline; NoWrap(t); }
            var toastText = Txt(toast.transform, "Text", "ما رح يظهر لك هالعرض بعد هلأ", cairo, 34, th.ivory); toastText.fontStyle = FontStyles.Bold; NoWrap(toastText);

            QmSetRefs(qm,
                ("menu", mr), ("tabRt", tr), ("dropBg", dbr), ("dropItems", dir), ("chevron", Rt(chev)), ("sumDot", Rt(sumDot)), ("offersRt", Rt(offers)), ("eventsRt", Rt(events)),
                ("offDot", Rt(offDot)), ("evDot", Rt(evDot)), ("offTime", tmr), ("offLabel", offLabel), ("evLabel", evLabel), ("bubble", bur), ("panel", pr0), ("rowsRoot", Rt(rowsGo)), ("toast", tor),
                ("tabImage", faceImg), ("ringImage", ringImg), ("edgeL", edgeLi), ("edgeR", edgeRi), ("tabSprite", tabS), ("pillSprite", pillS),
                ("sumText", sumText), ("offDotText", offDotText), ("evDotText", evDotText), ("offTimeText", timeText.GetComponent<ArabicText>()), ("toastText", toastText.GetComponent<ArabicText>()),
                ("tabButton", tabBtn), ("offersButton", offersBtn), ("eventsButton", eventsBtn), ("scrimButton", sBtn), ("openPaperButton", openPaper.GetComponent<Button>()), ("undoButton", ub), ("catcher", cBtn),
                ("menuGroup", menuCg), ("dropItemsGroup", dropCg), ("edgesGroup", edgesCg), ("hintGroup", hintCg), ("offLabelGroup", offLabelCg), ("evLabelGroup", evLabelCg),
                ("bubbleGroup", bubbleCg), ("panelGroup", panelCg), ("scrimGroup", scrimCg), ("toastGroup", toastCg),
                ("rowPrefab", LoadPrefab("QuickOfferRow")), ("emptyState", empty.gameObject), ("openPaperGo", openPaper));
            th.quickMenuPrefab = Save(root, "QuickMenu");
            EditorUtility.SetDirty(th);
        }
    }
}
