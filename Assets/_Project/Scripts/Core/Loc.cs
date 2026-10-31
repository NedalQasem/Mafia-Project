using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MafiaNight.Core
{
    /// <summary>
    /// The player's language (settings → الحساب → اللغة, PlayerPrefs "mafia.lang", Arabic by default) and the English texts of the game.
    /// The Arabic text of the code is the key: <see cref="T(string)"/> returns its English line from Resources/Kit/lang_en.txt (one "arabic TAB english" per line),
    /// or the Arabic text itself when the line is not there (and while the game is in Arabic). Texts with numbers or names use <see cref="F"/>.
    /// </summary>
    public static class Loc
    {
        public static bool English => PlayerPrefs.GetString(GameConstants.PREF_LANGUAGE, "ar") == "en";

        private static Dictionary<string, string> table;
        private static List<KeyValuePair<Regex, string>> patterns;                 // texts with {0}, {1}... in the table: numbers, names, any part of the line
        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        private static void Load()
        {
            if (table != null) return;
            table = new Dictionary<string, string>(); patterns = new List<KeyValuePair<Regex, string>>(); cache.Clear();
            var asset = Resources.Load<TextAsset>("Kit/lang_en");
            if (asset == null) return;
            foreach (var raw in asset.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                string ar = line.Substring(0, tab).Trim(), en = line.Substring(tab + 1).Trim();
                if (ar.Length == 0 || en.Length == 0) continue;
                ar = ar.Replace("\\n", "\n");
                en = en.Replace("\\n", "\n");
                if (ar.Contains("{")) patterns.Add(new KeyValuePair<Regex, string>(ToRegex(ar), en)); else table[ar] = en;
            }
        }

        // "قبل {0} دقيقة" matches "قبل ٥ دقيقة": {0} is a number, {*0} any part of the line; each arrives in the English line as {0} (Arabic digits turned Latin,
        // and a part that is itself a translated text translated)
        private static Regex ToRegex(string key)
        {
            var sb = new System.Text.StringBuilder("^");
            int i = 0;
            foreach (Match m in Regex.Matches(key, @"\{(\*?)(\d)\}"))
            {
                sb.Append(Regex.Escape(key.Substring(i, m.Index - i)));
                // {0} is a number (digits, separators, a clock time); {*0} is any part of the line (a name, a time phrase)
                sb.Append("(?<g" + m.Groups[2].Value + ">" + (m.Groups[1].Value == "*" ? ".+?" : @"[0-9٠-٩][0-9٠-٩٬٫.,:/\-]*") + ")");

                i = m.Index + m.Length;
            }
            sb.Append(Regex.Escape(key.Substring(i))).Append("$");
            return new Regex(sb.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant);
        }

        // a part of a line: its own English line (a whole text or a pattern), or a list of such parts ("٥٠٠ كوينز، ٢ جوهرة")
        private static string TranslatePart(string part, int depth)
        {
            string key = part.Trim();
            if (table.TryGetValue(key, out var en)) return en;
            if (depth < 3)
            {
                var own = FromPatterns(key, depth);
                if (own != null) return own;
                if (key.Contains("، "))
                {
                    var items = key.Split(new[] { "، " }, System.StringSplitOptions.None);
                    for (int i = 0; i < items.Length; i++) items[i] = TranslatePart(items[i], depth + 1);
                    return string.Join(", ", items);
                }
            }
            return part;
        }

        private static string FromPatterns(string ar, int depth = 0)
        {
            if (depth == 0 && cache.TryGetValue(ar, out var done)) return done;
            string result = null;
            foreach (var p in patterns)
            {
                var m = p.Key.Match(ar);
                if (!m.Success) continue;
                result = Regex.Replace(p.Value, @"\{(\d)\}", g =>
                {
                    var grp = m.Groups["g" + g.Groups[1].Value];
                    if (!grp.Success) return g.Value;
                    string part = grp.Value;
                    return LatinDigits(TranslatePart(part, depth + 1));
                });
                break;
            }
            if (depth == 0) { if (cache.Count > 4000) cache.Clear(); cache[ar] = result; }
            return result;
        }

        /// <summary>Reloads the table (after the file changed in the editor).</summary>
        public static void Reset() { table = null; }

        /// <summary>The English line of an Arabic text; the text itself when it is not translated or the game is in Arabic.</summary>
        public static string T(string ar)
        {
            if (!English || string.IsNullOrEmpty(ar)) return ar;
            Load();
            string key = ar.Trim();
            if (table.TryGetValue(key, out var en)) return en;
            return patterns.Count > 0 ? (FromPatterns(key) ?? ar) : ar;
        }

        /// <summary>An Arabic text with its English twin written in the code.</summary>
        public static string T(string ar, string en) => English && !string.IsNullOrEmpty(en) ? en : ar;

        /// <summary>A text with numbers or names: both formats take the same arguments ({0}, {1}...). Digits come out as the language's own.</summary>
        public static string F(string arFormat, string enFormat, params object[] args) => string.Format(English ? enFormat : arFormat, args);

        /// <summary>Arabic texts the screens asked for in English that the table does not have (collected while the game runs in English; for finding what is left to translate).</summary>
        public static readonly HashSet<string> Missing = new HashSet<string>();

        /// <summary>True when the table has this Arabic text.</summary>
        public static bool Has(string ar) { if (string.IsNullOrEmpty(ar)) return false; Load(); return table.ContainsKey(ar.Trim()); }

        /// <summary>Arabic-Indic digits (٠-٩) and their percent, decimal and thousands marks to the Latin ones, for English texts that came with Arabic digits.</summary>
        public static string LatinDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            System.Text.StringBuilder sb = null;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= '٠' && c <= '٩') { if (sb == null) sb = new System.Text.StringBuilder(s); sb[i] = (char)('0' + (c - '٠')); }
                else if (c == '٪' || c == '٫' || c == '٬') { if (sb == null) sb = new System.Text.StringBuilder(s); sb[i] = c == '٪' ? '%' : c == '٫' ? '.' : ','; }
            }
            return sb != null ? sb.ToString() : s;
        }
    }
}
