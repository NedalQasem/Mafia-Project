using System;
using System.Collections;
using MafiaNight.Core;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Economy;
using MafiaNight.Services.Profile;
using MafiaNight.Services.Scenes;
using MafiaNight.Services.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.Bootstrap
{
    /// <summary>
    /// Composition root. Lives in the Bootstrap scene next to the persistent services and runs the startup pipeline:
    /// 1. EOS login → 2. PlayFab login → 3. Load profile → 4. Fetch wallet → 5. Load the first scene.
    /// </summary>
    public class BootstrapManager : MonoBehaviour
    {
        private const string Tag = "Bootstrap";

        [Header("Loading UI")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private TextMeshProUGUI loadingText;
        [SerializeField] private Slider progressBar;

        [Header("Settings")]
        [SerializeField] private float minimumLoadingTime = 2f;
        [SerializeField] private float loginTimeout = 10f;
        [SerializeField] private float dataTimeout = 5f;

        private void Start()
        {
            // Plain (non-MonoBehaviour) services
            if (!ServiceLocator.IsRegistered<RoomSession>())
            {
                ServiceLocator.Register(new RoomSession());
            }

            StartCoroutine(InitializeGame());
        }

        /// <summary>Restart the pipeline (hook this to a "Retry" button on the error screen).</summary>
        public void RetryInitialization()
        {
            Log.Info("Retrying initialization...", Tag);
            StopAllCoroutines();
            StartCoroutine(InitializeGame());
        }

        private IEnumerator InitializeGame()
        {
            if (loadingPanel != null) loadingPanel.SetActive(true);
            float startTime = Time.time;

            if (!ServiceLocator.TryGet<IAuthService>(out var auth) ||
                !ServiceLocator.TryGet<IPlayerProfileService>(out var profile) ||
                !ServiceLocator.TryGet<ICurrencyService>(out var currency) ||
                !ServiceLocator.TryGet<ISceneLoader>(out var sceneLoader))
            {
                Fail("Core services are missing from the Bootstrap scene");
                yield break;
            }

            // ---------- 1-2. EOS + PlayFab login ----------
            string loginError = null;
            Action<string> onLoginFailed = error => loginError = error;
            auth.OnLoginFailed += onLoginFailed;

            UpdateLoadingUI("Logging in to Epic...", 0.2f);
            yield return WaitUntilOrTimeout(() => auth.IsEOSLoggedIn || loginError != null, loginTimeout);

            if (!auth.IsEOSLoggedIn)
            {
                auth.OnLoginFailed -= onLoginFailed;
                Fail(loginError ?? "Could not connect to Epic Services (timeout)");
                yield break;
            }

            UpdateLoadingUI("Connecting to PlayFab...", 0.4f);
            yield return WaitUntilOrTimeout(() => auth.IsPlayFabLoggedIn || loginError != null, loginTimeout);
            auth.OnLoginFailed -= onLoginFailed;

            if (!auth.IsPlayFabLoggedIn)
            {
                Fail(loginError ?? "Could not connect to PlayFab (timeout)");
                yield break;
            }

            // ---------- 3. Player profile ----------
            UpdateLoadingUI("Loading player data...", 0.6f);

            string profileError = null;
            Action<string> onProfileError = error => profileError = error;
            profile.OnLoadError += onProfileError;
            profile.LoadProfile(auth.PlayFabId);
            yield return WaitUntilOrTimeout(() => profile.HasProfile || profileError != null, dataTimeout);
            profile.OnLoadError -= onProfileError;

            if (profile.HasProfile)
            {
                var p = profile.LocalProfile;
                bool isNewPlayer = p.Statistics != null && p.Statistics.GamesPlayed == 0 && p.Level == 1 && p.CurrentXP == 0;
                if (isNewPlayer)
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

            // ---------- 4. Wallet ----------
            UpdateLoadingUI("Loading your wallet...", 0.8f);
            bool currencyDone = false;
            currency.FetchCurrencies(_ => currencyDone = true);
            yield return WaitUntilOrTimeout(() => currencyDone, dataTimeout);

            // ---------- 5. First scene ----------
            UpdateLoadingUI("Preparing Main Menu...", 0.9f);
            float loadTime = Time.time - startTime;
            if (loadTime < minimumLoadingTime)
            {
                yield return new WaitForSeconds(minimumLoadingTime - loadTime);
            }

            UpdateLoadingUI("Ready!", 1f);
            yield return new WaitForSeconds(0.5f);

            string firstScene = GetFirstSceneName();
            Log.Info($"✅ Initialization complete! Loading {firstScene}...", Tag);
            sceneLoader.LoadScene(firstScene);
        }

        /// <summary>
        /// MainMenu normally. In the Editor, returns to the scene that was open when Play was pressed
        /// (see Editor/PlayFromBootstrap).
        /// </summary>
        private static string GetFirstSceneName()
        {
#if UNITY_EDITOR
            string returnScene = UnityEditor.SessionState.GetString(GameConstants.EDITOR_RETURN_SCENE_KEY, "");
            if (!string.IsNullOrEmpty(returnScene) && returnScene != GameConstants.SCENE_BOOTSTRAP)
            {
                return returnScene;
            }
#endif
            return GameConstants.SCENE_MAIN_MENU;
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

        private void Fail(string message)
        {
            Log.Error(message, Tag);
            UpdateLoadingUI($"Error: {message}", 0f);
        }

        private void UpdateLoadingUI(string message, float progress)
        {
            if (loadingText != null) loadingText.text = message;
            if (progressBar != null) progressBar.value = progress;
        }
    }
}
