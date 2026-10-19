using MafiaNight.Core;
using UnityEngine;

namespace MafiaNight.Services.Config
{
    /// <summary>
    /// One backend environment (EOS deployment + PlayFab title). The Dev and Live assets live in
    /// Resources/Config; <see cref="Current"/> picks one: Development builds and the Editor use the choice made in the
    /// "Mafia ▸ Environment" menu (Dev by default), release builds always use Live.
    /// </summary>
    [CreateAssetMenu(fileName = "Environment_Dev", menuName = "Mafia Night/Environment Config")]
    public sealed class EnvironmentConfig : ScriptableObject
    {
        public const string EditorPrefKey = "MafiaNight.Environment";   // "Dev" | "Live" (also written by the editor menu)

        [Header("Identity")]
        public string environmentName = "Dev";

        [Header("EOS (Developer Portal)")]
        public string productName = "MafiaNight";
        public string productVersion = "1.0";
        public string productId = "";
        public string sandboxId = "";
        public string deploymentId = "";
        public string clientId = "";
        public string clientSecret = "";

        [Header("PlayFab")]
        public string playFabTitleId = "";
        [Tooltip("Name of the OpenID Connect connection in PlayFab (issuer https://api.epicgames.dev). Empty = CustomID fallback, allowed in the Editor / Development builds only.")]
        public string openIdConnectionId = "";

        public bool IsEosComplete =>
            !string.IsNullOrEmpty(productId) && !string.IsNullOrEmpty(sandboxId) && !string.IsNullOrEmpty(deploymentId) &&
            !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret);

        public bool IsPlayFabComplete => !string.IsNullOrEmpty(playFabTitleId);

        private static EnvironmentConfig cached;

        /// <summary>Name of the environment this run uses.</summary>
        public static string SelectedName
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetString(EditorPrefKey, "Dev");
#else
                return Debug.isDebugBuild ? "Dev" : "Live";
#endif
            }
        }

        /// <summary>The config for this run, or null when the asset is missing (callers then keep their scene values).</summary>
        public static EnvironmentConfig Current
        {
            get
            {
                if (cached != null) return cached;
                cached = Resources.Load<EnvironmentConfig>("Config/Environment_" + SelectedName);
                if (cached == null) Log.Warning("No EnvironmentConfig at Resources/Config/Environment_" + SelectedName, "Config");
                return cached;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;
    }
}
