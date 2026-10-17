using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>Brass lever switch: the knob slides, the track lights up green when on. In RTL "on" is the left side.</summary>
    public class KitToggle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image track;
        [SerializeField] private RectTransform knob;
        [SerializeField] private Image lit;
        [SerializeField] private bool isOn;
        [SerializeField] private float travel = 26f;

        public bool IsOn { get => isOn; set { isOn = value; Snap(); } }
        public event Action<bool> OnChanged;
        private float t, pressed;
        private bool held;

        public void OnPointerDown(PointerEventData e) { held = true; }
        public void OnPointerUp(PointerEventData e) { held = false; }

        private void Awake() { if (button != null) button.onClick.AddListener(() => { isOn = !isOn; OnChanged?.Invoke(isOn); }); Snap(); }
        private void OnEnable() { Snap(); }

        private void Snap() { t = isOn ? 1f : 0f; Apply(); }

        private void Update()
        {
            float goal = isOn ? 1f : 0f;
            float pg = held ? 1f : 0f;
            if (Mathf.Approximately(t, goal) && Mathf.Approximately(pressed, pg)) return;
            t = Mathf.MoveTowards(t, goal, Time.unscaledDeltaTime * 7f);
            pressed = Mathf.MoveTowards(pressed, pg, Time.unscaledDeltaTime * 12f);
            Apply();
        }

        private void Apply()
        {
            float e = 1f - Mathf.Pow(1f - t, 3f);
            if (knob != null) knob.anchoredPosition = new Vector2(Mathf.Lerp(travel, -travel, e), knob.anchoredPosition.y);
            if (lit != null) lit.color = new Color(1f, 1f, 1f, e);
            if (knob != null) knob.sizeDelta = new Vector2(Mathf.Lerp(60f, 70f, pressed), 60f);
        }
    }
}
