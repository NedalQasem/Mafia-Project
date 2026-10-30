using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// The offers banner of Docs/mockups/launch-banner.html as two prefabs: "LaunchAdPage" (one offer, a page of the carousel) and "LaunchBanner"
    /// (scrim, light cone, the newspaper sheet, brass close button, "not today"). Sizes are the HTML ones times 2.769 (390 px phone -> 1080 canvas).
    /// </summary>
    public static partial class KitBuilder
    {
        [MenuItem("Mafia Night/UI Kit/Build Launch Banner")]
        public static void BuildLaunchBannerMenu()
        {
            var th = BuildTheme();
            headFont = th.fontHeading;
            BuildLaunchBanner(th);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIKit] built the launch banner prefabs");
        }

        private static Image Bar(Transform parent, string name, Color c, float w, float h)
        {
            var g = Go(name, parent); var i = g.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
            Rt(g).sizeDelta = new Vector2(w, h);
            return i;
        }

        /// <summary>A "&gt;" or "&lt;" drawn with two bars (dir 1 points right, -1 points left).</summary>
        private static GameObject Chevron(Transform parent, string name, Color c, int dir, float len = 34f, float thick = 9f)
        {
            var root = Go(name, parent);
            for (int s = -1; s <= 1; s += 2)
            {
                var b = Bar(root.transform, "Bar", c, len, thick);
                var r = Rt(b.gameObject); r.anchoredPosition = new Vector2(-dir * len * 0.18f, s * len * 0.26f);
                r.localRotation = Quaternion.Euler(0, 0, -s * dir * 45f);
            }
            return root;
        }

        private static void BuildLaunchBanner(KitTheme th)
        {
            var ink = (Color)th.paperInk; var crim = new Color32(0xC0, 0x32, 0x3A, 255); var crimDark = (Color)th.crimson;
            var lalezar = th.fontLalezar != null ? th.fontLalezar : th.fontHeading; var cairo = th.fontBody;
            var soft = new Color32(0x4A, 0x3C, 0x24, 255);

            // ---------------------------------------------------------------- one offer (a page of the carousel)
            {
                var page = new GameObject("LaunchAdPage", typeof(RectTransform));
                { var pr0 = Rt(page); pr0.anchorMin = Vector2.zero; pr0.anchorMax = Vector2.one; pr0.sizeDelta = Vector2.zero; }   // stretches over the pages area
                page.AddComponent<CanvasGroup>();
                var v = page.AddComponent<VerticalLayoutGroup>(); v.spacing = 14; v.childAlignment = TextAnchor.UpperCenter;
                v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;

                // kicker: crimson title between two hairlines (HTML .kick)
                var mast = Go("Mast", page.transform);
                var mh = mast.AddComponent<HorizontalLayoutGroup>(); mh.spacing = 22; mh.childAlignment = TextAnchor.MiddleCenter; mh.childControlWidth = true; mh.childControlHeight = true; mh.childForceExpandWidth = false; mh.childForceExpandHeight = false;
                mast.AddComponent<LayoutElement>().preferredHeight = 58;
                for (int side = 0; side < 3; side++)
                {
                    if (side == 1)
                    {
                        var k = Txt(mast.transform, "Right", "عرض الليلة فقط", lalezar, 42, crimDark, TextAlignmentOptions.Center);
                        k.overflowMode = TextOverflowModes.Overflow; k.textWrappingMode = TextWrappingModes.NoWrap;
                    }
                    else
                    {
                        var line = Bar(mast.transform, "Line", new Color(ink.r, ink.g, ink.b, 0.55f), 100, 4);
                        var le = line.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1; le.preferredHeight = 4; le.minWidth = 20;
                    }
                }

                // the picture: it takes whatever height the other parts leave (as in the HTML flex column)
                var art = Go("Art", page.transform); var ale = art.AddComponent<LayoutElement>(); ale.flexibleHeight = 1; ale.minHeight = 440;
                art.transform.localScale = Vector3.one * 1.5f;   // the store draws its pictures for 76 px frames, the banner shows 140 px ones

                var hl = Txt(page.transform, "Headline", "عنوان العرض\nسطر ثانٍ", lalezar, 93, ink, TextAlignmentOptions.Center);
                hl.overflowMode = TextOverflowModes.Overflow; hl.textWrappingMode = TextWrappingModes.NoWrap; hl.lineSpacing = -50f;
                hl.gameObject.AddComponent<LayoutElement>().preferredHeight = 230;
                var bd = Txt(page.transform, "Body", "نص العرض", cairo, 35, new Color32(0x3A, 0x30, 0x20, 255), TextAlignmentOptions.Top, true);
                bd.fontStyle = FontStyles.Bold;
                bd.gameObject.AddComponent<LayoutElement>().preferredHeight = 104;

                // countdown: "ينتهي خلال ٠٢:١٤:٣٣"
                var cd = Go("Countdown", page.transform);
                var ch = cd.AddComponent<HorizontalLayoutGroup>(); ch.spacing = 16; ch.childAlignment = TextAnchor.MiddleCenter; ch.childControlWidth = true; ch.childControlHeight = true; ch.childForceExpandWidth = false; ch.childForceExpandHeight = false;
                cd.AddComponent<LayoutElement>().preferredHeight = 66;
                var cv = Txt(cd.transform, "Value", "٠٢:١٤:٣٣", lalezar, 55, ink, TextAlignmentOptions.Center); cv.overflowMode = TextOverflowModes.Overflow; cv.textWrappingMode = TextWrappingModes.NoWrap;
                var cl = Txt(cd.transform, "Label", "ينتهي خلال", cairo, 33, crimDark, TextAlignmentOptions.Center); cl.fontStyle = FontStyles.Bold; cl.overflowMode = TextOverflowModes.Overflow; cl.textWrappingMode = TextWrappingModes.NoWrap;

                // the normal price, crossed out
                var was = Go("Was", page.transform);
                var wh = was.AddComponent<HorizontalLayoutGroup>(); wh.spacing = 14; wh.childAlignment = TextAnchor.MiddleCenter; wh.childControlWidth = true; wh.childControlHeight = true; wh.childForceExpandWidth = false; wh.childForceExpandHeight = false;
                was.AddComponent<LayoutElement>().preferredHeight = 58;
                var wv = Txt(was.transform, "Value", "١٬٢٠٠", lalezar, 44, ink, TextAlignmentOptions.Center); wv.overflowMode = TextOverflowModes.Overflow; wv.textWrappingMode = TextWrappingModes.NoWrap;
                var strike = Bar(wv.transform, "Strike", crimDark, 100, 5);
                var sr = Rt(strike.gameObject); sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f); sr.anchoredPosition = Vector2.zero;
                var wl = Txt(was.transform, "Label", "السعر العادي", cairo, 34, soft, TextAlignmentOptions.Center); wl.fontStyle = FontStyles.Bold; wl.overflowMode = TextOverflowModes.Overflow; wl.textWrappingMode = TextWrappingModes.NoWrap;

                // buy: [price] [coin] اشتريه الآن
                var buy = MakeButton("Buy", th.leatherRed, th.ivory, "", th.fontHeading, 56);
                buy.transform.SetParent(page.transform, false);
                buy.AddComponent<LayoutElement>().preferredHeight = 136;
                var face = buy.transform.Find("Face");
                Object.DestroyImmediate(face.Find("Label").gameObject);
                var row = Go("Row", face); Stretch(Rt(row));
                var rh = row.AddComponent<HorizontalLayoutGroup>(); rh.spacing = 16; rh.childAlignment = TextAnchor.MiddleCenter; rh.childControlWidth = true; rh.childControlHeight = true; rh.childForceExpandWidth = false; rh.childForceExpandHeight = false; rh.padding = new RectOffset(20, 20, 10, 18);
                var price = Txt(row.transform, "Price", "٨٤٠", cairo, 46, new Color32(0xF0, 0xDC, 0xD8, 255)); price.fontStyle = FontStyles.Bold; price.overflowMode = TextOverflowModes.Overflow; price.textWrappingMode = TextWrappingModes.NoWrap;
                var coin = Go("Coin", row.transform); var coinImg = Img(coin, th.coin); coinImg.raycastTarget = false; coin.AddComponent<LayoutElement>().preferredWidth = 48; coin.GetComponent<LayoutElement>().preferredHeight = 48;
                var verb = Txt(row.transform, "Verb", "اشتريه الآن", cairo, 46, new Color32(0xF0, 0xDC, 0xD8, 255)); verb.fontStyle = FontStyles.Bold; verb.overflowMode = TextOverflowModes.Overflow; verb.textWrappingMode = TextWrappingModes.NoWrap;

                // "see it in the store": underlined link
                var more = Go("More", page.transform);
                var mimg = more.AddComponent<Image>(); mimg.color = Color.clear; mimg.raycastTarget = true;
                var mbtn = more.AddComponent<Button>(); mbtn.targetGraphic = mimg; mbtn.transition = Selectable.Transition.None;
                more.AddComponent<LayoutElement>().preferredHeight = 78;
                var mt = Txt(more.transform, "Label", "شوف في المتجر", cairo, 34, soft); mt.fontStyle = FontStyles.Bold | FontStyles.Underline; Stretch(Rt(mt.gameObject));

                // page turn shade and the stamp sit above everything and ignore the column
                var shade = Go("Shade", page.transform); Stretch(Rt(shade));
                var shI = shade.AddComponent<Image>(); shI.color = new Color(0, 0, 0, 0); shI.raycastTarget = false; shade.AddComponent<LayoutElement>().ignoreLayout = true;
                var st = Go("Stamp", page.transform); var str = Rt(st); str.anchorMin = str.anchorMax = new Vector2(0, 1); str.pivot = new Vector2(0.5f, 0.5f); str.anchoredPosition = new Vector2(150, -170); str.sizeDelta = new Vector2(222, 112);
                st.transform.localRotation = Quaternion.Euler(0, 0, 12f);
                st.AddComponent<LayoutElement>().ignoreLayout = true;
                var sti = Img(st, th.stampFrame, true, 1f); sti.color = crim;
                var stt = Txt(st.transform, "T", "−٣٠٪", lalezar, 66, crim); stt.overflowMode = TextOverflowModes.Overflow; stt.textWrappingMode = TextWrappingModes.NoWrap; Stretch(Rt(stt.gameObject), 6, 6, 6, 6);
                Save(page, "LaunchAdPage");
            }

            // ---------------------------------------------------------------- the banner
            var root = new GameObject("LaunchBanner", typeof(RectTransform));
            Stretch(Rt(root));
            var banner = root.AddComponent<KitLaunchBanner>();

            // scrim (a tap on the dark area closes it)
            var scrim = Go("Scrim", root.transform); Stretch(Rt(scrim));
            var scrimImg = scrim.AddComponent<Image>(); scrimImg.color = new Color(0.02f, 0.012f, 0.008f, 0.86f);   // the HTML dims the home screen and adds 72% black on top
            var scrimBtn = scrim.AddComponent<Button>(); scrimBtn.targetGraphic = scrimImg; scrimBtn.transition = Selectable.Transition.None;
            var scrimCg = scrim.AddComponent<CanvasGroup>();

            // light cone from the top
            var cone = Go("Cone", root.transform); var cr = Rt(cone); cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 1f); cr.pivot = new Vector2(0.5f, 1f); cr.anchoredPosition = new Vector2(0, 55); cr.sizeDelta = new Vector2(940, 1550);
            var coneImg = Img(cone, th.lampCone); coneImg.preserveAspect = false; coneImg.raycastTarget = false; coneImg.color = new Color(1f, 0.89f, 0.66f, 0.55f);
            var coneCg = cone.AddComponent<CanvasGroup>(); coneCg.blocksRaycasts = false;

            // the newspaper sheet, a little crooked
            var sheet = Go("Sheet", root.transform); var shr = Rt(sheet);
            shr.anchorMin = new Vector2(0.5f, 0f); shr.anchorMax = new Vector2(0.5f, 1f); shr.pivot = new Vector2(0.5f, 0.5f);
            shr.sizeDelta = new Vector2(970, -(194 + 299)); shr.anchoredPosition = new Vector2(0, (299 - 194) * 0.5f);   // 70 px from the top and 108 px from the bottom of the HTML phone
            shr.localRotation = Quaternion.Euler(0, 0, 1f);
            Img(sheet, th.paper, true, 1f, true);
            sheet.AddComponent<CanvasGroup>();

            // masthead: double rule, title, small line with the number of offers
            var mastHead = Go("Masthead", sheet.transform); var mhr = Rt(mastHead); mhr.anchorMin = mhr.anchorMax = new Vector2(0.5f, 1f); mhr.pivot = new Vector2(0.5f, 1f); mhr.anchoredPosition = new Vector2(0, -28); mhr.sizeDelta = new Vector2(892, 200);
            Image Rule(string n, float y, float h) { var b = Bar(mastHead.transform, n, ink, 892, h); var r = Rt(b.gameObject); r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 1f); r.anchoredPosition = new Vector2(0, -y); return b; }
            Rule("TopRule1", 0, 3); Rule("TopRule2", 6, 3);
            var title = Txt(mastHead.transform, "Title", "جريدة الليل", lalezar, 104, ink, TextAlignmentOptions.Center); title.overflowMode = TextOverflowModes.Overflow; title.textWrappingMode = TextWrappingModes.NoWrap;
            { var r = Rt(title.gameObject); r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 1f); r.anchoredPosition = new Vector2(0, -10); r.sizeDelta = new Vector2(892, 120); }
            Rule("MetaRule", 134, 4);
            var metaRight = Txt(mastHead.transform, "MetaRight", "العدد الخاص · عروض الليلة", cairo, 27, ink, TextAlignmentOptions.MidlineRight); metaRight.fontStyle = FontStyles.Bold;
            { var r = Rt(metaRight.gameObject); r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f); r.anchoredPosition = new Vector2(-8, -142); r.sizeDelta = new Vector2(600, 40); }
            var metaLeft = Txt(mastHead.transform, "MetaLeft", "٣ عروض", cairo, 27, crimDark, TextAlignmentOptions.MidlineLeft); metaLeft.fontStyle = FontStyles.Bold;
            { var r = Rt(metaLeft.gameObject); r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f); r.anchoredPosition = new Vector2(8, -142); r.sizeDelta = new Vector2(260, 40); }
            Rule("BottomRule1", 186, 3); Rule("BottomRule2", 192, 3);

            // carousel: pages, the thin red timer, the dots, and the two arrows
            var car = Go("Carousel", sheet.transform); var carr = Rt(car); Stretch(carr, 39, 39, 39, 250);
            var carHit = car.AddComponent<Image>(); carHit.color = Color.clear;
            var pagesRoot = Go("Pages", car.transform); var pgr = Rt(pagesRoot); Stretch(pgr, 0, 100, 0, 0);
            var track = Go("Progress", car.transform); var tr = Rt(track); tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f); tr.pivot = new Vector2(0.5f, 0f); tr.anchoredPosition = new Vector2(0, 66); tr.sizeDelta = new Vector2(0, 8);
            var trImg = track.AddComponent<Image>(); trImg.color = new Color(ink.r, ink.g, ink.b, 0.14f); trImg.raycastTarget = false;
            var fill = Go("Fill", track.transform); Stretch(Rt(fill)); var fi = fill.AddComponent<Image>(); fi.color = new Color32(0xB0, 0x2A, 0x32, 255); fi.raycastTarget = false;
            var dotsRoot = Go("Dots", car.transform); var dr = Rt(dotsRoot); dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0f); dr.pivot = new Vector2(0.5f, 0f); dr.anchoredPosition = new Vector2(0, 14); dr.sizeDelta = new Vector2(300, 36);
            var dl = dotsRoot.AddComponent<HorizontalLayoutGroup>(); dl.spacing = 19; dl.childAlignment = TextAnchor.MiddleCenter; dl.childControlWidth = false; dl.childControlHeight = false; dl.childForceExpandWidth = false;
            var arrowCol = new Color(ink.r, ink.g, ink.b, 0.55f);
            Button Arrow(string n, int dirX)
            {
                var a = Go(n, car.transform); var ar = Rt(a); ar.anchorMin = ar.anchorMax = new Vector2(dirX > 0 ? 1f : 0f, 0.5f); ar.pivot = new Vector2(dirX > 0 ? 1f : 0f, 0.5f); ar.anchoredPosition = new Vector2(dirX > 0 ? 24 : -24, 50); ar.sizeDelta = new Vector2(84, 120);
                var ai = a.AddComponent<Image>(); ai.color = Color.clear;
                var ab = a.AddComponent<Button>(); ab.targetGraphic = ai; ab.transition = Selectable.Transition.None;
                var chev = Chevron(a.transform, "Chevron", arrowCol, dirX > 0 ? 1 : -1, 48f, 9f); Place(Rt(chev), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
                return ab;
            }
            var prev = Arrow("Prev", 1); var next = Arrow("Next", -1);
            var carousel = car.AddComponent<StoreAdCarousel>();
            SetRef(carousel, "progress", Rt(fill)); SetRef(carousel, "pagesRoot", pgr); SetRef(carousel, "dotsRoot", dr);
            SetRef(carousel, "pagePrefab", LoadPrefab("LaunchAdPage")); SetRef(carousel, "dotPrefab", LoadPrefab("StoreAdDot"));
            { var so = new SerializedObject(carousel); so.FindProperty("period").floatValue = 6f; so.FindProperty("pageTilt").floatValue = 0f; so.ApplyModifiedPropertiesWithoutUndo(); }

            // brass close button on the corner of the page
            var close = Go("Close", root.transform); var clr = Rt(close); clr.anchorMin = clr.anchorMax = new Vector2(0f, 1f); clr.pivot = new Vector2(0f, 1f); clr.anchoredPosition = new Vector2(22, -160); clr.sizeDelta = new Vector2(105, 105);
            var clImg = Img(close, th.coin, false, 1f, true); clImg.preserveAspect = true;
            var clBtn = close.AddComponent<Button>(); clBtn.targetGraphic = clImg; clBtn.transition = Selectable.Transition.None;
            close.AddComponent<CanvasGroup>();
            var clFx = close.AddComponent<KitButtonFx>(); SetRef(clFx, "face", clr);
            for (int s = -1; s <= 1; s += 2) { var b = Bar(close.transform, "X", new Color32(0x2A, 0x1A, 0x08, 255), 46, 9); Rt(b.gameObject).localRotation = Quaternion.Euler(0, 0, s * 45f); }

            // below the page: "not today" and the honest note
            var below = Go("Below", root.transform); var br = Rt(below); br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f); br.pivot = new Vector2(0.5f, 0f); br.anchoredPosition = new Vector2(0, 72); br.sizeDelta = new Vector2(940, 172);
            var belowCg = below.AddComponent<CanvasGroup>();
            var skip = Go("Skip", below.transform); var skr = Rt(skip); skr.anchorMin = skr.anchorMax = new Vector2(0.5f, 1f); skr.pivot = new Vector2(0.5f, 1f); skr.anchoredPosition = Vector2.zero; skr.sizeDelta = new Vector2(620, 112);
            var skImg = Img(skip, th.leatherDark, true, 1f, true);
            var skBtn = skip.AddComponent<Button>(); skBtn.targetGraphic = skImg; skBtn.transition = Selectable.Transition.None;
            var skFx = skip.AddComponent<KitButtonFx>(); SetRef(skFx, "face", skr);
            var skh = skip.AddComponent<HorizontalLayoutGroup>(); skh.spacing = 25; skh.childAlignment = TextAnchor.MiddleCenter; skh.childControlWidth = true; skh.childControlHeight = true; skh.childForceExpandWidth = false; skh.childForceExpandHeight = false; skh.padding = new RectOffset(44, 44, 20, 24);
            skip.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var skLabel = Txt(skip.transform, "Label", "لا تعرضها لي اليوم", cairo, 36, th.ivory, TextAlignmentOptions.Center); skLabel.fontStyle = FontStyles.Bold; skLabel.overflowMode = TextOverflowModes.Overflow; skLabel.textWrappingMode = TextWrappingModes.NoWrap;
            var box = Go("Box", skip.transform); var bxLe = box.AddComponent<LayoutElement>(); bxLe.preferredWidth = 56; bxLe.preferredHeight = 56;
            var boxImg = Img(box, th.slotDark, true, 1f); boxImg.raycastTarget = false;
            var tick = Go("Tick", box.transform); Stretch(Rt(tick));
            { var b1 = Bar(tick.transform, "Short", th.brassLight, 20, 9); var r1 = Rt(b1.gameObject); r1.anchoredPosition = new Vector2(-9, -4); r1.localRotation = Quaternion.Euler(0, 0, -45f);
              var b2 = Bar(tick.transform, "Long", th.brassLight, 36, 9); var r2 = Rt(b2.gameObject); r2.anchoredPosition = new Vector2(7, 2); r2.localRotation = Quaternion.Euler(0, 0, 45f); }
            tick.SetActive(false);
            var fine = Txt(below.transform, "Fine", "العروض كلها للزينة والكوينز، وما بتعطي ميزة داخل المباراة.", cairo, 26, new Color32(0x8D, 0x85, 0x76, 255), TextAlignmentOptions.Top, true);
            { var r = Rt(fine.gameObject); r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 1f); r.anchoredPosition = new Vector2(0, -124); r.sizeDelta = new Vector2(880, 48); }

            SetRef(banner, "carousel", carousel); SetRef(banner, "sheet", shr); SetRef(banner, "closeButtonRect", clr); SetRef(banner, "below", br);
            SetRef(banner, "scrim", scrimCg); SetRef(banner, "cone", coneCg); SetRef(banner, "belowGroup", belowCg);
            SetRef(banner, "closeButton", clBtn); SetRef(banner, "scrimButton", scrimBtn); SetRef(banner, "skipButton", skBtn); SetRef(banner, "prevButton", prev); SetRef(banner, "nextButton", next);
            SetRef(banner, "skipTick", tick); SetRef(banner, "dotsRoot", dotsRoot); SetRef(banner, "progressTrack", track); SetRef(banner, "countLabel", metaLeft.GetComponent<ArabicText>());
            th.launchBannerPrefab = Save(root, "LaunchBanner");
            EditorUtility.SetDirty(th);
        }
    }
}
