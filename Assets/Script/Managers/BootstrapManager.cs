using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
using UnityEngine.UI;

namespace MafiaNight.Core
{
    /// <summary>
    /// Bootstrap Manager - المسؤول عن تهيئة اللعبة قبل البدء
    /// يتأكد من:
    /// 1. EOS Login Success
    /// 2. PlayFab Login Success
    /// 3. Currency Fetch
    /// 4. ثم ينقل إلى MainMenu
    /// </summary>
    public class BootstrapManager : MonoBehaviour
    {
        [Header("Loading UI")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private TextMeshProUGUI loadingText;
        [SerializeField] private Slider progressBar;

        [Header("Settings")]
        [SerializeField] private float minimumLoadingTime = 2f; // حد أدنى لوقت التحميل
        
        private void Start()
        {
            Debug.Log("[Bootstrap] Starting game initialization...");
            StartCoroutine(InitializeGame());
        }

        private IEnumerator InitializeGame()
        {
            // عرض شاشة التحميل
            if (loadingPanel != null)
                loadingPanel.SetActive(true);

            float startTime = Time.time;
            
            // ================== STEP 1: انتظار EOS SDK ==================
            UpdateLoadingUI("Connecting to Epic Services...", 0.1f);
            
            EOSSDKComponent eosComponent = FindObjectOfType<EOSSDKComponent>();
            if (eosComponent == null)
            {
                Debug.LogError("[Bootstrap] EOSSDKComponent not found!");
                UpdateLoadingUI("Error: EOS SDK not found!", 0f);
                yield break;
            }

            // انتظار تهيئة EOS (بيحصل في Start)
            yield return new WaitForSeconds(0.5f);

            // ================== STEP 2: انتظار EOS Login ==================
            UpdateLoadingUI("Logging in to Epic...", 0.3f);

            PlayFabEOSLogin playFabLogin = FindObjectOfType<PlayFabEOSLogin>();
            if (playFabLogin == null)
            {
                Debug.LogError("[Bootstrap] PlayFabEOSLogin not found!");
                UpdateLoadingUI("Error: PlayFab Login component not found!", 0f);
                yield break;
            }

            // انتظار EOS Login (بيتم تلقائياً من EOSSDKComponent)
            float timeout = 10f;
            float elapsed = 0f;
            
            while (!playFabLogin.IsEOSLoggedIn && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!playFabLogin.IsEOSLoggedIn)
            {
                Debug.LogError("[Bootstrap] EOS Login timeout!");
                UpdateLoadingUI("Error: Could not connect to Epic Services", 0f);
                yield break;
            }

            Debug.Log("[Bootstrap] ✅ EOS Login successful");

            // ================== STEP 3: انتظار PlayFab Login ==================
            UpdateLoadingUI("Connecting to PlayFab...", 0.5f);

            elapsed = 0f;
            while (!playFabLogin.IsPlayFabLoggedIn && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!playFabLogin.IsPlayFabLoggedIn)
            {
                Debug.LogError("[Bootstrap] PlayFab Login timeout!");
                UpdateLoadingUI("Error: Could not connect to PlayFab", 0f);
                yield break;
            }

            Debug.Log("[Bootstrap] ✅ PlayFab Login successful");

            // ================== STEP 4: جلب العملات ==================
            UpdateLoadingUI("Loading your profile...", 0.7f);

            var currencyManager = MafiaNight.Economy.CurrencyManager.Instance;
            if (currencyManager != null)
            {
                currencyManager.FetchCurrencies();
                // انتظار حتى يتم جلب العملات
                yield return new WaitForSeconds(1f);
            }

            Debug.Log("[Bootstrap] ✅ Currency fetched");

            // ================== STEP 5: تحميل بيانات اللاعب (PlayerProfile) ==================
            UpdateLoadingUI("Loading player data...", 0.8f);

            var profileManager = MafiaNight.Managers.PlayerProfileManager.Instance;
            if (profileManager != null)
            {
                bool profileLoaded = false;

                void OnProfileLoaded(MafiaNight.Player.PlayerProfile profile)
                {
                    profileLoaded = true;
                    profileManager.OnProfileLoaded -= OnProfileLoaded;
                    
                    // Check if this is a new player
                    bool isNewPlayer = profile.Statistics != null && 
                                       profile.Statistics.GamesPlayed == 0 && 
                                       profile.Level == 1 && 
                                       profile.CurrentXP == 0;
                    
                    if (isNewPlayer)
                    {
                        Debug.Log($"[Bootstrap] 🆕 New player detected - Saving initial profile to PlayFab");
                        profileManager.SaveProfile();
                    }
                    
                    Debug.Log($"[Bootstrap] ✅ PlayerProfile loaded: Level {profile.Level}, Rank {profile.Rank}");
                }
                
                profileManager.OnProfileLoaded += OnProfileLoaded;
                
                // If profile already loaded (from PlayFabEOSLogin callback)
                if (profileManager.HasProfile)
                {
                    profileLoaded = true;
                    Debug.Log($"[Bootstrap] ✅ PlayerProfile already loaded: Level {profileManager.LocalProfile.Level}");
                }

                // انتظار تحميل البيانات
                elapsed = 0f;
                timeout = 5f;
                while (!profileLoaded && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!profileLoaded)
                {
                    Debug.LogWarning("[Bootstrap] ⚠️ PlayerProfile load timeout - continuing anyway");
                    profileManager.OnProfileLoaded -= OnProfileLoaded;
                }
            }
            else
            {
                Debug.LogWarning("[Bootstrap] ⚠️ PlayerProfileManager not found - skipping profile load");
            }

            // ================== STEP 6: التأكد من وقت التحميل الأدنى ==================
            UpdateLoadingUI("Preparing Main Menu...", 0.9f);

            float loadTime = Time.time - startTime;
            if (loadTime < minimumLoadingTime)
            {
                yield return new WaitForSeconds(minimumLoadingTime - loadTime);
            }

            // ================== STEP 6: الانتقال إلى MainMenu ==================
            UpdateLoadingUI("Ready!", 1f);
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[Bootstrap] ✅ Initialization complete! Loading MainMenu...");
            SceneManager.LoadScene("MainMenu");
        }

        /// <summary>
        /// تحديث واجهة التحميل
        /// </summary>
        private void UpdateLoadingUI(string message, float progress)
        {
            if (loadingText != null)
                loadingText.text = message;

            if (progressBar != null)
                progressBar.value = progress;

            Debug.Log($"[Bootstrap] {message} ({progress * 100}%)");
        }

        /// <summary>
        /// في حالة فشل التهيئة - إعادة المحاولة
        /// </summary>
        public void RetryInitialization()
        {
            Debug.Log("[Bootstrap] Retrying initialization...");
            StopAllCoroutines();
            StartCoroutine(InitializeGame());
        }
    }
}
