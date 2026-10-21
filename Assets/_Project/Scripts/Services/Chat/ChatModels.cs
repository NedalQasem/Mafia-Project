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

    // ---------------------------------------------------------------- what the server sends (JsonUtility needs plain fields)

    [Serializable] internal sealed class MsgDto { public int id; public string ch, from, name, text; public int rank, sys; public long ts; }
    [Serializable] internal sealed class MemberDto { public string pf, name; }
    [Serializable]
    internal sealed class ConvDto
    {
        public string ch, kind, other, name, owner; public int rank, unread; public bool online;
        public MsgDto last; public List<MemberDto> members;
    }
    [Serializable] internal sealed class PollDto { public bool ok; public int cursor; public List<MsgDto> messages; public string error; }
    [Serializable] internal sealed class InboxDto { public bool ok; public List<ConvDto> conversations; }
    [Serializable] internal sealed class SendDto { public bool ok; public int id; public string ch, text, error; public long ts; public int retryMs; public bool dropped; }
    [Serializable] internal sealed class GroupDto { public bool ok; public string id, ch, error; }
    [Serializable] internal sealed class BlockedDto { public bool ok; public List<string> blocked; }
}
