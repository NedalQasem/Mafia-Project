using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MafiaNight.Core;
using PlayFab;
using UnityEngine;
using UnityEngine.Networking;

namespace MafiaNight.Services.Rooms
{
    /// <summary>One official room as the rewards service describes it (a room the game's team keeps open: the Mafia Office room...).</summary>
    public sealed class OfficialRoom
    {
        public string Id = "", Name = "", NameEn = "", Sub = "", SubEn = "", Welcome = "", WelcomeEn = "", LobbyId = "", MicMode = "request";
        public int Capacity = 64, MicSeats = 8, Online, AdminsOnline;
        public bool Pinned = true, ChatEnabled = true, Staff, Enabled = true;
        public List<string> Admins = new List<string>();
        /// <summary>The room bot reports it open: there is a lobby to join.</summary>
        public bool Open => !string.IsNullOrEmpty(LobbyId);
        public string DisplayName => Loc.English && !string.IsNullOrEmpty(NameEn) ? NameEn : Name;
        public string DisplaySub => Loc.English && !string.IsNullOrEmpty(SubEn) ? SubEn : Sub;
        public string DisplayWelcome => Loc.English && !string.IsNullOrEmpty(WelcomeEn) ? WelcomeEn : Welcome;
    }

    /// <summary>One line of an official room's chat history (as the rewards service keeps it).</summary>
    public sealed class OfficialChatLine { public int Id; public string FromPf = "", Name = "", Text = ""; public long Ts; public bool System; }

    /// <summary>The official rooms: the list for the rooms screen, and entering a room's chat (the service lets a player read and write it only while he is inside).</summary>
    public static class OfficialRooms
    {
        private const string Tag = "OfficialRooms";
        private static Runner runner;
        private static readonly List<OfficialRoom> cache = new List<OfficialRoom>();

        private sealed class Runner : MonoBehaviour { }

        [Serializable] private class RoomDto { public string id, name, nameEn, sub, subEn, welcome, welcomeEn, lobbyId, micMode; public int capacity, micSeats, online, adminsOnline; public bool pinned, chatEnabled, staff, enabled; public List<string> admins; }
        [Serializable] private class ListDto { public bool ok; public List<RoomDto> rooms; }
        [Serializable] private class LineDto { public int id; public string from, name, text; public long ts; public int sys; }
        [Serializable] private class EnterDto { public bool ok; public string welcome, welcomeEn; public List<LineDto> history; }

        public static IReadOnlyList<OfficialRoom> Rooms => cache;
        public static OfficialRoom Find(string id) => cache.FirstOrDefault(r => r.Id == id);

        private static string Base => (GameConstants.ApiUrl ?? "").TrimEnd('/');
        private static string Ticket => PlayFabSettings.staticPlayer?.ClientSessionTicket;

        private static void EnsureRunner()
        {
            if (runner != null) return;
            var go = new GameObject("OfficialRooms");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }

        private static IEnumerator Post(string path, string body, Action<string> done)
        {
            if (string.IsNullOrEmpty(Ticket) || string.IsNullOrEmpty(Base)) { done(null); yield break; }
            using (var req = new UnityWebRequest(Base + path, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Authorization", Ticket);
                req.timeout = 15;
                yield return req.SendWebRequest();
                done(req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null);
            }
        }

        /// <summary>Reloads the list (<paramref name="done"/>: true when the service answered).</summary>
        public static void Refresh(Action<bool> done = null)
        {
            EnsureRunner();
            runner.StartCoroutine(Post("/v1/rooms/official", "{}", text =>
            {
                ListDto dto = null;
                if (text != null) { try { dto = JsonUtility.FromJson<ListDto>(text); } catch { /* not json */ } }
                if (dto == null || !dto.ok || dto.rooms == null) { done?.Invoke(false); return; }
                cache.Clear();
                foreach (var r in dto.rooms)
                    cache.Add(new OfficialRoom
                    {
                        Id = r.id ?? "", Name = r.name ?? "", NameEn = r.nameEn ?? "", Sub = r.sub ?? "", SubEn = r.subEn ?? "", Welcome = r.welcome ?? "", WelcomeEn = r.welcomeEn ?? "", LobbyId = r.lobbyId ?? "",
                        MicMode = r.micMode ?? "request", Capacity = r.capacity, MicSeats = r.micSeats, Online = r.online, AdminsOnline = r.adminsOnline, Pinned = r.pinned, ChatEnabled = r.chatEnabled, Staff = r.staff, Enabled = r.enabled,
                        Admins = r.admins ?? new List<string>(),
                    });
                done?.Invoke(true);
            }));
        }

        /// <summary>Enters the chat of a room: the service starts sending its lines (through the chat poll) and answers with the last ones.</summary>
        public static void Enter(string id, Action<bool, List<OfficialChatLine>> done)
        {
            EnsureRunner();
            runner.StartCoroutine(Post("/v1/rooms/official/enter", "{\"id\":\"" + id + "\"}", text =>
            {
                EnterDto dto = null;
                if (text != null) { try { dto = JsonUtility.FromJson<EnterDto>(text); } catch { /* not json */ } }
                if (dto == null || !dto.ok) { done(false, new List<OfficialChatLine>()); return; }
                var list = new List<OfficialChatLine>();
                if (dto.history != null) foreach (var l in dto.history) list.Add(new OfficialChatLine { Id = l.id, FromPf = l.from ?? "", Name = l.name ?? "", Text = l.text ?? "", Ts = l.ts, System = l.sys == 1 });
                list.Sort((a, b) => a.Id.CompareTo(b.Id));
                done(true, list);
            }));
        }

        public static void Leave()
        {
            EnsureRunner();
            runner.StartCoroutine(Post("/v1/rooms/official/leave", "{}", _ => { }));
        }
    }
}
