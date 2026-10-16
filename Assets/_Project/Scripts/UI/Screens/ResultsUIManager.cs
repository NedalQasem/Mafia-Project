using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MafiaNight.Core;
using MafiaNight.Services.Economy;
using MafiaNight.Services.Scenes;

namespace MafiaNight.UI.Screens
{
    /// <summary>
    /// Manages the Results Scene UI - displays game outcome, rewards, and navigation
    /// </summary>
    public class ResultsUIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private GameObject winnerPanel;
        [SerializeField] private GameObject rewardsPanel;
        [SerializeField] private GameObject mvpPanel;
        
        [Header("Text Elements")]
        [SerializeField] private TextMeshProUGUI winnerText;
        [SerializeField] private TextMeshProUGUI winnerDescriptionText;
        [SerializeField] private TextMeshProUGUI coinsEarnedText;
        [SerializeField] private TextMeshProUGUI gemsEarnedText;
        [SerializeField] private TextMeshProUGUI mvpPlayerText;
        
        [Header("Buttons")]
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button mainMenuButton;
        
        [Header("Animation")]
        [SerializeField] private float rewardAnimationDuration = 2f;
        
        // State
        private ICurrencyService currencyManager;
        private int coinsEarned = 0;
        private int gemsEarned = 0;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            ValidateReferences();
        }
        
        private void Start()
        {
            ServiceLocator.TryGet(out currencyManager);
            SetupButtonListeners();
            
            // Initialize with placeholder data
            // (will be replaced with real data from GameManager)
            InitializeResults();
            
            Debug.Log("[ResultsUI] Initialized");
        }
        
        private void OnDestroy()
        {
            CleanupButtonListeners();
        }
        
        #endregion
        
        #region Initialization
        
        private void ValidateReferences()
        {
            if (resultsPanel == null)
                Debug.LogError("[ResultsUI] Results Panel is not assigned!");
            
            if (winnerText == null)
                Debug.LogError("[ResultsUI] Winner Text is not assigned!");
            
            if (coinsEarnedText == null)
                Debug.LogError("[ResultsUI] Coins Earned Text is not assigned!");
            
            if (playAgainButton == null)
                Debug.LogError("[ResultsUI] Play Again Button is not assigned!");
            
            if (mainMenuButton == null)
                Debug.LogError("[ResultsUI] Main Menu Button is not assigned!");
        }
        
        private void SetupButtonListeners()
        {
            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(OnPlayAgainClicked);
            
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }
        
        private void CleanupButtonListeners()
        {
            if (playAgainButton != null)
                playAgainButton.onClick.RemoveListener(OnPlayAgainClicked);
            
            if (mainMenuButton != null)
                mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
        }
        
        private void InitializeResults()
        {
            // Placeholder data - will be replaced by network data
            bool townWon = Random.value > 0.5f;
            int testCoins = Random.Range(50, 150);
            int testGems = Random.Range(0, 5);
            
            DisplayResults(townWon, testCoins, testGems, "Player1");
        }
        
        #endregion
        
        #region Results Display
        
        /// <summary>
        /// Displays the game results
        /// </summary>
        public void DisplayResults(bool townWon, int coins, int gems, string mvpPlayer)
        {
            coinsEarned = coins;
            gemsEarned = gems;
            
            // Display winner
            if (townWon)
            {
                SetWinner("🏛️ Town Wins!", "The citizens have protected the town!", new Color(0.2f, 0.8f, 0.3f));
            }
            else
            {
                SetWinner("🌑 Mafia Wins!", "Darkness has taken over the town...", new Color(0.8f, 0.2f, 0.2f));
            }
            
            // Display MVP
            if (mvpPanel != null && mvpPlayerText != null && !string.IsNullOrEmpty(mvpPlayer))
            {
                mvpPanel.SetActive(true);
                mvpPlayerText.text = $"⭐ MVP: {mvpPlayer}";
            }
            
            // Animate rewards
            StartCoroutine(AnimateRewards());
        }
        
        private void SetWinner(string winner, string description, Color color)
        {
            if (winnerText != null)
            {
                winnerText.text = winner;
                winnerText.color = color;
            }
            
            if (winnerDescriptionText != null)
            {
                winnerDescriptionText.text = description;
            }
        }
        
        #endregion
        
        #region Rewards Animation
        
        private System.Collections.IEnumerator AnimateRewards()
        {
            // Start from 0
            int currentCoins = 0;
            int currentGems = 0;
            
            float elapsed = 0f;
            
            while (elapsed < rewardAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / rewardAnimationDuration;
                
                // Animate coins
                currentCoins = Mathf.RoundToInt(Mathf.Lerp(0, coinsEarned, progress));
                if (coinsEarnedText != null)
                {
                    coinsEarnedText.text = $"+{currentCoins} Coins";
                }
                
                // Animate gems
                if (gemsEarned > 0)
                {
                    currentGems = Mathf.RoundToInt(Mathf.Lerp(0, gemsEarned, progress));
                    if (gemsEarnedText != null)
                    {
                        gemsEarnedText.text = $"+{currentGems} Gems";
                    }
                }
                
                yield return null;
            }
            
            // Ensure final values are set
            if (coinsEarnedText != null)
                coinsEarnedText.text = $"+{coinsEarned} Coins";
            
            if (gemsEarnedText != null && gemsEarned > 0)
                gemsEarnedText.text = $"+{gemsEarned} Gems";
            
            // Add rewards to player's account
            AddRewardsToPlayer();
        }
        
        private void AddRewardsToPlayer()
        {
            if (currencyManager == null) return;
            
            // Add coins
            if (coinsEarned > 0)
            {
                currencyManager.AddCoins(coinsEarned, () =>
                {
                    Debug.Log($"[ResultsUI] Added {coinsEarned} coins");
                }, (error) =>
                {
                    Debug.LogError($"[ResultsUI] Failed to add coins: {error}");
                });
            }
            
            // Add gems
            if (gemsEarned > 0)
            {
                currencyManager.AddGems(gemsEarned, () =>
                {
                    Debug.Log($"[ResultsUI] Added {gemsEarned} gems");
                }, (error) =>
                {
                    Debug.LogError($"[ResultsUI] Failed to add gems: {error}");
                });
            }
        }
        
        #endregion
        
        #region Button Handlers
        
        private void OnPlayAgainClicked()
        {
            Debug.Log("[ResultsUI] Play Again clicked");
            
            // Return to main menu to select room again
            ServiceLocator.Get<ISceneLoader>().LoadScene(GameConstants.SCENE_MAIN_MENU);
        }
        
        private void OnMainMenuClicked()
        {
            Debug.Log("[ResultsUI] Main Menu clicked");
            
            ServiceLocator.Get<ISceneLoader>().LoadScene(GameConstants.SCENE_MAIN_MENU);
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Sets the game result data manually
        /// </summary>
        public void SetGameResult(string winnerTeam, int earnedCoins, int earnedGems, string mvp)
        {
            bool townWon = winnerTeam.ToLower().Contains("town");
            DisplayResults(townWon, earnedCoins, earnedGems, mvp);
        }
        
        #endregion
    }
}
