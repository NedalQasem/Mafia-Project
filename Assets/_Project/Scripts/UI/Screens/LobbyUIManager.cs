using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MafiaNight.Core;
using MafiaNight.Economy;
using System.Collections;
using MafiaNight.Managers;
using MafiaNight.Player;

namespace MafiaNight.UI
{
    /// <summary>
    /// Manages Lobby UI - player list, ready states, start game, timer system
    /// </summary>
    public class LobbyUIManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private Transform playerListContainer;
        [SerializeField] private GameObject playerListItemPrefab;
        
        [Header("Buttons")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveButton;
        
        [Header("Text")]
        [SerializeField] private TextMeshProUGUI roomTierText;
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI timerText; // NEW: Timer display
        
        [Header("Timer Settings")]
        [SerializeField] private float waitingTimerDuration = 120f; // 2 minutes
        [SerializeField] private float autoStartTimerDuration = 30f; // 30 seconds
        [SerializeField] private int minPlayersToStart = 4; // Minimum players required
        [SerializeField] private int maxPlayers = 10;

        private GameManager gameManager;
        private CurrencyManager currencyManager;
        private bool isReady = false;
        
        // Timer state
        private float currentTimer;
        private bool isTimerActive = false;
        private bool isAutoStartMode = false; // false = waiting mode, true = auto-start mode
        private int entryFeePaid = 0; // Store entry fee for refund

        private void Start()
        {
            gameManager = GameManager.Instance;
            currencyManager = CurrencyManager.Instance;
            
            // 1. Register Local Player (The missing link!)
            if (gameManager != null && PlayerProfileManager.Instance != null)
            {
                var localProfile = PlayerProfileManager.Instance.LocalProfile;
                if (localProfile != null)
                {
                    // Create PlayerData from Profile
                    var visualData = new PlayerPublicData
                    {
                        DisplayName = localProfile.DisplayName,
                        ProfilePictureID = localProfile.Customization.ProfilePictureID,
                        ProfileFrameID = localProfile.Customization.ProfileFrameID,
                        IsVIP = localProfile.IsVIP,
                        Rank = localProfile.Rank
                    };

                    var playerData = new PlayerData(
                        localProfile.DisplayName, 
                        localProfile.PlayFabId, 
                        "", // EOS ID not stored in Profile, using empty
                        visualData
                    );

                    // Register into Game Manager
                    gameManager.RegisterPlayer(playerData);
                    Debug.Log($"[LobbyUI] Auto-joined local player: {playerData.PlayerName}");
                }
            }
            
            // Setup button listeners
            if (readyButton != null)
            {
                readyButton.onClick.AddListener(OnReadyButtonClicked);
            }
            
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameClicked);
                startGameButton.interactable = false; // Disabled until conditions met
            }
            
            if (leaveButton != null)
            {
                leaveButton.onClick.AddListener(OnLeaveButtonClicked);
            }

            // Subscribe to game state changes
            if (gameManager != null)
            {
                gameManager.OnGameStateChanged += OnGameStateChanged;
            }
            
            // Initialize room tier display
            string selectedTier = PlayerPrefs.GetString("SelectedRoomTier", "Starter");
            if (roomTierText != null)
            {
                roomTierText.text = $"{selectedTier} Room";
            }
            
            // Get entry fee for potential refund
            entryFeePaid = PlayerPrefs.GetInt("EntryFeePaid", 0);
            
            // Start waiting timer
            StartWaitingTimer();
            
            // Force update UI
            UpdatePlayerCount();

