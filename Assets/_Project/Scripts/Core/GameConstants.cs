namespace MafiaNight.Core
{
    /// <summary>
    /// Centralized constants to avoid magic strings
    /// </summary>
    public static class GameConstants
    {
        #region PlayerPrefs Keys
        public const string PREF_SAVED_EOS_CUSTOM_ID = "SavedEOSCustomID";
        public const string PREF_SELECTED_ROOM_TIER = "SelectedRoomTier";
        public const string PREF_PLAYER_CUSTOM_NAME = "PlayerCustomName";
        #endregion

        #region Scene Names
        public const string SCENE_LOBBY = "Lobby";
        public const string SCENE_MAIN_MENU = "MainMenu";
        #endregion

        #region UI Elements
        // Ideally avoid using these for GameObject.Find, use SerializeField instead
        public const string UI_HEADER_PANEL = "HeaderPanel";
        public const string UI_DOWN_BAR = "DownBar";
        #endregion
    }
}
