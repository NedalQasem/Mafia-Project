using Epic.OnlineServices;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.Platform;
using System;
using System.Runtime.InteropServices;
using System.Text;
using MafiaNight.Core;
using MafiaNight.Services.Config;
using UnityEngine;
using EosAuth = Epic.OnlineServices.Auth;
using EosConnect = Epic.OnlineServices.Connect;

namespace MafiaNight.Services.Auth
{
    /// <summary>
    /// How the local player gets an EOS Product User ID (PUID).
    /// </summary>
    public enum EosLoginMode
    {
        /// <summary>Anonymous per-device account (EOS Connect + Device ID). Works on mobile, no Epic account needed.</summary>
        DeviceId,

        /// <summary>
        /// Editor only: EOS DevAuthTool credential → Epic ID token → EOS Connect.
        /// Use it to run several players on one PC (each instance with a different credential name),
        /// because every instance on the same PC shares the same Device ID.
        /// </summary>
        DevAuthTool
    }

    /// <summary>
    /// Owns the EOS PlatformInterface: initializes the SDK (incl. Android), ticks it and logs the
    /// local player into EOS Connect, producing a <see cref="ProductUserId"/> (needed by Lobbies, P2P and RTC voice).
    /// Other systems react to <see cref="OnLoggedIn"/> / <see cref="OnLoginFailed"/>.
    /// </summary>
    public class EOSSDKComponent : MonoBehaviour, IEosPlatform
    {
        private const string Tag = "EOS";

        // Set these values as appropriate. For more information, see the Developer Portal documentation.
        public string m_ProductName = "MyUnityApplication";
        public string m_ProductVersion = "1.0";
        public string m_ProductId = "";
        public string m_SandboxId = "";
        public string m_DeploymentId = "";
        public string m_ClientId = "";
        public string m_ClientSecret = "";

        [Header("Login")]
        [Tooltip("Builds always use DeviceId. DevAuthTool is honoured in the Editor only.")]
        [SerializeField] private EosLoginMode loginMode = EosLoginMode.DeviceId;

        [Header("Voice (RTC)")]
        [Tooltip("What voice rooms do when the app goes to the background. LeaveRooms is the safe default on mobile " +
                 "(background microphone needs extra OS permissions).")]
        [SerializeField] private RTCBackgroundMode rtcBackgroundMode = RTCBackgroundMode.LeaveRooms;

        [Tooltip("EOS SDK log verbosity. Raise to Info/Verbose when diagnosing login or network issues.")]
        [SerializeField] private LogLevel sdkLogLevel = LogLevel.Error;

        [Header("DevAuthTool (Editor only)")]
        [SerializeField] private string devAuthHost = "localhost:8080";
        [SerializeField] private string devAuthCredentialName = "TestUser1";

        /// <summary>Raised on the first successful login (not on silent token refreshes).</summary>
        public event Action<ProductUserId> OnLoggedIn;
        public event Action<Result> OnLoginFailed;

        public PlatformInterface Platform => s_PlatformInterface;
        public ProductUserId LocalUserId { get; private set; }
        public bool IsLoggedIn => LocalUserId != null && LocalUserId.IsValid();
        public EosLoginMode ActiveLoginMode => Application.isEditor ? loginMode : EosLoginMode.DeviceId;

        private static PlatformInterface s_PlatformInterface;
        // True between a successful EOS_Initialize and EOS_Shutdown. Must be shut down before the
        // Editor unloads the DLL, even if platform creation failed - otherwise SDK threads hang the Editor.
        private static bool s_SdkInitialized;
        private const float c_PlatformTickInterval = 0.1f;
        private float m_PlatformTickTimer = 0f;
        private ulong _authExpirationNotifyId;
        private bool _isLoggingIn;

        // In the Editor the SDK is loaded/unloaded dynamically between play sessions.
#if UNITY_EDITOR
        private const string EditorLibraryPath = "Assets/ThirdParty/EOS-SDK/SDK/Bin/EOSSDK-Win64-Shipping.dll";

