using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The banner that slides in from the top when a friend invites the player to a room: who, which room, "join" and "later", and a thin
    /// bar that runs out. It builds itself from the kit theme, so it can appear over any screen. <c>KitInviteBanner.Show(canvas, ...)</c>.
    /// </summary>
    public sealed class KitInviteBanner : MonoBehaviour
    {
        private static KitInviteBanner open;

        private RectTransform panel;
        private RectTransform bar;
        private float seconds, age;
        private Action later;

        public static void Close() { if (open != null) Destroy(open.gameObject); open = null; }

        public static void Show(Transform canvasRoot, string title, string sub, Action join, Action later, float seconds = 14f)
        {
            Close();
            var th = KitTheme.Current;
            if (canvasRoot == null || th == null) return;
            var root = new GameObject("InviteBanner", typeof(RectTransform));
            root.transform.SetParent(canvasRoot, false);
            root.transform.SetAsLastSibling();
            var rt = (RectTransform)root.transform; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0, 260);
            var b = root.AddComponent<KitInviteBanner>();
            open = b; b.seconds = seconds; b.later = later;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var pr = (RectTransform)panel.transform; pr.anchorMin = new Vector2(0, 1); pr.anchorMax = new Vector2(1, 1); pr.pivot = new Vector2(0.5f, 1);
            pr.offsetMin = new Vector2(28, -250); pr.offsetMax = new Vector2(-28, -70);
            var pi = panel.GetComponent<Image>(); pi.sprite = th.leatherDark; pi.type = Image.Type.Sliced; pi.pixelsPerUnitMultiplier = 1.4f;
            b.panel = pr;

            var font = th.fontLalezar != null ? th.fontLalezar : th.fontHeading;
            Text(panel.transform, title, font, 48, th.leatherText, new Vector2(1, 1), new Vector2(-40, -26), new Vector2(560, 64), TextAlignmentOptions.MidlineRight);
            Text(panel.transform, sub, th.fontBody, 30, th.ivoryDim, new Vector2(1, 1), new Vector2(-40, -92), new Vector2(560, 48), TextAlignmentOptions.MidlineRight);
            Button(panel.transform, "انضم", font, th.brassPlate, th.brassText, new Vector2(0, 0.5f), new Vector2(36, 6), new Vector2(200, 100), () => { Close(); join?.Invoke(); });
            Button(panel.transform, "لاحقاً", font, th.leatherDark, th.ivoryDim, new Vector2(0, 0.5f), new Vector2(256, 6), new Vector2(200, 100), () => { Close(); later?.Invoke(); });

            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(panel.transform, false);
            var br = (RectTransform)bar.transform; br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0); br.pivot = new Vector2(1, 0); br.offsetMin = new Vector2(36, 18); br.offsetMax = new Vector2(-36, 26);
            bar.GetComponent<Image>().color = th.brass;
            b.bar = br;
            b.StartCoroutine(b.SlideIn());
        }

        private static void Text(Transform parent, string text, TMP_FontAsset font, float size, Color color, Vector2 anchor, Vector2 pos, Vector2 box, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(1, 1); r.anchoredPosition = pos; r.sizeDelta = box;
            var t = go.GetComponent<TextMeshProUGUI>(); if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
            var a = go.AddComponent<ArabicText>(); a.ArabicDigits = false; a.Text = text;
        }

        private static void Button(Transform parent, string label, TMP_FontAsset font, Sprite sprite, Color text, Vector2 anchor, Vector2 pos, Vector2 size, Action click)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(0, 0.5f); r.anchoredPosition = pos; r.sizeDelta = size;
            var img = go.GetComponent<Image>(); img.sprite = sprite; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.4f;
            var btn = go.GetComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(() => click?.Invoke());
            var lab = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lab.transform.SetParent(go.transform, false);
            var lr = (RectTransform)lab.transform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
            var t = lab.GetComponent<TextMeshProUGUI>(); if (font != null) t.font = font;
            t.fontSize = 44; t.color = text; t.alignment = TextAlignmentOptions.Midline; t.raycastTarget = false;
            var a = lab.AddComponent<ArabicText>(); a.ArabicDigits = false; a.Text = label;
        }

        private IEnumerator SlideIn()
        {
            var rt = (RectTransform)transform;
            float t = 0f;
            while (t < 0.25f) { t += Time.unscaledDeltaTime; rt.anchoredPosition = new Vector2(0, Mathf.Lerp(300f, 0f, t / 0.25f)); yield return null; }
            rt.anchoredPosition = Vector2.zero;
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            if (bar != null && seconds > 0f) bar.anchorMax = new Vector2(Mathf.Clamp01(1f - age / seconds), 0);
            if (age >= seconds) { var l = later; Close(); l?.Invoke(); }
        }

        private void OnDestroy() { if (open == this) open = null; }
    }
}
