using System;
using UnityEngine;

namespace MafiaNight.Core
{
    public enum DeviceTier { Low = 0, Mid = 1, High = 2 }

    /// <summary>
    /// One answer to "how much can this phone do?", used by every feature that costs something (animations, effects, caches, frame rate), so the game never asks a weak phone for what a strong one gives
    /// and a strong phone is never held back. The tier comes from the phone's memory, processor and graphics chip; the player can set it by hand (settings: graphics quality) or turn the battery saver on;
    /// and <see cref="DevicePerfGovernor"/> steps it down by itself if the phone cannot keep up. See Docs/13-performance-rules.md.
    /// </summary>
    public static class DevicePerf
    {
        public const string QualityKey = "mafia.quality";          // 0 low, 1 mid, 2 high; absent = automatic
        public const string SaverKey = "mafia.batterysaver";

        private static DeviceTier? detected;
        private static int governorSteps;                            // how many tiers the governor took away this session

        /// <summary>Raised when the tier, the saver or the reduce-motion choice changes: features re-read what they depend on.</summary>
        public static event Action Changed;

        /// <summary>What the phone's hardware allows.</summary>
        public static DeviceTier Detected
        {
            get
            {
                if (detected == null) detected = TierOf(SystemInfo.systemMemorySize, SystemInfo.processorCount, SystemInfo.graphicsDeviceName);
                return detected.Value;
            }
        }

        /// <summary>The player's own choice in settings, or null when he never chose (then the hardware decides).</summary>
        public static DeviceTier? Chosen
        {
            get { int q = PlayerPrefs.GetInt(QualityKey, -1); return q < 0 || q > 2 ? (DeviceTier?)null : (DeviceTier)q; }
            set { if (value == null) PlayerPrefs.DeleteKey(QualityKey); else PlayerPrefs.SetInt(QualityKey, (int)value.Value); PlayerPrefs.Save(); governorSteps = 0; Changed?.Invoke(); }
        }

        /// <summary>The tier in force now: the choice (or the hardware), minus what the governor took away.</summary>
        public static DeviceTier Tier => (DeviceTier)Mathf.Max(0, (int)(Chosen ?? Detected) - governorSteps);

