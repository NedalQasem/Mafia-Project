using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LiveKit;
using MafiaNight.Core;
using MafiaNight.Services.Rooms;
using MafiaNight.Services.Voice;
using PlayFab;
using UnityEngine;
using UnityEngine.Networking;
using AudioEncoding = LiveKit.Proto.AudioEncoding;
using TrackPublishOptions = LiveKit.Proto.TrackPublishOptions;
using TrackSource = LiveKit.Proto.TrackSource;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace MafiaNight.Voice.LiveKit
{
    /// <summary>
    /// <see cref="IVoiceService"/> on a self-hosted LiveKit room (rooms where many listen and a few speak; Docs/mockups/office-room.html).
    /// The game asks the rewards service for a token (<c>/v1/voice/token</c>: "lobby:&lt;EOS lobby id&gt;" or "official:&lt;id&gt;"), which says whether this player may speak.
    /// A listener only subscribes. The microphone goes through libwebrtc's echo cancellation and noise suppression (the mix Unity plays is the echo reference).
    /// Players are told apart by their PlayFab id (the LiveKit identity); the game's events use the EOS id, so the room's member list translates.
    /// </summary>
    public sealed class LiveKitVoiceService : IVoiceService, INamedRoomVoice
    {
        private const string Tag = "LiveKit";

        private sealed class Runner : MonoBehaviour { }
        private sealed class Remote { public GameObject Go; public AudioSource Source; public AudioStream Stream; public bool HasMic; public float Volume = 0.5f; public bool Muted; }

        private Runner runner;
        private Room room;
        private MicrophoneSource mic;
        private LocalAudioTrack micTrack;
        private GameObject micObject;
        private readonly Dictionary<string, Remote> remotes = new Dictionary<string, Remote>();      // PlayFab id -> his audio
        private readonly HashSet<string> speaking = new HashSet<string>();                           // EOS ids
        private readonly HashSet<string> muted = new HashSet<string>();                              // EOS ids the player stopped hearing
        private readonly Dictionary<string, float> volumes = new Dictionary<string, float>();
        private bool joining, canPublish, gameMicAllowed = true, wantMic = true, upgrading;
        private HashSet<string> hearOnly;
        private float master = 0.5f;
        private string roomName = "";
        private int generation;                                                  // every Leave and every new join moves it on: a join that sees another number was cancelled
        private readonly List<Action<VoiceError>> waiting = new List<Action<VoiceError>>();
        private string status = "";

        public bool IsConnected { get; private set; }
        public bool MicOn { get; private set; }
        /// <summary>One line for testers (shown in development builds): where the voice is, whether this player may speak, how many he hears.</summary>
        public string LastStatistics => (IsConnected ? "LK " + roomName + (canPublish ? " speak" : " listen") + " · hear " + remotes.Count + (MicOn ? " · mic on" : " · mic off") : "LK " + (joining ? "connecting" : "off")) + (status.Length > 0 ? " · " + status : "");

        public event Action<bool> OnConnectionChanged;
        public event Action<string, bool> OnSpeaking;
        public event Action OnChanged;

        /// <summary>The player may speak in the room he is in (a listener's token does not allow it).</summary>
        public bool CanSpeak => canPublish;

        private static string Base => (GameConstants.ApiUrl ?? "").TrimEnd('/');
        private static string Ticket => PlayFabSettings.staticPlayer?.ClientSessionTicket;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() { VoiceBackends.LiveKit = () => new LiveKitVoiceService(); }

        private IRoomService Rooms => ServiceLocator.TryGet(out IRoomService r) ? r : null;

        // ------------------------------------------------------------------ ids

        private string PfOf(string puid) { var m = Rooms?.Members.FirstOrDefault(x => x.Puid == puid); return m != null ? m.PlayFabId : null; }
        private string PuidOf(string pf) { var m = Rooms?.Members.FirstOrDefault(x => x.PlayFabId == pf); return m != null ? m.Puid : null; }

        // ------------------------------------------------------------------ joining

        public void Join(bool micOn, Action<VoiceError> done = null)
        {
            var rooms = Rooms;
            if (rooms == null || !rooms.InRoom || rooms.CurrentRoom == null) { done?.Invoke(VoiceError.NotInRoom); return; }
            Begin("lobby:" + rooms.CurrentRoom.Id, micOn, done);
        }

        /// <summary>Joins a named LiveKit room directly (an official room: "official:1001").</summary>
        public void JoinNamed(string name, bool micOn, Action<VoiceError> done = null) => Begin(name, micOn, done);

        private void Begin(string name, bool micOn, Action<VoiceError> done)
        {
            if (name == roomName && IsConnected) { done?.Invoke(VoiceError.None); return; }
            if (name == roomName && joining) { if (done != null) waiting.Add(done); return; }      // the same join is on its way: its answer is this one's answer
            if (IsConnected || joining) Leave();                                                   // the voice of another room is still up: leave it first
            roomName = name; wantMic = micOn;
            if (done != null) waiting.Add(done);
            EnsureRunner();
            runner.StartCoroutine(JoinRoutine(++generation));
        }

        private void Finish(VoiceError err)
        {
            var list = waiting.ToArray(); waiting.Clear();
            foreach (var d in list) d(err);
        }

        private void EnsureRunner()
        {
            if (runner != null) return;
            var go = new GameObject("LiveKitVoice");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }

        [Serializable] private class TokenDto { public bool ok; public string error, url, token, room; public bool canPublish; }

        private IEnumerator FetchToken(Action<TokenDto> got)
        {
            TokenDto dto = null;
            using (var req = new UnityWebRequest(Base + "/v1/voice/token", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes("{\"room\":\"" + roomName + "\"}"));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Authorization", Ticket);
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) { try { dto = JsonUtility.FromJson<TokenDto>(req.downloadHandler.text); } catch { /* not json */ } }
                else Log.Warning("Token request failed: " + req.responseCode + " " + req.error, Tag);
            }
            got(dto);
        }

        /// <summary>
        /// A listener was put on a mic seat: his token did not allow speaking. The service learns the seats from the room bot every few seconds, so ask again a few times,
        /// and join the room again with the new token as soon as it allows him.
        /// </summary>
        private IEnumerator Upgrade()
        {
            upgrading = true;
            for (int i = 0; i < 6 && wantMic && IsConnected && !canPublish; i++)
            {
                yield return new WaitForSecondsRealtime(i == 0 ? 1.5f : 2.5f);
                TokenDto dto = null;
                yield return FetchToken(d => dto = d);
                if (dto == null || !dto.ok || !dto.canPublish) continue;
                string name = roomName;
                Leave();
                yield return null;
                JoinNamed(name, true);
                break;
            }
            upgrading = false;
        }

        private IEnumerator JoinRoutine(int gen)
        {
            joining = true; status = "";
            if (string.IsNullOrEmpty(Ticket) || string.IsNullOrEmpty(Base)) { joining = false; status = "no login"; Finish(VoiceError.NotReady); yield break; }

            TokenDto dto = null;
            yield return FetchToken(d => dto = d);
            if (gen != generation) yield break;
            if (dto == null || !dto.ok || string.IsNullOrEmpty(dto.token)) { joining = false; status = "token refused"; Touch(); Finish(VoiceError.Failed); yield break; }
            canPublish = dto.canPublish;

            var mine = new Room();
            room = mine;
            mine.TrackSubscribed += OnTrackSubscribed;
            mine.TrackUnsubscribed += OnTrackUnsubscribed;
            mine.TrackMuted += (p, part) => Touch();
            mine.TrackUnmuted += (p, part) => Touch();
            mine.ParticipantDisconnected += p => { Drop(p.Identity); };
            mine.ActiveSpeakersChanged += OnSpeakers;
            mine.Reconnecting += _ => { if (room == mine) { status = "reconnecting"; Log.Warning("LiveKit is reconnecting", Tag); Touch(); } };
            mine.Reconnected += _ => { if (room == mine) { status = "reconnected"; Log.Info("LiveKit reconnected", Tag); Touch(); } };
            mine.DisconnectedWithReason += (_, reason) => { if (room == mine) { status = "lost: " + reason; Log.Warning("LiveKit disconnected: " + reason, Tag); } };
            mine.Disconnected += _ => { if (room == mine && IsConnected) { IsConnected = false; OnConnectionChanged?.Invoke(false); Touch(); } };
            var connect = mine.Connect(dto.url, dto.token, new RoomOptions());
            yield return connect;
            if (gen != generation) { try { mine.Disconnect(); } catch { /* it was cancelled */ } yield break; }
            if (connect.IsError) { Log.Warning("LiveKit connect failed", Tag); joining = false; status = "connect failed"; CleanUp(); Finish(VoiceError.Failed); yield break; }

            IsConnected = true;
            joining = false;
            OnConnectionChanged?.Invoke(true);
            Log.Info("Voice room " + roomName + " joined (" + (canPublish ? "may speak" : "listening") + ")", Tag);
            VoiceError err = VoiceError.None;
            if (wantMic && canPublish) yield return PublishMic(e => err = e);
            Finish(err);
        }

        // ------------------------------------------------------------------ the microphone

        private IEnumerator PublishMic(Action<VoiceError> done)
        {
            if (room == null || !IsConnected) { done?.Invoke(VoiceError.NotInRoom); yield break; }
            if (micTrack != null) { mic?.SetMute(false); MicOn = true; Touch(); done?.Invoke(VoiceError.None); yield break; }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Permission.RequestUserPermission(Permission.Microphone);
                float waited = 0f;
                while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && waited < 20f) { waited += Time.unscaledDeltaTime; yield return null; }
                if (!Permission.HasUserAuthorizedPermission(Permission.Microphone)) { done?.Invoke(VoiceError.NoPermission); yield break; }
            }
