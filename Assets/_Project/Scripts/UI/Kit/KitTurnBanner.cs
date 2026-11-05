using System.Collections;
using MafiaNight.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The banner in the middle of the table at night (Docs/mockups/match-turns.html): whose turn it is, for everybody to see. Gold when it is the viewer's own turn ("المافيا · دورك · اختر الآن"),
    /// dark when it is somebody else's ("الطبيب · دوره · يختار الآن"), blue-grey while everybody sleeps. Nobody can tell from it what anyone chooses. It takes no tap, pops in with a little
    /// overshoot (a device that saves its motion just shows it) and follows the middle of the table.
    /// </summary>
    public sealed class KitTurnBanner : MonoBehaviour
    {
        public enum Look { Mine, Other, Sleepy }

        private const float MinWidth = 640f, MaxWidth = 960f;
        private RectTransform rt, body;
        private CanvasGroup group;
        private ArabicText title, sub;
        private RectTransform follow;
        private Coroutine pop;
        private string shownKey;

        /// <summary>The banner follows the middle of this rect (the table's stage).</summary>
        public RectTransform Follow { set { follow = value; } }
        public bool Visible => group != null && group.alpha > 0.01f;
        public string Title => title != null ? title.Text : "";

        public static KitTurnBanner Create(RectTransform parent)
        {
            var th = KitTheme.Current;
            var go = new GameObject("TurnBanner", typeof(RectTransform), typeof(CanvasGroup));
            var r = (RectTransform)go.transform; r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(MinWidth, 240f);
            var b = go.AddComponent<KitTurnBanner>();
            b.rt = r; b.group = go.GetComponent<CanvasGroup>(); b.group.alpha = 0f; b.group.blocksRaycasts = false; b.group.interactable = false;      // it never takes a tap

            var body = new GameObject("Body", typeof(RectTransform), typeof(Image));
            body.transform.SetParent(go.transform, false);
            b.body = (RectTransform)body.transform; b.body.anchorMin = Vector2.zero; b.body.anchorMax = Vector2.one; b.body.offsetMin = b.body.offsetMax = Vector2.zero;
            var bi = body.GetComponent<Image>(); bi.raycastTarget = false; bi.type = Image.Type.Sliced;

            b.title = Text(go.transform, "Title", th.fontLalezar != null ? th.fontLalezar : th.fontBody, 92, 0.38f, 1f);
            b.sub = Text(go.transform, "Sub", th.fontBody, 38, 0f, 0.38f);
            b.sub.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            go.SetActive(false);
            return b;
        }

        private static ArabicText Text(Transform parent, string name, TMP_FontAsset font, float size, float yMin, float yMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = new Vector2(0, yMin); r.anchorMax = new Vector2(1, yMax); r.offsetMin = new Vector2(40, 6); r.offsetMax = new Vector2(-40, -6);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.alignment = TextAlignmentOptions.Midline; t.raycastTarget = false;
            t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = size * 0.5f; t.overflowMode = TextOverflowModes.Ellipsis; t.maxVisibleLines = 1;      // any role name or line keeps the banner's shape
            return go.AddComponent<ArabicText>();
        }

        private static Sprite Paper(Look look)
        {
            switch (look)
            {
                case Look.Mine: return KitProc.Paint("turnbanner.mine", new KitProc.Spec { Radius = 14, Top = new Color32(0xF5, 0xE2, 0xA8, 255), Mid = new Color32(0xC9, 0xA0, 0x50, 255), Bottom = new Color32(0x8A, 0x6A, 0x30, 255), MidAt = 0.6f, Edge = new Color32(0x4A, 0x35, 0x12, 255), EdgePx = 3, Shadow = Color.black, ShadowDy = 6, ShadowBlur = 1.4f, Sliced = true });
                case Look.Other: return KitProc.Paint("turnbanner.other", new KitProc.Spec { Radius = 14, Top = new Color32(0x33, 0x25, 0x1A, 255), Bottom = new Color32(0x1A, 0x11, 0x0B, 255), Edge = new Color32(0x6A, 0x55, 0x3A, 255), EdgePx = 3, Shadow = Color.black, ShadowDy = 5, ShadowBlur = 1.4f, Sliced = true });
                default: return KitProc.Paint("turnbanner.sleepy", new KitProc.Spec { Radius = 14, Top = new Color32(0x1C, 0x20, 0x38, 255), Bottom = new Color32(0x0E, 0x10, 0x22, 255), Edge = new Color32(0x3A, 0x40, 0x68, 255), EdgePx = 3, Shadow = Color.black, ShadowDy = 5, ShadowBlur = 1.4f, Sliced = true });
            }
        }

        /// <summary>Shows the banner (the same words again are not replayed, only refreshed).</summary>
        public void Show(Look look, string line, string small)
        {
            var th = KitTheme.Current;
            string key = look + "|" + line + "|" + small;
            body.GetComponent<Image>().sprite = Paper(look);
            title.Text = line; sub.Text = small;
            var tt = title.GetComponent<TextMeshProUGUI>(); var st = sub.GetComponent<TextMeshProUGUI>();
            tt.color = look == Look.Mine ? th.brassText : look == Look.Other ? th.brassLight : th.leatherText;
            st.color = look == Look.Mine ? th.brassText : th.leatherText;
            transform.SetAsLastSibling();
            LateUpdate();
            if (key == shownKey && Visible) return;
            shownKey = key;
            gameObject.SetActive(true);
            if (pop != null) StopCoroutine(pop);
            if (Application.isPlaying && DevicePerf.DecorativeMotion) pop = StartCoroutine(Pop()); else { group.alpha = 1f; rt.localScale = Vector3.one; }
        }

        public void Hide()
        {
            shownKey = null;
            if (pop != null) StopCoroutine(pop);
            pop = null;
            if (group != null) group.alpha = 0f;
            gameObject.SetActive(false);
        }

        private IEnumerator Pop()
        {
            const float dur = 0.45f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / dur);
                float back = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);      // easeOutBack: lands with a small overshoot
                rt.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, back);
                group.alpha = Mathf.Clamp01(k * 3f);
                yield return null;
            }
            rt.localScale = Vector3.one; group.alpha = 1f; pop = null;
        }

        private void LateUpdate()
        {
            if (rt == null) return;
            float w = Mathf.Clamp(Mathf.Max(MinWidth, title.GetComponent<TextMeshProUGUI>().preferredWidth + 160f), MinWidth, MaxWidth);
            rt.sizeDelta = new Vector2(w, 240f);
            if (follow == null) return;
            var world = follow.TransformPoint(follow.rect.center);
            var parent = (RectTransform)rt.parent;
            rt.anchoredPosition = (Vector2)parent.InverseTransformPoint(world) - parent.rect.center;
        }
    }
}
