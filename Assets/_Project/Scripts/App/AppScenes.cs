namespace MafiaNight.App
{
    /// <summary>Scene names of the new UI-kit screens, in nav-bar tab order (shop, family, rooms, ranks, settings).</summary>
    public static class AppScenes
    {
        public const string Store = "UIKitStore";
        public const string Chat = "UIKitChat";
        public const string Rooms = "UIKitRooms";
        public const string Leaderboards = "UIKitLeaderboards";
        public const string Settings = "UIKitSettings";
        public const string Loading = "UIKitLoading";
        public const string Match = MafiaNight.Core.GameConstants.SCENE_MATCH;

        public static readonly string[] ByTab = { Store, Chat, Rooms, Leaderboards, Settings };

        /// <summary>The screen shown after the Bootstrap finishes.</summary>
        public const string Home = MafiaNight.Core.GameConstants.SCENE_HOME;
    }
}
