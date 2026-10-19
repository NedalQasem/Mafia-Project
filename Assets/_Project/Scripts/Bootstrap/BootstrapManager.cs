using System;
using System.Collections;
using MafiaNight.Core;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Config;
using MafiaNight.Services.Economy;
using MafiaNight.Services.Profile;
using MafiaNight.Services.Scenes;
using MafiaNight.Services.Rooms;
using MafiaNight.Services.Session;
using MafiaNight.Services.Voice;
using MafiaNight.Services.Store;
using MafiaNight.Services.Telemetry;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.Bootstrap
{
    /// <summary>
    /// Composition root. Lives in the Bootstrap scene next to the persistent services and runs the startup pipeline:
    /// 1. EOS login → 2. PlayFab login → 3. Remote config → 4. Load profile → 5. Fetch wallet → 6. Load the first scene.
    /// Progress and errors are shown by an <see cref="IBootView"/> (the Kit loading screen); the old slider UI is only a fallback.
    /// </summary>
    public class BootstrapManager : MonoBehaviour
    {
        private const string Tag = "Bootstrap";

        [Header("Loading view")]
        [Tooltip("A component implementing IBootView (the Kit loading screen). Leave empty to use the legacy panel below.")]
        [SerializeField] private MonoBehaviour bootView;

        [Header("Legacy loading UI (fallback)")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private TextMeshProUGUI loadingText;
        [SerializeField] private Slider progressBar;

        [Header("Settings")]
        [SerializeField] private float minimumLoadingTime = 2f;
        [SerializeField] private float loginTimeout = 12f;
        [SerializeField] private float dataTimeout = 6f;
        [Tooltip("Wait for the player to tap the enter button after loading. Off = go straight to the first screen.")]
        [SerializeField] private bool waitForEnterTap = true;

        private IBootView view;
        private bool running;

        private void Start()
        {
            view = bootView as IBootView;

            // Plain (non-MonoBehaviour) services
            if (!ServiceLocator.IsRegistered<RoomSession>())
            {
                ServiceLocator.Register(new RoomSession());
            }
            if (!ServiceLocator.IsRegistered<IRoomService>())
            {
                ServiceLocator.Register<IRoomService>(new EosRoomService());
            }
            if (!ServiceLocator.IsRegistered<IVoiceService>())
            {
                ServiceLocator.Register<IVoiceService>(new EosVoiceService());
            }
            if (!ServiceLocator.IsRegistered<IStoreService>())
            {
                ServiceLocator.Register<IStoreService>(new StoreService());
            }
            if (!ServiceLocator.IsRegistered<IRemoteConfig>())
            {
                ServiceLocator.Register<IRemoteConfig>(new RemoteConfigService());
            }

            StartCoroutine(InitializeGame(false));
        }

        /// <summary>Restart the pipeline (the error screen's retry button calls this).</summary>
        public void RetryInitialization()
        {
            Log.Info("Retrying initialization...", Tag);
            StopAllCoroutines();
            running = false;
            StartCoroutine(InitializeGame(true));
        }

        private IEnumerator InitializeGame(bool isRetry)
        {
            if (running) yield break;
            running = true;
            if (loadingPanel != null && view == null) loadingPanel.SetActive(true);
            float startTime = Time.time;

            if (!ServiceLocator.TryGet<IAuthService>(out var auth) ||
                !ServiceLocator.TryGet<IPlayerProfileService>(out var profile) ||
                !ServiceLocator.TryGet<ICurrencyService>(out var currency) ||
                !ServiceLocator.TryGet<ISceneLoader>(out var sceneLoader))
            {
                Fail("Core services are missing from the Bootstrap scene", "تعذّر تشغيل اللعبة. أعد تثبيتها أو حدّثها.");
                yield break;
            }
            ServiceLocator.TryGet<IRemoteConfig>(out var remote);

            // ---------- 1-2. EOS + PlayFab login ----------
            string loginError = null;
            Action<string> onLoginFailed = error => loginError = error;
            auth.OnLoginFailed += onLoginFailed;
            if (isRetry && !auth.IsPlayFabLoggedIn) auth.RetryLogin();

            Progress(0.08f, "نتّصل بالحارة...");
            yield return WaitUntilOrTimeout(() => auth.IsEOSLoggedIn || loginError != null, loginTimeout);

            if (!auth.IsEOSLoggedIn)
            {
                auth.OnLoginFailed -= onLoginFailed;
                Fail(loginError ?? "Could not connect to Epic Services (timeout)", "تعذّر الاتصال بالخادم. تحقق من الإنترنت وحاول مجدداً.");
                yield break;
            }

            Progress(0.3f, "نسجّل دخولك...");
            yield return WaitUntilOrTimeout(() => auth.IsPlayFabLoggedIn || loginError != null, loginTimeout);
            auth.OnLoginFailed -= onLoginFailed;

            if (!auth.IsPlayFabLoggedIn)
            {
                Fail(loginError ?? "Could not connect to PlayFab (timeout)", "تعذّر تسجيل الدخول. حاول مجدداً بعد قليل.");
                yield break;
            }

            // ---------- 3. Remote config (version gate, maintenance, kill switches) ----------
            if (remote != null)
            {
                Progress(0.45f, "نقرأ آخر الأخبار...");
                bool configDone = false;
                remote.Fetch(_ => configDone = true);
                yield return WaitUntilOrTimeout(() => configDone, dataTimeout);

                if (remote.Data.maintenance)
                {
                    string msg = string.IsNullOrWhiteSpace(remote.Data.maintenanceMessage) ? "اللعبة تحت الصيانة الآن. عُد بعد قليل." : remote.Data.maintenanceMessage;
                    Fail("Maintenance mode", msg);
                    yield break;
                }
                if (!remote.IsVersionSupported)
                {
                    Fail($"App {Application.version} is older than {remote.Data.minVersion}", "يوجد تحديث جديد للعبة. حدّثها من المتجر لتكمل.");
                    yield break;
                }
            }

            // ---------- 4. Player profile ----------
            Progress(0.62f, "نفتح ملفّك...");

            string profileError = null;
            Action<string> onProfileError = error => profileError = error;
            profile.OnLoadError += onProfileError;
            profile.LoadProfile(auth.PlayFabId);
            yield return WaitUntilOrTimeout(() => profile.HasProfile || profileError != null, dataTimeout);
            profile.OnLoadError -= onProfileError;

            if (profile.HasProfile)
            {
                var p = profile.LocalProfile;
                if (profile.IsNewProfile)
                {
                    Log.Info("🆕 New player - saving initial profile", Tag);
                    profile.SaveProfile();
                }
                Log.Info($"✅ Profile loaded: Level {p.Level}, Rank {p.Rank}", Tag);
            }
            else
            {
                Log.Warning($"⚠️ Profile not loaded ({profileError ?? "timeout"}) - continuing", Tag);
            }

            // ---------- 5. Wallet ----------
            Progress(0.82f, "نحسب رصيدك...");
            bool currencyDone = false;
            currency.FetchCurrencies(_ => currencyDone = true);
            yield return WaitUntilOrTimeout(() => currencyDone, dataTimeout);

            // ---------- 6. First scene ----------
            Progress(0.95f, "نجهّز الطاولة...");
            float loadTime = Time.time - startTime;
            if (loadTime < minimumLoadingTime)
            {
                yield return new WaitForSeconds(minimumLoadingTime - loadTime);
            }

            Log.Info($"PlayFab calls during startup: {PlayFabMeter.Summary()}", Tag);
            running = false;

            string firstScene = GetFirstSceneName(out bool returnedFromEditorScene);
            Action enter = () =>
            {
                Log.Info($"✅ Initialization complete! Loading {firstScene}...", Tag);
                sceneLoader.LoadScene(firstScene);
            };

            if (view != null && waitForEnterTap && !returnedFromEditorScene) view.ShowReady(enter);
            else
            {
                Progress(1f, "جاهز");
                yield return new WaitForSeconds(0.4f);
                enter();
            }
        }

        /// <summary>
        /// The home screen normally. In the Editor, returns to the scene that was open when Play was pressed
        /// (see Editor/PlayFromBootstrap).
        /// </summary>
        private static string GetFirstSceneName(out bool fromEditorScene)
        {
            fromEditorScene = false;
#if UNITY_EDITOR
            string returnScene = UnityEditor.SessionState.GetString(GameConstants.EDITOR_RETURN_SCENE_KEY, "");
            if (!string.IsNullOrEmpty(returnScene) && returnScene != GameConstants.SCENE_BOOTSTRAP)
            {
                fromEditorScene = true;
                return returnScene;
            }
#endif
            return GameConstants.SCENE_HOME;
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeout)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Logs the technical reason and shows the player a short Arabic message with a retry button.</summary>
        private void Fail(string technical, string playerMessage)
        {
            running = false;
            Log.Error(technical, Tag);
            if (view != null) view.ShowError(playerMessage, RetryInitialization);
            else UpdateLegacy($"Error: {technical}", 0f);
        }

        private void Progress(float progress, string status)
        {
            if (view != null) view.SetProgress(progress, status);
            else UpdateLegacy(status, progress);
        }

        private void UpdateLegacy(string message, float progress)
        {
            if (loadingText != null) loadingText.text = message;
            if (progressBar != null) progressBar.value = progress;
        }
    }
}
