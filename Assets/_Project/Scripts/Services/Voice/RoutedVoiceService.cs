using System;
using System.Collections.Generic;
using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.Services.Rooms;
using UnityEngine;

namespace MafiaNight.Services.Voice
{
    /// <summary>
    /// The game's voice: picks the engine for the room the player is in. Match rooms stay on EOS (16 players, the night rules of the match);
    /// the other rooms (where many listen and a few speak) use LiveKit when it is in this build and the server answers, and fall back to EOS when it does not.
    /// The player can force EOS for every room with PlayerPrefs "mafia.voice" = "eos". All calls and events go to the engine in use.
    /// </summary>
    public sealed class RoutedVoiceService : IVoiceService
    {
        private const string Tag = "Voice";
        private readonly IVoiceService eos;
        private IVoiceService lk;
        private IVoiceService current;

        public RoutedVoiceService(IVoiceService eos)
        {
            this.eos = eos; current = eos;
            Forward(eos);
        }

        private void Forward(IVoiceService s)
        {
            s.OnConnectionChanged += c => { if (s == current) OnConnectionChanged?.Invoke(c); };
            s.OnSpeaking += (id, on) => { if (s == current) OnSpeaking?.Invoke(id, on); };
            s.OnChanged += () => { if (s == current) OnChanged?.Invoke(); };
        }

        /// <summary>True when the voice of the room in use is LiveKit.</summary>
        public bool UsesLiveKit => current == lk && lk != null;

        public bool IsConnected => current.IsConnected;
        public bool MicOn => current.MicOn;
        public string LastStatistics => current.LastStatistics;

        public event Action<bool> OnConnectionChanged;
        public event Action<string, bool> OnSpeaking;
        public event Action OnChanged;

        private static bool WantsLiveKit()
        {
            if (VoiceBackends.LiveKit == null || PlayerPrefs.GetString("mafia.voice", "livekit") == "eos") return false;
            if (!ServiceLocator.TryGet(out IRoomService rooms) || rooms.CurrentRoom == null) return false;
            return rooms.CurrentRoom.Mode != RoomMode.Mafia;
        }

        private static string OfficialRoomId()
        {
            if (!ServiceLocator.TryGet(out IRoomService rooms) || rooms.CurrentRoom == null) return null;
            return string.IsNullOrEmpty(rooms.CurrentRoom.OfficialId) ? null : rooms.CurrentRoom.OfficialId;
        }

        public void Join(bool micOn, Action<VoiceError> done = null)
        {
            if (current.IsConnected) { done?.Invoke(VoiceError.None); return; }
            string official = OfficialRoomId();
            if (official != null)
            {
                // an official room has no EOS voice (its lobby is too big for it): LiveKit or nothing
                if (VoiceBackends.LiveKit == null) { done?.Invoke(VoiceError.Failed); return; }
                if (lk == null) { lk = VoiceBackends.LiveKit(); Forward(lk); }
                current = lk;
                if (lk is INamedRoomVoice named) named.JoinNamed("official:" + official, micOn, done); else done?.Invoke(VoiceError.Failed);
                return;
            }
            if (!WantsLiveKit()) { current = eos; eos.Join(micOn, done); return; }
            if (lk == null) { lk = VoiceBackends.LiveKit(); Forward(lk); }
            current = lk;
            lk.Join(micOn, err =>
            {
                if (err == VoiceError.Failed || err == VoiceError.NotReady)
                {
                    Log.Warning("LiveKit voice failed (" + err + "): this room uses EOS voice", Tag);
                    current = eos;
                    eos.Join(micOn, done);
                    return;
                }
                done?.Invoke(err);
            });
        }

        public void Leave() { current.Leave(); if (current != eos) eos.Leave(); current = eos; }
        public void Tick() { current.Tick(); }
        public void SetMic(bool on) => current.SetMic(on);
        public void SetGameVoice(bool micAllowed, IReadOnlyCollection<string> hearOnly) => current.SetGameVoice(micAllowed, hearOnly);
        public void ClearGameVoice() => current.ClearGameVoice();
        public bool IsSpeaking(string puid) => current.IsSpeaking(puid);
        public bool IsRemoteMicOff(string puid) => current.IsRemoteMicOff(puid);
        public void SetPlayerMuted(string puid, bool muted) => current.SetPlayerMuted(puid, muted);
        public bool IsPlayerMuted(string puid) => current.IsPlayerMuted(puid);
        public void SetPlayerVolume(string puid, float volume01) => current.SetPlayerVolume(puid, volume01);
        public void ApplyVolumes() => current.ApplyVolumes();
        public void SetOutputVolume(float volume01) { eos.SetOutputVolume(volume01); lk?.SetOutputVolume(volume01); }
        public void SetInputVolume(float volume01) { eos.SetInputVolume(volume01); lk?.SetInputVolume(volume01); }
    }
}
