using System.Text;

namespace MafiaNight.Services
{
    /// <summary>Small helpers to write the JSON the server expects (JsonUtility cannot write free-form bodies).</summary>
    internal static class JsonText
    {
        public static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append(' '); break;
                    default: if (ch < ' ') sb.Append(' '); else sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }
    }
}
