using UnityEngine;

namespace MafiaNight.Data.Customization
{
    /// <summary>
    /// Chat background data - purchasable from shop
    /// Applied to player's messages in chat
    /// </summary>
    [CreateAssetMenu(fileName = "ChatBackground", menuName = "MafiaNight/Customization/Chat Background")]
    public class ChatBackgroundData : ScriptableObject
    {
        [Header("Identification")]
        [SerializeField] private string backgroundID;
        [SerializeField] private string backgroundName;
        
        [Header("Visuals")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Color backgroundColor = Color.white;
        [SerializeField] private Color textColor = Color.white;
        
        [Header("Pricing")]
        [SerializeField] private int priceCoins;
        [SerializeField] private int priceGems;
        
        [Header("Requirements")]
        [SerializeField] private bool isVIPOnly;
        [SerializeField] private int minLevel;
        
        [Header("Rarity")]
        [SerializeField] private Player.ItemRarity rarity;
        
        // Properties
        public string BackgroundID => backgroundID;
        public string BackgroundName => backgroundName;
        public Sprite BackgroundSprite => backgroundSprite;
        public Color BackgroundColor => backgroundColor;
        public Color TextColor => textColor;
        public int PriceCoins => priceCoins;
        public int PriceGems => priceGems;
        public bool IsVIPOnly => isVIPOnly;
        public int MinLevel => minLevel;
        public Player.ItemRarity Rarity => rarity;
        
        public bool CanPlayerPurchase(Player.PlayerProfile player)
        {
            if (isVIPOnly && !player.IsVIPActive()) return false;
            if (player.Level < minLevel) return false;
            return true;
        }
    }
}
