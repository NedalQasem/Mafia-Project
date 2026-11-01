using System;
using System.Collections.Generic;
using System.Threading;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.P2P;
using Epic.OnlineServices.Platform;

namespace MafiaNight.RoomBot
{
    /// <summary>
    /// EOS for the room bot: the platform, the device login and the few lobby / P2P calls it needs. Everything runs on the thread that made the platform
    /// (EOS needs that); callbacks arrive inside <see cref="Tick"/>. No voice: the voice of the rooms is LiveKit, so the lobbies have no RTC room (and hold up to 64).
    /// </summary>
    internal sealed class Eos
    {
        public const string BucketId = "mafia_ar_v1";           // the game's lobby bucket

        private PlatformInterface platform;
        public ProductUserId Me { get; private set; }
        public string MeId => Me != null ? Me.ToString() : "";
        public LobbyInterface Lobby => platform.GetLobbyInterface();
        public P2PInterface P2P => platform.GetP2PInterface();

        public bool Start(string productId, string sandboxId, string deploymentId, string clientId, string clientSecret)
        {
            var init = new InitializeOptions { ProductName = "MafiaVoiceChatRoomBot", ProductVersion = "1.0" };
            var r = PlatformInterface.Initialize(ref init);
            if (r != Result.Success && r != Result.AlreadyConfigured) { Log.Error("EOS initialize: " + r); return false; }
            LoggingInterface.SetLogLevel(LogCategory.AllCategories, LogLevel.Warning);
            LoggingInterface.SetCallback((ref LogMessage m) => { if (!m.Message.ToString().Contains("already exist")) Log.Info("[eos " + m.Level + "] " + m.Category + ": " + m.Message); });
            var credentials = new ClientCredentials { ClientId = clientId, ClientSecret = clientSecret };
            var options = new Options { ProductId = productId, SandboxId = sandboxId, DeploymentId = deploymentId, ClientCredentials = credentials, Flags = PlatformFlags.DisableOverlay };
            platform = PlatformInterface.Create(ref options);
            if (platform == null) { Log.Error("EOS platform could not be created (check the ids)"); return false; }
            return true;
        }

        public void Tick() { platform.Tick(); }

        /// <summary>Pumps EOS until <paramref name="until"/> is true or the time is up.</summary>
        public bool Pump(Func<bool> until, int ms)
        {
            var end = DateTime.UtcNow.AddMilliseconds(ms);
            while (!until() && DateTime.UtcNow < end) { Tick(); Thread.Sleep(20); }
            return until();
        }

        private ulong expireNotif;
        private bool relogging;
        private DateTime lastRelogin = DateTime.MinValue;

        /// <summary>EOS ends a Connect login after about an hour; it says so shortly before. Without a new login every call fails with InvalidAuth.</summary>
        public void WatchAuth()
        {
            var o = new AddNotifyAuthExpirationOptions();
            expireNotif = platform.GetConnectInterface().AddNotifyAuthExpiration(ref o, null, (ref AuthExpirationCallbackInfo i) => { Log.Warn("The EOS login is about to expire: logging in again"); Relogin(); });
        }

        /// <summary>Logs in again with the device id (the same user id comes back). At most once every 10 seconds.</summary>
        public void Relogin()
        {
            if (relogging || DateTime.UtcNow - lastRelogin < TimeSpan.FromSeconds(10)) return;
            relogging = true; lastRelogin = DateTime.UtcNow;
            var login = new LoginOptions { Credentials = new Credentials { Type = ExternalCredentialType.DeviceidAccessToken, Token = null }, UserLoginInfo = new UserLoginInfo { DisplayName = "Mafia Office" } };
            platform.GetConnectInterface().Login(ref login, null, (ref LoginCallbackInfo li) =>
            {
                relogging = false;
                Log.Info("Connect.Login (again): " + li.ResultCode);
                if (li.ResultCode == Result.Success && li.LocalUserId != null && li.LocalUserId.IsValid()) Me = li.LocalUserId;
            });
        }

        /// <summary>Logs in with the device id of this machine (one identity per machine; the bot is that identity).</summary>
        public bool Login(string displayName)
        {
            var connect = platform.GetConnectInterface();
            bool created = false, done = false;
            var dev = new CreateDeviceIdOptions { DeviceModel = "room-bot" };
            connect.CreateDeviceId(ref dev, null, (ref CreateDeviceIdCallbackInfo info) => created = true);
            Pump(() => created, 10000);
            var login = new LoginOptions { Credentials = new Credentials { Type = ExternalCredentialType.DeviceidAccessToken, Token = null }, UserLoginInfo = new UserLoginInfo { DisplayName = displayName } };
            connect.Login(ref login, null, (ref LoginCallbackInfo li) => { Log.Info("Connect.Login: " + li.ResultCode); Me = li.LocalUserId; done = true; });
            return Pump(() => done, 30000) && Me != null && Me.IsValid();
        }

        // ------------------------------------------------------------------ lobbies

        public static void AddStr(LobbyModification m, string key, string value)
        {
            var o = new LobbyModificationAddAttributeOptions { Attribute = new AttributeData { Key = key, Value = new AttributeDataValue { AsUtf8 = value ?? "" } }, Visibility = LobbyAttributeVisibility.Public };
            m.AddAttribute(ref o);
        }

