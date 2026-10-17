using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>Leather drawer: tap the header to slide the body open or closed.</summary>
    public class KitSettingsSection : MonoBehaviour
    {
        [SerializeField] private Button header;
        [SerializeField] private RectTransform body, content, chevron;
        [SerializeField] private LayoutElement bodyLayout;
        [SerializeField] private bool openAtStart;

        public bool IsOpen { get; private set; }
        public event System.Action<KitSettingsSection> OnHeaderClicked;
        private Coroutine anim;

        private void Awake() { if (header != null) header.onClick.AddListener(() => OnHeaderClicked?.Invoke(this)); }
        private void Start() { SetOpen(openAtStart, false); }

        public float TargetHeight() { LayoutRebuilder.ForceRebuildLayoutImmediate(content); return LayoutUtility.GetPreferredHeight(content); }

        public void SetOpen(bool open, bool animate = true)
        {
            IsOpen = open;
            if (chevron != null) chevron.localRotation = Quaternion.Euler(0, 0, open ? 0f : 90f);
            float to = open ? TargetHeight() : 0f;
            if (!animate || !isActiveAndEnabled) { Apply(to); return; }
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Slide(to));
        }

        private IEnumerator Slide(float to)
        {
            float from = bodyLayout.preferredHeight;
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / 0.4f, 3f);
                Apply(Mathf.Lerp(from, to, k));
                yield return null;
            }
            Apply(to);
        }

        private void Apply(float h)
        {
            bodyLayout.preferredHeight = h;
            body.sizeDelta = new Vector2(body.sizeDelta.x, h);
        }
    }
}
