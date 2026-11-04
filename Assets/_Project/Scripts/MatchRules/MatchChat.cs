namespace MafiaNight.MatchRules
{
    /// <summary>Where a written line goes: the room (before and after the match, and among the spectators), the table (the living players by day), the mafia (secret, at night).</summary>
    public enum ChatChannel { Room, Table, Mafia }

    /// <summary>What one player's chat box can do right now.</summary>
    public sealed class ChatPlan
    {
        public ChatChannel Channel = ChatChannel.Room;
        /// <summary>He cannot write (the box shows a brass lock and <see cref="LockedText"/>).</summary>
        public bool Locked;
        /// <summary>The lock is the silencer's tape, not the night.</summary>
        public bool Tape;
        public string LockedText = "";
        /// <summary>The name on the tab of the box ("الطاولة") and the hint inside the empty field.</summary>
        public string Tab = "", Prompt = "";
        /// <summary>A secret channel: its tab is red.</summary>
        public bool Secret;
    }

    /// <summary>
    /// The written chat of a match, as pure logic, following the voice rules: by day the living players write at the table, at night only the mafia write (to each other,
    /// secretly) and the rest of the town is locked out, the silenced player cannot write, the spectators write among themselves. Whoever receives a line checks it too
    /// (<see cref="Accept"/>), so a spectator's line never shows on a living player's screen.
    /// </summary>
    public static class MatchChat
    {
        public static string Code(ChatChannel c) => c == ChatChannel.Table ? "t" : c == ChatChannel.Mafia ? "m" : "r";
        public static ChatChannel FromCode(string s) => s == "t" ? ChatChannel.Table : s == "m" ? ChatChannel.Mafia : ChatChannel.Room;

        /// <summary>Before the first night and after the last day everybody is in the room's chat.</summary>
        private static bool Active(Phase p) => p != Phase.Lobby && p != Phase.RoleReveal && p != Phase.GameOver;

        /// <param name="alive">still in the match; the Avenger counts as alive during his own moment</param>
        public static ChatPlan For(Phase phase, Role? role, bool alive, bool silenced)
        {
            if (!Active(phase)) return new ChatPlan { Channel = ChatChannel.Room, Tab = "الغرفة", Prompt = "اكتب رسالة…" };
            if (!alive) return new ChatPlan { Channel = ChatChannel.Room, Tab = "المتفرجين", Prompt = "اكتب للمتفرجين…" };
            if (phase == Phase.Night)
            {
                if (role.HasValue && role.Value.IsMafiaTeam()) return new ChatPlan { Channel = ChatChannel.Mafia, Tab = "المافيا · سري", Prompt = "اكتب للمافيا فقط…", Secret = true };
                return new ChatPlan { Channel = ChatChannel.Table, Locked = true, LockedText = "الدردشة مقفلة بالليل", Tab = "الطاولة" };
            }
            if (silenced) return new ChatPlan { Channel = ChatChannel.Table, Locked = true, Tape = true, LockedText = "انسكتّ اليوم: كاتم الصوت سكّتك لحد الفجر", Tab = "الطاولة" };
            return new ChatPlan { Channel = ChatChannel.Table, Tab = "الطاولة", Prompt = "اكتب رسالة…" };
        }

        /// <summary>Does a receiver keep a line that arrived on <paramref name="channel"/>?</summary>
        /// <param name="receiverAlive">the receiver is in the match (the Avenger counts as alive during his moment)</param>
        /// <param name="senderAlive">the sender is in the match (same rule); a stranger who is not at the table is not</param>
        public static bool Accept(ChatChannel channel, Phase phase, bool receiverAlive, bool receiverIsMafia, bool senderAlive, bool senderIsMate)
        {
            bool active = Active(phase);
            switch (channel)
            {
                case ChatChannel.Room: return !active || (!receiverAlive && !senderAlive);     // a living player never reads the spectators
                case ChatChannel.Table: return active && phase != Phase.Night && senderAlive;
                case ChatChannel.Mafia: return active && phase == Phase.Night && receiverIsMafia && senderIsMate && senderAlive;
                default: return false;
            }
        }
    }

    public enum MicLook { Open, Closed, Waiting }

    /// <summary>What the microphone button shows: the lamp, and the short reason on the small plate next to it.</summary>
    public sealed class MicState
    {
        public MicLook Look;
        public string Reason = "";
        /// <summary>A tap changes something (the player may turn his mic on or off now).</summary>
        public bool Tappable;
    }

    public static class MatchMic
    {
        /// <param name="micAllowed">the match's voice plan lets the microphone send (<see cref="VoicePlan.MicAllowed"/>)</param>
        /// <param name="playerMicOn">the player's own mic switch</param>
        /// <param name="voiceConnected">the room's voice channel is up</param>
        /// <param name="alive">in the match (the Avenger counts as alive during his moment)</param>
        public static MicState Of(Phase phase, Role? role, bool alive, bool silenced, bool avengerNow, bool micAllowed, bool playerMicOn, bool voiceConnected)
        {
            bool inMatch = phase != Phase.Lobby && phase != Phase.RoleReveal && phase != Phase.GameOver;
            if (!voiceConnected) return new MicState { Look = MicLook.Waiting, Reason = "بانتظار الصوت" };
            if (!micAllowed)
            {
                string why = silenced ? "مُسكَت اليوم" : phase == Phase.Night ? "مغلق · الليل" : "مغلق";
                return new MicState { Look = MicLook.Closed, Reason = why };
            }
            if (!playerMicOn) return new MicState { Look = MicLook.Closed, Reason = "مكتوم منك", Tappable = true };
            string open = "مفتوح";
            if (inMatch)
            {
                if (avengerNow) open = "مفتوح · الكل بيسمعك";
                else if (!alive) open = "مفتوح · للمتفرجين";
                else if (phase == Phase.Night && role.HasValue && role.Value.IsMafiaTeam()) open = "مفتوح · للمافيا فقط";
            }
            return new MicState { Look = MicLook.Open, Reason = open, Tappable = true };
        }
    }
}
