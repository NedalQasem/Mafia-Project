using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Asks the phone for a steady 60 frames a second. Without a target, Android lets the display idle at 30 Hz (Samsung's adaptive refresh drops it when nothing is touched),
    /// and the game then ran at 27 fps and fell to 15 whenever a frame took a little longer than 33 ms.
    /// </summary>
    internal static class FrameRate
    {
        private const int Target = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply() { Application.targetFrameRate = Target; }
    }
}
