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
        private GUIStyle style;

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
        }

        private void OnGUI()
        {
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(10, Screen.height / 130), normal = { textColor = new Color(0.8f, 0.9f, 0.8f, 0.7f) } };
            GUI.Label(new Rect(6, 2, Screen.width, style.fontSize * 2f), $"{shownFps:0} fps · worst {shownWorst:0} ms · mem {(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() >> 20)} MB · target {Application.targetFrameRate} · vsync {QualitySettings.vSyncCount}", style);
        }
    }
}
