using System.Collections;
using MafiaNight.Data.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// A player's role card, big: opened by pressing the little card beside his seat (Docs/mockups/role-cards.html, section 2). The card flies out of the little one to the middle of the screen,
    /// growing while it turns face up; a press on it turns it over to what the role does, and a press anywhere else sends it back into its place. Built from the kit theme when shown:
    /// <c>KitCardViewer.Show(canvas, def, layers, "owner's name", littleCard)</c> (without the little card it just appears).
    /// </summary>
    public sealed class KitCardViewer : MonoBehaviour
    {
        private const float CardWidth = 560f, Rise = 0.02f;
        /// <summary>Seconds per step; the editor preview sets it to step the flight by hand (edit mode has no frames).</summary>
        public static float StepOverride;
        private static float Dt => StepOverride > 0f ? StepOverride : Time.unscaledDeltaTime;
        private static bool Plays => Application.isPlaying || StepOverride > 0f;

        private static KitCardViewer open;
        public static bool IsOpen => open != null;
        public static void CloseCurrent() { if (open != null) Destroy(open.gameObject); open = null; }

        private RectTransform holder, from;
        private KitRoleCard card;
        private Image dim;
        private CanvasGroup texts;
        private Vector2 last;
        private bool closing;
        private float finalScale = 1f;

        public static void Show(Transform canvasRoot, RoleCardDef def, CardLayers layers, string owner, RectTransform from = null)
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
            open = viewer; viewer.dim = bg; viewer.from = from;
            KitTap.On(root.transform, viewer.Close);

            // the card sits in a holder that is moved and scaled; the card's own turn only squeezes the card inside it
            var h = new GameObject("CardHolder", typeof(RectTransform));
            h.transform.SetParent(root.transform, false);
            viewer.holder = (RectTransform)h.transform;
            viewer.holder.anchorMin = viewer.holder.anchorMax = new Vector2(0.5f, 0.52f); viewer.holder.pivot = new Vector2(0.5f, 0.5f);
            viewer.holder.sizeDelta = new Vector2(CardWidth, CardWidth * KitRoleCard.RefH / KitRoleCard.RefW); viewer.holder.anchoredPosition = Vector2.zero;
            bool fly = from != null && Plays;
            viewer.card = KitRoleCard.Create(h.transform, def, layers, CardWidth, fly ? KitRoleCard.Face.Hidden : KitRoleCard.Face.Front);
            var cr = (RectTransform)viewer.card.transform; cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f); cr.pivot = new Vector2(0.5f, 0.5f); cr.anchoredPosition = Vector2.zero;
            viewer.card.Pressed += k => k.SetFace(k.Current == KitRoleCard.Face.Front ? KitRoleCard.Face.Back : KitRoleCard.Face.Front, true);

            var tg = new GameObject("Texts", typeof(RectTransform), typeof(CanvasGroup));
            tg.transform.SetParent(root.transform, false);
            var tgr = (RectTransform)tg.transform; tgr.anchorMin = Vector2.zero; tgr.anchorMax = Vector2.one; tgr.offsetMin = tgr.offsetMax = Vector2.zero;
            viewer.texts = tg.GetComponent<CanvasGroup>(); viewer.texts.blocksRaycasts = false; viewer.texts.alpha = fly ? 0f : 1f;       // the words come in with the card
            var who = Label(tg.transform, string.IsNullOrEmpty(owner) ? def.Name : owner + " · " + def.Name, th.fontLalezar, 58, th.brassLight);
            var wr = (RectTransform)who.transform; wr.anchorMin = new Vector2(0, 1); wr.anchorMax = new Vector2(1, 1); wr.pivot = new Vector2(0.5f, 1); wr.sizeDelta = new Vector2(-100, 120); wr.anchoredPosition = new Vector2(0, -150);
            var hint = Label(tg.transform, "اضغط الكرت لتقلبه · اضغط برّا لتغلق", th.fontBody, 32, th.ivoryDim);
            var hr = (RectTransform)hint.transform; hr.anchorMin = new Vector2(0, 0); hr.anchorMax = new Vector2(1, 0); hr.pivot = new Vector2(0.5f, 0); hr.sizeDelta = new Vector2(-100, 90); hr.anchoredPosition = new Vector2(0, 150);
            Canvas.ForceUpdateCanvases(); viewer.Fit();
            if (fly && Application.isPlaying) viewer.StartCoroutine(viewer.Fly(true));
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

        private void LateUpdate() { if (closing) return; var r = ((RectTransform)transform).rect.size; if (r != last) Fit(); }

        /// <summary>Keeps the card inside the free middle of the screen: smaller on a short or narrow one, never bigger than its own size.</summary>
        private void Fit()
        {
            var r = ((RectTransform)transform).rect.size; last = r;
            if (holder == null || r.y < 300f || r.x < 300f) return;
            float h = CardWidth * KitRoleCard.RefH / KitRoleCard.RefW;
            finalScale = Mathf.Clamp(Mathf.Min((r.x - 100f) / CardWidth, (r.y - 560f) / h), 0.3f, 1f);
            holder.localScale = Vector3.one * finalScale;
        }

        // ------------------------------------------------------------------ the card flies out of its place and back

        /// <summary>Where the little card is, as a position and a scale of the holder.</summary>
        private bool Origin(out Vector2 pos, out float scale)
        {
            pos = Vector2.zero; scale = finalScale;
            if (from == null) return false;
            var rootRt = (RectTransform)transform;
            Vector2 local = rootRt.InverseTransformPoint(from.position);
            pos = local - new Vector2(0f, Rise * rootRt.rect.height);        // the holder is anchored a little above the middle
            float world = from.rect.width * from.lossyScale.x, mine = CardWidth * rootRt.lossyScale.x;
            scale = mine > 0.01f ? world / mine : 0.15f;
            return true;
        }

        public IEnumerator Fly(bool opening)
        {
            yield return null;                                   // the canvas settles and Fit has run
            Fit();
            if (!Origin(out var near, out var small)) { if (!opening) Destroy(gameObject); yield break; }
            Vector2 far = Vector2.zero; float big = finalScale;
            float dur = opening ? 0.45f : 0.3f; bool turned = false;
            Color bgColor = new Color32(0x0A, 0x07, 0x05, 238);
            for (float t = 0; t < dur; t += Dt)
            {
                float k = Mathf.Clamp01(t / dur), e = 1f - Mathf.Pow(1f - k, 3f);
                float f = opening ? e : 1f - e;                  // 0 = in its place, 1 = in the middle
                if (from != null && Origin(out near, out small)) { }
                holder.anchoredPosition = Vector2.Lerp(near, far, f); holder.localScale = Vector3.one * Mathf.Lerp(small, big, f);
                dim.color = new Color(bgColor.r, bgColor.g, bgColor.b, bgColor.a * Mathf.Clamp01(f * 1.4f));
                if (texts != null) texts.alpha = Mathf.Clamp01((f - 0.4f) * 2f);
                if (opening && !turned && k > 0.2f) { card.SetFace(KitRoleCard.Face.Front, Application.isPlaying); turned = true; }      // turns face up on the way
                yield return null;
            }
            holder.anchoredPosition = opening ? far : near; holder.localScale = Vector3.one * (opening ? big : small);
            if (opening) { dim.color = bgColor; if (texts != null) texts.alpha = 1f; if (!turned) card.SetFace(KitRoleCard.Face.Front, Application.isPlaying); }
            else Destroy(gameObject);
        }

        /// <summary>Sends the card back into its place (or just closes when it has no place to go).</summary>
        public void Close()
        {
            if (closing) return;
            closing = true;
            if (from != null && Application.isPlaying && gameObject.activeInHierarchy) { StopAllCoroutines(); StartCoroutine(Back()); }
            else Destroy(gameObject);
        }

        private IEnumerator Back()
        {
            card.SetFace(KitRoleCard.Face.Hidden, false);
            yield return Fly(false);
        }

        private void OnDestroy() { if (open == this) open = null; }
    }
}
