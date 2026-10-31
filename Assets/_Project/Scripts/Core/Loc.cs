using System.Collections.Generic;
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

        private static void Load()
        {
            if (table != null) return;
            table = new Dictionary<string, string>();
            var asset = Resources.Load<TextAsset>("Kit/lang_en");
            if (asset == null) return;
            foreach (var raw in asset.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                string ar = line.Substring(0, tab).Trim(), en = line.Substring(tab + 1).Trim();
                if (ar.Length > 0 && en.Length > 0) table[ar] = en.Replace("\\n", "\n");
            }
        }

        /// <summary>Reloads the table (after the file changed in the editor).</summary>
        public static void Reset() { table = null; }

        /// <summary>The English line of an Arabic text; the text itself when it is not translated or the game is in Arabic.</summary>
        public static string T(string ar)
        {
            if (!English || string.IsNullOrEmpty(ar)) return ar;
            Load();
            return table.TryGetValue(ar.Trim(), out var en) ? en : ar;
        }

        /// <summary>An Arabic text with its English twin written in the code.</summary>
        public static string T(string ar, string en) => English && !string.IsNullOrEmpty(en) ? en : ar;

        /// <summary>A text with numbers or names: both formats take the same arguments ({0}, {1}...). Digits come out as the language's own.</summary>
        public static string F(string arFormat, string enFormat, params object[] args) => string.Format(English ? enFormat : arFormat, args);

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
