using System;
using System.Collections;
using System.Collections.Generic;
using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.MatchRules.Stage;
using MafiaNight.Services.Net;
using PlayFab;
using UnityEngine;

namespace MafiaNight.Services.Rooms
{
    /// <summary>What the admin board wants in one room of a player: how many bots, which ones, and what they do.</summary>
    public sealed class BotWish
    {
        public int Wanted;
        public bool Sit, Chat, Play;
        public List<BotProfile> Profiles = new List<BotProfile>();
        public List<string> MatchLines = new List<string>();
    }

    /// <summary>
    /// The host of a room tells the rewards service "this is my room, so many people are in it" and hears how many bots the admin board wants there
    /// (the board lists the rooms its hosts report, by the same number the players see). Asking again is harmless, so it goes through the retrying <see cref="Http"/>.
    /// </summary>
    public static class RoomBots
    {
        private static Runner runner;
        private sealed class Runner : MonoBehaviour { }

        [Serializable] private class ReportDto { public string id, name; public int count, capacity, real; public bool mafia, inMatch; }
        [Serializable] private class ProfileDto { public string id, name, style; public int level, rank; public List<string> lines; }
        [Serializable] private class ReplyDto { public bool ok; public int wanted; public bool sit, chat, play; public List<ProfileDto> profiles; public List<string> matchLines; }

        private static string Base => (GameConstants.ApiUrl ?? "").TrimEnd('/');
        private static string Ticket => PlayFabSettings.staticPlayer?.ClientSessionTicket;

        /// <summary>Reports the room and answers with the wish (null when the service did not answer, so the caller keeps what it had).</summary>
        public static void Report(RoomListing room, int real, int members, bool inMatch, Action<BotWish> done)
        {
            if (runner == null)
            {
                var go = new GameObject("RoomBots");
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<Runner>();
            }
            var body = JsonUtility.ToJson(new ReportDto { id = room.Id, name = room.Name ?? "", count = members, capacity = room.Capacity, real = real, mafia = room.Mode == RoomMode.Mafia, inMatch = inMatch });
            runner.StartCoroutine(Post(body, done));
        }

        private static IEnumerator Post(string body, Action<BotWish> done)
        {
            if (string.IsNullOrEmpty(Ticket) || string.IsNullOrEmpty(Base)) { done(null); yield break; }
            var net = new NetResult();
            yield return Http.PostJson(Base + "/v1/bots/room", body, Ticket, 10, net, read: true);
            done(net.Success ? Parse(net.Text) : null);
        }

        /// <summary>Reads the service's answer; null when it is not one.</summary>
        public static BotWish Parse(string json)
        {
            ReplyDto dto = null;
            try { dto = JsonUtility.FromJson<ReplyDto>(json); } catch { /* not json */ }
            if (dto == null || !dto.ok) return null;
            var wish = new BotWish { Wanted = Mathf.Clamp(dto.wanted, 0, 15), Sit = dto.sit, Chat = dto.chat, Play = dto.play };
            if (dto.profiles != null)
                foreach (var p in dto.profiles)
                    if (!string.IsNullOrEmpty(p.id) && !string.IsNullOrEmpty(p.name))
                        wish.Profiles.Add(new BotProfile { Id = p.id, Name = p.name, Level = p.level, Rank = p.rank, Style = p.style ?? "normal", Lines = p.lines ?? new List<string>() });
            if (dto.matchLines != null) wish.MatchLines = dto.matchLines;
            return wish;
        }
    }
}
