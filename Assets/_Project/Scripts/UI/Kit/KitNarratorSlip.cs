using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The narrator's line as a telegram slip: a strip of paper with a wax seal that comes down from the top of the table, stays for a few seconds and goes. It is what the cue
    /// shows when there is no voice-over (or the player plays without sound), so the narration never depends on an audio file. Built in code from the kit theme.
    /// </summary>
    public sealed class KitNarratorSlip : MonoBehaviour
    {
        private static readonly Vector2 Home = new Vector2(0, -250);

        private CanvasGroup group;
        private ArabicText text;
        private RectTransform rt;
        private Coroutine run;

        public static KitNarratorSlip Create(RectTransform parent)
        {
            var th = KitTheme.Current;
            var go = new GameObject("NarratorSlip", typeof(RectTransform), typeof(CanvasGroup));
            var r = (RectTransform)go.transform; r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 1f); r.sizeDelta = new Vector2(940, 170); r.anchoredPosition = Home;
            var slip = go.AddComponent<KitNarratorSlip>();
            slip.rt = r; slip.group = go.GetComponent<CanvasGroup>(); slip.group.alpha = 0f; slip.group.blocksRaycasts = false; slip.group.interactable = false;

            var paper = new GameObject("Paper", typeof(RectTransform), typeof(Image));
            paper.transform.SetParent(go.transform, false);
            var pr = (RectTransform)paper.transform; pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero; pr.localRotation = Quaternion.Euler(0, 0, -0.8f);
            var pi = paper.GetComponent<Image>(); pi.raycastTarget = false; pi.type = Image.Type.Sliced;
            pi.sprite = KitProc.Paint("slippaper", new KitProc.Spec { Radius = 6, Top = new Color32(0xE9, 0xDC, 0xB8, 255), Bottom = new Color32(0xD9, 0xC8, 0x9C, 255), Edge = new Color32(0x8A, 0x74, 0x48, 255), EdgePx = 3, Sliced = true });

            var seal = new GameObject("Seal", typeof(RectTransform), typeof(Image));
            seal.transform.SetParent(go.transform, false);
            var sr = (RectTransform)seal.transform; sr.anchorMin = sr.anchorMax = new Vector2(1, 0.5f); sr.sizeDelta = new Vector2(96, 96); sr.anchoredPosition = new Vector2(-26, 0);
            var si = seal.GetComponent<Image>(); si.raycastTarget = false;
            si.sprite = KitProc.Paint("slipseal", new KitProc.Spec { W = 88, H = 88, Radius = 44, Margin = 4, Top = new Color32(0xA2, 0x26, 0x2F, 255), Mid = new Color32(0x8A, 0x1C, 0x24, 255), Bottom = new Color32(0x56, 0x10, 0x16, 255), MidAt = 0.55f, Edge = new Color32(0x3A, 0x08, 0x0C, 255), EdgePx = 3, Shadow = Color.black, ShadowDy = 3, ShadowBlur = 1.2f });

            var t = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            t.transform.SetParent(go.transform, false);
            var tr = (RectTransform)t.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(40, 14); tr.offsetMax = new Vector2(-150, -14);
            var tm = t.GetComponent<TextMeshProUGUI>();
            tm.font = th.fontBody; tm.fontSize = 46; tm.color = th.paperInk; tm.alignment = TextAlignmentOptions.MidlineRight; tm.raycastTarget = false; tm.fontStyle = FontStyles.Bold;
            tm.enableAutoSizing = true; tm.fontSizeMax = 46; tm.fontSizeMin = 28; tm.overflowMode = TextOverflowModes.Ellipsis; tm.maxVisibleLines = 3;      // any length keeps the slip's shape
            slip.text = t.AddComponent<ArabicText>();
            go.SetActive(false);
            return slip;
        }

        /// <summary>Shows a line for about <paramref name="seconds"/>; a line already on show is replaced.</summary>
        public void Show(string line, float seconds)
        {
            if (string.IsNullOrEmpty(line)) return;
            text.Text = line;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            if (run != null) StopCoroutine(run);
            run = StartCoroutine(Play(Mathf.Clamp(seconds, 2.5f, 7f)));
        }

        public void Hide() { if (run != null) StopCoroutine(run); run = null; group.alpha = 0f; gameObject.SetActive(false); }

        private IEnumerator Play(float seconds)
        {
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime) { float k = t / 0.35f; group.alpha = k; rt.anchoredPosition = Home + new Vector2(0, 60 * (1 - k)); yield return null; }
            group.alpha = 1f; rt.anchoredPosition = Home;
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / 0.4f; yield return null; }
            group.alpha = 0f; gameObject.SetActive(false); run = null;
        }
    }
}
