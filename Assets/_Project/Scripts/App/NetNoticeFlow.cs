using MafiaNight.Services.Net;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Tells the player when the connection is lost and when it is back (<see cref="NetworkWatch"/>), so a screen that waits or a room that drops is not a mystery.
    /// A short loss says nothing (the requests retry by themselves); only one that lasts a few seconds shows a note. Lives across scenes.
    /// </summary>
    public sealed class NetNoticeFlow : MonoBehaviour
    {
        private const float QuietSeconds = 3f;          // a loss shorter than this is not worth a note
        private const float AgainAfter = 20f;           // and the same note is not repeated more often than this

        public static NetNoticeFlow Instance { get; private set; }

        private float offlineSince = -1f, lastNote = -100f;
        private bool noted;

        public static NetNoticeFlow Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("NetNoticeFlow");
            DontDestroyOnLoad(go);
            return go.AddComponent<NetNoticeFlow>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            NetworkWatch.Changed += OnChanged;
            if (!NetworkWatch.Online) offlineSince = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            NetworkWatch.Changed -= OnChanged;
            if (Instance == this) Instance = null;
        }

        private void OnChanged(bool online)
        {
            if (!online) { offlineSince = Time.unscaledTime; return; }
            offlineSince = -1f;
            if (noted) { noted = false; Say("رجع الاتصال بالإنترنت."); }
        }

        private void Update()
        {
            if (offlineSince < 0f || noted) return;
            if (Time.unscaledTime - offlineSince < QuietSeconds || Time.unscaledTime - lastNote < AgainAfter) return;
            noted = true; lastNote = Time.unscaledTime;
            Say("ما في اتصال بالإنترنت. عم نحاول نرجّع الاتصال…");
        }

        private static void Say(string text)
        {
            var toast = FindAnyObjectByType<KitToast>(FindObjectsInactive.Include);
            if (toast != null) toast.Show(text);
        }
    }
}
