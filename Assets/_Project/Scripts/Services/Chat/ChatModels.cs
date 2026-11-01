using System;
using System.Collections.Generic;

namespace MafiaNight.Services.Chat
{
    public enum ChatSendResult { Ok, Empty, Slow, Muted, YouBlocked, NotMember, Failed }

    /// <summary>One chat message as the game keeps it.</summary>
    public sealed class ChatMsg
    {
        public int Id;
        public string Channel;          // "public", "dm:<A>:<B>" or "grp:<id>"
        public string FromPf, Name, Text;
        public int Rank;
        public long Ts;                 // unix milliseconds
        public bool System, Mine;
    }

    /// <summary>A direct conversation or a group in the lists.</summary>
    public sealed class ChatConv
    {
        public string Channel;
        public bool IsGroup;
        public string Name, OtherPf;
        public int Rank;
        public bool Online;
        public int Unread;
        public ChatMsg Last;
        public List<KeyValuePair<string, string>> Members = new List<KeyValuePair<string, string>>();   // PlayFab id, name
        public string Owner;
    }

    /// <summary>A room as friends see it (where somebody sits, or what an invitation points to).</summary>
    public sealed class RoomRef
    {
        public string Id, Name, Kind, Code;
        public int Count, Cap;
        public bool Locked, InMatch;
        public bool IsPrivate => Kind == "private";
    }

    /// <summary>Short-lived news from the server: a friend request, an accepted request, an invitation.</summary>
    public sealed class ServerEvent
    {
        public string Type, Pf, Name;
        public int Level;
        public RoomRef Room;
    }

    // ---------------------------------------------------------------- what the server sends (JsonUtility needs plain fields)

    [Serializable] internal sealed class MsgDto { public int id; public string ch, from, name, text; public int rank, sys; public long ts; }
    [Serializable] internal sealed class MemberDto { public string pf, name; }
    [Serializable]
    internal sealed class ConvDto
    {
        public string ch, kind, other, name, owner; public int rank, unread; public bool online;
        public MsgDto last; public List<MemberDto> members;
    }
    [Serializable] internal sealed class RoomDto { public string id, name, kind, code; public int count, cap; public bool locked, inMatch; }
    [Serializable] internal sealed class EventDto { public string type, pf, name; public int level; public RoomDto room; }
    [Serializable] internal sealed class PollDto { public bool ok; public int cursor; public List<MsgDto> messages; public List<EventDto> events; public string error; }
    [Serializable] internal sealed class InboxDto { public bool ok; public List<ConvDto> conversations; }
    [Serializable] internal sealed class SendDto { public bool ok; public int id; public string ch, text, error; public long ts; public int retryMs; public bool dropped; }
    [Serializable] internal sealed class GroupDto { public bool ok; public string id, ch, error; }
    [Serializable] internal sealed class BlockedDto { public bool ok; public List<string> blocked; }
}
