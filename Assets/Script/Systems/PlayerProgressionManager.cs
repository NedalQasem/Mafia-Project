using UnityEngine;
using MafiaNight.Player;
using MafiaNight.Managers;

namespace MafiaNight.Systems
{
    /// <summary>
    /// Manages player progression: XP, Level
    /// DEPRECATED Logic: Most logic moved to PlayerProfile directly.
    /// This now acts as a helper/bridge.
    /// </summary>
    public class PlayerProgressionManager : MonoBehaviour
    {
        public static PlayerProgressionManager Instance { get; private set; }
        
        [Header("XP Settings")]
        [SerializeField] private int baseXPPerLevel = 100;
        [SerializeField] private float xpScalingFactor = 1.1f;
        
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
        
        public int CalculateXPForLevel(int level)
        {
            return Mathf.RoundToInt(baseXPPerLevel * Mathf.Pow(xpScalingFactor, level - 1));
        }
        
        public void AddXP(PlayerProfile profile, int xpAmount)
        {
            // Redirect to PlayerProfileManager
            var profileManager = PlayerProfileManager.Instance;
            if (profileManager != null && profileManager.LocalProfile == profile)
            {
                profileManager.AddXP(xpAmount);
                profileManager.SaveProfile();
            }
            else if (profile != null)
            {
                // Fallback for offline/test profiles
                profile.AddXP(xpAmount);
                Debug.LogWarning("[PlayerProgression] Profile added XP without Manager sync");
            }
        }
        
        // Rewards logic is now likely handled inside PlayerProfile.AddXP or via event in ProfileManager
        // Keeping this method signature to avoid breaking references, but it might be unused.
        public float GetLevelProgress(PlayerProfile profile)
        {
            if (profile == null) return 0f;
            return profile.GetLevelProgress();
        }
    }
}
