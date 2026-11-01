namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// True when the screens run on top of the real services (the app binder sets it). The demo wirings of the prototype
    /// scenes check it and leave mock data out, so real rooms never mix with fake ones.
    /// </summary>
    public static class KitLiveMode
    {
        public static bool Active;
    }
}
