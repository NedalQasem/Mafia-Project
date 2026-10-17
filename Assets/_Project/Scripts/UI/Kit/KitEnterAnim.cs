using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// One-shot "slide in and fade in" for an element that sits inside a layout group (HTML: opacity 0 + translateX → rest).
    /// The layout owns the real position, so this adds an animated x offset on top of it each frame
    /// and re-reads the base position whenever the layout moves the element.
    /// </summary>
    public sealed class KitEnterAnim : MonoBehaviour
    {
        private RectTransform rt;
        private CanvasGroup cg;
        private float delay, duration, fade, fromX, t, baseX, lastSet = float.NaN;
        private bool running;

        public static void Play(GameObject go, float delay, float fromX, float duration = 0.4f, float fade = 0.35f)
        {
            if (!Application.isPlaying || go == null || !go.activeInHierarchy) return;
            var a = go.GetComponent<KitEnterAnim>();
            if (a == null) a = go.AddComponent<KitEnterAnim>();
            a.Begin(delay, fromX, duration, fade);
        }

        private void Begin(float delay, float fromX, float duration, float fade)
        {
            rt = (RectTransform)transform;
            cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            this.delay = delay; this.fromX = fromX; this.duration = duration; this.fade = fade;
            t = 0f; lastSet = float.NaN; running = true;
            cg.alpha = 0f;
        }

        private void LateUpdate()
        {
            if (!running) return;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01((t - delay) / duration);
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            cg.alpha = Mathf.Clamp01((t - delay) / fade);

            var p = rt.anchoredPosition;
            if (float.IsNaN(lastSet) || !Mathf.Approximately(p.x, lastSet)) baseX = p.x;   // first frame, or the layout moved us
            p.x = baseX + fromX * (1f - ease);
            rt.anchoredPosition = p;
            lastSet = p.x;

            if (k >= 1f && cg.alpha >= 1f)
            {
                p.x = baseX; rt.anchoredPosition = p; cg.alpha = 1f;
                running = false;
            }
        }

        private void OnDisable() { if (running && cg != null) { cg.alpha = 1f; running = false; } }
    }
}
