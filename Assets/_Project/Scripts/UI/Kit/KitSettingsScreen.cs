using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MafiaNight.UI.Animations;

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
        [SerializeField] private KitSegmented sensitivity, language;
        [SerializeField] private KitToggle noiseToggle, pttToggle, muteJoinToggle, narratorToggle;
        [Header("Screen change effect")]
        [SerializeField] private KitSegmented fxSegment;
        [SerializeField] private KitToggle reduceMotionToggle;
        [SerializeField] private Button fxPreviewButton;
        [SerializeField] private RectTransform fxPreviewArea;
        [SerializeField] private GameObject fxPreviewOld;
        [SerializeField] private ArabicText fxHint;

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
            if (language != null)
            {
                language.Set(PlayerPrefs.GetString(MafiaNight.Core.GameConstants.PREF_LANGUAGE, "ar") == "en" ? 1 : 0);
                language.OnChanged += v => { PlayerPrefs.SetString(MafiaNight.Core.GameConstants.PREF_LANGUAGE, v == 1 ? "en" : "ar"); PlayerPrefs.Save(); };
            }
            SetupFx();
            Paint(0);
        }

        private static readonly string[] FxModes = { BurnSettings.Fire, BurnSettings.Simple, BurnSettings.Off };
        private bool previewing;

        private void SetupFx()
        {
            if (fxSegment != null)
            {
                fxSegment.Set(Mathf.Max(0, System.Array.IndexOf(FxModes, BurnSettings.Chosen)));
                fxSegment.OnChanged += i => { BurnSettings.Chosen = FxModes[Mathf.Clamp(i, 0, FxModes.Length - 1)]; PaintFxHint(); Preview(); };
            }
            if (reduceMotionToggle != null)
            {
                reduceMotionToggle.IsOn = BurnSettings.ReduceMotion;
                reduceMotionToggle.OnChanged += on => { BurnSettings.ReduceMotion = on; PaintFxHint(); };
            }
            if (fxPreviewButton != null) fxPreviewButton.onClick.AddListener(Preview);
            PaintFxHint();
        }

        private void PaintFxHint()
        {
            if (fxHint == null) return;
            string chosen = BurnSettings.Chosen;
            fxHint.Text = chosen == BurnSettings.Fire && BurnSettings.ReduceMotion ? "تقليل الحركة شغّال، فنستعمل التلاشي البسيط بدل الحرق."
                : chosen == BurnSettings.Fire ? "الشاشة القديمة تحترق من زاوية لزاوية وتظهر الجديدة تحتها، مع شرر ودخان."
                : chosen == BurnSettings.Simple ? "تلاشي سريع للشاشة القديمة. أخف على الجهاز."
                : "تبديل فوري بين الشاشات من غير أي حركة.";
            // the line may have changed its number of lines: the open drawer measures itself again
            if (sections != null) foreach (var s in sections) if (s != null && s.IsOpen && fxHint.transform.IsChildOf(s.transform)) s.SetOpen(true, false);
        }

        /// <summary>The preview strip: the "old screen" paper burns away (or fades, or just goes) over the "new screen".</summary>
        public void Preview()
        {
            if (previewing || fxPreviewArea == null || fxPreviewOld == null || !isActiveAndEnabled) return;
            StartCoroutine(PreviewRoutine());
        }

        private IEnumerator PreviewRoutine()
        {
            previewing = true;
            fxPreviewOld.SetActive(true);
            Texture2D snap = null;
            if (BurnSettings.Mode != BurnSettings.Off) yield return BurnTransition.Capture(fxPreviewArea, t => snap = t);
            fxPreviewOld.SetActive(false);
            if (snap != null) yield return BurnTransition.Attach(fxPreviewArea, snap).Play(BurnCorner.BottomRight, 0.9f);
            yield return new WaitForSecondsRealtime(0.7f);
            fxPreviewOld.SetActive(true);
            previewing = false;
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
                var br = meter[i].rectTransform; br.sizeDelta = new Vector2(br.sizeDelta.x, i < n ? 18f * 1080f / 390f : 18f * 1080f / 390f * 0.3f);      // lit bars stand tall, the others are short dashes (settings.html ".vu i")
                meter[i].color = i < n ? (i > 17 ? new Color32(0xE0, 0x43, 0x4B, 255) : i > 12 ? new Color32(0xE0, 0xB0, 0x40, 255) : new Color32(0x43, 0xC0, 0x8A, 255)) : new Color32(0x22, 0x22, 0x22, 255);
            }
        }
    }
}
