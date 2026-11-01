using System;

namespace MafiaNight.Services.Voice
{
    /// <summary>A voice engine that can join a room by name (an official room: "official:1001") instead of the room the player sits in.</summary>
    public interface INamedRoomVoice
    {
        void JoinNamed(string name, bool micOn, Action<VoiceError> done = null);
    }
}