        public static bool BatterySaver
        {
            get => PlayerPrefs.GetInt(SaverKey, 0) == 1;
            set { PlayerPrefs.SetInt(SaverKey, value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static bool ReduceMotion => PlayerPrefs.GetInt("mafia.reducemotion", 0) == 1;

        /// <summary>Hanging tags, swaying frames and other things that only decorate: off on a weak phone, with the saver, or when the player asked for less motion.</summary>
        public static bool DecorativeMotion => Tier != DeviceTier.Low && !BatterySaver && !ReduceMotion;

        /// <summary>How many animated avatar frames / effects may run at the same time (the rest show still).</summary>
        public static int MaxAnimatedFrames => BatterySaver || ReduceMotion ? 0 : Tier == DeviceTier.High ? 12 : Tier == DeviceTier.Mid ? 6 : 0;

        /// <summary>The rate animated frames and effects update at (a flame does not need 60).</summary>
        public static int AnimationFps => Tier == DeviceTier.High ? 30 : Tier == DeviceTier.Mid ? 20 : 12;

        /// <summary>The frame rate the game asks the phone for: the screen's best (up to 120) on a strong phone, 60 on the others, 30 with the battery saver.</summary>
        public static int TargetFps
        {
            get
            {
                if (BatterySaver) return 30;
                int best = 0;
                try { foreach (var r in Screen.resolutions) { int hz = Mathf.RoundToInt((float)r.refreshRateRatio.value); if (hz > best) best = hz; } } catch { /* a hint only */ }
                if (Tier == DeviceTier.High && best > 60) return Mathf.Min(best, 120);
                return 60;
            }
        }

        /// <summary>The memory the painted-sprite cache may hold, in megabytes.</summary>
        public static int SpriteCacheMb => Tier == DeviceTier.High ? 64 : Tier == DeviceTier.Mid ? 40 : 24;

        /// <summary>The default screen-change effect when the player never chose one: the burning page where it is free, the fade where it costs, none on a weak phone.</summary>
        public static string DefaultTransition => Tier == DeviceTier.High ? "fire" : Tier == DeviceTier.Mid ? "simple" : "off";

        /// <summary>Called by the governor: the phone cannot keep up, so take one tier away. Returns false when it is already at the bottom.</summary>
        public static bool StepDown(string reason)
        {
            if (Tier == DeviceTier.Low) return false;
            governorSteps++;
            GameJournal.Event("Perf", "tier lowered to " + Tier + ": " + reason);
            Changed?.Invoke();
            return true;
        }

        public static string Describe() =>
            "tier " + Tier + " (hardware " + Detected + (Chosen != null ? ", player chose " + Chosen : "") + (governorSteps > 0 ? ", governor -" + governorSteps : "") + (BatterySaver ? ", battery saver" : "") + ") · " +
            SystemInfo.systemMemorySize + " MB ram · " + SystemInfo.processorCount + " cores · " + SystemInfo.graphicsDeviceName + " · target " + TargetFps + " fps";

        // ---- the hardware, by three yardsticks; the tier is the weakest of them (pure functions: the tests feed them the numbers of real phones)

        public static DeviceTier TierOf(int ramMb, int cores, string gpuName) =>
            (DeviceTier)Mathf.Min((int)ByMemory(ramMb), Mathf.Min((int)ByCpu(cores), (int)ByGpu(gpuName)));

        public static DeviceTier ByMemory(int mb) => mb >= 7000 ? DeviceTier.High : mb >= 3500 ? DeviceTier.Mid : DeviceTier.Low;

        public static DeviceTier ByCpu(int cores) => cores >= 8 ? DeviceTier.High : cores >= 6 ? DeviceTier.Mid : DeviceTier.Low;

        public static DeviceTier ByGpu(string gpuName)
        {
            string g = (gpuName ?? "").ToLowerInvariant();
            if (g.Contains("adreno")) { int n = FirstNumber(g); return n >= 730 ? DeviceTier.High : n >= 610 ? DeviceTier.Mid : DeviceTier.Low; }          // 7xx flagship (a Galaxy S24 has the 750), 610-6xx mid, older low
            if (g.Contains("mali-g")) { int n = FirstNumber(g); return n >= 710 ? DeviceTier.High : n >= 51 ? DeviceTier.Mid : DeviceTier.Low; }            // G710 and up high, G51-G78 mid (a TECNO has the G57), G31 low
            if (g.Contains("mali") || g.Contains("powervr") || g.Contains("videocore") || g.Contains("tegra")) return DeviceTier.Low;                  // the older families
            return DeviceTier.High;                                                                                                                    // Xclipse, Apple, a computer, the editor: strong
        }

        private static int FirstNumber(string s)
        {
            int i = 0; while (i < s.Length && !char.IsDigit(s[i])) i++;
            int n = 0, digits = 0; while (i < s.Length && char.IsDigit(s[i]) && digits < 4) { n = n * 10 + (s[i] - '0'); i++; digits++; }
            return n;
        }
    }

    /// <summary>
    /// Watches the real cost of the frames (the CPU and GPU time the phone reports, not the wait for the screen) and takes a tier away when they stay over budget,
    /// so a phone that is weaker than its numbers say (hot, busy, throttled) falls to the lighter way by itself instead of stuttering. It never raises the tier by itself: no flapping.
    /// </summary>
    public sealed class DevicePerfGovernor : MonoBehaviour
    {
        private const float WarmupSeconds = 20f;         // loading, login and the fonts are heavy and not judged
        private const float QuietAfterSceneSeconds = 3f; // nor is the moment a screen is built
        private const int WindowsToStepDown = 3;         // three windows in a row over budget (about ten seconds)
        private const float WindowSeconds = 3.5f;

        private readonly FrameTiming[] timings = new FrameTiming[1];
        private readonly System.Collections.Generic.List<double> window = new System.Collections.Generic.List<double>();
        private float windowStart, nextSample, quietUntil;
        private int overWindows;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            GameJournal.Event("Perf", DevicePerf.Describe());
            if (Application.isEditor) return;                      // the editor's frame times say nothing about a phone
            var go = new GameObject("DevicePerfGovernor");
            DontDestroyOnLoad(go);
            go.AddComponent<DevicePerfGovernor>();
        }

        private void Awake() { UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => quietUntil = Time.unscaledTime + QuietAfterSceneSeconds; }

        /// <summary>True when the typical (median) frame of the window cost more than the budget: a few slow frames do not count, only a steady slowness does.</summary>
        public static bool IsOverBudget(System.Collections.Generic.IList<double> frameMs, double budgetMs)
        {
            if (frameMs == null || frameMs.Count < 3) return false;
            var sorted = new System.Collections.Generic.List<double>(frameMs); sorted.Sort();
            return sorted[sorted.Count / 2] > budgetMs;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now < WarmupSeconds || now < quietUntil || DevicePerf.Tier == DeviceTier.Low) { window.Clear(); windowStart = now; return; }
            if (Time.unscaledDeltaTime > 0.1f) { window.Clear(); windowStart = now; return; }       // a hitch (the app came back, a screen was built): start the window again
            if (now < nextSample) return;
            nextSample = now + 0.25f;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) < 1) return;
            double work = Math.Max(timings[0].cpuFrameTime, timings[0].gpuFrameTime);          // what the frame cost, not how long it waited for the screen
            if (work <= 0.0) return;                                                           // the phone does not report it: nothing to judge
            window.Add(work);
            if (now - windowStart < WindowSeconds) return;
            double budget = 1000.0 / Mathf.Min(60, DevicePerf.TargetFps);                       // a phone that cannot hold 60 is judged slow even when it asks for 120
            bool over = IsOverBudget(window, budget);
            double med = 0; if (window.Count > 0) { var sorted = new System.Collections.Generic.List<double>(window); sorted.Sort(); med = sorted[sorted.Count / 2]; }
            window.Clear(); windowStart = now;
            overWindows = over ? overWindows + 1 : 0;
            if (overWindows >= WindowsToStepDown)
            {
                overWindows = 0;
                DevicePerf.StepDown("the typical frame costs " + med.ToString("0.0") + " ms against a budget of " + budget.ToString("0.0") + " ms for about ten seconds");
            }
        }
    }
}
