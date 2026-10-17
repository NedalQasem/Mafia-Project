using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MafiaNight.UI.Kit
{
    /// <summary>Press and hold to confirm (used for deleting the account). A red bar fills while holding.</summary>
    public class KitHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private ArabicText label;
        [SerializeField] private string idleText = "حذف الحساب: اضغط مطولاً ٣ ثوانٍ";
        [SerializeField] private float seconds = 3f;

        public event Action OnConfirmed;
        private float held;
        private bool down, done;

        private void OnEnable() { ResetState(); }

        public void OnPointerDown(PointerEventData e) { if (!done) down = true; }
        public void OnPointerUp(PointerEventData e) { if (!done) ResetState(); }
        public void OnPointerExit(PointerEventData e) { if (!done) ResetState(); }

        private void ResetState() { down = false; held = 0; SetFill(0); if (label != null) label.Text = idleText; }

        private void SetFill(float p)
        {
            if (fill == null) return;
            fill.anchorMin = new Vector2(1f - p, 0); fill.anchorMax = new Vector2(1, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (!down || done) return;
            held += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(held / seconds);
            SetFill(p);
            if (label != null) label.Text = "استمر بالضغط... " + CoinPill.Format(Mathf.CeilToInt((1f - p) * seconds));
            if (p >= 1f)
            {
                done = true; down = false;
                if (label != null) label.Text = "هذا تجريبي: لن يُحذف شيء";
                OnConfirmed?.Invoke();
                Invoke(nameof(Unlock), 1.6f);
            }
        }

        private void Unlock() { done = false; ResetState(); }
    }
}
