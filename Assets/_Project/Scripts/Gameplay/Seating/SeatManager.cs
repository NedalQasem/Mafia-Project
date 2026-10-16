using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.Core;
using MafiaNight.Gameplay.Match;
using MafiaNight.Gameplay.Player;
using MafiaNight.Services.Auth;

namespace MafiaNight.Gameplay.Seating
{
    /// <summary>
    /// Manages all seats in the game scene.
    /// Handles assigning players to seats when the game starts.
    /// </summary>
    public class SeatManager : MonoBehaviour
    {
        private const string Tag = "SeatManager";

        [Header("Configuration")]
        [SerializeField] private List<GameSeat> seats;
        [SerializeField] private GameObject playerPrefab; // The Logic+Visual prefab

        private void Awake()
        {
            // Scene-scoped service (only exists in the Gameplay scene)
            ServiceLocator.Register(this);

            if (playerScale == Vector3.zero)
            {
                playerScale = new Vector3(0.15f, 0.15f, 0.15f);
            }

            // Auto-find seats if not assigned
            if (seats == null || seats.Count == 0)
            {
                seats = FindObjectsByType<GameSeat>().OrderBy(s => s.SeatIndex).ToList();
            }
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister(this);
        }

        private void Start()
        {
            if (!ServiceLocator.TryGet<GameManager>(out var gameManager)) return;

            var players = gameManager.AllPlayers;
            if (players.Count > 0)
            {
                Log.Info($"Found {players.Count} players in GameManager. Setting up seats...", Tag);
                SetupSeats(new List<PlayerData>(players));
            }
            else
            {
                Log.Warning("No players found in GameManager!", Tag);
            }
        }

        [Header("Dynamic Layout Settings")]
        [SerializeField] private Transform tableCenter;
        
        [System.Serializable]
        public class LayoutConfig
        {
            public string layoutName;
            public int targetPlayerCount; // e.g., 6, 10, 15
            public Transform layoutContainer; // Parent object containing empty Transforms as spawn points
        }

        [Header("Fixed Layouts (User    #2)")]
        [SerializeField] private List<LayoutConfig> layouts;
        
        [SerializeField] private Vector3 playerScale = new Vector3(0.12f, 0.12f, 0.12f);
        [SerializeField] private GameObject seatContainerPrefab;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;

        /// <summary>
        /// Sets up seats using PRE-DEFINED Spawn Points in the scene.
        /// </summary>
        public void SetupSeats(List<PlayerData> players)
        {
            ClearExistingSeats();

            int playerCount = players.Count;
            if (playerCount == 0) return;

            // 1. Find the best fitting layout
            LayoutConfig bestLayout = GetBestLayout(playerCount);

            if (bestLayout == null || bestLayout.layoutContainer == null)
            {
                Debug.LogError($"[SeatManager] No suitable layout found for {playerCount} players! Check Inspector.");
                return;
            }

            Debug.Log($"[SeatManager] Using Layout: {bestLayout.layoutName} for {playerCount} players.");
            
            // 2. Ensure the container is active
            bestLayout.layoutContainer.gameObject.SetActive(true);

            // 3. Get all spawn points (children of the container)
            List<Transform> spawnPoints = new List<Transform>();
            foreach (Transform child in bestLayout.layoutContainer)
            {
                if(child.gameObject.activeSelf) // Only use active points if desired, or all? Let's use all.
                    spawnPoints.Add(child);
            }

            if (spawnPoints.Count < playerCount)
            {
                Debug.LogWarning($"[SeatManager] Layout '{bestLayout.layoutName}' has only {spawnPoints.Count} points, but we have {playerCount} players!");
            }

            // 4. Place Players
            for (int i = 0; i < playerCount; i++)
            {
                // Safety check
                if (i >= spawnPoints.Count) break;

                Transform point = spawnPoints[i];
                
                // Spawn Seat at the fixed point position/rotation
                // PARENTING: We parent to the 'point' itself so it moves with the layout
                GameObject seatObj = Instantiate(seatContainerPrefab, point.position, point.rotation, point);
                seatObj.name = $"Seat_P{i}";

                // Component
                GameSeat seat = seatObj.GetComponent<GameSeat>();
                if (seat == null) seat = seatObj.AddComponent<GameSeat>();
                seats.Add(seat);

                // Spawn Player
                SpawnPlayerInSeat(players[i], seat);
            }
            
            // Hide the original layout container visuals if any (we just used its transforms)
            // Actually, we usually keep it active so the transforms exist, but maybe disable renderers if it has any?
            // For now, we assume they are empty Image/GameObjects.
        }

        private LayoutConfig GetBestLayout(int count)
        {
            if (layouts == null || layouts.Count == 0) return null;

            // Sort by count to find the smallest layout that fits, or the exact match
            // Strategy: Find exact match. If not, find the next largest.
            
            var sorted = layouts.OrderBy(x => x.targetPlayerCount).ToList();
            
            foreach (var layout in sorted)
            {
                if (layout.targetPlayerCount >= count)
                    return layout;
            }
            
            // If all are smaller, use the largest one
            return sorted.Last();
        }

        private void ClearExistingSeats()
        {
            if (seats != null) seats.Clear();
            else seats = new List<GameSeat>();

            // Destroy instantiated seats
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            
            // Note: We do NOT destroy the 'layouts' containers because they are scene references.
            // We might want to ensure only the active one is enabled? 
            // For safety, let's strictly use the 'transform' children for spawned seats.
        }

        private void SpawnPlayerInSeat(PlayerData data, GameSeat seat)
        {
            if (playerPrefab == null) return;

            GameObject playerObj = Instantiate(playerPrefab);
            playerObj.transform.localScale = playerScale;

            PlayerController controller = playerObj.GetComponent<PlayerController>();

            if (controller != null)
            {
                bool isLocal = ServiceLocator.TryGet<IAuthService>(out var auth) && data.PlayFabId == auth.PlayFabId;
                controller.Initialize(data.PlayerName, data.PlayFabId, data.EOSAccountId, isLocal, data.VisualData);
                seat.AssignPlayer(controller);
            }
        }
        
        public PlayerController GetLocalPlayerController()
        {
            foreach(var seat in seats)
            {
                if(seat.IsOccupied && seat.GetPlayer().IsLocal()) return seat.GetPlayer();
            }
            return null;
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || layouts == null) return;

            foreach (var layout in layouts)
            {
                if (layout.layoutContainer != null)
                {
                    Gizmos.color = Color.green;
                    foreach (Transform child in layout.layoutContainer)
                    {
                        Gizmos.DrawWireSphere(child.position, 0.3f);
                        // Draw facing direction
                        Gizmos.DrawLine(child.position, child.position + child.forward * 1.0f);
                    }
                }
            }
        }
    }
}
