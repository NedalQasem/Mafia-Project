using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Fits a full-screen container to <see cref="Screen.safeArea"/> (notch, camera hole, gesture bar) so the Kit screens
    /// never put controls under them. Backgrounds stay outside the container and fill the whole screen.
    /// Optional <see cref="bottomFill"/>: a strip painted behind the gesture bar in the nav bar's colour, so the footer
    /// seems to continue to the very edge instead of leaving a gap.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class KitSafeArea : MonoBehaviour
    {
        [SerializeField] private RectTransform bottomFill;

        private RectTransform rect;
        private Rect lastSafe;
        private Vector2Int lastSize;

        private void OnEnable() { rect = (RectTransform)transform; Apply(); }

        private void Update()
        {
            if (Screen.safeArea != lastSafe || Screen.width != lastSize.x || Screen.height != lastSize.y) Apply();
        }

        private void Apply()
        {
            if (rect == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            lastSafe = safe;
            // In the Editor the Game view can report a stale safe area (from another resolution): then use the whole screen
            if (safe.width <= 0f || safe.height <= 0f || safe.xMax > Screen.width + 1f || safe.yMax > Screen.height + 1f || safe.xMin < -1f || safe.yMin < -1f)
                safe = new Rect(0f, 0f, Screen.width, Screen.height); lastSize = new Vector2Int(Screen.width, Screen.height);

            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            if (bottomFill != null)
            {
                var canvas = GetComponentInParent<Canvas>();
                float scale = canvas != null && canvas.rootCanvas.scaleFactor > 0f ? canvas.rootCanvas.scaleFactor : 1f;
                bottomFill.sizeDelta = new Vector2(0f, safe.yMin / scale);
            }
        }
    }
}
