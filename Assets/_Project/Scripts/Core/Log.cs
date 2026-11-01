using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace MafiaNight.Core
{
    /// <summary>
    /// Project logger. Info logs are stripped from release builds
    /// (only kept in the Editor and Development builds); warnings and errors are always kept.
    /// </summary>
    public static class Log
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string message, string tag = null) => Debug.Log(Format(message, tag));

        public static void Warning(string message, string tag = null) => Debug.LogWarning(Format(message, tag));

        public static void Error(string message, string tag = null) => Debug.LogError(Format(message, tag));

        private static string Format(string message, string tag) =>
            string.IsNullOrEmpty(tag) ? message : $"[{tag}] {message}";
    }
}
