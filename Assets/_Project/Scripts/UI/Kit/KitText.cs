namespace MafiaNight.UI.Kit
{
    /// <summary>Helpers for text of unknown length. TMP's own ellipsis cannot be used with <see cref="ArabicText"/> (it cuts the wrong end of reversed text), so long single-line texts are cut here, before shaping.</summary>
    public static class KitText
    {
        /// <summary>Puts server text on a one-line label whatever its length: cut to <paramref name="chars"/> characters, then shrunk (down to <paramref name="minSize"/>) to fit the label's width.</summary>
        public static void Fit(ArabicText label, string text, int chars, float minSize)
        {
            label.Text = Clip(text, chars);
            var t = label.GetComponent<TMPro.TMP_Text>();
            if (!t.enableAutoSizing) { t.fontSizeMax = t.fontSize; t.fontSizeMin = minSize; t.enableAutoSizing = true; }
        }

        /// <summary>Several short lines (a headline written with line breaks): each line is clipped and only the first <paramref name="lines"/> are kept.</summary>
        public static string ClipLines(string s, int lines, int charsPerLine)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var parts = s.Split('\n'); var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length && i < lines; i++) { if (i > 0) sb.Append('\n'); sb.Append(Clip(parts[i].Trim(), charsPerLine)); }
            return sb.ToString();
        }

        /// <summary>Cuts <paramref name="s"/> to at most <paramref name="max"/> characters at a word boundary and adds "…". Short text is returned as it is.</summary>
        public static string Clip(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            int cut = s.LastIndexOf(' ', max - 1);
            if (cut < max / 2) cut = max - 1;       // a very long single word: cut inside it
            return s.Substring(0, cut).TrimEnd(' ', '،', ',', '.', ':', '-') + "…";
        }
    }
}
