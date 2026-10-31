using System;
using System.Collections.Generic;
using System.Text;

namespace MafiaNight.MatchRules.Net
{
    /// <summary>
    /// One message between the host and a player. Plain text on the wire (type, then arguments, separated by U+001F) so it needs no
    /// serializer and can be read in a log. Names are cleaned of the separator before they are sent.
    /// </summary>
    public sealed class WireMessage
    {
        public const char Sep = '\u001f';
        public string Type { get; }
        public string[] Args { get; }

        public WireMessage(string type, params string[] args) { Type = type; Args = args ?? new string[0]; }

        public string Arg(int i, string fallback = "") => i >= 0 && i < Args.Length ? Args[i] : fallback;
        public int Int(int i, int fallback = 0) => int.TryParse(Arg(i), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
        public float Float(int i, float fallback = 0f) => float.TryParse(Arg(i), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

        public static string Clean(string s) => (s ?? "").Replace(Sep, ' ').Replace('\n', ' ').Replace('\r', ' ');
        public static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        public byte[] ToBytes() => Encoding.UTF8.GetBytes(Type + (Args.Length > 0 ? Sep + string.Join(Sep.ToString(), Args) : ""));

        public static WireMessage FromBytes(byte[] data, int length = -1)
        {
            if (data == null) return null;
            string s = Encoding.UTF8.GetString(data, 0, length < 0 ? data.Length : length);
            if (s.Length == 0) return null;
            var parts = s.Split(Sep);
            var args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);
            return new WireMessage(parts[0], args);
        }

        public override string ToString() => Type + "(" + string.Join(", ", Args) + ")";
    }

    /// <summary>The message types. Player to host: Act. Host to players: everything else.</summary>
    public static class Msg
    {
        // any member -> every other member (room text chat, no host needed)
        public const string Chat = "chat";          // text
        // the mic seats of the room (RoomStage): a member asks the host, the host sends the whole stage to everybody
        public const string StageAct = "sact";      // member -> host: kind (sit, stand, hand, bring, down, lock, cfg), two arguments
        public const string Stage = "stage";        // host -> members: RoomStage.Export()
        // player -> host
        public const string Act = "act";            // kind, target   (kinds: mafia, save, check, skip, vote, ready)
        // host -> players
        public const string Start = "start";        // the match begins
        public const string Role = "role";          // role, mate ids...  (private: only to that player)
        public const string State = "state";        // phase, round, secondsLeft, then (id, name, alive, left) per player
        public const string Result = "result";      // kind, ActionResult  (answer to an Act)
        public const string MafiaVotes = "mvotes";  // (mafia id, victim id)... (private: only to the mafia)
        public const string Sheriff = "sheriff";    // target id, isMafia (private: only to the sheriff)
        public const string Dawn = "dawn";          // victim id, saved, died id, died role
        public const string Skip = "skip";          // count, needed
        public const string Tally = "tally";        // (target id, count)... live vote count
        public const string Verdict = "verdict";    // removed id, removed role, votes, abstained
        public const string Reveal = "reveal";      // (id, role)... roles of the players who died, for somebody who joined or came back mid-match
        public const string Over = "over";          // winner, (id, role)...
        public const string Snapshot = "snap";      // the whole match state, from the host to the member who would take over (private)
        public const string Abort = "abort";        // the match ended without a winner (the host was lost and nobody could take over)
    }

    /// <summary>What the host uses to reach a player and what it hears back. The network layer implements it (EOS P2P); tests use an in-memory hub.</summary>
    public interface IMatchTransport
    {
        void Send(string peerId, WireMessage message);
        /// <summary>sender id, message</summary>
        event Action<string, WireMessage> Received;
    }
}
