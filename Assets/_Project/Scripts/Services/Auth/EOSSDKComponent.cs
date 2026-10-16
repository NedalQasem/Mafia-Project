using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.Platform;
using System;
using System.Runtime.InteropServices;
using MafiaNight.Core;
using UnityEngine;

namespace MafiaNight.Services.Auth
{
    /// <summary>
    /// Owns the EOS PlatformInterface: initializes the SDK, ticks it and performs the EOS Auth login.
    /// Other systems react to <see cref="OnLoggedIn"/> / <see cref="OnLoginFailed"/> instead of being called directly.
    /// Based on the EOS C# SDK sample component.
    /// TODO: Developer (DevAuthTool) login only works in the Editor - switch to Connect + DeviceID for mobile builds.
    /// </summary>
    public class EOSSDKComponent : MonoBehaviour
    {
        // Set these values as appropriate. For more information, see the Developer Portal documentation.
        public string m_ProductName = "MyUnityApplication";
        public string m_ProductVersion = "1.0";
        public string m_ProductId = "";
        public string m_SandboxId = "";
        public string m_DeploymentId = "";
        public string m_ClientId = "";
        public string m_ClientSecret = "";
        public LoginCredentialType m_LoginCredentialType = LoginCredentialType.AccountPortal;
        // These fields correspond to Credentials.Id and Credentials.Token; their use differs based on the login type.
        public string m_LoginCredentialId = null;
        public string m_LoginCredentialToken = null;

        [Header("Developer Login (DevAuthTool)")]
        [SerializeField] private string devAuthHost = "localhost:8080";
        [SerializeField] private string devAuthCredentialName = "TestUser1";

        public event Action<EpicAccountId> OnLoggedIn;
        public event Action<Result> OnLoginFailed;

        public PlatformInterface Platform => s_PlatformInterface;

        private const string Tag = "EOS";
        private static PlatformInterface s_PlatformInterface;
        private const float c_PlatformTickInterval = 0.1f;
        private float m_PlatformTickTimer = 0f;

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
            // Already initialized (happens when stopping/starting in Editor)
            if (s_PlatformInterface != null)
            {
                Log.Warning("Platform already initialized - skipping initialization", Tag);
                StartLogin();
                return;
            }

            var initializeOptions = new InitializeOptions()
            {
                ProductName = m_ProductName,
                ProductVersion = m_ProductVersion
            };

            var initializeResult = PlatformInterface.Initialize(ref initializeOptions);

            if (initializeResult == Result.AlreadyConfigured)
            {
                Log.Warning("Platform already configured - continuing with existing configuration", Tag);
            }
            else if (initializeResult != Result.Success)
            {
                throw new Exception("Failed to initialize platform: " + initializeResult);
            }

            // Error-level only; raise to Warning/Info when debugging EOS
            LoggingInterface.SetLogLevel(LogCategory.AllCategories, LogLevel.Error);
            LoggingInterface.SetCallback((ref LogMessage logMessage) => Log.Error(logMessage.Message, Tag));

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
                throw new Exception("Failed to create platform");
            }

            Log.Info("✅ Platform initialized successfully", Tag);

            StartLogin();
        }

        public void StartLogin()
        {
            var loginOptions = new LoginOptions()
            {
                Credentials = new Credentials()
                {
                    Type = LoginCredentialType.Developer,
                    Id = devAuthHost,             // DevAuthTool address
                    Token = devAuthCredentialName // Credential name created in DevAuthTool
                },
                // Must match the scopes configured for the product in the Developer Portal.
                ScopeFlags = AuthScopeFlags.BasicProfile |
                             AuthScopeFlags.FriendsList |
                             AuthScopeFlags.Presence
            };

            // Platform must be ticked for this callback to fire.
            s_PlatformInterface.GetAuthInterface().Login(ref loginOptions, null, (ref LoginCallbackInfo loginCallbackInfo) =>
            {
                if (loginCallbackInfo.ResultCode == Result.Success)
                {
                    Log.Info("Login succeeded", Tag);
                    OnLoggedIn?.Invoke(loginCallbackInfo.LocalUserId);
                }
                else if (Common.IsOperationComplete(loginCallbackInfo.ResultCode))
                {
                    Log.Error("Login failed: " + loginCallbackInfo.ResultCode, Tag);
                    OnLoginFailed?.Invoke(loginCallbackInfo.ResultCode);
                }
            });
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

        private void ShutdownPlatform()
        {
            if (s_PlatformInterface != null)
            {
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
    }
}
