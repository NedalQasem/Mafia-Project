using UnityEngine;
using MafiaNight.Gameplay.Player;

namespace MafiaNight.Gameplay.Seating
{
    /// <summary>
    /// Represents a static seat at the game table.
    /// Holds the visual representation of the player assigned to this spot.
    /// </summary>
    public class GameSeat : MonoBehaviour
    {
        [Header("Seat Configuration")]
        [SerializeField] private int seatIndex;
        [SerializeField] private Transform playerAnchor; // Where the visual prefab spawns
        
        [Header("State")]
        [SerializeField] private bool isOccupied;
        private PlayerController seatedPlayer;

        public int SeatIndex => seatIndex;
        public bool IsOccupied => isOccupied;

        /// <summary>
        /// Assigns a player to this seat and positions their visual
        /// </summary>
        public void AssignPlayer(PlayerController player)
        {
            if (isOccupied)
            {
                Debug.LogWarning($"Seat {seatIndex} is already occupied!");
                return;
            }

            seatedPlayer = player;
            isOccupied = true;
            
            // Snap player to this seat
            if (player != null)
            {
                player.transform.SetParent(playerAnchor != null ? playerAnchor : transform);
                player.transform.localPosition = Vector3.zero;
                player.transform.localRotation = Quaternion.identity;
                
                // Helper to orient UI toward camera if needed
                // player.LookAtCamera(); 
            }
            
            Debug.Log($"Player {player.GetPlayerName()} assigned to Seat {seatIndex}");
        }

        public void ClearSeat()
        {
            if (seatedPlayer != null)
            {
                // Optional: Destroy player object or return to pool
                Destroy(seatedPlayer.gameObject); 
                seatedPlayer = null;
            }
            isOccupied = false;
        }

        public PlayerController GetPlayer()
        {
            return seatedPlayer;
        }
    }
}
