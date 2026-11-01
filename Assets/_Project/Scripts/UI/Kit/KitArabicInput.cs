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

        /// <summary>
        /// Makes an input over a part of a frame that is only a picture: an invisible real input (the phone keyboard talks to it) over
        /// <paramref name="area"/>, with the picture's own Arabic text (<paramref name="mirror"/>) drawing what is typed.
        /// </summary>
        public static KitArabicInput Attach(RectTransform area, ArabicText mirror, GameObject placeholder, TMP_InputField.ContentType type, int characterLimit, bool multiline = false)
        {
            var go = new GameObject("Input", typeof(RectTransform));
            go.SetActive(false);
            var rt = (RectTransform)go.transform; rt.SetParent(area, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var hit = go.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var vr = (RectTransform)vp.transform; vr.SetParent(rt, false); vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = vr.offsetMax = Vector2.zero;
            var tx = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var tr = (RectTransform)tx.transform; tr.SetParent(vr, false); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            var tmp = tx.GetComponent<TextMeshProUGUI>();
            var mt = mirror != null ? mirror.GetComponent<TMP_Text>() : null;
            if (mt != null) { tmp.font = mt.font; tmp.fontSize = mt.fontSize; }
            tmp.color = Color.clear; tmp.raycastTarget = false; tmp.alignment = TextAlignmentOptions.MidlineRight;
            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = vr; field.textComponent = tmp; field.targetGraphic = hit;
            field.contentType = type; field.characterLimit = characterLimit;
            field.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            var comp = go.AddComponent<KitArabicInput>();
            comp.input = field; comp.mirror = mirror; comp.placeholder = placeholder;
            go.SetActive(true);
            return comp;
        }

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
