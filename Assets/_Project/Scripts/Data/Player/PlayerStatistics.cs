using System;

namespace MafiaNight.Data.Player
{
    /// <summary>
    /// Player statistics tracking
    /// </summary>
    [System.Serializable]
    public class PlayerStatistics
    {
        // ==================== GAME STATS ====================
        public int GamesPlayed;
        public int GamesWon;
        public int GamesLost;
        
        // ==================== ROLE STATS ====================
        public int MafiaGames;
        public int MafiaWins;
        public int CitizenGames;
        public int CitizenWins;
        
        // ==================== SPECIAL STATS ====================
        /// <summary>Times eliminated as innocent</summary>
        public int FalseAccusations;
        
        /// <summary>Correct votes that eliminated mafia</summary>
        public int CorrectVotes;
        
        /// <summary>Messages sent in chat</summary>
        public int MessagesSent;
        
        /// <summary>Gifts sent to other players</summary>
        public int GiftsSent;
        
        /// <summary>Gifts received from other players</summary>
        public int GiftsReceived;
        
        // ==================== TIME TRACKING ====================
        public string LastLoginDate;
        public string AccountCreatedDate;
        public int TotalPlayTimeMinutes;
        
        // ==================== COMPUTED PROPERTIES ====================
        public float WinRate
        {
            get
            {
                if (GamesPlayed <= 0) return 0f;
                return (float)GamesWon / GamesPlayed;
            }
        }
        
        public float MafiaWinRate
        {
            get
            {
                if (MafiaGames <= 0) return 0f;
                return (float)MafiaWins / MafiaGames;
            }
        }
        
        public float CitizenWinRate
        {
            get
            {
                if (CitizenGames <= 0) return 0f;
                return (float)CitizenWins / CitizenGames;
            }
        }
        
        public string WinRatePercent => $"{WinRate * 100:F0}%";
        
        // ==================== CONSTRUCTOR ====================
        public PlayerStatistics()
        {
            GamesPlayed = 0;
            GamesWon = 0;
            GamesLost = 0;
            MafiaGames = 0;
            MafiaWins = 0;
            CitizenGames = 0;
            CitizenWins = 0;
            AccountCreatedDate = DateTime.UtcNow.ToString("o");
            LastLoginDate = DateTime.UtcNow.ToString("o");
        }
        
        // ==================== METHODS ====================
        public void RecordGame(bool won, bool wasMafia)
        {
            GamesPlayed++;
            
            if (wasMafia)
            {
                MafiaGames++;
                if (won) MafiaWins++;
            }
            else
            {
                CitizenGames++;
                if (won) CitizenWins++;
            }
            
            if (won)
            {
                GamesWon++;
            }
            else
            {
                GamesLost++;
            }
        }
        
        public void UpdateLastLogin()
        {
            LastLoginDate = DateTime.UtcNow.ToString("o");
        }
    }
}
