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
        public string Id, Kicker, Title, Body, Image;          // Image = the id of a picture uploaded on the admin board ("" = none)
        public GameEventKind Kind;
        public int Multiplier;            // bonus: what the coins of a match are multiplied by
        public int Goal, Progress;        // challenge
        public bool CountsGames;          // challenge: counts played matches instead of won ones
        public int RewardCoins;
        public bool Claimed, Claimable, Secret;
        public int EndsInSeconds, StartsInSeconds;
        public bool HasRanking, RankDistributed;
        public string RankMetric;
        public int RankMine, RankValue;          // my place (0 = not ranked yet) and my number
    }

    public sealed class RankRowData { public int Rank, Value; public string Name; public bool Me; }
    public sealed class RankPrizeData { public int From, To, Coins, Gems; public string[] Items = new string[0]; }

    /// <summary>The ranking inside one event: the top ten, my place and the prizes.</summary>
    public sealed class EventRanking
    {
        public string Id, Metric;
        public List<RankRowData> Rows = new List<RankRowData>();
        public int MineRank, MineValue, EndsInSeconds, Winners;
        public bool Distributed;
        public List<RankPrizeData> Prizes = new List<RankPrizeData>();
    }

    public sealed class EventsBoard
    {
        public List<GameEvent> Live = new List<GameEvent>(), Soon = new List<GameEvent>(), Done = new List<GameEvent>();
        public int Count => Live.Count + Soon.Count + Done.Count;
    }

    [Serializable] internal sealed class EventDto
    {
        public string id, kind, kicker, title, body, metric, kickerEn, titleEn, bodyEn, image;
        public int multiplier, goal, progress, rewardCoins, endsInSec, startsInSec;
        public bool claimed, claimable, secret;
        public RankingDto ranking;
    }
    [Serializable] internal sealed class PlaceDto { public int rank, value; }
    [Serializable] internal sealed class RankingDto { public string metric; public int winners; public bool distributed; public PlaceDto mine; }
    [Serializable] internal sealed class RankRowDto { public int rank, value; public string name; public bool me; }
    [Serializable] internal sealed class PrizeDto { public int from, to, coins, gems; public string[] items; }
    [Serializable] internal sealed class RankingViewDto { public bool ok; public string id, metric; public int winners, endsInSec; public bool distributed; public PlaceDto mine; public List<RankRowDto> rows; public List<PrizeDto> prizes; }
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

        /// <summary>The ranking of one event. The callback gets null when it failed.</summary>
        public static IEnumerator Ranking(string id, Action<EventRanking> done)
        {
            string text = null;
            yield return Post("/v1/events/ranking", "{\"id\":\"" + (id ?? "").Replace("\\", "").Replace("\"", "") + "\"}", (ok, t) => text = ok ? t : null);
            RankingViewDto d = null;
            if (text != null) { try { d = JsonUtility.FromJson<RankingViewDto>(text); } catch { } }
            if (d == null || !d.ok) { done?.Invoke(null); yield break; }
            var r = new EventRanking { Id = d.id, Metric = d.metric, Winners = d.winners, EndsInSeconds = d.endsInSec, Distributed = d.distributed, MineRank = d.mine != null ? d.mine.rank : 0, MineValue = d.mine != null ? d.mine.value : 0 };
            if (d.rows != null) foreach (var x in d.rows) r.Rows.Add(new RankRowData { Rank = x.rank, Value = x.value, Name = x.name, Me = x.me });
            if (d.prizes != null) foreach (var p in d.prizes) r.Prizes.Add(new RankPrizeData { From = p.from, To = p.to, Coins = p.coins, Gems = p.gems, Items = p.items ?? new string[0] });
            done?.Invoke(r);
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
                    Id = e.id, Image = e.image, Kicker = Localized.Pick(e.kicker, e.kickerEn), Title = Localized.Pick(e.title, e.titleEn), Body = Localized.Pick(e.body, e.bodyEn),
                    Kind = e.kind == "bonus" ? GameEventKind.Bonus : e.kind == "challenge" ? GameEventKind.Challenge : GameEventKind.Info,
                    Multiplier = e.multiplier, Goal = e.goal, Progress = e.progress, CountsGames = e.metric == "games", RewardCoins = e.rewardCoins,
                    Claimed = e.claimed, Claimable = e.claimable, Secret = e.secret, EndsInSeconds = e.endsInSec, StartsInSeconds = e.startsInSec,
                    HasRanking = e.ranking != null && !string.IsNullOrEmpty(e.ranking.metric), RankMetric = e.ranking != null ? e.ranking.metric : null, RankDistributed = e.ranking != null && e.ranking.distributed,
                    RankMine = e.ranking != null && e.ranking.mine != null ? e.ranking.mine.rank : 0, RankValue = e.ranking != null && e.ranking.mine != null ? e.ranking.mine.value : 0,
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
