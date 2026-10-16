using UnityEngine;
using System;
using System.Collections.Generic;
using MafiaNight.Player;

namespace MafiaNight.Core
{
    /// <summary>
    /// Central game manager controlling game flow and state
    /// Singleton pattern for global access
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game State")]
        [SerializeField] private GameState currentState = GameState.Lobby;
        
        [Header("Round Configuration")]
        [SerializeField] private int currentRound = 0;
        [SerializeField] private float nightDuration = 60f;
        [SerializeField] private float dayDuration = 120f;
        [SerializeField] private float votingDuration = 45f;

        [Header("Player Management")]
        private List<PlayerData> allPlayers = new List<PlayerData>();
        private List<PlayerData> alivePlayers = new List<PlayerData>();

        // Events
        public event Action<GameState> OnGameStateChanged;
        public event Action<int> OnRoundChanged;
        public event Action<PlayerData> OnPlayerDied;

        private float stateTimer;

        #region Unity Lifecycle

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            Debug.Log($"[GameManager] Initialized - Current State: {currentState}");
        }

        private void Update()
        {
            UpdateGameState();
        }

        #endregion

        #region Game State Management

        private void UpdateGameState()
        {
            if (currentState == GameState.Lobby) return;

            stateTimer -= Time.deltaTime;

            if (stateTimer <= 0f)
            {
                AdvanceGameState();
            }
        }

        public void ChangeGameState(GameState newState)
        {
            if (currentState == newState) return;

            ExitState(currentState);
            currentState = newState;
            EnterState(newState);

            OnGameStateChanged?.Invoke(newState);
            Debug.Log($"Game State Changed: {newState}");
        }

        private void EnterState(GameState state)
        {
            switch (state)
            {
                case GameState.Lobby:
                    stateTimer = -1f; // Infinite wait
                    break;

                case GameState.RoleAssignment:
                    AssignRoles();
                    stateTimer = 5f; // Short pause to show roles
                    break;

                case GameState.Night:
                    currentRound++;
                    OnRoundChanged?.Invoke(currentRound);
                    stateTimer = nightDuration;
                    EnableNightActions();
                    break;

                case GameState.Day:
                    stateTimer = dayDuration;
                    ProcessNightResults();
                    EnableDayDiscussion();
                    break;

                case GameState.Voting:
                    stateTimer = votingDuration;
                    StartVotingPhase();
                    break;

                case GameState.Results:
                    stateTimer = 10f;
                    ProcessVotingResults();
                    CheckWinCondition();
                    break;

                case GameState.GameOver:
                    ShowGameResults();
                    break;
            }
        }

        private void ExitState(GameState state)
        {
            // Cleanup when leaving a state
            switch (state)
            {
                case GameState.Night:
                    DisableNightActions();
                    break;

                case GameState.Day:
                    DisableDayDiscussion();
                    break;

                case GameState.Voting:
                    EndVotingPhase();
                    break;
            }
        }

        private void AdvanceGameState()
        {
            switch (currentState)
            {
                case GameState.RoleAssignment:
                    ChangeGameState(GameState.Night);
                    break;

                case GameState.Night:
                    ChangeGameState(GameState.Day);
                    break;

                case GameState.Day:
                    ChangeGameState(GameState.Voting);
                    break;

                case GameState.Voting:
                    ChangeGameState(GameState.Results);
                    break;

                case GameState.Results:
                    if (CheckWinCondition())
                    {
                        ChangeGameState(GameState.GameOver);
                    }
                    else
                    {
                        ChangeGameState(GameState.Night);
                    }
                    break;
            }
        }

        #endregion

        #region Player Management

        public void RegisterPlayer(PlayerData player)
        {
            if (!allPlayers.Contains(player))
            {
                allPlayers.Add(player);
                alivePlayers.Add(player);
                Debug.Log($"Player registered: {player.PlayerName}");
            }
        }

        public void UnregisterPlayer(PlayerData player)
        {
            allPlayers.Remove(player);
            alivePlayers.Remove(player);
        }

        public void KillPlayer(PlayerData player)
        {
            if (alivePlayers.Contains(player))
            {
                alivePlayers.Remove(player);
                player.IsAlive = false;
                OnPlayerDied?.Invoke(player);
                Debug.Log($"Player {player.PlayerName} has died");
            }
        }

        #endregion

        #region Game Logic Placeholders (To be implemented)

        private void AssignRoles()
        {
            Debug.Log($"[GameManager] Assigning roles to {alivePlayers.Count} players...");

            // 1. Shuffle players
            var shuffled = new List<PlayerData>(alivePlayers);
            int n = shuffled.Count;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n + 1);
                var value = shuffled[k];
                shuffled[k] = shuffled[n];
                shuffled[n] = value;
            }

            // 2. Define Count
            // Example Rule: 1 Mafia for every 4 players, 1 Doctor, 1 Sheriff
            int mafiaCount = Mathf.Max(1, shuffled.Count / 4);
            int doctorCount = 1;
            int sheriffCount = 1;

            // 3. Assign
            int currentIndex = 0;

            // Assign Mafia
            for (int i = 0; i < mafiaCount; i++)
            {
                if (currentIndex < shuffled.Count)
                    shuffled[currentIndex++].Role = PlayerRole.Mafia;
            }

            // Assign Doctor
            for (int i = 0; i < doctorCount; i++)
            {
                if (currentIndex < shuffled.Count)
                    shuffled[currentIndex++].Role = PlayerRole.Doctor;
            }

            // Assign Sheriff
            for (int i = 0; i < sheriffCount; i++)
            {
                if (currentIndex < shuffled.Count)
                    shuffled[currentIndex++].Role = PlayerRole.Sheriff;
            }

            // Rest are Civilians (Default is Civilian, but let's be explicit)
            while (currentIndex < shuffled.Count)
            {
                shuffled[currentIndex++].Role = PlayerRole.Civilian;
            }

            // Log Results (Hidden in real game)
            foreach (var p in shuffled)
            {
                Debug.Log($"Player assigned: {p.PlayerName} -> {p.Role}");
            }
        }

        private void EnableNightActions()
        {
            // TODO: Enable Mafia chat, special abilities
            Debug.Log("Night phase started - Mafia can act");
        }

        private void DisableNightActions()
        {
            Debug.Log("Night phase ended");
        }

        private void ProcessNightResults()
        {
            // TODO: Process who died during night
            Debug.Log("Processing night results...");
        }

        private void EnableDayDiscussion()
        {
            // TODO: Enable public voice chat
            Debug.Log("Day discussion started - Everyone can talk");
        }

        private void DisableDayDiscussion()
        {
            Debug.Log("Day discussion ended");
        }

        private void StartVotingPhase()
        {
            // TODO: Open voting UI
            Debug.Log("Voting phase started");
        }

        private void EndVotingPhase()
        {
            Debug.Log("Voting phase ended");
        }

        private void ProcessVotingResults()
        {
            // TODO: Execute the voted player
            Debug.Log("Processing voting results...");
        }

        private bool CheckWinCondition()
        {
            // TODO: Check if Mafia or Town won
            int mafiaCount = 0;
            int townCount = 0;

            foreach (var player in alivePlayers)
            {
                if (player.Role == PlayerRole.Mafia)
                    mafiaCount++;
                else
                    townCount++;
            }

            // Mafia wins if they equal or outnumber town
            if (mafiaCount >= townCount)
            {
                Debug.Log("Mafia Wins!");
                return true;
            }

            // Town wins if all mafia are dead
            if (mafiaCount == 0)
            {
                Debug.Log("Town Wins!");
                return true;
            }

            return false;
        }

        private void ShowGameResults()
        {
            Debug.Log("Game Over - Showing results");
            // TODO: Show win screen
        }

        #endregion

        #region Public API

        public void StartGame()
        {
            if (currentState == GameState.Lobby && allPlayers.Count >= 4)
            {
                ChangeGameState(GameState.RoleAssignment);
            }
            else
            {
                Debug.LogWarning("Cannot start game - not enough players");
            }
        }

        public GameState GetCurrentState() => currentState;
        public int GetCurrentRound() => currentRound;
        public float GetStateTimer() => stateTimer;
        public List<PlayerData> GetAlivePlayers() => new List<PlayerData>(alivePlayers);
        public List<PlayerData> GetAllPlayers() => new List<PlayerData>(allPlayers);

        #endregion
    }

    #region Enums and Data Classes

    public enum GameState
    {
        Lobby,
        RoleAssignment,
        Night,
        Day,
        Voting,
        Results,
        GameOver
    }

    public enum PlayerRole
    {
        Civilian,
        Mafia,
        Doctor,
        Sheriff
    }

    [System.Serializable]
    public class PlayerData
    {
        public string PlayerName { get; set; }
        public string PlayFabId { get; set; }
        public string EOSAccountId { get; set; }
        public PlayerRole Role { get; set; }
        public bool IsAlive { get; set; } = true;
        public bool IsHost { get; set; }

        public PlayerPublicData VisualData { get; set; }

        public PlayerData(string name, string playFabId, string eosId, PlayerPublicData visualData = null)
        {
            PlayerName = name;
            PlayFabId = playFabId;
            EOSAccountId = eosId;
            VisualData = visualData;
        }
    }

    #endregion
}
