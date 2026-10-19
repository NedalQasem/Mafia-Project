using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.RTCAudio;
using MafiaNight.Core;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Rooms;
using UnityEngine;

namespace MafiaNight.Services.Voice
{
    /// <summary>
    /// <see cref="IVoiceService"/> on the RTC room that belongs to the EOS lobby (created with the room, joined manually here).
    /// Audio is handled entirely by EOS: no server of ours, no cost.
    /// </summary>
    public sealed class EosVoiceService : IVoiceService
    {
        private const string Tag = "Voice";

        private IEosPlatform eos;
        private IRoomService rooms;

        private string roomName;                 // the RTC room name EOS gave to the lobby
        private string lobbyId;
        private ulong notifParticipant, notifConnection, notifBeforeSend;
        private int loudFrames, quietFrames;
        private bool selfSpeaking;
        private bool joining;
        private readonly HashSet<string> speaking = new HashSet<string>();
        private readonly HashSet<string> remoteMicOff = new HashSet<string>();
        private readonly HashSet<string> muted = new HashSet<string>();

        /// <summary>Log every participant update (diagnostics).</summary>
        public static bool LogEvents;

        public bool IsConnected { get; private set; }
        public bool MicOn { get; private set; } = true;

        public event Action<bool> OnConnectionChanged;
        public event Action<string, bool> OnSpeaking;
        public event Action OnChanged;

        private LobbyInterface Lobby => eos?.Platform?.GetLobbyInterface();
        private RTCAudioInterface Audio => eos?.Platform?.GetRTCInterface()?.GetAudioInterface();
        private ProductUserId Me => eos?.LocalUserId;

        private bool Ready()
        {
            if (eos == null) ServiceLocator.TryGet(out eos);
            if (rooms == null)
            {
                ServiceLocator.TryGet(out rooms);
                // leaving or losing the room ends the voice with it
                if (rooms != null) rooms.OnRoomLeft += _ => Leave();
            }
            return eos != null && eos.IsLoggedIn && Me != null && Me.IsValid() && Lobby != null && Audio != null;
        }

        // ------------------------------------------------------------------------------------------ join / leave

        public void Join(bool micOn, Action<VoiceError> done = null)
        {
            if (!Ready()) { done?.Invoke(VoiceError.NotReady); return; }
            var room = rooms?.CurrentRoom;
            if (room == null) { done?.Invoke(VoiceError.NotInRoom); return; }
            if (IsConnected && lobbyId == room.Id) { done?.Invoke(VoiceError.None); return; }
            if (joining) { done?.Invoke(VoiceError.None); return; }
            joining = true;

            RequestMicPermission(granted =>
            {
                if (!granted) { joining = false; done?.Invoke(VoiceError.NoPermission); return; }
                if (rooms.CurrentRoom == null || rooms.CurrentRoom.Id != room.Id) { joining = false; done?.Invoke(VoiceError.NotInRoom); return; }
                DoJoin(room.Id, micOn, done);
            });
        }

        private void DoJoin(string id, bool micOn, Action<VoiceError> done)
        {
            // EOS may already have connected us to the room's RTC channel when the lobby was created / joined
            var co = new IsRTCRoomConnectedOptions { LobbyId = id, LocalUserId = Me };
            if (Lobby.IsRTCRoomConnected(ref co, out bool already) == Result.Success && already) { Finish(id, micOn, done); return; }

            var jo = new JoinRTCRoomOptions
            {
                LobbyId = id,
                LocalUserId = Me,
                LocalRTCOptions = new LocalRTCOptions { LocalAudioDeviceInputStartsMuted = !micOn },
            };
            Lobby.JoinRTCRoom(ref jo, null, (ref JoinRTCRoomCallbackInfo data) =>
            {
                if (data.ResultCode != Result.Success)
                {
                    joining = false;
                    Log.Warning("JoinRTCRoom failed: " + data.ResultCode, Tag);
                    done?.Invoke(VoiceError.Failed);
                    return;
                }
                Finish(id, micOn, done);
            });
        }

