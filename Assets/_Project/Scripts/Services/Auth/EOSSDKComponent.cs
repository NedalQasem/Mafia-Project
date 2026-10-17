using Epic.OnlineServices;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.Platform;
using System;
using System.Runtime.InteropServices;
using System.Text;
using MafiaNight.Core;
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
    public class EOSSDKComponent : MonoBehaviour
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

        private void Awake()
        {
            Application.runInBackground = true;

#if UNITY_EDITOR
            m_LibraryPointer = LoadLibrary(EditorLibraryPath);
            if (m_LibraryPointer == IntPtr.Zero)
            {
                throw new Exception("Failed to load library " + EditorLibraryPath);
            }

            Bindings.Hook(m_LibraryPointer, GetProcAddress);
#endif
        }

        private void Start()
        {
            if (s_PlatformInterface == null && !InitializePlatform()) return;
            StartLogin();
        }

        // Calling tick on a regular interval is required for callbacks to work.
        private void Update()
        {
            if (s_PlatformInterface == null) return;

            m_PlatformTickTimer += Time.deltaTime;
            if (m_PlatformTickTimer >= c_PlatformTickInterval)
            {
                m_PlatformTickTimer = 0;
                s_PlatformInterface.Tick();
            }
        }

#if UNITY_EDITOR
        // Release the platform when stopping play mode in the Editor
        private void OnDestroy()
        {
            ShutdownPlatform();
        }
#endif

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
                Log.Warning("Platform already configured - continuing with existing configuration", Tag);
            }
            else if (initializeResult != Result.Success)
            {
                Log.Error("Failed to initialize EOS SDK: " + initializeResult, Tag);
                OnLoginFailed?.Invoke(initializeResult);
                return false;
            }

            LoggingInterface.SetLogLevel(LogCategory.AllCategories, sdkLogLevel);
            LoggingInterface.SetCallback((ref LogMessage logMessage) =>
            {
                string message = $"{logMessage.Category}: {logMessage.Message}";
                if (logMessage.Level <= LogLevel.Error) Log.Error(message, Tag);
                else if (logMessage.Level == LogLevel.Warning) Log.Warning(message, Tag);
                else Log.Info(message, Tag);
            });

            var options = new Options()
            {
                ProductId = m_ProductId,
                SandboxId = m_SandboxId,
                DeploymentId = m_DeploymentId,
                ClientCredentials = new ClientCredentials()
                {
                    ClientId = m_ClientId,
                    ClientSecret = m_ClientSecret
                },
                Flags = PlatformFlags.DisableOverlay
            };

            s_PlatformInterface = PlatformInterface.Create(ref options);
            if (s_PlatformInterface == null)
            {
                Log.Error("Failed to create EOS platform (check Product/Sandbox/Deployment/Client IDs)", Tag);
                OnLoginFailed?.Invoke(Result.InvalidParameters);
                return false;
            }

            Log.Info("✅ Platform initialized successfully", Tag);
            return true;
        }

        private Result InitializeSdk()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: the native library must be loaded and the Java side initialized with the activity first.
            using (var system = new AndroidJavaClass("java.lang.System"))
            {
                system.CallStatic("loadLibrary", "EOSSDK");
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
                PlatformInterface.Shutdown();
            }

#if UNITY_EDITOR
            if (m_LibraryPointer != IntPtr.Zero)
            {
                Bindings.Unhook();

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
            if (s_PlatformInterface == null)
            {
                Log.Error("StartLogin called before the platform was initialized", Tag);
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
