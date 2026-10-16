using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using MafiaNight.Player;
using MafiaNight.Managers;

namespace MafiaNight.Systems
{
    /// <summary>
    /// DEPRECATED: This class is kept for backward compatibility.
    /// Use PlayerProfileManager instead for new code.
    /// </summary>
    public class PlayerDataSync : MonoBehaviour
    {
        public static PlayerDataSync Instance { get; private set; }
        
        /// <summary>
        /// Returns profile from PlayerProfileManager
        /// </summary>
        public PlayerProfile CurrentProfile => PlayerProfileManager.Instance?.LocalProfile;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// DEPRECATED: Use PlayerProfileManager.Instance.LoadProfile() instead
        /// </summary>
        public void LoadPlayerData(System.Action<PlayerProfile> onSuccess, System.Action<string> onError = null)
        {
            Debug.LogWarning("[PlayerDataSync] DEPRECATED: Use PlayerProfileManager.Instance.LoadProfile() instead");
            
            var profileManager = PlayerProfileManager.Instance;
            if (profileManager != null)
            {
                // Subscribe to load event
                void OnLoaded(PlayerProfile profile)
                {
                    profileManager.OnProfileLoaded -= OnLoaded;
                    onSuccess?.Invoke(profile);
                }
                
                profileManager.OnProfileLoaded += OnLoaded;
                
                // If already loaded, return immediately
                if (profileManager.LocalProfile != null)
                {
                    onSuccess?.Invoke(profileManager.LocalProfile);
                    profileManager.OnProfileLoaded -= OnLoaded;
                }
            }
            else
            {
                onError?.Invoke("PlayerProfileManager not found");
            }
        }
        
        /// <summary>
        /// DEPRECATED: Use PlayerProfileManager.Instance.SaveProfile() instead
        /// </summary>
        public void SavePlayerData(PlayerProfile profile)
        {
            Debug.LogWarning("[PlayerDataSync] DEPRECATED: Use PlayerProfileManager.Instance.SaveProfile() instead");
            
            var profileManager = PlayerProfileManager.Instance;
            if (profileManager != null)
            {
                profileManager.SaveProfile();
            }
        }
        
        /// <summary>
        /// DEPRECATED: Use PlayerProfileManager.Instance.RecordGameResult() instead
        /// </summary>
        public void UpdateMatchStats(PlayerProfile profile, bool won, bool wasMafia)
        {
            Debug.LogWarning("[PlayerDataSync] DEPRECATED: Use PlayerProfileManager.Instance.RecordGameResult() instead");
            
            var profileManager = PlayerProfileManager.Instance;
            if (profileManager != null)
            {
                profileManager.RecordGameResult(won, wasMafia);
            }
        }
        
        /// <summary>
        /// DEPRECATED: Use PlayerProfileManager.Instance.LocalProfile instead
        /// </summary>
        public PlayerProfile GetCurrentProfile()
        {
            return CurrentProfile;
        }
    }
}