        [DllImport("Kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpLibFileName);

        [DllImport("Kernel32.dll")]
        private static extern int FreeLibrary(IntPtr hLibModule);

        [DllImport("Kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        private IntPtr m_LibraryPointer;
#endif

        #region Lifecycle

        private bool _registeredService;

        private void Awake()
        {
            Application.runInBackground = true;

            // the Dev / Live environment asset wins over values saved in the scene
            var env = EnvironmentConfig.Current;
            if (env != null && env.IsEosComplete)
            {
                m_ProductName = env.productName; m_ProductVersion = env.productVersion;
                m_ProductId = env.productId; m_SandboxId = env.sandboxId; m_DeploymentId = env.deploymentId;
                m_ClientId = env.clientId; m_ClientSecret = env.clientSecret;
                Log.Info($"EOS environment: {env.environmentName}", Tag);
            }
            else
            {
                Log.Warning("EnvironmentConfig missing or incomplete - using the IDs saved in the scene", Tag);
            }

#if UNITY_EDITOR
            // Mafia > Backend Status can switch the login without touching the scene (several players on one PC)
            if (UnityEditor.EditorPrefs.GetString("MafiaNight.EosLoginMode", "") == "DevAuthTool")
            {
                loginMode = EosLoginMode.DevAuthTool;
                devAuthCredentialName = UnityEditor.EditorPrefs.GetString("MafiaNight.DevAuthCredential", devAuthCredentialName);
            }
#endif

            if (!ServiceLocator.IsRegistered<IEosPlatform>())
            {
                ServiceLocator.Register<IEosPlatform>(this);
                _registeredService = true;
            }

#if UNITY_EDITOR
            m_LibraryPointer = LoadLibrary(EditorLibraryPath);
            if (m_LibraryPointer == IntPtr.Zero)
            {
                throw new Exception("Failed to load library " + EditorLibraryPath);
            }

            Bindings.Hook(m_LibraryPointer, GetProcAddress);
            WindowsBindings.Hook(m_LibraryPointer, GetProcAddress); // Windows-only functions (EOS_Platform_Create with RTC options)
#endif
        }

        private void Start()
        {
            if (s_PlatformInterface == null && !InitializePlatform()) return;
            if (LoginGate.Hold) { LoginGate.Released += StartLogin; return; }       // the very first start: the player chooses how to come in first
            StartLogin();
        }

        // Calling tick on a regular interval is required for callbacks to work.
        private void Update()
        {
            if (s_PlatformInterface == null) return;
            CheckNetworkStatus();

            m_PlatformTickTimer += Time.deltaTime;
            if (m_PlatformTickTimer >= c_PlatformTickInterval)
            {
                m_PlatformTickTimer = 0;
                s_PlatformInterface.Tick();
            }
        }

        // ---- the phone's life cycle: EOS has to be told when the game goes to the background and when it comes back, and when the network goes away or returns.
        // Without it, after the game was in the background (a swipe to the home screen, a call) EOS could keep its old connections and every lobby call failed until the game was restarted.

        private bool lastReachable = true;
        private float nextNetworkCheck;

        private void OnApplicationPause(bool paused)
        {
            if (s_PlatformInterface == null) return;
            try
            {
                var r = s_PlatformInterface.SetApplicationStatus(paused ? ApplicationStatus.BackgroundSuspended : ApplicationStatus.Foreground);
                Log.Info("EOS application status: " + (paused ? "background" : "foreground") + " -> " + r, Tag);
                if (!paused) { lastReachable = Application.internetReachability != NetworkReachability.NotReachable; s_PlatformInterface.SetNetworkStatus(lastReachable ? NetworkStatus.Online : NetworkStatus.Offline); }
            }
            catch (Exception e) { Log.Warning("EOS application status: " + e.Message, Tag); }
        }

        private void CheckNetworkStatus()
        {
            if (s_PlatformInterface == null || Time.unscaledTime < nextNetworkCheck) return;
            nextNetworkCheck = Time.unscaledTime + 1f;
            bool reachable = Application.internetReachability != NetworkReachability.NotReachable;
            if (reachable == lastReachable) return;
            lastReachable = reachable;
            try { Log.Info("EOS network status: " + (reachable ? "online" : "offline") + " -> " + s_PlatformInterface.SetNetworkStatus(reachable ? NetworkStatus.Online : NetworkStatus.Offline), Tag); }
            catch (Exception e) { Log.Warning("EOS network status: " + e.Message, Tag); }
        }

        private void OnDestroy()
        {
            LoginGate.Released -= StartLogin;
            if (_registeredService) ServiceLocator.Unregister<IEosPlatform>(this);
#if UNITY_EDITOR
            // Release the platform when stopping play mode in the Editor
            ShutdownPlatform();
#endif
        }

        private void OnApplicationQuit()
        {
            ShutdownPlatform();
        }

        #endregion

        #region Platform

        private bool InitializePlatform()
        {
            Result initializeResult = InitializeSdk();

            if (initializeResult == Result.AlreadyConfigured)
            {
                // expected from the second play-mode run on in the Editor: the SDK is initialized once per process
                Log.Info("Platform already configured - continuing with existing configuration", Tag);
            }
            else if (initializeResult != Result.Success)
            {
                Log.Error("Failed to initialize EOS SDK: " + initializeResult, Tag);
                OnLoginFailed?.Invoke(initializeResult);
                return false;
            }
            s_SdkInitialized = true;

            LoggingInterface.SetLogLevel(LogCategory.AllCategories, sdkLogLevel);
            LoggingInterface.SetCallback((ref LogMessage logMessage) =>
            {
                string message = $"{logMessage.Category}: {logMessage.Message}";
                // Expected on every launch after the first (DeviceID already created) - not an error
                if (message.Contains("DeviceId access credentials already exist")) { Log.Info(message, Tag); return; }
                // The RTC control channel drops (1006) when a lobby with a voice room is left, closed, or play mode stops: a warning, not a failure
                if (message.Contains("LogEOSRTC") && message.Contains("Control channel closed")) { Log.Warning(message, Tag); return; }
                // On shutdown (quit or leaving play mode) the voice threads are still winding down when the SDK stops: noise, not a failure
                if (message.Contains("The WebRTC thread is still alive")) { Log.Warning(message, Tag); return; }
                if (logMessage.Level <= LogLevel.Error) Log.Error(message, Tag);
                else if (logMessage.Level == LogLevel.Warning) Log.Warning(message, Tag);
                else Log.Info(message, Tag);
            });

            try
            {
                s_PlatformInterface = CreatePlatform();
            }
            catch (Exception e)
            {
                Log.Error($"Exception while creating EOS platform: {e}", Tag);
                s_PlatformInterface = null;
            }

            if (s_PlatformInterface == null)
            {
                Log.Error("Failed to create EOS platform (check Product/Sandbox/Deployment/Client IDs)", Tag);
                OnLoginFailed?.Invoke(Result.InvalidParameters);
                return false;
            }

            Log.Info("✅ Platform initialized successfully", Tag);
            return true;
        }

        /// <summary>
        /// Creates the platform with RTC (voice) enabled. Without RTCOptions, EOS disables voice
        /// and lobbies with an RTC room fail ("RTC is locally disabled").
        /// </summary>
        private PlatformInterface CreatePlatform()
        {
            var credentials = new ClientCredentials { ClientId = m_ClientId, ClientSecret = m_ClientSecret };

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            // Windows voice needs the path to XAudio 2.9 shipped with the SDK
            var windowsOptions = new WindowsOptions
            {
                ProductId = m_ProductId,
                SandboxId = m_SandboxId,
                DeploymentId = m_DeploymentId,
                ClientCredentials = credentials,
                Flags = PlatformFlags.DisableOverlay,
                RTCOptions = new WindowsRTCOptions
                {
                    PlatformSpecificOptions = new WindowsRTCOptionsPlatformSpecificOptions
                    {
                        XAudio29DllPath = GetXAudioDllPath()
                    },
                    BackgroundMode = rtcBackgroundMode
                }
            };
            return PlatformInterface.Create(ref windowsOptions);
#else
            var options = new Options
            {
                ProductId = m_ProductId,
                SandboxId = m_SandboxId,
                DeploymentId = m_DeploymentId,
                ClientCredentials = credentials,
                Flags = PlatformFlags.DisableOverlay,
                RTCOptions = new RTCOptions { BackgroundMode = rtcBackgroundMode }
            };
            return PlatformInterface.Create(ref options);
#endif
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private static string GetXAudioDllPath()
        {
#if UNITY_EDITOR
            const string relative = "Assets/ThirdParty/EOS-SDK/SDK/Bin/x64/xaudio2_9redist.dll";
            return System.IO.Path.GetFullPath(relative);
#else
            // Unity copies x64 native plugins next to the game data in Windows builds
            return System.IO.Path.Combine(Application.dataPath, "Plugins", "x86_64", "xaudio2_9redist.dll");
#endif
        }
#endif

        private Result InitializeSdk()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: the native library must be loaded and the Java side initialized with the activity first.
            // loaded from our own Java class: a JNI call to System.loadLibrary has no caller class and searches the system namespace only
            using (var loader = new AndroidJavaClass("com.mafianight.eos.EosLibraryLoader"))
            {
                loader.CallStatic("load");
            }
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var eosSdk = new AndroidJavaClass("com.epicgames.mobile.eossdk.EOSSDK"))
            {
                eosSdk.CallStatic("init", activity);
            }

            var androidOptions = new AndroidInitializeOptions()
            {
                ProductName = m_ProductName,
                ProductVersion = m_ProductVersion,
                SystemInitializeOptions = new AndroidInitializeOptionsSystemInitializeOptions()
            };
            return PlatformInterface.Initialize(ref androidOptions);
#else
            var initializeOptions = new InitializeOptions()
            {
                ProductName = m_ProductName,
                ProductVersion = m_ProductVersion
            };
            return PlatformInterface.Initialize(ref initializeOptions);
#endif
        }

        private void ShutdownPlatform()
        {
            if (s_PlatformInterface != null)
            {
                if (_authExpirationNotifyId != 0)
                {
                    s_PlatformInterface.GetConnectInterface().RemoveNotifyAuthExpiration(_authExpirationNotifyId);
                    _authExpirationNotifyId = 0;
                }

                Log.Info("Releasing platform", Tag);
                s_PlatformInterface.Release();
                s_PlatformInterface = null;
            }

            // Shut the SDK down whenever it was initialized - also when platform creation failed.
            if (s_SdkInitialized)
            {
                PlatformInterface.Shutdown();
                s_SdkInitialized = false;
            }

#if UNITY_EDITOR
            if (m_LibraryPointer != IntPtr.Zero)
            {
                Bindings.Unhook();
                WindowsBindings.Unhook();

                // Free until the module ref count is 0
                while (FreeLibrary(m_LibraryPointer) != 0) { }
                m_LibraryPointer = IntPtr.Zero;
            }
#endif
        }

        #endregion

        #region Login

        /// <summary>Logs into EOS Connect using <see cref="ActiveLoginMode"/>. Safe to call again to retry.</summary>
        public void StartLogin()
        {
            if (s_PlatformInterface == null && !InitializePlatform())
            {
                Log.Error("StartLogin: the EOS platform could not be created", Tag);
                return;
            }
            if (_isLoggingIn) return;
            _isLoggingIn = true;

            Log.Info($"Logging in ({ActiveLoginMode})...", Tag);

            if (ActiveLoginMode == EosLoginMode.DevAuthTool)
                LoginWithDevAuthTool();
            else
                LoginWithDeviceId();
        }

        // ---------- Device ID ----------

        private void LoginWithDeviceId()
        {
            var options = new EosConnect.CreateDeviceIdOptions
            {
                DeviceModel = TruncateUtf8(SystemInfo.deviceModel, 64)
            };

            s_PlatformInterface.GetConnectInterface().CreateDeviceId(ref options, null,
                (ref EosConnect.CreateDeviceIdCallbackInfo info) =>
                {
                    // DuplicateNotAllowed = this device already has an ID → just log in
                    if (info.ResultCode == Result.Success || info.ResultCode == Result.DuplicateNotAllowed)
                    {
                        ConnectLogin(ExternalCredentialType.DeviceidAccessToken, null, GetLoginDisplayName());
                    }
                    else if (Common.IsOperationComplete(info.ResultCode))
                    {
                        FailLogin("CreateDeviceId", info.ResultCode);
                    }
                });
        }

        // ---------- DevAuthTool (Editor) ----------

        private void LoginWithDevAuthTool()
        {
            var options = new EosAuth.LoginOptions
            {
                Credentials = new EosAuth.Credentials
                {
                    Type = EosAuth.LoginCredentialType.Developer,
                    Id = devAuthHost,             // DevAuthTool address
                    Token = devAuthCredentialName // Credential name created in DevAuthTool
                },
                ScopeFlags = EosAuth.AuthScopeFlags.BasicProfile
            };

            s_PlatformInterface.GetAuthInterface().Login(ref options, null,
                (ref EosAuth.LoginCallbackInfo info) =>
                {
                    if (info.ResultCode == Result.Success)
                    {
                        var copyOptions = new EosAuth.CopyIdTokenOptions { AccountId = info.LocalUserId };
                        var copyResult = s_PlatformInterface.GetAuthInterface().CopyIdToken(ref copyOptions, out var idToken);
                        if (copyResult != Result.Success || !idToken.HasValue)
                        {
                            FailLogin("Auth.CopyIdToken", copyResult);
                            return;
                        }

                        ConnectLogin(ExternalCredentialType.EpicIdToken, idToken.Value.JsonWebToken, null);
                    }
                    else if (Common.IsOperationComplete(info.ResultCode))
                    {
                        if (info.ResultCode == Result.NotFound)
                        {
                            Log.Error($"DevAuthTool has no credential named '{devAuthCredentialName}' " +
                                      $"(or it is not running on {devAuthHost})", Tag);
                        }
                        FailLogin("Auth.Login (DevAuthTool)", info.ResultCode);
                    }
                });
        }

        // ---------- EOS Connect ----------

        private void ConnectLogin(ExternalCredentialType type, Utf8String token, string displayName)
        {
            var options = new EosConnect.LoginOptions
            {
                Credentials = new EosConnect.Credentials { Type = type, Token = token }
            };

            if (!string.IsNullOrEmpty(displayName))
            {
                options.UserLoginInfo = new EosConnect.UserLoginInfo { DisplayName = displayName };
            }

            s_PlatformInterface.GetConnectInterface().Login(ref options, null,
                (ref EosConnect.LoginCallbackInfo info) =>
                {
                    if (info.ResultCode == Result.Success)
                    {
                        CompleteLogin(info.LocalUserId);
                    }
                    else if (info.ResultCode == Result.InvalidUser)
                    {
                        // First time for this identity: create the EOS product user
                        CreateConnectUser(info.ContinuanceToken);
                    }
                    else if (Common.IsOperationComplete(info.ResultCode))
                    {
                        FailLogin("Connect.Login", info.ResultCode);
                    }
                });
        }

        private void CreateConnectUser(ContinuanceToken continuanceToken)
        {
            var options = new EosConnect.CreateUserOptions { ContinuanceToken = continuanceToken };

            s_PlatformInterface.GetConnectInterface().CreateUser(ref options, null,
                (ref EosConnect.CreateUserCallbackInfo info) =>
                {
                    if (info.ResultCode == Result.Success)
                    {
                        Log.Info("Created new EOS product user", Tag);
                        CompleteLogin(info.LocalUserId);
                    }
                    else if (Common.IsOperationComplete(info.ResultCode))
                    {
                        FailLogin("Connect.CreateUser", info.ResultCode);
                    }
                });
        }

        private void CompleteLogin(ProductUserId userId)
        {
            _isLoggingIn = false;
            bool isRefresh = IsLoggedIn && LocalUserId.ToString() == userId.ToString();
            LocalUserId = userId;

            RegisterAuthExpiration();

            if (isRefresh)
            {
                Log.Info("Connect token refreshed", Tag);
                return;
            }

            Log.Info($"✅ Logged in. ProductUserId: {userId}", Tag);
            OnLoggedIn?.Invoke(userId);
        }

        private void FailLogin(string step, Result result)
        {
            _isLoggingIn = false;
            Log.Error($"{step} failed: {result}", Tag);
            OnLoginFailed?.Invoke(result);
        }

        /// <summary>EOS Connect tokens expire (~1h): re-login silently before they do.</summary>
        private void RegisterAuthExpiration()
        {
            if (_authExpirationNotifyId != 0) return;

            var options = new EosConnect.AddNotifyAuthExpirationOptions();
            _authExpirationNotifyId = s_PlatformInterface.GetConnectInterface().AddNotifyAuthExpiration(ref options, null,
                (ref EosConnect.AuthExpirationCallbackInfo info) =>
                {
                    Log.Info("Connect auth about to expire - refreshing", Tag);
                    StartLogin();
                });
        }

        /// <summary>
        /// Returns the EOS Connect ID token (JWT) of the logged-in player, e.g. for PlayFab LoginWithOpenIdConnect.
        /// </summary>
        public bool TryGetIdToken(out string jwt)
        {
            jwt = null;
            if (!IsLoggedIn) return false;

            var options = new EosConnect.CopyIdTokenOptions { LocalUserId = LocalUserId };
            var result = s_PlatformInterface.GetConnectInterface().CopyIdToken(ref options, out var idToken);
            if (result != Result.Success || !idToken.HasValue) return false;

            jwt = idToken.Value.JsonWebToken;
            return !string.IsNullOrEmpty(jwt);
        }

        #endregion

        #region Helpers

        private static string GetLoginDisplayName()
        {
            string name = PlayerPrefs.GetString(GameConstants.PREF_PLAYER_CUSTOM_NAME, "");
            if (string.IsNullOrWhiteSpace(name)) name = "Player";
            return TruncateUtf8(name, EosConnect.ConnectInterface.USERLOGININFO_DISPLAYNAME_MAX_LENGTH);
        }

        /// <summary>Cuts a string so its UTF-8 encoding fits in <paramref name="maxBytes"/> (Arabic chars are 2 bytes).</summary>
        private static string TruncateUtf8(string value, int maxBytes)
        {
            if (string.IsNullOrEmpty(value)) return "Unknown";
            while (Encoding.UTF8.GetByteCount(value) > maxBytes)
            {
                value = value.Substring(0, value.Length - 1);
            }
            return value;
        }

        #endregion
    }
}
