using System;
using System.Collections.Generic;
using MafiaNight.Core;
using UnityEngine;

namespace MafiaNight.Gameplay.Match
{
    /// <summary>
    /// Controls the match flow: Lobby → RoleAssignment → Night → Day → Voting → Results → (Night | GameOver).
    /// TODO(network): today this runs locally on every client. With EOS it must run only on the
    /// lobby owner (host), which replicates state/roles to the other players.
    /// </summary>
    public class GameManager : PersistentService<GameManager>
    {
        private const string Tag = "GameManager";

        [Header("Game State")]
        [SerializeField] private GameState currentState = GameState.Lobby;

        [Header("Round Configuration")]
        [SerializeField] private int currentRound = 0;
        [SerializeField] private float nightDuration = 60f;
        [SerializeField] private float dayDuration = 120f;
        [SerializeField] private float votingDuration = 45f;

        private readonly List<PlayerData> allPlayers = new List<PlayerData>();
        private readonly List<PlayerData> alivePlayers = new List<PlayerData>();
        private float stateTimer;

        public event Action<GameState> OnGameStateChanged;
        public event Action<int> OnRoundChanged;
        public event Action<PlayerData> OnPlayerDied;
        public event Action<PlayerData> OnPlayerRegistered;

        public GameState CurrentState => currentState;
        public int CurrentRound => currentRound;
        public float StateTimer => stateTimer;
        public IReadOnlyList<PlayerData> AllPlayers => allPlayers;
        public IReadOnlyList<PlayerData> AlivePlayers => alivePlayers;

        protected override void OnServiceAwake()
        {
            Log.Info($"Initialized - Current State: {currentState}", Tag);
        }

        private void Update()
        {
            if (currentState == GameState.Lobby || currentState == GameState.GameOver) return;

            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                AdvanceGameState();
            }
        }

        #region Public API

        public void StartGame()
        {
            if (currentState == GameState.Lobby && allPlayers.Count >= GameConstants.MIN_PLAYERS_TO_START)
            {
                ChangeGameState(GameState.RoleAssignment);
            }
            else
            {
                Log.Warning($"Cannot start game - state {currentState}, {allPlayers.Count} players", Tag);
            }
        }

        public void ChangeGameState(GameState newState)
        {
            if (currentState == newState) return;

            ExitState(currentState);
            currentState = newState;
            EnterState(newState);

            OnGameStateChanged?.Invoke(newState);
            Log.Info($"Game State Changed: {newState}", Tag);
        }

        public void RegisterPlayer(PlayerData player)
        {
            if (player == null || allPlayers.Contains(player)) return;

            allPlayers.Add(player);
            alivePlayers.Add(player);
            Log.Info($"Player registered: {player.PlayerName}", Tag);
            OnPlayerRegistered?.Invoke(player);
        }

        public void UnregisterPlayer(PlayerData player)
        {
            allPlayers.Remove(player);
            alivePlayers.Remove(player);
        }

        public void KillPlayer(PlayerData player)
        {
            if (!alivePlayers.Remove(player)) return;

            player.IsAlive = false;
            OnPlayerDied?.Invoke(player);
            Log.Info($"Player {player.PlayerName} has died", Tag);
        }

        /// <summary>Clears players and returns to the Lobby state (e.g. after leaving a match).</summary>
        public void ResetMatch()
        {
            allPlayers.Clear();
            alivePlayers.Clear();
            currentRound = 0;
            currentState = GameState.Lobby;
            stateTimer = 0f;
        }

        #endregion

        #region State Machine

        private void EnterState(GameState state)
        {
            switch (state)
            {
                case GameState.Lobby:
                    stateTimer = -1f;
                    break;

                case GameState.RoleAssignment:
                    AssignRoles();
                    stateTimer = 5f; // Short pause to show roles
                    break;

                case GameState.Night:
                    currentRound++;
                    OnRoundChanged?.Invoke(currentRound);
                    stateTimer = nightDuration;
                    break;

                case GameState.Day:
                    stateTimer = dayDuration;
                    ProcessNightResults();
                    break;

                case GameState.Voting:
                    stateTimer = votingDuration;
                    break;

                case GameState.Results:
                    stateTimer = 10f;
                    ProcessVotingResults();
                    break;

                case GameState.GameOver:
                    Log.Info("Game Over", Tag);
                    break;
            }
        }

        private void ExitState(GameState state)
        {
            // Reserved for per-phase cleanup (voice channels, action windows)
        }

        private void AdvanceGameState()
        {
            switch (currentState)
            {
                case GameState.RoleAssignment: ChangeGameState(GameState.Night); break;
                case GameState.Night: ChangeGameState(GameState.Day); break;
                case GameState.Day: ChangeGameState(GameState.Voting); break;
                case GameState.Voting: ChangeGameState(GameState.Results); break;
                case GameState.Results:
                    ChangeGameState(CheckWinCondition() ? GameState.GameOver : GameState.Night);
                    break;
            }
        }

        #endregion

        #region Game Rules

        private void AssignRoles()
        {
            Log.Info($"Assigning roles to {alivePlayers.Count} players...", Tag);

            // Fisher-Yates shuffle
            var shuffled = new List<PlayerData>(alivePlayers);
            for (int n = shuffled.Count - 1; n > 0; n--)
            {
                int k = UnityEngine.Random.Range(0, n + 1);
                (shuffled[k], shuffled[n]) = (shuffled[n], shuffled[k]);
            }

            // Rule: 1 Mafia per 4 players, 1 Doctor, 1 Sheriff, rest Civilians
            int mafiaCount = Mathf.Max(1, shuffled.Count / 4);
            int index = 0;

            for (int i = 0; i < mafiaCount && index < shuffled.Count; i++) shuffled[index++].Role = PlayerRole.Mafia;
            if (index < shuffled.Count) shuffled[index++].Role = PlayerRole.Doctor;
            if (index < shuffled.Count) shuffled[index++].Role = PlayerRole.Sheriff;
            while (index < shuffled.Count) shuffled[index++].Role = PlayerRole.Civilian;
        }

        private void ProcessNightResults()
        {
            // TODO: Resolve Mafia kill vs Doctor save, Sheriff investigation
        }

        private void ProcessVotingResults()
        {
            // TODO: Eliminate the most-voted player
        }

        private bool CheckWinCondition()
        {
            int mafiaCount = 0;
            int townCount = 0;

            foreach (var player in alivePlayers)
            {
                if (player.Role == PlayerRole.Mafia) mafiaCount++;
                else townCount++;
            }

            if (mafiaCount == 0)
            {
                Log.Info("Town Wins!", Tag);
                return true;
            }

            if (mafiaCount >= townCount)
            {
                Log.Info("Mafia Wins!", Tag);
                return true;
            }

            return false;
        }

        #endregion
    }
}
