using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MafiaNight.UI.Kit
{
    /// <summary>Radio dial: drag around the knob to set 0..100 (270 degree sweep).</summary>
    public class KitKnob : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [SerializeField] private RectTransform pointer;
        [SerializeField] private ArabicText valueLabel;
        [SerializeField, Range(0, 100)] private int value = 70;

        public event Action<int> OnChanged;
        public int Value => value;

        private void OnEnable() { Apply(); }

        public void Set(int v) { value = Mathf.Clamp(v, 0, 100); Apply(); }

        public void OnPointerDown(PointerEventData e) { Pick(e); }
        public void OnDrag(PointerEventData e) { Pick(e); }

        private void Pick(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local)) return;
            float a = Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg;           // 0 = up, clockwise positive
            if (a < -135f) a = a < -157f ? 135f : -135f;
            if (a > 135f) a = 135f;
            value = Mathf.RoundToInt((a + 135f) / 2.7f);
            Apply(); OnChanged?.Invoke(value);
        }

        private void Apply()
        {
            if (pointer != null) pointer.localRotation = Quaternion.Euler(0, 0, 135f - value * 2.7f);
            if (valueLabel != null) valueLabel.Text = CoinPill.Format(value) + "٪";
        }
    }
}
