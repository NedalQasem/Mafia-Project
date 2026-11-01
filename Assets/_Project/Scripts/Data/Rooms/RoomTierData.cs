using UnityEngine;

namespace MafiaNight.Data
{
    /// <summary>
    /// Defines a room tier with entry fee, player capacity, and rewards
    /// ScriptableObject for easy balancing and data-driven design
    /// </summary>
    [CreateAssetMenu(fileName = "RoomTier", menuName = "Mafia Night/Room Tier Data")]
    public class RoomTierData : ScriptableObject
    {
        [Header("Tier Info")]
        [SerializeField] private string tierName = "Starter";
        [SerializeField] private Color tierColor = Color.white;

        [Header("Requirements")]
        [SerializeField] private int entryFee = 50;
        [SerializeField] private int maxPlayers = 6;

        [Header("Rewards")]
        [SerializeField] private int winnerRewardPerPlayer = 80;
        [SerializeField] private float houseFeePercentage = 0.2f; // 20% house fee

        #region Public Getters

        public string TierName => tierName;
        public Color TierColor => tierColor;
        public int EntryFee => entryFee;
        public int MaxPlayers => maxPlayers;
        public int WinnerRewardPerPlayer => winnerRewardPerPlayer;
        public float HouseFeePercentage => houseFeePercentage;

        /// <summary>
        /// Calculate total pot (all entry fees)
        /// </summary>
        public int GetTotalPot()
        {
            return entryFee * maxPlayers;
        }

        /// <summary>
        /// Calculate total reward pool after house fee
        /// </summary>
        public int GetTotalRewardPool()
        {
            int totalPot = GetTotalPot();
            return Mathf.RoundToInt(totalPot * (1f - houseFeePercentage));
        }

        #endregion

        #region Validation

        private void OnValidate()
        {
            // Auto-calculate winner reward based on pot and house fee
            if (maxPlayers > 0)
            {
                int rewardPool = GetTotalRewardPool();
                // Assume roughly half the players are winners (town vs mafia)
                int estimatedWinners = Mathf.Max(1, maxPlayers / 2);
                winnerRewardPerPlayer = Mathf.RoundToInt((float)rewardPool / estimatedWinners);
            }

            // Ensure valid values
            entryFee = Mathf.Max(1, entryFee);
            maxPlayers = Mathf.Clamp(maxPlayers, 4, 20);
            houseFeePercentage = Mathf.Clamp01(houseFeePercentage);
        }

        #endregion
    }
}
