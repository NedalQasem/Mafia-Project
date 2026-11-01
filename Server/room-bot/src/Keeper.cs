using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using MafiaNight.MatchRules.Net;
using MafiaNight.MatchRules.Stage;

namespace MafiaNight.RoomBot
{
    /// <summary>
    /// Keeps the official rooms open. The rewards service says which rooms exist (its answer to every report); for each one the keeper owns an EOS lobby
    /// (no voice room: the voice is LiveKit), runs the mic seats of the room with the game's own <see cref="StageSync"/> (the keeper is the host), and tells the
    /// service every few seconds who is inside and how the stage looks. Orders from the admin board (kick, take down from the mic) arrive in the same answer.
    /// The bots the board orders for a room are made-up members run here by a <see cref="BotCrew"/>: they sit on a few mic seats and write in the room's chat.
    /// </summary>
    internal sealed class Keeper
    {
        private sealed class Def
        {
            public string Id = "", Name = ""; public int Capacity = 64, MicSeats = 8; public bool MicRequest = true, ChatEnabled = true;
            public HashSet<string> Admins = new HashSet<string>();
            public string Key => Id + "|" + Name + "|" + Capacity + "|" + MicSeats + "|" + MicRequest + "|" + ChatEnabled;
        }

        private sealed class Kept
        {
            public Def Def;
            public string LobbyId, AppliedKey = "";
            public bool Creating;
            public DateTime NextTry = DateTime.MinValue;
            public List<Eos.Member> Members = new List<Eos.Member>();
            public StageSync Stage;
            public BotCrew Crew;
            public readonly HashSet<string> Known = new HashSet<string>();
            public string PfOf(string puid) => Members.FirstOrDefault(m => m.Puid == puid)?.Pf ?? "";
        }

        private const string SocketName = "mafia-match";
        private const byte Channel = 0;

        private readonly Eos eos;
        private readonly string apiUrl, botKey;
        private readonly Dictionary<string, Kept> rooms = new Dictionary<string, Kept>();
        private readonly ConcurrentQueue<string> answers = new ConcurrentQueue<string>();
        private readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private readonly PacketFramer.Reassembler reassembler = new PacketFramer.Reassembler();
        private readonly byte[] buffer = new byte[4096];
        private bool beating;
        private DateTime nextBeat = DateTime.MinValue, nextUpdate = DateTime.MinValue;
        private ulong requestNotif, closedNotif;
        private bool running = true;
        private readonly Random rng = new Random();
        private List<BotProfile> farmProfiles = new List<BotProfile>();
        private readonly Dictionary<string, (int count, bool sit, bool chat)> farmOrders = new Dictionary<string, (int, bool, bool)>();   // "official:1001" -> what the board wants

        public Keeper(Eos eos, string apiUrl, string botKey) { this.eos = eos; this.apiUrl = apiUrl.TrimEnd('/'); this.botKey = botKey; }

        public void Stop() { running = false; }

        public void Run()
        {
            OpenSocket();
            Log.Info("Keeper running as " + eos.MeId);
            while (running)
            {
                eos.Tick();
                ReceivePackets();
                while (answers.TryDequeue(out var json)) Apply(json);
                var now = DateTime.UtcNow;
                if (now >= nextUpdate) { nextUpdate = now.AddSeconds(1); foreach (var r in rooms.Values.ToList()) Update(r); }
                if (now >= nextBeat && !beating) { nextBeat = now.AddSeconds(5); Beat(); }
                Thread.Sleep(20);
            }
            foreach (var r in rooms.Values) if (r.LobbyId != null) eos.Destroy(r.LobbyId);
            eos.Pump(() => false, 800);
        }

        // ------------------------------------------------------------------ the service

