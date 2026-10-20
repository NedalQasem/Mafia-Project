using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using MafiaNight.Core;
using MafiaNight.Data.Player;
using MafiaNight.Data.Rooms;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Profile;
using UnityEngine;
using EosAttribute = Epic.OnlineServices.Lobby.Attribute;

namespace MafiaNight.Services.Rooms
{
    /// <summary>
    /// <see cref="IRoomService"/> on EOS Lobbies (free, no server of ours). A room is one lobby in the bucket
    /// <see cref="RoomRules.BucketId"/>; everything the list needs lives in public lobby attributes, everything
    /// about a player in public member attributes. Private rooms are findable only by their code attribute.
    /// </summary>
    public sealed class EosRoomService : IRoomService
    {
        private const string Tag = "Rooms";

        // lobby attributes
        private const string KName = "name", KType = "room_type", KMode = "mode", KLang = "lang", KCode = "code";
        private const string KOwnerName = "owner_name", KOwnerLevel = "owner_level", KOwnerRank = "owner_rank", KOwnerPf = "owner_pf";
        private const string KLocked = "locked", KInMatch = "in_match", KChat = "chat", KBackground = "bg", KCreated = "created";
        // member attributes
        private const string MName = "name", MRank = "rank", MLevel = "level", MPf = "pf";

        private IEosPlatform eos;
        private IPlayerProfileService profile;

        private readonly Dictionary<string, LobbyDetails> found = new Dictionary<string, LobbyDetails>();
        private readonly List<RoomMember> members = new List<RoomMember>();
        private readonly Queue<float> recentCalls = new Queue<float>();
        private LobbyDetails currentDetails;
        private ulong notifUpdate, notifMemberUpdate, notifMemberStatus;
        private bool subscribed;
        private string lastHost;        // host puid at the previous update, to notice a hand-over
        private bool returningHost;     // a PromoteMember call back to the owner is running

        public RoomListing CurrentRoom { get; private set; }
        public bool InRoom => CurrentRoom != null;
        public IReadOnlyList<RoomMember> Members => members;

        public event Action<RoomListing> OnRoomJoined;
        public event Action<string> OnRoomLeft;
        public event Action OnRoomChanged;
        public event Action<string> OnHostChanged;

        public bool IAmHost => CurrentRoom != null && Me != null && CurrentRoom.OwnerPuid == Me.ToString();

        public bool IsTemporaryHost => IAmHost && !string.IsNullOrEmpty(CurrentRoom.OwnerPlayFabId) && CurrentRoom.OwnerPlayFabId != MyPf;

        // ------------------------------------------------------------------------------------------ helpers

        private LobbyInterface Lobby => eos?.Platform?.GetLobbyInterface();
        private ProductUserId Me => eos?.LocalUserId;

        private bool Ready()
        {
            if (eos == null) ServiceLocator.TryGet(out eos);
            if (profile == null) ServiceLocator.TryGet(out profile);
            return eos != null && eos.IsLoggedIn && Me != null && Me.IsValid() && Lobby != null;
        }

        /// <summary>EOS allows 30 create/join calls per minute per player; keep well below.</summary>
        private bool Allow()
        {
            float now = Time.unscaledTime;
            while (recentCalls.Count > 0 && now - recentCalls.Peek() > 60f) recentCalls.Dequeue();
            if (recentCalls.Count >= 20) return false;
            recentCalls.Enqueue(now);
            return true;
        }

        private static bool IsOk(Result r) => r == Result.Success;

        private static void AddStr(LobbyModification m, string key, string value)
        {
            var o = new LobbyModificationAddAttributeOptions { Attribute = new AttributeData { Key = key, Value = new AttributeDataValue { AsUtf8 = value ?? "" } }, Visibility = LobbyAttributeVisibility.Public };
            m.AddAttribute(ref o);
        }

        private static void AddInt(LobbyModification m, string key, long value)
        {
            var o = new LobbyModificationAddAttributeOptions { Attribute = new AttributeData { Key = key, Value = new AttributeDataValue { AsInt64 = value } }, Visibility = LobbyAttributeVisibility.Public };
            m.AddAttribute(ref o);
        }

