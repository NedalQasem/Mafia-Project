using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The list of everybody in a room (a tap on the member count of <see cref="KitRoomScreen"/>): a sheet from the bottom with a scrolling list, one row per
    /// member with his tags and state. It builds itself from the kit theme: <c>KitMembersSheet.Show(canvas, "title", rows, tapped)</c>.
    /// </summary>
    public sealed class KitMembersSheet : MonoBehaviour
    {
        public sealed class Row
        {
            public string Id = "", Name = "", Tag = "", State = "";
            public bool Me, Staff, IsNew;
        }

        private static KitMembersSheet open;
        public static bool IsOpen => open != null;
        public static void CloseCurrent() { if (open != null) Destroy(open.gameObject); open = null; }

        public static void Show(Transform canvasRoot, string title, string count, IList<Row> rows, Action<Row> tapped)
        {
            CloseCurrent();
            var th = KitTheme.Current;
            if (canvasRoot == null || th == null) return;
            var root = new GameObject("MembersSheet", typeof(RectTransform));
            root.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)root.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            root.transform.SetAsLastSibling();
            open = root.AddComponent<KitMembersSheet>();

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
            dim.transform.SetParent(root.transform, false);
            var dr = (RectTransform)dim.transform; dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = dr.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0, 0, 0, 0.62f);
            dim.GetComponent<Button>().onClick.AddListener(CloseCurrent);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var pr = (RectTransform)panel.transform; pr.anchorMin = new Vector2(0, 0); pr.anchorMax = new Vector2(1, 0.72f); pr.offsetMin = pr.offsetMax = Vector2.zero;
            var pi = panel.GetComponent<Image>(); pi.sprite = th.leatherDark; pi.type = Image.Type.Sliced; pi.pixelsPerUnitMultiplier = 1.4f;

            var head = Label(panel.transform, title, th.fontLalezar, 62, th.leatherText, TextAlignmentOptions.MidlineRight);
            var hr = (RectTransform)head.transform; hr.anchorMin = new Vector2(0, 1); hr.anchorMax = new Vector2(1, 1); hr.pivot = new Vector2(0.5f, 1); hr.sizeDelta = new Vector2(-100, 120); hr.anchoredPosition = new Vector2(0, -34);
            var cnt = Label(panel.transform, count, th.fontBody, 42, th.brass, TextAlignmentOptions.MidlineLeft);
            var cr = (RectTransform)cnt.transform; cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1); cr.pivot = new Vector2(0.5f, 1); cr.sizeDelta = new Vector2(-100, 120); cr.anchoredPosition = new Vector2(0, -34);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(panel.transform, false);
            var vr = (RectTransform)viewport.transform; vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = new Vector2(30, 60); vr.offsetMax = new Vector2(-30, -170);
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var cor = (RectTransform)content.transform; cor.anchorMin = new Vector2(0, 1); cor.anchorMax = new Vector2(1, 1); cor.pivot = new Vector2(0.5f, 1); cor.offsetMin = cor.offsetMax = Vector2.zero;
            var v = content.GetComponent<VerticalLayoutGroup>(); v.spacing = 6; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = panel.AddComponent<ScrollRect>(); scroll.viewport = vr; scroll.content = cor; scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 40f;

            foreach (var r in rows)
            {
                var row = r;
                var go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                go.transform.SetParent(content.transform, false);
                go.GetComponent<LayoutElement>().preferredHeight = 136;
                var bg = go.GetComponent<Image>(); bg.color = new Color(1, 1, 1, 0.045f);
                KitTap.On(go.transform, () => tapped?.Invoke(row));

                var disc = Img(go.transform, th.dotWhite, row.Staff ? th.brass : row.Me ? new Color32(0xE0, 0x52, 0x5A, 255) : new Color32(0x4A, 0x3C, 0x28, 255), 96); Anchor(disc, new Vector2(1, 0.5f), new Vector2(-70, 0));
                var inner = Img(disc, th.dotWhite, new Color32(0x1A, 0x15, 0x12, 255), 88); Anchor(inner, new Vector2(0.5f, 0.5f), Vector2.zero);
                var sil = Img(inner, th.fedSil, row.Staff ? new Color32(0xD9, 0xC4, 0x8A, 255) : new Color32(0x9A, 0x8E, 0x78, 255), 62); Anchor(sil, new Vector2(0.5f, 0.5f), Vector2.zero);

                var nm = Label(go.transform, KitText.Clip(row.Name, 16), th.fontBody, 46, row.Staff ? th.brassLight : th.leatherText, TextAlignmentOptions.MidlineRight);
                var nr = (RectTransform)nm.transform; nr.anchorMin = new Vector2(0, 0); nr.anchorMax = new Vector2(1, 1); nr.offsetMin = new Vector2(260, 0); nr.offsetMax = new Vector2(-150, 0);
                string tag = row.Me ? "أنت" : row.Tag;
                if (!string.IsNullOrEmpty(tag) || row.IsNew)
                {
                    var ct = Label(go.transform, string.IsNullOrEmpty(tag) ? "جديد" : tag, th.fontLalezar, 32, row.IsNew && !row.Staff ? th.teal : th.brassLight, TextAlignmentOptions.MidlineRight);
                    var ctr = (RectTransform)ct.transform; ctr.anchorMin = new Vector2(0, 0); ctr.anchorMax = new Vector2(0, 1); ctr.pivot = new Vector2(0, 0.5f); ctr.sizeDelta = new Vector2(220, 0); ctr.anchoredPosition = new Vector2(250, 36);
                }
                var st = Label(go.transform, row.State, th.fontBody, 36, th.ivoryDim, TextAlignmentOptions.MidlineLeft);
                var sr = (RectTransform)st.transform; sr.anchorMin = new Vector2(0, 0); sr.anchorMax = new Vector2(0, 1); sr.pivot = new Vector2(0, 0.5f); sr.sizeDelta = new Vector2(300, 0); sr.anchoredPosition = new Vector2(24, 0);
            }
        }

        private static RectTransform Img(Transform parent, Sprite sprite, Color color, float size)
        {
            var go = new GameObject("Img", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = sprite; i.color = color; i.raycastTarget = false;
            var r = (RectTransform)go.transform; r.sizeDelta = new Vector2(size, size);
            return r;
        }

        private static void Anchor(RectTransform r, Vector2 anchor, Vector2 pos) { r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; }

        private static ArabicText Label(Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false; t.overflowMode = TextOverflowModes.Overflow;
            var a = go.AddComponent<ArabicText>(); a.ArabicDigits = true; a.Text = text;
            return a;
        }

        private void OnDestroy() { if (open == this) open = null; }
    }
}
