using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MafiaNight.Core;

namespace MafiaNight.UI
{
    /// <summary>
    /// Manages Lobby UI - player list, ready states, start game
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

        private GameManager gameManager;
        private bool isReady = false;

        private void Start()
        {
            gameManager = GameManager.Instance;
            
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

            Debug.Log($"[LobbyUI] Initialized - Room: {selectedTier}");
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
        }

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
                gameManager.StartGame();
                SceneTransitionManager.Instance.LoadScene("Gameplay");
                Debug.Log("[LobbyUI] Start Game clicked");
            }
        }
        
        private void OnLeaveButtonClicked()
        {
            Debug.Log("[LobbyUI] Leaving lobby...");
            
            // TODO: Leave EOS session properly
            SceneTransitionManager.Instance.LoadScene("MainMenu");
        }

        #endregion

        #region UI Updates

        private void UpdatePlayerCount()
        {
            if (gameManager == null || playerCountText == null) return;

            int playerCount = gameManager.GetAllPlayers().Count;
            playerCountText.text = $"Players: {playerCount}/10";

            // Enable start button if enough players
            if (startGameButton != null)
            {
                startGameButton.interactable = playerCount >= 4;
            }
        }

        public void SetRoomCode(string code)
        {
            if (roomCodeText != null)
            {
                roomCodeText.text = $"Room: {code}";
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
