using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using PlayFab;
using PlayFab.ClientModels;

/// <summary>
/// Integrates Epic Online Services (EOS) authentication with PlayFab.
/// Logs into EOS first, then uses the EOS Account ID to login to PlayFab.
/// </summary>
public class PlayFabEOSLogin : MonoBehaviour
{
    [Header("PlayFab Settings")]
    [SerializeField] private string playFabTitleId = "YOUR_TITLE_ID"; // Set this in Inspector or PlayFab EdEx will auto-configure
    
    [Header("Status")]
    [SerializeField] private bool isEOSLoggedIn = false;
    [SerializeField] private bool isPlayFabLoggedIn = false;
    
    private EpicAccountId _eosAccountId;
    private string _eosAccountIdString;

    private void Start()
    {
        // Configure PlayFab (if not already configured via EdEx)
        if (!string.IsNullOrEmpty(playFabTitleId) && playFabTitleId != "YOUR_TITLE_ID")
        {
            PlayFabSettings.TitleId = playFabTitleId;
        }
        
        Debug.Log("PlayFab EOS Login Manager initialized. Waiting for EOS login...");
    }

    /// <summary>
    /// Call this method after EOS login succeeds (from EOSSDKComponent callback)
    /// </summary>
    public void OnEOSLoginSuccess(EpicAccountId accountId)
    {
        _eosAccountId = accountId;
        _eosAccountIdString = accountId.ToString();
        isEOSLoggedIn = true;
        
        Debug.Log($"EOS Login Success! Account ID: {_eosAccountIdString}");
        
        // Now login to PlayFab using EOS Account ID
        LoginToPlayFab();
    }

    private void LoginToPlayFab()
    {
        Debug.Log("Attempting PlayFab login with EOS Account ID...");
        
        // Use CustomID login - EOS Account ID as the unique identifier
        var request = new LoginWithCustomIDRequest
        {
            CustomId = _eosAccountIdString,
            CreateAccount = true, // Create account if doesn't exist
            TitleId = PlayFabSettings.TitleId
        };

        PlayFabClientAPI.LoginWithCustomID(request, OnPlayFabLoginSuccess, OnPlayFabLoginFailure);
    }

    private void OnPlayFabLoginSuccess(LoginResult result)
    {
        isPlayFabLoggedIn = true;
        Debug.Log($"PlayFab Login Success! PlayFab ID: {result.PlayFabId}");
        Debug.Log($"New Account: {result.NewlyCreated}");
        
        // Save CustomID for fallback login (testing/development)
        PlayerPrefs.SetString("SavedEOSCustomID", _eosAccountIdString);
        PlayerPrefs.Save();
        Debug.Log($"[PlayFab] Saved CustomID for fallback: {_eosAccountIdString}");
        
        // Fetch currency balances now that we're logged in
        var currencyManager = MafiaNight.Economy.CurrencyManager.Instance;
        if (currencyManager != null)
        {
            currencyManager.FetchCurrencies();
        }
        
        // Optional: Update display name with EOS username
        UpdatePlayFabDisplayName();
    }

    private void OnPlayFabLoginFailure(PlayFabError error)
    {
        isPlayFabLoggedIn = false;
        Debug.LogError($"PlayFab Login Failed: {error.GenerateErrorReport()}");
    }

    private void UpdatePlayFabDisplayName()
    {
        // First, check if player already has a display name
        var getProfileRequest = new GetPlayerProfileRequest
        {
            ProfileConstraints = new PlayerProfileViewConstraints
            {
                ShowDisplayName = true
            }
        };

        PlayFabClientAPI.GetPlayerProfile(getProfileRequest, 
            (profileResult) =>
            {
                string existingName = profileResult.PlayerProfile?.DisplayName;
                
                // Only update if name is null/empty or still using the old EOSPlayer_ format
                if (string.IsNullOrEmpty(existingName) || existingName.StartsWith("EOSPlayer_"))
                {
                    // Try to get a better name from EOS (you can customize this)
                    string newDisplayName = GetBetterDisplayName();
                    
                    var updateRequest = new UpdateUserTitleDisplayNameRequest
                    {
                        DisplayName = newDisplayName
                    };

                    PlayFabClientAPI.UpdateUserTitleDisplayName(updateRequest, 
                        OnDisplayNameUpdateSuccess, 
                        OnDisplayNameUpdateFailure);
                }
                else
                {
                    Debug.Log($"[PlayFab] Player already has display name: {existingName}");
                }
            },
            (error) =>
            {
                Debug.LogWarning($"[PlayFab] Failed to get profile: {error.GenerateErrorReport()}");
                // Fallback: set default name
                SetDefaultDisplayName();
            });
    }
    
