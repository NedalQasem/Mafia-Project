using System;
using System.Threading;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.Platform;

namespace MafiaNight.RoomBot
{
    /// <summary>Step 1 of the room bot: proves that EOS runs from a console program (platform, device login, a lobby with a voice room for 64).</summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string productId = Env("EOS_PRODUCT_ID"), sandboxId = Env("EOS_SANDBOX_ID"), deploymentId = Env("EOS_DEPLOYMENT_ID"), clientId = Env("EOS_CLIENT_ID"), clientSecret = Env("EOS_CLIENT_SECRET");
            if (productId == null || sandboxId == null || deploymentId == null || clientId == null || clientSecret == null) { Console.Error.WriteLine("EOS_PRODUCT_ID, EOS_SANDBOX_ID, EOS_DEPLOYMENT_ID, EOS_CLIENT_ID and EOS_CLIENT_SECRET are needed."); return 2; }

            var init = new InitializeOptions { ProductName = "MafiaVoiceChatRoomBot", ProductVersion = "1.0" };
            var r = PlatformInterface.Initialize(ref init);
            Console.WriteLine("Initialize: " + r);
            if (r != Result.Success && r != Result.AlreadyConfigured) return 3;
            LoggingInterface.SetLogLevel(LogCategory.AllCategories, LogLevel.Warning);
            LoggingInterface.SetCallback((ref LogMessage m) => Console.WriteLine("[eos " + m.Level + "] " + m.Category + ": " + m.Message));

            var credentials = new ClientCredentials { ClientId = clientId, ClientSecret = clientSecret };
            PlatformInterface platform;
#if EOS_PLATFORM_WINDOWS_64
            var options = new WindowsOptions
            {
                ProductId = productId, SandboxId = sandboxId, DeploymentId = deploymentId, ClientCredentials = credentials, Flags = PlatformFlags.DisableOverlay,
                RTCOptions = new WindowsRTCOptions { PlatformSpecificOptions = new WindowsRTCOptionsPlatformSpecificOptions { XAudio29DllPath = System.IO.Path.Combine(AppContext.BaseDirectory, "xaudio2_9redist.dll") }, BackgroundMode = RTCBackgroundMode.LeaveRooms },
            };
            platform = PlatformInterface.Create(ref options);
#else
            var options = new Options { ProductId = productId, SandboxId = sandboxId, DeploymentId = deploymentId, ClientCredentials = credentials, Flags = PlatformFlags.DisableOverlay, RTCOptions = new RTCOptions { BackgroundMode = RTCBackgroundMode.LeaveRooms } };
            platform = PlatformInterface.Create(ref options);
#endif
            if (platform == null) { Console.Error.WriteLine("Platform create failed"); return 4; }
            Console.WriteLine("Platform created");

            // everything runs on this thread: EOS (RTC especially) must be ticked by the thread that made the platform, and the callbacks arrive inside Tick
            bool Pump(Func<bool> until, int ms) { var end = DateTime.UtcNow.AddMilliseconds(ms); while (!until() && DateTime.UtcNow < end) { platform.Tick(); Thread.Sleep(20); } return until(); }

            ProductUserId me = null; bool loggedIn = false;
            var connect = platform.GetConnectInterface();
            var dev = new CreateDeviceIdOptions { DeviceModel = "room-bot" };
            connect.CreateDeviceId(ref dev, null, (ref CreateDeviceIdCallbackInfo info) =>
            {
                Console.WriteLine("CreateDeviceId: " + info.ResultCode);
            });
            Pump(() => false, 1500);
            var login = new LoginOptions { Credentials = new Credentials { Type = ExternalCredentialType.DeviceidAccessToken, Token = null }, UserLoginInfo = new UserLoginInfo { DisplayName = "Mafia Office" } };
            connect.Login(ref login, null, (ref LoginCallbackInfo li) => { Console.WriteLine("Connect.Login: " + li.ResultCode + " user " + li.LocalUserId); me = li.LocalUserId; loggedIn = true; });
            if (!Pump(() => loggedIn, 30000) || me == null || !me.IsValid()) { Console.Error.WriteLine("login did not finish"); return 5; }

            var lobby = platform.GetLobbyInterface();
            int failures = 0;
            foreach (var (members, rtc) in new[] { (16u, true), (64u, true), (64u, false) })
            {
                string lobbyId = null; bool created = false;
                var create = new CreateLobbyOptions
                {
                    LocalUserId = me, MaxLobbyMembers = members, PermissionLevel = LobbyPermissionLevel.Publicadvertised, PresenceEnabled = false, AllowInvites = true,
                    BucketId = "mafia_ar_v1", DisableHostMigration = false, EnableRTCRoom = rtc, RTCRoomJoinActionType = LobbyRTCRoomJoinActionType.ManualJoin, EnableJoinById = true, CrossplayOptOut = false,
                };
                lobby.CreateLobby(ref create, null, (ref CreateLobbyCallbackInfo c) => { Console.WriteLine("CreateLobby members=" + members + " voice=" + rtc + ": " + c.ResultCode); if (c.ResultCode == Result.Success) lobbyId = c.LobbyId.ToString(); created = true; });
                Pump(() => created, 30000);
                if (lobbyId == null) { failures++; continue; }
                bool destroyed = false;
                var destroy = new DestroyLobbyOptions { LocalUserId = me, LobbyId = lobbyId };
                lobby.DestroyLobby(ref destroy, null, (ref DestroyLobbyCallbackInfo d) => { destroyed = true; });
                Pump(() => destroyed, 15000);
            }
            platform.Release();
            PlatformInterface.Shutdown();
            return failures;
        }

        private static string Env(string name) { var v = Environment.GetEnvironmentVariable(name); return string.IsNullOrWhiteSpace(v) ? null : v.Trim(); }
    }
}