#else
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone)) yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone)) { done?.Invoke(VoiceError.NoPermission); yield break; }
#endif
            if (Microphone.devices.Length == 0) { done?.Invoke(VoiceError.Failed); yield break; }

            micObject = new GameObject("LiveKitMic");
            UnityEngine.Object.DontDestroyOnLoad(micObject);
            mic = new MicrophoneSource(Microphone.devices[0], micObject, AudioProcessingOptions.Default);
            micTrack = LocalAudioTrack.CreateAudioTrack("mic", mic, room);
            var options = new TrackPublishOptions { Source = TrackSource.SourceMicrophone, AudioEncoding = new AudioEncoding { MaxBitrate = 32000 } };
            var publish = room.LocalParticipant.PublishTrack(micTrack, options);
            yield return publish;
            if (publish.IsError) { Log.Warning("Publishing the microphone failed", Tag); StopMic(); done?.Invoke(VoiceError.Failed); yield break; }
            mic.Start();
            MicOn = gameMicAllowed;
            if (!gameMicAllowed) mic.SetMute(true);
            Touch();
            done?.Invoke(VoiceError.None);
        }

        private void StopMic()
        {
            try { if (mic != null) { mic.Stop(); mic.Dispose(); } } catch (Exception e) { Log.Warning("Stopping the microphone: " + e.Message, Tag); }
            mic = null; micTrack = null;
            if (micObject != null) UnityEngine.Object.Destroy(micObject);
            micObject = null; MicOn = false;
        }

        public void SetMic(bool on)
        {
            wantMic = on;
            if (!IsConnected) return;
            if (on && !gameMicAllowed) return;
            if (on && !canPublish && roomName.StartsWith("official:") && runner != null) { if (!upgrading) runner.StartCoroutine(Upgrade()); return; }
            if (on && micTrack == null && runner != null) { runner.StartCoroutine(PublishMic(null)); return; }
            if (mic != null) { mic.SetMute(!on); MicOn = on; Touch(); }
        }

        public void SetGameVoice(bool micAllowed, IReadOnlyCollection<string> hearOnlyIds)
        {
            gameMicAllowed = micAllowed;
            hearOnly = hearOnlyIds == null ? null : new HashSet<string>(hearOnlyIds);
            if (mic != null) { bool open = micAllowed && wantMic; mic.SetMute(!open); MicOn = open; }
            ApplyVolumes();
            Touch();
        }

        public void ClearGameVoice() => SetGameVoice(true, null);

        // ------------------------------------------------------------------ others

        private void OnTrackSubscribed(IRemoteTrack track, RemoteTrackPublication pub, RemoteParticipant who)
        {
            if (!(track is RemoteAudioTrack audio)) return;
            Drop(who.Identity);
            var go = new GameObject("lk-" + who.Identity);
            UnityEngine.Object.DontDestroyOnLoad(go);
            var src = go.AddComponent<AudioSource>();
            var r = new Remote { Go = go, Source = src, Stream = new AudioStream(audio, src), HasMic = true };
            remotes[who.Identity] = r;
            ApplyVolume(who.Identity, r);
            Touch();
        }

        private void OnTrackUnsubscribed(IRemoteTrack track, RemoteTrackPublication pub, RemoteParticipant who) { if (track is RemoteAudioTrack) Drop(who.Identity); }

        private void Drop(string pf)
        {
            if (!remotes.TryGetValue(pf, out var r)) return;
            try { r.Stream?.Dispose(); } catch { /* already gone */ }
            if (r.Go != null) UnityEngine.Object.Destroy(r.Go);
            remotes.Remove(pf);
            string puid = PuidOf(pf);
            if (puid != null && speaking.Remove(puid)) OnSpeaking?.Invoke(puid, false);
            Touch();
        }

        private void OnSpeakers(List<Participant> list)
        {
            var now = new HashSet<string>();
            foreach (var p in list) { string puid = PuidOf(p.Identity); if (puid != null) now.Add(puid); }
            foreach (var id in speaking.Where(x => !now.Contains(x)).ToList()) { speaking.Remove(id); OnSpeaking?.Invoke(id, false); }
            foreach (var id in now) if (speaking.Add(id)) OnSpeaking?.Invoke(id, true);
        }

        public bool IsSpeaking(string puid) => speaking.Contains(puid);
        public bool IsRemoteMicOff(string puid) { string pf = PfOf(puid); return pf == null || !remotes.TryGetValue(pf, out var r) || !r.HasMic; }

        public void SetPlayerMuted(string puid, bool on) { if (on) muted.Add(puid); else muted.Remove(puid); ApplyVolumes(); Touch(); }
        public bool IsPlayerMuted(string puid) => muted.Contains(puid);
        public void SetPlayerVolume(string puid, float v) { volumes[puid] = Mathf.Clamp01(v); ApplyVolumes(); }
        public void SetOutputVolume(float v) { master = Mathf.Clamp01(v); ApplyVolumes(); }
        public void SetInputVolume(float v) { /* the capture level of the phone is the system's; the SDK has no gain control here */ }

        public void ApplyVolumes() { foreach (var kv in remotes) ApplyVolume(kv.Key, kv.Value); }

        private void ApplyVolume(string pf, Remote r)
        {
            string puid = PuidOf(pf);
            bool silent = (puid != null && muted.Contains(puid)) || (hearOnly != null && (puid == null || !hearOnly.Contains(puid)));
            float own = puid != null && volumes.TryGetValue(puid, out var v) ? v : 0.5f;
            if (r.Source != null) r.Source.volume = silent ? 0f : Mathf.Clamp01(master * own * 4f);
        }

        // ------------------------------------------------------------------ leaving

        public void Tick() { }

        public void Leave()
        {
            joining = false; generation++; status = "";
            Finish(VoiceError.NotInRoom);
            StopMic();
            CleanUp();
            if (IsConnected) { IsConnected = false; OnConnectionChanged?.Invoke(false); }
            Touch();
        }

        private void CleanUp()
        {
            foreach (var id in remotes.Keys.ToList()) Drop(id);
            speaking.Clear();
            try { room?.Disconnect(); } catch (Exception e) { Log.Warning("Disconnect: " + e.Message, Tag); }
            room = null;
        }

        private void Touch() => OnChanged?.Invoke();
    }
}
