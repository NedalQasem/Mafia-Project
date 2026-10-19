using System;

namespace MafiaNight.Services.Voice
{
    public enum VoiceError
    {
        None,
        NotInRoom,
        NoPermission,   // the player refused the microphone
        NotReady,       // EOS is not logged in
        Failed,
    }

    /// <summary>
    /// Voice of the room the player is in (EOS RTC, free). Joining is automatic when a room is entered;
    /// the microphone, other players and the volumes can be controlled afterwards.
    /// </summary>
    public interface IVoiceService
    {
        bool IsConnected { get; }
        /// <summary>The microphone is sending (not muted by the player).</summary>
        bool MicOn { get; }

        /// <summary>Joined or left the voice channel of the room.</summary>
        event Action<bool> OnConnectionChanged;
        /// <summary>A player started or stopped talking: (puid, speaking).</summary>
        event Action<string, bool> OnSpeaking;
        /// <summary>Something about the mics or mutes changed (for icons).</summary>
        event Action OnChanged;

        /// <summary>Joins the voice channel of the current room. <paramref name="micOn"/>: start with the mic open.</summary>
        void Join(bool micOn, Action<VoiceError> done = null);
        void Leave();
        /// <summary>The newest connection statistics EOS sent (JSON text, empty until the first one arrives).</summary>
        string LastStatistics { get; }

        /// <summary>Call every frame from the main thread (own speaking indicator).</summary>
        void Tick();

        void SetMic(bool on);
        bool IsSpeaking(string puid);
        /// <summary>Remote player seems muted by himself (his mic is off) - shown on his seat.</summary>
        bool IsRemoteMicOff(string puid);

        /// <summary>Stops hearing a player (only on this device).</summary>
        void SetPlayerMuted(string puid, bool muted);
        bool IsPlayerMuted(string puid);
        /// <summary>0..1, 0.5 = normal.</summary>
        void SetPlayerVolume(string puid, float volume01);
        /// <summary>Applies the saved dial levels (VoicePrefs) to the live connection.</summary>
        void ApplyVolumes();
        /// <summary>Everything you hear, 0..1 (0.5 = normal).</summary>
        void SetOutputVolume(float volume01);
        /// <summary>Your microphone level, 0..1 (0.5 = normal).</summary>
        void SetInputVolume(float volume01);
    }
}
