using MafiaNight.Core;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Asks the phone for the frame rate the device tier allows (<see cref="DevicePerf.TargetFps"/>: the screen's best, up to 120, on a strong phone; 60 elsewhere; 30 with the battery saver).
    /// Without any request Android lets the display idle at 30 Hz (Samsung's adaptive refresh drops it when nothing is touched), and the game then ran at 27 fps and fell to 15
    /// whenever a frame took a little longer than 33 ms. A high target only holds the display up: a frame that is slow to make is still slow (the journal's cpu / gpu times say why).
    /// </summary>
    internal static class FrameRate
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            Application.targetFrameRate = DevicePerf.TargetFps;
            DevicePerf.Changed += () => Application.targetFrameRate = DevicePerf.TargetFps;       // the player changed the quality or the saver, or the governor lowered the tier
        }
    }
}
