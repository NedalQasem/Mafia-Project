using System.Collections.Generic;
using UnityEngine;

namespace MafiaNight.Data.Customization
{
    /// <summary>
    /// Database containing all customization items
    /// Use this to look up items by ID
    /// </summary>
    [CreateAssetMenu(fileName = "CustomizationDatabase", menuName = "MafiaNight/Customization/Database")]
    public class CustomizationDatabase : ScriptableObject
    {
        [Header("Profile Frames")]
        [SerializeField] private List<ProfileFrameData> profileFrames;
        
        [Header("Chat Backgrounds")]
        [SerializeField] private List<ChatBackgroundData> chatBackgrounds;
        
        [Header("Gifts")]
        [SerializeField] private List<GiftItemData> gifts;
        
        // Cached lookups
        private Dictionary<string, ProfileFrameData> _frameCache;
        private Dictionary<string, ChatBackgroundData> _backgroundCache;
        private Dictionary<string, GiftItemData> _giftCache;
        
        private void OnEnable()
        {
            BuildCache();
        }
        
        private void BuildCache()
        {
            // Build frame cache
            _frameCache = new Dictionary<string, ProfileFrameData>();
            if (profileFrames != null)
            {
                for (int i = 0; i < profileFrames.Count; i++)
                {
                    if (profileFrames[i] != null && !string.IsNullOrEmpty(profileFrames[i].FrameID))
                    {
                        _frameCache[profileFrames[i].FrameID] = profileFrames[i];
                    }
                }
            }
            
            // Build background cache
            _backgroundCache = new Dictionary<string, ChatBackgroundData>();
            if (chatBackgrounds != null)
            {
                for (int i = 0; i < chatBackgrounds.Count; i++)
                {
                    if (chatBackgrounds[i] != null && !string.IsNullOrEmpty(chatBackgrounds[i].BackgroundID))
                    {
                        _backgroundCache[chatBackgrounds[i].BackgroundID] = chatBackgrounds[i];
                    }
                }
            }
            
            // Build gift cache
            _giftCache = new Dictionary<string, GiftItemData>();
            if (gifts != null)
            {
                for (int i = 0; i < gifts.Count; i++)
                {
                    if (gifts[i] != null && !string.IsNullOrEmpty(gifts[i].GiftID))
                    {
                        _giftCache[gifts[i].GiftID] = gifts[i];
                    }
                }
            }
        }
        
        // ==================== LOOKUP METHODS ====================
        
        public ProfileFrameData GetFrame(string frameID)
        {
            if (_frameCache == null) BuildCache();
            
            if (string.IsNullOrEmpty(frameID) || frameID == "default")
            {
                return profileFrames != null && profileFrames.Count > 0 ? profileFrames[0] : null;
            }
            
            _frameCache.TryGetValue(frameID, out ProfileFrameData frame);
            return frame;
        }
        
        public ChatBackgroundData GetBackground(string bgID)
        {
            if (_backgroundCache == null) BuildCache();
            
            if (string.IsNullOrEmpty(bgID) || bgID == "default")
            {
                return chatBackgrounds != null && chatBackgrounds.Count > 0 ? chatBackgrounds[0] : null;
            }
            
            _backgroundCache.TryGetValue(bgID, out ChatBackgroundData bg);
            return bg;
        }
        
        public GiftItemData GetGift(string giftID)
        {
            if (_giftCache == null) BuildCache();
            
            _giftCache.TryGetValue(giftID, out GiftItemData gift);
            return gift;
        }
        
        // ==================== LIST ACCESSORS ====================
        
        public List<ProfileFrameData> GetAllFrames() => profileFrames;
        public List<ChatBackgroundData> GetAllBackgrounds() => chatBackgrounds;
        public List<GiftItemData> GetAllGifts() => gifts;
        
        public List<ProfileFrameData> GetFramesByRarity(Player.ItemRarity rarity)
        {
            List<ProfileFrameData> result = new List<ProfileFrameData>();
            if (profileFrames != null)
            {
                for (int i = 0; i < profileFrames.Count; i++)
                {
                    if (profileFrames[i].Rarity == rarity)
                    {
                        result.Add(profileFrames[i]);
                    }
                }
            }
            return result;
        }
    }
}
