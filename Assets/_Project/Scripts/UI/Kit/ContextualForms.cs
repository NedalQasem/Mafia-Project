using System.Collections.Generic;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Glyph choices of a calligraphic font (Aref Ruqaa) that depend on the neighbouring letters. The table is made by Tools/ui-gen/fontfix.js
    /// (Resources/Kit/Forms/&lt;font asset name&gt;.txt): a default glyph per letter and joining form, plus exceptions for specific neighbours.
    /// Glyphs are Private Use code points (U+E000..) that exist in the converted font.
    /// </summary>
    public sealed class ContextualForms
    {
        private readonly Dictionary<(char letter, int form), char> defaults = new Dictionary<(char, int), char>();
        private readonly Dictionary<(char prev, char letter, char next), char> exceptions = new Dictionary<(char, char, char), char>();

        private static readonly Dictionary<string, ContextualForms> Cache = new Dictionary<string, ContextualForms>();

        /// <summary>The table for a font asset, or null when the font has none (then the normal presentation forms are used).</summary>
        public static ContextualForms For(string fontAssetName)
        {
            if (string.IsNullOrEmpty(fontAssetName)) return null;
            if (Cache.TryGetValue(fontAssetName, out var c)) return c;
            var ta = Resources.Load<TextAsset>("Kit/Forms/" + fontAssetName);
            c = ta != null ? Parse(ta.text) : null;
            Cache[fontAssetName] = c;
            return c;
        }

        private static ContextualForms Parse(string text)
        {
            var f = new ContextualForms();
            foreach (var line in text.Split('\n'))
            {
                var p = line.TrimEnd('\r').Split('\t');
                if (p.Length == 4 && p[0] == "D") f.defaults[(p[1][0], int.Parse(p[2]))] = (char)System.Convert.ToInt32(p[3], 16);
                else if (p.Length == 5 && p[0] == "E") f.exceptions[(p[1][0], p[2][0], p[3][0])] = (char)System.Convert.ToInt32(p[4], 16);
            }
            return f;
        }

        /// <param name="prev">the previous letter when it joins this one, else '-'</param>
        /// <param name="next">the next letter when this one joins it, else '-'</param>
        /// <param name="form">0 isolated, 1 final, 2 initial, 3 medial</param>
        public bool TryGet(char prev, char letter, char next, int form, out char glyph)
        {
            if (exceptions.TryGetValue((prev, letter, next), out glyph)) return true;
            return defaults.TryGetValue((letter, form), out glyph);
        }
    }
}