        private static void AddMemberStr(LobbyModification m, string key, string value)
        {
            var o = new LobbyModificationAddMemberAttributeOptions { Attribute = new AttributeData { Key = key, Value = new AttributeDataValue { AsUtf8 = value ?? "" } }, Visibility = LobbyAttributeVisibility.Public };
            m.AddMemberAttribute(ref o);
        }

        private static void AddMemberInt(LobbyModification m, string key, long value)
        {
            var o = new LobbyModificationAddMemberAttributeOptions { Attribute = new AttributeData { Key = key, Value = new AttributeDataValue { AsInt64 = value } }, Visibility = LobbyAttributeVisibility.Public };
            m.AddMemberAttribute(ref o);
        }

        private static string Str(LobbyDetails d, string key)
        {
            var o = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = key };
            if (d.CopyAttributeByKey(ref o, out EosAttribute? a) != Result.Success || a == null || a.Value.Data == null) return null;
            return a.Value.Data.Value.Value.AsUtf8;
        }

        private static long Int(LobbyDetails d, string key, long fallback = 0)
        {
            var o = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = key };
            if (d.CopyAttributeByKey(ref o, out EosAttribute? a) != Result.Success || a == null || a.Value.Data == null) return fallback;
            return a.Value.Data.Value.Value.AsInt64 ?? fallback;
        }

        private static string MemberStr(LobbyDetails d, ProductUserId who, string key)
        {
            var o = new LobbyDetailsCopyMemberAttributeByKeyOptions { TargetUserId = who, AttrKey = key };
            if (d.CopyMemberAttributeByKey(ref o, out EosAttribute? a) != Result.Success || a == null || a.Value.Data == null) return null;
            return a.Value.Data.Value.Value.AsUtf8;
        }

        private static long MemberInt(LobbyDetails d, ProductUserId who, string key, long fallback = 0)
        {
            var o = new LobbyDetailsCopyMemberAttributeByKeyOptions { TargetUserId = who, AttrKey = key };
            if (d.CopyMemberAttributeByKey(ref o, out EosAttribute? a) != Result.Success || a == null || a.Value.Data == null) return fallback;
            return a.Value.Data.Value.Value.AsInt64 ?? fallback;
        }

        private static RoomListing Parse(LobbyDetails d)
        {
            var io = new LobbyDetailsCopyInfoOptions();
            if (d.CopyInfo(ref io, out LobbyDetailsInfo? infoN) != Result.Success || infoN == null) return null;
            var info = infoN.Value;
            var co = new LobbyDetailsGetMemberCountOptions();
            string type = Str(d, KType);
            return new RoomListing
            {
                Id = info.LobbyId,
                OwnerPuid = info.LobbyOwnerUserId != null ? info.LobbyOwnerUserId.ToString() : "",
                Capacity = (int)info.MaxMembers,
                Count = (int)d.GetMemberCount(ref co),
                Name = Str(d, KName) ?? "غرفة",
                OwnerName = Str(d, KOwnerName) ?? "",
                OwnerPlayFabId = Str(d, KOwnerPf) ?? "",
                OwnerLevel = (int)Int(d, KOwnerLevel, 1),
                OwnerRank = (int)Int(d, KOwnerRank, 0),
                Kind = type == "vip" ? RoomKind.Vip : type == "private" ? RoomKind.Private : RoomKind.Public,
                Mode = Str(d, KMode) == "mafia" ? RoomMode.Mafia : RoomMode.Hangout,
                Code = Str(d, KCode) ?? "",
                InMatch = Int(d, KInMatch) == 1,
                Locked = Int(d, KLocked) == 1,
                ChatEnabled = Int(d, KChat, 1) == 1,
                BackgroundId = Str(d, KBackground) ?? "",
                CreatedUnix = Int(d, KCreated),
            };
        }

        private List<RoomMember> ParseMembers(LobbyDetails d, string hostPuid)
        {
            var list = new List<RoomMember>();
            var co = new LobbyDetailsGetMemberCountOptions();
            uint n = d.GetMemberCount(ref co);
            string mine = Me != null ? Me.ToString() : "";
            for (uint i = 0; i < n; i++)
            {
                var mo = new LobbyDetailsGetMemberByIndexOptions { MemberIndex = i };
                var puid = d.GetMemberByIndex(ref mo);
                if (puid == null) continue;
                string id = puid.ToString();
                list.Add(new RoomMember
                {
                    Puid = id,
                    PlayFabId = MemberStr(d, puid, MPf) ?? "",
                    Name = MemberStr(d, puid, MName) ?? "لاعب",
                    Rank = (int)MemberInt(d, puid, MRank, 0),
                    Level = (int)MemberInt(d, puid, MLevel, 1),
                    IsHost = id == hostPuid,
                    IsMe = id == mine,
                });
            }
            // host first, everybody else keeps the join order (a hand-over picks the next one in line)
            int h = list.FindIndex(m => m.IsHost);
            if (h > 0) { var host = list[h]; list.RemoveAt(h); list.Insert(0, host); }
            return list;
        }

        private string MyName => string.IsNullOrEmpty(profile?.LocalProfile?.DisplayName) ? "لاعب" : profile.LocalProfile.DisplayName;
        private int MyLevel => Mathf.Max(1, profile?.LocalProfile?.Level ?? 1);
        private int MyRank => profile?.LocalProfile != null ? RankLadder.FromPoints(profile.LocalProfile.RankPoints).RankIndex : 0;
        private string MyPf => profile?.LocalProfile?.PlayFabId ?? "";

        private void FillMemberAttributes(LobbyModification m)
        {
            AddMemberStr(m, MName, MyName);
            AddMemberInt(m, MRank, MyRank);
            AddMemberInt(m, MLevel, MyLevel);
            AddMemberStr(m, MPf, MyPf);
        }

        // ------------------------------------------------------------------------------------------ list / search

        private void Search(Action<LobbySearch> configure, Action<Result, List<LobbyDetails>> done)
        {
            var so = new CreateLobbySearchOptions { MaxResults = 50 };
            var rc = Lobby.CreateLobbySearch(ref so, out LobbySearch search);
            if (!IsOk(rc) || search == null) { done(rc, null); return; }

            var bucket = new LobbySearchSetParameterOptions { Parameter = new AttributeData { Key = LobbyInterface.SEARCH_BUCKET_ID, Value = new AttributeDataValue { AsUtf8 = RoomRules.BucketId } }, ComparisonOp = ComparisonOp.Equal };
            search.SetParameter(ref bucket);
            configure(search);

            var fo = new LobbySearchFindOptions { LocalUserId = Me };
            search.Find(ref fo, null, (ref LobbySearchFindCallbackInfo data) =>
            {
                var result = data.ResultCode;
                var list = new List<LobbyDetails>();
                if (IsOk(result))
                {
                    var co = new LobbySearchGetSearchResultCountOptions();
                    uint n = search.GetSearchResultCount(ref co);
                    for (uint i = 0; i < n; i++)
                    {
                        var copt = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = i };
                        if (IsOk(search.CopySearchResultByIndex(ref copt, out LobbyDetails d)) && d != null) list.Add(d);
                    }
                }
                search.Release();
                done(result, list);
            });
        }

        private List<RoomListing> Absorb(List<LobbyDetails> details)
        {
            foreach (var kv in found) if (kv.Value != currentDetails) kv.Value.Release();
            found.Clear();
            var rooms = new List<RoomListing>();
            foreach (var d in details)
            {
                var r = Parse(d);
                if (r == null || string.IsNullOrEmpty(r.Id)) { d.Release(); continue; }
                found[r.Id] = d;
                rooms.Add(r);
            }
            return rooms;
        }

        public void RefreshList(Action<RoomResult> done)
        {
            if (!Ready()) { done(RoomResult.Fail(RoomError.NotReady)); return; }
            if (!Allow()) { done(RoomResult.Fail(RoomError.Busy)); return; }
            Search(s =>
            {
                var notPrivate = new LobbySearchSetParameterOptions { Parameter = new AttributeData { Key = KType, Value = new AttributeDataValue { AsUtf8 = "private" } }, ComparisonOp = ComparisonOp.Notequal };
                s.SetParameter(ref notPrivate);
            }, (result, details) =>
            {
                if (!IsOk(result)) { done(RoomResult.Fail(RoomError.Failed, result.ToString())); return; }
                var rooms = Absorb(details);
                rooms.RemoveAll(r => r.Locked);   // locked rooms are not offered
                // VIP rooms first, then the fullest (the list is for people who want company)
                rooms.Sort((a, b) =>
                {
                    int v = (b.Kind == RoomKind.Vip).CompareTo(a.Kind == RoomKind.Vip);
                    return v != 0 ? v : b.Count.CompareTo(a.Count);
                });
                done(RoomResult.Success(rooms: rooms));
            });
        }

        // ------------------------------------------------------------------------------------------ create

        public void Create(RoomSettings s, Action<RoomResult> done)
        {
            if (!Ready()) { done(RoomResult.Fail(RoomError.NotReady)); return; }
            string name = (s.Name ?? "").Trim();
            if (name.Length == 0) { done(RoomResult.Fail(RoomError.BadName)); return; }
            if (name.Length > RoomRules.MaxNameLength) name = name.Substring(0, RoomRules.MaxNameLength);
            if (s.Kind == RoomKind.Public && MyLevel < RoomRules.PublicMinLevel) { done(RoomResult.Fail(RoomError.LevelRequired)); return; }
            if (s.Kind == RoomKind.Vip && profile?.LocalProfile?.IsVIPActive() != true) { done(RoomResult.Fail(RoomError.VipRequired)); return; }
            if (!Allow()) { done(RoomResult.Fail(RoomError.Busy)); return; }
            int capacity = Mathf.Clamp(s.Capacity, RoomRules.MinSeats, RoomRules.MaxSeats);

            LeaveCurrent(() =>
            {
                var co = new CreateLobbyOptions
                {
                    LocalUserId = Me,
                    MaxLobbyMembers = (uint)capacity,
                    PermissionLevel = LobbyPermissionLevel.Publicadvertised,   // private rooms are still searchable, but only by their code
                    PresenceEnabled = false,                                   // players have no Epic account
                    AllowInvites = true,
                    BucketId = RoomRules.BucketId,
                    DisableHostMigration = false,                              // when the host leaves, the room goes on with another host
                    EnableRTCRoom = true,
                    RTCRoomJoinActionType = LobbyRTCRoomJoinActionType.ManualJoin,   // voice is joined explicitly (M3)
                    EnableJoinById = true,
                    CrossplayOptOut = false,
                };
                Lobby.CreateLobby(ref co, null, (ref CreateLobbyCallbackInfo data) =>
                {
                    if (!IsOk(data.ResultCode)) { done(RoomResult.Fail(RoomError.Failed, "CreateLobby " + data.ResultCode)); return; }
                    string lobbyId = data.LobbyId;
                    string code = s.Kind == RoomKind.Private ? RoomRules.NewCode() : "";
                    PublishRoomData(lobbyId, name, s, code, ok =>
                    {
                        if (!ok)
                        {
                            var dop = new DestroyLobbyOptions { LocalUserId = Me, LobbyId = lobbyId };
                            Lobby.DestroyLobby(ref dop, null, (ref DestroyLobbyCallbackInfo _) => { });
                            done(RoomResult.Fail(RoomError.Failed, "UpdateLobby"));
                            return;
                        }
                        Enter(lobbyId, done);
                    });
                });
            });
        }

        private void PublishRoomData(string lobbyId, string name, RoomSettings s, string code, Action<bool> done)
        {
            var uo = new UpdateLobbyModificationOptions { LocalUserId = Me, LobbyId = lobbyId };
            if (!IsOk(Lobby.UpdateLobbyModification(ref uo, out LobbyModification m)) || m == null) { done(false); return; }
            AddStr(m, KName, name);
            AddStr(m, KType, s.Kind == RoomKind.Vip ? "vip" : s.Kind == RoomKind.Private ? "private" : "public");
            AddStr(m, KMode, s.Mode == RoomMode.Mafia ? "mafia" : "hangout");
            AddStr(m, KLang, RoomRules.Language);
            AddStr(m, KCode, code);
            AddStr(m, KOwnerName, MyName);
            AddStr(m, KOwnerPf, MyPf);
            AddInt(m, KOwnerLevel, MyLevel);
            AddInt(m, KOwnerRank, MyRank);
            AddInt(m, KInMatch, 0);
            AddInt(m, KLocked, 0);
            AddInt(m, KChat, s.ChatEnabled ? 1 : 0);
            AddStr(m, KBackground, s.BackgroundId);
            AddInt(m, KCreated, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            FillMemberAttributes(m);
            var upd = new UpdateLobbyOptions { LobbyModificationHandle = m };
            Lobby.UpdateLobby(ref upd, null, (ref UpdateLobbyCallbackInfo data) => { m.Release(); done(IsOk(data.ResultCode)); });
        }

        // ------------------------------------------------------------------------------------------ join

        public void Join(RoomListing room, Action<RoomResult> done)
        {
            if (!Ready()) { done(RoomResult.Fail(RoomError.NotReady)); return; }
            if (room == null) { done(RoomResult.Fail(RoomError.NotFound)); return; }
            if (CurrentRoom != null && CurrentRoom.Id == room.Id) { done(RoomResult.Success(CurrentRoom)); return; }
            if (found.TryGetValue(room.Id, out var details)) { JoinDetails(details, done); return; }

            // not in the last list (e.g. a stale card): look it up by id
            if (!Allow()) { done(RoomResult.Fail(RoomError.Busy)); return; }
            Search(s => { var lo = new LobbySearchSetLobbyIdOptions { LobbyId = room.Id }; s.SetLobbyId(ref lo); }, (result, list) =>
            {
                if (!IsOk(result) || list == null || list.Count == 0) { done(RoomResult.Fail(RoomError.NotFound, result.ToString())); return; }
                var rooms = Absorb(list);
                if (rooms.Count == 0 || !found.TryGetValue(rooms[0].Id, out var d)) { done(RoomResult.Fail(RoomError.NotFound)); return; }
                JoinDetails(d, done);
            });
        }

        public void JoinByCode(string code, Action<RoomResult> done)
        {
            if (!Ready()) { done(RoomResult.Fail(RoomError.NotReady)); return; }
            code = RoomRules.NormalizeCode(code);
            if (code.Length != RoomRules.CodeLength) { done(RoomResult.Fail(RoomError.NotFound)); return; }
            if (!Allow()) { done(RoomResult.Fail(RoomError.Busy)); return; }
            Search(s =>
            {
                var po = new LobbySearchSetParameterOptions { Parameter = new AttributeData { Key = KCode, Value = new AttributeDataValue { AsUtf8 = code } }, ComparisonOp = ComparisonOp.Equal };
                s.SetParameter(ref po);
            }, (result, list) =>
            {
                if (!IsOk(result) || list == null || list.Count == 0) { done(RoomResult.Fail(RoomError.NotFound, result.ToString())); return; }
                var rooms = Absorb(list);
                if (rooms.Count == 0 || !found.TryGetValue(rooms[0].Id, out var d)) { done(RoomResult.Fail(RoomError.NotFound)); return; }
                JoinDetails(d, done);
            });
        }

        public void QuickJoin(Action<RoomResult> done)
        {
            RefreshList(r =>
            {
                if (!r.Ok) { done(r); return; }
                RoomListing best = null;
                foreach (var room in r.Rooms)
                {
                    if (room.IsFull || (CurrentRoom != null && CurrentRoom.Id == room.Id)) continue;
                    if (best == null || room.Count > best.Count) best = room;
                }
                if (best == null) { done(RoomResult.Fail(RoomError.NoRooms)); return; }
                Join(best, done);
            });
        }

        private void JoinDetails(LobbyDetails details, Action<RoomResult> done)
        {
            var room = Parse(details);
            if (room == null) { done(RoomResult.Fail(RoomError.NotFound)); return; }
            if (room.IsFull) { done(RoomResult.Fail(RoomError.Full)); return; }
            // a locked room still lets its creator back in (they get the host role back)
            if (room.Locked && (string.IsNullOrEmpty(room.OwnerPlayFabId) || room.OwnerPlayFabId != MyPf)) { done(RoomResult.Fail(RoomError.Locked)); return; }
            if (!Allow()) { done(RoomResult.Fail(RoomError.Busy)); return; }

            LeaveCurrent(() =>
            {
                var jo = new JoinLobbyOptions
                {
                    LobbyDetailsHandle = details,
                    LocalUserId = Me,
                    PresenceEnabled = false,
                    CrossplayOptOut = false,
                    RTCRoomJoinActionType = LobbyRTCRoomJoinActionType.ManualJoin,
                };
                Lobby.JoinLobby(ref jo, null, (ref JoinLobbyCallbackInfo data) =>
                {
                    if (!IsOk(data.ResultCode))
                    {
                        done(RoomResult.Fail(data.ResultCode == Result.LobbyTooManyPlayers ? RoomError.Full : data.ResultCode == Result.NotFound ? RoomError.NotFound : RoomError.Failed, "JoinLobby " + data.ResultCode));
                        return;
                    }
                    string lobbyId = data.LobbyId;
                    var uo = new UpdateLobbyModificationOptions { LocalUserId = Me, LobbyId = lobbyId };
                    if (!IsOk(Lobby.UpdateLobbyModification(ref uo, out LobbyModification m)) || m == null) { Enter(lobbyId, done); return; }
                    FillMemberAttributes(m);
                    var upd = new UpdateLobbyOptions { LobbyModificationHandle = m };
                    Lobby.UpdateLobby(ref upd, null, (ref UpdateLobbyCallbackInfo _) => { m.Release(); Enter(lobbyId, done); });
                });
            });
        }

        // ------------------------------------------------------------------------------------------ the room I am in

        private void Enter(string lobbyId, Action<RoomResult> done)
        {
            var o = new CopyLobbyDetailsHandleOptions { LobbyId = lobbyId, LocalUserId = Me };
            if (!IsOk(Lobby.CopyLobbyDetailsHandle(ref o, out LobbyDetails d)) || d == null) { done(RoomResult.Fail(RoomError.Failed, "CopyLobbyDetailsHandle")); return; }
            if (currentDetails != null && !found.ContainsValue(currentDetails)) currentDetails.Release();
            currentDetails = d;
            ReadCurrent();
            lastHost = CurrentRoom?.OwnerPuid;
            Subscribe();
            Log.Info($"In room '{CurrentRoom?.Name}' ({CurrentRoom?.Count}/{CurrentRoom?.Capacity})", Tag);
            OnRoomJoined?.Invoke(CurrentRoom);
            done(RoomResult.Success(CurrentRoom));
        }

        private void ReadCurrent()
        {
            if (currentDetails == null) return;
            var room = Parse(currentDetails);
            if (room == null) return;
            CurrentRoom = room;
            members.Clear();
            members.AddRange(ParseMembers(currentDetails, room.OwnerPuid));
        }

        private void Subscribe()
        {
            if (subscribed) return;
            var lobby = Lobby;
            var uo = new AddNotifyLobbyUpdateReceivedOptions();
            notifUpdate = lobby.AddNotifyLobbyUpdateReceived(ref uo, null, (ref LobbyUpdateReceivedCallbackInfo d) => Changed(d.LobbyId));
            var mo = new AddNotifyLobbyMemberUpdateReceivedOptions();
            notifMemberUpdate = lobby.AddNotifyLobbyMemberUpdateReceived(ref mo, null, (ref LobbyMemberUpdateReceivedCallbackInfo d) => Changed(d.LobbyId));
            var so = new AddNotifyLobbyMemberStatusReceivedOptions();
            notifMemberStatus = lobby.AddNotifyLobbyMemberStatusReceived(ref so, null, (ref LobbyMemberStatusReceivedCallbackInfo d) => MemberStatus(d.LobbyId, d.TargetUserId, d.CurrentStatus));
            subscribed = true;
        }

        private void Changed(string lobbyId)
        {
            if (CurrentRoom == null || CurrentRoom.Id != lobbyId) return;
            ReadCurrent();
            AnnounceHostChange();
            ReturnHostToOwner();
            OnRoomChanged?.Invoke();
        }

        // ------------------------------------------------------------------------------------------ host hand-over

        private RoomMember OwnerMember()
        {
            string pf = CurrentRoom?.OwnerPlayFabId;
            if (string.IsNullOrEmpty(pf)) return null;
            foreach (var m in members) if (m.PlayFabId == pf) return m;
            return null;
        }

        /// <summary>Tells the players when the host changed (the owner left and somebody took over, or the owner came back).</summary>
        private void AnnounceHostChange()
        {
            string host = CurrentRoom?.OwnerPuid;
            if (string.IsNullOrEmpty(host) || host == lastHost) { lastHost = host ?? lastHost; return; }
            lastHost = host;
            var hostMember = members.Find(m => m.IsHost);
            string ownerName = string.IsNullOrEmpty(CurrentRoom.OwnerName) ? "صاحب الغرفة" : CurrentRoom.OwnerName;
            bool ownerBack = hostMember != null && !string.IsNullOrEmpty(CurrentRoom.OwnerPlayFabId) && hostMember.PlayFabId == CurrentRoom.OwnerPlayFabId;
            string msg;
            if (IAmHost) msg = ownerBack ? "رجعت مضيف الغرفة." : "صرت مضيف الغرفة مؤقتاً لحد ما يرجع " + ownerName + ".";
            else if (ownerBack) msg = "رجع " + ownerName + " مضيفاً للغرفة.";
            else msg = (hostMember != null ? hostMember.Name : "لاعب") + " صار المضيف المؤقت.";
            Log.Info("Host is now " + (hostMember?.Name ?? host), Tag);
            OnHostChanged?.Invoke(msg);
        }

        /// <summary>The current host gives the role back when the room's creator is in the room again (not in the middle of a match).</summary>
        private void ReturnHostToOwner()
        {
            if (!IAmHost || returningHost || CurrentRoom == null || CurrentRoom.InMatch) return;
            var owner = OwnerMember();
            if (owner == null || owner.IsHost || owner.IsMe) return;
            returningHost = true;
            var p = new PromoteMemberOptions { LobbyId = CurrentRoom.Id, LocalUserId = Me, TargetUserId = ProductUserId.FromString(owner.Puid) };
            Lobby.PromoteMember(ref p, null, (ref PromoteMemberCallbackInfo d) =>
            {
                returningHost = false;
                if (!IsOk(d.ResultCode)) Log.Warning("Could not hand the host role back: " + d.ResultCode, Tag);
            });
        }

        public string NextHostCandidate => PickSuccessor();

        /// <summary>The member who takes over when the local host leaves: the next one in line.</summary>
        private string PickSuccessor()
        {
            if (!IAmHost) return null;
            foreach (var m in members) if (!m.IsMe) return m.Puid;
            return null;
        }

        /// <summary>Leaves the lobby; a host promotes <paramref name="successor"/> first so the room stays open.</summary>
        private void LeaveLobbyNow(string lobbyId, string successor, Action next)
        {
            void DoLeave()
            {
                var l = new LeaveLobbyOptions { LocalUserId = Me, LobbyId = lobbyId };
                Lobby.LeaveLobby(ref l, null, (ref LeaveLobbyCallbackInfo _) => next?.Invoke());   // EOS closes the lobby when the last member leaves
            }
            if (string.IsNullOrEmpty(successor)) { DoLeave(); return; }
            var p = new PromoteMemberOptions { LobbyId = lobbyId, LocalUserId = Me, TargetUserId = ProductUserId.FromString(successor) };
            Lobby.PromoteMember(ref p, null, (ref PromoteMemberCallbackInfo d) =>
            {
                if (!IsOk(d.ResultCode)) Log.Warning("Hand-over failed (" + d.ResultCode + "), EOS picks the next host", Tag);
                DoLeave();
            });
        }

        private void MemberStatus(string lobbyId, ProductUserId who, LobbyMemberStatus status)
        {
            if (CurrentRoom == null || CurrentRoom.Id != lobbyId) return;
            bool me = who != null && Me != null && who.ToString() == Me.ToString();
            if (me && (status == LobbyMemberStatus.Kicked || status == LobbyMemberStatus.Closed || status == LobbyMemberStatus.Disconnected))
            {
                string why = status == LobbyMemberStatus.Kicked ? "طُردت من الغرفة." : status == LobbyMemberStatus.Closed ? "انسكّرت الغرفة." : "انقطع الاتصال بالغرفة.";
                ClearCurrent();
                OnRoomLeft?.Invoke(why);
                return;
            }
            Changed(lobbyId);
        }

        private void ClearCurrent()
        {
            if (currentDetails != null && !found.ContainsValue(currentDetails)) currentDetails.Release();
            currentDetails = null;
            CurrentRoom = null;
            lastHost = null;
            members.Clear();
        }

        /// <summary>Leaves the current room before another one is entered. A host hands the room over instead of closing it.</summary>
        private void LeaveCurrent(Action next)
        {
            if (CurrentRoom == null) { next(); return; }
            string id = CurrentRoom.Id;
            string successor = PickSuccessor();
            ClearCurrent();
            OnRoomLeft?.Invoke("");
            LeaveLobbyNow(id, successor, next);
        }

        public void Leave(Action done = null)
        {
            if (CurrentRoom == null || !Ready()) { done?.Invoke(); return; }
            string id = CurrentRoom.Id;
            string successor = PickSuccessor();
            ClearCurrent();
            OnRoomLeft?.Invoke("");
            LeaveLobbyNow(id, successor, done);
        }

        public void SetLocked(bool locked, Action<bool> done = null) => SetFlag(KLocked, locked, r => r.Locked = locked, done);

        public void SetChatEnabled(bool enabled, Action<bool> done = null) => SetFlag(KChat, enabled, r => r.ChatEnabled = enabled, done);

        public void SetInMatch(bool inMatch, Action<bool> done = null) => SetFlag(KInMatch, inMatch, r => r.InMatch = inMatch, done);

        /// <summary>Host only: writes a 0/1 lobby attribute and mirrors it into the local room on success.</summary>
        private void SetFlag(string key, bool value, Action<RoomListing> apply, Action<bool> done)
        {
            if (CurrentRoom == null || !IAmHost || !Ready()) { done?.Invoke(false); return; }
            string id = CurrentRoom.Id;
            var uo = new UpdateLobbyModificationOptions { LocalUserId = Me, LobbyId = id };
            if (!IsOk(Lobby.UpdateLobbyModification(ref uo, out LobbyModification m)) || m == null) { done?.Invoke(false); return; }
            AddInt(m, key, value ? 1 : 0);
            var upd = new UpdateLobbyOptions { LobbyModificationHandle = m };
            Lobby.UpdateLobby(ref upd, null, (ref UpdateLobbyCallbackInfo data) =>
            {
                m.Release();
                bool ok = IsOk(data.ResultCode);
                if (ok && CurrentRoom != null && CurrentRoom.Id == id) { apply(CurrentRoom); OnRoomChanged?.Invoke(); }
                done?.Invoke(ok);
            });
        }

        public void Kick(string puid, Action<bool> done = null)
        {
            if (CurrentRoom == null || !IAmHost || string.IsNullOrEmpty(puid) || !Ready()) { done?.Invoke(false); return; }
            var target = ProductUserId.FromString(puid);
            var k = new KickMemberOptions { LobbyId = CurrentRoom.Id, LocalUserId = Me, TargetUserId = target };
            Lobby.KickMember(ref k, null, (ref KickMemberCallbackInfo data) => done?.Invoke(IsOk(data.ResultCode)));
        }
    }
}
