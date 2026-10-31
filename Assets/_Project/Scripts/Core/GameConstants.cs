namespace MafiaNight.Core
{
    /// <summary>
    /// Centralized constants to avoid magic strings
    /// </summary>
    public static class GameConstants
    {
        #region Servers
        /// <summary>Our rewards server (XP, rank and coins for finished matches). Empty turns result reporting off.</summary>
        public const string REWARDS_API_URL = "https://api.mena-gaming.com";
        /// <summary>The server the game talks to (rewards and chat). In the editor and development builds the PlayerPrefs key "MafiaApiUrl" can point it to a test server.</summary>
        public static string ApiUrl
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            get { var o = UnityEngine.PlayerPrefs.GetString("MafiaApiUrl", ""); return string.IsNullOrEmpty(o) ? REWARDS_API_URL : o; }
#else
            get { return REWARDS_API_URL; }
#endif
        }
        #endregion

        /// <summary>The only address players see inside the game (the owner's other mailbox is for the developer consoles).</summary>
        public const string SUPPORT_EMAIL = "supportmafia@mena-gaming.com";

        /// <summary>The Google "web" OAuth client of the game (a public identifier, not a secret): the Android sign-in asks Google for an ID token meant for it, and the server accepts only tokens meant for it.</summary>
        public const string GOOGLE_WEB_CLIENT_ID = "1007236526273-19121cletqstpgahntdot10rvhmv5n48.apps.googleusercontent.com";

        /// <summary>The game's Facebook app (developers.facebook.com, under mafia@). Both are public; the app SECRET lives only on the server.
        /// They are also written into Assets/Plugins/Android/AndroidManifest.xml (meta-data and the fb&lt;id&gt; scheme): change both places together.</summary>
        public const string FACEBOOK_APP_ID = "2057586301904403";
        public const string FACEBOOK_CLIENT_TOKEN = "";         // App settings -> Advanced -> Client token
        public static bool FacebookReady => !string.IsNullOrEmpty(FACEBOOK_CLIENT_TOKEN);

        #region PlayerPrefs Keys
        public const string PREF_PLAYER_CUSTOM_NAME = "PlayerCustomName";
        /// <summary>The language the player chose in the settings: "ar" or "en". Not set = Arabic.</summary>
        public const string PREF_LANGUAGE = "mafia.lang";
        #endregion

        #region Scene Names (must match Build Settings)
        public const string SCENE_BOOTSTRAP = "Bootstrap";
        public const string SCENE_MAIN_MENU = "MainMenu";
        public const string SCENE_LOBBY = "Lobby";
        public const string SCENE_GAMEPLAY = "Gameplay";
        public const string SCENE_RESULTS = "Results";
        /// <summary>First screen after the Bootstrap (the rooms list of the new UI kit). Set back to SCENE_MAIN_MENU to use the old menu.</summary>
        public const string SCENE_HOME = "UIKitRooms";
        /// <summary>The table screens of a running match.</summary>
        public const string SCENE_MATCH = "UIKitMatch";
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
