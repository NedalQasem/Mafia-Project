using System;
using System.Collections.Generic;
using System.Text;
using MafiaNight.Core;
using UnityEngine;

namespace MafiaNight.Services.Telemetry
{
    /// <summary>
    /// Cost guard for PlayFab. Every PlayFab call goes through <see cref="Allow"/>: it counts the call per API,
    /// warns when a session is unusually chatty, and refuses WRITES beyond a per-minute limit so a bug or a spamming
    /// client cannot burn the monthly meter. Limits and the freeze switch can be changed from the remote config.
    /// </summary>
    public static class PlayFabMeter
    {
        private const string Tag = "PlayFabMeter";

        public static int ReadLimitPerMinute = 90;      // soft: only warns
        public static int WriteLimitPerMinute = 30;     // hard: Allow returns false for writes above it
        public static int SessionWarnAt = 600;          // total calls in one session

        /// <summary>Set by the remote kill-switch: all writes are refused until the flag is cleared.</summary>
        public static bool WritesFrozen;

        public static int Total { get; private set; }
        public static int Reads { get; private set; }
        public static int Writes { get; private set; }
        public static int Blocked { get; private set; }

        public static event Action<string> OnLimitHit;

        private static readonly Dictionary<string, int> perApi = new Dictionary<string, int>();
        private static readonly Queue<float> readWindow = new Queue<float>();
        private static readonly Queue<float> writeWindow = new Queue<float>();
        private static float lastWarn = -100f;
        private static bool sessionWarned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Total = Reads = Writes = Blocked = 0;
            perApi.Clear(); readWindow.Clear(); writeWindow.Clear();
            WritesFrozen = false; sessionWarned = false; lastWarn = -100f;
        }

        /// <summary>Call right before a PlayFab request. Returns false when a write must not be sent.</summary>
        public static bool Allow(string api, bool write)
        {
            float now = Time.realtimeSinceStartup;
            Trim(readWindow, now); Trim(writeWindow, now);

            if (write)
            {
                if (WritesFrozen) return Block(api, "الكتابات موقوفة من الإعدادات عن بعد");
                if (writeWindow.Count >= WriteLimitPerMinute) return Block(api, "تجاوز حد الكتابات بالدقيقة (" + WriteLimitPerMinute + ")");
                writeWindow.Enqueue(now); Writes++;
            }
            else
            {
                readWindow.Enqueue(now); Reads++;
                if (readWindow.Count > ReadLimitPerMinute) Warn(api, "قراءات كثيرة بالدقيقة (" + readWindow.Count + ")");
            }

            Total++;
            perApi.TryGetValue(api, out int n); perApi[api] = n + 1;
            if (!sessionWarned && Total >= SessionWarnAt) { sessionWarned = true; Warn(api, "استدعاءات الجلسة وصلت " + Total); }
            return true;
        }

        private static void Trim(Queue<float> q, float now) { while (q.Count > 0 && now - q.Peek() > 60f) q.Dequeue(); }

        private static bool Block(string api, string reason)
        {
            Blocked++;
            Warn(api, "رُفض: " + reason);
            return false;
        }

        private static void Warn(string api, string reason)
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastWarn < 5f) return;
            lastWarn = now;
            Log.Warning($"{api}: {reason}. {Summary()}", Tag);
            OnLimitHit?.Invoke(reason);
        }

        public static string Summary() => $"total={Total} reads={Reads} writes={Writes} blocked={Blocked}";

        /// <summary>Per-API breakdown for the editor window.</summary>
        public static string Report()
        {
            var sb = new StringBuilder(Summary());
            foreach (var kv in perApi) sb.Append('\n').Append(kv.Key).Append(": ").Append(kv.Value);
            return sb.ToString();
        }
    }
}
