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

        private Coroutine test;

        private void Awake()
        {
            foreach (var s in sections) s.OnHeaderClicked += OnSection;
            if (micTestButton != null) micTestButton.onClick.AddListener(ToggleTest);
            Paint(0);
        }

        private void OnSection(KitSettingsSection s)
        {
            bool open = !s.IsOpen;
            foreach (var o in sections) if (o != s && o.IsOpen) o.SetOpen(false);
            s.SetOpen(open);
        }

        private void ToggleTest()
        {
            if (test != null) { StopCoroutine(test); test = null; Paint(0); micTestLabel.Text = "جرّب المايك"; return; }
            test = StartCoroutine(Test());
        }

        private IEnumerator Test()
        {
            micTestLabel.Text = "تكلّم الآن... (اضغط للإيقاف)";
            float level = 0;
            for (float t = 0; t < 5f; t += Time.unscaledDeltaTime)
            {
                float target = (0.25f + Random.value * 0.75f) * (micKnob != null ? micKnob.Value / 100f : 1f);
                level += (target - level) * 0.35f;
                Paint(level);
                yield return new WaitForSecondsRealtime(0.08f);
            }
            Paint(0); micTestLabel.Text = "جرّب المايك"; test = null;
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
