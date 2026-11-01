using System;
using UnityEngine;

namespace MafiaNight.Data.Player
{
    /// <summary>
    /// Complete player profile with all data
    /// Used for local player with full access to private data
    /// </summary>
    [System.Serializable]
    public class PlayerProfile
    {
        #region Core Identity
        
        [Header("Identity")]
        public string PlayFabId;
        public string DisplayName;
        
        #endregion
        
        #region Progression
        
        [Header("Progression")]
        public int Level;
        public int CurrentXP;
        public int XPToNextLevel;
        public PlayerRank Rank;
        public int RankPoints;
        
        #endregion
        
        // Coins/Gems are NOT stored here: PlayFab virtual currency (ICurrencyService) is the single source of truth.

        #region VIP Status
        
        [Header("VIP")]
        public bool IsVIP;
        public string VIPExpiryDate;
        
        #endregion
        
        #region Customization & Inventory
        
        [Header("Customization")]
        public PlayerCustomization Customization;
        
        [Header("Inventory")]
        public PlayerInventory Inventory;
        
        [Header("Statistics")]
        public PlayerStatistics Statistics;
        
        #endregion
        
        #region Constructors
        
        public PlayerProfile()
        {
            Level = 1;
            CurrentXP = 0;
            XPToNextLevel = 100;
            Rank = PlayerRank.Bronze;
            RankPoints = 0;
            IsVIP = false;
            
            Customization = new PlayerCustomization();
            Inventory = new PlayerInventory();
            Statistics = new PlayerStatistics();
        }
        
        #endregion
        
        #region VIP Methods
        
        public bool IsVIPActive()
        {
            if (!IsVIP || string.IsNullOrEmpty(VIPExpiryDate))
                return false;
                
            if (DateTime.TryParse(VIPExpiryDate, out DateTime expiryDate))
            {
                return DateTime.UtcNow < expiryDate;
            }
            return false;
        }
        
        public int GetVIPDaysRemaining()
        {
            if (!IsVIPActive()) return 0;
            
            if (DateTime.TryParse(VIPExpiryDate, out DateTime expiryDate))
            {
                return (int)(expiryDate - DateTime.UtcNow).TotalDays;
            }
            return 0;
        }
        
        #endregion
        
        #region Progression Methods
        
        public float GetLevelProgress()
        {
            if (XPToNextLevel <= 0) return 0f;
            return (float)CurrentXP / XPToNextLevel;
        }
        
        public string GetLevelProgressText()
        {
            return $"{CurrentXP} / {XPToNextLevel} XP";
        }
        
        public bool AddXP(int amount)
        {
            CurrentXP += amount;
            bool leveledUp = false;
            
            while (CurrentXP >= XPToNextLevel)
            {
                CurrentXP -= XPToNextLevel;
                Level++;
                XPToNextLevel = CalculateXPForLevel(Level);
                leveledUp = true;
            }
            
            return leveledUp;
        }
        
        private int CalculateXPForLevel(int level)
        {
            return 100 + (level * 50);
        }
        
        #endregion
        
        #region Rank Methods
        
        public string GetRankName()
        {
            return Rank.ToString();
        }
        
        public void UpdateRank()
        {
            Rank = RankRules.GetRankFromPoints(RankPoints);
        }
        
        public void AddRankPoints(int points)
        {
            RankPoints += points;
            if (RankPoints < 0) RankPoints = 0;
            UpdateRank();
        }
        
        #endregion
        
        #region Public Data Export
        
        /// <summary>
        /// Get minimal public data for display to other players
        /// </summary>
        public PlayerPublicData ToPublicData()
        {
            return new PlayerPublicData
            {
                PlayFabId = PlayFabId,
                DisplayName = DisplayName,
                Level = Level,
                Rank = Rank,
                IsVIP = IsVIPActive(),
                ProfilePictureID = Customization?.ProfilePictureID ?? "default",
                ProfileFrameID = Customization?.ProfileFrameID ?? "default"
            };
        }
        
        #endregion
    }
    
    /// <summary>
    /// Minimal player data visible to other players
    /// Used in Lobby, Gameplay, Chat
    /// </summary>
    [System.Serializable]
    public class PlayerPublicData
    {
        public string PlayFabId;
        public string DisplayName;
        public int Level;
        public PlayerRank Rank;
        public bool IsVIP;
        public string ProfilePictureID;
        public string ProfileFrameID;
        
        public string GetRankName() => Rank.ToString();
    }
}
