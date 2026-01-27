using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;

namespace MafiaNight.Economy
{
    /// <summary>
    /// Manages player currencies (Coins and Gems) via PlayFab
    /// Singleton for global access
    /// </summary>
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        // Currency codes (must match PlayFab Virtual Currency setup)
        private const string COINS_CODE = "CO";
        private const string GEMS_CODE = "GM";

        [Header("Current Balances")]
        [SerializeField] private int currentCoins = 0;
        [SerializeField] private int currentGems = 0;

        // Events
        public event Action<int> OnCoinsChanged;
        public event Action<int> OnGemsChanged;

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Don't fetch here - wait for PlayFab login to complete first
            // FetchCurrencies will be called from PlayFabEOSLogin after successful login
        }

        #endregion

        #region Fetch Currencies

        public void FetchCurrencies()
        {
            var request = new GetUserInventoryRequest();

            PlayFabClientAPI.GetUserInventory(request, result =>
            {
                // Update local balances
                if (result.VirtualCurrency.ContainsKey(COINS_CODE))
                {
                    currentCoins = result.VirtualCurrency[COINS_CODE];
                    OnCoinsChanged?.Invoke(currentCoins);
                }

                if (result.VirtualCurrency.ContainsKey(GEMS_CODE))
                {
                    currentGems = result.VirtualCurrency[GEMS_CODE];
                    OnGemsChanged?.Invoke(currentGems);
                }

                Debug.Log($"[CurrencyManager] Fetched: {currentCoins} Coins, {currentGems} Gems");

            }, error =>
            {
                Debug.LogError($"[CurrencyManager] Fetch failed: {error.ErrorMessage}");
            });
        }

        #endregion

        #region Add Currency

        public void AddCoins(int amount, Action onSuccess = null, Action<string> onError = null)
        {
            var request = new AddUserVirtualCurrencyRequest
            {
                VirtualCurrency = COINS_CODE,
                Amount = amount
            };

            PlayFabClientAPI.AddUserVirtualCurrency(request, result =>
            {
                currentCoins = result.Balance;
                OnCoinsChanged?.Invoke(currentCoins);
                Debug.Log($"[CurrencyManager] Added {amount} coins. New balance: {currentCoins}");
                onSuccess?.Invoke();

            }, error =>
            {
                Debug.LogError($"[CurrencyManager] Add coins failed: {error.ErrorMessage}");
                onError?.Invoke(error.ErrorMessage);
            });
        }

        public void AddGems(int amount, Action onSuccess = null, Action<string> onError = null)
        {
            var request = new AddUserVirtualCurrencyRequest
            {
                VirtualCurrency = GEMS_CODE,
                Amount = amount
            };

            PlayFabClientAPI.AddUserVirtualCurrency(request, result =>
            {
                currentGems = result.Balance;
                OnGemsChanged?.Invoke(currentGems);
                Debug.Log($"[CurrencyManager] Added {amount} gems. New balance: {currentGems}");
                onSuccess?.Invoke();

            }, error =>
            {
                Debug.LogError($"[CurrencyManager] Add gems failed: {error.ErrorMessage}");
                onError?.Invoke(error.ErrorMessage);
            });
        }

        #endregion

        #region Deduct Currency

        public void DeductCoins(int amount, Action onSuccess = null, Action<string> onError = null)
        {
            // Check balance first
            if (currentCoins < amount)
            {
                Debug.LogWarning($"[CurrencyManager] Insufficient coins! Need {amount}, have {currentCoins}");
                onError?.Invoke("Insufficient coins");
                return;
            }

            var request = new SubtractUserVirtualCurrencyRequest
            {
                VirtualCurrency = COINS_CODE,
                Amount = amount
            };

            PlayFabClientAPI.SubtractUserVirtualCurrency(request, result =>
            {
                currentCoins = result.Balance;
                OnCoinsChanged?.Invoke(currentCoins);
                Debug.Log($"[CurrencyManager] Deducted {amount} coins. New balance: {currentCoins}");
                onSuccess?.Invoke();

            }, error =>
            {
                Debug.LogError($"[CurrencyManager] Deduct coins failed: {error.ErrorMessage}");
                onError?.Invoke(error.ErrorMessage);
            });
        }

        public void DeductGems(int amount, Action onSuccess = null, Action<string> onError = null)
        {
            if (currentGems < amount)
            {
                Debug.LogWarning($"[CurrencyManager] Insufficient gems! Need {amount}, have {currentGems}");
                onError?.Invoke("Insufficient gems");
                return;
            }

            var request = new SubtractUserVirtualCurrencyRequest
            {
                VirtualCurrency = GEMS_CODE,
                Amount = amount
            };

            PlayFabClientAPI.SubtractUserVirtualCurrency(request, result =>
            {
                currentGems = result.Balance;
                OnGemsChanged?.Invoke(currentGems);
                Debug.Log($"[CurrencyManager] Deducted {amount} gems. New balance: {currentGems}");
                onSuccess?.Invoke();

            }, error =>
            {
                Debug.LogError($"[CurrencyManager] Deduct gems failed: {error.ErrorMessage}");
                onError?.Invoke(error.ErrorMessage);
            });
        }

        #endregion

        #region Public Getters

        public int GetCoins() => currentCoins;
        public int GetGems() => currentGems;

        public bool HasEnoughCoins(int amount) => currentCoins >= amount;
        public bool HasEnoughGems(int amount) => currentGems >= amount;

        #endregion
    }
}