            Debug.Log($"[LobbyUI] Initialized - Room: {selectedTier}, Entry Fee: {entryFeePaid}");
        }

        private void OnDestroy()
        {
            // Cleanup
            if (readyButton != null)
            {
                readyButton.onClick.RemoveListener(OnReadyButtonClicked);
            }
            
            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveListener(OnStartGameClicked);
            }
            
            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveListener(OnLeaveButtonClicked);
            }

            if (gameManager != null)
            {
                gameManager.OnGameStateChanged -= OnGameStateChanged;
            }
        }

        private void Update()
        {
            UpdatePlayerCount();
            UpdateTimer();
        }

        #region Timer System
        
        /// <summary>
        /// Start 2-minute waiting timer
        /// </summary>
        private void StartWaitingTimer()
        {
            currentTimer = waitingTimerDuration;
            isTimerActive = true;
            isAutoStartMode = false;
            Debug.Log("[LobbyUI] Started waiting timer (2 minutes)");
        }
        
        /// <summary>
        /// Switch to 30-second auto-start timer
        /// </summary>
        private void StartAutoStartTimer()
        {
            currentTimer = autoStartTimerDuration;
            isTimerActive = true;
            isAutoStartMode = true;
            Debug.Log("[LobbyUI] Switched to auto-start timer (30 seconds)");
        }
        
        /// <summary>
        /// Update timer each frame
        /// </summary>
        private void UpdateTimer()
        {
            if (!isTimerActive) return;
            
            // Check if we should switch modes
            int playerCount = gameManager != null ? gameManager.GetAllPlayers().Count : 0;
            
            // If lobby is full and we're in waiting mode, switch to auto-start
            if (!isAutoStartMode && playerCount >= minPlayersToStart)
            {
                StartAutoStartTimer();
            }
            
            // If lobby dropped below minimum and we're in auto-start mode, switch back
            if (isAutoStartMode && playerCount < minPlayersToStart)
            {
                StartWaitingTimer();
            }
            
            // Countdown
            currentTimer -= Time.deltaTime;
            
            // Update display
            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(currentTimer / 60f);
                int seconds = Mathf.FloorToInt(currentTimer % 60f);
                timerText.text = $"{minutes:00}:{seconds:00}";
                
                // Color warning (last 10 seconds)
                if (currentTimer <= 10f)
                {
                    timerText.color = Color.red;
                }
                else
                {
                    timerText.color = Color.white;
                }
            }
            
            // Timer expired
            if (currentTimer <= 0f)
            {
                OnTimerExpired();
            }
        }
        
        /// <summary>
        /// Handle timer expiration
        /// </summary>
        private void OnTimerExpired()
        {
            isTimerActive = false;
            
            int playerCount = gameManager != null ? gameManager.GetAllPlayers().Count : 0;
            
            if (isAutoStartMode)
            {
                // Auto-start game
                Debug.Log("[LobbyUI] Auto-start timer expired - Starting game!");
                OnStartGameClicked();
            }
            else
            {
                // Waiting timer expired - kick to MainMenu with refund
                Debug.Log("[LobbyUI] Waiting timer expired - Kicking to MainMenu with refund");
                RefundAndKick();
            }
        }
        
        /// <summary>
        /// Refund entry fee and return to MainMenu
        /// </summary>
        private void RefundAndKick()
        {
            if (currencyManager != null && entryFeePaid > 0)
            {
                currencyManager.AddCoins(entryFeePaid, 
                    () => {
                        Debug.Log($"[LobbyUI] Refunded {entryFeePaid} coins");
                        
                        // Show message (optional)
                        if (timerText != null)
                        {
                            timerText.text = "Lobby timeout - Refunded!";
                            timerText.color = Color.yellow;
                        }
                        
                        // Return to MainMenu after 2 seconds
                        StartCoroutine(ReturnToMainMenuDelayed(2f));
                    },
                    (error) => {
                        Debug.LogError($"[LobbyUI] Refund failed: {error}");
                        // Still kick even if refund fails
                        StartCoroutine(ReturnToMainMenuDelayed(1f));
                    }
                );
            }
            else
            {
                // No refund needed, just kick
                StartCoroutine(ReturnToMainMenuDelayed(1f));
            }
        }
        
        private IEnumerator ReturnToMainMenuDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            SceneTransitionManager.Instance?.LoadScene("MainMenu");
        }
        
        #endregion

        #region Button Handlers

        private void OnReadyButtonClicked()
        {
            isReady = !isReady;
            
            // Update button text
            var buttonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = isReady ? "Not Ready" : "Ready";
            }

            Debug.Log($"[LobbyUI] Player ready state: {isReady}");
            
            // TODO: Send ready state to server/network
        }

        private void OnStartGameClicked()
        {
            if (gameManager != null)
            {
                // Stop timer
                isTimerActive = false;
                
                gameManager.StartGame();
                SceneTransitionManager.Instance?.LoadScene("Gameplay");
                Debug.Log("[LobbyUI] Start Game clicked");
            }
        }
        
        private void OnLeaveButtonClicked()
        {
            Debug.Log("[LobbyUI] Leaving lobby...");
            
            // Stop timer and refund
            isTimerActive = false;
            RefundAndKick();
        }

        #endregion

        #region UI Updates

        private void UpdatePlayerCount()
        {
            if (gameManager == null || playerCountText == null) return;

            int playerCount = gameManager.GetAllPlayers().Count;
            playerCountText.text = $"{playerCount}/{maxPlayers}";

            // Enable start button if enough players
            if (startGameButton != null)
            {
                startGameButton.interactable = playerCount >= minPlayersToStart;
            }
        }

        public void SetRoomCode(string code)
        {
            if (roomCodeText != null)
            {
                roomCodeText.text = code;
            }
        }

        #endregion

        #region Game State Reactions

        private void OnGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.Lobby:
                    ShowLobby();
                    break;

                case GameState.RoleAssignment:
                case GameState.Night:
                case GameState.Day:
                    HideLobby();
                    break;
            }
        }

        private void ShowLobby()
        {
            if (lobbyPanel != null)
            {
                lobbyPanel.SetActive(true);
            }
        }

        private void HideLobby()
        {
            if (lobbyPanel != null)
            {
                lobbyPanel.SetActive(false);
            }
        }

        #endregion
    }
}
