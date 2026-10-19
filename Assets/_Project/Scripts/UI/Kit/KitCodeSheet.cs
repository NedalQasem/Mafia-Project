using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>Bottom sheet where the player types the 4-letter code of a private room.</summary>
    public class KitCodeSheet : MonoBehaviour
    {
        [SerializeField] private CanvasGroup dim;
        [SerializeField] private RectTransform sheet;
        [SerializeField] private TMP_InputField input;
        [SerializeField] private ArabicText message;
        [SerializeField] private Button joinButton, closeButton;
        [SerializeField] private int length = 4;

        public event Action<string> OnSubmit;

        private Coroutine anim;
        private bool busy;

        private void Awake()
        {
            joinButton.onClick.AddListener(Submit);
            closeButton.onClick.AddListener(Close);
            var d = dim != null ? dim.GetComponent<Button>() : null; if (d != null) d.onClick.AddListener(Close);
            input.characterLimit = length;
            input.selectionColor = new Color(0.72f, 0.56f, 0.28f, 0.35f);
            input.onValueChanged.AddListener(v =>
            {
                string up = v.ToUpperInvariant();
                if (up != v) input.SetTextWithoutNotify(up);
                message.Text = "";
            });
            input.onSubmit.AddListener(_ => Submit());
            dim.alpha = 0f; dim.blocksRaycasts = false;
            sheet.anchoredPosition = new Vector2(0f, -(sheet.rect.height + 80f));
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            input.SetTextWithoutNotify(""); message.Text = ""; busy = false;
            Play(true);
            input.ActivateInputField();
        }

        public void Close() { input.DeactivateInputField(); Play(false); }

        public void ShowMessage(string text) { message.Text = text; busy = false; }

        public void SetBusy(bool value) { busy = value; joinButton.interactable = !value; }

        private void Submit()
        {
            if (busy) return;
            if (input.text.Trim().Length < length) { message.Text = "الكود " + length + " خانات."; return; }
            OnSubmit?.Invoke(input.text.Trim());
        }

        private void Play(bool show) { if (anim != null) StopCoroutine(anim); anim = StartCoroutine(Slide(show)); }

        private IEnumerator Slide(bool show)
        {
            dim.blocksRaycasts = show;
            float from = sheet.anchoredPosition.y, to = show ? 0f : -(sheet.rect.height + 80f);
            float a0 = dim.alpha, a1 = show ? 1f : 0f;
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.4f), 3f);
                sheet.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, to, k));
                dim.alpha = Mathf.Lerp(a0, a1, k);
                yield return null;
            }
            sheet.anchoredPosition = new Vector2(0f, to); dim.alpha = a1;
        }
    }
}
