using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The bottom of the table screens: the match's written chat (the last lines floating over the room's background, the box to write in with the name of its channel on a
    /// brass tab, or a lock when the match closes it) and the microphone button with a small plate that says why it is open or closed. It is built in code from the kit theme
    /// and knows no match rules: the screen flow hands it a <see cref="Vm"/> each time something changes. Docs/mockups/match-roles.html (the dock) is its picture.
    /// </summary>
    public sealed class KitMatchDock : MonoBehaviour
    {
        public enum Mic { Open, Closed, Waiting }

        public sealed class Line { public string Name = "", Text = ""; public bool Mine, Secret; }

        public sealed class Vm
        {
            public IList<Line> Lines = new List<Line>();
            public string Tab = "", Prompt = "";
            public bool Secret, Locked, Tape;
            public string LockedText = "";
            public Mic MicLook;
            public string MicReason = "";
            public bool MicTappable;
        }

        /// <summary>Height of the dock in canvas units; the table screens above it shrink by this much on a short screen.</summary>
        public const float Height = 340f;
        private const float CompactHeight = 236f;
        private const int Slots = 3;
        private bool compact, fitted;
        private RectTransform feedRt;
        /// <summary>The height the dock has now: on a short screen it keeps one line only, so the table above it keeps its size.</summary>
        public float CurrentHeight { get; private set; } = Height;

        public event Action<string> OnSend;
        public event Action OnMic;

        private sealed class Slot { public CanvasGroup Group; public ArabicText Name, Text; }
        private readonly List<Slot> slots = new List<Slot>();
        private Image micFace, micIcon, lamp, platePlate;
        private ArabicText plateText, tabText, mirror, lockText;
        private GameObject sendBox, fieldBox, lockBox, tape, placeholder;
        private Image tabImage;
        private RectTransform tabRt, mirrorRt, placeholderRt, lockIconRt;
        private KitArabicInput field;
        private bool micTappable;
        private string lastSig = "";
        private Vm last;
        private string flash = "";
        private float flashUntil;

        /// <summary>Says something short on the plate for a moment (a line that was refused).</summary>
        public void Flash(string message) { if (string.IsNullOrEmpty(message)) return; flash = message; flashUntil = Time.unscaledTime + 2.6f; if (last != null) Render(last); }

        /// <summary>Takes the height the screen allows: a short screen (a tablet, a small window) keeps one chat line.</summary>
        private void Fit()
        {
            var p = transform.parent as RectTransform;
            if (p == null) return;
            bool c = p.rect.height < 2100f;
            if (fitted && c == compact) return;
            fitted = true; compact = c;
            CurrentHeight = c ? CompactHeight : Height;
            ((RectTransform)transform).sizeDelta = new Vector2(0, CurrentHeight);
            feedRt.offsetMin = new Vector2(40, 144); feedRt.offsetMax = new Vector2(-40, 144 + (c ? 62 : 66 * Slots));
        }

        private void Update() { Fit(); if (flashUntil > 0f && Time.unscaledTime >= flashUntil) { flashUntil = 0f; if (last != null) Render(last); } }

        public static KitMatchDock Create(RectTransform parent)
        {
            var th = KitTheme.Current;
            var go = new GameObject("MatchDock", typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0); rt.sizeDelta = new Vector2(0, Height); rt.anchoredPosition = Vector2.zero;
            var dock = go.AddComponent<KitMatchDock>();
            dock.Build(rt, th);
            rt.SetAsLastSibling();        // over the frames, which fill the screen (they shrink to leave the dock its place)
            return dock;
        }

        // ---------------------------------------------------------------- building

        private void Build(RectTransform root, KitTheme th)
        {
            // the dock's own background: the room fades into leather towards the bottom; it also stops taps that miss a part
            var bg = Img(root, "Fade", KitProc.Paint("dockfade", new KitProc.Spec { W = 64, H = 540, Top = Color.clear, Mid = new Color(0.035f, 0.02f, 0.014f, 0.82f), Bottom = new Color(0.035f, 0.02f, 0.014f, 0.98f), MidAt = 0.45f }), Color.white, true);
            { var r = bg.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = new Vector2(0, 200); }     // its top fades into the table above, so there is no seam

            // the last lines (the newest at the bottom, the older ones fainter)
            var feed = new GameObject("Feed", typeof(RectTransform));
            var fr = (RectTransform)feed.transform; fr.SetParent(root, false); feedRt = fr;
            fr.anchorMin = new Vector2(0, 0); fr.anchorMax = new Vector2(1, 0); fr.pivot = new Vector2(0.5f, 0); fr.offsetMin = new Vector2(40, 150); fr.offsetMax = new Vector2(-40, 150 + 66 * Slots);
            for (int i = 0; i < Slots; i++)
            {
                var line = new GameObject("Line" + i, typeof(RectTransform), typeof(CanvasGroup), typeof(HorizontalLayoutGroup));
                var lr = (RectTransform)line.transform; lr.SetParent(fr, false);
                lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 0); lr.pivot = new Vector2(0.5f, 0); lr.sizeDelta = new Vector2(0, 62); lr.anchoredPosition = new Vector2(0, 66 * (Slots - 1 - i));
                var h = line.GetComponent<HorizontalLayoutGroup>();
                h.reverseArrangement = true; h.childAlignment = TextAnchor.MiddleLeft; h.spacing = 14; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = true;
                var name = Label(line.transform, "", th.fontBody, 40, th.brassLight, TextAlignmentOptions.MidlineRight, 1, "Name", true);
                name.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
                name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0;
                var text = Label(line.transform, "", th.fontBody, 40, th.ivory, TextAlignmentOptions.MidlineRight, 1, "Text", true);
                text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                slots.Add(new Slot { Group = line.GetComponent<CanvasGroup>(), Name = name, Text = text });
            }

            // the bottom row, built from the right: the mic, the plate that says why, the box to write in (or the lock), send
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rr = (RectTransform)row.transform; rr.SetParent(root, false);
            rr.anchorMin = new Vector2(0, 0); rr.anchorMax = new Vector2(1, 0); rr.pivot = new Vector2(0.5f, 0); rr.offsetMin = new Vector2(36, 18); rr.offsetMax = new Vector2(-36, 138);
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.reverseArrangement = true; hl.childAlignment = TextAnchor.MiddleRight; hl.spacing = 14; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;

            // mic
            var mic = Box(rr, "Mic", 124, 0);
            micFace = Img(mic, "Face", null, Color.white, false);
            micIcon = Img(mic, "Icon", null, Color.white, false);
            { var ir = micIcon.rectTransform; ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f); ir.sizeDelta = new Vector2(66, 66); ir.anchoredPosition = Vector2.zero; }
            lamp = Img(mic, "Lamp", null, Color.white, false);
            { var lr = lamp.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(1, 1); lr.sizeDelta = new Vector2(30, 30); lr.anchoredPosition = new Vector2(-14, -14); }
            KitTap.On(mic, () => { if (micTappable) OnMic?.Invoke(); });

            // the plate with the reason
            var pl = Box(rr, "Plate", 300, 0);
            platePlate = Img(pl, "Bg", Sliced("dockplate", 14, new Color32(0x1A, 0x11, 0x0D, 240), new Color32(0x6B, 0x5A, 0x3F, 255), 3), Color.white, false);
            platePlate.type = Image.Type.Sliced; platePlate.pixelsPerUnitMultiplier = 1f;
            { var r = platePlate.rectTransform; r.anchorMin = new Vector2(0, 0.5f); r.anchorMax = new Vector2(1, 0.5f); r.pivot = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(0, 76); r.anchoredPosition = Vector2.zero; }
            plateText = Label(platePlate.transform, "", th.fontBody, 34, th.brassLight, TextAlignmentOptions.Midline, 1, "Reason", true);
            { var r = (RectTransform)plateText.transform; Stretch(r); r.offsetMin = new Vector2(14, 0); r.offsetMax = new Vector2(-14, 0); plateText.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold; }

            // the box to write in
            fieldBox = Box(rr, "Field", 0, 1).gameObject;
            var fimg = Img(fieldBox.transform, "Bg", Sliced("dockfield", 16, new Color32(0x0E, 0x0A, 0x07, 235), new Color32(0x2A, 0x20, 0x18, 255), 3), Color.white, true);
            fimg.type = Image.Type.Sliced; fimg.pixelsPerUnitMultiplier = 1f;
            { var r = fimg.rectTransform; r.anchorMin = new Vector2(0, 0.5f); r.anchorMax = new Vector2(1, 0.5f); r.pivot = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(0, 100); r.anchoredPosition = Vector2.zero; }
            tabImage = Img(fimg.transform, "Tab", Sliced("dockTab", 10, new Color32(0xC9, 0xA0, 0x50, 255), Color.clear, 0), Color.white, false);
            tabImage.type = Image.Type.Sliced; tabImage.pixelsPerUnitMultiplier = 1f; tabRt = tabImage.rectTransform;
            tabRt.anchorMin = tabRt.anchorMax = new Vector2(1, 0.5f); tabRt.pivot = new Vector2(1, 0.5f); tabRt.sizeDelta = new Vector2(190, 62); tabRt.anchoredPosition = new Vector2(-18, 0);
            tabText = Label(tabImage.transform, "", th.fontLalezar, 34, th.brassText, TextAlignmentOptions.Midline, 1, "TabText", true);
            Stretch((RectTransform)tabText.transform);
            var ph = Label(fimg.transform, "اكتب رسالة…", th.fontBody, 36, th.ivoryDim, TextAlignmentOptions.MidlineRight, 1, "Placeholder", true);
            placeholder = ph.gameObject; placeholderRt = (RectTransform)ph.transform;
            mirror = Label(fimg.transform, "", th.fontBody, 38, th.ivory, TextAlignmentOptions.MidlineRight, 1, "Mirror", false);
            mirrorRt = (RectTransform)mirror.transform;
            Stretch(placeholderRt); Stretch(mirrorRt);
            field = KitArabicInput.Attach(fimg.rectTransform, mirror, placeholder, TMP_InputField.ContentType.Standard, 160);
            field.OnSubmit += Send;
            field.OnChanged += t => { if (sendBox != null) sendBox.SetActive(!string.IsNullOrWhiteSpace(t) && fieldBox.activeSelf); };

            // the lock (the night, or the silencer's tape)
            lockBox = Box(rr, "Locked", 0, 1).gameObject;
            var lbg = Img(lockBox.transform, "Bg", Sliced("dockfield", 16, new Color32(0x14, 0x0C, 0x08, 240), new Color32(0x2A, 0x20, 0x18, 255), 3), Color.white, true);
            lbg.type = Image.Type.Sliced; lbg.pixelsPerUnitMultiplier = 1f;
            { var r = lbg.rectTransform; r.anchorMin = new Vector2(0, 0.5f); r.anchorMax = new Vector2(1, 0.5f); r.pivot = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(0, 100); r.anchoredPosition = Vector2.zero; }
            tape = Img(lbg.transform, "Tape", Sliced("docktape", 3, new Color32(0xD2, 0xC3, 0x98, 255), new Color32(0x8A, 0x7A, 0x4C, 255), 2), Color.white, false).gameObject;
            tape.GetComponent<Image>().type = Image.Type.Sliced;
            { var r = (RectTransform)tape.transform; r.anchorMin = new Vector2(0, 0.5f); r.anchorMax = new Vector2(1, 0.5f); r.sizeDelta = new Vector2(24, 72); r.anchoredPosition = Vector2.zero; r.localRotation = Quaternion.Euler(0, 0, -1.5f); }
            var li = Img(lbg.transform, "LockIcon", KitSvg.Icon(KitSvg.Lock, 2f), new Color32(0xB9, 0x90, 0x4A, 255), false);
            lockIconRt = li.rectTransform; lockIconRt.anchorMin = lockIconRt.anchorMax = new Vector2(1, 0.5f); lockIconRt.sizeDelta = new Vector2(50, 50); lockIconRt.anchoredPosition = new Vector2(-30, 0);
            lockText = Label(lbg.transform, "", th.fontBody, 38, new Color32(0x8D, 0x74, 0x4A, 255), TextAlignmentOptions.MidlineRight, 1, "LockText", true);
            { var r = (RectTransform)lockText.transform; Stretch(r); r.offsetMin = new Vector2(24, 0); r.offsetMax = new Vector2(-96, 0); lockText.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold; }

            // send
            var snd = Box(rr, "Send", 100, 0);
            sendBox = snd.gameObject;
            var sface = Img(snd, "Face", KitProc.Paint("docksend", new KitProc.Spec { W = 96, H = 96, Radius = 48, Margin = 6, Top = new Color32(0xA2, 0x26, 0x2F, 255), Mid = new Color32(0x8A, 0x1C, 0x24, 255), Bottom = new Color32(0x56, 0x10, 0x16, 255), MidAt = 0.55f, Shadow = Color.black, ShadowDy = 3, ShadowBlur = 1.2f }), Color.white, false);
            Center(sface.rectTransform, 108);
            var sic = Img(snd, "Icon", KitSvg.Icon(KitSvg.Send, 2.2f), new Color32(0xF0, 0xDC, 0xD8, 255), false);
            Center(sic.rectTransform, 48);
            KitTap.On(snd, () => Send(field.Text));
            sendBox.SetActive(false);
        }

        // ---------------------------------------------------------------- drawing

        public void Render(Vm v)
        {
            if (v == null) return;
            last = v;
            Fit();
            var th = KitTheme.Current;
            // lines
            for (int i = 0; i < Slots; i++)
            {
                var s = slots[i];
                int src = v.Lines.Count - Slots + i;                 // the oldest of the last three first
                bool has = !v.Locked && src >= 0 && src < v.Lines.Count && (!compact || i == Slots - 1);
                s.Group.gameObject.SetActive(has);
                if (!has) continue;
                var l = v.Lines[src];
                s.Group.alpha = i == Slots - 1 ? 1f : i == Slots - 2 ? 0.7f : 0.42f;
                s.Name.Text = KitText.Clip(l.Name, 14) + ":";
                s.Name.GetComponent<TMP_Text>().color = l.Mine ? th.ivoryDim : l.Secret ? new Color32(0xE0, 0x58, 0x5F, 255) : th.brassLight;
                s.Text.Text = l.Text;
            }

            // the box, or the lock
            fieldBox.SetActive(!v.Locked);
            lockBox.SetActive(v.Locked);
            if (v.Locked)
            {
                lockText.Text = v.LockedText; tape.SetActive(v.Tape);
                lockIconRt.gameObject.SetActive(!v.Tape);
                { var r = (RectTransform)lockText.transform; r.offsetMax = new Vector2(v.Tape ? -24 : -96, 0); r.offsetMin = new Vector2(24, 0); }
                lockText.GetComponent<TMP_Text>().alignment = v.Tape ? TextAlignmentOptions.Midline : TextAlignmentOptions.MidlineRight;
                lockText.GetComponent<TMP_Text>().color = v.Tape ? new Color32(0x2A, 0x20, 0x14, 255) : new Color32(0x8D, 0x74, 0x4A, 255);
                sendBox.SetActive(false);
            }
            else
            {
                tabText.Text = v.Tab;
                var tt = tabText.GetComponent<TMP_Text>(); tt.ForceMeshUpdate();
                float w = Mathf.Clamp(tt.preferredWidth + 56, 150, 360);
                tabRt.sizeDelta = new Vector2(w, 62);
                tabImage.color = v.Secret ? new Color32(0xA3, 0x24, 0x2D, 255) : Color.white;
                tabText.GetComponent<TMP_Text>().color = v.Secret ? new Color32(0xF3, 0xE6, 0xC8, 255) : th.brassText;
                placeholderRt.offsetMin = new Vector2(20, 0); placeholderRt.offsetMax = new Vector2(-(w + 40), 0);
                mirrorRt.offsetMin = new Vector2(20, 0); mirrorRt.offsetMax = new Vector2(-(w + 40), 0);
                var pa = placeholder.GetComponent<ArabicText>(); if (pa != null && pa.Text != v.Prompt) pa.Text = v.Prompt;
            }

            // the mic
            micTappable = v.MicTappable;
            string sig = v.MicLook + "|" + v.MicReason;
            bool flashing = flashUntil > Time.unscaledTime;
            plateText.Text = flashing ? flash : v.MicReason;
            if (sig != lastSig)
            {
                lastSig = sig;
                var face = MicFace(v.MicLook);
                micFace.sprite = face;
                float size = 124f, m = (face.rect.width / KitProc.Q - size) * 0.5f;
                micFace.rectTransform.anchorMin = Vector2.zero; micFace.rectTransform.anchorMax = Vector2.one; micFace.rectTransform.offsetMin = new Vector2(-m, -m); micFace.rectTransform.offsetMax = new Vector2(m, m);
                micIcon.sprite = KitSvg.Icon(v.MicLook == Mic.Open ? KitSvg.Mic : KitSvg.MicOff, 2.2f);
                micIcon.color = v.MicLook == Mic.Open ? (Color)new Color32(0x2A, 0x1A, 0x08, 255) : v.MicLook == Mic.Closed ? new Color32(0xE6, 0xB3, 0xB6, 255) : th.ivoryDim;
                lamp.sprite = KitProc.Paint("dockLamp" + (int)v.MicLook, new KitProc.Spec { W = 26, H = 26, Radius = 13, Margin = 4, Top = Lamp(v.MicLook), Bottom = Lamp(v.MicLook), Edge = new Color32(0x0B, 0x08, 0x07, 255), EdgePx = 3, Shadow = Color.black, ShadowDy = 2, ShadowBlur = 1.2f });
                float lm = (lamp.sprite.rect.width / KitProc.Q - 30f) * 0.5f; lamp.rectTransform.sizeDelta = new Vector2(30 + 2 * lm, 30 + 2 * lm);
                plateText.GetComponent<TMP_Text>().color = v.MicLook == Mic.Open ? th.brassLight : v.MicLook == Mic.Closed ? (Color)new Color32(0xE6, 0xB3, 0xB6, 255) : th.ivoryDim;
                platePlate.sprite = Sliced("dockplate" + (int)v.MicLook, 14, new Color32(0x1A, 0x11, 0x0D, 240), v.MicLook == Mic.Open ? new Color32(0xB9, 0x90, 0x48, 200) : v.MicLook == Mic.Closed ? new Color32(0x8A, 0x1C, 0x24, 255) : new Color32(0x6B, 0x5A, 0x3F, 255), 3);
            }
        }

        private static Color Lamp(Mic m) => m == Mic.Open ? (Color)new Color32(0x6F, 0xE0, 0xB6, 255) : m == Mic.Closed ? new Color32(0xE0, 0x58, 0x5F, 255) : new Color32(0xB8, 0x8A, 0x3C, 255);

        private static Sprite MicFace(Mic m)
        {
            var s = new KitProc.Spec { W = 124, H = 124, Radius = 62, Margin = 10, MidAt = 0.55f, Shadow = Color.black, ShadowDy = 4, ShadowBlur = 1.4f };
            if (m == Mic.Open) { s.Top = new Color32(0xF0, 0xD7, 0x8F, 255); s.Mid = new Color32(0xC9, 0x9A, 0x3C, 255); s.Bottom = new Color32(0x7D, 0x5A, 0x1C, 255); s.TopLight = new Color(1, 1, 1, 0.25f); s.TopLightPx = 2; }
            else if (m == Mic.Closed) { s.Top = new Color32(0x3B, 0x2A, 0x20, 255); s.Mid = new Color32(0x2A, 0x1C, 0x15, 255); s.Bottom = new Color32(0x1A, 0x11, 0x0D, 255); s.Edge = new Color32(0x8A, 0x1C, 0x24, 255); s.EdgePx = 5; }
            else { s.Top = new Color32(0x2A, 0x20, 0x18, 255); s.Mid = new Color32(0x1F, 0x17, 0x11, 255); s.Bottom = new Color32(0x14, 0x0E, 0x0A, 255); s.Edge = new Color32(0x6B, 0x5A, 0x3F, 255); s.EdgePx = 4; }
            return KitProc.Paint("dockmic" + (int)m, s);
        }

        private void Send(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || !fieldBox.activeSelf) return;
            field.Text = "";
            if (sendBox != null) sendBox.SetActive(false);
            OnSend?.Invoke(text);
        }

        // ---------------------------------------------------------------- small helpers

        private static Sprite Sliced(string key, float radius, Color fill, Color edge, float edgePx)
        {
            var s = new KitProc.Spec { Radius = radius, Top = fill, Bottom = fill, Sliced = true };
            if (edge.a > 0f) { s.Edge = edge; s.EdgePx = edgePx; }
            return KitProc.Paint("sl" + key + ColorUtility.ToHtmlStringRGBA(fill) + ColorUtility.ToHtmlStringRGBA(edge), s);
        }

        /// <summary>A transparent cell of the bottom row: a fixed width (or flexible with 1), the full height.</summary>
        private static RectTransform Box(Transform parent, string name, float width, float flexible)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            if (width > 0) { le.preferredWidth = width; le.minWidth = flexible > 0 ? 0 : width; }
            le.flexibleWidth = flexible;
            return (RectTransform)go.transform;
        }

        private static Image Img(Transform parent, string name, Sprite sprite, Color color, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = sprite; i.color = color; i.raycastTarget = raycast;
            return i;
        }

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static void Center(RectTransform r, float size) { r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(size, size); r.anchoredPosition = Vector2.zero; }

        private static ArabicText Label(Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align, int maxLines, string name, bool shrink)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
            if (shrink) { t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = size * 0.6f; }
            t.overflowMode = TextOverflowModes.Ellipsis; t.maxVisibleLines = maxLines;
            var a = go.AddComponent<ArabicText>(); a.ArabicDigits = true; a.Text = text;
            return a;
        }
    }
}