        private void Beat()
        {
            beating = true;
            var report = new
            {
                rooms = rooms.Values.Where(r => r.LobbyId != null && r.Stage != null).Select(r => new
                {
                    id = r.Def.Id, lobbyId = r.LobbyId,
                    members = r.Members.Where(m => !m.Keeper).Select(m => new { puid = m.Puid, pf = m.Pf, name = m.Name, bot = false })
                        .Concat(r.Stage.Virtuals.Select(v => new { puid = v.Id, pf = "", name = v.Name, bot = true })).ToArray(),
                    stage = new { head = r.Stage.Stage.HeadHolder ?? "", seats = Enumerable.Range(0, r.Stage.Stage.SeatCount).Select(i => r.Stage.Stage.SeatHolder(i)).Where(s => s != null).ToArray(), queue = r.Stage.Stage.Queue.ToArray() },
                }).ToArray(),
                real = rooms.Values.Sum(r => r.Members.Count(m => !m.Keeper)),
                bots = rooms.Values.Where(r => r.Stage != null).SelectMany(r => r.Stage.Virtuals.Select(v => new { id = v.Id.Substring(BotCrew.IdPrefix.Length), room = "official:" + r.Def.Id, status = r.Stage.Stage.IsSeated(v.Id) ? "seated" : "idle", role = "" })).ToArray(),
            };
            string body = JsonSerializer.Serialize(report);
            Task.Run(async () =>
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl + "/v1/bot/beat") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                    req.Headers.Add("x-bot-key", botKey);
                    using var res = await http.SendAsync(req);
                    string text = await res.Content.ReadAsStringAsync();
                    if (res.IsSuccessStatusCode) answers.Enqueue(text); else Log.Warn("Beat refused: " + (int)res.StatusCode + " " + text);
                }
                catch (Exception e) { Log.Warn("Beat failed: " + e.Message); }
                finally { beating = false; }
            });
        }

        private void Apply(string json)
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(json); } catch { return; }
            using (doc)
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("rooms", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    var seen = new HashSet<string>();
                    foreach (var e in list.EnumerateArray())
                    {
                        var d = new Def
                        {
                            Id = Str(e, "id"), Name = Str(e, "name"), Capacity = Math.Clamp(Int(e, "capacity", 64), 4, 64), MicSeats = Math.Clamp(Int(e, "micSeats", 8), 2, 12),
                            MicRequest = Str(e, "micMode") != "free", ChatEnabled = !e.TryGetProperty("chatEnabled", out var ce) || ce.ValueKind != JsonValueKind.False,
                        };
                        if (e.TryGetProperty("admins", out var ad) && ad.ValueKind == JsonValueKind.Array) foreach (var a in ad.EnumerateArray()) if (a.ValueKind == JsonValueKind.String) d.Admins.Add(a.GetString());
                        if (d.Id.Length == 0) continue;
                        seen.Add(d.Id);
                        if (rooms.TryGetValue(d.Id, out var kept)) kept.Def = d; else { rooms[d.Id] = new Kept { Def = d }; Log.Info("Room " + d.Id + " (" + d.Name + ") will be kept open"); }
                    }
                    foreach (var id in rooms.Keys.Where(k => !seen.Contains(k)).ToList()) { var gone = rooms[id]; if (gone.LobbyId != null) eos.Destroy(gone.LobbyId); rooms.Remove(id); Log.Info("Room " + id + " was removed"); }
                }
                if (root.TryGetProperty("farm", out var farm) && farm.ValueKind == JsonValueKind.Object) ApplyFarm(farm);
                if (root.TryGetProperty("commands", out var cmds) && cmds.ValueKind == JsonValueKind.Array)
                    foreach (var c in cmds.EnumerateArray()) Command(Str(c, "room"), Str(c, "kind"), Str(c, "puid"));
            }
        }

        /// <summary>The board's bots: the profiles, and for each room how many of them it wants there (orders of the form "official:<id>").</summary>
        private void ApplyFarm(JsonElement farm)
        {
            var profiles = new List<BotProfile>();
            if (farm.TryGetProperty("profiles", out var ps) && ps.ValueKind == JsonValueKind.Array)
                foreach (var p in ps.EnumerateArray())
                {
                    var bp = new BotProfile { Id = Str(p, "id"), Name = Str(p, "name"), Level = Int(p, "level", 1), Rank = Int(p, "rank", 0), Style = Str(p, "style") };
                    if (p.TryGetProperty("lines", out var ls) && ls.ValueKind == JsonValueKind.Array) foreach (var l in ls.EnumerateArray()) if (l.ValueKind == JsonValueKind.String && l.GetString().Length > 0) bp.Lines.Add(l.GetString());
                    if (bp.Id.Length > 0 && bp.Name.Length > 0) profiles.Add(bp);
                }
            farmProfiles = profiles;
            farmOrders.Clear();
            if (farm.TryGetProperty("orders", out var os) && os.ValueKind == JsonValueKind.Array)
                foreach (var o in os.EnumerateArray())
                {
                    string room = Str(o, "room");
                    if (!room.StartsWith("official:")) continue;      // the rooms of players are for the bots run by a player's device
                    bool sit = !o.TryGetProperty("sit", out var s) || s.ValueKind != JsonValueKind.False, chat = !o.TryGetProperty("chat", out var c) || c.ValueKind != JsonValueKind.False;
                    farmOrders.TryGetValue(room, out var old);
                    farmOrders[room] = (old.count + Math.Max(0, Int(o, "count", 0)), old.sit || sit, old.chat || chat);
                }
        }

        private void RunCrew(Kept r)
        {
            if (r.Crew == null)
                r.Crew = new BotCrew(r.Stage, () => DateTime.UtcNow, rng, (who, text) => Say(r.Def.Id, who, text), () => r.Members.Count(m => !m.Keeper));
            farmOrders.TryGetValue("official:" + r.Def.Id, out var want);
            var elsewhere = new HashSet<string>(rooms.Values.Where(o => o != r && o.Crew != null).SelectMany(o => o.Crew.Ids));
            int room = Math.Max(0, r.Def.Capacity - r.Members.Count(m => !m.Keeper));
            r.Crew.Configure(farmProfiles.Where(p => !elsewhere.Contains(BotCrew.IdOf(p))), Math.Min(want.count, room), want.sit, want.chat && r.Def.ChatEnabled);
            r.Crew.Tick();
        }

        private void Say(string roomId, VirtualMember who, string text)
        {
            string body = JsonSerializer.Serialize(new { room = roomId, bot = new { id = who.Id.Substring(BotCrew.IdPrefix.Length), name = who.Name, rank = who.Rank }, text });
            Task.Run(async () =>
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl + "/v1/bot/say") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                    req.Headers.Add("x-bot-key", botKey);
                    using var res = await http.SendAsync(req);
                    if (!res.IsSuccessStatusCode) Log.Warn("Say refused: " + (int)res.StatusCode);
                }
                catch (Exception e) { Log.Warn("Say failed: " + e.Message); }
            });
        }

        private void Command(string roomId, string kind, string puid)
        {
            if (!rooms.TryGetValue(roomId, out var r) || r.LobbyId == null || puid.Length == 0) return;
            Log.Info("Order: " + kind + " " + puid + " in room " + roomId);
            if (puid.StartsWith(BotCrew.IdPrefix)) { if (kind != "kick") r.Stage?.Down(puid); return; }       // a bot is not an EOS user: only the mic can be taken from it
            if (kind == "kick") eos.Kick(r.LobbyId, puid);
            else if (kind == "down") r.Stage?.Down(puid);
        }

        private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
        private static int Int(JsonElement e, string name, int d) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : d;

        // ------------------------------------------------------------------ one room

        private void Update(Kept r)
        {
            var now = DateTime.UtcNow;
            if (r.LobbyId == null)
            {
                if (r.Creating || now < r.NextTry) return;
                r.Creating = true;
                eos.CreateLobby((uint)r.Def.Capacity, m => FillLobby(m, r.Def, true), id =>
                {
                    r.Creating = false;
                    if (id == null) { r.NextTry = DateTime.UtcNow.AddSeconds(10); return; }
                    r.LobbyId = id; r.AppliedKey = r.Def.Key; r.Known.Clear();
                    r.Stage = new StageSync(eos.MeId, () => eos.MeId, () => r.Members.Select(m => m.Puid).ToList(), (to, msg) => Send(to, msg),
                        puid => r.Def.Admins.Contains(r.PfOf(puid)), r.Def.MicSeats, r.Def.MicRequest ? MicMode.Request : MicMode.Free);
                    Log.Info("Room " + r.Def.Id + " is open: lobby " + id);
                });
                return;
            }
            var members = eos.Members(r.LobbyId);
            if (members == null) { Log.Warn("Room " + r.Def.Id + ": the lobby is gone, making it again"); r.LobbyId = null; r.Stage = null; r.Crew = null; r.Members.Clear(); r.NextTry = now.AddSeconds(3); return; }
            r.Members = members;
            var present = new HashSet<string>(members.Select(m => m.Puid));
            foreach (var m in members) if (r.Known.Add(m.Puid) && !m.Keeper) { Log.Info("Room " + r.Def.Id + ": " + m.Name + " joined"); r.Stage.MemberJoined(m.Puid); r.Crew?.Greet(); }
            foreach (var id in r.Known.Where(k => !present.Contains(k)).ToList()) { r.Known.Remove(id); r.Stage.MemberLeft(id); }
            RunCrew(r);
            if (r.AppliedKey != r.Def.Key)
            {
                r.AppliedKey = r.Def.Key;
                eos.Update(r.LobbyId, m => FillLobby(m, r.Def, false), ok => { if (!ok) r.AppliedKey = ""; });
                r.Stage.Configure(r.Def.MicSeats, r.Def.MicRequest ? MicMode.Request : MicMode.Free);
            }
        }

        private static void FillLobby(Epic.OnlineServices.Lobby.LobbyModification m, Def d, bool first)
        {
            Eos.AddStr(m, "name", d.Name); Eos.AddInt(m, "chat", d.ChatEnabled ? 1 : 0); Eos.AddInt(m, "mic_seats", d.MicSeats); Eos.AddInt(m, "mic_mode", d.MicRequest ? 1 : 0);
            if (!first) return;
            Eos.AddStr(m, "room_type", "private");            // private lobbies are not in the players' ordinary list: the official room is pinned from the service's list
            Eos.AddStr(m, "official", d.Id);
            Eos.AddStr(m, "mode", "hangout"); Eos.AddStr(m, "lang", "ar"); Eos.AddStr(m, "code", "");
            Eos.AddStr(m, "owner_name", "المكتب"); Eos.AddStr(m, "owner_pf", ""); Eos.AddInt(m, "owner_level", 1); Eos.AddInt(m, "owner_rank", 7);
            Eos.AddInt(m, "in_match", 0); Eos.AddInt(m, "locked", 0); Eos.AddStr(m, "bg", ""); Eos.AddInt(m, "created", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Eos.AddMemberStr(m, "name", "المكتب"); Eos.AddMemberInt(m, "rank", 7); Eos.AddMemberInt(m, "level", 1); Eos.AddMemberStr(m, "pf", ""); Eos.AddMemberInt(m, "keeper", 1);
        }

        // ------------------------------------------------------------------ P2P (the game's own socket, so the players' devices talk to the keeper as to a host)

        private static SocketId Socket => new SocketId { SocketName = SocketName };

        private void OpenSocket()
        {
            var p = eos.P2P;
            var ro = new AddNotifyPeerConnectionRequestOptions { LocalUserId = eos.Me, SocketId = Socket };
            requestNotif = p.AddNotifyPeerConnectionRequest(ref ro, null, (ref OnIncomingConnectionRequestInfo info) =>
            {
                if (!InSomeRoom(info.RemoteUserId?.ToString())) return;
                var ao = new AcceptConnectionOptions { LocalUserId = eos.Me, RemoteUserId = info.RemoteUserId, SocketId = Socket };
                eos.P2P.AcceptConnection(ref ao);
            });
            var co = new AddNotifyPeerConnectionClosedOptions { LocalUserId = eos.Me, SocketId = Socket };
            closedNotif = p.AddNotifyPeerConnectionClosed(ref co, null, (ref OnRemoteConnectionClosedInfo info) => { if (info.RemoteUserId != null) reassembler.Forget(info.RemoteUserId.ToString()); });
        }

        private bool InSomeRoom(string puid) => puid != null && rooms.Values.Any(r => r.Members.Any(m => m.Puid == puid));

        private void Send(string peer, WireMessage message)
        {
            var remote = ProductUserId.FromString(peer);
            if (remote == null || !InSomeRoom(peer)) return;
            foreach (var piece in PacketFramer.Split(message.ToBytes()))
            {
                var so = new SendPacketOptions { LocalUserId = eos.Me, RemoteUserId = remote, SocketId = Socket, Channel = Channel, Data = new ArraySegment<byte>(piece), AllowDelayedDelivery = true, Reliability = PacketReliability.ReliableOrdered };
                var r = eos.P2P.SendPacket(ref so);
                if (r != Result.Success) { Log.Warn("SendPacket " + r + " (" + message.Type + ")"); return; }
            }
        }

        private void ReceivePackets()
        {
            ProductUserId peer = null; var socket = new SocketId();
            for (int i = 0; i < 256; i++)
            {
                var ro = new ReceivePacketOptions { LocalUserId = eos.Me, MaxDataSizeBytes = (uint)buffer.Length, RequestedChannel = Channel };
                var r = eos.P2P.ReceivePacket(ref ro, ref peer, ref socket, out byte ch, new ArraySegment<byte>(buffer), out uint written);
                if (r != Result.Success) break;
                string from = peer.ToString();
                var kept = rooms.Values.FirstOrDefault(k => k.Members.Any(m => m.Puid == from));
                if (kept == null) continue;
                var whole = reassembler.Push(from, buffer, (int)written);
                if (whole == null) continue;
                var msg = WireMessage.FromBytes(whole);
                if (msg != null) kept.Stage?.Receive(from, msg);
            }
        }
    }
}
