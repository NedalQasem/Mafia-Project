using System;
using Epic.OnlineServices;
using MafiaNight.Core;
using MafiaNight.Services.Config;
using MafiaNight.Services.Telemetry;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace MafiaNight.Services.Auth
{
    /// <summary>
    /// Bridges EOS authentication to PlayFab: waits for the EOS login on the same GameObject,
    /// then logs into PlayFab with the EOS Account ID and raises <see cref="OnPlayFabLoggedIn"/>.
    /// Loading the profile / wallet afterwards is the Bootstrap's job, not this class's.
    /// </summary>
    [RequireComponent(typeof(EOSSDKComponent))]
    public class PlayFabEOSLogin : PersistentService<IAuthService>, IAuthService
    {
        private const string Tag = "Auth";
        private const string DefaultTitleIdPlaceholder = "YOUR_TITLE_ID";

        [Header("PlayFab Settings")]
        [SerializeField] private string playFabTitleId = DefaultTitleIdPlaceholder; // Or configured by PlayFab EdEx

        [Tooltip("Name of the OpenID Connect connection configured in PlayFab for EOS (issuer https://api.epicgames.dev). " +
                 "Empty = fall back to the less secure CustomID login.")]
        [SerializeField] private string openIdConnectionId = "";

        [Header("Status (read-only)")]
        [SerializeField] private bool isEOSLoggedIn = false;
        [SerializeField] private bool isPlayFabLoggedIn = false;

        private EOSSDKComponent _eos;
        private string _productUserId;
        private string _playFabId;

        public bool IsEOSLoggedIn => isEOSLoggedIn;
        public bool IsPlayFabLoggedIn => isPlayFabLoggedIn;
        public string ProductUserId => _productUserId;
        public string PlayFabId => _playFabId;

        public event Action<string> OnPlayFabLoggedIn;
        public event Action<string> OnLoginFailed;

        protected override void OnServiceAwake()
        {
            // the Dev / Live environment asset wins over values saved in the scene
            var env = EnvironmentConfig.Current;
            if (env != null && env.IsPlayFabComplete) playFabTitleId = env.playFabTitleId;
            if (env != null && !string.IsNullOrEmpty(env.openIdConnectionId)) openIdConnectionId = env.openIdConnectionId;

            if (!string.IsNullOrEmpty(playFabTitleId) && playFabTitleId != DefaultTitleIdPlaceholder)
            {
                PlayFabSettings.TitleId = playFabTitleId;
            }

            _eos = GetComponent<EOSSDKComponent>();
            _eos.OnLoggedIn += HandleEOSLoggedIn;
            _eos.OnLoginFailed += HandleEOSLoginFailed;
        }

        protected override void OnDestroy()
        {
            if (_eos != null)
            {
                _eos.OnLoggedIn -= HandleEOSLoggedIn;
                _eos.OnLoginFailed -= HandleEOSLoginFailed;
            }
            base.OnDestroy();
        }

        /// <summary>Retries from the step that failed: PlayFab when EOS already succeeded, otherwise the whole EOS login.</summary>
        public void RetryLogin()
        {
            if (isPlayFabLoggedIn) return;
            if (isEOSLoggedIn) LoginToPlayFab();
            else _eos.StartLogin();
        }

        #region EOS

        private void HandleEOSLoggedIn(ProductUserId userId)
        {
            _productUserId = userId.ToString();
            isEOSLoggedIn = true;

            Log.Info($"EOS login success. ProductUserId: {_productUserId}", Tag);
            LoginToPlayFab();
        }

        private void HandleEOSLoginFailed(Result result)
        {
            isEOSLoggedIn = false;
            OnLoginFailed?.Invoke($"EOS login failed: {result}");
        }

        #endregion

        #region PlayFab

        private void LoginToPlayFab()
        {
            // Preferred: PlayFab verifies the EOS-signed ID token itself (can't be forged by the client)
            if (!string.IsNullOrEmpty(openIdConnectionId) && _eos.TryGetIdToken(out string idToken))
            {
                Log.Info("Logging into PlayFab with EOS ID token (OpenID Connect)...", Tag);
                var oidcRequest = new LoginWithOpenIdConnectRequest
                {
                    ConnectionId = openIdConnectionId,
                    IdToken = idToken,
                    CreateAccount = true,
                    TitleId = PlayFabSettings.TitleId
                };
                PlayFabMeter.Allow("LoginWithOpenIdConnect", false);
                PlayFabClientAPI.LoginWithOpenIdConnect(oidcRequest, HandlePlayFabLoginSuccess, HandlePlayFabLoginFailure);
                return;
            }

            // CustomID = ProductUserId can be impersonated by anyone who knows it, so release builds refuse it.
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                Log.Error("OpenID Connect is not configured for PlayFab (EnvironmentConfig.openIdConnectionId)", Tag);
                OnLoginFailed?.Invoke("تعذّر تسجيل الدخول: الإعداد الأمني غير مكتمل");
                return;
            }

            // TODO(security): CustomID = ProductUserId can be impersonated by anyone who knows it.
            // Configure the OpenID connection in PlayFab and set openIdConnectionId (see Docs/04-technical-debt.md).
            Log.Warning("Logging into PlayFab with CustomID (insecure - set openIdConnectionId)", Tag);
            var request = new LoginWithCustomIDRequest
            {
                CustomId = _productUserId,
                CreateAccount = true,
                TitleId = PlayFabSettings.TitleId
            };

            PlayFabMeter.Allow("LoginWithCustomID", false);
            PlayFabClientAPI.LoginWithCustomID(request, HandlePlayFabLoginSuccess, HandlePlayFabLoginFailure);
        }

        private void HandlePlayFabLoginSuccess(LoginResult result)
        {
            isPlayFabLoggedIn = true;
            _playFabId = result.PlayFabId;
            Log.Info($"PlayFab login success. PlayFab ID: {result.PlayFabId} (new account: {result.NewlyCreated})", Tag);

            EnsureDisplayName();
            OnPlayFabLoggedIn?.Invoke(result.PlayFabId);
        }

        private void HandlePlayFabLoginFailure(PlayFabError error)
        {
            isPlayFabLoggedIn = false;
            Log.Error($"PlayFab login failed: {error.GenerateErrorReport()}", Tag);
            OnLoginFailed?.Invoke($"PlayFab login failed: {error.ErrorMessage}");
        }

        #endregion

        #region Display Name

        /// <summary>Gives new accounts (or legacy "EOSPlayer_" names) a readable display name.</summary>
        private void EnsureDisplayName()
        {
            var request = new GetPlayerProfileRequest
            {
                ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true }
            };

            PlayFabMeter.Allow("GetPlayerProfile", false);
            PlayFabClientAPI.GetPlayerProfile(request,
                result =>
                {
                    string existingName = result.PlayerProfile?.DisplayName;
                    if (string.IsNullOrEmpty(existingName) || existingName.StartsWith("EOSPlayer_"))
                    {
                        SetDisplayName(PickDefaultDisplayName());
                    }
                },
                error =>
                {
                    Log.Warning($"Failed to get profile: {error.GenerateErrorReport()}", Tag);
                    SetDisplayName(PickDefaultDisplayName());
                });
        }

        private static string PickDefaultDisplayName()
        {
            string customName = PlayerPrefs.GetString(GameConstants.PREF_PLAYER_CUSTOM_NAME, "");
            if (!string.IsNullOrEmpty(customName)) return customName;

            // TODO: Query EOS UserInfo for the real EOS display name
            return $"Player{UnityEngine.Random.Range(1000, 9999)}";
        }

        private void SetDisplayName(string displayName)
        {
            if (!PlayFabMeter.Allow("UpdateUserTitleDisplayName", true)) return;
            var request = new UpdateUserTitleDisplayNameRequest { DisplayName = displayName };
            PlayFabClientAPI.UpdateUserTitleDisplayName(request,
                result => Log.Info($"Display name set: {result.DisplayName}", Tag),
                error => Log.Warning($"Failed to set display name: {error.GenerateErrorReport()}", Tag));
        }

        #endregion
    }
}
