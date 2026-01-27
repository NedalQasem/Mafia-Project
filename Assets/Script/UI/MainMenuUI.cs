using UnityEngine;
using TMPro;
using MafiaNight.Economy;
using MafiaNight.Core; // For SceneTransitionManager

namespace MafiaNight.UI
{
    /// <summary>
    /// Main menu UI controller - displays player profile and handles room selection
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Profile Display")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private TextMeshProUGUI gemsText;

        [Header("Room Buttons")]
        [SerializeField] private UnityEngine.UI.Button starterRoomButton;
        [SerializeField] private UnityEngine.UI.Button proRoomButton;
        [SerializeField] private UnityEngine.UI.Button eliteRoomButton;

        [Header("Room Tier Data")]
        [SerializeField] private MafiaNight.Data.RoomTierData starterRoomTier;
        [SerializeField] private MafiaNight.Data.RoomTierData proRoomTier;
        [SerializeField] private MafiaNight.Data.RoomTierData eliteRoomTier;

        private CurrencyManager currencyManager;

        private void Start()
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

            // Setup button listeners
            if (starterRoomButton != null && starterRoomTier != null)
                starterRoomButton.onClick.AddListener(() => OnRoomButtonClicked(starterRoomTier));

            if (proRoomButton != null && proRoomTier != null)
                proRoomButton.onClick.AddListener(() => OnRoomButtonClicked(proRoomTier));

            if (eliteRoomButton != null && eliteRoomTier != null)
                eliteRoomButton.onClick.AddListener(() => OnRoomButtonClicked(eliteRoomTier));

            // Fetch player name from PlayFab
            FetchPlayerName();

            Debug.Log("[MainMenuUI] Initialized");
        }

        private void FetchPlayerName()
    {
        // Get name from PlayFabEOSLogin (synced with PlayFab)
        PlayFabEOSLogin eosLogin = FindObjectOfType<PlayFabEOSLogin>();
        
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
        else
        {
            // Fallback: Try to login directly with saved CustomID (for testing/development)
            Debug.LogWarning("[MainMenuUI] PlayFab not logged in via EOS - attempting direct login");
            TryDirectPlayFabLogin();
        }
    }
    
    private void TryDirectPlayFabLogin()
    {
        // Try to get saved CustomID from PlayerPrefs (from previous session)
        string savedCustomId = PlayerPrefs.GetString("SavedEOSCustomID", "");
        
        if (!string.IsNullOrEmpty(savedCustomId))
        {
            Debug.Log($"[MainMenuUI] Found saved CustomID: {savedCustomId} - attempting login");
            
            var request = new PlayFab.ClientModels.LoginWithCustomIDRequest
            {
                CustomId = savedCustomId,
                CreateAccount = false // Don't create - account should exist
            };
            
            PlayFab.PlayFabClientAPI.LoginWithCustomID(request, 
                (result) =>
                {
                    Debug.Log($"[MainMenuUI] ✅ Direct PlayFab login successful!");
                    FetchDisplayNameDirect();
                },
                (error) =>
                {
                    Debug.LogError($"[MainMenuUI] Direct login failed: {error.ErrorMessage}");
                    ShowPlaceholderName();
                });
        }
        else
        {
            Debug.LogWarning("[MainMenuUI] No saved CustomID - cannot login");
            ShowPlaceholderName();
        }
    }
    
    private void FetchDisplayNameDirect()
    {
        var request = new PlayFab.ClientModels.GetPlayerProfileRequest
        {
            ProfileConstraints = new PlayFab.ClientModels.PlayerProfileViewConstraints
            {
                ShowDisplayName = true
            }
        };
        
        PlayFab.PlayFabClientAPI.GetPlayerProfile(request,
            (result) =>
            {
                string displayName = result.PlayerProfile?.DisplayName ?? "Player";
                if (playerNameText != null)
                {
                    playerNameText.SetText(displayName);
                    Debug.Log($"[MainMenuUI] ✅ Display name fetched: {displayName}");
                }
            },
            (error) =>
            {
                Debug.LogError($"[MainMenuUI] Failed to fetch display name: {error.ErrorMessage}");
                ShowPlaceholderName();
            });
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
            // Cleanup
            if (currencyManager != null)
            {
                currencyManager.OnCoinsChanged -= UpdateCoinsDisplay;
                currencyManager.OnGemsChanged -= UpdateGemsDisplay;
            }

            if (starterRoomButton != null)
                starterRoomButton.onClick.RemoveAllListeners();

            if (proRoomButton != null)
                proRoomButton.onClick.RemoveAllListeners();

            if (eliteRoomButton != null)
                eliteRoomButton.onClick.RemoveAllListeners();
        }

        #region Display Updates

        private void UpdateCoinsDisplay(int amount)
        {
            if (coinsText != null)
            {
                coinsText.text = $"Coins: {amount}";
            }
        }

        private void UpdateGemsDisplay(int amount)
        {
            if (gemsText != null)
            {
                gemsText.text = $"Gems: {amount}";
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
                    PlayerPrefs.SetString("SelectedRoomTier", roomTier.TierName);
                    SceneTransitionManager.Instance.LoadScene("Lobby");
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
    }
}
