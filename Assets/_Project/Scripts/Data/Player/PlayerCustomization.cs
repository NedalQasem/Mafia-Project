using System;
using System.Collections.Generic;

namespace MafiaNight.Data.Player
{
    [System.Serializable]
    public class EquippedSlot
    {
        public string CategoryId;
        public string ItemId;
    }

    /// <summary>
    /// Player customization data - cosmetic items and preferences
    /// </summary>
    [System.Serializable]
    public class PlayerCustomization
    {
        // ==================== ACTIVE ITEMS ====================
        /// <summary>Current profile picture ID</summary>
        public string ProfilePictureID;
        
        /// <summary>Current profile frame ID (from shop)</summary>
        public string ProfileFrameID;
        
        /// <summary>Current chat background ID</summary>
        public string ChatBackgroundID;
        
        /// <summary>Current chat bubble style ID</summary>
        public string ChatBubbleID;
        
        // ==================== OWNED ITEMS ====================
        /// <summary>List of owned profile frame IDs</summary>
        public List<string> OwnedFrames;
        
        /// <summary>List of owned chat background IDs</summary>
        public List<string> OwnedBackgrounds;
        
        /// <summary>List of owned chat bubble IDs</summary>
        public List<string> OwnedBubbles;
        
        // ==================== EQUIPPED (store catalog) ====================
        /// <summary>What the player wears from the store: one item per category (frames, bubbles, titles...).</summary>
        public List<EquippedSlot> Equipped = new List<EquippedSlot>();

        public string GetEquipped(string categoryId)
        {
            if (Equipped == null) return null;
            foreach (var s in Equipped) if (s.CategoryId == categoryId) return s.ItemId;
            return null;
        }

        /// <summary>Wears an item in its category (null/empty itemId takes it off).</summary>
        public void SetEquipped(string categoryId, string itemId)
        {
            if (Equipped == null) Equipped = new List<EquippedSlot>();
            for (int i = 0; i < Equipped.Count; i++)
            {
                if (Equipped[i].CategoryId != categoryId) continue;
                if (string.IsNullOrEmpty(itemId)) Equipped.RemoveAt(i); else Equipped[i].ItemId = itemId;
                return;
            }
            if (!string.IsNullOrEmpty(itemId)) Equipped.Add(new EquippedSlot { CategoryId = categoryId, ItemId = itemId });
        }

        // ==================== CONSTRUCTOR ====================
        public PlayerCustomization()
        {
            // Default values
            ProfilePictureID = "default";
            ProfileFrameID = "default";
            ChatBackgroundID = "default";
            ChatBubbleID = "default";
            
            OwnedFrames = new List<string> { "default" };
            OwnedBackgrounds = new List<string> { "default" };
            OwnedBubbles = new List<string> { "default" };
        }
        
        // ==================== HELPERS ====================
        public bool OwnsFrame(string frameID)
        {
            return OwnedFrames != null && OwnedFrames.Contains(frameID);
        }
        
        public bool OwnsBackground(string bgID)
        {
            return OwnedBackgrounds != null && OwnedBackgrounds.Contains(bgID);
        }
        
        public bool OwnsBubble(string bubbleID)
        {
            return OwnedBubbles != null && OwnedBubbles.Contains(bubbleID);
        }
        
        public void AddFrame(string frameID)
        {
            if (OwnedFrames == null) OwnedFrames = new List<string>();
            if (!OwnedFrames.Contains(frameID))
            {
                OwnedFrames.Add(frameID);
            }
        }
        
        public void AddBackground(string bgID)
        {
            if (OwnedBackgrounds == null) OwnedBackgrounds = new List<string>();
            if (!OwnedBackgrounds.Contains(bgID))
            {
                OwnedBackgrounds.Add(bgID);
            }
        }
    }
}
