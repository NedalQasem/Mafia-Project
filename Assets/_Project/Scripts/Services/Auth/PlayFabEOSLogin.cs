using System;
using Epic.OnlineServices;
using MafiaNight.Core;
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

        [Header("Status (read-only)")]
        [SerializeField] private bool isEOSLoggedIn = false;
        [SerializeField] private bool isPlayFabLoggedIn = false;

        private EOSSDKComponent _eos;
        private string _eosAccountIdString;
        private string _playFabId;

        public bool IsEOSLoggedIn => isEOSLoggedIn;
        public bool IsPlayFabLoggedIn => isPlayFabLoggedIn;
        public string EOSAccountId => _eosAccountIdString;
        public string PlayFabId => _playFabId;

        public event Action<string> OnPlayFabLoggedIn;
        public event Action<string> OnLoginFailed;

        protected override void OnServiceAwake()
        {
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

        #region EOS

        private void HandleEOSLoggedIn(EpicAccountId accountId)
        {
            _eosAccountIdString = accountId.ToString();
            isEOSLoggedIn = true;

            Log.Info($"EOS login success. Account ID: {_eosAccountIdString}", Tag);
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
            Log.Info("Logging into PlayFab with EOS Account ID...", Tag);

            // TODO(security): CustomID = EOS Account ID is guessable. Move to LoginWithOpenIdConnect
            // using the EOS ID token so PlayFab verifies the identity server-side.
            var request = new LoginWithCustomIDRequest
            {
                CustomId = _eosAccountIdString,
                CreateAccount = true,
                TitleId = PlayFabSettings.TitleId
            };

            PlayFabClientAPI.LoginWithCustomID(request, HandlePlayFabLoginSuccess, HandlePlayFabLoginFailure);
        }

        private void HandlePlayFabLoginSuccess(LoginResult result)
        {
            isPlayFabLoggedIn = true;
            _playFabId = result.PlayFabId;
            Log.Info($"PlayFab login success. PlayFab ID: {result.PlayFabId} (new account: {result.NewlyCreated})", Tag);

            PlayerPrefs.SetString(GameConstants.PREF_SAVED_EOS_CUSTOM_ID, _eosAccountIdString);
            PlayerPrefs.Save();

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
            var request = new UpdateUserTitleDisplayNameRequest { DisplayName = displayName };
            PlayFabClientAPI.UpdateUserTitleDisplayName(request,
                result => Log.Info($"Display name set: {result.DisplayName}", Tag),
                error => Log.Warning($"Failed to set display name: {error.GenerateErrorReport()}", Tag));
        }

        #endregion
    }
}