        private void Finish(string id, bool micOn, Action<VoiceError> done)
        {
            joining = false;
            var no = new GetRTCRoomNameOptions { LobbyId = id, LocalUserId = Me };
            if (Lobby.GetRTCRoomName(ref no, out Utf8String name) != Result.Success) { Log.Warning("GetRTCRoomName failed", Tag); done?.Invoke(VoiceError.Failed); return; }

            lobbyId = id; roomName = name;
            Subscribe();
            IsConnected = true;
            SetMic(micOn);            // makes the real sending state match what the UI shows
            Log.Info($"Voice connected (mic {(micOn ? "on" : "off")})", Tag);
            OnConnectionChanged?.Invoke(true);
            OnChanged?.Invoke();
            done?.Invoke(VoiceError.None);
        }

        public void Leave()
        {
            if (!IsConnected && !joining) return;
            string id = lobbyId;
            bool wasConnected = IsConnected;
            Unsubscribe();
            IsConnected = false; joining = false;
            speaking.Clear(); remoteMicOff.Clear(); muted.Clear();
            roomName = null; lobbyId = null;
            if (!string.IsNullOrEmpty(id) && Ready())
            {
                var lo = new LeaveRTCRoomOptions { LobbyId = id, LocalUserId = Me };
                Lobby.LeaveRTCRoom(ref lo, null, (ref LeaveRTCRoomCallbackInfo _) => { });
            }
            if (wasConnected) { OnConnectionChanged?.Invoke(false); OnChanged?.Invoke(); }
        }

        private void Subscribe()
        {
            Unsubscribe();
            var po = new AddNotifyParticipantUpdatedOptions { LocalUserId = Me, RoomName = roomName };
            notifParticipant = Audio.AddNotifyParticipantUpdated(ref po, null, (ref ParticipantUpdatedCallbackInfo d) => OnParticipant(d.ParticipantId, d.Speaking, d.AudioStatus));
            // EOS reports other players talking; for yourself it sends nothing, so the outgoing microphone frames are measured here
            var bo = new AddNotifyAudioBeforeSendOptions { LocalUserId = Me, RoomName = roomName };
            notifBeforeSend = Audio.AddNotifyAudioBeforeSend(ref bo, null, (ref AudioBeforeSendCallbackInfo d) => OnOutgoingFrame(d.Buffer));
            var co = new AddNotifyRTCRoomConnectionChangedOptions();
            notifConnection = Lobby.AddNotifyRTCRoomConnectionChanged(ref co, null, (ref RTCRoomConnectionChangedCallbackInfo d) =>
            {
                if (d.LobbyId != lobbyId || d.IsConnected) return;
                Log.Warning("Voice disconnected: " + d.DisconnectReason, Tag);
                Unsubscribe();
                IsConnected = false;
                speaking.Clear();
                OnConnectionChanged?.Invoke(false);
                OnChanged?.Invoke();
            });
        }

        private void Unsubscribe()
        {
            if (notifParticipant != 0 && Audio != null) Audio.RemoveNotifyParticipantUpdated(notifParticipant);
            if (notifConnection != 0 && Lobby != null) Lobby.RemoveNotifyRTCRoomConnectionChanged(notifConnection);
            if (notifBeforeSend != 0 && Audio != null) Audio.RemoveNotifyAudioBeforeSend(notifBeforeSend);
            notifParticipant = notifConnection = notifBeforeSend = 0;
            loudFrames = quietFrames = 0;
            if (selfSpeaking && Me != null) { selfSpeaking = false; OnSpeaking?.Invoke(Me.ToString(), false); }
        }

        private const float LoudPeak = 0.02f;   // quiet speech peaks around 0.02, silence stays below 0.002

        private void OnOutgoingFrame(AudioBuffer? buffer)
        {
            if (buffer == null || Me == null) return;
            var frames = buffer.Value.Frames;
            float peak = 0f;
            if (MicOn && frames != null)
                for (int i = 0; i < frames.Length; i += 4) { float a = Mathf.Abs(frames[i]) / 32768f; if (a > peak) peak = a; }
            if (peak >= LoudPeak) { loudFrames++; quietFrames = 0; } else { quietFrames++; loudFrames = 0; }
            bool next = selfSpeaking;
            if (!selfSpeaking && loudFrames >= 3) next = true;
            else if (selfSpeaking && quietFrames >= 25) next = false;   // 10 ms frames: about a quarter of a second of silence
            if (next == selfSpeaking) return;
            selfSpeaking = next;
            string id = Me.ToString();
            if (next) speaking.Add(id); else speaking.Remove(id);
            OnSpeaking?.Invoke(id, next);
        }

