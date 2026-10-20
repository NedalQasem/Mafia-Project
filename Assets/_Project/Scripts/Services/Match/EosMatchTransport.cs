using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.MatchRules.Net;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Rooms;

namespace MafiaNight.Services.Match
{
    /// <summary>
    /// <see cref="IMatchTransport"/> on EOS peer-to-peer (free, relayed when two phones cannot see each other). One reliable ordered
    /// socket per room. Only the players of the current room are trusted: connection requests from anybody else are refused.
    /// EOS says who sent each packet, so a player cannot pretend to be another one. Call <see cref="Tick"/> every frame.
    /// </summary>
    public sealed class EosMatchTransport : IMatchTransport, IDisposable
    {
        private const string Tag = "MatchNet";
        private const string SocketName = "mafia-match";
        private const byte Channel = 0;
        private const uint MaxPacket = 4096;

        private readonly IEosPlatform eos;
        private readonly IRoomService rooms;
        private readonly byte[] buffer = new byte[MaxPacket];
        private ulong requestNotif, closedNotif;
        private bool opened;

        public event Action<string, WireMessage> Received;
        /// <summary>A peer's connection closed (he left or lost the network). Argument: his Product User Id.</summary>
        public event Action<string> PeerLost;

        public EosMatchTransport(IEosPlatform eos, IRoomService rooms) { this.eos = eos; this.rooms = rooms; }

        private P2PInterface P2P => eos?.Platform?.GetP2PInterface();
        private ProductUserId Me => eos?.LocalUserId;
        private static SocketId Socket => new SocketId { SocketName = SocketName };

        /// <summary>Start listening (call when the player is in a room).</summary>
        public void Open()
        {
            if (opened || P2P == null || Me == null) return;
            var sock = Socket;
            var ro = new AddNotifyPeerConnectionRequestOptions { LocalUserId = Me, SocketId = sock };
            requestNotif = P2P.AddNotifyPeerConnectionRequest(ref ro, null, OnRequest);
            var co = new AddNotifyPeerConnectionClosedOptions { LocalUserId = Me, SocketId = sock };
            closedNotif = P2P.AddNotifyPeerConnectionClosed(ref co, null, OnClosed);
            opened = true;
            Log.Info("P2P socket opened", Tag);
        }

        public void Close()
        {
            if (!opened) return;
            var p = P2P;
            if (p != null)
            {
                p.RemoveNotifyPeerConnectionRequest(requestNotif);
                p.RemoveNotifyPeerConnectionClosed(closedNotif);
                var cc = new CloseConnectionsOptions { LocalUserId = Me, SocketId = Socket };
                p.CloseConnections(ref cc);
            }
            opened = false;
        }

        public void Dispose() { Close(); }

        private bool InMyRoom(ProductUserId who)
        {
            if (who == null) return false;
            string id = who.ToString();
            foreach (var m in rooms.Members) if (m.Puid == id) return true;
            return false;
        }

        private void OnRequest(ref OnIncomingConnectionRequestInfo info)
        {
            if (!InMyRoom(info.RemoteUserId)) { Log.Warning("Refused a connection from a player outside the room", Tag); return; }
            var ao = new AcceptConnectionOptions { LocalUserId = Me, RemoteUserId = info.RemoteUserId, SocketId = Socket };
            P2P.AcceptConnection(ref ao);
        }

        private void OnClosed(ref OnRemoteConnectionClosedInfo info)
        {
            if (info.RemoteUserId != null) PeerLost?.Invoke(info.RemoteUserId.ToString());
        }

        public void Send(string peerId, WireMessage message)
        {
            var p = P2P; if (p == null || Me == null) return;
            var remote = ProductUserId.FromString(peerId);
            if (remote == null || !InMyRoom(remote)) return;
            var data = message.ToBytes();
            if (data.Length > MaxPacket) { Log.Warning("Message too big: " + message.Type, Tag); return; }
            var so = new SendPacketOptions
            {
                LocalUserId = Me, RemoteUserId = remote, SocketId = Socket, Channel = Channel,
                Data = new ArraySegment<byte>(data), AllowDelayedDelivery = true, Reliability = PacketReliability.ReliableOrdered,
            };
            var r = p.SendPacket(ref so);
            if (r != Result.Success) Log.Warning("SendPacket " + r + " (" + message.Type + ")", Tag);
        }

        /// <summary>Reads everything that arrived since the last call.</summary>
        public void Tick()
        {
            var p = P2P; if (!opened || p == null || Me == null) return;
            ProductUserId peer = null; var socket = new SocketId();
            for (int i = 0; i < 64; i++)   // a frame never needs more than this
            {
                var ro = new ReceivePacketOptions { LocalUserId = Me, MaxDataSizeBytes = MaxPacket, RequestedChannel = Channel };
                var r = p.ReceivePacket(ref ro, ref peer, ref socket, out byte ch, new ArraySegment<byte>(buffer), out uint written);
                if (r != Result.Success) break;
                if (!InMyRoom(peer)) continue;
                var msg = WireMessage.FromBytes(buffer, (int)written);
                if (msg != null) Received?.Invoke(peer.ToString(), msg);
            }
        }
    }
}
