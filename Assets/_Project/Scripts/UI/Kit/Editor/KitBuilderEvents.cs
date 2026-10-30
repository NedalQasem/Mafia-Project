using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static MafiaNight.UI.Kit.Editor.KitProceduralTextures;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// The events board of Docs/mockups/events.html as two prefabs: "EventsBoard" (scrim, leather folder with studs and a bullet hole, newspaper
    /// masthead with a wax seal, index tabs, the scrolling list, a toast) and "EventCard" (one paper clipping: stamp, tape or paper clip, coffee
    /// ring, burn mark, tally marks, countdown, reward line and button). Sizes are the HTML ones times 2.769 (390 px phone -> 1080 canvas).
    /// The small sprites are drawn here (leather folder, torn paper, tape, clip, ring, burn, wax seal, stud, bullet hole, green leather).
    /// </summary>
    public static partial class KitBuilder
    {
        private static readonly string[] EvNames = { "ev_board", "ev_paper", "ev_green", "ev_tape", "ev_clip", "ev_ring", "ev_burn", "ev_seal", "ev_stud", "ev_hole", "ev_time" };

        [MenuItem("Mafia Night/UI Kit/Build Events Board")]
        public static void BuildEventsBoardMenu()
        {
            var th = BuildTheme();
            headFont = th.fontHeading;
            EnsureEventSprites(true);
            BuildEventsBoard(th);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIKit] built the events board prefabs");
        }

        internal static void EnsureEventSprites(bool force = false)
        {
            bool missing = force;
            foreach (var n in EvNames) if (!File.Exists(Art + n + ".png")) missing = true;
            if (missing) { Directory.CreateDirectory(Art); DrawEventSprites(); AssetDatabase.Refresh(); }
            foreach (var n in EvNames)
            {
                var imp = AssetImporter.GetAtPath(Art + n + ".png") as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.filterMode = FilterMode.Bilinear; imp.spritePixelsPerUnit = 100;
                imp.wrapMode = TextureWrapMode.Clamp; imp.textureCompression = TextureImporterCompression.Uncompressed;
                var s = new TextureImporterSettings(); imp.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect;
                // left, bottom, right, top
                s.spriteBorder = n == "ev_board" ? new Vector4(60, 60, 60, 60) : n == "ev_paper" ? new Vector4(90, 100, 90, 90) : n == "ev_green" ? new Vector4(56, 56, 56, 56) : n == "ev_time" ? new Vector4(26, 26, 26, 26) : Vector4.zero;
                imp.SetTextureSettings(s);
                imp.SaveAndReimport();
            }
        }

        private static float EvTear(float x)
        {
            float u = x / 40f; int i = (int)u; float f = u - i; f = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(i, 3), Hash(i + 1, 3), f);
        }

        private static void DrawEventSprites()
        {
            Color Grain(Color c, int x, int y, float amt) { float n = (Hash(x, y) - .5f) * amt; return new Color(c.r + n, c.g + n * .9f, c.b + n * .7f, c.a); }
            Color Brass(float x, float y, float w, float h)
            {
                var c0 = new Vector2(.35f * w, .28f * h);
                float t = Vector2.Distance(new Vector2(x, y), c0) / Vector2.Distance(c0, new Vector2(w, h));
                return t < .55f ? Color.Lerp(C(0xf5e2a8), C(0xb99048), t / .55f) : Color.Lerp(C(0xb99048), C(0x6e4b1c), Mathf.Clamp01((t - .55f) / .45f));
            }

            // the leather folder: black rim, thin brass ring, leather, a dashed cream stitch inside
            Make("ev_board", 256, 256, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 128f, 128f, 128f, 128f, 40f);
                if (d > 0f) return Color.clear;
                if (d > -3f) return Color.black;
                if (d > -11f) return C(0x6e5224);
                if (d > -14f) return Color.black;
                var c = Grain(Color.Lerp(C(0x3b2a20), C(0x1a110d), y / 256f), (int)x, (int)y, .07f);
                if (d > -19f && d <= -14f && y < 90f) c = Over(C(0xffffff, .12f), c);
                if (Mathf.Abs(d + 28f) < 2.1f && ((x + y) % 26f) < 15f) c = Over(C(0xc9a35f, .45f), c);
                return c;
            });
            // a clipping: ivory paper, soft darker edges, a torn bottom edge (the bottom 100 px are the 9-slice border, so the tear keeps its size)
            Make("ev_paper", 900, 320, (x, y) =>
            {
                float tear = 320f - 52f + EvTear(x) * 36f;
                if (y > tear) return Color.clear;
                float dist = Mathf.Min(Mathf.Min(x, 900f - x), Mathf.Min(y, 286f - y));
                float t = Mathf.Clamp01(1f - dist / 90f); t = t * t * .6f;
                var c = Grain(Color.Lerp(C(0xd9cfb4), C(0x8a7a5a), t), (int)x, (int)y, .07f);
                if (y > tear - 3f) c = Color.Lerp(c, C(0x6a5a3a), .55f);
                return c;
            });
            // green leather for the "claim" button
            Make("ev_green", 192, 144, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 96f, 72f, 96f, 72f, 40f);
                if (d > 0f) return Color.clear;
                if (d > -5f) return Color.black;
                float k = y / 144f;
                var c = k < .55f ? Color.Lerp(C(0x2c6a55), C(0x1d4a3c), k / .55f) : Color.Lerp(C(0x1d4a3c), C(0x102c24), (k - .55f) / .45f);
                c = Grain(c, (int)x, (int)y, .08f);
                if (d > -10f && y < 50f) c = Over(C(0xffffff, .14f), c);
                return c;
            });
            // the black countdown label with a thin brass edge
            Make("ev_time", 96, 64, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 48f, 32f, 48f, 32f, 14f);
                if (d > 0f) return Color.clear;
                if (d > -2.5f) return Color.black;
                if (d > -6f) return C(0x6e5224);
                return C(0x0b0807);
            });
            // sticky tape with zig-zag ends
            Make("ev_tape", 180, 56, (x, y) =>
            {
                float zig = ((int)(y / 7f) % 2 == 0) ? 0f : 6f;
                if (x < 4f + zig || x > 176f - zig) return Color.clear;
                var c = C(0xd8c88a, .66f); float n = (Hash((int)x, (int)y) - .5f) * .12f;
                return new Color(c.r + n, c.g + n, c.b + n, c.a);
            });
            // paper clip: two nested loops, grey
            Make("ev_clip", 60, 150, (x, y) =>
            {
                float o = Mathf.Abs(RoundRectSdf(x, y, 30f, 76f, 20f, 62f, 20f)), i = Mathf.Abs(RoundRectSdf(x, y, 30f, 70f, 10f, 44f, 10f));
                bool on = (o < 2.7f && !(x > 30f && y < 40f)) || (i < 2.7f && y > 30f);
                if (!on) return Color.clear;
                return x < 30f ? C(0xc4c4c2) : C(0x8c8c8a);
            });
            // coffee ring
            Make("ev_ring", 150, 150, (x, y) =>
            {
                float dx = x - 75f, dy = y - 75f, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                float rr = 60f + 3f * Mathf.Sin(a * 3f) + 1.5f * Mathf.Sin(a * 7f);
                float al = 0f;
                if (Mathf.Abs(r - rr) < 5f) al = .3f - Mathf.Abs(r - rr) * .03f;
                if (Mathf.Abs(r - (rr - 9f)) < 1.5f && a > 0f) al = Mathf.Max(al, .14f);
                if (r < rr) al = Mathf.Max(al, .05f);
                return C(0x7a4a1c, al);
            });
            // cigar burn
            Make("ev_burn", 80, 80, (x, y) =>
            {
                float t = Vector2.Distance(new Vector2(x, y), new Vector2(40f, 40f)) / 40f;
                if (t < .28f) return C(0x0b0705);
                if (t < .34f) return C(0x3a1b0a);
                if (t < .58f) return C(0x6b3a16, Mathf.Lerp(.67f, .4f, (t - .34f) / .24f));
                if (t < .72f) return C(0x6b3a16, Mathf.Lerp(.4f, 0f, (t - .58f) / .14f));
                return Color.clear;
            });
            // wax seal
            Make("ev_seal", 128, 128, (x, y) =>
            {
                float dx = x - 64f, dy = y - 64f, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                float edge = 58f + 3.2f * Mathf.Sin(a * 7f) + 2f * Mathf.Sin(a * 13f + 1f);
                if (r > edge) return Color.clear;
                if (r > edge - 3f) return C(0x4a0b10);
                float t = Vector2.Distance(new Vector2(x, y), new Vector2(45f, 40f)) / 95f;
                var c = t < .55f ? Color.Lerp(C(0xd8404a), C(0x8a1c24), t / .55f) : Color.Lerp(C(0x8a1c24), C(0x4a0b10), Mathf.Clamp01((t - .55f) / .45f));
                if (Mathf.Abs(r - 44f) < 1.8f) c = Color.Lerp(c, Color.black, .4f);
                if (r > 50f && y < 64f) c = Over(C(0xffffff, .22f), c);
                return c;
            });
            // brass stud
            Make("ev_stud", 48, 48, (x, y) =>
            {
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(24f, 24f));
                if (r > 24f) return Color.clear;
                if (r > 21f) return Color.black;
                return Brass(x, y, 48f, 48f);
            });
            // bullet hole with two cracks
            Make("ev_hole", 110, 110, (x, y) =>
            {
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(55f, 55f));
                float crack = Mathf.Min(QmSeg(x, y, 55f, 55f, 55f + 48f * Mathf.Cos(.38f), 55f - 48f * Mathf.Sin(.38f)), QmSeg(x, y, 55f, 55f, 55f - 36f * Mathf.Cos(1.06f), 55f + 36f * Mathf.Sin(1.06f)));
                if (r < 18f) return Color.black;
                if (r < 22f) return C(0x1a0f0a);
                if (crack < 1.6f && r < 50f) return C(0x000000, .6f);
                if (r < 34f) return C(0x3b2a20, (34f - r) / 12f * .8f);
                return Color.clear;
            });
        }

        private static void BuildEventsBoard(KitTheme th)
        {
            var ink = (Color)th.paperInk; var crimDark = (Color)th.crimson; var crim = new Color32(0xC0, 0x32, 0x3A, 255);
            var lalezar = th.fontLalezar != null ? th.fontLalezar : th.fontHeading; var cairo = th.fontBody; var aref = th.fontAref != null ? th.fontAref : th.fontCalligraphy;
            var soft = new Color32(0x4A, 0x3C, 0x24, 255); var gold = new Color32(0xFF, 0xD9, 0x8A, 255);
            EnsureEventSprites();
            EnsureQuickMenuSprites();
            var sBoard = Spr("ev_board"); var sPaper = Spr("ev_paper"); var sGreen = Spr("ev_green"); var sTape = Spr("ev_tape"); var sClip = Spr("ev_clip"); var sRing = Spr("ev_ring");
            var sBurn = Spr("ev_burn"); var sSeal = Spr("ev_seal"); var sStud = Spr("ev_stud"); var sHole = Spr("ev_hole"); var sBadge = Spr("qm_badge");

            GameObject Pic(string n, Transform parent, Sprite s, float w, float h)
            {
                var g = Go(n, parent); var i = g.AddComponent<Image>(); i.sprite = s; i.raycastTarget = false; i.preserveAspect = true; Rt(g).sizeDelta = new Vector2(w, h); return g;
            }
            void NoWrap(TextMeshProUGUI t) { t.overflowMode = TextOverflowModes.Overflow; t.textWrappingMode = TextWrappingModes.NoWrap; }
            LayoutElement Le(GameObject g, float pw = -1, float ph = -1, float fw = -1) { var l = g.AddComponent<LayoutElement>(); if (pw >= 0) l.preferredWidth = pw; if (ph >= 0) l.preferredHeight = ph; if (fw >= 0) l.flexibleWidth = fw; return l; }
            void At(GameObject g, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null) { var r = Rt(g); r.anchorMin = r.anchorMax = anchor; r.pivot = pivot ?? new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size; }
            HorizontalLayoutGroup Row(GameObject g, TextAnchor a, float spacing = 14) { var h = g.AddComponent<HorizontalLayoutGroup>(); h.spacing = spacing; h.childAlignment = a; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false; return h; }

            // ================================================================ one clipping
            {
                var card = new GameObject("EventCard", typeof(RectTransform)); Rt(card).sizeDelta = new Vector2(900, 520);
                var pi = Img(card, sPaper, true, 1f, false); pi.raycastTarget = false;
                var v = card.AddComponent<VerticalLayoutGroup>(); v.padding = new RectOffset(40, 40, 38, 66); v.spacing = 6; v.childAlignment = TextAnchor.UpperCenter;
                v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
                card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var kc = card.AddComponent<KitEventCard>(); SetRef(kc, "redFace", th.leatherRed); SetRef(kc, "greenFace", sGreen);

                // decorations (ignore the column)
                var deco = Go("Deco", card.transform); Stretch(Rt(deco)); Le(deco).ignoreLayout = true;
                At(Pic("Tape", deco.transform, sTape, 180, 56), new Vector2(.5f, 1f), new Vector2(0, 8), new Vector2(180, 56)); deco.transform.Find("Tape").localRotation = Quaternion.Euler(0, 0, 2f);
                At(Pic("Clip", deco.transform, sClip, 56, 140), new Vector2(.5f, 1f), new Vector2(0, -10), new Vector2(56, 140));
                At(Pic("Ring", deco.transform, sRing, 130, 130), new Vector2(1f, 0f), new Vector2(-250, 130), new Vector2(130, 130));
                At(Pic("Burn", deco.transform, sBurn, 76, 76), new Vector2(0f, 0f), new Vector2(80, 120), new Vector2(76, 76));
                var st = Go("Stamp", deco.transform); At(st, new Vector2(0f, 1f), new Vector2(150, -128), new Vector2(200, 92)); st.transform.localRotation = Quaternion.Euler(0, 0, 12f);
                var sti = Img(st, th.stampFrame, true, .9f); sti.color = crim;
                var stt = Txt(st.transform, "Text", "شغّال", lalezar, 54, crim); NoWrap(stt); Stretch(Rt(stt.gameObject), 6, 6, 6, 6);
                var big = Go("BigStamp", deco.transform); At(big, new Vector2(.5f, .5f), new Vector2(-90, -34), new Vector2(430, 190)); big.transform.localRotation = Quaternion.Euler(0, 0, 9f);
                var bgi = Img(big, th.stampFrame, true, 1.2f); bgi.color = new Color32(0x1D, 0x4A, 0x3C, 235);
                var bgt = Txt(big.transform, "Text", "انتهى", lalezar, 112, new Color32(0x1D, 0x4A, 0x3C, 235)); NoWrap(bgt); Stretch(Rt(bgt.gameObject), 6, 6, 6, 6);

                // kicker line
                var kick = Go("Kick", card.transform); Le(kick, -1, 50);
                var kk = Txt(kick.transform, "Kicker", "عرض الأسبوع", cairo, 26, crim, TextAlignmentOptions.MidlineRight); kk.fontStyle = FontStyles.Bold; NoWrap(kk); Stretch(Rt(kk.gameObject), 0, 6, 0, 0);
                var kn = Txt(kick.transform, "Number", "الحدث ١", cairo, 26, soft, TextAlignmentOptions.MidlineLeft); kn.fontStyle = FontStyles.Bold; NoWrap(kn); Stretch(Rt(kn.gameObject), 0, 6, 0, 0);
                var kr = Bar(kick.transform, "Rule", ink, 100, 4); { var r = Rt(kr.gameObject); r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(.5f, 0); r.sizeDelta = new Vector2(0, 4); r.anchoredPosition = Vector2.zero; }

                // title (room on the left for the stamp)
                var tr = Go("TitleRow", card.transform); Le(tr, -1, 70); var trh = Row(tr, TextAnchor.MiddleRight, 0); trh.padding = new RectOffset(220, 0, 0, 0);
                var ti = Txt(tr.transform, "Title", "عنوان الحدث", lalezar, 58, ink, TextAlignmentOptions.MidlineRight); NoWrap(ti); Le(ti.gameObject, -1, -1, 1);

                var body = Txt(card.transform, "Body", "وصف الحدث", cairo, 30, soft, TextAlignmentOptions.TopRight, true); body.fontStyle = FontStyles.Bold; Le(body.gameObject, -1, 96);

                // typed "top secret" and the black bars over the prize
                var ty = Go("TypedRow", card.transform); Le(ty, -1, 46); Row(ty, TextAnchor.MiddleRight);
                var tbox = Go("Box", ty.transform); Le(tbox, 300, 46); var tbi = Img(tbox, th.stampFrame, true, .7f); tbi.color = ink;
                var tbt = Txt(tbox.transform, "Text", "TOP SECRET", cairo, 24, ink); tbt.fontStyle = FontStyles.Bold; tbt.characterSpacing = 6f; NoWrap(tbt); Stretch(Rt(tbt.gameObject), 6, 2, 6, 2);
                var rd = Go("Redact", card.transform); Le(rd, -1, 54); Row(rd, TextAnchor.MiddleRight, 16);
                var bar2 = Bar(rd.transform, "Bar2", new Color32(0x15, 0x10, 0x0D, 255), 105, 30); Le(bar2.gameObject, 105, 30);
                var bar1 = Bar(rd.transform, "Bar1", new Color32(0x15, 0x10, 0x0D, 255), 194, 30); Le(bar1.gameObject, 194, 30);
                var rl = Txt(rd.transform, "Label", "الجائزة:", cairo, 30, soft); rl.fontStyle = FontStyles.Bold; NoWrap(rl);

                // tally marks: ten strokes, every fifth crossed in red
                var ta = Go("Tally", card.transform); Le(ta, -1, 76);
                var tc = Txt(ta.transform, "Count", "٦ من ١٠", lalezar, 38, ink, TextAlignmentOptions.MidlineLeft); NoWrap(tc); At(tc.gameObject, new Vector2(0, .5f), new Vector2(0, 0), new Vector2(330, 70), new Vector2(0, .5f));
                var mk = Go("Marks", ta.transform); At(mk, new Vector2(1, .5f), new Vector2(0, 0), new Vector2(330, 70), new Vector2(1, .5f));
                for (int i = 0; i < 10; i++)
                {
                    var m = Bar(mk.transform, "M" + i, new Color(.11f, .09f, .06f, .14f), 14, 58);
                    At(m.gameObject, new Vector2(1, .5f), new Vector2(-(7 + i * 28 + (i >= 5 ? 24 : 0)), 0), new Vector2(14, 58));
                }
                for (int g = 0; g < 2; g++)
                {
                    var s = Bar(mk.transform, "Strike" + g, crim, 122, 13);
                    At(s.gameObject, new Vector2(1, .5f), new Vector2(-(7 + (g * 5 + 2) * 28 + (g > 0 ? 24 : 0)), 0), new Vector2(122, 13));
                    s.transform.localRotation = Quaternion.Euler(0, 0, 24f);
                }

                // countdown: "ينتهي خلال [٠٢:١٤:٣٣]"
                var cd = Go("Count", card.transform); Le(cd, -1, 60); Row(cd, TextAnchor.MiddleRight, 14);
                var cb = Go("Box", cd.transform); var cbi = Img(cb, Spr("ev_time"), true, 1f); cbi.raycastTarget = false;
                var cbh = Row(cb, TextAnchor.MiddleCenter, 0); cbh.padding = new RectOffset(24, 24, 2, 8); cb.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize; Le(cb, -1, 54);
                var cv = Txt(cb.transform, "Value", "٠٢:١٤:٣٣", lalezar, 38, gold); NoWrap(cv);
                var cl = Txt(cd.transform, "Label", "ينتهي خلال", cairo, 28, crim); cl.fontStyle = FontStyles.Bold; NoWrap(cl);

                // reward and the button: [button] ........ [label] [coin] [value]
                var rw = Go("Reward", card.transform); Le(rw, -1, 90); Row(rw, TextAnchor.MiddleCenter, 12);
                var act = MakeButton("Action", th.leatherRed, th.ivory, "العب الآن", lalezar, 40); act.transform.SetParent(rw.transform, false); Le(act, 270, 80); act.AddComponent<CanvasGroup>();
                var spacer = Go("Spacer", rw.transform); Le(spacer, 0, 0, 1);
                var rlb = Txt(rw.transform, "Label", "على كل ربح", cairo, 24, soft); rlb.fontStyle = FontStyles.Bold; NoWrap(rlb);
                var rco = Go("Coin", rw.transform); var rci = Img(rco, th.coin); rci.raycastTarget = false; Le(rco, 40, 40);
                var rv = Txt(rw.transform, "Value", "×٢", lalezar, 44, ink); NoWrap(rv);
                Save(card, "EventCard");
            }

            // ================================================================ the board
            var root = new GameObject("EventsBoard", typeof(RectTransform)); Stretch(Rt(root));
            var view = root.AddComponent<KitEventsBoard>();

            var scrim = Go("Scrim", root.transform); Stretch(Rt(scrim));
            var scrimImg = scrim.AddComponent<Image>(); scrimImg.color = new Color(.02f, .012f, .008f, .8f);
            var scrimBtn = scrim.AddComponent<Button>(); scrimBtn.targetGraphic = scrimImg; scrimBtn.transition = Selectable.Transition.None;
            var scrimCg = scrim.AddComponent<CanvasGroup>();

            var brd = Go("Board", root.transform); Stretch(Rt(brd), 33, 255, 33, 205);
            var bi = Img(brd, sBoard, true, 1f, true); var boardCg = brd.AddComponent<CanvasGroup>();

            // masthead: a strip of newspaper with a double rule, slightly crooked, the wax seal on its corner
            var mast = Go("Mast", brd.transform); At(mast, new Vector2(.5f, 1f), new Vector2(0, -44), new Vector2(914, 158), new Vector2(.5f, 1f)); mast.transform.localRotation = Quaternion.Euler(0, 0, .4f);
            Img(mast, th.paper, true, 2.2f);
            var mt = Txt(mast.transform, "Title", "أخبار الحارة", lalezar, 74, ink); NoWrap(mt); At(mt.gameObject, new Vector2(.5f, 1f), new Vector2(0, -2), new Vector2(800, 94), new Vector2(.5f, 1f));
            { var b1 = Bar(mast.transform, "Rule1", ink, 850, 4); At(b1.gameObject, new Vector2(.5f, 1f), new Vector2(0, -96), new Vector2(850, 4), new Vector2(.5f, 1f));
              var b2 = Bar(mast.transform, "Rule2", ink, 850, 4); At(b2.gameObject, new Vector2(.5f, 1f), new Vector2(0, -105), new Vector2(850, 4), new Vector2(.5f, 1f)); }
            var mr = Txt(mast.transform, "Sub", "مواعيد وصفقات لوقت محدود", cairo, 24, soft, TextAlignmentOptions.MidlineRight); mr.fontStyle = FontStyles.Bold; NoWrap(mr); At(mr.gameObject, new Vector2(1f, 1f), new Vector2(-34, -112), new Vector2(560, 38), new Vector2(1f, 1f));
            var ml = Txt(mast.transform, "Count", "٢ شغّالة", cairo, 24, crimDark, TextAlignmentOptions.MidlineLeft); ml.fontStyle = FontStyles.Bold; NoWrap(ml); At(ml.gameObject, new Vector2(0f, 1f), new Vector2(34, -112), new Vector2(300, 38), new Vector2(0f, 1f));
            var seal = Pic("Seal", mast.transform, sSeal, 104, 104); At(seal, new Vector2(1f, 1f), new Vector2(-8, 8), new Vector2(104, 104));
            { var sg = Txt(seal.transform, "Letter", "م", lalezar, 66, new Color32(0xE8, 0xA0, 0xA4, 255)); NoWrap(sg); Stretch(Rt(sg.gameObject)); }

            // index tabs: right to left running, soon, finished
            var tabsRoot = Go("Tabs", brd.transform); At(tabsRoot, new Vector2(.5f, 1f), new Vector2(0, -228), new Vector2(914, 80), new Vector2(.5f, 1f));
            var tabBtns = new Button[3]; var tabImgs = new Image[3]; var tabTxt = new ArabicText[3];
            string[] tabNames = { "شغّالة", "قريباً", "انتهت" }; float[] tabX = { 309f, 0f, -309f };
            for (int i = 0; i < 3; i++)
            {
                var tb = Go("Tab" + i, tabsRoot.transform); At(tb, new Vector2(.5f, 1f), new Vector2(tabX[i], i == 0 ? 0 : -8), new Vector2(295, 80), new Vector2(.5f, 1f));
                var ti2 = Img(tb, i == 0 ? th.paper : th.leatherDark, true, 2.2f, true);
                tabBtns[i] = tb.AddComponent<Button>(); tabBtns[i].targetGraphic = ti2; tabBtns[i].transition = Selectable.Transition.None; tabImgs[i] = ti2;
                var tl = Txt(tb.transform, "Label", tabNames[i], lalezar, 38, i == 0 ? ink : new Color32(0xB9, 0xA9, 0x8A, 255)); NoWrap(tl); Stretch(Rt(tl.gameObject), 0, 8, 0, 0); tabTxt[i] = tl.GetComponent<ArabicText>();
            }
            var badge = Pic("Badge", tabBtns[0].transform, sBadge, 54, 54); At(badge, new Vector2(1f, 1f), new Vector2(8, 12), new Vector2(54, 54));
            var bt = Txt(badge.transform, "Count", "٢", lalezar, 34, Color.white); NoWrap(bt); Stretch(Rt(bt.gameObject));

            // the list: a dark inset with the scrolling clippings
            var lb = Go("List", brd.transform); Stretch(Rt(lb), 28, 130, 28, 310); var lbi = lb.AddComponent<Image>(); lbi.color = new Color(.043f, .031f, .027f, .4f);
            var vp = Go("Viewport", lb.transform); Stretch(Rt(vp)); var vpi = vp.AddComponent<Image>(); vpi.color = new Color(0, 0, 0, 0); vp.AddComponent<RectMask2D>();
            var content = Go("Content", vp.transform); var cr = Rt(content); cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1); cr.pivot = new Vector2(.5f, 1); cr.sizeDelta = Vector2.zero;
            var cv2 = content.AddComponent<VerticalLayoutGroup>(); cv2.padding = new RectOffset(22, 22, 34, 30); cv2.spacing = 38; cv2.childAlignment = TextAnchor.UpperCenter;
            cv2.childControlWidth = true; cv2.childControlHeight = true; cv2.childForceExpandWidth = true; cv2.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sc = lb.AddComponent<ScrollRect>(); sc.viewport = Rt(vp); sc.content = cr; sc.horizontal = false; sc.vertical = true; sc.movementType = ScrollRect.MovementType.Elastic; sc.scrollSensitivity = 40f;

            // nothing to show: a paper note
            var empty = Go("Empty", lb.transform); At(empty, new Vector2(.5f, .5f), new Vector2(0, 40), new Vector2(780, 250)); empty.transform.localRotation = Quaternion.Euler(0, 0, 1f);
            Img(empty, th.paper, true, 2f);
            var et = Txt(empty.transform, "Title", "الدفتر فاضي", lalezar, 62, ink); NoWrap(et); At(et.gameObject, new Vector2(.5f, 1f), new Vector2(0, -26), new Vector2(660, 80), new Vector2(.5f, 1f));
            { var r1 = Bar(empty.transform, "Rule", ink, 600, 4); At(r1.gameObject, new Vector2(.5f, 1f), new Vector2(0, -112), new Vector2(600, 4), new Vector2(.5f, 1f)); }
            var em = Txt(empty.transform, "Text", "ما في أحداث شغّالة هلأ. مرّ علينا بعدين.", cairo, 32, soft, TextAlignmentOptions.Top, true); em.fontStyle = FontStyles.Bold; At(em.gameObject, new Vector2(.5f, 1f), new Vector2(0, -128), new Vector2(660, 100), new Vector2(.5f, 1f));

            var foot = Txt(brd.transform, "Foot", "الأخبار بتوصلنا من السيرفر وبتتحدّث لحالها", cairo, 24, new Color32(0x8D, 0x74, 0x4A, 255)); foot.fontStyle = FontStyles.Bold; NoWrap(foot);
            At(foot.gameObject, new Vector2(.5f, 0f), new Vector2(0, 52), new Vector2(760, 40));

            // the details of the folder: brass studs and a bullet hole
            At(Pic("StudA", brd.transform, sStud, 36, 36), new Vector2(0f, 0f), new Vector2(34, 34), new Vector2(36, 36));
            At(Pic("StudB", brd.transform, sStud, 36, 36), new Vector2(1f, 0f), new Vector2(-34, 34), new Vector2(36, 36));
            At(Pic("Hole", brd.transform, sHole, 66, 66), new Vector2(1f, 0f), new Vector2(-96, 92), new Vector2(66, 66));

            // close: brass coin with an X, on the corner of the masthead
            var close = Go("Close", brd.transform); At(close, new Vector2(0f, 1f), new Vector2(44, -39), new Vector2(94, 94), new Vector2(0f, 1f));
            var cli = Img(close, th.coin, false, 1f, true); cli.preserveAspect = true;
            var clb = close.AddComponent<Button>(); clb.targetGraphic = cli; clb.transition = Selectable.Transition.None;
            var clFx = close.AddComponent<KitButtonFx>(); SetRef(clFx, "face", Rt(close));
            for (int s = -1; s <= 1; s += 2) { var b = Bar(close.transform, "X", new Color32(0x2A, 0x1A, 0x08, 255), 42, 9); Rt(b.gameObject).localRotation = Quaternion.Euler(0, 0, s * 45f); }

            // toast
            var toast = Go("Toast", brd.transform); At(toast, new Vector2(.5f, 0f), new Vector2(0, 150), new Vector2(560, 96)); Img(toast, th.leatherDark, true, 2.4f);
            var toastCg = toast.AddComponent<CanvasGroup>(); toastCg.blocksRaycasts = false;
            var tt = Txt(toast.transform, "Label", "+٥٠٠ ذهب", cairo, 38, th.brassLight, TextAlignmentOptions.Center); tt.fontStyle = FontStyles.Bold; NoWrap(tt); Stretch(Rt(tt.gameObject), 16, 10, 16, 6);

            SetRef(view, "board", Rt(brd)); SetRef(view, "content", cr); SetRef(view, "scroll", sc);
            SetRef(view, "scrimGroup", scrimCg); SetRef(view, "boardGroup", boardCg); SetRef(view, "toastGroup", toastCg);
            SetRef(view, "closeButton", clb); SetRef(view, "scrimButton", scrimBtn);
            SetArray(view, "tabButtons", tabBtns); SetArray(view, "tabImages", tabImgs); SetArray(view, "tabLabels", tabTxt);
            SetRef(view, "liveBadge", badge); SetRef(view, "emptyNote", empty);
            SetRef(view, "liveBadgeText", bt.GetComponent<ArabicText>()); SetRef(view, "countText", ml.GetComponent<ArabicText>());
            SetRef(view, "toastText", tt.GetComponent<ArabicText>()); SetRef(view, "emptyText", em.GetComponent<ArabicText>());
            SetRef(view, "cardPrefab", LoadPrefab("EventCard")); SetRef(view, "paperTab", th.paper); SetRef(view, "leatherTab", th.leatherDark);
            th.eventsBoardPrefab = Save(root, "EventsBoard");
            EditorUtility.SetDirty(th);
        }

        private static void SetArray(Object comp, string field, Object[] values)
        {
            var so = new SerializedObject(comp); var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[UIKit] missing field " + field); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
