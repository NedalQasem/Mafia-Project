using MafiaNight.Core;
using MafiaNight.Data.Settings;
using MafiaNight.Services.Voice;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Settings screen glue: shows the saved voice settings, saves every change, applies the dials to a live voice
    /// connection, and runs the microphone test with the real microphone. Added by <see cref="KitAppBinder"/>.
    /// </summary>
    public sealed class VoiceSettingsFlow : MonoBehaviour
    {
        private const string Tag = "VoiceSettings";

        private KitSettingsScreen screen;
        private IVoiceService audio;

        private AudioClip clip;
        private string device;
        private bool testing;
        private float[] window = new float[2048];
        private float lastLevel;

        private void Start()
        {
            screen = FindAnyObjectByType<KitSettingsScreen>();
            if (screen == null) { Destroy(this); return; }
            ServiceLocator.TryGet(out audio);

            screen.Load(VoicePrefs.Mic, VoicePrefs.Speaker, VoicePrefs.Sensitivity, VoicePrefs.NoiseIsolation, VoicePrefs.PushToTalk, VoicePrefs.MuteOnJoin, VoicePrefs.Narrator);
            screen.OnMicVolume += v => { VoicePrefs.Mic = v; audio?.ApplyVolumes(); };
            screen.OnSpeakerVolume += v => { VoicePrefs.Speaker = v; audio?.ApplyVolumes(); };
            screen.OnSensitivity += v => VoicePrefs.Sensitivity = v;
            screen.OnToggleChanged += (key, on) =>
            {
                if (key == "noise") VoicePrefs.NoiseIsolation = on;
                else if (key == "ptt") VoicePrefs.PushToTalk = on;
                else if (key == "mutejoin") VoicePrefs.MuteOnJoin = on;
                else if (key == "narrator") VoicePrefs.Narrator = on;
            };
            screen.OnMicTest += OnTest;
            screen.MicLevel = Level;
        }

        private void OnDestroy() { StopMic(); }

        private void OnTest(bool run)
        {
            if (!run) { StopMic(); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
            const string mic = "android.permission.RECORD_AUDIO";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(mic))
            {
                var cb = new UnityEngine.Android.PermissionCallbacks();
                cb.PermissionGranted += _ => StartMic();
                UnityEngine.Android.Permission.RequestUserPermission(mic, cb);
                return;
            }
#endif
            StartMic();
        }

        private void StartMic()
        {
            if (testing || Microphone.devices.Length == 0) { if (Microphone.devices.Length == 0) Log.Warning("No microphone found", Tag); return; }
            device = null;   // the default microphone
            clip = Microphone.Start(device, true, 1, 16000);
            testing = clip != null;
        }

        private void StopMic()
        {
            if (!testing) return;
            Microphone.End(device);
            testing = false; clip = null; lastLevel = 0f;
        }

        /// <summary>Loudest sample of the last ~0.13 s.</summary>
        private float Level()
        {
            if (!testing || clip == null) return 0f;
            int pos = Microphone.GetPosition(device);
            if (pos <= 0) return lastLevel;
            int n = Mathf.Min(window.Length, pos);
            clip.GetData(window, Mathf.Max(0, pos - n));
            float peak = 0f;
            for (int i = 0; i < n; i++) { float a = Mathf.Abs(window[i]); if (a > peak) peak = a; }
            lastLevel = peak;
            return peak;
        }
    }
}
