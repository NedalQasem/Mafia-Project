using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Text field that shows Arabic correctly while typing. The real <see cref="TMP_InputField"/> keeps the logical text
    /// (and talks to the phone keyboard) but is invisible; an <see cref="ArabicText"/> mirror draws the shaped, right-to-left
    /// version and a small caret is drawn at the logical end of the text.
    /// </summary>
    public class KitArabicInput : MonoBehaviour
    {
        [SerializeField] private TMP_InputField input;
        [SerializeField] private ArabicText mirror;
        [SerializeField] private GameObject placeholder;
        [SerializeField] private RectTransform caret;

        public event Action<string> OnChanged;
        public event Action<string> OnSubmit;

        public string Text
        {
            get => input.text;
            set { input.SetTextWithoutNotify(value ?? ""); Sync(); }
        }

        private void Awake()
        {
            input.onValueChanged.AddListener(_ => { Sync(); OnChanged?.Invoke(input.text); });
            input.onSubmit.AddListener(t => OnSubmit?.Invoke(t));
            // the real text and its selection stay invisible: the mirror is what the player reads
            input.customCaretColor = true; input.caretColor = Color.clear; input.selectionColor = Color.clear;
            if (input.textComponent != null) { var c = input.textComponent.color; c.a = 0f; input.textComponent.color = c; }
            Sync();
        }

        public void Focus() { input.ActivateInputField(); }

        private void Sync()
        {
            string t = input.text;
            if (mirror != null) mirror.Text = t;
            if (placeholder != null) placeholder.SetActive(string.IsNullOrEmpty(t));
        }

        private void Update()
        {
            if (caret == null || mirror == null) return;
            bool show = input.isFocused && (int)(Time.unscaledTime * 2f) % 2 == 0;
            caret.gameObject.SetActive(show);
            if (!show) return;
            var tmp = mirror.GetComponent<TMP_Text>();
            float w = string.IsNullOrEmpty(input.text) ? 0f : tmp.preferredWidth;
            caret.anchoredPosition = new Vector2(-(w + 6f), caret.anchoredPosition.y);   // text is right-aligned: its logical end is on the left
        }
    }
}
