using System;
using UnityEngine;

namespace MafiaNight.Data.Settings
{
    /// <summary>
    /// The player's voice settings on this device (PlayerPrefs). 50 on a volume dial is the normal level (EOS: 50 = unchanged,
    /// 0 = silent, 100 = twice as loud).
    /// </summary>
    public static class VoicePrefs
    {
        private const string KMic = "voice.mic", KSpeaker = "voice.speaker", KSens = "voice.sens", KNoise = "voice.noise",
                             KPtt = "voice.ptt", KMuteJoin = "voice.mutejoin", KNarrator = "voice.narrator";

        /// <summary>Raised after any value was saved.</summary>
        public static event Action Changed;

        public static int Mic { get => Get(KMic, 50); set => Set(KMic, Mathf.Clamp(value, 0, 100)); }
        public static int Speaker { get => Get(KSpeaker, 50); set => Set(KSpeaker, Mathf.Clamp(value, 0, 100)); }
        /// <summary>0 = high, 1 = medium, 2 = low: how loud you must be before your seat shows "talking".</summary>
        public static int Sensitivity { get => Get(KSens, 1); set => Set(KSens, Mathf.Clamp(value, 0, 2)); }
        public static bool NoiseIsolation { get => Get(KNoise, 1) == 1; set => Set(KNoise, value ? 1 : 0); }
        public static bool PushToTalk { get => Get(KPtt, 0) == 1; set => Set(KPtt, value ? 1 : 0); }
        public static bool MuteOnJoin { get => Get(KMuteJoin, 0) == 1; set => Set(KMuteJoin, value ? 1 : 0); }
        public static bool Narrator { get => Get(KNarrator, 1) == 1; set => Set(KNarrator, value ? 1 : 0); }

        /// <summary>Peak level (0..1) that counts as talking for the chosen sensitivity.</summary>
        public static float TalkThreshold => Sensitivity == 0 ? 0.012f : Sensitivity == 2 ? 0.04f : 0.02f;

        private static int Get(string key, int fallback)
        {
            try { return PlayerPrefs.GetInt(key, fallback); } catch { return fallback; }
        }

        private static void Set(string key, int value)
        {
            try { PlayerPrefs.SetInt(key, value); PlayerPrefs.Save(); } catch { }
            Changed?.Invoke();
        }
    }
}
