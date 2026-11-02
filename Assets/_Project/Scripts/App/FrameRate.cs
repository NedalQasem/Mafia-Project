using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Asks the phone for the fastest refresh rate its screen supports (120 Hz on a Galaxy S24 Ultra, 60 on most others; never more than 120 to spare the battery).
    /// Without any request Android lets the display idle at 30 Hz (Samsung's adaptive refresh drops it when nothing is touched), and the game then ran at 27 fps and fell to 15
    /// whenever a frame took a little longer than 33 ms. A high target only holds the display up: a frame that is slow to make is still slow (the journal's cpu / gpu times say why).
    /// </summary>
    internal static class FrameRate
    {
        private const int Cap = 120, Fallback = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            int best = 0;
            try { foreach (var r in Screen.resolutions) { int hz = Mathf.RoundToInt((float)r.refreshRateRatio.value); if (hz > best) best = hz; } } catch { /* the list is only a hint */ }
            Application.targetFrameRate = best >= Fallback ? Mathf.Min(best, Cap) : Fallback;
        }
    }
}
