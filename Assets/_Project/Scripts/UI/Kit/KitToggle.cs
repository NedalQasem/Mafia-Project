using System;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>Brass lever switch: the knob slides, the track lights up green when on. In RTL "on" is the left side.</summary>
    public class KitToggle : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image track;
        [SerializeField] private RectTransform knob;
        [SerializeField] private Image lit;
        [SerializeField] private bool isOn;
        [SerializeField] private float travel = 26f;

        public bool IsOn { get => isOn; set { isOn = value; Snap(); } }
        public event Action<bool> OnChanged;
        private float t;

        private void Awake() { if (button != null) button.onClick.AddListener(() => { isOn = !isOn; OnChanged?.Invoke(isOn); }); Snap(); }
        private void OnEnable() { Snap(); }

        private void Snap() { t = isOn ? 1f : 0f; Apply(); }

        private void Update()
        {
            float goal = isOn ? 1f : 0f;
            if (Mathf.Approximately(t, goal)) return;
            t = Mathf.MoveTowards(t, goal, Time.unscaledDeltaTime * 7f);
            Apply();
        }

        private void Apply()
        {
            float e = 1f - Mathf.Pow(1f - t, 3f);
            if (knob != null) knob.anchoredPosition = new Vector2(Mathf.Lerp(travel, -travel, e), knob.anchoredPosition.y);
            if (lit != null) lit.color = new Color(0.25f, 0.85f, 0.5f, 0.75f * e);
            if (track != null) track.color = Color.Lerp(Color.white, new Color(0.35f, 0.85f, 0.6f, 1f), e);
        }
    }
}
