using UnityEngine;

namespace MafiaNight.Data.Customization
{
    /// <summary>
    /// Profile frame data - purchasable from shop
    /// </summary>
    [CreateAssetMenu(fileName = "ProfileFrame", menuName = "MafiaNight/Customization/Profile Frame")]
    public class ProfileFrameData : ScriptableObject
    {
        [Header("Identification")]
        [SerializeField] private string frameID;
        [SerializeField] private string frameName;
        
        [Header("Visuals")]
        [SerializeField] private Sprite frameSprite;
        [SerializeField] private Color frameColor = Color.white;
        
        [Header("Pricing")]
        [SerializeField] private int priceCoins;
        [SerializeField] private int priceGems;
        
        [Header("Requirements")]
        [SerializeField] private bool isVIPOnly;
        [SerializeField] private int minLevel;
        [SerializeField] private Player.PlayerRank minRank;
        
        [Header("Rarity")]
        [SerializeField] private Player.ItemRarity rarity;
        
        // Properties
        public string FrameID => frameID;
        public string FrameName => frameName;
        public Sprite FrameSprite => frameSprite;
        public Color FrameColor => frameColor;
        public int PriceCoins => priceCoins;
        public int PriceGems => priceGems;
        public bool IsVIPOnly => isVIPOnly;
        public int MinLevel => minLevel;
        public Player.PlayerRank MinRank => minRank;
        public Player.ItemRarity Rarity => rarity;
        
        /// <summary>
        /// Check if player can purchase this frame
        /// </summary>
        public bool CanPlayerPurchase(Player.PlayerProfile player)
        {
            if (isVIPOnly && !player.IsVIPActive()) return false;
            if (player.Level < minLevel) return false;
            if ((int)player.Rank < (int)minRank) return false;
            return true;
        }
        
        /// <summary>
        /// Check if player can afford with coins
        /// </summary>
        public bool CanAffordCoins(int playerCoins)
        {
            return priceCoins > 0 && playerCoins >= priceCoins;
        }
        
        /// <summary>
        /// Check if player can afford with gems
        /// </summary>
        public bool CanAffordGems(int playerGems)
        {
            return priceGems > 0 && playerGems >= priceGems;
        }
    }
}
