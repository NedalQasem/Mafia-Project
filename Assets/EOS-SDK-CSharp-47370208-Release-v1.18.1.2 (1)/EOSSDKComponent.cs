// This code is provided for demonstration purposes and is not intended to represent ideal practices.
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.Platform;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

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
    // These fields correspond to \<see cref="Credentials.Id" /> and \<see cref="Credentials.Token" />,
    // and their use differs based on the login type. For more information, see \<see cref="Credentials" />
    // and the Auth Interface documentation.
    public string m_LoginCredentialId = null;
    public string m_LoginCredentialToken = null;

    private static PlatformInterface s_PlatformInterface;
    private const float c_PlatformTickInterval = 0.1f;
    private float m_PlatformTickTimer = 0f;

    // If we're in editor, we should dynamically load and unload the SDK between play sessions.
    // This allows us to initialize the SDK each time the game is run in editor.
#if UNITY_EDITOR
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
        var libraryPath = "Assets/EOS-SDK-CSharp-47370208-Release-v1.18.1.2 (1)/SDK/Bin/EOSSDK-Win64-Shipping.dll";

        m_LibraryPointer = LoadLibrary(libraryPath);
        if (m_LibraryPointer == IntPtr.Zero)
        {
            throw new Exception("Failed to load library" + libraryPath);
        }

        Bindings.Hook(m_LibraryPointer, GetProcAddress);
#endif
    }

#if UNITY_EDITOR
    // ✅ FIX: Cleanup when stopping play in Editor
    private void OnDestroy()
    {
        if (s_PlatformInterface != null)
        {
            Debug.Log("[EOS] Cleaning up platform (Editor stop)");
            s_PlatformInterface.Release();
            s_PlatformInterface = null;
            PlatformInterface.Shutdown();
        }

        if (m_LibraryPointer != IntPtr.Zero)
        {
            Bindings.Unhook();
            while (FreeLibrary(m_LibraryPointer) != 0) { }
            m_LibraryPointer = IntPtr.Zero;
        }
    }
#endif

    private void OnApplicationQuit()
    {
        if (s_PlatformInterface != null)
        {
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

    void Start()
    {
        // ✅ FIX: Check if already initialized (happens when stopping/starting in Editor)
        if (s_PlatformInterface != null)
        {
            Debug.LogWarning("[EOS] Platform already initialized - skipping initialization");
            StartLogin(); // Just trigger login
            return;
        }
        
        var initializeOptions = new InitializeOptions()
        {
            ProductName = m_ProductName,
            ProductVersion = m_ProductVersion
        };

        var initializeResult = PlatformInterface.Initialize(ref initializeOptions);
        
        // ✅ FIX: Handle AlreadyConfigured case gracefully
        if (initializeResult == Result.AlreadyConfigured)
        {
            Debug.LogWarning("[EOS] Platform already configured - continuing with existing configuration");
            // Continue with platform creation
        }
        else if (initializeResult != Result.Success)
        {
            throw new Exception("Failed to initialize platform: " + initializeResult);
        }

        // Reduced logging to Error only (change to Warning or Info if needed for debugging)
        LoggingInterface.SetLogLevel(LogCategory.AllCategories, LogLevel.Error);
        LoggingInterface.SetCallback((ref LogMessage logMessage) => Debug.LogError($"[EOS] {logMessage.Message}"));

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
            Flags = Epic.OnlineServices.Platform.PlatformFlags.DisableOverlay
        };

        s_PlatformInterface = PlatformInterface.Create(ref options);
        if (s_PlatformInterface == null)
        {
            throw new Exception("Failed to create platform");
        }
        
        Debug.Log("[EOS] ✅ Platform initialized successfully");
        
        // Auto-login on start
        StartLogin();
    }

    public void StartLogin()
    {
        var loginOptions = new LoginOptions()
        {
            Credentials = new Credentials()
            {
                Type = Epic.OnlineServices.Auth.LoginCredentialType.Developer,
                Id = "localhost:8080",  // DevAuthTool address (check DevAuthTool window for exact port)
                Token = "TestUser1"     // CHANGE THIS: Put the username you create in DevAuthTool
            },
            // Change these scopes to match the ones set up on your product in the Developer Portal.
            ScopeFlags = Epic.OnlineServices.Auth.AuthScopeFlags.BasicProfile | 
                         Epic.OnlineServices.Auth.AuthScopeFlags.FriendsList |
                         Epic.OnlineServices.Auth.AuthScopeFlags.Presence
        };

        // Ensure platform tick is called on an interval, or this will not callback.
        s_PlatformInterface.GetAuthInterface().Login(ref loginOptions, null, (ref LoginCallbackInfo loginCallbackInfo) =>
        {
            if (loginCallbackInfo.ResultCode == Result.Success)
            {
                Debug.Log("Login succeeded");
                
                // Login to PlayFab with EOS Account ID
                var playFabLogin = FindObjectOfType<PlayFabEOSLogin>();
                if (playFabLogin != null)
                {
                    playFabLogin.OnEOSLoginSuccess(loginCallbackInfo.LocalUserId);
                }
                else
                {
                    Debug.LogWarning("PlayFabEOSLogin component not found in scene");
                }
            }
            else if (Common.IsOperationComplete(loginCallbackInfo.ResultCode))
            {
                Debug.Log("Login failed: " + loginCallbackInfo.ResultCode);
            }
        });
    }

    // Calling tick on a regular interval is required for callbacks to work.
    private void Update()
    {
        if (s_PlatformInterface != null)
        {
            m_PlatformTickTimer += Time.deltaTime;

            if (m_PlatformTickTimer >= c_PlatformTickInterval)
            {
                m_PlatformTickTimer = 0;
                s_PlatformInterface.Tick();
            }
        }
    }
}