        private void OnParticipant(ProductUserId who, bool isSpeaking, RTCAudioStatus status)
        {
            if (who == null) return;
            string id = who.ToString();
            if (LogEvents) Log.Info($"participant {id.Substring(Mathf.Max(0, id.Length - 4))} speaking={isSpeaking} audio={status}", Tag);
            bool changed = false;
            bool off = status == RTCAudioStatus.Disabled || status == RTCAudioStatus.AdminDisabled;
            if (off ? remoteMicOff.Add(id) : remoteMicOff.Remove(id)) changed = true;
            bool was = speaking.Contains(id);
            if (isSpeaking && !was) { speaking.Add(id); OnSpeaking?.Invoke(id, true); }
            else if (!isSpeaking && was) { speaking.Remove(id); OnSpeaking?.Invoke(id, false); }
            if (changed) OnChanged?.Invoke();
        }

        // ------------------------------------------------------------------------------------------ controls

        public void SetMic(bool on)
        {
            MicOn = on;
            OnChanged?.Invoke();
            if (!IsConnected || !Ready()) return;
            var o = new UpdateSendingOptions { LocalUserId = Me, RoomName = roomName, AudioStatus = on ? RTCAudioStatus.Enabled : RTCAudioStatus.Disabled };
            Audio.UpdateSending(ref o, null, (ref UpdateSendingCallbackInfo d) => { if (d.ResultCode != Result.Success) Log.Warning("UpdateSending: " + d.ResultCode, Tag); });
        }

        public bool IsSpeaking(string puid) => puid != null && speaking.Contains(puid);
        public bool IsRemoteMicOff(string puid) => puid != null && remoteMicOff.Contains(puid);
        public bool IsPlayerMuted(string puid) => puid != null && muted.Contains(puid);

        public void SetPlayerMuted(string puid, bool value)
        {
            if (string.IsNullOrEmpty(puid)) return;
            if (value) muted.Add(puid); else muted.Remove(puid);
            OnChanged?.Invoke();
            if (!IsConnected || !Ready()) return;
            var o = new UpdateReceivingOptions { LocalUserId = Me, RoomName = roomName, ParticipantId = ProductUserId.FromString(puid), AudioEnabled = !value };
            Audio.UpdateReceiving(ref o, null, (ref UpdateReceivingCallbackInfo d) => { if (d.ResultCode != Result.Success) Log.Warning("UpdateReceiving: " + d.ResultCode, Tag); });
        }

        public void SetPlayerVolume(string puid, float volume01)
        {
            if (!IsConnected || !Ready() || string.IsNullOrEmpty(puid)) return;
            var o = new UpdateParticipantVolumeOptions { LocalUserId = Me, RoomName = roomName, ParticipantId = ProductUserId.FromString(puid), Volume = Mathf.Clamp01(volume01) * 100f };
            Audio.UpdateParticipantVolume(ref o, null, (ref UpdateParticipantVolumeCallbackInfo _) => { });
        }

        public void SetOutputVolume(float volume01)
        {
            if (!IsConnected || !Ready()) return;
            var o = new UpdateReceivingVolumeOptions { LocalUserId = Me, RoomName = roomName, Volume = Mathf.Clamp01(volume01) * 100f };
            Audio.UpdateReceivingVolume(ref o, null, (ref UpdateReceivingVolumeCallbackInfo _) => { });
        }

        public void SetInputVolume(float volume01)
        {
            if (!IsConnected || !Ready()) return;
            var o = new UpdateSendingVolumeOptions { LocalUserId = Me, RoomName = roomName, Volume = Mathf.Clamp01(volume01) * 100f };
            Audio.UpdateSendingVolume(ref o, null, (ref UpdateSendingVolumeCallbackInfo _) => { });
        }

        // ------------------------------------------------------------------------------------------ Android permission

        private static void RequestMicPermission(Action<bool> result)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const string mic = "android.permission.RECORD_AUDIO";
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(mic)) { result(true); return; }
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => result(true);
            callbacks.PermissionDenied += _ => result(false);
            callbacks.PermissionDeniedAndDontAskAgain += _ => result(false);
            UnityEngine.Android.Permission.RequestUserPermission(mic, callbacks);
#else
            result(true);
#endif
        }
    }
}