        public static void AddInt(LobbyModification m, string key, long value)
        {
            var o = new LobbyModificationAddAttributeOptions { Attribute = new AttributeData { Key = key, Value = new AttributeDataValue { AsInt64 = value } }, Visibility = LobbyAttributeVisibility.Public };
            m.AddAttribute(ref o);
        }

        public static void AddMemberStr(LobbyModification m, string key, string value)
        {
            var o = new LobbyModificationAddMemberAttributeOptions { Attribute = new AttributeData { Key = key, Value = new AttributeDataValue { AsUtf8 = value ?? "" } }, Visibility = LobbyAttributeVisibility.Public };
            m.AddMemberAttribute(ref o);
        }

        public static void AddMemberInt(LobbyModification m, string key, long value)
        {
            var o = new LobbyModificationAddMemberAttributeOptions { Attribute = new AttributeData { Key = key, Value = new AttributeDataValue { AsInt64 = value } }, Visibility = LobbyAttributeVisibility.Public };
            m.AddMemberAttribute(ref o);
        }

        /// <summary>Creates a lobby (no voice room) and gives it its attributes; the callback gets the lobby id or null.</summary>
        public void CreateLobby(uint capacity, Action<LobbyModification> fill, Action<string> done)
        {
            var create = new CreateLobbyOptions
            {
                LocalUserId = Me, MaxLobbyMembers = capacity, PermissionLevel = LobbyPermissionLevel.Publicadvertised, PresenceEnabled = false, AllowInvites = true,
                BucketId = BucketId, DisableHostMigration = true, EnableRTCRoom = false, EnableJoinById = true, CrossplayOptOut = false,
            };
            Lobby.CreateLobby(ref create, null, (ref CreateLobbyCallbackInfo c) =>
            {
                if (c.ResultCode != Result.Success) { Log.Warn("CreateLobby: " + c.ResultCode); done(null); return; }
                string id = c.LobbyId.ToString();
                Update(id, fill, ok => done(ok ? id : null));
            });
        }

        public void Update(string lobbyId, Action<LobbyModification> fill, Action<bool> done)
        {
            var uo = new UpdateLobbyModificationOptions { LocalUserId = Me, LobbyId = lobbyId };
            if (Lobby.UpdateLobbyModification(ref uo, out LobbyModification m) != Result.Success || m == null) { done(false); return; }
            fill(m);
            var upd = new UpdateLobbyOptions { LobbyModificationHandle = m };
            Lobby.UpdateLobby(ref upd, null, (ref UpdateLobbyCallbackInfo d) => { m.Release(); done(d.ResultCode == Result.Success); });
        }

        public void Destroy(string lobbyId)
        {
            var o = new DestroyLobbyOptions { LocalUserId = Me, LobbyId = lobbyId };
            Lobby.DestroyLobby(ref o, null, (ref DestroyLobbyCallbackInfo d) => { });
        }

        public void Kick(string lobbyId, string puid)
        {
            var target = ProductUserId.FromString(puid);
            if (target == null) return;
            var o = new KickMemberOptions { LobbyId = lobbyId, LocalUserId = Me, TargetUserId = target };
            Lobby.KickMember(ref o, null, (ref KickMemberCallbackInfo d) => Log.Info("Kick: " + d.ResultCode));
        }

        public sealed class Member { public string Puid = "", Pf = "", Name = ""; public bool Keeper; }

        /// <summary>The members of a lobby we own, read from EOS (null when the lobby is gone).</summary>
        public List<Member> Members(string lobbyId)
        {
            var o = new CopyLobbyDetailsHandleOptions { LobbyId = lobbyId, LocalUserId = Me };
            if (Lobby.CopyLobbyDetailsHandle(ref o, out LobbyDetails d) != Result.Success || d == null) return null;
            try
            {
                var list = new List<Member>();
                var co = new LobbyDetailsGetMemberCountOptions();
                uint n = d.GetMemberCount(ref co);
                for (uint i = 0; i < n; i++)
                {
                    var mo = new LobbyDetailsGetMemberByIndexOptions { MemberIndex = i };
                    var puid = d.GetMemberByIndex(ref mo);
                    if (puid == null) continue;
                    list.Add(new Member { Puid = puid.ToString(), Pf = MemberStr(d, puid, "pf") ?? "", Name = MemberStr(d, puid, "name") ?? "لاعب", Keeper = puid.ToString() == MeId });
                }
                return list;
            }
            finally { d.Release(); }
        }

        private static string MemberStr(LobbyDetails d, ProductUserId who, string key)
        {
            var o = new LobbyDetailsCopyMemberAttributeByKeyOptions { TargetUserId = who, AttrKey = key };
            if (d.CopyMemberAttributeByKey(ref o, out Epic.OnlineServices.Lobby.Attribute? a) != Result.Success || a == null || a.Value.Data == null) return null;
            return a.Value.Data.Value.Value.AsUtf8;
        }

        public void Shutdown()
        {
            try { platform?.Release(); PlatformInterface.Shutdown(); } catch (Exception e) { Log.Warn("Shutdown: " + e.Message); }
        }
    }

    internal static class Log
    {
        public static void Info(string s) => Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + s);
        public static void Warn(string s) => Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " WARN " + s);
        public static void Error(string s) => Console.Error.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " ERROR " + s);
    }
}
