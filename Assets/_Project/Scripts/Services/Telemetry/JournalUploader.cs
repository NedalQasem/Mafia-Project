using System.Collections;
using System.Text;
using MafiaNight.Core;
using MafiaNight.Services.Chat;
using MafiaNight.Services.Net;
using PlayFab;
using UnityEngine;

namespace MafiaNight.Services.Telemetry
{
    /// <summary>
    /// Sends the test journal (<see cref="GameJournal"/>) to our server every few seconds, once the player is signed in, so it can be read per player on the admin board.
    /// Lines that could not be sent wait for the next try. Only runs where the journal is on (test builds).
    /// </summary>
    public sealed class JournalUploader : MonoBehaviour
    {
        private const float Every = 15f;
        private const int BatchLines = 200;
        private const int BatchChars = 8000;          // a small body goes through any proxy; a big one can be refused (413) and then never leaves
        private const int BatchesPerTurn = 6;

        private static JournalUploader instance;
        private float next = 8f;
        private bool sending, introduced, serverHasNoJournal;
        private string sessionId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (!GameJournal.Enabled || instance != null) return;
            var go = new GameObject("JournalUploader");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<JournalUploader>();
        }

        /// <summary>Sends what is waiting right now (before the app goes away, or when a tester asks).</summary>
        public static void Flush() { if (instance != null && !instance.sending) instance.StartCoroutine(instance.Send()); }

        private static string Base => (GameConstants.ApiUrl ?? "").TrimEnd('/');
        private static string Ticket => PlayFabSettings.staticPlayer?.ClientSessionTicket;

        private void Awake() { sessionId = System.Guid.NewGuid().ToString("N").Substring(0, 12); }

        private void Update()
        {
            if (sending || Time.unscaledTime < next) return;
            next = Time.unscaledTime + Every;
            StartCoroutine(Send());
        }

        private void OnApplicationPause(bool paused) { if (paused) Flush(); }

        private IEnumerator Send()
        {
            if (sending || serverHasNoJournal || string.IsNullOrEmpty(Base) || string.IsNullOrEmpty(Ticket) || !NetworkWatch.Online) yield break;
            if (!introduced)
            {
                introduced = true;
                GameJournal.Event("Session", "signed in as " + PlayFabSettings.staticPlayer?.PlayFabId + " (journal id " + sessionId + ")");
            }
            sending = true;
            for (int round = 0; round < BatchesPerTurn; round++)
            {
                var lines = GameJournal.TakeForUpload(BatchLines, BatchChars);
                if (lines.Count == 0) break;
                var sb = new StringBuilder(lines.Count * 120);
                sb.Append("{\"sid\":\"").Append(sessionId).Append("\",\"build\":\"").Append(JsonText.Esc(Application.version)).Append("\",\"device\":\"").Append(JsonText.Esc(SystemInfo.deviceModel)).Append("\",\"lines\":[");
                for (int i = 0; i < lines.Count; i++) { if (i > 0) sb.Append(','); sb.Append('"').Append(JsonText.Esc(lines[i])).Append('"'); }
                sb.Append("]}");
                var net = new NetResult();
                yield return Http.PostJson(Base + "/v1/diag/upload", sb.ToString(), Ticket, 20, net);
                if (net.Success) continue;
                if (net.Code == 404) { serverHasNoJournal = true; GameJournal.Event("Journal", "the server has no journal endpoint yet: lines stay in the file on the phone only"); break; }
                if (net.Code == 400 || net.Code == 413) { GameJournal.Event("Journal", "the server refused a batch of " + lines.Count + " lines (HTTP " + net.Code + "): dropped, the file on the phone still has them"); continue; }     // sending the same batch again would never work
                GameJournal.Requeue(lines);          // the connection or the server was busy: they go out with the next try
                next = Time.unscaledTime + Every * 2f;
                break;
            }
            sending = false;
        }
    }
}
