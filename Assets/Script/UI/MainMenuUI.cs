using System.Linq;
using UnityEngine;
using TMPro;
using MafiaNight.Economy;
using MafiaNight.Managers;
using MafiaNight.Player;
using MafiaNight.Core;

namespace MafiaNight.UI
{
    /// <summary>
    /// Main menu UI controller - displays player profile and handles room selection
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Profile Display")]
        [SerializeField] private UnityEngine.UI.Image avatarImage;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private TextMeshProUGUI gemsText;
        [SerializeField] private UnityEngine.UI.Button settingsButton;

        [Header("Player Profile Display")]
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private UnityEngine.UI.Image rankBadgeImage;
        [SerializeField] private UnityEngine.UI.Scrollbar xpProgressBar;
        [SerializeField] private TextMeshProUGUI xpText;

        [Header("Room Buttons")] // TODO: Replace with carousel
        [SerializeField] private UnityEngine.UI.Button starterRoomButton;
        [SerializeField] private UnityEngine.UI.Button proRoomButton;
        [SerializeField] private UnityEngine.UI.Button eliteRoomButton;

        [Header("Room Tier Data")]
        [SerializeField] private MafiaNight.Data.RoomTierData starterRoomTier;
        [SerializeField] private MafiaNight.Data.RoomTierData proRoomTier;
        [SerializeField] private MafiaNight.Data.RoomTierData eliteRoomTier;

        [Header("Layout Elements")]
        [SerializeField] private GameObject headerPanel;
        [SerializeField] private GameObject downBar;

        private CurrencyManager currencyManager;

        private async void Start()
        {
            currencyManager = CurrencyManager.Instance;

            if (currencyManager != null)
            {
                // Subscribe to currency changes
                currencyManager.OnCoinsChanged += UpdateCoinsDisplay;
                currencyManager.OnGemsChanged += UpdateGemsDisplay;

                // Initial display
                UpdateCoinsDisplay(currencyManager.GetCoins());
                UpdateGemsDisplay(currencyManager.GetGems());
            }
            
            // Setup settings button
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);

            // Setup room button listeners (TODO: Remove when carousel is added)
            // Ideally should be a dynamic list, but for now fixed buttons
            SetupRoomButton(starterRoomButton, starterRoomTier);
            SetupRoomButton(proRoomButton, proRoomTier);
            SetupRoomButton(eliteRoomButton, eliteRoomTier);

            // Fetch player name from PlayFab
            FetchPlayerName();
            
            // Update player profile display (Level, XP, Rank)
            UpdateProfileDisplay();

            Debug.Log("[MainMenuUI] Initialized");
            
