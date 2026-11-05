using System;
using MafiaNight.Data.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Your role, as the card: a full-screen "you are..." moment with the role card (a tap turns it over to what the role does), a line under it
    /// (your partners, or the tier of the role) and one button. Built from the kit theme when shown: <c>KitRoleReveal.Show(canvas, def, layers, line, "ready", onReady)</c>.
    /// </summary>
    public sealed class KitRoleReveal : MonoBehaviour
    {
        private static KitRoleReveal open;
        public static bool IsOpen => open != null;
        public static void CloseCurrent() { if (open != null) Destroy(open.gameObject); open = null; }

        private ArabicText buttonLabel;

        public static void Show(Transform canvasRoot, RoleCardDef def, CardLayers layers, string line, string button, Action ready, string roster = null)
        {
            CloseCurrent();
            var th = KitTheme.Current;
            if (canvasRoot == null || th == null || def == null) return;
            var root = new GameObject("RoleReveal", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)root.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            root.transform.SetAsLastSibling();
            var bg = root.GetComponent<Image>(); bg.color = new Color32(0x0E, 0x0A, 0x07, 252); bg.raycastTarget = true;      // opaque, and it stops every tap behind it
            var sheet = root.AddComponent<KitRoleReveal>();
            open = sheet;

            var title = Label(root.transform, "دورك بهالسهرة", th.fontLalezar, 64, th.brassLight, TextAlignmentOptions.Midline);
            var tr = (RectTransform)title.transform; tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(0.5f, 1); tr.sizeDelta = new Vector2(-120, 130); tr.anchoredPosition = new Vector2(0, -170);

            if (!string.IsNullOrEmpty(roster))
            {   // the roles of this match, public to everybody, so a night call never gives anybody away
                var list = Label(root.transform, "الأدوار بهالسهرة: " + roster, th.fontBody, 34, th.ivoryDim, TextAlignmentOptions.Midline);
                var lt = list.GetComponent<TMP_Text>(); lt.enableAutoSizing = true; lt.fontSizeMax = 34; lt.fontSizeMin = 22; lt.maxVisibleLines = 2;
                var lrr = (RectTransform)list.transform; lrr.anchorMin = new Vector2(0, 1); lrr.anchorMax = new Vector2(1, 1); lrr.pivot = new Vector2(0.5f, 1); lrr.sizeDelta = new Vector2(-120, 100); lrr.anchoredPosition = new Vector2(0, -300);
            }

            // the card: as wide as the screen allows but never taller than the middle of the screen (short and tall phones alike)
            var card = KitRoleCard.Create(root.transform, def, layers, 640f);
            var cr = (RectTransform)card.transform; cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.54f); cr.pivot = new Vector2(0.5f, 0.5f); cr.anchoredPosition = Vector2.zero;
            card.Pressed += c => c.SetFace(c.Current == KitRoleCard.Face.Front ? KitRoleCard.Face.Back : KitRoleCard.Face.Front, true);
            var fit = root.AddComponent<KitCardFit>(); fit.Setup(cr, 640f, KitRoleCard.RefH / KitRoleCard.RefW, string.IsNullOrEmpty(roster) ? 0f : 110f);
            Canvas.ForceUpdateCanvases(); fit.Apply();

            var hint = Label(root.transform, "اضغط على الكرت لتقلبه", th.fontBody, 34, th.ivoryDim, TextAlignmentOptions.Midline);
            var hr = (RectTransform)hint.transform; hr.anchorMin = new Vector2(0, 0); hr.anchorMax = new Vector2(1, 0); hr.pivot = new Vector2(0.5f, 0); hr.sizeDelta = new Vector2(-120, 90); hr.anchoredPosition = new Vector2(0, 405);

            var info = Label(root.transform, line, th.fontBody, 40, th.leatherText, TextAlignmentOptions.Midline);
            var ir = (RectTransform)info.transform; ir.anchorMin = new Vector2(0, 0); ir.anchorMax = new Vector2(1, 0); ir.pivot = new Vector2(0.5f, 0); ir.sizeDelta = new Vector2(-120, 110); ir.anchoredPosition = new Vector2(0, 290);
            info.GetComponent<TMP_Text>().enableAutoSizing = true; info.GetComponent<TMP_Text>().fontSizeMax = 40; info.GetComponent<TMP_Text>().fontSizeMin = 24;

            var b = new GameObject("Ready", typeof(RectTransform), typeof(Image));
            b.transform.SetParent(root.transform, false);
            var br = (RectTransform)b.transform; br.anchorMin = br.anchorMax = new Vector2(0.5f, 0); br.pivot = new Vector2(0.5f, 0); br.sizeDelta = new Vector2(760, 130); br.anchoredPosition = new Vector2(0, 130);
            var bi = b.GetComponent<Image>(); bi.sprite = th.brassPlate; bi.type = Image.Type.Sliced; bi.pixelsPerUnitMultiplier = 1.4f;
            KitTap.On(b.transform, () => ready?.Invoke());
            sheet.buttonLabel = Label(b.transform, button, th.fontLalezar, 52, th.brassText, TextAlignmentOptions.Midline);
            var lr = (RectTransform)sheet.buttonLabel.transform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        }

        /// <summary>The button says what the match waits for (after the player pressed it).</summary>
        public static void SetButton(string text) { if (open != null && open.buttonLabel != null && open.buttonLabel.Text != text) open.buttonLabel.Text = text; }

        private static ArabicText Label(Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false; t.overflowMode = TextOverflowModes.Ellipsis;
            var a = go.AddComponent<ArabicText>(); a.ArabicDigits = true; a.Text = text;
            return a;
        }

        private void OnDestroy() { if (open == this) open = null; }
    }

    /// <summary>Keeps the card inside the free middle of a screen: scales it down when the screen is short (a wide or tablet-like ratio), never up.</summary>
    public sealed class KitCardFit : MonoBehaviour
    {
        private RectTransform card;
        private float width, aspect, extraTop;
        private Vector2 last;

        public void Setup(RectTransform card, float width, float aspect, float extraTop = 0f) { this.card = card; this.width = width; this.aspect = aspect; this.extraTop = extraTop; }

        private void LateUpdate() { var r = ((RectTransform)transform).rect.size; if (r != last) Apply(); }

        public void Apply()
        {
            var r = ((RectTransform)transform).rect.size; last = r;
            if (card == null || r.y < 300f || r.x < 300f) return;      // not laid out yet
            float room = r.y - 170f - 130f - extraTop - 760f;           // under the title (and the roster of roles), above the hint, line and button
            float h = width * aspect;
            card.localScale = Vector3.one * Mathf.Clamp(room / h, 0.35f, 1f) * Mathf.Min(1f, (r.x - 80f) / width);
        }
    }
}
