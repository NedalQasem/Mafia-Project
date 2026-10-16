using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MafiaNight.Data.Player;
using MafiaNight.Data.Customization;

namespace MafiaNight.UI.Components
{
    /// <summary>
    /// Displays player info in lobby - compact view
    /// Shows: Name, Picture, Frame, Rank, VIP Badge
    /// Style similar to Jawaker
    /// </summary>
    public class LobbyPlayerCard : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Profile Section")]
        [SerializeField] private Image profilePicture;
        [SerializeField] private Image profileFrame;
        [SerializeField] private Image vipBadge;
        
        [Header("Info Section")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image rankIcon;
        [SerializeField] private TextMeshProUGUI levelText;
        
        [Header("Visual Settings")]
        [SerializeField] private Color vipNameColor = new Color(1f, 0.84f, 0f); // Gold
        [SerializeField] private Color normalNameColor = Color.white;
        
        [Header("Rank Icons")]
        [SerializeField] private Sprite bronzeIcon;
        [SerializeField] private Sprite silverIcon;
        [SerializeField] private Sprite goldIcon;
        [SerializeField] private Sprite platinumIcon;
        [SerializeField] private Sprite diamondIcon;
        [SerializeField] private Sprite masterIcon;
        [SerializeField] private Sprite grandmasterIcon;
        
        [Header("References")]
        [SerializeField] private CustomizationDatabase customizationDB;
        
        #endregion
        
        #region Private Fields
        
        private PlayerPublicData _currentPlayerData;
        private string _playerId;
        
        #endregion
        
        #region Properties
        
        public string PlayerId => _playerId;
        public PlayerPublicData PlayerData => _currentPlayerData;
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Initialize card with player public data
        /// </summary>
        public void SetPlayerData(PlayerPublicData data)
        {
            if (data == null)
            {
                ClearCard();
                return;
            }
            
            _currentPlayerData = data;
            _playerId = data.PlayFabId;
            
            UpdateDisplay();
        }
        
        /// <summary>
        /// Initialize card with full player profile (extracts public data)
        /// </summary>
        public void SetPlayerProfile(PlayerProfile profile)
        {
            if (profile == null)
            {
                ClearCard();
                return;
            }
            
            SetPlayerData(profile.ToPublicData());
        }
        
        /// <summary>
        /// Clear the card display
        /// </summary>
        public void ClearCard()
        {
            _currentPlayerData = null;
            _playerId = null;
            
            if (playerNameText != null) playerNameText.text = "";
            if (levelText != null) levelText.text = "";
            if (vipBadge != null) vipBadge.gameObject.SetActive(false);
            if (profilePicture != null) profilePicture.color = new Color(1, 1, 1, 0.3f);
        }
        
        /// <summary>
        /// Update only the name (for quick updates)
        /// </summary>
        public void UpdateName(string newName)
        {
            if (_currentPlayerData != null)
            {
                _currentPlayerData.DisplayName = newName;
            }
            
            if (playerNameText != null)
            {
                playerNameText.text = newName;
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private void UpdateDisplay()
        {
            if (_currentPlayerData == null) return;
            
            // Name
            if (playerNameText != null)
            {
                playerNameText.text = _currentPlayerData.DisplayName;
                playerNameText.color = _currentPlayerData.IsVIP ? vipNameColor : normalNameColor;
            }
            
            // Level
            if (levelText != null)
            {
                levelText.text = $"Lv.{_currentPlayerData.Level}";
            }
            
            // VIP Badge
            if (vipBadge != null)
            {
                vipBadge.gameObject.SetActive(_currentPlayerData.IsVIP);
            }
            
            // Rank Icon
            UpdateRankIcon();
            
            // Profile Frame
            UpdateProfileFrame();
            
            // Profile Picture
            UpdateProfilePicture();
        }
        
        private void UpdateRankIcon()
        {
            if (rankIcon == null) return;
            
            Sprite icon = GetRankSprite(_currentPlayerData.Rank);
            if (icon != null)
            {
                rankIcon.sprite = icon;
                rankIcon.gameObject.SetActive(true);
            }
            else
            {
                rankIcon.gameObject.SetActive(false);
            }
        }
        
        private Sprite GetRankSprite(PlayerRank rank)
        {
            switch (rank)
            {
                case PlayerRank.Bronze: return bronzeIcon;
                case PlayerRank.Silver: return silverIcon;
                case PlayerRank.Gold: return goldIcon;
                case PlayerRank.Platinum: return platinumIcon;
                case PlayerRank.Diamond: return diamondIcon;
                case PlayerRank.Master: return masterIcon;
                case PlayerRank.Grandmaster: return grandmasterIcon;
                default: return bronzeIcon;
            }
        }
        
        private void UpdateProfileFrame()
        {
            if (profileFrame == null || customizationDB == null) return;
            
            string frameID = _currentPlayerData.ProfileFrameID;
            ProfileFrameData frameData = customizationDB.GetFrame(frameID);
            
            if (frameData != null && frameData.FrameSprite != null)
            {
                profileFrame.sprite = frameData.FrameSprite;
                profileFrame.color = frameData.FrameColor;
                profileFrame.gameObject.SetActive(true);
            }
            else
            {
                // Use default or hide
                profileFrame.gameObject.SetActive(false);
            }
        }
        
        private void UpdateProfilePicture()
        {
            if (profilePicture == null) return;
            
            // TODO: Load profile picture from URL or local cache
            // For now, just ensure it's visible
            profilePicture.color = Color.white;
            
            // Future: LoadProfilePicture(_currentPlayerData.ProfilePictureID);
        }
        
        #endregion
        
        #region Editor Helpers
        
#if UNITY_EDITOR
        [ContextMenu("Test With Sample Data")]
        private void TestWithSampleData()
        {
            PlayerPublicData testData = new PlayerPublicData
            {
                PlayFabId = "TEST123",
                DisplayName = "TestPlayer",
                Level = 25,
                Rank = PlayerRank.Gold,
                IsVIP = true,
                ProfilePictureID = "default",
                ProfileFrameID = "gold_frame"
            };
            
            SetPlayerData(testData);
        }
#endif
        
        #endregion
    }
}
