using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MafiaNight.Core;
using MafiaNight.Gameplay.Match;
using MafiaNight.Services.Scenes;

namespace MafiaNight.UI.Screens
{
    /// <summary>
    /// Manages the Gameplay Scene UI - phase display, timer, role info, player list
    /// </summary>
    public class GameplayUIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject gameplayPanel;
        [SerializeField] private GameObject phasePanel;
        [SerializeField] private GameObject rolePanel;
        // [SerializeField] private GameObject playerListPanel; // DEPRECATED
        
        [Header("Text Elements")]
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI roleText;
        [SerializeField] private TextMeshProUGUI roleDescriptionText;
        
        // [Header("Player List")] - DEPRECATED
        // [SerializeField] private Transform playerListContainer;
        // [SerializeField] private GameObject playerItemPrefab;
        
        [Header("Action Buttons")]
        [SerializeField] private Button voteButton;
        [SerializeField] private Button actionButton; // For night actions (kill, investigate, etc)
        [SerializeField] private Button leaveButton;
        
        // State
        private GameManager gameManager;
        private float phaseTimeRemaining = 60f;
        private GameState currentPhase = GameState.Night;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            ValidateReferences();
        }
        
        private void Start()
        {
            ServiceLocator.TryGet(out gameManager);
            
            if (gameManager != null)
            {
                gameManager.OnGameStateChanged += OnGameStateChanged;
            }
            
            SetupButtonListeners();
            InitializeUI();
            
            Debug.Log("[GameplayUI] Initialized");
        }
        
        private void Update()
        {
            UpdateTimer();
        }
        
        private void OnDestroy()
        {
            CleanupButtonListeners();
            
            if (gameManager != null)
            {
                gameManager.OnGameStateChanged -= OnGameStateChanged;
            }
        }
        
        #endregion
        
        #region Initialization
        
        private void ValidateReferences()
        {
            if (gameplayPanel == null)
                Debug.LogError("[GameplayUI] Gameplay Panel is not assigned!");
            
            if (phasePanel == null)
                Debug.LogError("[GameplayUI] Phase Panel is not assigned!");
            
            if (phaseText == null)
                Debug.LogError("[GameplayUI] Phase Text is not assigned!");
            
            if (timerText == null)
                Debug.LogError("[GameplayUI] Timer Text is not assigned!");
            
            if (roleText == null)
                Debug.LogError("[GameplayUI] Role Text is not assigned!");
        }
        
        private void SetupButtonListeners()
        {
            if (voteButton != null)
                voteButton.onClick.AddListener(OnVoteButtonClicked);
            
            if (actionButton != null)
                actionButton.onClick.AddListener(OnActionButtonClicked);
            
            if (leaveButton != null)
                leaveButton.onClick.AddListener(OnLeaveButtonClicked);
        }
        
        private void CleanupButtonListeners()
        {
            if (voteButton != null)
                voteButton.onClick.RemoveListener(OnVoteButtonClicked);
            
            if (actionButton != null)
                actionButton.onClick.RemoveListener(OnActionButtonClicked);
            
            if (leaveButton != null)
                leaveButton.onClick.RemoveListener(OnLeaveButtonClicked);
        }
        
        private void InitializeUI()
        {
            // Set initial phase
            UpdatePhaseDisplay(GameState.Night);
            
            // Set placeholder role (will be updated by network)
            SetRole("???", ""); // Clear description initially
            
            if (roleDescriptionText != null) roleDescriptionText.text = "";
            
            // Hide action buttons initially
            if (voteButton != null)
                voteButton.gameObject.SetActive(false);
            
            if (actionButton != null)
                actionButton.gameObject.SetActive(false);
        }
        
        #endregion
        
        #region Phase Management
        
        private void OnGameStateChanged(GameState newState)
        {
            currentPhase = newState;
            UpdatePhaseDisplay(newState);
            UpdateActionButtons(newState);
            
            Debug.Log($"[GameplayUI] Phase changed to: {newState}");
        }
        
        private void UpdatePhaseDisplay(GameState phase)
        {
            if (phaseText == null) return;
            
            switch (phase)
            {
                case GameState.Night:
                    phaseText.text = "🌙 Night Phase";
                    phaseText.color = new Color(0.5f, 0.1f, 0.1f); // Dark red
                    phaseTimeRemaining = 60f;
                    break;
                
                case GameState.Day:
                    phaseText.text = "☀️ Day Phase";
                    phaseText.color = new Color(1f, 0.8f, 0.2f); // Yellow
                    phaseTimeRemaining = 90f;
                    break;
                
                case GameState.Voting:
                    phaseText.text = "🗳️ Voting Phase";
                    phaseText.color = new Color(0.2f, 0.5f, 1f); // Blue
                    phaseTimeRemaining = 45f;
                    break;
                
                default:
                    phaseText.text = "Waiting...";
                    phaseText.color = Color.white;
                    break;
            }
        }
        
        private void UpdateActionButtons(GameState phase)
        {
            if (voteButton == null || actionButton == null) return;
            
            switch (phase)
            {
                case GameState.Night:
                    voteButton.gameObject.SetActive(false);
                    actionButton.gameObject.SetActive(true);
                    break;
                
                case GameState.Day:
                    voteButton.gameObject.SetActive(false);
                    actionButton.gameObject.SetActive(false);
                    break;
                
                case GameState.Voting:
                    voteButton.gameObject.SetActive(true);
                    actionButton.gameObject.SetActive(false);
                    break;
            }
        }
        
        #endregion
        
        #region Timer
        
        private void UpdateTimer()
        {
            if (timerText == null) return;
            
            // Decrease timer (this will be synced from server in real game)
            phaseTimeRemaining -= Time.deltaTime;
            
            if (phaseTimeRemaining < 0)
            {
                phaseTimeRemaining = 60f; // Reset (temporary)
            }
            
            // Format: MM:SS
            int minutes = Mathf.FloorToInt(phaseTimeRemaining / 60f);
            int seconds = Mathf.FloorToInt(phaseTimeRemaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
            
            // Change color when time is running out
            if (phaseTimeRemaining < 10f)
            {
                timerText.color = Color.red;
            }
            else
            {
                timerText.color = Color.white;
            }
        }
        
        #endregion
        
        #region Role Management
        
        /// <summary>
        /// Sets the player's role display
        /// </summary>
        public void SetRole(string roleName, string description)
        {
            if (roleText != null)
            {
                roleText.text = $"Your Role: {roleName}";
            }
            
            if (roleDescriptionText != null)
            {
                roleDescriptionText.text = description;
            }
            
            Debug.Log($"[GameplayUI] Role set: {roleName}");
        }
        
        #endregion
        
        #region Player List Management (DEPRECATED - Handled by SeatManager)
        
        // Removed generic list to avoid UI clutter. 
        // Players are now visualized as 3D Seats in the scene.
        
        #endregion
        
        #region Button Handlers
        
        private void OnVoteButtonClicked()
        {
            Debug.Log("[GameplayUI] Vote button clicked");
            // TODO: Open voting panel
        }
        
        private void OnActionButtonClicked()
        {
            Debug.Log("[GameplayUI] Action button clicked");
            // TODO: Open action panel based on role
        }
        
        private void OnLeaveButtonClicked()
        {
            Debug.Log("[GameplayUI] Leaving game...");
            
            // TODO: Properly disconnect from EOS session
            ServiceLocator.Get<ISceneLoader>().LoadScene(GameConstants.SCENE_MAIN_MENU);
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Shows a notification message to the player
        /// </summary>
        public void ShowNotification(string message)
        {
            Debug.Log($"[GameplayUI] Notification: {message}");
            // TODO: Implement notification panel
        }
        
        /// <summary>
        /// Updates the phase timer from network
        /// </summary>
        public void SetPhaseTimer(float timeRemaining)
        {
            phaseTimeRemaining = timeRemaining;
        }
        
        /// <summary>Debug button hook (wired in the Gameplay scene).</summary>
        public void OnTestResultsClicked()
        {
            Debug.Log("[GameplayUI] Testing Results Scene");
            ServiceLocator.Get<ISceneLoader>().LoadScene(GameConstants.SCENE_RESULTS);
        }

        #endregion
    }
}
