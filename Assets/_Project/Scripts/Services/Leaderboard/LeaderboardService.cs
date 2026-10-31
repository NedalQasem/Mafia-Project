using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MafiaNight.Core;
using PlayFab;
using UnityEngine;
using UnityEngine.Networking;

namespace MafiaNight.Services.Leaderboard
{
    /// <summary>One line of a board.</summary>
    public sealed class BoardRow
    {
        public string Pf, Name;
        public int Rank, Level, Value;
        public bool Me;
    }

    /// <summary>A board of the day or the week for one category, with this player's place on it.</summary>
    public sealed class BoardResult
    {
        public bool Ok;
        public int Period, Cat;
        public int ResetInSeconds;
        public List<BoardRow> Top = new List<BoardRow>();
        public BoardRow Me;
        public int MePosition;        // 0 = not on the board yet
        public int Tenth;             // value of the tenth place (0 when fewer than ten)
        public int Need;              // how much more this player needs to enter the first ten (0 = already in)
        /// <summary>What the first places of this board get when the period is over (empty: no prizes).</summary>
        public List<BoardPrize> Prizes = new List<BoardPrize>();
    }

    /// <summary>The prize of places From..To of a board: coins, gems, store items, and the package name when it is one.</summary>
    public sealed class BoardPrize { public int From, To, Coins, Gems; public string[] Items = new string[0]; public string Name; }

    public sealed class WallRecord { public string Name; public int Value; }

    /// <summary>The hall of fame: the godfather and the records of all time.</summary>
    public sealed class WallResult
    {
        public bool Ok;
        public string GodfatherName; public int GodfatherPoints;
        public WallRecord Streak, Games, MafiaWins, SheriffWins, Survived, Hours;
    }

    [Serializable] internal sealed class RowDto { public string pf, name; public int rank, level, value; public bool me; }
    [Serializable] internal sealed class MeDto { public string name; public int rank, level, value, position, tenth, need; }
    [Serializable] internal sealed class PrizeDto { public int from, to, coins, gems; public string[] items; public string name; }
    [Serializable] internal sealed class BoardDto { public bool ok; public int cat, resetIn; public List<RowDto> top; public MeDto me; public List<PrizeDto> prizes; }
    [Serializable] internal sealed class GodDto { public string pf, name; public int rank, points; }
    [Serializable] internal sealed class RecDto { public string name; public int value; }
    [Serializable] internal sealed class RecordsDto { public RecDto streak, games, mafiaWins, sheriffWins, survived, hours; }
    [Serializable] internal sealed class WallDto { public bool ok; public GodDto godfather; public RecordsDto records; }

    /// <summary>
    /// Reads the leaderboards from our server. The server writes the numbers itself when it pays a finished match; the game only reads.
    /// Create it with <see cref="Ensure"/>.
    /// </summary>
    public sealed class LeaderboardService : MonoBehaviour
    {
        public static LeaderboardService Instance { get; private set; }

        public static LeaderboardService Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("LeaderboardService");
            DontDestroyOnLoad(go);
            return go.AddComponent<LeaderboardService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private static string Base => (GameConstants.ApiUrl ?? "").TrimEnd('/');
        private static string Ticket => PlayFabSettings.staticPlayer?.ClientSessionTicket;
        public static bool Ready => !string.IsNullOrEmpty(Ticket) && !string.IsNullOrEmpty(Base);

        /// <summary>period: 0 day, 1 week. The callback gets <c>Ok == false</c> when the server cannot be reached.</summary>
        public void LoadBoard(int period, int cat, Action<BoardResult> done)
        {
            StartCoroutine(Post("/v1/leaderboard", "{\"period\":\"" + (period == 0 ? "day" : "week") + "\",\"cat\":" + cat + "}", text =>
            {
                BoardDto d = null; try { d = JsonUtility.FromJson<BoardDto>(text); } catch { }
                var r = new BoardResult { Period = period, Cat = cat };
                if (d != null && d.ok)
                {
                    r.Ok = true; r.ResetInSeconds = d.resetIn;
                    if (d.top != null) foreach (var x in d.top) r.Top.Add(new BoardRow { Pf = x.pf, Name = x.name, Rank = x.rank, Level = x.level, Value = x.value, Me = x.me });
                    if (d.prizes != null) foreach (var p in d.prizes) r.Prizes.Add(new BoardPrize { From = p.from, To = p.to, Coins = p.coins, Gems = p.gems, Items = p.items ?? new string[0], Name = p.name });
                    if (d.me != null) { r.Me = new BoardRow { Name = d.me.name, Rank = d.me.rank, Level = d.me.level, Value = d.me.value, Me = true }; r.MePosition = d.me.position; r.Tenth = d.me.tenth; r.Need = d.me.need; }
                }
                done?.Invoke(r);
            }));
        }

        public void LoadWall(Action<WallResult> done)
        {
            StartCoroutine(Post("/v1/wall", "{}", text =>
            {
                WallDto d = null; try { d = JsonUtility.FromJson<WallDto>(text); } catch { }
                var r = new WallResult();
                if (d != null && d.ok)
                {
                    r.Ok = true;
                    if (d.godfather != null && !string.IsNullOrEmpty(d.godfather.name)) { r.GodfatherName = d.godfather.name; r.GodfatherPoints = d.godfather.points; }
                    if (d.records != null)
                    {
                        r.Streak = Rec(d.records.streak); r.Games = Rec(d.records.games); r.MafiaWins = Rec(d.records.mafiaWins);
                        r.SheriffWins = Rec(d.records.sheriffWins); r.Survived = Rec(d.records.survived); r.Hours = Rec(d.records.hours);
                    }
                }
                done?.Invoke(r);
            }));
        }

        private static WallRecord Rec(RecDto d) => d != null && !string.IsNullOrEmpty(d.name) ? new WallRecord { Name = d.name, Value = d.value } : null;

        private static IEnumerator Post(string path, string json, Action<string> done)
        {
            if (!Ready) { done?.Invoke(null); yield break; }
            using (var req = new UnityWebRequest(Base + path, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Authorization", Ticket);
                req.timeout = 15;
                yield return req.SendWebRequest();
                done?.Invoke(req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null);
            }
        }
    }
}
