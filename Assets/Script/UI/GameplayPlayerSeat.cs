using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MafiaNight.Player;
using MafiaNight.Data.Customization;

namespace MafiaNight.UI
{
    /// <summary>
    /// Displays player info during gameplay - circular seat view
    /// Shows: Picture (circular), Frame, Name, Rank Badge, VIP indicator
    /// Style similar to Jawaker gameplay view
    /// </summary>
    public class GameplayPlayerSeat : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Profile Display")]
        [SerializeField] private Image profilePicture;
        [SerializeField] private Image profileFrame;
        [SerializeField] private Image vipIndicator;
        [SerializeField] private Image rankBadge;
        
        [Header("Name Display")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private int maxNameLength = 10;
        
        [Header("Game State")]
        [SerializeField] private GameObject aliveOverlay;
        [SerializeField] private GameObject eliminatedOverlay;
        [SerializeField] private GameObject speakingIndicator;
        [SerializeField] private GameObject votedIndicator;
        
        [Header("Role Display (Host Only)")]
        [SerializeField] private Image roleIcon;
        [SerializeField] private Sprite mafiaRoleSprite;
        [SerializeField] private Sprite citizenRoleSprite;
        
        [Header("Visual Settings")]
        [SerializeField] private Color vipGlowColor = new Color(1f, 0.84f, 0f, 0.5f);
        [SerializeField] private Color eliminatedTint = new Color(0.3f, 0.3f, 0.3f, 1f);
        
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
        
        private PlayerPublicData _playerData;
        private string _playerId;
        private bool _isEliminated;
        private bool _isSpeaking;
        private bool _hasVoted;
        
        #endregion
        
        #region Properties
        
        public string PlayerId => _playerId;
        public PlayerPublicData PlayerData => _playerData;
        public bool IsEliminated => _isEliminated;
        public bool IsSpeaking => _isSpeaking;
        
        #endregion
        
        #region Public Methods - Setup
        
        /// <summary>
        /// Initialize seat with player data
        /// </summary>
        public void SetPlayer(PlayerPublicData data)
        {
            if (data == null)
            {
                ClearSeat();
                return;
            }
            
            _playerData = data;
            _playerId = data.PlayFabId;
            _isEliminated = false;
            _isSpeaking = false;
            _hasVoted = false;
            
            UpdateDisplay();
            SetAlive();
        }
        
        /// <summary>
        /// Clear the seat
        /// </summary>
        public void ClearSeat()
        {
            _playerData = null;
            _playerId = null;
            
            if (playerNameText != null) playerNameText.text = "";
            if (profilePicture != null) profilePicture.color = new Color(1, 1, 1, 0.2f);
            if (vipIndicator != null) vipIndicator.gameObject.SetActive(false);
            if (rankBadge != null) rankBadge.gameObject.SetActive(false);
            if (roleIcon != null) roleIcon.gameObject.SetActive(false);
            
            SetAlive();
        }
        
        #endregion
        
        #region Public Methods - Game State
        
        /// <summary>
        /// Mark player as eliminated
        /// </summary>
        public void SetEliminated()
        {
            _isEliminated = true;
            
            if (eliminatedOverlay != null)
                eliminatedOverlay.SetActive(true);
            
            if (aliveOverlay != null)
                aliveOverlay.SetActive(false);
            
            if (profilePicture != null)
                profilePicture.color = eliminatedTint;
            
            SetSpeaking(false);
        }
        
        /// <summary>
        /// Mark player as alive
        /// </summary>
        public void SetAlive()
        {
            _isEliminated = false;
            
            if (eliminatedOverlay != null)
                eliminatedOverlay.SetActive(false);
            
            if (aliveOverlay != null)
                aliveOverlay.SetActive(true);
            
            if (profilePicture != null)
                profilePicture.color = Color.white;
        }
        
        /// <summary>
        /// Toggle speaking indicator (voice chat active)
        /// </summary>
        public void SetSpeaking(bool speaking)
        {
            _isSpeaking = speaking;
            
            if (speakingIndicator != null)
                speakingIndicator.SetActive(speaking && !_isEliminated);
        }
        
        /// <summary>
        /// Show that this player has voted
        /// </summary>
        public void SetVoted(bool voted)
        {
            _hasVoted = voted;
            
            if (votedIndicator != null)
                votedIndicator.SetActive(voted);
        }
        
        /// <summary>
        /// Show player's role (for host or end of game)
        /// </summary>
        public void ShowRole(bool isMafia)
        {
            if (roleIcon == null) return;
            
            roleIcon.sprite = isMafia ? mafiaRoleSprite : citizenRoleSprite;
            roleIcon.gameObject.SetActive(true);
        }
        
        /// <summary>
        /// Hide player's role
        /// </summary>
        public void HideRole()
        {
            if (roleIcon != null)
                roleIcon.gameObject.SetActive(false);
        }
        
        #endregion
        
        #region Private Methods
        
        private void UpdateDisplay()
        {
            if (_playerData == null) return;
            
            // Name (truncated)
            if (playerNameText != null)
            {
                string displayName = _playerData.DisplayName;
                if (displayName.Length > maxNameLength)
                {
                    displayName = displayName.Substring(0, maxNameLength - 2) + "..";
                }
                playerNameText.text = displayName;
            }
            
            // VIP Indicator
            if (vipIndicator != null)
            {
                vipIndicator.gameObject.SetActive(_playerData.IsVIP);
                if (_playerData.IsVIP)
                {
                    vipIndicator.color = vipGlowColor;
                }
            }
            
            // Rank Badge
            UpdateRankBadge();
            
            // Profile Frame
            UpdateProfileFrame();
            
            // Profile Picture
            UpdateProfilePicture();
            
            // Hide role by default
            HideRole();
        }
        
        private void UpdateRankBadge()
        {
            if (rankBadge == null) return;
            
            Sprite icon = GetRankSprite(_playerData.Rank);
            if (icon != null)
            {
                rankBadge.sprite = icon;
                rankBadge.gameObject.SetActive(true);
            }
            else
            {
                rankBadge.gameObject.SetActive(false);
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
            
            string frameID = _playerData.ProfileFrameID;
            ProfileFrameData frameData = customizationDB.GetFrame(frameID);
            
            if (frameData != null && frameData.FrameSprite != null)
            {
                profileFrame.sprite = frameData.FrameSprite;
                profileFrame.color = frameData.FrameColor;
                profileFrame.gameObject.SetActive(true);
            }
        }
        
        private void UpdateProfilePicture()
        {
            if (profilePicture == null) return;
            
            // TODO: Load from URL or cache
            profilePicture.color = Color.white;
        }
        
        #endregion
    }
}
