using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using MafiaNight.MatchRules.Stage;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Rooms;
using UnityEngine;

namespace MafiaNight.Services.Match
{
    /// <summary>
    /// A room's match on this device: the transport, the player's own view (<see cref="Client"/>) and, on the host only, the referee
    /// (<see cref="Host"/>). The screens read <see cref="Client"/>; only the host's device ever holds the secrets. Call <see cref="Tick"/> every frame.
    /// While a match runs, the host keeps sending a snapshot of it to the member who would take over; if the host is lost, that member
    /// (or whoever EOS makes the new host, via the snapshot holder) continues the match. Without any snapshot the match is cancelled.
    /// </summary>
    public sealed class MatchSession : IDisposable
    {
        private const string Tag = "MatchSession";
        private const float SnapshotEvery = 1f;       // at most one snapshot per second, only when something changed
        private const float TakeOverWait = 5f;        // how long a new host waits for a snapshot before cancelling the match

        private readonly IRoomService rooms;
        private readonly EosMatchTransport net;
        private readonly BotMatchTransport botNet;       // the host's transport with the room's bots in it (they play in the match)
        private readonly string myId;
        private Action<string> peerLost;

        private bool snapshotDirty;
        private float nextSnapshotAt;
        private List<string> held;                    // the last snapshot this device was sent as the would-be successor
        private string lastHostId;
        private float takeOverWaited = -1f;           // >= 0 while a new host waits for a snapshot
        private bool gaveSnapshotToNewHost;

        public ClientMatchController Client { get; private set; }
        public HostMatchController Host { get; private set; }
        /// <summary>The mic seats of the room (who sits where, the raised hands). The room's host is the authority.</summary>
        public StageSync Stage { get; private set; }
        public bool IAmHost => Host != null;
        /// <summary>The bots the admin board put into this room take part in the next match (host only; set by the room's bot flow).</summary>
        public bool BotsPlay { get; set; } = true;
        /// <summary>How many players a match started now would have: the members, and the bots when they play.</summary>
        public int PlayerCount => rooms.Members.Count + BotsPlaying().Count;
        /// <summary>A match is running on this device as its referee.</summary>
        public bool MatchRunning => Host != null && Host.Running;

        /// <summary>The match was cancelled for everybody (reason in Arabic); the screens go back to the room.</summary>
        public event Action<string> Cancelled;
        /// <summary>A member of the room wrote in the room chat (sender id as EOS reports it, text).</summary>
        public event Action<string, string> RoomChat;

        public MatchSession(IEosPlatform eos, IRoomService rooms)
        {
            this.rooms = rooms;
            net = new EosMatchTransport(eos, rooms);
            myId = eos.LocalUserId?.ToString() ?? "";
            Client = new ClientMatchController(myId, SendToHost) { Trace = line => Log.Info(line, "Match") };
            Client.Aborted += reason => Cancelled?.Invoke(reason);
            botNet = new BotMatchTransport(net, new SystemRandom());
            botNet.ToHost = (id, m) => Host?.Receive(id, m);
            net.Received += OnReceived;
            peerLost = id => Host?.PlayerLeft(id);
            net.PeerLost += peerLost;
            net.Open();
            lastHostId = HostId;

            var room = rooms.CurrentRoom;
            Stage = new StageSync(myId, () => HostId, () => rooms.Members.Select(m => m.Puid), net.Send, IsOfficialAdmin,
                room != null ? room.MicSeats : 8, room != null && room.MicByRequest ? MicMode.Request : MicMode.Free);
            if (rooms.IAmHost && !IsOfficial) Stage.Sit(RoomStage.Head);          // the host takes the big seat (an official room's host is the room bot: its big seat is for the admins)
        }

        private bool IsOfficial => !string.IsNullOrEmpty(rooms.CurrentRoom?.OfficialId);

        /// <summary>The admins of an official room (named in the admin board by PlayFab id) run its stage like a host does.</summary>
        private bool IsOfficialAdmin(string puid)
        {
            var room = rooms.CurrentRoom;
            if (room == null || string.IsNullOrEmpty(room.OfficialId)) return false;
            var def = OfficialRooms.Find(room.OfficialId);
            var m = rooms.Members.FirstOrDefault(x => x.Puid == puid);
            return def != null && m != null && !string.IsNullOrEmpty(m.PlayFabId) && def.Admins.Contains(m.PlayFabId);
        }

