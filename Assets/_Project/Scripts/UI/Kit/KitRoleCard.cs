using System;
using System.Collections;
using MafiaNight.Data.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// A role's card as in Docs/mockups/role-cards.html: a fixed portrait inside four layers the player bought (frame, background, motion, card back).
    /// Three faces: the front (portrait, name, danger), the back (what the role does), and the hidden one (the owner's card back, shown while the role is secret).
    /// Everything is painted once at its size by <see cref="KitProc"/> (cached), so a card costs a handful of images; it is sized by its width and keeps the proportions of the mockup (210x310).
    /// </summary>
    public sealed class KitRoleCard : MonoBehaviour
    {
        public enum Face { Front, Back, Hidden }

        public const float RefW = 210f, RefH = 310f;

        private RoleCardDef def;
        private CardLayers layers;
        private float K;
        private KitTheme th;
        private RectTransform front, back, hidden;
        private KitCardMotion motion;

        public Face Current { get; private set; }
        public RoleCardDef Def => def;
        public CardLayers Layers => layers;
        /// <summary>The card was pressed (the front turns over to what the role does and back).</summary>
        public event Action<KitRoleCard> Pressed;

        // ------------------------------------------------------------------ creating

        /// <summary>A card of a role. <paramref name="width"/> is in canvas units; the height follows the mockup's proportions.</summary>
        public static KitRoleCard Create(Transform parent, RoleCardDef def, CardLayers layers, float width, Face face = Face.Front)
        {
            var go = new GameObject("RoleCard_" + (def != null ? def.Id : "hidden"), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<KitRoleCard>();
            c.Build(def, layers ?? new CardLayers(), width, face);
            return c;
        }

        /// <summary>The face-down card of somebody whose role is secret: his card back inside his frame and background.</summary>
        public static KitRoleCard CreateHidden(Transform parent, CardLayers layers, float width)
        {
            return Create(parent, null, layers, width, Face.Hidden);
        }

        private void Build(RoleCardDef d, CardLayers l, float width, Face face)
        {
            def = d; layers = l; K = width / RefW; th = KitTheme.Current;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(width, RefH * K);
            if (d != null) { front = BuildFront(); back = BuildBack(); }
            hidden = BuildHidden();
            SetFace(d == null ? Face.Hidden : face, false);
            motion = gameObject.AddComponent<KitCardMotion>();
            motion.Setup(rt, l.Motion, K);
        }

        // ------------------------------------------------------------------ faces

        public void SetFace(Face f, bool animate)
        {
            if (f == Face.Front && front == null) f = Face.Hidden;
            if (f == Face.Back && back == null) f = Face.Hidden;
            if (!animate || !gameObject.activeInHierarchy) { Show(f); Current = f; return; }
            StopAllCoroutines();
            StartCoroutine(Turn(f));
        }

        public void Flip() { if (def != null) SetFace(Current == Face.Front ? Face.Back : Face.Front, true); }

        private void Show(Face f)
        {
            if (front != null) front.gameObject.SetActive(f == Face.Front);
            if (back != null) back.gameObject.SetActive(f == Face.Back);
            if (hidden != null) hidden.gameObject.SetActive(f == Face.Hidden);
        }

        private IEnumerator Turn(Face to)
        {
            var rt = (RectTransform)transform;
            const float half = 0.16f;
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime) { float k = 1f - t / half; rt.localScale = new Vector3(Mathf.Max(0.02f, k * k), 1f, 1f); yield return null; }
            Show(to); Current = to;
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime) { float k = t / half; rt.localScale = new Vector3(Mathf.Max(0.02f, 1f - (1f - k) * (1f - k)), 1f, 1f); yield return null; }
            rt.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ the front

        private RectTransform BuildFront()
        {
            var root = Stretch("Front", transform);
            var fs = CardStyles.Frame(layers.Frame);
            var bg = CardStyles.Background(layers.Background);
            FrameAndPlate(root, fs, bg, false);
            Ornaments(root, fs);

            // the portrait in its window, with a ring around it
            float px = 20 * K, pw = 170 * K, ph = 208 * K, ptop = 24 * K;
            var ring = Img(root, "PortraitRing", KitProc.Paint("rcring" + Key(fs.Line) + Mathf.RoundToInt(K * 100), new KitProc.Spec
            {
                W = pw, H = ph, Radius = 12 * K, Margin = 8 * K, Top = Color.black, Bottom = Color.black,
            }.WithRings(new KeyValuePair2(Color.black, 2f * K), new KeyValuePair2((Color)fs.Line, 1.5f * K))), Color.white);
            PlaceTL(ring.rectTransform, px - 8 * K, ptop - 8 * K, pw + 16 * K, ph + 16 * K);

            var window = Stretch("Portrait", root);
            PlaceTL(window, px, ptop, pw, ph);
            var maskImg = window.gameObject.AddComponent<Image>();
            maskImg.sprite = KitProc.Paint("rcmask" + Mathf.RoundToInt(pw) + "x" + Mathf.RoundToInt(ph), new KitProc.Spec { W = pw, H = ph, Radius = 12 * K, Top = Color.white, Bottom = Color.white });
            maskImg.raycastTarget = false;
            window.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var art = RoleCardArt.Current != null ? RoleCardArt.Current.Portrait(def.Id) : null;
            var pic = Img(window, "Picture", art, art != null ? Color.white : (Color)def.AccentDark);
            if (art != null)
            {
                float aspect = art.rect.width / art.rect.height;
                float w = pw, h = pw / aspect;
                if (h < ph) { h = ph; w = ph * aspect; }
                pic.rectTransform.anchorMin = pic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                pic.rectTransform.sizeDelta = new Vector2(w, h);
                pic.rectTransform.anchoredPosition = new Vector2(0, (h - ph) * 0.18f);        // the face sits high in the picture: crop more from the bottom
            }
            else Anchor(pic.rectTransform, Vector2.zero, Vector2.one);
            var shade = Img(window, "Shade", KitProc.Paint("rcshade" + Mathf.RoundToInt(pw) + "x" + Mathf.RoundToInt(ph), new KitProc.Spec
            {
                W = pw, H = ph, Radius = 12 * K, Top = new Color(0, 0, 0, 0), Mid = new Color(0, 0, 0, 0), MidAt = 0.55f, Bottom = new Color(0, 0, 0, KitProc.LinA(0.72f)),
            }), Color.white);
            Anchor(shade.rectTransform, Vector2.zero, Vector2.one);

            // the corner gems (the role's emblem) and the bullets of the danger
            Gem(root, true); Gem(root, false);
            Bullets(root);

            // the name banner
            float nbW = 142 * K, nbH = 46 * K;
            var banner = Img(root, "Banner", KitProc.Paint("rcbanner" + Key(fs.Line) + Mathf.RoundToInt(nbW) + "x" + Mathf.RoundToInt(nbH), new KitProc.Spec
            {
                W = nbW, H = nbH, Radius = 6 * K, Margin = 6 * K, Top = new Color(0, 0, 0, 0.8f), Bottom = new Color(0, 0, 0, 0.93f),
            }.WithRings(new KeyValuePair2((Color)fs.Line, 1.5f * K), new KeyValuePair2(Color.black, 1.5f * K))), Color.white);
            PlaceBL(banner.rectTransform, 34 * K - 6 * K, 26 * K - 6 * K, nbW + 12 * K, nbH + 12 * K);
            var name = Txt(root, "Name", def.Name, th != null ? th.fontLalezar : null, 21.6f * K, fs.Orn, TextAlignmentOptions.Center);
            PlaceBL(name.Rt(), 34 * K, 26 * K + 21 * K, nbW, 26 * K);
            var en = Txt(root, "English", def.English, th != null ? th.fontBody : null, 8.4f * K, new Color(fs.Orn.r / 255f, fs.Orn.g / 255f, fs.Orn.b / 255f, 0.78f), TextAlignmentOptions.Center);
            en.GetComponent<TMP_Text>().characterSpacing = 8f; en.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            PlaceBL(en.Rt(), 34 * K, 26 * K + 5 * K, nbW, 14 * K);
            return root;
        }

        // ------------------------------------------------------------------ the back (what the role does)

        private RectTransform BuildBack()
        {
            var root = Stretch("Back", transform);
            var fs = CardStyles.Frame(layers.Frame);
            FrameAndPlate(root, fs, null, true);

            Color ink = new Color32(0x1C, 0x17, 0x0F, 255);
            // header: the emblem disc and the name, a rule under them
            float disc = 28 * K;
            var d = Img(root, "Disc", KitProc.Paint("rcdisc" + Key(def.Accent) + Key(def.AccentDark) + Mathf.RoundToInt(disc), new KitProc.Spec
            {
                W = disc, H = disc, Radius = disc * 0.5f, Margin = 3 * K, Radial = true, RadialAt = new Vector2(0.35f, 0.28f), RA = def.Accent, RB = def.AccentDark, RC = Color.black, RBAt = 0.6f,
            }.WithRings(new KeyValuePair2(Color.black, 1.5f * K))), Color.white);
            PlaceTR(d.rectTransform, 20 * K - 3 * K, 18 * K - 3 * K, disc + 6 * K, disc + 6 * K);
            var ic = Img(d.transform, "Emblem", KitSvg.Icon(KitCardArt.RoleEmblem(def.Id), def.Id == "doctor" ? 4.2f : 2.2f, 64), new Color32(0xF3, 0xE6, 0xC8, 255));
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); ic.rectTransform.sizeDelta = new Vector2(15 * K, 15 * K);
            var title = Txt(root, "Title", def.Name, th != null ? th.fontLalezar : null, 20 * K, ink, TextAlignmentOptions.MidlineRight);
            PlaceTR(title.Rt(), 20 * K + disc + 8 * K, 18 * K, RefW * K - 20 * K - disc - 8 * K - 20 * K, disc);
            var rule = Img(root, "Rule", null, new Color(0.11f, 0.09f, 0.06f, 0.27f));
            PlaceTL(rule.rectTransform, 20 * K, 54 * K, (RefW - 40) * K, 1.5f * K);

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < def.Lines.Length; i++) { if (i > 0) sb.Append('\n'); sb.Append("• ").Append(def.Lines[i]); }
            var lines = Txt(root, "Lines", sb.ToString(), th != null ? th.fontBody : null, 11.2f * K, new Color32(0x2A, 0x21, 0x14, 255), TextAlignmentOptions.TopRight);
            var lt = lines.GetComponent<TMP_Text>(); lt.fontStyle = FontStyles.Bold; lt.overflowMode = TextOverflowModes.Truncate; lt.lineSpacing = 18f; lt.paragraphSpacing = 6f * K;
            lines.Wrap = true;          // ArabicText breaks the lines itself so the word order of every line stays right
            PlaceTL(lines.Rt(), 22 * K, 66 * K, (RefW - 44) * K, 150 * K);

            // the win ribbon
            float rw = (RefW - 40) * K, rh = 40 * K;
            var rib = Img(root, "Win", KitProc.Paint("rcwin" + Key(def.AccentDark) + Mathf.RoundToInt(rw) + "x" + Mathf.RoundToInt(rh), new KitProc.Spec { W = rw, H = rh, Radius = 6 * K, Top = def.AccentDark, Bottom = Color.black }), Color.white);
            PlaceBL(rib.rectTransform, 20 * K, 18 * K, rw, rh);
            var win = Txt(rib.transform, "WinText", def.Win, th != null ? th.fontBody : null, 10.2f * K, new Color32(0xF3, 0xE6, 0xC8, 255), TextAlignmentOptions.Center);
            win.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold; win.Wrap = true;
            Anchor(win.Rt(), Vector2.zero, Vector2.one); win.Rt().offsetMin = new Vector2(6 * K, 2 * K); win.Rt().offsetMax = new Vector2(-6 * K, -2 * K);
            return root;
        }

        // ------------------------------------------------------------------ the hidden face (the owner's card back)

        private RectTransform BuildHidden()
        {
            var root = Stretch("Hidden", transform);
            var fs = CardStyles.Frame(layers.Frame);
            var bg = CardStyles.Background(layers.Background);
            FrameAndPlate(root, fs, bg, false);
            Ornaments(root, fs);
            float s = 132 * K;
            var emb = Img(root, "Emblem", KitSvg.Icon(KitCardArt.BackEmblem(layers.Back), 1.4f, 256), fs.Orn);
            emb.rectTransform.anchorMin = emb.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); emb.rectTransform.anchoredPosition = new Vector2(0, 6 * K); emb.rectTransform.sizeDelta = new Vector2(s, s);
            var cap = Txt(root, "Caption", def != null ? "" : "دور سري", th != null ? th.fontLalezar : null, 15 * K, new Color(fs.Orn.r / 255f, fs.Orn.g / 255f, fs.Orn.b / 255f, 0.85f), TextAlignmentOptions.Center);
            PlaceBL(cap.Rt(), 30 * K, 30 * K, (RefW - 60) * K, 22 * K);
            return root;
        }

        // ------------------------------------------------------------------ parts

        private void FrameAndPlate(RectTransform root, FrameStyle fs, BackgroundStyle bg, bool paper)
        {
            float m = 22 * K, w = RefW * K, h = RefH * K;
            var frame = Img(root, "Frame", KitProc.Paint("rcframe" + Key(fs.F1) + Key(fs.F2) + Key(fs.F3) + Mathf.RoundToInt(w), new KitProc.Spec
            {
                W = w, H = h, Radius = 16 * K, Margin = m, Top = fs.F1, Mid = fs.F2, MidAt = 0.55f, Bottom = fs.F3,
                TopLight = new Color(1, 1, 1, 0.4f), TopLightPx = 1f * K,
                Shadow = Color.black, ShadowDy = 12 * K, ShadowBlur = 16 * K,
            }.WithRings(new KeyValuePair2(Color.black, 2f * K))), Color.white);
            PlaceTL(frame.rectTransform, -m, -m, w + 2 * m, h + 2 * m);
            frame.raycastTarget = true;
            KitTap.On(frame.transform, () => Pressed?.Invoke(this));

            float pw = (RefW - 16) * K, ph = (RefH - 16) * K;
            var spec = new KitProc.Spec { W = pw, H = ph, Radius = 10 * K, Margin = 2 * K, Edge = Color.black, EdgePx = 1.5f * K };
            if (paper)
            {
                spec.Top = new Color32(0xE3, 0xD9, 0xBD, 255); spec.Bottom = new Color32(0xD0, 0xC4, 0xA0, 255);
                spec.Dash = new Color(0.11f, 0.09f, 0.06f, 0.33f);
            }
            else
            {
                spec.Top = bg.Top; spec.Bottom = bg.Bottom;
                if (bg.Fire) { spec.Radial = true; spec.RadialAt = new Vector2(0.5f, 1.05f); spec.RA = new Color32(0xFF, 0xB3, 0x47, 255); spec.RB = new Color32(0xB8, 0x33, 0x0C, 255); spec.RC = new Color32(0x1A, 0x06, 0x04, 255); spec.RBAt = 0.38f; spec.RadialReach = 1.0f; }
                spec.Dash = fs.Line;
            }
            spec.DashInset = 5 * K; spec.DashPx = 1.5f * K; spec.DashLen = 99999f; spec.DashGap = 0f; spec.DashRadius = 7 * K;
            var plate = Img(root, "Plate", KitProc.Paint("rcplate" + (paper ? "paper" : layers.Background) + Key(fs.Line) + Mathf.RoundToInt(pw), spec), Color.white);
            PlaceTL(plate.rectTransform, 8 * K - 2 * K, 8 * K - 2 * K, pw + 4 * K, ph + 4 * K);
        }

        private void Ornaments(RectTransform root, FrameStyle fs)
        {
            var sprite = KitSvg.Icon(KitCardArt.Ornament, 1.5f, 96);
            float s = 36 * K, o = 11 * K;
            for (int i = 0; i < 4; i++)
            {
                var im = Img(root, "Orn" + i, sprite, fs.Orn);
                var r = im.rectTransform; r.sizeDelta = new Vector2(s, s);
                bool right = (i & 1) == 1, bottom = i >= 2;
                r.anchorMin = r.anchorMax = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f); r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2((right ? -1 : 1) * (o + s * 0.5f), (bottom ? 1 : -1) * (o + s * 0.5f));
                r.localScale = new Vector3(right ? -1f : 1f, bottom ? -1f : 1f, 1f);
            }
        }

        private void Gem(RectTransform root, bool topRight)
        {
            var gem = Img(root, topRight ? "GemTop" : "GemBottom", KitCardArt.Gem(def.Accent, def.AccentDark), Color.white);
            float w = 34 * K, h = 42 * K;
            if (topRight) PlaceTR(gem.rectTransform, 16 * K, 6 * K, w, h);
            else
            {   // turned half a round about its own centre, so it stays inside the corner
                var gr = gem.rectTransform; gr.anchorMin = gr.anchorMax = Vector2.zero; gr.pivot = new Vector2(0.5f, 0.5f); gr.sizeDelta = new Vector2(w, h);
                gr.anchoredPosition = new Vector2(12 * K + w * 0.5f, 12 * K + h * 0.5f); gr.localRotation = Quaternion.Euler(0, 0, 180);
            }
            var ic = Img(gem.transform, "Emblem", KitSvg.Icon(KitCardArt.RoleEmblem(def.Id), def.Id == "doctor" ? 4.2f : 2.4f, 64), new Color32(0xFF, 0xF3, 0xD8, 255));
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); ic.rectTransform.sizeDelta = new Vector2(16 * K, 16 * K); ic.rectTransform.anchoredPosition = new Vector2(0, 2 * K);
            if (!topRight) ic.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);       // the gem is turned, its emblem stays upright
        }

        private void Bullets(RectTransform root)
        {
            var on = KitProc.Paint("rcbullet-on" + Mathf.RoundToInt(K * 100), new KitProc.Spec { W = 7 * K, H = 11 * K, Radius = 3 * K, Margin = 1.5f * K, Top = new Color32(0xF5, 0xE2, 0xA8, 255), Bottom = new Color32(0xB9, 0x8A, 0x2E, 255) }.WithRings(new KeyValuePair2(Color.black, 1f * K)));
            var off = KitProc.Paint("rcbullet-off" + Mathf.RoundToInt(K * 100), new KitProc.Spec { W = 7 * K, H = 11 * K, Radius = 3 * K, Margin = 1.5f * K, Top = new Color32(0x3A, 0x2E, 0x22, 255), Bottom = new Color32(0x3A, 0x2E, 0x22, 255) }.WithRings(new KeyValuePair2(Color.black, 1f * K)));
            for (int i = 0; i < 4; i++)
            {
                var b = Img(root, "Bullet" + i, i < def.Danger ? on : off, Color.white);
                PlaceTR(b.rectTransform, 16 * K - 1.5f * K, (50 + i * 13) * K - 1.5f * K, 7 * K + 3 * K, 11 * K + 3 * K);
            }
        }

        // ------------------------------------------------------------------ small helpers

        private static string Key(Color32 c) { return ColorUtility.ToHtmlStringRGBA(c); }
        private static string Key(Color c) { return ColorUtility.ToHtmlStringRGBA(c); }

        private static RectTransform Stretch(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform; r.SetParent(parent, false);
            Anchor(r, Vector2.zero, Vector2.one);
            return r;
        }

        private static void Anchor(RectTransform r, Vector2 min, Vector2 max) { r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; }

        // positions measured from a corner of the card, in canvas units (the card's top-left is its origin in the mockup)
        private static void PlaceTL(RectTransform r, float left, float top, float w, float h) { r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(left, -top); r.sizeDelta = new Vector2(w, h); }
        private static void PlaceTR(RectTransform r, float right, float top, float w, float h) { r.anchorMin = r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 1); r.anchoredPosition = new Vector2(-right, -top); r.sizeDelta = new Vector2(w, h); }
        private static void PlaceBL(RectTransform r, float left, float bottom, float w, float h) { r.anchorMin = r.anchorMax = new Vector2(0, 0); r.pivot = new Vector2(0, 0); r.anchoredPosition = new Vector2(left, bottom); r.sizeDelta = new Vector2(w, h); }

        private static Image Img(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = sprite; i.color = color; i.raycastTarget = false;
            return i;
        }

        private static ArabicText Txt(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false; t.overflowMode = TextOverflowModes.Overflow;
            var a = go.AddComponent<ArabicText>(); a.ArabicDigits = true; a.Text = text;
            return a;
        }

    }

    /// <summary>A ring around a painted plate: colour and width reaching outwards.</summary>
    public readonly struct KeyValuePair2
    {
        public readonly Color Color; public readonly float Width;
        public KeyValuePair2(Color c, float w) { Color = c; Width = w; }
    }

    public static class KitProcSpecExtensions
    {
        public static KitProc.Spec WithRings(this KitProc.Spec s, params KeyValuePair2[] rings)
        {
            foreach (var r in rings) s.Rings.Add(new System.Collections.Generic.KeyValuePair<Color, float>(r.Color, r.Width));
            return s;
        }
    }
}
