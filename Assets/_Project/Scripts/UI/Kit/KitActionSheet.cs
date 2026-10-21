using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// A small bottom sheet with a title and a few buttons (message this player, report, block...). It builds itself from the kit theme when
    /// shown, so any screen can use it without a prefab: <c>KitActionSheet.Show(canvas, "title", ("label", action), ...)</c>.
    /// </summary>
    public sealed class KitActionSheet : MonoBehaviour
    {
        public readonly struct Choice
        {
            public readonly string Label; public readonly Action Run; public readonly bool Danger; public readonly bool Quiet;
            public Choice(string label, Action run, bool danger = false, bool quiet = false) { Label = label; Run = run; Danger = danger; Quiet = quiet; }
        }

        private static KitActionSheet open;

        public static bool IsOpen => open != null;
        public static void CloseCurrent() { if (open != null) Destroy(open.gameObject); open = null; }

        public static void Show(Transform canvasRoot, string title, params Choice[] choices)
        {
            CloseCurrent();
            var th = KitTheme.Current;
            if (canvasRoot == null || th == null) return;
            var root = new GameObject("ActionSheet", typeof(RectTransform));
            root.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)root.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            root.transform.SetAsLastSibling();
            var sheet = root.AddComponent<KitActionSheet>();
            open = sheet;

            // dim, tapping it closes the sheet
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
            dim.transform.SetParent(root.transform, false);
            var dr = (RectTransform)dim.transform; dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = dr.offsetMax = Vector2.zero;
            var di = dim.GetComponent<Image>(); di.color = new Color(0, 0, 0, 0.62f);
            dim.GetComponent<Button>().onClick.AddListener(CloseCurrent);

            // the panel at the bottom
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(root.transform, false);
            var pr = (RectTransform)panel.transform; pr.anchorMin = new Vector2(0, 0); pr.anchorMax = new Vector2(1, 0); pr.pivot = new Vector2(0.5f, 0); pr.anchoredPosition = Vector2.zero; pr.sizeDelta = new Vector2(0, 0);
            var pi = panel.GetComponent<Image>(); pi.sprite = th.leatherDark; pi.type = Image.Type.Sliced; pi.pixelsPerUnitMultiplier = 1.4f;
            var v = panel.GetComponent<VerticalLayoutGroup>(); v.padding = new RectOffset(48, 48, 60, 90); v.spacing = 26; v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var font = th.fontLalezar != null ? th.fontLalezar : th.fontHeading;      // the heavy heading font reads well on the plates
            if (!string.IsNullOrEmpty(title)) Label(panel.transform, title, font, 50, th.leatherText, 96);
            foreach (var c in choices)
            {
                var ch = c;
                var b = new GameObject("Choice", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                b.transform.SetParent(panel.transform, false);
                b.GetComponent<LayoutElement>().preferredHeight = 130;
                var img = b.GetComponent<Image>();
                img.sprite = ch.Danger ? th.leatherRed : ch.Quiet ? th.leatherDark : th.brassPlate; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.4f;
                var btn = b.GetComponent<Button>(); btn.targetGraphic = img;
                btn.onClick.AddListener(() => { CloseCurrent(); ch.Run?.Invoke(); });
                Color text = ch.Danger ? th.leatherText : ch.Quiet ? th.ivoryDim : th.brassText;
                var lab = Label(b.transform, ch.Label, font, 48, text, 130);
                var lrt = (RectTransform)lab.transform; lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(pr);
        }

        private static ArabicText Label(Transform parent, string text, TMP_FontAsset font, float size, Color color, float height)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Midline; t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            var a = go.AddComponent<ArabicText>(); a.ArabicDigits = false; a.Text = text;
            return a;
        }

        private void OnDestroy() { if (open == this) open = null; }
    }
}