        private string HostId => rooms.Members.FirstOrDefault(m => m.IsHost)?.Puid;
        private bool MatchActive { get { var p = Client.Phase; return p != Phase.Lobby && p != Phase.GameOver; } }

        private void SendToHost(WireMessage m)
        {
            string host = HostId;
            if (host == null) return;
            if (host == myId) Host?.Receive(myId, m); else net.Send(host, m);
        }

        private void OnReceived(string sender, WireMessage m)
        {
            if (m.Type == Msg.Chat)
            {
                if (Client.SilencedId != null && Client.SilencedId == sender && Client.Phase != Phase.Night && Client.Phase != Phase.GameOver) return;   // silenced: even a modified client cannot write
                RoomChat?.Invoke(sender, m.Arg(0)); return;
            }
            if (m.Type == Msg.BotChat) { if (sender == HostId) RoomChat?.Invoke(m.Arg(0), m.Arg(1)); return; }       // only the host speaks for a bot
            if (m.Type == Msg.StageAct || m.Type == Msg.Stage || m.Type == Msg.Roster) { Stage.Receive(sender, m); return; }
            if (m.Type == Msg.Act) { Host?.Receive(sender, m); return; }
            if (m.Type == Msg.Snapshot)
            {
                if (sender == HostId && !rooms.IAmHost) { held = m.Args.ToList(); gaveSnapshotToNewHost = false; }          // from the host: keep it
                else if (takeOverWaited >= 0f && rooms.IAmHost) TakeOverWith(m.Args.ToList());                              // we are the new host and were waiting for it
                return;
            }
            if (sender == HostId) Client.Apply(m);        // only the room's host may tell us how the match goes
        }

        /// <summary>Sends a room chat line to every other member (peer to peer, nothing is stored on a server).</summary>
        public void SendChat(string text)
        {
            var msg = new WireMessage(Msg.Chat, text);
            foreach (var m in rooms.Members) if (m.Puid != myId) net.Send(m.Puid, msg);
        }

        private List<VirtualMember> BotsPlaying() => BotsPlay && rooms.IAmHost && Stage != null ? Stage.Virtuals.ToList() : new List<VirtualMember>();

        /// <summary>Host only: a bot of the room writes in the room chat (everybody, the host included, sees it as a line from that bot).</summary>
        public void BotSay(VirtualMember bot, string text)
        {
            if (!rooms.IAmHost || bot == null || string.IsNullOrEmpty(text)) return;
            var msg = new WireMessage(Msg.BotChat, bot.Id, text);
            foreach (var m in rooms.Members) if (m.Puid != myId) net.Send(m.Puid, msg);
            RoomChat?.Invoke(bot.Id, text);
        }

        /// <summary>Host only: deals the roles and starts. False if the room does not have 4–16 players.</summary>
        public bool StartMatch(MatchConfig config = null)
        {
            if (!rooms.IAmHost || Host != null && Host.Running) return false;
            var bots = BotsPlaying();
            var players = rooms.Members.Select(m => new KeyValuePair<string, string>(m.Puid, m.Name ?? "")).Concat(bots.Select(b => new KeyValuePair<string, string>(b.Id, b.Name ?? ""))).ToList();
            config = config ?? MatchConfig.WithAllRoles();
            if (!MafiaMatch.CanStart(players.Count, config)) return false;
            var match = new MafiaMatch(config, players, new SystemRandom());
            Host?.Dispose();
            botNet.Set(bots.Select(b => b.Id));
            Host = new HostMatchController(match, botNet, myId, Client.Apply) { Trace = line => Log.Info(line, "MatchHost") };
            Host.Finished += OnFinished;
            Host.Changed += () => snapshotDirty = true;
            var roster = rooms.Members.ToDictionary(m => m.Puid, m => m.PlayFabId ?? "");
            foreach (var b in bots) roster[b.Id] = "";           // a bot has no account: no coins, no rank, no report
            if (!Host.Begin(Guid.NewGuid().ToString("N"), roster)) { Host.Dispose(); Host = null; botNet.Set(new string[0]); return false; }
            rooms.SetInMatch(true);
            Log.Info("Match started with " + match.Players.Count + " players (" + bots.Count + " bots)", Tag);
            return true;
        }

