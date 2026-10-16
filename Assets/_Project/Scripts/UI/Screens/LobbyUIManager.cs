using System.Collections;
using MafiaNight.Core;
using MafiaNight.Data.Player;
using MafiaNight.Gameplay.Match;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Economy;
using MafiaNight.Services.Profile;
using MafiaNight.Services.Scenes;
using MafiaNight.Services.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Screens
{
    /// <summary>
    /// Manages Lobby UI - player list, ready states, start game, timer system
    /// </summary>
    public class LobbyUIManager : MonoBehaviour
    {
        private const string Tag = "LobbyUI";

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
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Timer Settings")]
        [SerializeField] private float waitingTimerDuration = 120f; // 2 minutes
        [SerializeField] private float autoStartTimerDuration = 30f; // 30 seconds
        [SerializeField] private int minPlayersToStart = GameConstants.MIN_PLAYERS_TO_START;
        [SerializeField] private int maxPlayers = 10;

        private GameManager _gameManager;
        private ICurrencyService _currency;
        private RoomSession _session;
        private bool isReady = false;
        private bool _isLeaving = false;

        // Timer state
        private float currentTimer;
        private bool isTimerActive = false;
        private bool isAutoStartMode = false; // false = waiting mode, true = auto-start mode
        private int _lastShownPlayerCount = -1;

        private int PlayerCount => _gameManager != null ? _gameManager.AllPlayers.Count : 0;

        private void Start()
        {
            ServiceLocator.TryGet(out _gameManager);
            ServiceLocator.TryGet(out _currency);
            ServiceLocator.TryGet(out _session);

            if (_session != null && _session.HasRoom)
            {
                maxPlayers = _session.SelectedTier.MaxPlayers;
            }

            if (_gameManager != null)
            {
                // GameManager persists between scenes: start every lobby clean
                _gameManager.ResetMatch();
                RegisterLocalPlayer();
                _gameManager.OnGameStateChanged += OnGameStateChanged;
            }

            if (readyButton != null) readyButton.onClick.AddListener(OnReadyButtonClicked);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveButtonClicked);
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameClicked);
                startGameButton.interactable = false; // Disabled until conditions met
            }

            string tierName = _session != null && _session.HasRoom ? _session.SelectedTier.TierName : "Starter";
            if (roomTierText != null) roomTierText.text = $"{tierName} Room";

            StartWaitingTimer();
            UpdatePlayerCount();

            Log.Info($"Initialized - Room: {tierName}, Entry Fee: {_session?.EntryFeePaid ?? 0}", Tag);
        }

        private void RegisterLocalPlayer()
        {
            if (!ServiceLocator.TryGet<IPlayerProfileService>(out var profileService) || !profileService.HasProfile) return;

            var profile = profileService.LocalProfile;
            ServiceLocator.TryGet<IAuthService>(out var auth);

            var playerData = new PlayerData(
                profile.DisplayName,
                profile.PlayFabId,
                auth?.EOSAccountId ?? "",
                profileService.PublicData);

            _gameManager.RegisterPlayer(playerData);
            Log.Info($"Local player joined: {playerData.PlayerName}", Tag);
        }

        private void OnDestroy()
        {
            if (readyButton != null) readyButton.onClick.RemoveListener(OnReadyButtonClicked);
            if (startGameButton != null) startGameButton.onClick.RemoveListener(OnStartGameClicked);
            if (leaveButton != null) leaveButton.onClick.RemoveListener(OnLeaveButtonClicked);

            if (_gameManager != null) _gameManager.OnGameStateChanged -= OnGameStateChanged;
        }

        private void Update()
        {
            UpdatePlayerCount();
            UpdateTimer();
        }

        #region Timer System

        private void StartWaitingTimer()
        {
            currentTimer = waitingTimerDuration;
            isTimerActive = true;
            isAutoStartMode = false;
        }

        private void StartAutoStartTimer()
        {
            currentTimer = autoStartTimerDuration;
            isTimerActive = true;
            isAutoStartMode = true;
        }

        private void UpdateTimer()
        {
            if (!isTimerActive) return;

            int playerCount = PlayerCount;

            // Enough players → switch to the short auto-start countdown, and back if players leave
            if (!isAutoStartMode && playerCount >= minPlayersToStart) StartAutoStartTimer();
            if (isAutoStartMode && playerCount < minPlayersToStart) StartWaitingTimer();

            currentTimer -= Time.deltaTime;

            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(currentTimer / 60f);
                int seconds = Mathf.FloorToInt(currentTimer % 60f);
                timerText.text = $"{minutes:00}:{seconds:00}";
                timerText.color = currentTimer <= 10f ? Color.red : Color.white;
            }

            if (currentTimer <= 0f)
            {
                OnTimerExpired();
            }
        }

        private void OnTimerExpired()
        {
            isTimerActive = false;

            if (isAutoStartMode)
            {
                Log.Info("Auto-start timer expired - Starting game!", Tag);
                OnStartGameClicked();
            }
            else
            {
                Log.Info("Waiting timer expired - returning to Main Menu with refund", Tag);
                RefundAndLeave();
            }
        }

        /// <summary>Refunds the entry fee (once) and returns to the Main Menu.</summary>
        private void RefundAndLeave()
        {
            if (_isLeaving) return;
            _isLeaving = true;
            isTimerActive = false;

            int refund = _session != null ? _session.TakeRefund() : 0;
            _session?.Clear();
            _gameManager?.ResetMatch();

            if (_currency == null || refund <= 0)
            {
                StartCoroutine(ReturnToMainMenuDelayed(1f));
                return;
            }

            _currency.AddCoins(refund,
                onSuccess: () =>
                {
                    Log.Info($"Refunded {refund} coins", Tag);
                    if (timerText != null)
                    {
                        timerText.text = "Lobby timeout - Refunded!";
                        timerText.color = Color.yellow;
                    }
                    StartCoroutine(ReturnToMainMenuDelayed(2f));
                },
                onError: error =>
                {
                    Log.Error($"Refund failed: {error}", Tag);
                    StartCoroutine(ReturnToMainMenuDelayed(1f));
                });
        }

        private IEnumerator ReturnToMainMenuDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            ServiceLocator.Get<ISceneLoader>().LoadScene(GameConstants.SCENE_MAIN_MENU);
        }

        #endregion

        #region Button Handlers

        private void OnReadyButtonClicked()
        {
            isReady = !isReady;

            var buttonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null) buttonText.text = isReady ? "Not Ready" : "Ready";

            // TODO(network): Send ready state through the EOS lobby
        }

        private void OnStartGameClicked()
        {
            if (_gameManager == null || _isLeaving) return;

            isTimerActive = false;
            _session?.MarkFeeSpent();

            _gameManager.StartGame();
            ServiceLocator.Get<ISceneLoader>().LoadScene(GameConstants.SCENE_GAMEPLAY);
        }

        private void OnLeaveButtonClicked()
        {
            Log.Info("Leaving lobby...", Tag);
            RefundAndLeave();
        }

        #endregion

        #region UI Updates

        private void UpdatePlayerCount()
        {
            int playerCount = PlayerCount;
            if (playerCount == _lastShownPlayerCount) return;
            _lastShownPlayerCount = playerCount;

            if (playerCountText != null) playerCountText.text = $"{playerCount}/{maxPlayers}";
            if (startGameButton != null) startGameButton.interactable = playerCount >= minPlayersToStart;
        }

        public void SetRoomCode(string code)
        {
            if (roomCodeText != null) roomCodeText.text = code;
        }

        #endregion

        #region Game State Reactions

        private void OnGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.Lobby:
                    if (lobbyPanel != null) lobbyPanel.SetActive(true);
                    break;

                case GameState.RoleAssignment:
                case GameState.Night:
                case GameState.Day:
                    if (lobbyPanel != null) lobbyPanel.SetActive(false);
                    break;
            }
        }

        #endregion
    }
}