    private string GetBetterDisplayName()
    {
        // Priority order:
        // 1. Custom name from PlayerPrefs (if user set it)
        string customName = PlayerPrefs.GetString("PlayerCustomName", "");
        if (!string.IsNullOrEmpty(customName))
        {
            Debug.Log($"[PlayFab] Using custom name: {customName}");
            return customName;
        }
        
        // 2. EOS Display Name (if available)
        // TODO: Implement EOS UserInfo query to get actual EOS display name
        
        // 3. Fallback: Generic Player name
        return $"Player{UnityEngine.Random.Range(1000, 9999)}";
    }
    
    private void SetDefaultDisplayName()
    {
        var request = new UpdateUserTitleDisplayNameRequest
        {
            DisplayName = GetBetterDisplayName()
        };

        PlayFabClientAPI.UpdateUserTitleDisplayName(request, 
            OnDisplayNameUpdateSuccess, 
            OnDisplayNameUpdateFailure);
    }


    private void OnDisplayNameUpdateSuccess(UpdateUserTitleDisplayNameResult result)
    {
        Debug.Log($"[PlayFab] Display Name Updated: {result.DisplayName}");
    }

    private void OnDisplayNameUpdateFailure(PlayFabError error)
    {
        Debug.LogWarning($"[PlayFab] Failed to update display name: {error.GenerateErrorReport()}");
    }

    /// <summary>
    /// Example: Save player data to PlayFab
    /// </summary>
    public void SavePlayerData(string key, string value)
    {
        if (!isPlayFabLoggedIn)
        {
            Debug.LogWarning("Cannot save data - not logged into PlayFab");
            return;
        }

        var request = new UpdateUserDataRequest
        {
            Data = new System.Collections.Generic.Dictionary<string, string>
            {
                { key, value }
            }
        };

        PlayFabClientAPI.UpdateUserData(request, OnDataSaveSuccess, OnDataSaveFailure);
    }

    private void OnDataSaveSuccess(UpdateUserDataResult result)
    {
        Debug.Log("Player data saved successfully to PlayFab");
    }

    private void OnDataSaveFailure(PlayFabError error)
    {
        Debug.LogError($"Failed to save player data: {error.GenerateErrorReport()}");
    }

    /// <summary>
    /// Example: Load player data from PlayFab
    /// </summary>
    public void LoadPlayerData(string key, System.Action<string> onSuccess)
    {
        if (!isPlayFabLoggedIn)
        {
            Debug.LogWarning("Cannot load data - not logged into PlayFab");
            return;
        }

        var request = new GetUserDataRequest();
        
        PlayFabClientAPI.GetUserData(request, 
            (result) => 
            {
                if (result.Data.TryGetValue(key, out var record))
                {
                    Debug.Log($"Loaded data for key '{key}': {record.Value}");
                    onSuccess?.Invoke(record.Value);
                }
                else
                {
                    Debug.Log($"No data found for key '{key}'");
                    onSuccess?.Invoke(null);
                }
            }, 
            (error) => 
            {
                Debug.LogError($"Failed to load player data: {error.GenerateErrorReport()}");
                onSuccess?.Invoke(null);
            });
    }

    // Public getters
    public bool IsEOSLoggedIn => isEOSLoggedIn;
    public bool IsPlayFabLoggedIn => isPlayFabLoggedIn;
    public string EOSAccountId => _eosAccountIdString;
    
    /// <summary>
    /// Get the current PlayFab display name
    /// </summary>
    public void GetDisplayName(System.Action<string> callback)
    {
        if (!isPlayFabLoggedIn)
        {
            callback?.Invoke("Not Logged In");
            return;
        }
        
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
                string displayName = result.PlayerProfile?.DisplayName ?? "Unknown";
                callback?.Invoke(displayName);
            },
            (error) =>
            {
                Debug.LogError($"Failed to get display name: {error.GenerateErrorReport()}");
                callback?.Invoke("Error");
            });
    }
}

