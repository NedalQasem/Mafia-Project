using System;
using System.Collections.Generic;
using UnityEngine;

namespace MafiaNight.Data.Rooms
{
    public enum RoomKind { Public, Private, Vip }

    public enum RoomMode { Hangout, Mafia }

    /// <summary>What the owner chooses on the create-room screen.</summary>
    [Serializable]
    public class RoomSettings
    {
        public string Name = "";
        public RoomKind Kind = RoomKind.Private;
        public RoomMode Mode = RoomMode.Hangout;
        public int Capacity = 12;
        public bool ChatEnabled = true;
        public bool MicMutedOnJoin;
        /// <summary>Catalog id of the room background (store item), empty = default.</summary>
        public string BackgroundId = "";
    }

    /// <summary>A room as other players see it (one EOS lobby).</summary>
    [Serializable]
    public class RoomListing
    {
        public string Id;                 // EOS lobby id
        public string Name;
        public string OwnerPuid;          // current host (can change when the host leaves)
        public string OwnerPlayFabId;     // the player who created the room
        public string OwnerName;
        public int OwnerRank;             // 0-based rank index
        public int OwnerLevel;
        public int Count;
        public int Capacity;
        public RoomKind Kind;
        public RoomMode Mode;
        public string Code = "";          // private rooms
        public bool InMatch;              // a mafia round is running
        public bool ChatEnabled = true;
        public string BackgroundId = "";
        public long CreatedUnix;

        public bool IsFull => Count >= Capacity;
        public bool IsNew => CreatedUnix > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - CreatedUnix < 600;
    }

    /// <summary>One player sitting in a room.</summary>
    [Serializable]
    public class RoomMember
    {
        public string Puid;
        public string PlayFabId;
        public string Name;
        public int Rank;
        public int Level;
        public bool IsHost;
        public bool IsMe;
    }

    /// <summary>The rules from Docs/05-rooms-design.md in one place.</summary>
    public static class RoomRules
    {
        public const int MinSeats = 4;
        public const int MaxSeats = 16;            // EOS voice rooms hold exactly 16 (tested)
        public const int MaxNameLength = 24;
        public const int CodeLength = 4;
        public const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // no 0/O/1/I
        public const string BucketId = "mafia_ar_v1";
        public const string Language = "ar";

        /// <summary>Level needed to open a public room. Editor and development builds lower it so one account can test.</summary>
        public static int PublicMinLevel => Application.isEditor || Debug.isDebugBuild ? 1 : 5;

        public static string NewCode()
        {
            var chars = new char[CodeLength];
            for (int i = 0; i < chars.Length; i++) chars[i] = CodeAlphabet[UnityEngine.Random.Range(0, CodeAlphabet.Length)];
            return new string(chars);
        }

        public static string NormalizeCode(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in input.ToUpperInvariant()) if (CodeAlphabet.IndexOf(c) >= 0) sb.Append(c);
            return sb.ToString();
        }
    }
}
