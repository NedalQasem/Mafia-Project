using MafiaNight.Data.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// A player's role card, big: opened by pressing the little card beside his seat (Docs/mockups/role-cards.html, section 2). The card turns over to what the role does when it is pressed,
    /// and a press anywhere else closes the viewer. Built from the kit theme when shown: <c>KitCardViewer.Show(canvas, def, layers, "owner's name")</c>.
    /// </summary>
    public sealed class KitCardViewer : MonoBehaviour
    {
        private const float CardWidth = 560f;
        private static KitCardViewer open;
        public static bool IsOpen => open != null;
        public static void CloseCurrent() { if (open != null) Destroy(open.gameObject); open = null; }

        private RectTransform card;
        private Vector2 last;

        public static void Show(Transform canvasRoot, RoleCardDef def, CardLayers layers, string owner)
        {
            CloseCurrent();
            var th = KitTheme.Current;
            if (canvasRoot == null || th == null || def == null) return;
            var root = new GameObject("CardViewer", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)root.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            root.transform.SetAsLastSibling();
            var bg = root.GetComponent<Image>(); bg.color = new Color32(0x0A, 0x07, 0x05, 238); bg.raycastTarget = true;      // stops every tap behind it; a press on it closes the viewer
            var viewer = root.AddComponent<KitCardViewer>();
            open = viewer;
            KitTap.On(root.transform, CloseCurrent);

            var c = KitRoleCard.Create(root.transform, def, layers, CardWidth);
            viewer.card = (RectTransform)c.transform;
            viewer.card.anchorMin = viewer.card.anchorMax = new Vector2(0.5f, 0.52f); viewer.card.pivot = new Vector2(0.5f, 0.5f); viewer.card.anchoredPosition = Vector2.zero;
            c.Pressed += k => k.SetFace(k.Current == KitRoleCard.Face.Front ? KitRoleCard.Face.Back : KitRoleCard.Face.Front, true);

            var who = Label(root.transform, string.IsNullOrEmpty(owner) ? def.Name : owner + " · " + def.Name, th.fontLalezar, 58, th.brassLight);
            var wr = (RectTransform)who.transform; wr.anchorMin = new Vector2(0, 1); wr.anchorMax = new Vector2(1, 1); wr.pivot = new Vector2(0.5f, 1); wr.sizeDelta = new Vector2(-100, 120); wr.anchoredPosition = new Vector2(0, -150);
            var hint = Label(root.transform, "اضغط الكرت لتقلبه · اضغط برّا لتغلق", th.fontBody, 32, th.ivoryDim);
            var hr = (RectTransform)hint.transform; hr.anchorMin = new Vector2(0, 0); hr.anchorMax = new Vector2(1, 0); hr.pivot = new Vector2(0.5f, 0); hr.sizeDelta = new Vector2(-100, 90); hr.anchoredPosition = new Vector2(0, 150);
            Canvas.ForceUpdateCanvases(); viewer.Fit();
        }

        private static ArabicText Label(Transform parent, string text, TMP_FontAsset font, float size, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Midline; t.raycastTarget = false;
            t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = size * 0.5f; t.overflowMode = TextOverflowModes.Ellipsis;      // any name keeps one line
            var a = go.AddComponent<ArabicText>(); a.Text = text;
            return a;
        }

        private void LateUpdate() { var r = ((RectTransform)transform).rect.size; if (r != last) Fit(); }

        /// <summary>Keeps the card inside the free middle of the screen: smaller on a short or narrow one, never bigger than its own size.</summary>
        private void Fit()
        {
            var r = ((RectTransform)transform).rect.size; last = r;
            if (card == null || r.y < 300f || r.x < 300f) return;
            float h = CardWidth * KitRoleCard.RefH / KitRoleCard.RefW;
            card.localScale = Vector3.one * Mathf.Clamp(Mathf.Min((r.x - 100f) / CardWidth, (r.y - 560f) / h), 0.3f, 1f);
        }

        private void OnDestroy() { if (open == this) open = null; }
    }
}
