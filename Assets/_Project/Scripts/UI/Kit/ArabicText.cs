using System;
using TMPro;
using UnityEngine;
using MafiaNight.Core;
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
        /// <summary>Turn Latin digits into Arabic-Indic digits (default). Off keeps the digits exactly as written.</summary>
        public bool ArabicDigits { get => arabicDigits; set { arabicDigits = value; Refresh(); } }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Application.isPlaying && Loc.English) { var c = GetComponentInParent<Canvas>(); SetMirrored(c != null && KitLocale.IsMirrored(c.rootCanvas)); }
            Refresh();
        }

        private bool mirroredNow;

        /// <summary>
        /// English mode draws the canvas in a mirror (<see cref="KitLocale"/>): this text is flipped back so its letters read normally, and aligned the
        /// other way (what was at the right of its box in Arabic sits at the left of the mirrored box).
        /// </summary>
        public void SetMirrored(bool on)
        {
            if (!Application.isPlaying || on == mirroredNow) return;
            mirroredNow = on;
            if (tmp == null) tmp = GetComponent<TMP_Text>();
            if (tmp != null)
            {
                var h = tmp.horizontalAlignment;
                if (h == HorizontalAlignmentOptions.Left) tmp.horizontalAlignment = HorizontalAlignmentOptions.Right;
                else if (h == HorizontalAlignmentOptions.Right) tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
            }
            transform.localRotation = transform.localRotation * Quaternion.Euler(0f, 180f, 0f);      // turned back: the letters read normally in the mirror
            Refresh();
        }
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
                // Lalezar's pair-kerning table throws joined letters on top of each other (a final reh after a yaa jumps ~100 px back and hides
                // under its neighbours): shaped text needs no kerning from it
                if (tmp.font != null && tmp.font.name.IndexOf("Lalezar", StringComparison.OrdinalIgnoreCase) >= 0 && tmp.fontFeatures != null && tmp.fontFeatures.Count > 0)
                    tmp.fontFeatures = new System.Collections.Generic.List<UnityEngine.TextCore.OTL_FeatureTag>();

                if (Application.isPlaying && Loc.English)
                {
                    // the English line (or the Arabic one when it is not translated yet): English is not shaped and breaks its lines by itself
                    string en = Loc.LatinDigits(Loc.T(text));
                    if (!ArabicShaper.ContainsArabic(en))
                    {
                        tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                        tmp.text = en;
                        return;
                    }
                }
                if (!ArabicShaper.ContainsArabic(text)) { tmp.text = text; return; }
                tmp.text = wrap ? WrapShaped() : Shape(text);
            }
            finally { refreshing = false; }
        }

        // calligraphic fonts may not have the lam-alef ligature glyphs; then the two letters are joined instead
        private string Shape(string s)
        {
            bool lig = tmp.font == null || tmp.font.HasCharacter((char)0xFEFB, true, true);
            return ArabicShaper.Shape(s, arabicDigits, lig, tmp.font != null ? ContextualForms.For(tmp.font.name) : null);
        }

        private string WrapShaped()
        {
            float max = tmp.rectTransform.rect.width - tmp.margin.x - tmp.margin.z;
            if (max <= 1f) return Shape(text);

            var paragraphs = text.Split('\n');
            var lines = new System.Collections.Generic.List<string>();
            foreach (var p in paragraphs)
            {
                var words = p.Split(' ');
                string cur = "";
                foreach (var w in words)
                {
                    string cand = cur.Length == 0 ? w : cur + " " + w;
                    float width = tmp.GetPreferredValues(Shape(cand)).x;
                    if (width > max && cur.Length > 0)
                    {
                        lines.Add(Shape(cur));
                        cur = w;
                    }
                    else cur = cand;
                }
                lines.Add(Shape(cur));
            }
            return string.Join("\n", lines);
        }
    }
}
