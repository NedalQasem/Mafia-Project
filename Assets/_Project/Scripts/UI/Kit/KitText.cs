namespace MafiaNight.UI.Kit
{
    /// <summary>Helpers for text of unknown length. TMP's own ellipsis cannot be used with <see cref="ArabicText"/> (it cuts the wrong end of reversed text), so long single-line texts are cut here, before shaping.</summary>
    public static class KitText
    {
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
