using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static MafiaNight.UI.Kit.Editor.KitProceduralTextures;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// The private message from the office (Docs/mockups/direct-message.html): the "DirectLetter" prefab (paper letter with a wax seal, hazard tape and
    /// a "تحذير" stamp for warnings, a taped photo, the button) and the three small sprites it and the mail card share (hazard tape, photo print,
    /// strip of tape). Building it also rebuilds the mail board, whose cards show messages too.
    /// </summary>
    public static partial class KitBuilder
    {
        private static readonly string[] DmNames = { "dm_tape", "dm_photo", "dm_strip" };

        [MenuItem("Mafia Night/UI Kit/Build Direct Letter")]
        public static void BuildDirectLetterMenu()
        {
            var th = BuildTheme();
            headFont = th.fontHeading;
            EnsureEventSprites();
            EnsureDmSprites(true);
            if (LoadPrefab("EventCard") == null) BuildEventsBoard(th);
            BuildMailBoard(th);              // the cards carry the message look
            BuildDirectLetter(th);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIKit] built the direct letter and the mail board");
        }

        internal static void EnsureDmSprites(bool force = false)
        {
            bool missing = force;
            foreach (var n in DmNames) if (!File.Exists(Art + n + ".png")) missing = true;
            if (missing) { Directory.CreateDirectory(Art); DrawDmSprites(); AssetDatabase.Refresh(); }
            foreach (var n in DmNames)
            {
                var imp = AssetImporter.GetAtPath(Art + n + ".png") as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.filterMode = FilterMode.Bilinear; imp.spritePixelsPerUnit = 100;
                imp.wrapMode = n == "dm_tape" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; imp.textureCompression = TextureImporterCompression.Uncompressed;
                var s = new TextureImporterSettings(); imp.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect;
                s.spriteBorder = n == "dm_photo" ? new Vector4(14, 14, 14, 14) : Vector4.zero;
                imp.SetTextureSettings(s);
                imp.SaveAndReimport();
            }
        }

        private static void DrawDmSprites()
        {
            // hazard tape: black and crimson diagonal stripes that tile in both directions
            Make("dm_tape", 64, 64, (x, y) => ((int)(x + y)) % 64 < 32 ? C(0x14100c) : C(0xc0323a));
            // a photo print: warm white with a thin darker edge and a little grain
            Make("dm_photo", 64, 64, (x, y) =>
            {
                float edge = Mathf.Min(Mathf.Min(x, 63f - x), Mathf.Min(y, 63f - y));
                var c = C(0xefe6cf);
                float n = (Hash((int)x, (int)y) - .5f) * .05f; c = new Color(c.r + n, c.g + n, c.b + n * .8f, 1f);
                if (edge < 1.6f) c = Over(C(0x1c170f, .45f), c);
                else if (edge < 6f) c = Over(C(0x8a7a5a, .16f * (1f - edge / 6f)), c);
                return c;
            });
            // a strip of yellowed sticky tape
            Make("dm_strip", 128, 40, (x, y) =>
            {
                if (x < 2f || x > 125f || y < 2f || y > 37f) return Color.clear;
                bool zig = ((int)(y / 5f) % 2 == 0) ? x < 6f || x > 121f : x < 3f || x > 124f;
                if (zig) return Color.clear;
                return new Color(0.91f, 0.85f, 0.66f, .82f);
            });
        }

        private static void BuildDirectLetter(KitTheme th)
        {
            var ink = (Color)th.paperInk; var crimDark = (Color)th.crimson; var crim = new Color32(0xC0, 0x32, 0x3A, 255);
            var lalezar = th.fontLalezar != null ? th.fontLalezar : th.fontHeading; var cairo = th.fontBody;
            var soft = new Color32(0x4A, 0x3C, 0x24, 255);
            EnsureEventSprites(); EnsureMailSprites(); EnsureDmSprites();
            var sPaper = Spr("ml_paper"); var sSeal = Spr("ev_seal"); var sGreen = Spr("ev_green");
            var sTape = Spr("dm_tape"); var sPhoto = Spr("dm_photo"); var sStrip = Spr("dm_strip");

            void NoWrap(TextMeshProUGUI t) { t.overflowMode = TextOverflowModes.Overflow; t.textWrappingMode = TextWrappingModes.NoWrap; }
            LayoutElement Le(GameObject g, float pw = -1, float ph = -1, float fw = -1) { var l = g.AddComponent<LayoutElement>(); if (pw >= 0) l.preferredWidth = pw; if (ph >= 0) l.preferredHeight = ph; if (fw >= 0) l.flexibleWidth = fw; return l; }
            void At(GameObject g, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null) { var r = Rt(g); r.anchorMin = r.anchorMax = anchor; r.pivot = pivot ?? new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size; }
            HorizontalLayoutGroup Row(GameObject g, TextAnchor a, float spacing = 14) { var h = g.AddComponent<HorizontalLayoutGroup>(); h.spacing = spacing; h.childAlignment = a; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false; return h; }

            var root = new GameObject("DirectLetter", typeof(RectTransform)); Stretch(Rt(root));
            var view = root.AddComponent<KitDirectLetter>();

            var scrim = Go("Scrim", root.transform); Stretch(Rt(scrim));
            var scrimImg = scrim.AddComponent<Image>(); scrimImg.color = new Color(.02f, .012f, .008f, .88f); scrimImg.raycastTarget = true;
            var scrimCg = scrim.AddComponent<CanvasGroup>();

            // ---- the letter
            var letter = Go("Letter", root.transform); At(letter, new Vector2(.5f, .5f), new Vector2(0, 70), new Vector2(940, 900));
            var paper = Img(letter, sPaper, true, 1f); paper.raycastTarget = true;
            var letterCg = letter.AddComponent<CanvasGroup>();
            var v = letter.AddComponent<VerticalLayoutGroup>(); v.padding = new RectOffset(60, 60, 78, 70); v.spacing = 10; v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            letter.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var deco = Go("Deco", letter.transform); Stretch(Rt(deco)); Le(deco).ignoreLayout = true;
            GameObject Tape(string name, bool top)
            {
                var t = Go(name, deco.transform); var r = Rt(t); r.anchorMin = new Vector2(0, top ? 1 : 0); r.anchorMax = new Vector2(1, top ? 1 : 0); r.pivot = new Vector2(.5f, top ? 1 : 0); r.sizeDelta = new Vector2(0, 36); r.anchoredPosition = Vector2.zero;
                var i = t.AddComponent<Image>(); i.sprite = sTape; i.type = Image.Type.Tiled; i.raycastTarget = false; return t;
            }
            var tapeTop = Tape("TapeTop", true); var tapeBottom = Tape("TapeBottom", false);
            var seal = Go("Seal", deco.transform); At(seal, new Vector2(1f, 1f), new Vector2(-44, 36), new Vector2(130, 130));
            var sealImg = Img(seal, sSeal); sealImg.raycastTarget = false;
            var sealT = Txt(seal.transform, "Letter", "م", lalezar, 82, new Color32(0xE8, 0xA0, 0xA4, 255)); NoWrap(sealT); Stretch(Rt(sealT.gameObject));
            var stamp = Go("WarnStamp", deco.transform); At(stamp, new Vector2(0f, 1f), new Vector2(250, -150), new Vector2(380, 150)); stamp.transform.localRotation = Quaternion.Euler(0, 0, 13f);
            var stampImg = Img(stamp, th.stampFrame, true, .9f); stampImg.color = crim;
            var stampT = Txt(stamp.transform, "Text", "تحذير", lalezar, 104, crim); NoWrap(stampT); Stretch(Rt(stampT.gameObject), 6, 6, 6, 6);

            var head = Go("Head", letter.transform); Le(head, -1, 54);
            var who = Txt(head.transform, "Who", "رسالة خاصة · من مكتب الريّس", cairo, 28, crim, TextAlignmentOptions.MidlineRight); who.fontStyle = FontStyles.Bold; NoWrap(who); Stretch(Rt(who.gameObject), 0, 4, 0, 0);
            var when = Txt(head.transform, "When", "هلأ", cairo, 28, soft, TextAlignmentOptions.MidlineLeft); when.fontStyle = FontStyles.Bold; NoWrap(when); Stretch(Rt(when.gameObject), 0, 4, 0, 0);
            var rule = Bar(head.transform, "Rule", ink, 100, 5); { var r = Rt(rule.gameObject); r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(.5f, 0); r.sizeDelta = new Vector2(0, 5); r.anchoredPosition = Vector2.zero; }

            var titleRow = Go("TitleRow", letter.transform); Le(titleRow, -1, 110); var trh = titleRow.AddComponent<HorizontalLayoutGroup>(); trh.childControlWidth = true; trh.childControlHeight = true; trh.childForceExpandWidth = true; trh.childForceExpandHeight = true;
            var title = Txt(titleRow.transform, "Title", "ملاحظة على ملفك", lalezar, 78, ink, TextAlignmentOptions.TopRight, true);
            var body = Txt(letter.transform, "Body", "نص الرسالة", cairo, 34, soft, TextAlignmentOptions.TopRight, true); body.fontStyle = FontStyles.Bold; Le(body.gameObject, -1, 110);

            // the photo taped on
            var photo = Go("Photo", letter.transform); Le(photo, -1, 420);
            var photoFrame = Img(photo, sPhoto, true, 1f, true); photo.transform.localRotation = Quaternion.Euler(0, 0, 1.2f);
            var photoBtn = photo.AddComponent<Button>(); photoBtn.targetGraphic = photoFrame; photoBtn.transition = Selectable.Transition.None;
            var pic = Go("Pic", photo.transform); Stretch(Rt(pic), 16, 16, 16, 16); var photoPic = pic.AddComponent<RawImage>(); photoPic.raycastTarget = false; photoPic.color = new Color(1, 1, 1, 0);
            var cap = Go("Hint", photo.transform); At(cap, new Vector2(0f, 0f), new Vector2(34, 34), new Vector2(330, 48), new Vector2(0f, 0f));
            { var ci = cap.AddComponent<Image>(); ci.color = new Color(.043f, .031f, .027f, .8f); ci.raycastTarget = false; var ct = Txt(cap.transform, "Text", "اضغط للتكبير", cairo, 24, new Color32(0xE6, 0xD9, 0xBD, 255)); ct.fontStyle = FontStyles.Bold; NoWrap(ct); Stretch(Rt(ct.gameObject)); }
            for (int k = 0; k < 2; k++)
            {
                var st = Go("Tape" + k, photo.transform); At(st, new Vector2(k == 0 ? 0f : 1f, 1f), new Vector2(k == 0 ? 90 : -90, 4), new Vector2(150, 46)); st.transform.localRotation = Quaternion.Euler(0, 0, k == 0 ? -12f : 10f);
                var si = st.AddComponent<Image>(); si.sprite = sStrip; si.raycastTarget = false;
            }

            var must = Go("Must", letter.transform); Le(must, -1, 78); var mi = must.AddComponent<Image>(); mi.color = new Color(.54f, .11f, .14f, .12f); mi.raycastTarget = false;
            { var mt = Txt(must.transform, "Text", "قراءة التحذير إلزامية قبل ما تكمّل", cairo, 30, crimDark, TextAlignmentOptions.Center); mt.fontStyle = FontStyles.Bold; NoWrap(mt); Stretch(Rt(mt.gameObject), 10, 4, 10, 4); }

            var actions = Go("Actions", letter.transform); Le(actions, -1, 130); Row(actions, TextAnchor.MiddleCenter, 12);
            var ok = MakeButton("Ok", sGreen, th.ivory, "حسناً", lalezar, 52); ok.transform.SetParent(actions.transform, false); Le(ok, 440, 112); ok.AddComponent<CanvasGroup>();

            var keep = Txt(letter.transform, "Keep", "الرسالة ضلّت محفوظة بالبريد", cairo, 26, soft, TextAlignmentOptions.Center); keep.fontStyle = FontStyles.Bold; NoWrap(keep); Le(keep.gameObject, -1, 42);

            // ---- the big picture
            var viewer = Go("Viewer", root.transform); Stretch(Rt(viewer));
            var vi = viewer.AddComponent<Image>(); vi.color = new Color(0, 0, 0, .94f); vi.raycastTarget = true;
            var vb = viewer.AddComponent<Button>(); vb.targetGraphic = vi; vb.transition = Selectable.Transition.None;
            var vf = Go("Frame", viewer.transform); Stretch(Rt(vf), 70, 380, 70, 380); vf.transform.localRotation = Quaternion.Euler(0, 0, -.8f);
            var vfi = Img(vf, sPhoto, true, 1f); vfi.raycastTarget = false;
            var vp = Go("Pic", vf.transform); Stretch(Rt(vp), 22, 22, 22, 22); var vpr = vp.AddComponent<RawImage>(); vpr.raycastTarget = false;
            var fit = vp.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1.5f;
            var vh = Txt(viewer.transform, "Hint", "اضغط بأي مكان لتسكير الصورة", cairo, 30, new Color32(0x8A, 0x82, 0x72, 255)); vh.fontStyle = FontStyles.Bold; NoWrap(vh); At(vh.gameObject, new Vector2(.5f, 0f), new Vector2(0, 250), new Vector2(900, 50));

            SetRef(view, "letter", Rt(letter)); SetRef(view, "scrimGroup", scrimCg); SetRef(view, "letterGroup", letterCg); SetRef(view, "paper", paper); SetRef(view, "okFace", ok.transform.Find("Face").GetComponent<Image>());
            SetRef(view, "tapeTop", tapeTop); SetRef(view, "tapeBottom", tapeBottom); SetRef(view, "warnStamp", stamp); SetRef(view, "mustNote", must); SetRef(view, "photo", photo); SetRef(view, "viewer", viewer); SetRef(view, "keepNote", keep.gameObject);
            SetRef(view, "fromText", who.GetComponent<ArabicText>()); SetRef(view, "whenText", when.GetComponent<ArabicText>()); SetRef(view, "titleText", title.GetComponent<ArabicText>()); SetRef(view, "bodyText", body.GetComponent<ArabicText>());
            SetRef(view, "okLabel", ok.transform.Find("Face/Label").GetComponent<ArabicText>()); SetRef(view, "sealLetter", sealT.GetComponent<ArabicText>()); SetRef(view, "sealImage", sealImg); SetRef(view, "titleRow", trh);
            SetRef(view, "okButton", ok.GetComponent<Button>()); SetRef(view, "photoButton", photoBtn); SetRef(view, "viewerButton", vb);
            SetRef(view, "photoPic", photoPic); SetRef(view, "viewerPic", vpr); SetRef(view, "greenFace", sGreen); SetRef(view, "redFace", th.leatherRed);
            th.directLetterPrefab = Save(root, "DirectLetter");
            EditorUtility.SetDirty(th);
        }
    }
}
