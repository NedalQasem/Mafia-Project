using MafiaNight.Core;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Android: the game fills the screen but keeps the bottom navigation bar (the gesture pill) visible, so leaving the game takes one ordinary swipe from the bottom.
    /// With the whole system UI hidden (the player-settings "start in fullscreen") the first swipe from the bottom edge only shows the bars and the game keeps hiding them again,
    /// which made it hard to go home. Only the top status bar stays hidden (the swipe from the top still pulls the notifications down).
    /// The activity hides the bars again whenever it regains focus, so this is applied again then and once a second.
    /// </summary>
    public sealed class SystemBars : MonoBehaviour
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const float Every = 1f;
        private float next;
        private bool logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("SystemBars");
            DontDestroyOnLoad(go);
            go.AddComponent<SystemBars>();
        }

        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + Every;
            Apply();
        }

        private void OnApplicationFocus(bool focus) { if (focus) next = 0f; }

        private void Apply()
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    if (activity == null) return;
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                    {
                        try
                        {
                            var window = activity.Call<AndroidJavaObject>("getWindow");
                            int sdk;
                            using (var v = new AndroidJavaClass("android.os.Build$VERSION")) sdk = v.GetStatic<int>("SDK_INT");
                            if (sdk >= 30)
                            {
                                var controller = window.Call<AndroidJavaObject>("getInsetsController");
                                if (controller == null) return;
                                using (var type = new AndroidJavaClass("android.view.WindowInsets$Type"))
                                {
                                    controller.Call("show", type.CallStatic<int>("navigationBars"));
                                    controller.Call("hide", type.CallStatic<int>("statusBars"));
                                }
                                controller.Call("setSystemBarsBehavior", 2);           // BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE: a swipe from the top shows the status bar for a moment
                            }
                            else
                            {
                                var decor = window.Call<AndroidJavaObject>("getDecorView");
                                decor.Call("setSystemUiVisibility", 4 | 256 | 1024);   // fullscreen (no status bar) | layout stable | layout fullscreen: the navigation bar stays
                            }
                        }
                        catch (System.Exception e) { Log.Warning("System bars (UI thread): " + e.Message, "SystemBars"); }
                    }));
                }
                if (!logged) { logged = true; GameJournal.Event("App", "system bars: status bar hidden, navigation bar kept (one swipe from the bottom leaves the game)"); }
            }
            catch (System.Exception e) { Log.Warning("System bars: " + e.Message, "SystemBars"); next = Time.unscaledTime + 10f; }
        }
#endif
    }
}
