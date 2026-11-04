using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// For a popup whose parts were all placed against the edges of the tall 1080x2400 phone screen (the offers banner: the sheet, its close button and the
    /// strip below it): the parts are moved into one group that behaves like the tall screen. On a tall screen the group simply fills it; on a shorter one
    /// (tablet, small window) it keeps the 1080x2400 design size and is scaled down uniformly, so the parts keep their places and never overlap.
    /// </summary>
    public sealed class KitFitGroup : MonoBehaviour
    {
        private Vector2 design = new Vector2(1080f, 2400f);
        private RectTransform rt, parent;
        private Vector2 lastSize;
        private float reserve;

        /// <summary>Keeps the bottom <paramref name="units"/> of the screen free (a dock under the group): the group fits in what is left, from the top down.</summary>
        public void SetReserve(float units) { if (Mathf.Approximately(reserve, units)) return; reserve = units; lastSize = Vector2.zero; }

        /// <summary>Puts <paramref name="parts"/> (children of <paramref name="root"/>) into a new fitting group on top of the other children.</summary>
        public static KitFitGroup Wrap(RectTransform root, params RectTransform[] parts) => Wrap(root, 2400f, parts);

        /// <summary>Same, for a screen drawn for a design height other than 2400 (the match frames use the height of the HTML phone, 2188).</summary>
        public static KitFitGroup Wrap(RectTransform root, float designHeight, params RectTransform[] parts)
        {
            var go = new GameObject("FitGroup", typeof(RectTransform)); go.layer = root.gameObject.layer;
            var r = (RectTransform)go.transform; r.SetParent(root, false);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            foreach (var p in parts) if (p != null) p.SetParent(r, true);
            var g = go.AddComponent<KitFitGroup>(); g.design = new Vector2(1080f, designHeight);
            return g;
        }

        private void Awake() { rt = (RectTransform)transform; parent = rt.parent as RectTransform; }

        private void LateUpdate() { Apply(); }

        /// <summary>Fits the group to its parent now (every frame at runtime through LateUpdate; the editor previews call it themselves).</summary>
        public void Apply()
        {
            if (rt == null) { rt = (RectTransform)transform; parent = rt.parent as RectTransform; }
            if (parent == null) return;
            var size = parent.rect.size;
            if (size == lastSize) return;
            lastSize = size;
            float avail = size.y - reserve;
            if (avail >= design.y - 1f)
            {
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(0, reserve); rt.offsetMax = Vector2.zero; rt.localScale = Vector3.one;
                return;
            }
            float k = Mathf.Clamp(Mathf.Min(avail / design.y, size.x / design.x, 1f), 0.2f, 1f);
            if (reserve > 0f)
            {   // from the top: the dock sits under the group
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = design; rt.anchoredPosition = Vector2.zero; rt.localScale = Vector3.one * k;
                return;
            }
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = design; rt.anchoredPosition = Vector2.zero; rt.localScale = Vector3.one * k;
        }
    }
}
