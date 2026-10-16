using System;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using MafiaNight.Player;

namespace MafiaNight.Managers
{
    /// <summary>
    /// Manages local player profile data and PlayFab synchronization
    /// Singleton - access via Instance
    /// </summary>
    public class PlayerProfileManager : MonoBehaviour
    {
        #region Singleton
        
        private static PlayerProfileManager _instance;
        public static PlayerProfileManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<PlayerProfileManager>();
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Events
        
        public event Action<PlayerProfile> OnProfileLoaded;
        public event Action<PlayerProfile> OnProfileUpdated;
        public event Action<string> OnLoadError;
        public event Action OnProfileSaved;
        
        #endregion
        
        #region Serialized Fields
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        
        #endregion
        
        #region Private Fields
        
        private PlayerProfile _localProfile;
        private bool _isLoading;
        private bool _isSaving;
        private string _playFabId;
        
        // PlayFab data keys
        private const string KEY_PROFILE = "PlayerProfile";
        private const string KEY_CUSTOMIZATION = "Customization";
        private const string KEY_INVENTORY = "Inventory";
        private const string KEY_STATISTICS = "Statistics";
        
        #endregion
        
        #region Properties
        
        /// <summary>Current local player profile</summary>
        public PlayerProfile LocalProfile => _localProfile;
        
        /// <summary>Quick access to public data for sharing</summary>
        public PlayerPublicData PublicData => _localProfile?.ToPublicData();
        
        public bool IsLoading => _isLoading;
        public bool IsSaving => _isSaving;
        public bool HasProfile => _localProfile != null;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            _localProfile = new PlayerProfile();
        }
        
        #endregion
        
        #region Public Methods - Load
        
        /// <summary>
        /// Load player profile from PlayFab after login
        /// </summary>
        public void LoadProfile(string playFabId)
        {
            if (_isLoading)
            {
                Log("Already loading profile...");
                return;
            }
            
            _playFabId = playFabId;
            _isLoading = true;
            
            Log($"Loading profile for PlayFab ID: {playFabId}");
            
            // Get user data from PlayFab
            var request = new GetUserDataRequest
            {
                Keys = new System.Collections.Generic.List<string>
                {
                    KEY_PROFILE,
                    KEY_CUSTOMIZATION,
                    KEY_INVENTORY,
                    KEY_STATISTICS
                }
            };
            
            PlayFabClientAPI.GetUserData(request, OnProfileDataLoaded, OnProfileLoadError);
        }
        
        /// <summary>
        /// Refresh profile from PlayFab
        /// </summary>
        public void RefreshProfile()
        {
            if (!string.IsNullOrEmpty(_playFabId))
            {
                LoadProfile(_playFabId);
            }
        }
        
        #endregion
        
        #region Public Methods - Save
        
        /// <summary>
        /// Save entire profile to PlayFab
        /// </summary>
        public void SaveProfile()
        {
            if (_localProfile == null)
            {
                LogError("No profile to save!");
                return;
            }
            
            if (_isSaving)
            {
                Log("Already saving...");
                return;
            }
            
            _isSaving = true;
            Log("Saving profile to PlayFab...");
            
            // Serialize profile data
            string profileJson = JsonUtility.ToJson(_localProfile);
            string customizationJson = JsonUtility.ToJson(_localProfile.Customization);
            string inventoryJson = JsonUtility.ToJson(_localProfile.Inventory);
            string statisticsJson = JsonUtility.ToJson(_localProfile.Statistics);
            
            var request = new UpdateUserDataRequest
            {
                Data = new System.Collections.Generic.Dictionary<string, string>
                {
                    { KEY_PROFILE, profileJson },
                    { KEY_CUSTOMIZATION, customizationJson },
                    { KEY_INVENTORY, inventoryJson },
                    { KEY_STATISTICS, statisticsJson }
                }
            };
            
            PlayFabClientAPI.UpdateUserData(request, HandleProfileSaveSuccess, OnProfileSaveError);
        }
        
