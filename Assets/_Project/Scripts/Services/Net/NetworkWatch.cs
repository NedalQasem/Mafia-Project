using System;
using MafiaNight.Core;
using UnityEngine;

namespace MafiaNight.Services.Net
{
    /// <summary>
    /// What the game knows about the phone's connection: the system's own reachability, and whether our requests actually get through
    /// (a Wi-Fi without internet, a captive portal or a dead DNS look "reachable" to the system, but every request fails to find the host).
    /// <see cref="Http"/> reports each outcome here; screens listen to <see cref="Changed"/> to tell the player and to pick up where they left off.
    /// </summary>
    public sealed class NetworkWatch : MonoBehaviour
    {
        private const string Tag = "Network";
        private const int FailuresToCallItOffline = 3;
        private const float QuietSecondsBeforeRetrying = 15f;

        private static NetworkWatch instance;
        private static int failureStreak;
        private static bool online = true;
        private static float lastFailureAt;

        /// <summary>The system says there is a network, and our requests are not failing to connect one after another.</summary>
        public static bool Online => online;
        /// <summary>The system alone: false means no network at all, so waiting is pointless until it changes.</summary>
        public static bool Reachable => Application.internetReachability != NetworkReachability.NotReachable;

        /// <summary>true when the connection came back, false when it was lost.</summary>
        public static event Action<bool> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("NetworkWatch");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<NetworkWatch>();
        }

        /// <summary>A request could not reach the server (no route, host not found, connection refused).</summary>
        public static void ReportFailure() { failureStreak++; lastFailureAt = Time.unscaledTime; Evaluate(); }

        /// <summary>A request got an answer (any HTTP status): the connection works.</summary>
        public static void ReportSuccess() { failureStreak = 0; Evaluate(); }

        private static void Evaluate()
        {
            bool now = Reachable && failureStreak < FailuresToCallItOffline;
            if (now == online) return;
            online = now;
            Log.Info(now ? "Connection is back" : "Connection lost (system reachable=" + Reachable + ", failed requests in a row=" + failureStreak + ")", Tag);
            Changed?.Invoke(now);
        }

        private float nextCheck;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 1f;
            // nothing of ours has failed for a while (so nothing is left to report the recovery): assume it works and let the next request prove it
            if (!online && Reachable && Time.unscaledTime - lastFailureAt > QuietSecondsBeforeRetrying) failureStreak = 0;
            Evaluate();
        }
    }
}
