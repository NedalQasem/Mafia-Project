using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MafiaNight.Core;
using PlayFab;
using UnityEngine;
using UnityEngine.Networking;

namespace MafiaNight.Services.Config
{
    public enum GameEventKind { Info, Bonus, Challenge }

    /// <summary>One event of the events board, already shaped for this player (his progress, whether he may claim).</summary>
    public sealed class GameEvent
    {
        public string Id, Kicker, Title, Body;
        public GameEventKind Kind;
        public int Multiplier;            // bonus: what the coins of a match are multiplied by
        public int Goal, Progress;        // challenge
        public bool CountsGames;          // challenge: counts played matches instead of won ones
        public int RewardCoins;
        public bool Claimed, Claimable, Secret;
        public int EndsInSeconds, StartsInSeconds;
    }

    public sealed class EventsBoard
    {
        public List<GameEvent> Live = new List<GameEvent>(), Soon = new List<GameEvent>(), Done = new List<GameEvent>();
        public int Count => Live.Count + Soon.Count + Done.Count;
    }

    [Serializable] internal sealed class EventDto
    {
        public string id, kind, kicker, title, body, metric;
        public int multiplier, goal, progress, rewardCoins, endsInSec, startsInSec;
        public bool claimed, claimable, secret;
    }
    [Serializable] internal sealed class EventsDto { public bool ok; public List<EventDto> live, soon, done; }
    [Serializable] internal sealed class ClaimDto { public bool ok; public int coins; public string error; }

    /// <summary>
    /// The events board of the home screen (Server/rewards-api, section "events" of config.json): bonuses, challenges and announcements. The server
    /// counts the challenges itself from the matches it paid and pays the claim; the game only reads and asks to claim.
    /// </summary>
    public static class EventsService
    {
        private static string Base => (GameConstants.ApiUrl ?? "").TrimEnd('/');
        private static string Ticket => PlayFabSettings.staticPlayer?.ClientSessionTicket;

        /// <summary>The callback gets null when the server cannot be reached; an empty board when it has no events.</summary>
        public static IEnumerator Fetch(Action<EventsBoard> done)
        {
            string text = null;
            yield return Post("/v1/events", "{}", (ok, t) => text = ok ? t : null);
            EventsDto d = null;
            if (text != null) { try { d = JsonUtility.FromJson<EventsDto>(text); } catch { } }
            if (d == null || !d.ok) { done?.Invoke(null); yield break; }
            var b = new EventsBoard();
            Fill(b.Live, d.live); Fill(b.Soon, d.soon); Fill(b.Done, d.done);
            done?.Invoke(b);
        }

        /// <summary>Asks the server to pay the reward of a finished challenge. The callback gets the coins paid, or 0 when it was refused or failed.</summary>
        public static IEnumerator Claim(string id, Action<int> done)
        {
            string text = null;
            yield return Post("/v1/events/claim", "{\"id\":\"" + (id ?? "").Replace("\\", "").Replace("\"", "") + "\"}", (ok, t) => text = t);
            ClaimDto d = null;
            if (!string.IsNullOrEmpty(text)) { try { d = JsonUtility.FromJson<ClaimDto>(text); } catch { } }
            done?.Invoke(d != null && d.ok ? d.coins : 0);
        }

        private static void Fill(List<GameEvent> into, List<EventDto> from)
        {
            if (from == null) return;
            foreach (var e in from)
                into.Add(new GameEvent
                {
                    Id = e.id, Kicker = e.kicker, Title = e.title, Body = e.body,
                    Kind = e.kind == "bonus" ? GameEventKind.Bonus : e.kind == "challenge" ? GameEventKind.Challenge : GameEventKind.Info,
                    Multiplier = e.multiplier, Goal = e.goal, Progress = e.progress, CountsGames = e.metric == "games", RewardCoins = e.rewardCoins,
                    Claimed = e.claimed, Claimable = e.claimable, Secret = e.secret, EndsInSeconds = e.endsInSec, StartsInSeconds = e.startsInSec,
                });
        }

        // done(ok, body): body is also given for an error status, so a refused claim can still be read
        private static IEnumerator Post(string path, string json, Action<bool, string> done)
        {
            string ticket = Ticket;
            if (string.IsNullOrEmpty(Base) || string.IsNullOrEmpty(ticket)) { done?.Invoke(false, null); yield break; }
            using (var req = new UnityWebRequest(Base + path, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Authorization", ticket);
                req.timeout = 10;
                yield return req.SendWebRequest();
                done?.Invoke(req.result == UnityWebRequest.Result.Success, req.downloadHandler != null ? req.downloadHandler.text : null);
            }
        }
    }
}