        /// <summary>
        /// Save only customization data (quick save for cosmetic changes)
        /// </summary>
        public void SaveCustomization()
        {
            if (_localProfile?.Customization == null) return;
            
            string json = JsonUtility.ToJson(_localProfile.Customization);
            
            var request = new UpdateUserDataRequest
            {
                Data = new System.Collections.Generic.Dictionary<string, string>
                {
                    { KEY_CUSTOMIZATION, json }
                }
            };
            
            PlayFabClientAPI.UpdateUserData(request, 
                (result) => Log("Customization saved"),
                (error) => LogError($"Failed to save customization: {error.GenerateErrorReport()}"));
        }
        
        /// <summary>
        /// Save only statistics (after game ends)
        /// </summary>
        public void SaveStatistics()
        {
            if (_localProfile?.Statistics == null) return;
            
            string json = JsonUtility.ToJson(_localProfile.Statistics);
            
            var request = new UpdateUserDataRequest
            {
                Data = new System.Collections.Generic.Dictionary<string, string>
                {
                    { KEY_STATISTICS, json }
                }
            };
            
            PlayFabClientAPI.UpdateUserData(request,
                (result) => Log("Statistics saved"),
                (error) => LogError($"Failed to save statistics: {error.GenerateErrorReport()}"));
        }
        
        #endregion
        
        #region Public Methods - Update Profile
        
        /// <summary>
        /// Update display name locally and on PlayFab
        /// </summary>
        public void UpdateDisplayName(string newName, Action<bool> callback = null)
        {
            if (string.IsNullOrEmpty(newName) || newName.Length < 3)
            {
                callback?.Invoke(false);
                return;
            }
            
            var request = new UpdateUserTitleDisplayNameRequest
            {
                DisplayName = newName
            };
            
            PlayFabClientAPI.UpdateUserTitleDisplayName(request,
                (result) =>
                {
                    _localProfile.DisplayName = result.DisplayName;
                    Log($"Display name updated: {result.DisplayName}");
                    OnProfileUpdated?.Invoke(_localProfile);
                    callback?.Invoke(true);
                },
                (error) =>
                {
                    LogError($"Failed to update name: {error.GenerateErrorReport()}");
                    callback?.Invoke(false);
                });
        }
        
        /// <summary>
        /// Add XP to player (auto level up)
        /// </summary>
        public bool AddXP(int amount)
        {
            if (_localProfile == null) return false;
            
            bool leveledUp = _localProfile.AddXP(amount);
            OnProfileUpdated?.Invoke(_localProfile);
            
            if (leveledUp)
            {
                Log($"Level Up! Now level {_localProfile.Level}");
            }
            
            return leveledUp;
        }
        
        /// <summary>
        /// Add rank points (after game)
        /// </summary>
        public void AddRankPoints(int points)
        {
            if (_localProfile == null) return;
            
            PlayerRank oldRank = _localProfile.Rank;
            _localProfile.AddRankPoints(points);
            
            if (_localProfile.Rank != oldRank)
            {
                Log($"Rank changed: {oldRank} → {_localProfile.Rank}");
            }
            
            OnProfileUpdated?.Invoke(_localProfile);
        }
        
        /// <summary>
        /// Record game result
        /// </summary>
        public void RecordGameResult(bool won, bool wasMafia)
        {
            if (_localProfile?.Statistics == null) return;
            
            _localProfile.Statistics.RecordGame(won, wasMafia);
            Log($"Game recorded: {(won ? "Won" : "Lost")} as {(wasMafia ? "Mafia" : "Citizen")}");
            
            // Auto-save statistics
            SaveStatistics();
        }
        
        /// <summary>
        /// Set active profile frame
        /// </summary>
        public void SetActiveFrame(string frameID)
        {
            if (_localProfile?.Customization == null) return;
            
            if (_localProfile.Customization.OwnsFrame(frameID))
            {
                _localProfile.Customization.ProfileFrameID = frameID;
                OnProfileUpdated?.Invoke(_localProfile);
                SaveCustomization();
            }
        }
        
