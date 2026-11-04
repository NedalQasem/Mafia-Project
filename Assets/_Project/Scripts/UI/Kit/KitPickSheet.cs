using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The night / avenge choice of the roles that pick a second target (the silencer, the sniper, the Avenger): a sheet from the bottom with a title, a line of
    /// explanation, a scrolling list of players (the chosen one in brass) and one or two buttons. It builds itself from the kit theme and stays until the match closes
    /// it, because the choice is part of the phase: <c>KitPickSheet.Show(canvas, spec)</c>, called again with the new selection to redraw.
    /// </summary>
    public sealed class KitPickSheet : MonoBehaviour
    {
        public sealed class Row { public string Id = "", Name = ""; }
        public sealed class Button2 { public string Label = ""; public Action Run; public bool Quiet, Disabled; }

        public sealed class Spec
        {
            public string Title = "", Body = "", Timer = "", Selected;
            public IList<Row> Rows = new List<Row>();
            public Action<string> Picked;
            public IList<Button2> Buttons = new List<Button2>();
        }

        private static KitPickSheet open;
        public static bool IsOpen => open != null;
        public static void CloseCurrent() { if (open != null) Destroy(open.gameObject); open = null; }
        /// <summary>For the editor tools, where Destroy is not allowed.</summary>
        public static void CloseCurrentImmediate() { if (open != null) DestroyImmediate(open.gameObject); open = null; }

        private ScrollRect scroll;

        /// <summary>Opens the sheet, or redraws the open one with the new selection (the list keeps its scroll position).</summary>
        public static void Show(Transform canvasRoot, Spec spec)
        {
            var th = KitTheme.Current;
            if (canvasRoot == null || th == null || spec == null) return;
            float keep = open != null && open.scroll != null ? open.scroll.verticalNormalizedPosition : 1f;
            CloseCurrent();
            var root = new GameObject("PickSheet", typeof(RectTransform));
            root.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)root.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            root.transform.SetAsLastSibling();
            var sheet = root.AddComponent<KitPickSheet>();
            open = sheet;

            // a dim that stops every tap behind the sheet; the match closes the sheet, the player cannot
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(root.transform, false);
            var dr = (RectTransform)dim.transform; dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = dr.offsetMax = Vector2.zero;
            var di = dim.GetComponent<Image>(); di.color = new Color(0, 0, 0, 0.5f); di.raycastTarget = true;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var pr = (RectTransform)panel.transform; pr.anchorMin = new Vector2(0, 0); pr.anchorMax = new Vector2(1, 0.7f); pr.offsetMin = pr.offsetMax = Vector2.zero;
            var pi = panel.GetComponent<Image>(); pi.sprite = th.leatherDark; pi.type = Image.Type.Sliced; pi.pixelsPerUnitMultiplier = 1.4f; pi.raycastTarget = true;

            const float footer = 260f;
            var head = Label(panel.transform, spec.Title, th.fontLalezar, 58, th.leatherText, TextAlignmentOptions.MidlineRight, 1);
            StretchTop(head, 230, 40, 26, 110);
            var timer = Label(panel.transform, spec.Timer, th.fontBody, 40, th.brass, TextAlignmentOptions.MidlineLeft, 1, "Timer");
            { var tr0 = (RectTransform)timer.transform; tr0.anchorMin = tr0.anchorMax = new Vector2(0, 1); tr0.pivot = new Vector2(0, 1); tr0.sizeDelta = new Vector2(190, 110); tr0.anchoredPosition = new Vector2(40, -26); }
            var body = Label(panel.transform, spec.Body, th.fontBody, 38, th.ivoryDim, TextAlignmentOptions.TopRight, 3);
            StretchTop(body, 40, 40, 128, 150);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(panel.transform, false);
            var vr = (RectTransform)viewport.transform; vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = new Vector2(30, footer); vr.offsetMax = new Vector2(-30, -290);
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var cor = (RectTransform)content.transform; cor.anchorMin = new Vector2(0, 1); cor.anchorMax = new Vector2(1, 1); cor.pivot = new Vector2(0.5f, 1); cor.offsetMin = cor.offsetMax = Vector2.zero;
            var v = content.GetComponent<VerticalLayoutGroup>(); v.spacing = 8; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sheet.scroll = panel.AddComponent<ScrollRect>();
            sheet.scroll.viewport = vr; sheet.scroll.content = cor; sheet.scroll.horizontal = false; sheet.scroll.vertical = true; sheet.scroll.scrollSensitivity = 40f;
            sheet.scroll.movementType = ScrollRect.MovementType.Clamped;

            foreach (var r in spec.Rows)
            {
                var row = r;
                bool on = row.Id == spec.Selected;
                var go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                go.transform.SetParent(content.transform, false);
                go.GetComponent<LayoutElement>().preferredHeight = 124;
                var bg = go.GetComponent<Image>();
                if (on) { bg.sprite = th.brassPlate; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1.4f; } else bg.color = new Color(1, 1, 1, 0.055f);
                KitTap.On(go.transform, () => spec.Picked?.Invoke(row.Id));
                var nm = Label(go.transform, KitText.Clip(row.Name, 18), th.fontBody, 48, on ? th.brassText : th.leatherText, TextAlignmentOptions.MidlineRight, 1);
                var nr = (RectTransform)nm.transform; nr.anchorMin = Vector2.zero; nr.anchorMax = Vector2.one; nr.offsetMin = new Vector2(40, 0); nr.offsetMax = new Vector2(-50, 0);
            }

            // the buttons at the foot: the main one is a brass plate, the quiet one dark
            var foot = new GameObject("Foot", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            foot.transform.SetParent(panel.transform, false);
            var fr = (RectTransform)foot.transform; fr.anchorMin = new Vector2(0, 0); fr.anchorMax = new Vector2(1, 0); fr.pivot = new Vector2(0.5f, 0); fr.sizeDelta = new Vector2(-70, footer - 40); fr.anchoredPosition = new Vector2(0, 30);
            var h = foot.GetComponent<HorizontalLayoutGroup>(); h.spacing = 24; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            h.padding = new RectOffset(0, 0, 40, 40);
            foreach (var b in spec.Buttons)
            {
                var btn = b;
                var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                go.transform.SetParent(foot.transform, false);
                go.GetComponent<LayoutElement>().flexibleWidth = btn.Quiet ? 1f : 1.6f;
                var img = go.GetComponent<Image>();
                img.sprite = btn.Quiet ? th.leatherDark : th.brassPlate; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.4f;
                img.color = btn.Disabled ? new Color(1, 1, 1, 0.4f) : Color.white;
                if (btn.Quiet) { var outline = go.AddComponent<Outline>(); outline.effectColor = new Color(th.brass.r, th.brass.g, th.brass.b, 0.5f); outline.effectDistance = new Vector2(3, -3); }
                if (!btn.Disabled) KitTap.On(go.transform, () => btn.Run?.Invoke());
                var lab = Label(go.transform, btn.Label, th.fontLalezar, 46, btn.Quiet ? th.leatherText : th.brassText, TextAlignmentOptions.Midline, 1);
                var lr = (RectTransform)lab.transform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(20, 0); lr.offsetMax = new Vector2(-20, 0);
            }

            Canvas.ForceUpdateCanvases();
            sheet.scroll.verticalNormalizedPosition = keep;
        }

        /// <summary>Changes only the small timer line of the open sheet (no rebuild).</summary>
        public static void SetTimer(string text)
        {
            if (open == null) return;
            var t = open.transform.Find("Panel/Timer");
            var a = t != null ? t.GetComponent<ArabicText>() : null;
            if (a != null && a.Text != text) a.Text = text;
        }

        private static ArabicText Label(Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align, int maxLines, string name = "Label")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
            t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = size * 0.55f;      // a long name or line shrinks instead of breaking the look
            t.overflowMode = TextOverflowModes.Ellipsis; t.maxVisibleLines = maxLines;
            var a = go.AddComponent<ArabicText>(); a.ArabicDigits = true; a.Text = text;
            return a;
        }

        /// <summary>Stretches a label across the top of the panel with the given side margins, distance from the top and height.</summary>
        private static void StretchTop(ArabicText a, float left, float right, float top, float height)
        {
            var r = (RectTransform)a.transform;
            r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1);
            r.sizeDelta = new Vector2(-(left + right), height); r.anchoredPosition = new Vector2((left - right) / 2f, -top);
        }

        private void OnDestroy() { if (open == this) open = null; }
    }
}
