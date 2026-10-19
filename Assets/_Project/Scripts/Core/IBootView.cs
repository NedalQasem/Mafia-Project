using System;

namespace MafiaNight.Core
{
    /// <summary>
    /// What the Bootstrap needs from a loading screen: real progress with a status line, an error state with a retry
    /// action, and a "ready" state. Keeps the startup code independent from the UI Kit assembly.
    /// </summary>
    public interface IBootView
    {
        /// <summary>progress01 in 0..1; status is a short Arabic line shown under the bar (null keeps the previous one).</summary>
        void SetProgress(float progress01, string status);

        /// <summary>Shows a blocking error with a retry button. <paramref name="retry"/> runs when the player taps it.</summary>
        void ShowError(string message, Action retry);

        /// <summary>Everything is loaded: show the enter button (or call <paramref name="enter"/> at once if the view has none).</summary>
        void ShowReady(Action enter);
    }
}
