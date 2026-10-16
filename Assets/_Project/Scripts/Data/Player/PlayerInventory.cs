using System;
using System.Collections.Generic;

namespace MafiaNight.Data.Player
{
    /// <summary>
    /// Player inventory - gifts, emotes, badges
    /// </summary>
    [System.Serializable]
    public class PlayerInventory
    {
        // ==================== GIFTS ====================
        /// <summary>Owned gifts with quantities</summary>
        public List<GiftItem> Gifts;
        
        // ==================== EMOTES ====================
        /// <summary>Owned emote IDs</summary>
        public List<string> Emotes;
        
        // ==================== BADGES ====================
        /// <summary>Earned badge IDs</summary>
        public List<string> Badges;
        
        // ==================== CONSTRUCTOR ====================
        public PlayerInventory()
        {
            Gifts = new List<GiftItem>();
            Emotes = new List<string>();
            Badges = new List<string>();
        }
        
        // ==================== GIFT METHODS ====================
        public int GetGiftQuantity(string giftID)
        {
            if (Gifts == null) return 0;
            
            for (int i = 0; i < Gifts.Count; i++)
            {
                if (Gifts[i].GiftID == giftID)
                {
                    return Gifts[i].Quantity;
                }
            }
            return 0;
        }
        
        public void AddGift(string giftID, int quantity = 1)
        {
            if (Gifts == null) Gifts = new List<GiftItem>();
            
            for (int i = 0; i < Gifts.Count; i++)
            {
                if (Gifts[i].GiftID == giftID)
                {
                    Gifts[i].Quantity += quantity;
                    return;
                }
            }
            
            // Not found, add new
            Gifts.Add(new GiftItem { GiftID = giftID, Quantity = quantity });
        }
        
        public bool UseGift(string giftID, int quantity = 1)
        {
            if (Gifts == null) return false;
            
            for (int i = 0; i < Gifts.Count; i++)
            {
                if (Gifts[i].GiftID == giftID && Gifts[i].Quantity >= quantity)
                {
                    Gifts[i].Quantity -= quantity;
                    if (Gifts[i].Quantity <= 0)
                    {
                        Gifts.RemoveAt(i);
                    }
                    return true;
                }
            }
            return false;
        }
        
        // ==================== BADGE METHODS ====================
        public bool HasBadge(string badgeID)
        {
            return Badges != null && Badges.Contains(badgeID);
        }
        
        public void AddBadge(string badgeID)
        {
            if (Badges == null) Badges = new List<string>();
            if (!Badges.Contains(badgeID))
            {
                Badges.Add(badgeID);
            }
        }
    }
    
    /// <summary>
    /// Single gift item with quantity
    /// </summary>
    [System.Serializable]
    public class GiftItem
    {
        public string GiftID;
        public int Quantity;
    }
}
