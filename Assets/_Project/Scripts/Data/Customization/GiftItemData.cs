using UnityEngine;

namespace MafiaNight.Data.Customization
{
    /// <summary>
    /// Gift item data - can be sent to other players
    /// </summary>
    [CreateAssetMenu(fileName = "GiftItem", menuName = "MafiaNight/Customization/Gift Item")]
    public class GiftItemData : ScriptableObject
    {
        [Header("Identification")]
        [SerializeField] private string giftID;
        [SerializeField] private string giftName;
        [SerializeField, TextArea] private string description;
        
        [Header("Visuals")]
        [SerializeField] private Sprite giftIcon;
        [SerializeField] private GameObject giftAnimationPrefab;
        
        [Header("Pricing")]
        [SerializeField] private int priceCoins;
        [SerializeField] private int priceGems;
        
        [Header("Properties")]
        [SerializeField] private Player.ItemRarity rarity;
        [SerializeField] private bool isStackable = true;
        [SerializeField] private int maxStack = 99;
        
        // Properties
        public string GiftID => giftID;
        public string GiftName => giftName;
        public string Description => description;
        public Sprite GiftIcon => giftIcon;
        public GameObject GiftAnimationPrefab => giftAnimationPrefab;
        public int PriceCoins => priceCoins;
        public int PriceGems => priceGems;
        public Player.ItemRarity Rarity => rarity;
        public bool IsStackable => isStackable;
        public int MaxStack => maxStack;
    }
}
