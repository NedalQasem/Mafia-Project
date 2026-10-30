using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static MafiaNight.UI.Kit.Editor.KitProceduralTextures;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// The mail ("البريد") of Docs/mockups/mailbox.html as two prefabs: "MailBoard" (leather folder, masthead with the wax seal, "collect all",
    /// the list, the promo code strip, a toast) and "MailCard" (one telegram: sheet with a dashed edge, sender, title, text, package chips,
    /// expiry and the collect button). It reuses the sprites of the events board; its own are the telegram sheet and the gem.
    /// </summary>
    public static partial class KitBuilder
    {
        private static readonly string[] MlNames = { "ml_paper", "ml_gem" };

        [MenuItem("Mafia Night/UI Kit/Build Mail Board")]
        public static void BuildMailBoardMenu()
        {
            var th = BuildTheme();
            headFont = th.fontHeading;
            EnsureEventSprites();
            EnsureMailSprites(true);
            if (LoadPrefab("EventCard") == null) BuildEventsBoard(th);       // the mail reuses sprites made with it
            BuildMailBoard(th);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIKit] built the mail board prefabs");
        }

        internal static void EnsureMailSprites(bool force = false)
        {
            bool missing = force;
            foreach (var n in MlNames) if (!File.Exists(Art + n + ".png")) missing = true;
            if (missing) { Directory.CreateDirectory(Art); DrawMailSprites(); AssetDatabase.Refresh(); }
            foreach (var n in MlNames)
            {
                var imp = AssetImporter.GetAtPath(Art + n + ".png") as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.filterMode = FilterMode.Bilinear; imp.spritePixelsPerUnit = 100;
                imp.wrapMode = TextureWrapMode.Clamp; imp.textureCompression = TextureImporterCompression.Uncompressed;
                var s = new TextureImporterSettings(); imp.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect;
                s.spriteBorder = n == "ml_paper" ? new Vector4(70, 70, 70, 70) : Vector4.zero;
                imp.SetTextureSettings(s);
                imp.SaveAndReimport();
            }
        }

        private static void DrawMailSprites()
        {
            // the telegram: ivory paper, soft darker edges, a dashed dark line inside (like a form to tear off)
            Make("ml_paper", 300, 300, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 150f, 150f, 150f, 150f, 6f);
                if (d > 0f) return Color.clear;
                float dist = Mathf.Min(Mathf.Min(x, 300f - x), Mathf.Min(y, 300f - y));
                float t = Mathf.Clamp01(1f - dist / 70f); t = t * t * .55f;
                var c = Color.Lerp(C(0xd9cfb4), C(0x8a7a5a), t);
                float n = (Hash((int)x, (int)y) - .5f) * .07f; c = new Color(c.r + n, c.g + n, c.b + n * .8f, 1f);
                float e = RoundRectSdf(x, y, 150f, 150f, 134f, 134f, 4f);
                bool dash = (x < 150f ? (int)(y / 14f) : (int)(y / 14f)) % 2 == 0 || (int)(x / 14f) % 2 == 0;
                if (Mathf.Abs(e) < 2.2f && dash) c = Over(C(0x1c170f, .42f), c);
                return c;
            });
            // a gem: a green diamond with a facet line
            Make("ml_gem", 64, 64, (x, y) =>
            {
                float dx = Mathf.Abs(x - 32f), dy = Mathf.Abs(y - 32f);
                float d = dx + dy;
                if (d > 28f) return Color.clear;
                if (d > 25f) return Color.black;
                var c = Color.Lerp(C(0x9be8d0), C(0x2c8f74), Mathf.Clamp01((x + y - 20f) / 60f));
                if (y < 32f && dx < 6f) c = Over(C(0xffffff, .3f), c);
                return c;
            });
        }

        private static void BuildMailBoard(KitTheme th)
        {
            var ink = (Color)th.paperInk; var crimDark = (Color)th.crimson; var crim = new Color32(0xC0, 0x32, 0x3A, 255);
            var lalezar = th.fontLalezar != null ? th.fontLalezar : th.fontHeading; var cairo = th.fontBody;
            var soft = new Color32(0x4A, 0x3C, 0x24, 255); var gold = new Color32(0xFF, 0xD9, 0x8A, 255);
            EnsureEventSprites(); EnsureMailSprites(); EnsureQuickMenuSprites(); EnsureDmSprites();
            var sBoard = Spr("ev_board"); var sGreen = Spr("ev_green"); var sTime = Spr("ev_time"); var sSeal = Spr("ev_seal"); var sStud = Spr("ev_stud"); var sHole = Spr("ev_hole");
            var sPaper = Spr("ml_paper"); var sGem = Spr("ml_gem"); var sBadge = Spr("qm_badge");

            GameObject Pic(string n, Transform parent, Sprite s, float w, float h)
            {
                var g = Go(n, parent); var i = g.AddComponent<Image>(); i.sprite = s; i.raycastTarget = false; i.preserveAspect = true; Rt(g).sizeDelta = new Vector2(w, h); return g;
            }
            void NoWrap(TextMeshProUGUI t) { t.overflowMode = TextOverflowModes.Overflow; t.textWrappingMode = TextWrappingModes.NoWrap; }
            LayoutElement Le(GameObject g, float pw = -1, float ph = -1, float fw = -1) { var l = g.AddComponent<LayoutElement>(); if (pw >= 0) l.preferredWidth = pw; if (ph >= 0) l.preferredHeight = ph; if (fw >= 0) l.flexibleWidth = fw; return l; }
            void At(GameObject g, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null) { var r = Rt(g); r.anchorMin = r.anchorMax = anchor; r.pivot = pivot ?? new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size; }
            HorizontalLayoutGroup Row(GameObject g, TextAnchor a, float spacing = 14) { var h = g.AddComponent<HorizontalLayoutGroup>(); h.spacing = spacing; h.childAlignment = a; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false; return h; }
            GameObject Chip(string name, Transform parent, Sprite icon, string label)
            {
                var c = Go(name, parent); Img(c, sTime, true, 1f).raycastTarget = false; Le(c, -1, 62);
                var h = Row(c, TextAnchor.MiddleCenter, 10); h.padding = new RectOffset(24, 24, 2, 8);
                c.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                if (icon != null) { var ic = Go("Icon", c.transform); var ii = Img(ic, icon); ii.raycastTarget = false; Le(ic, 34, 34); }
                var t = Txt(c.transform, "Text", label, lalezar, 38, gold); NoWrap(t);
                return c;
            }

            // ================================================================ one telegram
            {
                var card = new GameObject("MailCard", typeof(RectTransform)); Rt(card).sizeDelta = new Vector2(900, 480);
                var pi = Img(card, sPaper, true, 1f, false); pi.raycastTarget = false;
                var v = card.AddComponent<VerticalLayoutGroup>(); v.padding = new RectOffset(46, 46, 40, 44); v.spacing = 8; v.childAlignment = TextAnchor.UpperCenter;
                v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
                card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var kc = card.AddComponent<KitMailCard>(); SetRef(kc, "greenFace", sGreen); SetRef(kc, "redFace", th.leatherRed);

                var deco = Go("Deco", card.transform); Stretch(Rt(deco)); Le(deco).ignoreLayout = true;
                var dot = Pic("Dot", deco.transform, sBadge, 46, 46); At(dot, new Vector2(1f, 1f), new Vector2(-6, -2), new Vector2(46, 46));
                var st = Go("Stamp", deco.transform); At(st, new Vector2(0f, 1f), new Vector2(200, -150), new Vector2(300, 100)); st.transform.localRotation = Quaternion.Euler(0, 0, 12f);
                var sti = Img(st, th.stampFrame, true, .9f); sti.color = new Color32(0x1D, 0x4A, 0x3C, 255);
                var stt = Txt(st.transform, "Text", "تمّ الاستلام", lalezar, 44, new Color32(0x1D, 0x4A, 0x3C, 255)); NoWrap(stt); Stretch(Rt(stt.gameObject), 6, 6, 6, 6);

                // a private message: red edge and hazard tape for a warning, a small taped photo for a picture
                var band = Go("Band", deco.transform); { var br = Rt(band); br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(0, 1); br.pivot = new Vector2(0, .5f); br.sizeDelta = new Vector2(16, -20); br.anchoredPosition = Vector2.zero; var bi = band.AddComponent<Image>(); bi.color = crim; bi.raycastTarget = false; }
                var tp = Go("Tape", deco.transform); { var tr = Rt(tp); tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(.5f, 1); tr.sizeDelta = new Vector2(-20, 22); tr.anchoredPosition = new Vector2(0, -8); var ti = tp.AddComponent<Image>(); ti.sprite = Spr("dm_tape"); ti.type = Image.Type.Tiled; ti.raycastTarget = false; }
                var thumb = Go("Thumb", deco.transform); At(thumb, new Vector2(1f, 1f), new Vector2(-50, -186), new Vector2(224, 160), new Vector2(1f, 1f)); thumb.transform.localRotation = Quaternion.Euler(0, 0, -2f);
                { var tfi = Img(thumb, Spr("dm_photo"), true, 1f); tfi.raycastTarget = false; var tpic = Go("Pic", thumb.transform); Stretch(Rt(tpic), 10, 10, 10, 10); var tr2 = tpic.AddComponent<RawImage>(); tr2.raycastTarget = false; tr2.color = new Color(1, 1, 1, 0); }

                var from = Go("From", card.transform); Le(from, -1, 48);
                var who = Txt(from.transform, "Who", "من: مكتب الريّس", cairo, 25, crim, TextAlignmentOptions.MidlineRight); who.fontStyle = FontStyles.Bold; NoWrap(who); Stretch(Rt(who.gameObject), 0, 6, 0, 0);
                var when = Txt(from.transform, "When", "قبل ساعة", cairo, 25, soft, TextAlignmentOptions.MidlineLeft); when.fontStyle = FontStyles.Bold; NoWrap(when); Stretch(Rt(when.gameObject), 0, 6, 0, 0);
                var rule = Bar(from.transform, "Rule", ink, 100, 4); { var r = Rt(rule.gameObject); r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(.5f, 0); r.sizeDelta = new Vector2(0, 4); r.anchoredPosition = Vector2.zero; }

                var title = Txt(card.transform, "Title", "عنوان البرقية", lalezar, 54, ink, TextAlignmentOptions.MidlineRight); NoWrap(title); Le(title.gameObject, -1, 72);
                var body = Txt(card.transform, "Body", "نص البرقية", cairo, 30, soft, TextAlignmentOptions.TopRight, true); body.fontStyle = FontStyles.Bold; Le(body.gameObject, -1, 100);

                var chips = Go("Chips", card.transform); Le(chips, -1, 66); Row(chips, TextAnchor.MiddleRight, 12);
                var itemChip = Chip("Item", chips.transform, null, "إطار الجمر");
                var gemChip = Chip("Gem", chips.transform, sGem, "٥");
                var coinChip = Chip("Coin", chips.transform, th.coin, "١٬٠٠٠");

                var rw = Go("Row", card.transform); Le(rw, -1, 92); Row(rw, TextAnchor.MiddleCenter, 12);
                var act = MakeButton("Action", sGreen, th.ivory, "استلم", lalezar, 40); act.transform.SetParent(rw.transform, false); Le(act, 260, 80); act.AddComponent<CanvasGroup>();
                var spacer = Go("Spacer", rw.transform); Le(spacer, 0, 0, 1);
                var exp = Txt(rw.transform, "Expiry", "تنتهي بعد ٦ أيام", cairo, 26, crim); exp.fontStyle = FontStyles.Bold; NoWrap(exp);
                Save(card, "MailCard");
            }

            // ================================================================ the board
            var root = new GameObject("MailBoard", typeof(RectTransform)); Stretch(Rt(root));
            var view = root.AddComponent<KitMailBoard>();

            var scrim = Go("Scrim", root.transform); Stretch(Rt(scrim));
            var scrimImg = scrim.AddComponent<Image>(); scrimImg.color = new Color(.02f, .012f, .008f, .8f);
            var scrimBtn = scrim.AddComponent<Button>(); scrimBtn.targetGraphic = scrimImg; scrimBtn.transition = Selectable.Transition.None;
            var scrimCg = scrim.AddComponent<CanvasGroup>();

            var brd = Go("Board", root.transform); Stretch(Rt(brd), 33, 255, 33, 205);
            Img(brd, sBoard, true, 1f, true); var boardCg = brd.AddComponent<CanvasGroup>();

            var mast = Go("Mast", brd.transform); At(mast, new Vector2(.5f, 1f), new Vector2(0, -44), new Vector2(914, 158), new Vector2(.5f, 1f)); mast.transform.localRotation = Quaternion.Euler(0, 0, .4f);
            Img(mast, th.paper, true, 2.2f);
            var mt = Txt(mast.transform, "Title", "البريد", lalezar, 74, ink); NoWrap(mt); At(mt.gameObject, new Vector2(.5f, 1f), new Vector2(0, -2), new Vector2(800, 94), new Vector2(.5f, 1f));
            { var b1 = Bar(mast.transform, "Rule1", ink, 850, 4); At(b1.gameObject, new Vector2(.5f, 1f), new Vector2(0, -96), new Vector2(850, 4), new Vector2(.5f, 1f));
              var b2 = Bar(mast.transform, "Rule2", ink, 850, 4); At(b2.gameObject, new Vector2(.5f, 1f), new Vector2(0, -105), new Vector2(850, 4), new Vector2(.5f, 1f)); }
            var mr = Txt(mast.transform, "Sub", "برقيات من مكتب الريّس", cairo, 24, soft, TextAlignmentOptions.MidlineRight); mr.fontStyle = FontStyles.Bold; NoWrap(mr); At(mr.gameObject, new Vector2(1f, 1f), new Vector2(-34, -112), new Vector2(560, 38), new Vector2(1f, 1f));
            var ml = Txt(mast.transform, "New", "٢ جديدة", cairo, 24, crimDark, TextAlignmentOptions.MidlineLeft); ml.fontStyle = FontStyles.Bold; NoWrap(ml); At(ml.gameObject, new Vector2(0f, 1f), new Vector2(34, -112), new Vector2(300, 38), new Vector2(0f, 1f));
            var seal = Pic("Seal", mast.transform, sSeal, 104, 104); At(seal, new Vector2(1f, 1f), new Vector2(-8, 8), new Vector2(104, 104));
            { var sg = Txt(seal.transform, "Letter", "م", lalezar, 66, new Color32(0xE8, 0xA0, 0xA4, 255)); NoWrap(sg); Stretch(Rt(sg.gameObject)); }

            // counter and "collect all"
            var tools = Go("Tools", brd.transform); At(tools, new Vector2(.5f, 1f), new Vector2(0, -222), new Vector2(914, 72), new Vector2(.5f, 1f));
            var cnt = Txt(tools.transform, "Count", "٣ برقيات", cairo, 28, new Color32(0xB9, 0xA9, 0x8A, 255), TextAlignmentOptions.MidlineRight); cnt.fontStyle = FontStyles.Bold; NoWrap(cnt); At(cnt.gameObject, new Vector2(1f, .5f), new Vector2(-6, 0), new Vector2(400, 60), new Vector2(1f, .5f));
            var all = MakeButton("ClaimAll", sGreen, th.ivory, "استلم الكل", lalezar, 38); all.transform.SetParent(tools.transform, false); At(all, new Vector2(0f, .5f), new Vector2(4, 0), new Vector2(300, 74), new Vector2(0f, .5f));

            // the list
            var lb = Go("List", brd.transform); Stretch(Rt(lb), 28, 232, 28, 306); var lbi = lb.AddComponent<Image>(); lbi.color = new Color(.043f, .031f, .027f, .4f);
            var vp = Go("Viewport", lb.transform); Stretch(Rt(vp)); var vpi = vp.AddComponent<Image>(); vpi.color = new Color(0, 0, 0, 0); vp.AddComponent<RectMask2D>();
            var content = Go("Content", vp.transform); var cr = Rt(content); cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1); cr.pivot = new Vector2(.5f, 1); cr.sizeDelta = Vector2.zero;
            var cv = content.AddComponent<VerticalLayoutGroup>(); cv.padding = new RectOffset(22, 22, 34, 30); cv.spacing = 44; cv.childAlignment = TextAnchor.UpperCenter;
            cv.childControlWidth = true; cv.childControlHeight = true; cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sc = lb.AddComponent<ScrollRect>(); sc.viewport = Rt(vp); sc.content = cr; sc.horizontal = false; sc.vertical = true; sc.movementType = ScrollRect.MovementType.Elastic; sc.scrollSensitivity = 40f;

            var empty = Go("Empty", lb.transform); At(empty, new Vector2(.5f, .5f), new Vector2(0, 40), new Vector2(780, 250)); empty.transform.localRotation = Quaternion.Euler(0, 0, 1f);
            Img(empty, th.paper, true, 2f);
            var et = Txt(empty.transform, "Title", "الصندوق فاضي", lalezar, 62, ink); NoWrap(et); At(et.gameObject, new Vector2(.5f, 1f), new Vector2(0, -26), new Vector2(660, 80), new Vector2(.5f, 1f));
            { var r1 = Bar(empty.transform, "Rule", ink, 600, 4); At(r1.gameObject, new Vector2(.5f, 1f), new Vector2(0, -112), new Vector2(600, 4), new Vector2(.5f, 1f)); }
            var em = Txt(empty.transform, "Text", "ما في برقيات جديدة. لما الإدارة تبعتلك شي بيظهر هون.", cairo, 32, soft, TextAlignmentOptions.Top, true); em.fontStyle = FontStyles.Bold; At(em.gameObject, new Vector2(.5f, 1f), new Vector2(0, -128), new Vector2(660, 100), new Vector2(.5f, 1f));

            // the promo code strip (the app turns it on when codes exist)
            var strip = Go("CodeStrip", brd.transform); At(strip, new Vector2(.5f, 0f), new Vector2(0, 56), new Vector2(900, 110), new Vector2(.5f, 0f)); Img(strip, th.leatherDark, true, 2.2f);
            { var t = Txt(strip.transform, "Label", "عندك كود؟", cairo, 30, new Color32(0xB9, 0xA9, 0x8A, 255), TextAlignmentOptions.MidlineRight); t.fontStyle = FontStyles.Bold; NoWrap(t); At(t.gameObject, new Vector2(1f, .5f), new Vector2(-26, 0), new Vector2(220, 80), new Vector2(1f, .5f)); }
            // input field (Latin letters and digits only), the "activate" button on the left, a line for the answer above the strip
            var inp = Go("Input", strip.transform); { var ir = Rt(inp); ir.anchorMin = new Vector2(0f, .5f); ir.anchorMax = new Vector2(1f, .5f); ir.offsetMin = new Vector2(236, -40); ir.offsetMax = new Vector2(-262, 40); }
            var inpImg = Img(inp, th.paper, true, 2.2f, true); inpImg.color = new Color32(0xF0, 0xE8, 0xD2, 255);
            var area = Go("TextArea", inp.transform); Stretch(Rt(area), 18, 6, 18, 6); area.AddComponent<RectMask2D>();
            var ph = Txt(area.transform, "Placeholder", "MAFIA2026", cairo, 40, new Color(.3f, .25f, .15f, .45f), TextAlignmentOptions.Center); ph.characterSpacing = 6f; NoWrap(ph); Stretch(Rt(ph.gameObject));
            var typed = Go("Text", area.transform); Stretch(Rt(typed)); var tm = typed.AddComponent<TextMeshProUGUI>();
            tm.font = cairo; tm.fontSize = 42; tm.fontStyle = FontStyles.Bold; tm.color = ink; tm.alignment = TextAlignmentOptions.Center; tm.characterSpacing = 6f; tm.raycastTarget = false; tm.textWrappingMode = TextWrappingModes.NoWrap;
            var field = inp.AddComponent<TMP_InputField>();
            field.textViewport = Rt(area); field.textComponent = tm; field.placeholder = ph; field.characterLimit = 20; field.contentType = TMP_InputField.ContentType.Alphanumeric; field.targetGraphic = inpImg;
            var redeem = MakeButton("Redeem", th.leatherRed, th.ivory, "فعّل", lalezar, 42); redeem.transform.SetParent(strip.transform, false); At(redeem, new Vector2(0f, .5f), new Vector2(18, 0), new Vector2(200, 80), new Vector2(0f, .5f));
            var cmsg = Txt(brd.transform, "CodeMessage", "", cairo, 28, new Color32(0x8F, 0xE0, 0xBD, 255), TextAlignmentOptions.Center); cmsg.fontStyle = FontStyles.Bold; NoWrap(cmsg); At(cmsg.gameObject, new Vector2(.5f, 0f), new Vector2(0, 178), new Vector2(880, 46), new Vector2(.5f, 0f));

            At(Pic("StudA", brd.transform, sStud, 36, 36), new Vector2(0f, 0f), new Vector2(34, 34), new Vector2(36, 36));
            At(Pic("StudB", brd.transform, sStud, 36, 36), new Vector2(1f, 0f), new Vector2(-34, 34), new Vector2(36, 36));
            At(Pic("Hole", brd.transform, sHole, 66, 66), new Vector2(1f, 0f), new Vector2(-96, 92), new Vector2(66, 66));

            var close = Go("Close", brd.transform); At(close, new Vector2(0f, 1f), new Vector2(44, -39), new Vector2(94, 94), new Vector2(0f, 1f));
            var cli = Img(close, th.coin, false, 1f, true); cli.preserveAspect = true;
            var clb = close.AddComponent<Button>(); clb.targetGraphic = cli; clb.transition = Selectable.Transition.None;
            var clFx = close.AddComponent<KitButtonFx>(); SetRef(clFx, "face", Rt(close));
            for (int s = -1; s <= 1; s += 2) { var b = Bar(close.transform, "X", new Color32(0x2A, 0x1A, 0x08, 255), 42, 9); Rt(b.gameObject).localRotation = Quaternion.Euler(0, 0, s * 45f); }

            var toast = Go("Toast", brd.transform); At(toast, new Vector2(.5f, 0f), new Vector2(0, 250), new Vector2(620, 96)); Img(toast, th.leatherDark, true, 2.4f);
            var toastCg = toast.AddComponent<CanvasGroup>(); toastCg.blocksRaycasts = false;
            var tt = Txt(toast.transform, "Label", "انضاف الرصيد لحسابك", cairo, 36, th.brassLight, TextAlignmentOptions.Center); tt.fontStyle = FontStyles.Bold; NoWrap(tt); Stretch(Rt(tt.gameObject), 16, 10, 16, 6);

            SetRef(view, "board", Rt(brd)); SetRef(view, "content", cr); SetRef(view, "scroll", sc);
            SetRef(view, "scrimGroup", scrimCg); SetRef(view, "boardGroup", boardCg); SetRef(view, "toastGroup", toastCg);
            SetRef(view, "closeButton", clb); SetRef(view, "scrimButton", scrimBtn); SetRef(view, "claimAllButton", all.GetComponent<Button>());
            SetRef(view, "emptyNote", empty); SetRef(view, "codeStrip", strip);
            SetRef(view, "codeInput", field); SetRef(view, "redeemButton", redeem.GetComponent<Button>()); SetRef(view, "codeMessage", cmsg.GetComponent<ArabicText>());
            SetRef(view, "countText", cnt.GetComponent<ArabicText>()); SetRef(view, "newText", ml.GetComponent<ArabicText>()); SetRef(view, "toastText", tt.GetComponent<ArabicText>());
            SetRef(view, "cardPrefab", LoadPrefab("MailCard"));
            th.mailBoardPrefab = Save(root, "MailBoard");
            EditorUtility.SetDirty(th);
        }
    }
}
