using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TMPro;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Draws the glyphs the game's texts need into the dynamic font atlases while the game is still loading, a few milliseconds per frame.
    /// A dynamic font makes each glyph (and, the first time, the font's whole OpenType layout table) when a text first shows it, so the first visit of every screen froze for a third of a second
    /// or more on a phone (the journal's "first frames" time, and the editor profile: TMP.TryAddCharacter / FontEngine.GetOpenTypeLayoutTables). Done once here, the screens find them ready.
    /// </summary>
    public sealed class FontPrewarm : MonoBehaviour
    {
        private const double BudgetMs = 5.0;       // per frame: the loading screen keeps moving
        private const int Chunk = 8;

        private static FontPrewarm instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("FontPrewarm");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<FontPrewarm>();
        }

        private IEnumerator Start()
        {
            yield return null;
            var theme = KitTheme.Current;
            if (theme == null) yield break;
            var fonts = new List<TMP_FontAsset>();
            foreach (var f in new[] { theme.fontBody, theme.fontLalezar, theme.fontHeading, theme.fontCalligraphy, theme.fontAref })
                if (f != null && f.atlasPopulationMode == AtlasPopulationMode.Dynamic && !fonts.Contains(f)) fonts.Add(f);

            string set = GlyphSet();
            var total = Stopwatch.StartNew();
            foreach (var font in fonts)
            {
                int i = 0;
                while (i < set.Length)
                {
                    var frame = Stopwatch.StartNew();
                    while (i < set.Length && frame.Elapsed.TotalMilliseconds < BudgetMs)
                    {
                        int n = Mathf.Min(Chunk, set.Length - i);
                        try { font.TryAddCharacters(set.Substring(i, n), out _, true); }
                        catch (System.Exception e) { MafiaNight.Core.Log.Warning("Font " + font.name + ": " + e.Message, "Fonts"); i = set.Length; break; }
                        i += n;
                    }
                    yield return null;
                }
            }
            MafiaNight.Core.Log.Info("Fonts ready: " + fonts.Count + " fonts, " + set.Length + " glyphs each, " + total.ElapsedMilliseconds + " ms", "Fonts");
            Destroy(gameObject);
        }

        /// <summary>Latin letters and digits, the punctuation the texts use, the Arabic letters, marks and digits, and the joined forms the shaper writes (U+FE70..FEFC).</summary>
        private static string GlyphSet()
        {
            var sb = new StringBuilder(400);
            for (int c = 0x20; c <= 0x7E; c++) sb.Append((char)c);
            foreach (int c in new[] { 0x00A0, 0x00AB, 0x00BB, 0x00B7, 0x00D7, 0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2026 }) sb.Append((char)c);
            for (int c = 0x0621; c <= 0x063A; c++) sb.Append((char)c);
            for (int c = 0x0640; c <= 0x0652; c++) sb.Append((char)c);
            for (int c = 0x0660; c <= 0x0669; c++) sb.Append((char)c);
            foreach (int c in new[] { 0x060C, 0x061B, 0x061F, 0x067E, 0x0686, 0x0698, 0x06A9, 0x06AF, 0x06CC }) sb.Append((char)c);
            for (int c = 0xFE70; c <= 0xFEFC; c++) sb.Append((char)c);
            return sb.ToString();
        }
    }
}
