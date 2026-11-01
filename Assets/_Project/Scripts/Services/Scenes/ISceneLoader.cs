namespace MafiaNight.Services.Scenes
{
    /// <summary>
    /// Loads scenes with a transition. Use GameConstants.SCENE_* for names.
    /// </summary>
    public interface ISceneLoader
    {
        bool IsTransitioning { get; }
        void LoadScene(string sceneName);
    }
}
