using System.Collections;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>A small paper note that slides in with a short message and fades out.</summary>
    public class KitToast : MonoBehaviour
    {
        [SerializeField] private ArabicText label;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float seconds = 2.8f;

        private Coroutine run;

        private void Awake() { if (group != null) group.alpha = 0f; }

        public void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            label.Text = message;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            if (run != null) StopCoroutine(run);
            run = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            var rt = (RectTransform)transform;
            Vector2 home = rt.anchoredPosition;
            for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / 0.25f, 3f);
                group.alpha = k; rt.anchoredPosition = home + new Vector2(0f, (1f - k) * -40f);
                yield return null;
            }
            group.alpha = 1f; rt.anchoredPosition = home;
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / 0.35f; yield return null; }
            group.alpha = 0f;
            run = null;
        }
    }
}
