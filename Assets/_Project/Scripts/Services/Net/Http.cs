using System;
using System.Collections;
using System.Text;
using MafiaNight.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace MafiaNight.Services.Net
{
    /// <summary>What a request ended with. <see cref="Text"/> is the body also for an error status, so a refused call can still be read.</summary>
    public sealed class NetResult
    {
        public bool Success;
        public long Code;
        public string Text = "";
        public string Error = "";
        public int Attempts;
    }

    /// <summary>
    /// The game's way to call our server: waits for a network instead of failing at once, tries again (with a growing pause) when the failure was the connection and not the server's answer,
    /// and tells <see cref="NetworkWatch"/> how it went. A write is repeated only when the request cannot have reached the server (host not found, could not connect);
    /// a read (<c>read: true</c>) is also repeated after a timeout, a dropped connection or a busy server (502/503/504).
    /// </summary>
    public static class Http
    {
        private const string Tag = "Http";
        private const int MaxAttempts = 3;
        private const float MaxWaitForNetwork = 8f;
        private static readonly float[] Pauses = { 0.7f, 1.8f };

        public static IEnumerator PostJson(string url, string json, string ticket, int timeoutSeconds, NetResult result, bool read = false)
        {
            byte[] body = Encoding.UTF8.GetBytes(json ?? "{}");
            return Send(() =>
            {
                var req = new UnityWebRequest(url, "POST") { uploadHandler = new UploadHandlerRaw(body), downloadHandler = new DownloadHandlerBuffer() };
                req.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(ticket)) req.SetRequestHeader("X-Authorization", ticket);
                return req;
            }, timeoutSeconds, result, read, url);
        }

        public static IEnumerator Get(string url, string ticket, int timeoutSeconds, NetResult result)
        {
            return Send(() =>
            {
                var req = UnityWebRequest.Get(url);
                if (!string.IsNullOrEmpty(ticket)) req.SetRequestHeader("X-Authorization", ticket);
                return req;
            }, timeoutSeconds, result, true, url);
        }

        private static IEnumerator WaitForNetwork()
        {
            float waited = 0f;
            while (!NetworkWatch.Reachable && waited < MaxWaitForNetwork) { waited += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator Send(Func<UnityWebRequest> make, int timeoutSeconds, NetResult result, bool read, string url)
        {
            yield return WaitForNetwork();      // no network at all: give it a moment to come back before spending an attempt

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                bool again;
                using (var req = make())
                {
                    req.timeout = timeoutSeconds;
                    yield return req.SendWebRequest();
                    result.Attempts = attempt;
                    result.Code = req.responseCode;
                    result.Error = req.error ?? "";
                    result.Text = req.downloadHandler != null ? req.downloadHandler.text ?? "" : "";
                    result.Success = req.result == UnityWebRequest.Result.Success;

                    bool transport = req.result == UnityWebRequest.Result.ConnectionError;
                    bool neverReached = transport && NeverReachedServer(result.Error);
                    if (req.responseCode > 0) NetworkWatch.ReportSuccess();
                    else if (neverReached) NetworkWatch.ReportFailure();

                    bool busy = read && (req.responseCode == 502 || req.responseCode == 503 || req.responseCode == 504);
                    again = attempt < MaxAttempts && !result.Success && (busy || (transport && (read || neverReached)));
                    if (again) Log.Warning("Request " + Short(url) + " failed (" + (string.IsNullOrEmpty(result.Error) ? result.Code.ToString() : result.Error) + "), try " + (attempt + 1) + " of " + MaxAttempts, Tag);
                }
                if (!again) yield break;
                yield return new WaitForSecondsRealtime(Pauses[Mathf.Min(attempt - 1, Pauses.Length - 1)]);
                yield return WaitForNetwork();
            }
        }

        /// <summary>The failure came before the server could have seen the request, so repeating even a write is safe.</summary>
        public static bool NeverReachedServer(string error)
        {
            if (string.IsNullOrEmpty(error)) return false;
            string e = error.ToLowerInvariant();
            return e.Contains("resolve") || e.Contains("cannot connect") || e.Contains("failed to connect") || e.Contains("send failure") || e.Contains("broken pipe") || e.Contains("connection refused");
        }

        private static string Short(string url)
        {
            int i = url.IndexOf("/v1/", StringComparison.Ordinal);
            return i >= 0 ? url.Substring(i) : url;
        }
    }
}