            // Animate UI elements (sequential reveal)
            await AnimateUIElements();
        }

        private void SetupRoomButton(UnityEngine.UI.Button button, MafiaNight.Data.RoomTierData tierData)
        {
            if (button != null && tierData != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnRoomButtonClicked(tierData));
            }
        }

        private void FetchPlayerName()
        {
            // Get name from PlayFabEOSLogin (synced with PlayFab)
            // Use Singleton if available, otherwise find one (safe fallback)
            PlayFabEOSLogin eosLogin = PlayFabEOSLogin.Instance;
            if (eosLogin == null) eosLogin = FindObjectOfType<PlayFabEOSLogin>();
            
            if (eosLogin != null && eosLogin.IsPlayFabLoggedIn)
            {
                // Primary path: EOS is logged in
                eosLogin.GetDisplayName((displayName) =>
                {
                    if (playerNameText != null)
                    {
                        playerNameText.SetText(displayName);
                        Debug.Log($"[MainMenuUI] Player name loaded from PlayFab: {displayName}");
                    }
                });
            }
            else if (eosLogin != null)
            {
                // Fallback direct login via PlayFabEOSLogin manager
                Debug.LogWarning("[MainMenuUI] PlayFab not logged in via EOS - attempting direct login via Manager");
                eosLogin.TryDirectLoginWithSavedId(
                    onSuccess: () => 
                    {
                        Debug.Log("[MainMenuUI] Direct login success, fetching name...");
                        eosLogin.GetDisplayName(name => playerNameText?.SetText(name));
                    },
                    onFailure: (error) => 
                    {
                        ShowPlaceholderName();
                    }
                );
            }
            else
            {
                ShowPlaceholderName();
            }
        }
    
        private void ShowPlaceholderName()
        {
            if (playerNameText != null)
            {
                playerNameText.SetText("Guest");
            }
        }

        private void OnDestroy()
        {
            // Cleanup currency manager
            if (currencyManager != null)
            {
                currencyManager.OnCoinsChanged -= UpdateCoinsDisplay;
                currencyManager.OnGemsChanged -= UpdateGemsDisplay;
            }
            
            // Cleanup profile manager
            var profileManager = PlayerProfileManager.Instance;
            if (profileManager != null)
            {
                profileManager.OnProfileLoaded -= OnProfileLoaded;
                profileManager.OnProfileUpdated -= OnProfileUpdated;
            }
            
            if (settingsButton != null)
                settingsButton.onClick.RemoveAllListeners();

            if (starterRoomButton != null) starterRoomButton.onClick.RemoveAllListeners();
            if (proRoomButton != null) proRoomButton.onClick.RemoveAllListeners();
            if (eliteRoomButton != null) eliteRoomButton.onClick.RemoveAllListeners();
        }

        #region Display Updates

        private void UpdateCoinsDisplay(int amount)
        {
            if (coinsText != null)
            {
                coinsText.text = $"{amount}";
            }
        }

        private void UpdateGemsDisplay(int amount)
        {
            if (gemsText != null)
            {
                gemsText.text = $"{amount}";
            }
        }

        #endregion
        
        #region Player Profile Display
        
        private void UpdateProfileDisplay()
        {
            var profileManager = PlayerProfileManager.Instance;
            if (profileManager == null || profileManager.LocalProfile == null)
            {
                Debug.LogWarning("[MainMenuUI] PlayerProfile not loaded yet - subscribing to event");
                
                // Subscribe to profile loaded event
                if (profileManager != null)
                {
                    profileManager.OnProfileLoaded += OnProfileLoaded;
                    profileManager.OnProfileUpdated += OnProfileUpdated;
                }
                return;
            }
            
            DisplayProfile(profileManager.LocalProfile);
        }
        
        private void OnProfileLoaded(PlayerProfile profile)
        {
            DisplayProfile(profile);
        }
        
        private void OnProfileUpdated(PlayerProfile profile)
        {
            DisplayProfile(profile);
        }
        
        private void DisplayProfile(PlayerProfile profile)
        {
            if (profile == null) return;
            
            // Player Name
            if (playerNameText != null && !string.IsNullOrEmpty(profile.DisplayName))
            {
                playerNameText.text = profile.DisplayName;
            }
            
            // Level Display
            if (levelText != null)
                levelText.text = $"Level {profile.Level}";
            
            // Rank Display
            if (rankText != null)
                rankText.text = profile.Rank.ToString();
            
            // Rank Badge Color
            if (rankBadgeImage != null)
                rankBadgeImage.color = GetRankColor(profile.Rank);
            
            // XP Progress Bar
            if (xpProgressBar != null)
            {
                xpProgressBar.value = profile.GetLevelProgress();
            }
            
            // XP Text
            if (xpText != null)
                xpText.text = profile.GetLevelProgressText();
            
            Debug.Log($"[MainMenuUI] Profile displayed: {profile.DisplayName}, Level {profile.Level}, Rank {profile.Rank}");
        }
        
        private UnityEngine.Color GetRankColor(PlayerRank rank)
        {
            switch (rank)
            {
                case PlayerRank.Bronze:
                    return new UnityEngine.Color(0.8f, 0.5f, 0.2f); // Bronze
                case PlayerRank.Silver:
                    return new UnityEngine.Color(0.75f, 0.75f, 0.75f); // Silver
                case PlayerRank.Gold:
                    return new UnityEngine.Color(1f, 0.84f, 0f); // Gold
                case PlayerRank.Platinum:
                    return new UnityEngine.Color(0.3f, 0.8f, 0.8f); // Platinum (Teal)
                case PlayerRank.Diamond:
                    return new UnityEngine.Color(0.7f, 0.9f, 1f); // Diamond (Light Blue)
                case PlayerRank.Master:
                    return new UnityEngine.Color(0.9f, 0.3f, 0.9f); // Master (Purple)
                case PlayerRank.Grandmaster:
                    return new UnityEngine.Color(1f, 0.2f, 0.2f); // Grandmaster (Red)
                default:
                    return UnityEngine.Color.white;
            }
        }
        
        #endregion

        #region Button Handlers

        private void OnRoomButtonClicked(MafiaNight.Data.RoomTierData roomTier)
        {
            if (roomTier == null)
            {
                Debug.LogError("[MainMenuUI] Room tier data is null!");
                return;
            }

            Debug.Log($"[MainMenuUI] {roomTier.TierName} Room clicked - Fee: {roomTier.EntryFee}, Players: {roomTier.MaxPlayers}");

            // Check if player has enough coins
            if (currencyManager != null && currencyManager.HasEnoughCoins(roomTier.EntryFee))
            {
                Debug.Log($"Player has enough coins! Total Pot: {roomTier.GetTotalPot()}, Reward Pool: {roomTier.GetTotalRewardPool()}");
                currencyManager.DeductCoins(roomTier.EntryFee, () => {
                    // Store selected room tier for lobby
                    PlayerPrefs.SetString(GameConstants.PREF_SELECTED_ROOM_TIER, roomTier.TierName);
                    SceneTransitionManager.Instance.LoadScene(GameConstants.SCENE_LOBBY);
                }, (error) => {
                    Debug.LogError($"Failed to deduct coins: {error}");
                });
            }
            else
            {
                Debug.LogWarning($"Not enough coins! Need {roomTier.EntryFee}, have {currencyManager?.GetCoins()}");
                // TODO: Show "Not enough coins" popup
            }
        }


        #endregion
        
        #region UI Animations
        
        /// <summary>
        /// Animate UI elements on scene load (sequential reveal)
        /// </summary>
        private async System.Threading.Tasks.Task AnimateUIElements()
        {
            // Optional: Disable to test without animations
            bool enableAnimations = true;
            if (!enableAnimations) return;
            
            // Get UI elements (adjust GameObject names as per your hierarchy)
            GameObject[] roomCards = new GameObject[]
            {
                starterRoomButton?.gameObject,
                proRoomButton?.gameObject,
                eliteRoomButton?.gameObject
            };
            
            try
            {
                // Animate header first
                if (headerPanel != null)
                {
                    await MafiaNight.Animations.UIElementAnimator.ScaleAndFadeIn(
                        headerPanel, 
                        MafiaNight.Animations.AnimationPresets.NormalDuration
                    );
                }
                
                // Small delay
                await System.Threading.Tasks.Task.Delay(100);
                
                // Animate room cards sequentially
                var validCards = System.Linq.Enumerable.Where(roomCards, card => card != null).ToArray();
                if (validCards.Length > 0)
                {
                    await MafiaNight.Animations.UIElementAnimator.AnimateSequential(
                        validCards,
                        MafiaNight.Animations.AnimationPresets.ShortDelay,
                        MafiaNight.Animations.AnimationPresets.QuickDuration
                    );
                }
                
                // Animate bottom nav last
                if (downBar != null)
                {
                    var rectTransform = downBar.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        await MafiaNight.Animations.UIElementAnimator.SlideIn(
                            rectTransform,
                            MafiaNight.Animations.UIElementAnimator.SlideDirection.Up,
                            MafiaNight.Animations.AnimationPresets.NormalDuration,
                            distance: 200f
                        );
                    }
                }
                
                Debug.Log("[MainMenuUI] UI animations complete!");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MainMenuUI] Animation error (non-critical): {ex.Message}");
            }
        }
        
        #endregion
        
        #region Settings
        
        private void OnSettingsClicked()
        {
            Debug.Log("[MainMenuUI] Settings button clicked");
            // TODO: Open settings panel
            // - Audio settings
            // - Language
            // - Account management
            // - Logout
        }
        
        #endregion
    }
}
