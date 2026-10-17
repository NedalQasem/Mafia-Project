using UnityEngine;
using UnityEngine.EventSystems;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Press feedback for kit buttons: the face sinks like real leather/brass and springs back.
    /// Attach to the button root; assign the visual child that should move.
    /// </summary>
    [DisallowMultipleComponent]
    public class KitButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform face;
        [SerializeField] private float sink = 8f;
        [SerializeField] private float pressedScale = 0.985f;

        private Vector2 restPos;
        private float t;          // 0 = rest, 1 = pressed
        private float target;
        private float velocity;
        private bool cached;

        private void Cache()
        {
            if (cached) return;
            if (face == null) face = transform as RectTransform;
            restPos = face.anchoredPosition;
            cached = true;
        }

        private void OnEnable() { Cache(); t = target = 0; Apply(); }
        public void OnPointerDown(PointerEventData e) { target = 1f; }
        public void OnPointerUp(PointerEventData e) { target = 0f; }
        public void OnPointerExit(PointerEventData e) { target = 0f; }

        private void Update()
        {
            if (Mathf.Approximately(t, target) && Mathf.Abs(velocity) < 0.001f) return;
            // fast down, springy up
            t = Mathf.SmoothDamp(t, target, ref velocity, target > t ? 0.03f : 0.09f, Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(t - target) < 0.002f) { t = target; velocity = 0; }
            Apply();
        }

        private void Apply()
        {
            if (face == null) return;
            face.anchoredPosition = restPos + Vector2.down * (sink * t);
            float s = Mathf.Lerp(1f, pressedScale, t);
            face.localScale = new Vector3(s, s, 1f);
        }
    }
}
