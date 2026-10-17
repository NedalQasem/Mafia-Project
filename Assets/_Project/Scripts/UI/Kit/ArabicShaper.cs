using System.Collections.Generic;
using System.Text;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Minimal Arabic text shaper for TextMeshPro (which has no Arabic joining or bidi support).
    /// Input: logical-order string. Output: string made of Arabic Presentation Forms in visual (left-to-right) order,
    /// ready to be assigned to a TMP_Text. The font MUST contain the Arabic Presentation Forms-B block (U+FE70-FEFF).
    /// Numbers and Latin words keep their reading order. Combining marks stay attached to their base letter.
    /// </summary>
    public static class ArabicShaper
    {
        // base letter -> { isolated, final, initial, medial }, 0 = form does not exist
        private static readonly Dictionary<char, char[]> Forms = new Dictionary<char, char[]>
        {
            {'ء', F(0xFE80, 0, 0, 0)},
            {'آ', F(0xFE81, 0xFE82, 0, 0)}, {'أ', F(0xFE83, 0xFE84, 0, 0)},
            {'ؤ', F(0xFE85, 0xFE86, 0, 0)}, {'إ', F(0xFE87, 0xFE88, 0, 0)},
            {'ئ', F(0xFE89, 0xFE8A, 0xFE8B, 0xFE8C)}, {'ا', F(0xFE8D, 0xFE8E, 0, 0)},
            {'ب', F(0xFE8F, 0xFE90, 0xFE91, 0xFE92)}, {'ة', F(0xFE93, 0xFE94, 0, 0)},
            {'ت', F(0xFE95, 0xFE96, 0xFE97, 0xFE98)}, {'ث', F(0xFE99, 0xFE9A, 0xFE9B, 0xFE9C)},
            {'ج', F(0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0)}, {'ح', F(0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4)},
            {'خ', F(0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8)}, {'د', F(0xFEA9, 0xFEAA, 0, 0)},
            {'ذ', F(0xFEAB, 0xFEAC, 0, 0)}, {'ر', F(0xFEAD, 0xFEAE, 0, 0)},
            {'ز', F(0xFEAF, 0xFEB0, 0, 0)}, {'س', F(0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4)},
            {'ش', F(0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8)}, {'ص', F(0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC)},
            {'ض', F(0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0)}, {'ط', F(0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4)},
            {'ظ', F(0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8)}, {'ع', F(0xFEC9, 0xFECA, 0xFECB, 0xFECC)},
            {'غ', F(0xFECD, 0xFECE, 0xFECF, 0xFED0)}, {'ـ', F(0x0640, 0x0640, 0x0640, 0x0640)},
            {'ف', F(0xFED1, 0xFED2, 0xFED3, 0xFED4)}, {'ق', F(0xFED5, 0xFED6, 0xFED7, 0xFED8)},
            {'ك', F(0xFED9, 0xFEDA, 0xFEDB, 0xFEDC)}, {'ل', F(0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0)},
            {'م', F(0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4)}, {'ن', F(0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8)},
            {'ه', F(0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC)}, {'و', F(0xFEED, 0xFEEE, 0, 0)},
            {'ى', F(0xFEEF, 0xFEF0, 0, 0)}, {'ي', F(0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4)},
        };

        // lam + alef variants -> { isolated, final }
        private static readonly Dictionary<char, char[]> LamAlef = new Dictionary<char, char[]>
        {
            {'آ', new[]{'ﻵ', 'ﻶ'}}, {'أ', new[]{'ﻷ', 'ﻸ'}},
            {'إ', new[]{'ﻹ', 'ﻺ'}}, {'ا', new[]{'ﻻ', 'ﻼ'}},
        };

        private static char[] F(int iso, int fin, int ini, int med) =>
            new[] { (char)iso, (char)fin, (char)ini, (char)med };

        private enum Dir { R, L, N }

        /// <summary>Shape a (possibly multi-line) logical string into visual order.</summary>
        public static string Shape(string logical, bool arabicDigits = false)
        {
            if (string.IsNullOrEmpty(logical)) return logical;
            var lines = logical.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = ShapeLine(lines[i], arabicDigits);
            return string.Join("\n", lines);
        }

        public static bool ContainsArabic(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (var c in s) if (IsArabic(c)) return true;
            return false;
        }

        private static string ShapeLine(string s, bool arabicDigits)
        {
            if (!ContainsArabic(s)) return s;
            var shaped = new List<char>(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (arabicDigits && c >= '0' && c <= '9') { shaped.Add((char)('٠' + (c - '0'))); continue; }
                if (!Forms.TryGetValue(c, out var f)) { shaped.Add(c); continue; }

                bool prevJoins = PrevConnects(s, i);

                if (c == 'ل')
                {
                    int j = NextBase(s, i);
                    if (j >= 0 && LamAlef.TryGetValue(s[j], out var lig))
                    {
                        shaped.Add(prevJoins ? lig[1] : lig[0]);
                        for (int k = i + 1; k < j; k++) shaped.Add(s[k]);   // marks between lam and alef
                        i = j;
                        continue;
                    }
                }

                bool nextJoins = f[2] != 0 && NextAccepts(s, i);
                char pick;
                if (prevJoins && nextJoins && f[3] != 0) pick = f[3];
                else if (prevJoins && f[1] != 0) pick = f[1];
                else if (nextJoins && f[2] != 0) pick = f[2];
                else pick = c;   // isolated form = the base letter (many fonts lack the FExx isolated glyphs)
                shaped.Add(pick);
            }
            return Visual(shaped);
        }

        private static bool IsMark(char c) =>
            (c >= 'ً' && c <= 'ٟ') || c == 'ٰ' || (c >= 'ۖ' && c <= 'ۭ');

        private static int NextBase(string s, int i)
        {
            for (int j = i + 1; j < s.Length; j++) if (!IsMark(s[j])) return j;
            return -1;
        }

        private static bool PrevConnects(string s, int i)
        {
            for (int j = i - 1; j >= 0; j--)
            {
                if (IsMark(s[j])) continue;
                return Forms.TryGetValue(s[j], out var pf) && pf[2] != 0;
            }
            return false;
        }

        private static bool NextAccepts(string s, int i)
        {
            int j = NextBase(s, i);
            return j >= 0 && Forms.TryGetValue(s[j], out var nf) && nf[1] != 0;
        }

        private static bool IsArabic(char c) =>
            (c >= '؀' && c <= 'ۿ') || (c >= 'ݐ' && c <= 'ݿ') ||
            (c >= 'ﭐ' && c <= '﷿') || (c >= 'ﹰ' && c <= '﻿');

        private static bool IsNumberChar(char c) =>
            char.IsDigit(c) || (c >= '٠' && c <= '٩') || (c >= '۰' && c <= '۹') ||
            c == '٫' || c == '٬';

        private static Dir Classify(char c)
        {
            if (IsMark(c)) return Dir.R;
            if (IsArabic(c) && !IsNumberChar(c)) return Dir.R;
            if (IsNumberChar(c) || char.IsLetter(c)) return Dir.L;
            return Dir.N;
        }

        private static char Mirror(char c)
        {
            switch (c)
            {
                case '(': return ')'; case ')': return '(';
                case '[': return ']'; case ']': return '[';
                case '{': return '}'; case '}': return '{';
                case '<': return '>'; case '>': return '<';
                case '«': return '»'; case '»': return '«';
                default: return c;
            }
        }

        // Logical (already shaped) -> visual order for an RTL paragraph.
        private static string Visual(List<char> chars)
        {
            int n = chars.Count;
            var dir = new Dir[n];
            for (int i = 0; i < n; i++) dir[i] = Classify(chars[i]);

            // resolve neutrals: between equal strong types take that type, otherwise paragraph direction (R)
            for (int i = 0; i < n; i++)
            {
                if (dir[i] != Dir.N) continue;
                int a = i; while (a > 0 && dir[a - 1] == Dir.N) a--;
                int b = i; while (b < n - 1 && dir[b + 1] == Dir.N) b++;
                Dir left = a > 0 ? dir[a - 1] : Dir.R, right = b < n - 1 ? dir[b + 1] : Dir.R;
                Dir res = left == right ? left : Dir.R;
                for (int k = a; k <= b; k++) dir[k] = res;
                i = b;
            }

            // split into runs of same direction
            var runs = new List<(Dir d, int start, int end)>();
            for (int i = 0; i < n;)
            {
                int j = i; while (j + 1 < n && dir[j + 1] == dir[i]) j++;
                runs.Add((dir[i], i, j));
                i = j + 1;
            }

            var sb = new StringBuilder(n);
            for (int r = runs.Count - 1; r >= 0; r--)   // runs are laid out right-to-left
            {
                var (d, st, en) = runs[r];
                if (d == Dir.L)
                {
                    for (int k = st; k <= en; k++) sb.Append(chars[k]);
                }
                else
                {
                    // reverse by clusters (base letter + its combining marks keep their order)
                    int k = en;
                    while (k >= st)
                    {
                        int baseIdx = k;
                        while (baseIdx > st && IsMark(chars[baseIdx])) baseIdx--;
                        for (int m = baseIdx; m <= k; m++) sb.Append(Mirror(chars[m]));
                        k = baseIdx - 1;
                    }
                }
            }
            return sb.ToString();
        }
    }
}
