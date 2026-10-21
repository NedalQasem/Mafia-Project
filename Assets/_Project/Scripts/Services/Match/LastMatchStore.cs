using System;
using MafiaNight.Data.Rooms;
using UnityEngine;

namespace MafiaNight.Services.Match
{
    /// <summary>The room of the match this player is playing, remembered on the device so he can walk back into it after a crash or a lost connection.</summary>
    public static class LastMatchStore
    {
        private const string Key = "mafia.lastMatch";
        /// <summary>After this long the match is surely over; the memory is dropped.</summary>
        public const int MaxAgeMinutes = 25;

        [Serializable] private sealed class Saved { public string id, name, code; public int kind; public long at; }

        public sealed class Entry { public string RoomId, RoomName, Code; public RoomKind Kind; }

        public static void Save(RoomListing room)
        {
            if (room == null || string.IsNullOrEmpty(room.Id)) return;
            try
            {
                var s = new Saved { id = room.Id, name = room.Name ?? "", code = room.Code ?? "", kind = (int)room.Kind, at = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(s));
                PlayerPrefs.Save();
            }
            catch (Exception) { /* storage not available: only the convenience is lost */ }
        }

        /// <summary>The remembered room, or null when there is none or it is too old.</summary>
        public static Entry Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                if (string.IsNullOrEmpty(json)) return null;
                var s = JsonUtility.FromJson<Saved>(json);
                if (s == null || string.IsNullOrEmpty(s.id)) return null;
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - s.at > MaxAgeMinutes * 60) { Clear(); return null; }
                return new Entry { RoomId = s.id, RoomName = s.name, Code = s.code, Kind = (RoomKind)s.kind };
            }
            catch (Exception) { return null; }
        }

        public static void Clear()
        {
            try { if (PlayerPrefs.HasKey(Key)) { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); } }
            catch (Exception) { }
        }
    }
}
