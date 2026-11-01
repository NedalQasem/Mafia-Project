using MafiaNight.Core;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Config;
using MafiaNight.Services.Telemetry;
using UnityEditor;
using UnityEngine;

namespace MafiaNight.Editor
{
    /// <summary>
    /// Mafia ▸ Backend Status: which environment (Dev / Live) the next Play uses, how to log in (device ID or a DevAuthTool
    /// credential, to run several players on one PC), and — while playing — the login state and how many PlayFab calls
    /// this session has made (cost guard). Also the environment shortcuts in the same menu.
    /// </summary>
    public sealed class BackendStatusWindow : EditorWindow
    {
        public const string EnvKey = EnvironmentConfig.EditorPrefKey;      // "Dev" | "Live"
        public const string LoginModeKey = "MafiaNight.EosLoginMode";      // "DeviceId" | "DevAuthTool"
        public const string CredentialKey = "MafiaNight.DevAuthCredential";

        [MenuItem("Mafia/Backend Status")]
        private static void Open() => GetWindow<BackendStatusWindow>("Backend Status");

        [MenuItem("Mafia/Environment/Use Dev")] private static void UseDev() => EditorPrefs.SetString(EnvKey, "Dev");
        [MenuItem("Mafia/Environment/Use Dev", true)] private static bool UseDevCheck() { Menu.SetChecked("Mafia/Environment/Use Dev", EditorPrefs.GetString(EnvKey, "Dev") == "Dev"); return true; }
        [MenuItem("Mafia/Environment/Use Live")] private static void UseLive() => EditorPrefs.SetString(EnvKey, "Live");
        [MenuItem("Mafia/Environment/Use Live", true)] private static bool UseLiveCheck() { Menu.SetChecked("Mafia/Environment/Use Live", EditorPrefs.GetString(EnvKey, "Dev") == "Live"); return true; }

        private void OnEnable() { EditorApplication.update += Repaint; }
        private void OnDisable() { EditorApplication.update -= Repaint; }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Environment (next Play)", EditorStyles.boldLabel);
            string env = EditorPrefs.GetString(EnvKey, "Dev");
            int choice = GUILayout.Toolbar(env == "Live" ? 1 : 0, new[] { "Dev", "Live" });
            string chosen = choice == 1 ? "Live" : "Dev";
            if (chosen != env) EditorPrefs.SetString(EnvKey, chosen);

            var cfg = Resources.Load<EnvironmentConfig>("Config/Environment_" + chosen);
            if (cfg == null) EditorGUILayout.HelpBox("Missing asset Resources/Config/Environment_" + chosen, MessageType.Error);
            else
            {
                EditorGUILayout.ObjectField("Asset", cfg, typeof(EnvironmentConfig), false);
                EditorGUILayout.LabelField("EOS ids complete", cfg.IsEosComplete ? "yes" : "NO (scene values are used)");
                EditorGUILayout.LabelField("PlayFab title", string.IsNullOrEmpty(cfg.playFabTitleId) ? "-" : cfg.playFabTitleId);
                EditorGUILayout.LabelField("OpenID connection", string.IsNullOrEmpty(cfg.openIdConnectionId) ? "not set (CustomID fallback)" : cfg.openIdConnectionId);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Login in the Editor", EditorStyles.boldLabel);
            string mode = EditorPrefs.GetString(LoginModeKey, "");
            int m = GUILayout.Toolbar(mode == "DevAuthTool" ? 1 : 0, new[] { "Scene setting / Device ID", "DevAuthTool credential" });
            string newMode = m == 1 ? "DevAuthTool" : "";
            if (newMode != mode) EditorPrefs.SetString(LoginModeKey, newMode);
            if (newMode == "DevAuthTool")
            {
                string cred = EditorPrefs.GetString(CredentialKey, "TestUser1");
                string nc = EditorGUILayout.TextField("Credential name", cred);
                if (nc != cred) EditorPrefs.SetString(CredentialKey, nc);
                EditorGUILayout.HelpBox("Use a different credential for every Editor instance to be several players on one PC. DevAuthTool must be running.", MessageType.None);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Session (play mode)", EditorStyles.boldLabel);
            if (!Application.isPlaying) { EditorGUILayout.HelpBox("Enter Play mode to see the live state.", MessageType.Info); return; }

            if (ServiceLocator.TryGet<IAuthService>(out var auth))
            {
                EditorGUILayout.LabelField("EOS", auth.IsEOSLoggedIn ? "logged in" : "not logged in");
                EditorGUILayout.LabelField("PUID", string.IsNullOrEmpty(auth.ProductUserId) ? "-" : auth.ProductUserId);
                EditorGUILayout.LabelField("PlayFab", auth.IsPlayFabLoggedIn ? "logged in" : "not logged in");
                EditorGUILayout.LabelField("PlayFab ID", string.IsNullOrEmpty(auth.PlayFabId) ? "-" : auth.PlayFabId);
            }
            else EditorGUILayout.LabelField("Auth service", "not registered");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PlayFab calls (cost guard)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(PlayFabMeter.Report(), PlayFabMeter.Blocked > 0 ? MessageType.Warning : MessageType.None);
            EditorGUILayout.LabelField("Limits / min", $"reads {PlayFabMeter.ReadLimitPerMinute}  writes {PlayFabMeter.WriteLimitPerMinute}  frozen: {PlayFabMeter.WritesFrozen}");
            if (GUILayout.Button("Reset counters")) PlayFabMeter.Reset();
        }
    }
}
