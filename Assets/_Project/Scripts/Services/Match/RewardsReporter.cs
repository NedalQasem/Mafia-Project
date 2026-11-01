using System;
using System.Collections;
using System.Text;
using MafiaNight.Core;
using MafiaNight.MatchRules.Net;
using PlayFab;
using UnityEngine;
using UnityEngine.Networking;

namespace MafiaNight.Services.Match
{
    public enum RewardStatus { None, Sending, Waiting, Granted, Capped, Rejected, Failed }

    /// <summary>What the rewards server paid this player for a match.</summary>
    public sealed class RewardResult
    {
        public RewardStatus Status;
        public int Xp, Coins, RankPoints;
        public bool Won;
    }

    /// <summary>
    /// Tells our rewards server how a match ended (see Server/rewards-api). The server decides and pays; the game only asks.
    /// The same request is repeated every few seconds while the server waits for more than half of the players to report.
    /// </summary>
    public static class RewardsReporter
    {
        private const string Tag = "Rewards";
        private const int MaxAsks = 14;               // ~ 70 s of waiting for the other players' reports
        private const float AskEvery = 5f;

        [Serializable] private class Answer { public bool ok, granted, waiting, capped, rejected, won; public int xp, coins, rankPoints; public string error; }

        public static IEnumerator Run(MatchReport report, Action<RewardResult> progress)
        {
            string baseUrl = GameConstants.ApiUrl;
            if (string.IsNullOrEmpty(baseUrl) || report == null) yield break;
            var body = Encoding.UTF8.GetBytes(ToJson(report));
            int failures = 0;
            for (int ask = 0; ask < MaxAsks; ask++)
            {
                string ticket = PlayFabSettings.staticPlayer?.ClientSessionTicket;
                if (string.IsNullOrEmpty(ticket)) { Log.Warning("No PlayFab session: results not reported", Tag); progress?.Invoke(new RewardResult { Status = RewardStatus.Failed }); yield break; }

                using (var req = new UnityWebRequest(baseUrl.TrimEnd('/') + "/v1/match-result", "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(body);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("X-Authorization", ticket);
                    req.timeout = 15;
                    progress?.Invoke(new RewardResult { Status = RewardStatus.Sending });
                    yield return req.SendWebRequest();

                    Answer a = null;
                    try { a = JsonUtility.FromJson<Answer>(req.downloadHandler.text); } catch { /* not json */ }
                    if (req.result == UnityWebRequest.Result.Success && a != null && a.ok)
                    {
                        if (a.granted) { progress?.Invoke(new RewardResult { Status = RewardStatus.Granted, Xp = a.xp, Coins = a.coins, RankPoints = a.rankPoints, Won = a.won }); yield break; }
                        if (a.capped) { progress?.Invoke(new RewardResult { Status = RewardStatus.Capped }); yield break; }
                        if (a.rejected) { progress?.Invoke(new RewardResult { Status = RewardStatus.Rejected }); yield break; }
                        progress?.Invoke(new RewardResult { Status = RewardStatus.Waiting });
                    }
                    else if (req.responseCode == 400 || req.responseCode == 401 || req.responseCode == 403 || req.responseCode == 410)
                    {
                        Log.Warning("Rewards server refused the report: " + req.responseCode + " " + (a != null ? a.error : ""), Tag);
                        progress?.Invoke(new RewardResult { Status = RewardStatus.Rejected });
                        yield break;
                    }
                    else if (++failures >= 4)
                    {
                        Log.Warning("Rewards server unreachable: " + req.error, Tag);
                        progress?.Invoke(new RewardResult { Status = RewardStatus.Failed });
                        yield break;
                    }
                }
                yield return new WaitForSecondsRealtime(AskEvery);
            }
            progress?.Invoke(new RewardResult { Status = RewardStatus.Failed });
        }

        private static string ToJson(MatchReport r)
        {
            var sb = new StringBuilder("{");
            sb.Append("\"matchId\":\"").Append(Esc(r.MatchId)).Append("\",\"players\":[");
            for (int i = 0; i < r.PlayFabIds.Count; i++) { if (i > 0) sb.Append(','); sb.Append('"').Append(Esc(r.PlayFabIds[i])).Append('"'); }
            sb.Append("],\"winner\":\"").Append(Esc(r.Winner)).Append("\",\"myRole\":\"").Append(Esc(r.MyRole));
            sb.Append("\",\"survived\":").Append(r.Survived ? "true" : "false");
            sb.Append(",\"rounds\":").Append(r.Rounds).Append(",\"durationSec\":").Append(r.DurationSec).Append('}');
            return sb.ToString();
        }

        private static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
