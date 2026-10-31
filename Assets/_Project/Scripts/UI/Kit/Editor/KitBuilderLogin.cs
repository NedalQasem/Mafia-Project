using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// The login screens of Docs/mockups/login.html as two prefabs: "LoginChoice" (logo, Google / Facebook / Apple plates, guest, office link) and
    /// "AccountSheet" (Settings → الحساب: the kind of account and one row per way to come back).
    /// </summary>
    public static partial class KitBuilder
    {
        [MenuItem("Mafia Night/UI Kit/Build Login Screens")]
        public static void BuildLoginMenu()
        {
            var th = BuildTheme();
            headFont = th.fontHeading;
            EnsureEventSprites();
            th.recoverySheetPrefab = LoadPrefab("RecoverySheet");          // the office login reuses the settings sheet
            EnsureBrandSprites();
            BuildLoginChoice(th);
            BuildAccountSheet(th);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIKit] built the login screens");
        }

        // the round logos of the three companies, made from Tools/brand-icons (their own marks, not letters in a circle)
        private static readonly string[] BrandNames = { "brand_google", "brand_facebook", "brand_apple" };

        internal static void EnsureBrandSprites()
        {
            foreach (var n in BrandNames)
            {
                var imp = AssetImporter.GetAtPath(Art + n + ".png") as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.filterMode = FilterMode.Bilinear; imp.spritePixelsPerUnit = 100;
                imp.wrapMode = TextureWrapMode.Clamp; imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
        }

        private static GameObject Logo(Transform parent, string name, Sprite logo, float size)
        {
            var g = Go(name, parent); Rt(g).sizeDelta = new Vector2(size, size);
            var i = g.AddComponent<Image>(); i.sprite = logo; i.preserveAspect = true; i.raycastTarget = false;
            return g;
        }

        private static GameObject Circle(Transform parent, string name, Sprite dot, Color fill, string letter, Color letterColor, TMP_FontAsset font, float size, float letterSize)
        {
            var g = Go(name, parent); Rt(g).sizeDelta = new Vector2(size, size);
            var i = g.AddComponent<Image>(); i.sprite = dot; i.color = fill; i.raycastTarget = false;
            var t = Txt(g.transform, "Letter", letter, font, letterSize, letterColor); t.fontStyle = FontStyles.Bold; t.overflowMode = TextOverflowModes.Overflow; t.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(Rt(t.gameObject));
            return g;
        }

        private static void BuildLoginChoice(KitTheme th)
        {
            var lalezar = th.fontLalezar != null ? th.fontLalezar : th.fontHeading; var cairo = th.fontBody;
            var ink = (Color)th.paperInk; var brass = new Color32(0xB9, 0x90, 0x48, 255); var dim = new Color32(0x8A, 0x82, 0x72, 255);
            var dot = th.dotWhite;

            void At(GameObject g, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null) { var r = Rt(g); r.anchorMin = r.anchorMax = anchor; r.pivot = pivot ?? new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size; }
            GameObject Plate(Transform parent, string name, Sprite face, Color text, string label, Sprite logo, Color ic, string letter, Color letterColor, float y, float h)
            {
                var b = MakeButton(name, face, text, label, lalezar, 54); b.transform.SetParent(parent, false);
                At(b, new Vector2(.5f, 1f), new Vector2(0, y), new Vector2(900, h), new Vector2(.5f, 1f));
                var lbl = b.transform.Find("Face/Label"); Rt(lbl.gameObject).offsetMax = new Vector2(-150, Rt(lbl.gameObject).offsetMax.y); Rt(lbl.gameObject).offsetMin = new Vector2(60, Rt(lbl.gameObject).offsetMin.y);
                var c = logo != null ? Logo(b.transform, "Icon", logo, 92) : Circle(b.transform, "Icon", dot, ic, letter, letterColor, th.fontHeading, 84, 52); At(c, new Vector2(1f, .5f), new Vector2(-84, 6), new Vector2(logo != null ? 92 : 84, logo != null ? 92 : 84));
                return b;
            }
            GameObject Soon(Transform parent)
            {
                var s = Go("Soon", parent); At(s, new Vector2(0f, .5f), new Vector2(110, 6), new Vector2(150, 52));
                var si = s.AddComponent<Image>(); si.sprite = th.chipSheet != null ? th.chipSheet : dot; si.color = new Color(.04f, .03f, .03f, .92f); si.raycastTarget = false;
                var st = Txt(s.transform, "Text", "قريباً", cairo, 28, new Color32(0xE6, 0xD9, 0xBD, 255)); st.fontStyle = FontStyles.Bold; Stretch(Rt(st.gameObject));
                return s;
            }

            var root = new GameObject("LoginChoice", typeof(RectTransform)); Stretch(Rt(root));
            var view = root.AddComponent<KitLoginChoice>();
            var cg = root.AddComponent<CanvasGroup>();

            var bg = Go("Background", root.transform); Stretch(Rt(bg)); var bgi = bg.AddComponent<Image>(); bgi.color = new Color32(0x0B, 0x08, 0x0C, 255); bgi.raycastTarget = true;
            var haze = Go("Haze", root.transform); Stretch(Rt(haze)); haze.AddComponent<Image>().color = new Color(.45f, .1f, .12f, .04f); haze.GetComponent<Image>().raycastTarget = false;

            var content = Go("Content", root.transform); Stretch(Rt(content));

            var logo = Txt(content.transform, "Logo", "المافيا", lalezar, 230, th.ivory, TextAlignmentOptions.Center); logo.overflowMode = TextOverflowModes.Overflow; logo.textWrappingMode = TextWrappingModes.NoWrap;
            At(logo.gameObject, new Vector2(.5f, 1f), new Vector2(0, -250), new Vector2(980, 280), new Vector2(.5f, 1f));
            var tag = Txt(content.transform, "Tag", "الليلة لا ترحم", cairo, 44, brass, TextAlignmentOptions.Center); tag.fontStyle = FontStyles.Bold; tag.overflowMode = TextOverflowModes.Overflow; tag.textWrappingMode = TextWrappingModes.NoWrap;
            At(tag.gameObject, new Vector2(.5f, 1f), new Vector2(0, -540), new Vector2(900, 70), new Vector2(.5f, 1f));

            var google = Plate(content.transform, "Google", th.leatherRed, th.ivory, "الدخول بحساب Google", Spr("brand_google"), new Color32(0xF4, 0xEF, 0xE3, 255), "G", new Color32(0xD1, 0x48, 0x36, 255), -820, 160);
            var facebook = Plate(content.transform, "Facebook", th.leatherDark, th.ivory, "الدخول بحساب Facebook", Spr("brand_facebook"), new Color32(0x18, 0x77, 0xF2, 255), "f", Color.white, -1000, 160);
            var apple = Plate(content.transform, "Apple", th.leatherDark, th.ivory, "الدخول بحساب Apple", Spr("brand_apple"), new Color32(0x11, 0x11, 0x11, 255), "A", Color.white, -1180, 160);
            var googleSoon = Soon(google.transform); var facebookSoon = Soon(facebook.transform); var appleSoon = Soon(apple.transform);
            googleSoon.SetActive(false); facebookSoon.SetActive(false); appleSoon.SetActive(true);

            var or = Txt(content.transform, "Or", "أو", cairo, 32, dim, TextAlignmentOptions.Center); or.fontStyle = FontStyles.Bold; At(or.gameObject, new Vector2(.5f, 1f), new Vector2(0, -1400), new Vector2(200, 50), new Vector2(.5f, 1f));
            { var l1 = Bar(content.transform, "OrL", new Color32(0x3A, 0x32, 0x2A, 255), 340, 3); At(l1.gameObject, new Vector2(.5f, 1f), new Vector2(-300, -1425), new Vector2(340, 3), new Vector2(.5f, 1f));
              var l2 = Bar(content.transform, "OrR", new Color32(0x3A, 0x32, 0x2A, 255), 340, 3); At(l2.gameObject, new Vector2(.5f, 1f), new Vector2(300, -1425), new Vector2(340, 3), new Vector2(.5f, 1f)); }

            var guest = Plate(content.transform, "Guest", th.btnGold, ink, "ادخل ضيف", null, new Color32(0xF4, 0xEF, 0xE3, 255), "ض", ink, -1480, 150);
            var office = Go("Office", content.transform); At(office, new Vector2(.5f, 1f), new Vector2(0, -1700), new Vector2(900, 90), new Vector2(.5f, 1f));
            var oi = office.AddComponent<Image>(); oi.color = new Color(0, 0, 0, 0); oi.raycastTarget = true;
            var ob = office.AddComponent<Button>(); ob.targetGraphic = oi; ob.transition = Selectable.Transition.None;
            var ot = Txt(office.transform, "Text", "عندي حساب من مكتب المافيا", cairo, 40, new Color32(0xB9, 0xA9, 0x8A, 255)); ot.fontStyle = FontStyles.Bold | FontStyles.Underline; Stretch(Rt(ot.gameObject));
            var fine = Txt(content.transform, "Fine", "الضيف بيلعب فوراً. اربط حسابك لاحقاً من الإعدادات عشان ما تخسر تقدمك لو بدّلت جهازك.", cairo, 30, dim, TextAlignmentOptions.Top, true); fine.fontStyle = FontStyles.Bold;
            At(fine.gameObject, new Vector2(.5f, 1f), new Vector2(0, -1850), new Vector2(860, 130), new Vector2(.5f, 1f));

            SetRef(view, "content", Rt(content)); SetRef(view, "group", cg);
            SetRef(view, "googleButton", google.GetComponent<Button>()); SetRef(view, "facebookButton", facebook.GetComponent<Button>()); SetRef(view, "appleButton", apple.GetComponent<Button>());
            SetRef(view, "guestButton", guest.GetComponent<Button>()); SetRef(view, "officeButton", ob);
            SetRef(view, "googleSoon", googleSoon); SetRef(view, "facebookSoon", facebookSoon); SetRef(view, "appleSoon", appleSoon);
            th.loginChoicePrefab = Save(root, "LoginChoice");
            EditorUtility.SetDirty(th);
        }

        private static void BuildAccountSheet(KitTheme th)
        {
            var lalezar = th.fontLalezar != null ? th.fontLalezar : th.fontHeading; var cairo = th.fontBody;
            var ink = (Color)th.paperInk; var soft = new Color32(0x4A, 0x3C, 0x24, 255); var dim = new Color32(0x8A, 0x82, 0x72, 255); var gold = new Color32(0xF5, 0xE2, 0xA8, 255);
            var sBoard = Spr("ev_board"); var sGreen = Spr("ev_green");
            var dot = th.dotWhite;

            void At(GameObject g, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null) { var r = Rt(g); r.anchorMin = r.anchorMax = anchor; r.pivot = pivot ?? new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size; }

            var root = new GameObject("AccountSheet", typeof(RectTransform)); Stretch(Rt(root));
            var view = root.AddComponent<KitAccountSheet>();
            var scrim = Go("Scrim", root.transform); Stretch(Rt(scrim));
            var scrimImg = scrim.AddComponent<Image>(); scrimImg.color = new Color(.02f, .012f, .008f, .85f);
            var scrimBtn = scrim.AddComponent<Button>(); scrimBtn.targetGraphic = scrimImg; scrimBtn.transition = Selectable.Transition.None;
            var scrimCg = scrim.AddComponent<CanvasGroup>();

            var brd = Go("Board", root.transform); At(brd, new Vector2(.5f, .5f), new Vector2(0, 40), new Vector2(980, 1330));
            Img(brd, sBoard, true, 1f, true); var boardCg = brd.AddComponent<CanvasGroup>();

            var title = Txt(brd.transform, "Title", "الحساب", lalezar, 92, gold, TextAlignmentOptions.MidlineRight); title.overflowMode = TextOverflowModes.Overflow; title.textWrappingMode = TextWrappingModes.NoWrap;
            At(title.gameObject, new Vector2(1f, 1f), new Vector2(-70, -60), new Vector2(600, 120), new Vector2(1f, 1f));

            // status note
            var stat = Go("Status", brd.transform); At(stat, new Vector2(.5f, 1f), new Vector2(0, -210), new Vector2(880, 230), new Vector2(.5f, 1f));
            var sti = stat.AddComponent<Image>(); sti.color = new Color(.04f, .03f, .03f, .45f); sti.raycastTarget = false;
            var st = Txt(stat.transform, "Text", "حسابك الحالي: ضيف", cairo, 42, new Color32(0xE6, 0xD9, 0xBD, 255), TextAlignmentOptions.MidlineRight); st.fontStyle = FontStyles.Bold; st.overflowMode = TextOverflowModes.Overflow; st.textWrappingMode = TextWrappingModes.NoWrap;
            At(st.gameObject, new Vector2(1f, 1f), new Vector2(-30, -22), new Vector2(820, 64), new Vector2(1f, 1f));
            var warnGo = Go("Warn", stat.transform); At(warnGo, new Vector2(.5f, 0f), new Vector2(0, 18), new Vector2(840, 120), new Vector2(.5f, 0f));
            var wt = Txt(warnGo.transform, "Text", "لو ما ربطته، بينحذف بعد ٢٣ يوم من آخر دخول وبتخسر تقدمك.", cairo, 32, new Color32(0xFF, 0x9A, 0x9A, 255), TextAlignmentOptions.TopRight, true); wt.fontStyle = FontStyles.Bold; Stretch(Rt(wt.gameObject));

            // one row per way to come back
            string[] keys = { "google", "facebook", "apple", "office" };
            string[] names = { "Google", "Facebook", "Apple", "مكتب المافيا" };
            string[] subs = { "بدّل جهازك بدون ما تخسر شي", "بدّل جهازك بدون ما تخسر شي", "الدخول بحساب Apple", "اسم وكلمة سر من الدعم" };
            string[] letters = { "G", "f", "A", "م" };
            Sprite[] logos = { Spr("brand_google"), Spr("brand_facebook"), Spr("brand_apple"), null };
            Color[] fills = { new Color32(0xF4, 0xEF, 0xE3, 255), new Color32(0x18, 0x77, 0xF2, 255), new Color32(0x11, 0x11, 0x11, 255), th.crimson };
            Color[] letterCols = { new Color32(0xD1, 0x48, 0x36, 255), Color.white, Color.white, new Color32(0xF6, 0xD6, 0xD8, 255) };
            var rowData = new (Button action, GameObject linked, GameObject soon, ArabicText sub)[4];
            for (int i = 0; i < 4; i++)
            {
                var row = Go("Row_" + keys[i], brd.transform); At(row, new Vector2(.5f, 1f), new Vector2(0, -490 - i * 190), new Vector2(880, 170), new Vector2(.5f, 1f));
                Img(row, th.leatherDark, true, 2.2f);
                var c = logos[i] != null ? Logo(row.transform, "Icon", logos[i], 96) : Circle(row.transform, "Icon", dot, fills[i], letters[i], letterCols[i], th.fontHeading, 92, 56); At(c, new Vector2(1f, .5f), new Vector2(-72, 0), new Vector2(92, 92));
                var nm = Txt(row.transform, "Name", names[i], lalezar, 50, th.ivory, TextAlignmentOptions.MidlineRight); nm.overflowMode = TextOverflowModes.Overflow; nm.textWrappingMode = TextWrappingModes.NoWrap;
                At(nm.gameObject, new Vector2(1f, 1f), new Vector2(-140, -22), new Vector2(430, 66), new Vector2(1f, 1f));
                var sb = Txt(row.transform, "Sub", subs[i], cairo, 28, dim, TextAlignmentOptions.MidlineRight); sb.fontStyle = FontStyles.Bold; sb.overflowMode = TextOverflowModes.Overflow; sb.textWrappingMode = TextWrappingModes.NoWrap;
                At(sb.gameObject, new Vector2(1f, 0f), new Vector2(-140, 22), new Vector2(430, 50), new Vector2(1f, 0f));
                var act = MakeButton("Action", keys[i] == "office" ? th.leatherDark : th.leatherRed, th.ivory, keys[i] == "office" ? "استرجاع" : "ربط", lalezar, 42); act.transform.SetParent(row.transform, false);
                At(act, new Vector2(0f, .5f), new Vector2(40, 0), new Vector2(230, 100), new Vector2(0f, .5f));
                var ok = Txt(row.transform, "Linked", "✓ مربوط", cairo, 34, new Color32(0x8F, 0xE0, 0xBD, 255), TextAlignmentOptions.Center); ok.fontStyle = FontStyles.Bold; At(ok.gameObject, new Vector2(0f, .5f), new Vector2(155, 0), new Vector2(230, 60)); ok.gameObject.SetActive(false);
                var soon = Txt(row.transform, "Soon", "قريباً", cairo, 32, new Color32(0xB9, 0xA9, 0x8A, 255), TextAlignmentOptions.Center); soon.fontStyle = FontStyles.Bold; At(soon.gameObject, new Vector2(0f, .5f), new Vector2(155, 0), new Vector2(230, 60)); soon.gameObject.SetActive(false);
                rowData[i] = (act.GetComponent<Button>(), ok.gameObject, soon.gameObject, sb.GetComponent<ArabicText>());
            }

            var close = Go("Close", brd.transform); At(close, new Vector2(0f, 1f), new Vector2(60, -60), new Vector2(94, 94), new Vector2(0f, 1f));
            var cli = Img(close, th.coin, false, 1f, true); cli.preserveAspect = true;
            var clb = close.AddComponent<Button>(); clb.targetGraphic = cli; clb.transition = Selectable.Transition.None;
            var clFx = close.AddComponent<KitButtonFx>(); SetRef(clFx, "face", Rt(close));
            for (int s = -1; s <= 1; s += 2) { var b = Bar(close.transform, "X", new Color32(0x2A, 0x1A, 0x08, 255), 42, 9); Rt(b.gameObject).localRotation = Quaternion.Euler(0, 0, s * 45f); }

            SetRef(view, "board", Rt(brd)); SetRef(view, "scrimGroup", scrimCg); SetRef(view, "boardGroup", boardCg);
            SetRef(view, "closeButton", clb); SetRef(view, "scrimButton", scrimBtn);
            SetRef(view, "statusText", st.GetComponent<ArabicText>()); SetRef(view, "warnText", wt.GetComponent<ArabicText>()); SetRef(view, "warnObject", warnGo);
            var so = new SerializedObject(view); var arr = so.FindProperty("rows"); arr.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("key").stringValue = keys[i];
                e.FindPropertyRelative("action").objectReferenceValue = rowData[i].action;
                e.FindPropertyRelative("linkedMark").objectReferenceValue = rowData[i].linked;
                e.FindPropertyRelative("soonMark").objectReferenceValue = rowData[i].soon;
                e.FindPropertyRelative("sub").objectReferenceValue = rowData[i].sub;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            th.accountSheetPrefab = Save(root, "AccountSheet");
            EditorUtility.SetDirty(th);
        }
    }
}
