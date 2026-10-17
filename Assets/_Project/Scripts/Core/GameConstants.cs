namespace MafiaNight.Core
{
    /// <summary>
    /// Centralized constants to avoid magic strings
    /// </summary>
    public static class GameConstants
    {
        #region PlayerPrefs Keys
        public const string PREF_PLAYER_CUSTOM_NAME = "PlayerCustomName";
        #endregion

        #region Scene Names (must match Build Settings)
        public const string SCENE_BOOTSTRAP = "Bootstrap";
        public const string SCENE_MAIN_MENU = "MainMenu";
        public const string SCENE_LOBBY = "Lobby";
        public const string SCENE_GAMEPLAY = "Gameplay";
        public const string SCENE_RESULTS = "Results";
        #endregion

        #region Editor
        /// <summary>SessionState key: scene to return to after Bootstrap when Play is pressed in another scene.</summary>
        public const string EDITOR_RETURN_SCENE_KEY = "MafiaNight.EditorReturnScene";
        #endregion

        #region Match Rules
        public const int MIN_PLAYERS_TO_START = 4;
        #endregion
    }
}
