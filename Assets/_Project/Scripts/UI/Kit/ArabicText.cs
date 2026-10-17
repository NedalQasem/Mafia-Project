using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Put next to a TMP_Text. Set <see cref="Text"/> with normal logical Arabic; this component shapes it
    /// (joined letters, right-to-left order) and, if wrapping is on, breaks lines itself so word order stays correct.
    /// Rich-text tags are not supported inside shaped text.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ArabicText : UIBehaviour
    {
        [TextArea(1, 6)] [SerializeField] private string text = "";
        [SerializeField] private bool wrap;
        [SerializeField] private bool arabicDigits = true;

        private TMP_Text tmp;
        private bool refreshing;

        public string Text
        {
            get => text;
            set { if (text == value) return; text = value; Refresh(); }
        }

        public bool Wrap { get => wrap; set { wrap = value; Refresh(); } }

        protected override void OnEnable() { base.OnEnable(); Refresh(); }
        protected override void OnRectTransformDimensionsChange() { base.OnRectTransformDimensionsChange(); if (wrap) Refresh(); }
#if UNITY_EDITOR
        protected override void OnValidate() { base.OnValidate(); if (isActiveAndEnabled) Refresh(); }
#endif

        public void Refresh()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                if (tmp == null) tmp = GetComponent<TMP_Text>();
                if (tmp == null) return;
                tmp.isRightToLeftText = false;     // we already output visual order
                tmp.richText = false;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;

                if (!ArabicShaper.ContainsArabic(text)) { tmp.text = text; return; }
                tmp.text = wrap ? WrapShaped() : ArabicShaper.Shape(text, arabicDigits);
            }
            finally { refreshing = false; }
        }

        private string WrapShaped()
        {
            float max = tmp.rectTransform.rect.width - tmp.margin.x - tmp.margin.z;
            if (max <= 1f) return ArabicShaper.Shape(text, arabicDigits);

            var paragraphs = text.Split('\n');
            var lines = new System.Collections.Generic.List<string>();
            foreach (var p in paragraphs)
            {
                var words = p.Split(' ');
                string cur = "";
                foreach (var w in words)
                {
                    string cand = cur.Length == 0 ? w : cur + " " + w;
                    float width = tmp.GetPreferredValues(ArabicShaper.Shape(cand, arabicDigits)).x;
                    if (width > max && cur.Length > 0)
                    {
                        lines.Add(ArabicShaper.Shape(cur, arabicDigits));
                        cur = w;
                    }
                    else cur = cand;
                }
                lines.Add(ArabicShaper.Shape(cur, arabicDigits));
            }
            return string.Join("\n", lines);
        }
    }
}
