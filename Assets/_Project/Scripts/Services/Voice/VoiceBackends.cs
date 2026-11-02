using System;

namespace MafiaNight.Services.Voice
{
    /// <summary>
    /// The voice engines the game can use besides EOS. The LiveKit one lives in its own assembly (only compiled when the LiveKit package is in the
    /// project) and puts its factory here when the game starts, so nothing in the game needs to know whether the package is there.
    /// </summary>
    public static class VoiceBackends
    {
        /// <summary>Makes the LiveKit voice (rooms where many listen and a few speak), or null when the package is not in this project.</summary>
        public static Func<IVoiceService> LiveKit;

        /// <summary>PlayerPrefs key of LiveKit's audio path: "unity" (Unity microphone, software echo cancellation) or "platform" (the phone's voice-call audio path).</summary>
        public const string LiveKitModeKey = "mafia.lkmode";
    }
}
