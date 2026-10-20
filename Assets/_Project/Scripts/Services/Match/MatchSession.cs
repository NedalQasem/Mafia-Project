using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Rooms;

namespace MafiaNight.Services.Match
{
    /// <summary>
    /// A room's match on this device: the transport, the player's own view (<see cref="Client"/>) and, on the host only, the referee
    /// (<see cref="Host"/>). The screens read <see cref="Client"/>; only the host's device ever holds the secrets. Call <see cref="Tick"/> every frame.
    /// </summary>
    public sealed class MatchSession : IDisposable
    {
        private const string Tag = "MatchSession";

        private readonly IRoomService rooms;
        private readonly EosMatchTransport net;
        private readonly string myId;
        private Action<string> peerLost;

        public ClientMatchController Client { get; private set; }
        public HostMatchController Host { get; private set; }
        public bool IAmHost => Host != null;

        public MatchSession(IEosPlatform eos, IRoomService rooms)
        {
            this.rooms = rooms;
            net = new EosMatchTransport(eos, rooms);
            myId = eos.LocalUserId?.ToString() ?? "";
            Client = new ClientMatchController(myId, SendToHost);
            net.Received += OnReceived;
            peerLost = id => Host?.PlayerLeft(id);
            net.PeerLost += peerLost;
            net.Open();
        }

        private string HostId => rooms.Members.FirstOrDefault(m => m.IsHost)?.Puid;

        private void SendToHost(WireMessage m)
        {
            string host = HostId;
            if (host == null) return;
            if (host == myId) Host?.Receive(myId, m); else net.Send(host, m);
        }

        private void OnReceived(string sender, WireMessage m)
        {
            if (m.Type == Msg.Act) { Host?.Receive(sender, m); return; }
            if (sender == HostId) Client.Apply(m);        // only the room's host may tell us how the match goes
        }

        /// <summary>Host only: deals the roles and starts. False if the room does not have 4–16 players.</summary>
        public bool StartMatch(MatchConfig config = null)
        {
            if (!rooms.IAmHost || Host != null && Host.Running) return false;
            var players = rooms.Members.Select(m => new KeyValuePair<string, string>(m.Puid, m.Name ?? ""));
            config = config ?? new MatchConfig();
            if (!MafiaMatch.CanStart(rooms.Members.Count, config)) return false;
            var match = new MafiaMatch(config, players, new SystemRandom());
            Host?.Dispose();
            Host = new HostMatchController(match, net, myId, Client.Apply);
            Host.Finished += OnFinished;
            if (!Host.Begin()) { Host.Dispose(); Host = null; return false; }
            rooms.SetInMatch(true);
            Log.Info("Match started with " + match.Players.Count + " players", Tag);
            return true;
        }

        private void OnFinished() { if (rooms.IAmHost) rooms.SetInMatch(false); }

        /// <summary>A player left the room: he counts as dead.</summary>
        public void MemberLeft(string puid) { Host?.PlayerLeft(puid); }

        /// <summary>Somebody entered the room: during a match he watches.</summary>
        public void MemberJoined(string puid) { Host?.AddSpectator(puid); }

        public void Tick(float dt)
        {
            net.Tick();
            Host?.Tick(dt);
            Client.Tick(dt);
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
