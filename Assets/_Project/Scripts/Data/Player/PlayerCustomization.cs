using System;
using System.Collections.Generic;

namespace MafiaNight.Player
{
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
