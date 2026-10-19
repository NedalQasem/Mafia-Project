using System;
using System.Collections.Generic;
using MafiaNight.Core;
using MafiaNight.Services.Telemetry;
using MafiaNight.Data.Player;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace MafiaNight.Services.Profile
{
    /// <summary>
    /// Manages the local player profile and its PlayFab User Data synchronization.
    /// </summary>
    public class PlayerProfileManager : PersistentService<IPlayerProfileService>, IPlayerProfileService
    {
        private const string Tag = "Profile";

        // PlayFab User Data keys
        private const string KEY_PROFILE = "PlayerProfile";
        private const string KEY_CUSTOMIZATION = "Customization";
        private const string KEY_INVENTORY = "Inventory";
        private const string KEY_STATISTICS = "Statistics";

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private PlayerProfile _localProfile;
        private bool _isLoading;
        private bool _isSaving;
        private string _playFabId;

        #region IPlayerProfileService

        public PlayerProfile LocalProfile => _localProfile;
        public PlayerPublicData PublicData => _localProfile?.ToPublicData();
        public bool HasProfile => _localProfile != null;
        public bool IsLoading => _isLoading;
        public bool IsSaving => _isSaving;
        public bool IsNewProfile { get; private set; }

        public event Action<PlayerProfile> OnProfileLoaded;
        public event Action<PlayerProfile> OnProfileUpdated;
        public event Action<string> OnLoadError;
        public event Action OnProfileSaved;

        #endregion

        #region Load

        public void LoadProfile(string playFabId)
        {
            if (_isLoading)
            {
                LogInfo("Already loading profile...");
                return;
            }

            _playFabId = playFabId;
            _isLoading = true;
            LogInfo($"Loading profile for PlayFab ID: {playFabId}");

            var request = new GetUserDataRequest
            {
                Keys = new List<string> { KEY_PROFILE, KEY_CUSTOMIZATION, KEY_INVENTORY, KEY_STATISTICS }
            };

            PlayFabMeter.Allow("GetUserData", false);
            PlayFabClientAPI.GetUserData(request, HandleProfileDataLoaded, HandleProfileLoadError);
        }

        public void RefreshProfile()
        {
            if (!string.IsNullOrEmpty(_playFabId))
            {
                LoadProfile(_playFabId);
            }
        }

        #endregion

        #region Save

        public void SaveProfile()
        {
            if (_localProfile == null)
            {
                Log.Error("No profile to save!", Tag);
                return;
            }

            if (_isSaving)
            {
                LogInfo("Already saving...");
                return;
            }

            if (!PlayFabMeter.Allow("UpdateUserData", true)) return;   // cost guard: write limit reached
            _isSaving = true;
            LogInfo("Saving profile to PlayFab...");

            var request = new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string>
                {
                    { KEY_PROFILE, JsonUtility.ToJson(_localProfile) },
                    { KEY_CUSTOMIZATION, JsonUtility.ToJson(_localProfile.Customization) },
                    { KEY_INVENTORY, JsonUtility.ToJson(_localProfile.Inventory) },
                    { KEY_STATISTICS, JsonUtility.ToJson(_localProfile.Statistics) }
                }
            };

            PlayFabClientAPI.UpdateUserData(request, HandleProfileSaveSuccess, HandleProfileSaveError);
        }

        /// <summary>Quick save for cosmetic changes only.</summary>
        private void SaveCustomization()
        {
            if (_localProfile?.Customization == null) return;
            SaveSingleKey(KEY_CUSTOMIZATION, JsonUtility.ToJson(_localProfile.Customization));
        }

        private void SaveSingleKey(string key, string json)
        {
            if (!PlayFabMeter.Allow("UpdateUserData", true)) return;
            var request = new UpdateUserDataRequest { Data = new Dictionary<string, string> { { key, json } } };
            PlayFabClientAPI.UpdateUserData(request,
                result => LogInfo($"{key} saved"),
                error => Log.Error($"Failed to save {key}: {error.GenerateErrorReport()}", Tag));
        }

        #endregion

        #region Update Profile

        public void UpdateDisplayName(string newName, Action<bool> callback = null)
        {
            if (string.IsNullOrEmpty(newName) || newName.Length < 3)
            {
                callback?.Invoke(false);
                return;
            }

            if (!PlayFabMeter.Allow("UpdateUserTitleDisplayName", true)) { callback?.Invoke(false); return; }
            var request = new UpdateUserTitleDisplayNameRequest { DisplayName = newName };
            PlayFabClientAPI.UpdateUserTitleDisplayName(request,
                result =>
                {
                    _localProfile.DisplayName = result.DisplayName;
                    LogInfo($"Display name updated: {result.DisplayName}");
                    OnProfileUpdated?.Invoke(_localProfile);
                    callback?.Invoke(true);
                },
                error =>
                {
                    Log.Error($"Failed to update name: {error.GenerateErrorReport()}", Tag);
                    callback?.Invoke(false);
                });
        }

        /// <summary>Adds XP (auto level-up). Returns true if the player leveled up.</summary>
        public bool AddXP(int amount)
        {
            if (_localProfile == null) return false;

            bool leveledUp = _localProfile.AddXP(amount);
            if (leveledUp) LogInfo($"Level Up! Now level {_localProfile.Level}");

            OnProfileUpdated?.Invoke(_localProfile);
            return leveledUp;
        }

        public void RecordMatchResult(bool won, bool wasMafia, bool isMvp)
        {
            if (_localProfile == null) return;

            PlayerRank oldRank = _localProfile.Rank;
            int points = RankRules.GetMatchPoints(won, isMvp);
            _localProfile.AddRankPoints(points);
            _localProfile.Statistics?.RecordGame(won, wasMafia);

            LogInfo($"Match recorded: {(won ? "Won" : "Lost")} as {(wasMafia ? "Mafia" : "Citizen")}, " +
                    $"{points:+#;-#;0} pts → {_localProfile.RankPoints} ({_localProfile.Rank})");

            if (_localProfile.Rank != oldRank)
            {
                LogInfo($"Rank changed: {oldRank} → {_localProfile.Rank}");
                // TODO: rank-up rewards must be granted server-side (CloudScript), not by the client.
            }

            OnProfileUpdated?.Invoke(_localProfile);
            SaveProfile();
        }

        public void Equip(string categoryId, string itemId)
        {
            if (_localProfile == null || string.IsNullOrEmpty(categoryId)) return;
            if (_localProfile.Customization == null) _localProfile.Customization = new PlayerCustomization();
            if (_localProfile.Customization.GetEquipped(categoryId) == itemId) return;
            _localProfile.Customization.SetEquipped(categoryId, itemId);
            OnProfileUpdated?.Invoke(_localProfile);
            SaveCustomization();
        }

        public void SetActiveFrame(string frameID)
        {
            if (_localProfile?.Customization == null) return;
            if (!_localProfile.Customization.OwnsFrame(frameID)) return;

            _localProfile.Customization.ProfileFrameID = frameID;
            OnProfileUpdated?.Invoke(_localProfile);
            SaveCustomization();
        }

        public void SetActiveChatBackground(string bgID)
        {
            if (_localProfile?.Customization == null) return;
            if (!_localProfile.Customization.OwnsBackground(bgID)) return;

            _localProfile.Customization.ChatBackgroundID = bgID;
            OnProfileUpdated?.Invoke(_localProfile);
            SaveCustomization();
        }

        #endregion

        #region PlayFab Callbacks

        private void HandleProfileDataLoaded(GetUserDataResult result)
        {
            _isLoading = false;

            try
            {
                if (result.Data.TryGetValue(KEY_PROFILE, out var profileRecord))
                {
                    _localProfile = JsonUtility.FromJson<PlayerProfile>(profileRecord.Value);
                    IsNewProfile = false;
                    LogInfo("Profile loaded from PlayFab");
                }
                else
                {
                    _localProfile = new PlayerProfile { PlayFabId = _playFabId };
                    IsNewProfile = true;
                    LogInfo("Created new profile for new player");
                }

                _localProfile.Customization = LoadOrDefault(result, KEY_CUSTOMIZATION, () => new PlayerCustomization());
                _localProfile.Inventory = LoadOrDefault(result, KEY_INVENTORY, () => new PlayerInventory());
                _localProfile.Statistics = LoadOrDefault(result, KEY_STATISTICS, () => new PlayerStatistics());

                _localProfile.Statistics.UpdateLastLogin();
                FetchDisplayName();

                OnProfileLoaded?.Invoke(_localProfile);
            }
            catch (Exception e)
            {
                Log.Error($"Error parsing profile data: {e.Message}", Tag);
                OnLoadError?.Invoke(e.Message);
            }
        }

        private static T LoadOrDefault<T>(GetUserDataResult result, string key, Func<T> createDefault)
        {
            return result.Data.TryGetValue(key, out var record)
                ? JsonUtility.FromJson<T>(record.Value)
                : createDefault();
        }

        private void HandleProfileLoadError(PlayFabError error)
        {
            _isLoading = false;
            Log.Error($"Failed to load profile: {error.GenerateErrorReport()}", Tag);
            OnLoadError?.Invoke(error.GenerateErrorReport());
        }

        private void HandleProfileSaveSuccess(UpdateUserDataResult result)
        {
            _isSaving = false;
            LogInfo("Profile saved successfully");
            OnProfileSaved?.Invoke();
        }

        private void HandleProfileSaveError(PlayFabError error)
        {
            _isSaving = false;
            Log.Error($"Failed to save profile: {error.GenerateErrorReport()}", Tag);
        }

        private void FetchDisplayName()
        {
            var request = new GetPlayerProfileRequest
            {
                ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true }
            };

            PlayFabMeter.Allow("GetPlayerProfile", false);
            PlayFabClientAPI.GetPlayerProfile(request,
                result =>
                {
                    if (!string.IsNullOrEmpty(result.PlayerProfile?.DisplayName))
                    {
                        _localProfile.DisplayName = result.PlayerProfile.DisplayName;
                        OnProfileUpdated?.Invoke(_localProfile);
                    }
                },
                error => Log.Error($"Failed to get display name: {error.GenerateErrorReport()}", Tag));
        }

        #endregion

        private void LogInfo(string message)
        {
            if (showDebugLogs) Log.Info(message, Tag);
        }
    }
}