        /// <summary>
        /// Set active chat background
        /// </summary>
        public void SetActiveChatBackground(string bgID)
        {
            if (_localProfile?.Customization == null) return;
            
            if (_localProfile.Customization.OwnsBackground(bgID))
            {
                _localProfile.Customization.ChatBackgroundID = bgID;
                OnProfileUpdated?.Invoke(_localProfile);
                SaveCustomization();
            }
        }
        
        #endregion
        
        #region PlayFab Callbacks
        
        private void OnProfileDataLoaded(GetUserDataResult result)
        {
            _isLoading = false;
            
            try
            {
                // Load main profile
                if (result.Data.TryGetValue(KEY_PROFILE, out var profileRecord))
                {
                    _localProfile = JsonUtility.FromJson<PlayerProfile>(profileRecord.Value);
                    Log("Profile loaded from PlayFab");
                }
                else
                {
                    // New player - create default profile
                    _localProfile = new PlayerProfile();
                    _localProfile.PlayFabId = _playFabId;
                    Log("Created new profile for new player");
                }
                
                // Load customization
                if (result.Data.TryGetValue(KEY_CUSTOMIZATION, out var customRecord))
                {
                    _localProfile.Customization = JsonUtility.FromJson<PlayerCustomization>(customRecord.Value);
                }
                else
                {
                    _localProfile.Customization = new PlayerCustomization();
                }
                
                // Load inventory
                if (result.Data.TryGetValue(KEY_INVENTORY, out var invRecord))
                {
                    _localProfile.Inventory = JsonUtility.FromJson<PlayerInventory>(invRecord.Value);
                }
                else
                {
                    _localProfile.Inventory = new PlayerInventory();
                }
                
                // Load statistics
                if (result.Data.TryGetValue(KEY_STATISTICS, out var statsRecord))
                {
                    _localProfile.Statistics = JsonUtility.FromJson<PlayerStatistics>(statsRecord.Value);
                }
                else
                {
                    _localProfile.Statistics = new PlayerStatistics();
                }
                
                // Update last login
                _localProfile.Statistics.UpdateLastLogin();
                
                // Fetch display name from PlayFab profile
                FetchDisplayName();
                
                OnProfileLoaded?.Invoke(_localProfile);
            }
            catch (Exception e)
            {
                LogError($"Error parsing profile data: {e.Message}");
                OnLoadError?.Invoke(e.Message);
            }
        }
        
        private void OnProfileLoadError(PlayFabError error)
        {
            _isLoading = false;
            LogError($"Failed to load profile: {error.GenerateErrorReport()}");
            OnLoadError?.Invoke(error.GenerateErrorReport());
        }
        
        private void HandleProfileSaveSuccess(UpdateUserDataResult result)
        {
            _isSaving = false;
            Log("Profile saved successfully");
            OnProfileSaved?.Invoke();
        }
        
        private void OnProfileSaveError(PlayFabError error)
        {
            _isSaving = false;
            LogError($"Failed to save profile: {error.GenerateErrorReport()}");
        }
        
        private void FetchDisplayName()
        {
            var request = new GetPlayerProfileRequest
            {
                ProfileConstraints = new PlayerProfileViewConstraints
                {
                    ShowDisplayName = true
                }
            };
            
            PlayFabClientAPI.GetPlayerProfile(request,
                (result) =>
                {
                    if (!string.IsNullOrEmpty(result.PlayerProfile?.DisplayName))
                    {
                        _localProfile.DisplayName = result.PlayerProfile.DisplayName;
                        OnProfileUpdated?.Invoke(_localProfile);
                    }
                },
                (error) => LogError($"Failed to get display name: {error.GenerateErrorReport()}"));
        }
        
        #endregion
        
        #region Logging
        
        private void Log(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[PlayerProfileManager] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[PlayerProfileManager] {message}");
        }
        
        #endregion
    }
}
