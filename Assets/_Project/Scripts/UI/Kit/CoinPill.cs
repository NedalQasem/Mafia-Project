using System.Collections;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>Coin balance pill. Value counts up/down smoothly. Digits are shown Arabic-Indic with ٬ grouping.</summary>
    public class CoinPill : MonoBehaviour
    {
        [SerializeField] private ArabicText label;
        [SerializeField] private RectTransform coinIcon;
        private int shown;
        private Coroutine anim;

        public void SetValue(int value, bool animate = true)
        {
            if (!animate || !isActiveAndEnabled) { shown = value; Render(value); return; }
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Count(shown, value));
        }

        private IEnumerator Count(int from, int to)
        {
            const float dur = 0.7f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / dur, 3f);
                Render(Mathf.RoundToInt(Mathf.Lerp(from, to, k)));
                if (coinIcon != null) coinIcon.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 14f) * 40f, 0);
                yield return null;
            }
            shown = to; Render(to);
            if (coinIcon != null) coinIcon.localRotation = Quaternion.identity;
        }

        private void Render(int v)
        {
            if (label == null) return;
            label.Text = Format(v);
        }

        public static string Format(int v)
        {
            string s = Mathf.Abs(v).ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", "٬");
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) sb.Append(c >= '0' && c <= '9' ? (char)('٠' + (c - '0')) : c);
            return (v < 0 ? "-" : "") + sb;
        }
    }
}
