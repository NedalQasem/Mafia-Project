using UnityEngine;
using MafiaNight.Core;

namespace MafiaNight.Player
{
    /// <summary>
    /// Represents individual player in the game
    /// Handles local player state and integrations
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Player Identity")]
        [SerializeField] private string playerName;
        [SerializeField] private bool isLocalPlayer = false;

        [Header("Player Data")]
        private PlayerData playerData;
        private PlayerRole assignedRole = PlayerRole.Civilian;
        private bool isAlive = true;

        [Header("Voice Chat")]
        [SerializeField] private bool isMuted = false;
        [SerializeField] private bool canSpeak = false;

        // References
        private GameManager gameManager;

        #region Unity Lifecycle

        private void Awake()
        {
            gameManager = GameManager.Instance;
        }

        private void Start()
        {
            if (gameManager != null)
            {
                gameManager.OnGameStateChanged += OnGameStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameStateChanged -= OnGameStateChanged;
            }
        }

        #endregion

        #region Initialization

        [Header("Visuals")]
        [SerializeField] private PlayerVisuals playerVisuals;

        public void Initialize(string name, string playFabId, string eosId, bool isLocal, PlayerPublicData visualData = null)
        {
            playerName = name;
            isLocalPlayer = isLocal;

            playerData = new PlayerData(name, playFabId, eosId, visualData);
            
            if (gameManager != null)
            {
                gameManager.RegisterPlayer(playerData);
            }

            // Init Visuals
            if (playerVisuals != null && visualData != null)
            {
                playerVisuals.SetVisuals(visualData);
            }

            Debug.Log($"Player {playerName} initialized (Local: {isLocal})");
        }

        public void AssignRole(PlayerRole role)
        {
            assignedRole = role;
            if (playerData != null)
            {
                playerData.Role = role;
            }

            Debug.Log($"Player {playerName} assigned role: {role}");

            // If local player, show role UI
            if (isLocalPlayer)
            {
                ShowRoleNotification(role);
            }
        }

        #endregion

        #region Game State Reactions

        private void OnGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.Night:
                    HandleNightPhase();
                    break;

                case GameState.Day:
                    HandleDayPhase();
                    break;

                case GameState.Voting:
                    HandleVotingPhase();
                    break;

                case GameState.Results:
                    HandleResultsPhase();
                    break;
            }
        }

        private void HandleNightPhase()
        {
            if (!isAlive || !isLocalPlayer) return;

            // Only Mafia can talk during night
            if (assignedRole == PlayerRole.Mafia)
            {
                EnableVoiceChat();
                Debug.Log($"{playerName} (Mafia) can now talk during night");
            }
            else
            {
                DisableVoiceChat();
            }
        }

        private void HandleDayPhase()
        {
            if (!isAlive || !isLocalPlayer) return;

            // Everyone alive can talk during day
            EnableVoiceChat();
            Debug.Log($"{playerName} can now talk during day");
        }

        private void HandleVotingPhase()
        {
            if (!isAlive || !isLocalPlayer) return;

            // Open voting UI for alive players
            Debug.Log($"{playerName} can now vote");
        }

        private void HandleResultsPhase()
        {
            // Process results (handled by GameManager)
        }

        #endregion

        #region Voice Chat Control

        public void EnableVoiceChat()
        {
            if (!isAlive) return;

            canSpeak = true;
            isMuted = false;
            
            // TODO: Enable microphone through EOS Voice
            Debug.Log($"{playerName}: Voice chat enabled");
        }

        public void DisableVoiceChat()
        {
            canSpeak = false;
            isMuted = true;

            // TODO: Disable microphone through EOS Voice
            Debug.Log($"{playerName}: Voice chat disabled");
        }

        public void ToggleMute()
        {
            if (!canSpeak) return;

            isMuted = !isMuted;
            // TODO: Mute/unmute microphone
            Debug.Log($"{playerName}: Microphone {(isMuted ? "muted" : "unmuted")}");
        }

        #endregion

        #region Player Actions

        public void Die()
        {
            isAlive = false;
            if (playerData != null)
            {
                playerData.IsAlive = false;
            }

            DisableVoiceChat();
            
            if (isLocalPlayer)
            {
                ShowDeathScreen();
            }

            Debug.Log($"{playerName} has died");
        }

        public void Vote(PlayerController target)
        {
            if (!isAlive || !isLocalPlayer) return;

            Debug.Log($"{playerName} voted for {target.playerName}");
            // TODO: Send vote to server/network
        }

        #endregion

        #region UI Notifications (Placeholders)

        private void ShowRoleNotification(PlayerRole role)
        {
            // TODO: Show UI popup with assigned role
            Debug.Log($">>> You are: {role} <<<");
        }

        private void ShowDeathScreen()
        {
            // TODO: Show death UI overlay
            Debug.Log(">>> You are dead - You can spectate <<<");
        }

        #endregion

        #region Public Getters

        public string GetPlayerName() => playerName;
        public PlayerRole GetRole() => assignedRole;
        public bool IsAlive() => isAlive;
        public bool IsLocal() => isLocalPlayer;
        public bool CanSpeak() => canSpeak;
        public bool IsMuted() => isMuted;
        public PlayerData GetPlayerData() => playerData;

        #endregion
    }
}
