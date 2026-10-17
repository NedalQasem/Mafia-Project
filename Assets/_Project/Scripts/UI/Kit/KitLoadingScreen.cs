using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Loading screen: the old mafia sedan drives through a night city (skyline layers + road scroll, wheels spin, exhaust, headlight).
    /// Call <see cref="SetProgress"/> from the real loader, or leave Auto Run on for the prototype.
    /// </summary>
    public class KitLoadingScreen : MonoBehaviour
    {
        [SerializeField] private RawImage farCity, nearCity, dash, curb;
        [SerializeField] private RectTransform car, wheelFront, wheelRear;
        [SerializeField] private Image beam;
        [SerializeField] private Image[] stars, puffs;
        [SerializeField] private RectTransform barFill;
        [SerializeField] private ArabicText percent, phase, tip;
        [SerializeField] private GameObject enterButton;
        [SerializeField] private Button enter;
        [SerializeField] private bool autoRun = true;
        [SerializeField] private float duration = 7f;

        public event Action OnEnter;
        private float progress;
        private bool ready;
        private float tipTimer;
        private int tipIndex;
        private Vector2 carRest;
        private static readonly string[] Tips = { "الصمت في السهرة له ثمن.", "المختار صوته يحسب صوتين.", "اسأل كثيراً، وصدّق قليلاً.", "ما كل من ابتسم صاحبك.", "القهوة تبرد، والمافيا لا تنام." };
        private static readonly string[] Phases = { "نتّصل بالحارة...", "نجهّز الطاولة...", "نوزّع الأوراق...", "بقيت لحظة..." };
        private static readonly int[] PhaseAt = { 0, 30, 60, 92 };

        private void Start()
        {
            if (!Application.isPlaying) return;
            carRest = car.anchoredPosition;
            Fit(farCity); Fit(nearCity); Fit(dash); Fit(curb);
            if (enter != null) enter.onClick.AddListener(() => { OnEnter?.Invoke(); if (autoRun) Restart(); });
            tip.Text = Tips[0];
            if (autoRun) StartCoroutine(Demo());
        }

        private static void Fit(RawImage r)
        {
            if (r == null || r.texture == null) return;
            var rt = (RectTransform)r.transform;
            r.uvRect = new Rect(0, 0, rt.rect.width / r.texture.width * (rt.rect.height / r.texture.height > 0 ? 1f : 1f), 1f);
        }

        public void Restart() { StopAllCoroutines(); ready = false; StartCoroutine(Demo()); }

        private IEnumerator Demo()
        {
            ready = false; enterButton.SetActive(false);
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.Clamp01(t / duration);
                SetProgress(p < .5f ? 2f * p * p : 1f - Mathf.Pow(-2f * p + 2f, 2f) / 2f);
                yield return null;
            }
            SetProgress(1f);
        }

        /// <summary>0..1</summary>
        public void SetProgress(float p)
        {
            progress = Mathf.Clamp01(p);
            int v = Mathf.RoundToInt(progress * 100f);
            percent.Text = CoinPill.Format(v) + "٪";
            for (int i = PhaseAt.Length - 1; i >= 0; i--) if (v >= PhaseAt[i]) { phase.Text = Phases[i]; break; }
            barFill.anchorMin = new Vector2(1f - progress, 0); barFill.anchorMax = new Vector2(1, 1); barFill.offsetMin = barFill.offsetMax = Vector2.zero;
            if (progress >= 1f && !ready) { ready = true; phase.Text = "جاهز"; enterButton.SetActive(true); tip.gameObject.SetActive(false); StartCoroutine(Honk()); }
            if (progress < 1f) { ready = false; if (enterButton != null) enterButton.SetActive(false); tip.gameObject.SetActive(true); }
        }

        private IEnumerator Honk()
        {
            for (int i = 0; i < 3; i++)
            {
                var c = beam.color; c.a = 0.2f; beam.color = c; yield return new WaitForSecondsRealtime(0.14f);
                c.a = 1f; beam.color = c; yield return new WaitForSecondsRealtime(0.14f);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            float dt = Time.unscaledDeltaTime;
            Scroll(farCity, 0.010f * dt); Scroll(nearCity, 0.045f * dt); Scroll(curb, 0.6f * dt); Scroll(dash, 0.55f * dt);
            if (wheelFront != null) { wheelFront.Rotate(0, 0, 640f * dt); wheelRear.Rotate(0, 0, 640f * dt); }
            if (car != null)
            {
                float t = Time.unscaledTime;
                car.anchoredPosition = carRest + new Vector2(0, Mathf.Sin(t * 9f) * 2.2f + Mathf.Sin(t * 5.3f) * 1.4f);
                car.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 7f) * 0.18f);
            }
            if (beam != null && !ready) { var c = beam.color; c.a = 0.8f + Mathf.Sin(Time.unscaledTime * 3f) * 0.12f; beam.color = c; }
            for (int i = 0; i < stars.Length; i++) { var c = stars[i].color; c.a = 0.45f + 0.4f * Mathf.Sin(Time.unscaledTime * (1f + i % 5 * 0.3f) + i); stars[i].color = c; }
            for (int i = 0; i < puffs.Length; i++)
            {
                float k = ((Time.unscaledTime * 0.7f) + i / (float)puffs.Length) % 1f;
                var rt = (RectTransform)puffs[i].transform;
                rt.anchoredPosition = new Vector2(-k * 90f, k * 70f);
                rt.localScale = Vector3.one * (0.5f + k * 2.4f);
                var c = puffs[i].color; c.a = (1f - k) * 0.5f; puffs[i].color = c;
            }
            if (!ready)
            {
                tipTimer += dt;
                if (tipTimer > 2.6f) { tipTimer = 0; tipIndex = (tipIndex + 1) % Tips.Length; tip.Text = Tips[tipIndex]; }
            }
        }

        private static void Scroll(RawImage r, float dx)
        {
            if (r == null) return;
            var uv = r.uvRect; uv.x -= dx; r.uvRect = uv;
        }
    }
}
