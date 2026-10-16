using UnityEngine;
using MafiaNight.Player;

namespace MafiaNight.Systems
{
    /// <summary>
    /// Manages player rank system (ELO-style)
    /// Updated to use new PlayerRank enum with 7 ranks
    /// </summary>
    public class PlayerRankManager : MonoBehaviour
    {
        public static PlayerRankManager Instance { get; private set; }
        
        [Header("Rank Points")]
        [SerializeField] private int winPoints = 50;
        [SerializeField] private int losePoints = -20;
        [SerializeField] private int mvpBonus = 10;
        
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
        
        public PlayerRank GetRankFromPoints(int points)
        {
            if (points >= 6000) return PlayerRank.Grandmaster;
            if (points >= 5000) return PlayerRank.Master;
            if (points >= 4000) return PlayerRank.Diamond;
            if (points >= 3000) return PlayerRank.Platinum;
            if (points >= 2000) return PlayerRank.Gold;
            if (points >= 1000) return PlayerRank.Silver;
            return PlayerRank.Bronze;
        }
        
        public void AddRankPoints(PlayerProfile profile, bool won, bool isMVP = false)
        {
            if (profile == null)
            {
                Debug.LogError("[PlayerRank] Profile is null!");
                return;
            }
            
            int pointsToAdd = won ? winPoints : losePoints;
            
            if (isMVP && won)
            {
                pointsToAdd += mvpBonus;
            }
            
            profile.RankPoints += pointsToAdd;
            
            if (profile.RankPoints < 0)
                profile.RankPoints = 0;
            
            Debug.Log($"[PlayerRank] {(won ? "Win" : "Loss")} {(isMVP ? "+ MVP" : "")} = {pointsToAdd:+#;-#;0} points (Total: {profile.RankPoints})");
            
            PlayerRank oldRank = profile.Rank;
            profile.Rank = GetRankFromPoints(profile.RankPoints);
            
            if (profile.Rank > oldRank)
            {
                Debug.Log($"🎊 Rank Up! {oldRank} → {profile.Rank}");
                OnRankUp(profile, oldRank);
            }
            else if (profile.Rank < oldRank)
            {
                Debug.Log($"⬇️ Rank Down! {oldRank} → {profile.Rank}");
            }
            
            // Use PlayerProfileManager instead of PlayerDataSync
            var profileManager = MafiaNight.Managers.PlayerProfileManager.Instance;
            if (profileManager != null)
            {
                profileManager.SaveProfile();
            }
        }
        
        private void OnRankUp(PlayerProfile profile, PlayerRank oldRank)
        {
            switch (profile.Rank)
            {
                case PlayerRank.Silver:
                    profile.Coins += 200;
                    Debug.Log("🎁 Silver Rank Reward: +200 Coins");
                    break;
                    
                case PlayerRank.Gold:
                    profile.Coins += 500;
                    Debug.Log("🎁 Gold Rank Reward: +500 Coins");
                    break;
                    
                case PlayerRank.Platinum:
                    profile.Gems += 10;
                    Debug.Log("🎁 Platinum Rank Reward: +10 Gems");
                    break;
                    
                case PlayerRank.Diamond:
                    profile.Gems += 25;
                    Debug.Log("🎁 Diamond Rank Reward: +25 Gems");
                    break;
                    
                case PlayerRank.Master:
                    profile.Gems += 50;
                    Debug.Log("🎁 Master Rank Reward: +50 Gems");
                    break;
                    
                case PlayerRank.Grandmaster:
                    profile.Gems += 100;
                    Debug.Log("🎁 Grandmaster Rank Reward: +100 Gems");
                    break;
            }
        }
        
        public string GetRankName(PlayerRank rank)
        {
            return rank.ToString();
        }
        
        public Color GetRankColor(PlayerRank rank)
        {
            switch (rank)
            {
                case PlayerRank.Bronze: return new Color(0.8f, 0.5f, 0.2f);
                case PlayerRank.Silver: return new Color(0.75f, 0.75f, 0.75f);
                case PlayerRank.Gold: return new Color(1f, 0.84f, 0f);
                case PlayerRank.Platinum: return new Color(0.3f, 0.8f, 0.8f);
                case PlayerRank.Diamond: return new Color(0.7f, 0.9f, 1f);
                case PlayerRank.Master: return new Color(0.9f, 0.3f, 0.9f);
                case PlayerRank.Grandmaster: return new Color(1f, 0.2f, 0.2f);
                default: return Color.white;
            }
        }
        
        public int GetPointsToNextRank(int currentPoints)
        {
            if (currentPoints >= 6000) return 0;
            if (currentPoints >= 5000) return 6000 - currentPoints;
            if (currentPoints >= 4000) return 5000 - currentPoints;
            if (currentPoints >= 3000) return 4000 - currentPoints;
            if (currentPoints >= 2000) return 3000 - currentPoints;
            if (currentPoints >= 1000) return 2000 - currentPoints;
            return 1000 - currentPoints;
        }
    }
}
