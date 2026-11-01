using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Keeps a board that was drawn for the tall 1080x2400 phone screen whole on any other shape (a tablet, a foldable, a small desktop window). The board
    /// is laid out with fixed margins from the screen edges, so on a short screen it would be squeezed until nothing fits. When the screen is shorter than
    /// the design, the board keeps its design size, sits in the middle and is scaled down uniformly to fit with a margin; on a tall screen nothing changes.
    /// Animations multiply their own scale by <see cref="Scale"/>.
    /// </summary>
    public sealed class KitScreenFit : MonoBehaviour
    {
        private static readonly Vector2 Reference = new Vector2(1080f, 2400f);

        private RectTransform rt, parent;
        private Vector2 aMin, aMax, oMin, oMax, pivot, design, designCenter;
        private bool fitting, ready;
        private Vector2 lastSize;

        /// <summary>1 on a tall screen; below 1 when the board had to shrink.</summary>
        public float Scale { get; private set; } = 1f;

        private void Awake()
        {
            rt = (RectTransform)transform; parent = rt.parent as RectTransform;
            aMin = rt.anchorMin; aMax = rt.anchorMax; oMin = rt.offsetMin; oMax = rt.offsetMax; pivot = rt.pivot;
            design = new Vector2(Reference.x * (aMax.x - aMin.x) + oMax.x - oMin.x, Reference.y * (aMax.y - aMin.y) + oMax.y - oMin.y);
            var lo = new Vector2(Reference.x * aMin.x + oMin.x, Reference.y * aMin.y + oMin.y); var hi = new Vector2(Reference.x * aMax.x + oMax.x, Reference.y * aMax.y + oMax.y);
            designCenter = (lo + hi) * 0.5f - Reference * 0.5f;        // where the board's middle is, relative to the middle of the screen
            ready = parent != null && design.x > 1f && design.y > 1f;
        }

        /// <summary>Applies the fit now (the board needs it before its first frame).</summary>
        public void Refresh() { lastSize = Vector2.zero; LateUpdate(); }

        private void LateUpdate()
        {
            if (!ready) return;
            var size = parent.rect.size;
            if (size == lastSize) return;
            lastSize = size;
            float stretchedH = size.y * (aMax.y - aMin.y) + oMax.y - oMin.y;
            if (stretchedH >= design.y * 0.92f)
            {
                if (fitting) { fitting = false; Scale = 1f; rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax; rt.localScale = Vector3.one; }
                return;
            }
            fitting = true;
            Scale = Mathf.Clamp(Mathf.Min((size.y - 100f) / design.y, (size.x - 20f) / design.x, 1f), 0.2f, 1f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = design; rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one * Scale;
        }
    }
}
