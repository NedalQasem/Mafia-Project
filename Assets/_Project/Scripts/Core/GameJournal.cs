using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MafiaNight.Core
{
    /// <summary>
    /// The test journal: one plain-text line for everything that happens on a test phone (every log line of the game and of the SDKs, the screens the player visits,
    /// the buttons he taps, the room and voice events, and anything the game finds illogical), so a problem can be understood afterwards without the tester describing it.
    /// Written to a file on the phone (see <see cref="Folder"/>) and, when <see cref="MafiaNight.Services.Telemetry.JournalUploader"/> is on, sent to our server, which keeps it per player.
    /// On in development builds (the test builds) and in a release build only when PlayerPrefs "mafia.diag" is 1. Times are UTC so two phones can be lined up.
    /// Line: <c>HH:mm:ss.fff +secondsSinceStart KIND Tag | text</c>; KIND: I info, W warning, E error, X exception, EV event, TAP tap, AN anomaly (the game saw something that makes no sense).
    /// </summary>
    public static class GameJournal
    {
        private const int KeepFiles = 12;
        private const int MaxLineChars = 500;
        private const int MaxQueued = 4000;

        public static bool Enabled { get; private set; }
        public static string Folder => Path.Combine(Application.persistentDataPath, "journal");
        /// <summary>The file of this run.</summary>
        public static string CurrentFile { get; private set; }

        private static readonly ConcurrentQueue<string> forFile = new ConcurrentQueue<string>();
        private static readonly ConcurrentQueue<string> forUpload = new ConcurrentQueue<string>();
        private static readonly object gate = new object();
        private static string lastKey = "";
        private static int repeats;
        private static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        private static DateTime startedUtc;
        private static StreamWriter writer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            Enabled = Debug.isDebugBuild || PlayerPrefs.GetInt("mafia.diag", 0) == 1;
            if (!Enabled) return;
            startedUtc = DateTime.UtcNow;
            try
            {
                Directory.CreateDirectory(Folder);
                Prune();
                CurrentFile = Path.Combine(Folder, startedUtc.ToString("yyyyMMdd-HHmmss") + ".log");
                writer = new StreamWriter(new FileStream(CurrentFile, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = false };
            }
            catch (Exception e) { Debug.LogWarning("[Journal] cannot open the journal file: " + e.Message); writer = null; }

            Application.logMessageReceivedThreaded += OnUnityLog;
            SceneManager.sceneLoaded += (s, m) => Event("Scene", "loaded " + s.name);
            var go = new GameObject("GameJournal");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<JournalHost>();
            Header();
        }

        private static void Header()
        {
            Add("EV", "Session", "journal started " + startedUtc.ToString("yyyy-MM-dd HH:mm:ss") + " UTC (all times below are UTC)");
            Add("EV", "Session", "app " + Application.version + " · unity " + Application.unityVersion + " · " + (Debug.isDebugBuild ? "development build" : "release build") + " · platform " + Application.platform);
            Add("EV", "Session", "device " + SystemInfo.deviceModel + " · " + SystemInfo.operatingSystem + " · ram " + SystemInfo.systemMemorySize + " MB · gpu " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")");
            Add("EV", "Session", "screen " + Screen.width + "x" + Screen.height + " @" + Screen.currentResolution.refreshRateRatio + " Hz · language " + Application.systemLanguage + " · quality " + QualitySettings.names[QualitySettings.GetQualityLevel()] + " · vsync " + QualitySettings.vSyncCount + " · target fps " + Application.targetFrameRate);
            Add("EV", "Session", "network " + Application.internetReachability + " · voice engine " + PlayerPrefs.GetString("mafia.voice", "eos") + "/" + PlayerPrefs.GetString("mafia.lkmode", "unity") + " · saved language " + PlayerPrefs.GetString("mafia.lang", "ar"));
        }

        // ------------------------------------------------------------------------------------------ writing

        /// <summary>Something that happened: a screen opened, a room was joined, the voice connected...</summary>
        public static void Event(string tag, string text) => Add("EV", tag, text);

        /// <summary>A button the player tapped.</summary>
        public static void Tap(string what) => Add("TAP", "UI", what);

        /// <summary>The game saw something that makes no sense (a state that should not exist). The first thing to read when a tester reports a problem.</summary>
        public static void Anomaly(string tag, string text) => Add("AN", tag, text);

        private static void Add(string kind, string tag, string text)
        {
            if (!Enabled) return;
            if (text == null) text = "";
            if (text.Length > MaxLineChars) text = text.Substring(0, MaxLineChars) + "…";
            text = text.Replace('\r', ' ').Replace('\n', ' ');
            string key = kind + tag + text;
            lock (gate)
            {
                // the same line over and over is written once, then "repeated N times"
                if (key == lastKey && kind != "EV" && kind != "TAP") { repeats++; return; }
                if (repeats > 0) Push(Stamp() + " I Journal | the line above repeated " + repeats + " more times");
                repeats = 0; lastKey = key;
                Push(Stamp() + " " + kind + " " + tag + " | " + text);
            }
        }

        private static void Push(string line)
        {
            forFile.Enqueue(line);
            forUpload.Enqueue(line);
            while (forUpload.Count > MaxQueued) forUpload.TryDequeue(out _);        // not connected for a long time: the oldest lines are dropped from the upload, never from the file
        }

        private static string Stamp()
        {
            double since = clock.Elapsed.TotalSeconds;          // not Time.*: the log callback can come from any thread
            return DateTime.UtcNow.ToString("HH:mm:ss.fff") + " +" + since.ToString("0.0");
        }

        private static void OnUnityLog(string condition, string stack, LogType type)
        {
            if (!Enabled) return;
            string kind = type == LogType.Warning ? "W" : type == LogType.Log ? "I" : type == LogType.Exception ? "X" : "E";
            string tag = "Unity";
            if (condition != null && condition.Length > 2 && condition[0] == '[')
            {
                int end = condition.IndexOf(']');
                if (end > 1 && end < 40) { tag = condition.Substring(1, end - 1); condition = condition.Substring(end + 1).TrimStart(); }
            }
            Add(kind, tag, condition);
            if ((type == LogType.Exception || type == LogType.Error) && !string.IsNullOrEmpty(stack))
            {
                var lines = stack.Split('\n');
                for (int i = 0; i < lines.Length && i < 6; i++) if (lines[i].Length > 0) Add(kind, tag, "   at " + lines[i].Trim());
            }
        }

        // ------------------------------------------------------------------------------------------ for the uploader and the file

        /// <summary>Takes up to <paramref name="max"/> lines waiting to be sent (they leave the queue; give them back with <see cref="Requeue"/> if the send fails).</summary>
        public static List<string> TakeForUpload(int max)
        {
            var list = new List<string>();
            while (list.Count < max && forUpload.TryDequeue(out var line)) list.Add(line);
            return list;
        }

        /// <summary>Puts lines back at the end after a failed send (their order among themselves is kept).</summary>
        public static void Requeue(List<string> lines) { foreach (var l in lines) forUpload.Enqueue(l); }

        /// <summary>The whole file of this run as text (for copying to the clipboard).</summary>
        public static string ReadCurrent()
        {
            try
            {
                Flush();
                if (CurrentFile == null || !File.Exists(CurrentFile)) return "";
                using (var fs = new FileStream(CurrentFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.UTF8)) return sr.ReadToEnd();
            }
            catch (Exception e) { return "cannot read the journal: " + e.Message; }
        }

        internal static void Flush()
        {
            if (writer == null) return;
            try
            {
                while (forFile.TryDequeue(out var line)) writer.WriteLine(line);
                writer.Flush();
            }
            catch (Exception e) { Debug.LogWarning("[Journal] writing failed: " + e.Message); writer = null; }
        }

        private static void Prune()
        {
            try
            {
                var files = new List<string>(Directory.GetFiles(Folder, "*.log"));
                files.Sort(StringComparer.Ordinal);
                for (int i = 0; i < files.Count - (KeepFiles - 1); i++) File.Delete(files[i]);
            }
            catch { /* an old file that cannot be removed is not worth a failure */ }
        }

        private sealed class JournalHost : MonoBehaviour
        {
            private float next;
            private bool hitching;

            private void Update()
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.7f) { if (!hitching) Event("Perf", "a frame took " + (dt * 1000f).ToString("0") + " ms (the screen froze)"); hitching = true; } else hitching = false;
                if (Time.unscaledTime < next) return;
                next = Time.unscaledTime + 2f;
                Flush();
            }

            private void OnApplicationPause(bool paused) { Event("App", paused ? "went to the background" : "came back to the foreground (network " + Application.internetReachability + ")"); if (paused) Flush(); }
            private void OnApplicationQuit() { Event("App", "quit"); Flush(); }
        }
    }
}
