using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>Gentle up-and-down float (HTML "flo": translateY -5px at mid cycle, 3.5s).</summary>
    public sealed class KitBob : MonoBehaviour
    {
        [SerializeField] private float amplitude = 14f;
        [SerializeField] private float period = 3.5f;
        private RectTransform rt;
        private float baseY, last = float.NaN;

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (rt == null) rt = (RectTransform)transform;
            var p = rt.anchoredPosition;
            if (float.IsNaN(last) || !Mathf.Approximately(p.y, last)) baseY = p.y;
            float k = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime / period * Mathf.PI * 2f);
            p.y = baseY + amplitude * k; rt.anchoredPosition = p; last = p.y;
        }

        private void OnDisable() { if (rt != null && !float.IsNaN(last)) { var p = rt.anchoredPosition; p.y = baseY; rt.anchoredPosition = p; last = float.NaN; } }
    }
}
