using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>Settings: drawers (only one open), radio dials, mic test meter, toggles.</summary>
    public class KitSettingsScreen : MonoBehaviour
    {
        [SerializeField] private KitSettingsSection[] sections;
        [SerializeField] private KitKnob micKnob, speakerKnob;
        [SerializeField] private Image[] meter;
        [SerializeField] private Button micTestButton;
        [SerializeField] private ArabicText micTestLabel;
        [SerializeField] private KitSegmented sensitivity;
        [SerializeField] private KitToggle noiseToggle, pttToggle, muteJoinToggle, narratorToggle;

        public event Action<int> OnMicVolume, OnSpeakerVolume, OnSensitivity;
        /// <summary>key: "noise", "ptt", "mutejoin" or "narrator".</summary>
        public event Action<string, bool> OnToggleChanged;
        /// <summary>The mic test started (true) or stopped (false). The app layer starts and stops the real microphone.</summary>
        public event Action<bool> OnMicTest;
        /// <summary>Returns the current microphone level 0..1. Without it the prototype animation runs.</summary>
        public Func<float> MicLevel;

        private Coroutine test;

        private void Awake()
        {
            foreach (var s in sections) s.OnHeaderClicked += OnSection;
            if (micTestButton != null) micTestButton.onClick.AddListener(ToggleTest);
            if (micKnob != null) micKnob.OnChanged += v => OnMicVolume?.Invoke(v);
            if (speakerKnob != null) speakerKnob.OnChanged += v => OnSpeakerVolume?.Invoke(v);
            if (sensitivity != null) sensitivity.OnChanged += v => OnSensitivity?.Invoke(v);
            if (noiseToggle != null) noiseToggle.OnChanged += on => OnToggleChanged?.Invoke("noise", on);
            if (pttToggle != null) pttToggle.OnChanged += on => OnToggleChanged?.Invoke("ptt", on);
            if (muteJoinToggle != null) muteJoinToggle.OnChanged += on => OnToggleChanged?.Invoke("mutejoin", on);
            if (narratorToggle != null) narratorToggle.OnChanged += on => OnToggleChanged?.Invoke("narrator", on);
            Paint(0);
        }

        private void OnSection(KitSettingsSection s)
        {
            bool open = !s.IsOpen;
            foreach (var o in sections) if (o != s && o.IsOpen) o.SetOpen(false);
            s.SetOpen(open);
        }

        /// <summary>Shows saved values without raising the change events.</summary>
        public void Load(int mic, int speaker, int sens, bool noise, bool ptt, bool muteJoin, bool narrator)
        {
            if (micKnob != null) micKnob.Set(mic);
            if (speakerKnob != null) speakerKnob.Set(speaker);
            if (sensitivity != null) sensitivity.Set(sens);
            if (noiseToggle != null) noiseToggle.IsOn = noise;
            if (pttToggle != null) pttToggle.IsOn = ptt;
            if (muteJoinToggle != null) muteJoinToggle.IsOn = muteJoin;
            if (narratorToggle != null) narratorToggle.IsOn = narrator;
        }

        private void StopTest()
        {
            if (test != null) { StopCoroutine(test); test = null; }
            Paint(0); micTestLabel.Text = "جرّب المايك";
            OnMicTest?.Invoke(false);
        }

        private void OnDisable() { if (test != null) StopTest(); }

        private void ToggleTest()
        {
            if (test != null) { StopTest(); return; }
            OnMicTest?.Invoke(true);
            test = StartCoroutine(Test());
        }

        private IEnumerator Test()
        {
            micTestLabel.Text = "تكلّم الآن... (اضغط للإيقاف)";
            float level = 0;
            for (float t = 0; t < 8f; t += Time.unscaledDeltaTime)
            {
                float target;
                if (MicLevel != null)
                {
                    // speech peaks around 0.02-0.3: a square-root curve makes quiet voices visible on the meter
                    float raw = Mathf.Clamp01(MicLevel() * (micKnob != null ? micKnob.Value / 50f : 1f));
                    target = Mathf.Clamp01(Mathf.Sqrt(raw));
                }
                else target = (0.25f + UnityEngine.Random.value * 0.75f) * (micKnob != null ? micKnob.Value / 100f : 1f);
                level += (target - level) * 0.4f;
                Paint(level);
                yield return null;
            }
            test = null; StopTest();
        }

        public void Paint(float level)
        {
            if (meter == null) return;
            int n = Mathf.RoundToInt(level * meter.Length);
            for (int i = 0; i < meter.Length; i++)
            {
                if (meter[i] == null) continue;
                meter[i].color = i < n ? (i > 17 ? new Color32(0xE0, 0x43, 0x4B, 255) : i > 12 ? new Color32(0xE0, 0xB0, 0x40, 255) : new Color32(0x43, 0xC0, 0x8A, 255)) : new Color32(0x22, 0x22, 0x22, 255);
            }
        }
    }
}