        private void OnFinished() { if (rooms.IAmHost) rooms.SetInMatch(false); held = null; }

        /// <summary>A player left the room: he counts as dead.</summary>
        public void MemberLeft(string puid) { Host?.PlayerLeft(puid); Stage.MemberLeft(puid); }

        /// <summary>Somebody entered the room: during a match he watches, or, if he played before and left, he gets his seat back.</summary>
        public void MemberJoined(string puid) { Host?.AddSpectator(puid); Stage.MemberJoined(puid); }

        /// <summary>The host kicked a member: if he comes back during this match he only watches.</summary>
        public void MemberKicked(string puid) { Host?.Forbid(puid); }

        // ---------------------------------------------------------------- losing the host

        /// <summary>This device just became the room's host while a match was running: continue it, or cancel it.</summary>
        public void BecameHost()
        {
            if (Host != null || !MatchActive) return;
            if (held != null && TakeOverWith(held)) return;
            takeOverWaited = 0f;           // the member who holds the snapshot may still send it to us
            Log.Info("New host without a snapshot: waiting for one", Tag);
        }

        private bool TakeOverWith(List<string> data)
        {
            var host = HostMatchController.Resume(data, MatchConfig.WithAllRoles(), new SystemRandom(), net, myId, Client.Apply);
            if (host == null) { Log.Warning("Snapshot unreadable", Tag); return false; }
            Host?.Dispose();
            Host = host;
            Host.Trace = line => Log.Info(line, "MatchHost");
            Host.Finished += OnFinished;
            Host.Changed += () => snapshotDirty = true;
            takeOverWaited = -1f;
            held = null;
            // players who are gone from the room count as left (the old host first of all)
            var inRoom = new HashSet<string>(rooms.Members.Select(m => m.Puid));
            foreach (var p in Host.Match.Players.ToList()) if (!inRoom.Contains(p.Id)) Host.PlayerLeft(p.Id);
            Host.Reannounce();
            Log.Info("Took over the match as host", Tag);
            return true;
        }

        private void CancelMatch(string reason)
        {
            takeOverWaited = -1f;
            foreach (var m in rooms.Members) if (m.Puid != myId) net.Send(m.Puid, new WireMessage(Msg.Abort, reason));
            Client.Apply(new WireMessage(Msg.Abort, reason));
            if (rooms.IAmHost) rooms.SetInMatch(false);
            Log.Warning("Match cancelled: " + reason, Tag);
        }

        public void Tick(float dt)
        {
            net.Tick();
            Host?.Tick(dt);
            botNet.Tick(dt);
            Client.Tick(dt);

            // host side: keep the would-be successor up to date
            if (Host != null && Host.Running && snapshotDirty && Time.unscaledTime >= nextSnapshotAt)
            {
                string successor = rooms.NextHostCandidate;
                if (!string.IsNullOrEmpty(successor))
                {
                    net.Send(successor, new WireMessage(Msg.Snapshot, Host.ExportState().ToArray()));
                    nextSnapshotAt = Time.unscaledTime + SnapshotEvery;
                    snapshotDirty = false;
                }
            }

            // somebody else became host: the snapshot holder passes the snapshot on
            string host = HostId;
            if (host != null && host != lastHostId)
            {
                lastHostId = host;
                if (host == myId) { Stage.HostChanged(); if (Stage.Stage.HeadHolder == null && !IsOfficial) Stage.Sit(RoomStage.Head); }
                if (host == myId) BecameHost();
                else if (held != null && !gaveSnapshotToNewHost && MatchActive)
                {
                    gaveSnapshotToNewHost = true;
                    net.Send(host, new WireMessage(Msg.Snapshot, held.ToArray()));
                }
            }

            // a new host that never got a snapshot cancels the match
            if (takeOverWaited >= 0f)
            {
                takeOverWaited += dt;
                if (Host != null) takeOverWaited = -1f;
                else if (takeOverWaited >= TakeOverWait) CancelMatch("انتهت المباراة لأن المضيف انقطع.");
            }
        }

        public void Dispose()
        {
            Host?.Dispose();
            net.Received -= OnReceived;
            net.PeerLost -= peerLost;
            net.Dispose();
        }
    }
}
