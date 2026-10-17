using MafiaNight.Data.Player;

namespace MafiaNight.Gameplay.Match
{
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

    /// <summary>
    /// A participant of the current match.
    /// </summary>
    [System.Serializable]
    public class PlayerData
    {
        public string PlayerName { get; set; }
        public string PlayFabId { get; set; }
        public string ProductUserId { get; set; }
        public PlayerRole Role { get; set; }
        public bool IsAlive { get; set; } = true;
        public bool IsHost { get; set; }

        public PlayerPublicData VisualData { get; set; }

        public PlayerData(string name, string playFabId, string productUserId, PlayerPublicData visualData = null)
        {
            PlayerName = name;
            PlayFabId = playFabId;
            ProductUserId = productUserId;
            VisualData = visualData;
        }
    }
}
