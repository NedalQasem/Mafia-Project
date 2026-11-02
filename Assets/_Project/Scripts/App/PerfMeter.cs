using MafiaNight.Core;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Development builds only: a thin line in the status-bar area with the frame rate, the slowest frame of the last half second and the memory in use,
    /// so "it feels slow" can be answered with numbers (the build does not carry it).
    /// </summary>
    public sealed class PerfMeter : MonoBehaviour
    {
        private float acc, worst, shownFps, shownWorst;
        private int frames;
        private GUIStyle style, buttonStyle;
        private float copied;
        private string label = "";
        private Rect labelRect;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (!Debug.isDebugBuild || FindAnyObjectByType<PerfMeter>() != null) return;
            var go = new GameObject("PerfMeter");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfMeter>();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            acc += dt; frames++; if (dt > worst) worst = dt;
            if (acc < 0.5f) return;
            shownFps = frames / acc; shownWorst = worst * 1000f;
            acc = 0f; frames = 0; worst = 0f;
            label = $"{shownFps:0} fps · worst {shownWorst:0} ms · mem {(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() >> 20)} MB · target {Application.targetFrameRate} · vsync {QualitySettings.vSyncCount}";
        }

        private void OnGUI()
        {
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(10, Screen.height / 130), normal = { textColor = new Color(0.8f, 0.9f, 0.8f, 0.7f) } };
            // a small button for the testers: sends the journal to the server now and copies it, so it can be pasted to whoever is looking into a problem
            if (GameJournal.Enabled)
            {
                if (buttonStyle == null) buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Max(10, Screen.height / 110) };
                float bw = Screen.width * 0.13f, bh = Mathf.Max(28, Screen.height / 45f);
                if (GUI.Button(new Rect(Screen.width - bw - 6, 2, bw, bh), copied > Time.unscaledTime ? "copied" : "LOG", buttonStyle))
                {
                    MafiaNight.Services.Telemetry.JournalUploader.Flush();
                    GameJournal.Event("Journal", "the tester copied the journal");
                    GUIUtility.systemCopyBuffer = GameJournal.ReadCurrent();
                    copied = Time.unscaledTime + 2f;
                }
            }
            if (labelRect.width == 0f) labelRect = new Rect(6, 2, Screen.width - Screen.width * 0.14f, style.fontSize * 2f);
            GUI.Label(labelRect, label, style);
        }
    }
}
