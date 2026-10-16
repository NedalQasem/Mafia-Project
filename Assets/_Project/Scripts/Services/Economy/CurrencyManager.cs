using System;
using MafiaNight.Core;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace MafiaNight.Services.Economy
{
    /// <summary>
    /// Manages player currencies (Coins and Gems) via PlayFab virtual currency.
    /// </summary>
    public class CurrencyManager : PersistentService<ICurrencyService>, ICurrencyService
    {
        private const string Tag = "Currency";

        // Currency codes (must match PlayFab Virtual Currency setup)
        private const string COINS_CODE = "CO";
        private const string GEMS_CODE = "GM";

        [Header("Current Balances (read-only)")]
        [SerializeField] private int currentCoins = 0;
        [SerializeField] private int currentGems = 0;

        public int Coins => currentCoins;
        public int Gems => currentGems;

        public event Action<int> OnCoinsChanged;
        public event Action<int> OnGemsChanged;

        public bool HasEnoughCoins(int amount) => currentCoins >= amount;
        public bool HasEnoughGems(int amount) => currentGems >= amount;

        #region Fetch

        public void FetchCurrencies(Action<bool> onComplete = null)
        {
            PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(), result =>
            {
                if (result.VirtualCurrency.TryGetValue(COINS_CODE, out int coins)) SetCoins(coins);
                if (result.VirtualCurrency.TryGetValue(GEMS_CODE, out int gems)) SetGems(gems);

                Log.Info($"Fetched: {currentCoins} Coins, {currentGems} Gems", Tag);
                onComplete?.Invoke(true);
            }, error =>
            {
                Log.Error($"Fetch failed: {error.ErrorMessage}", Tag);
                onComplete?.Invoke(false);
            });
        }

        #endregion

        #region Add / Deduct

        public void AddCoins(int amount, Action onSuccess = null, Action<string> onError = null) =>
            AddCurrency(COINS_CODE, amount, SetCoins, onSuccess, onError);

        public void AddGems(int amount, Action onSuccess = null, Action<string> onError = null) =>
            AddCurrency(GEMS_CODE, amount, SetGems, onSuccess, onError);

        public void DeductCoins(int amount, Action onSuccess = null, Action<string> onError = null)
        {
            if (!HasEnoughCoins(amount))
            {
                Log.Warning($"Insufficient coins! Need {amount}, have {currentCoins}", Tag);
                onError?.Invoke("Insufficient coins");
                return;
            }
            SubtractCurrency(COINS_CODE, amount, SetCoins, onSuccess, onError);
        }

        public void DeductGems(int amount, Action onSuccess = null, Action<string> onError = null)
        {
            if (!HasEnoughGems(amount))
            {
                Log.Warning($"Insufficient gems! Need {amount}, have {currentGems}", Tag);
                onError?.Invoke("Insufficient gems");
                return;
            }
            SubtractCurrency(GEMS_CODE, amount, SetGems, onSuccess, onError);
        }

        // TODO(security): AddUserVirtualCurrency must be disabled for clients in PlayFab and replaced by CloudScript.
        private void AddCurrency(string code, int amount, Action<int> setBalance, Action onSuccess, Action<string> onError)
        {
            var request = new AddUserVirtualCurrencyRequest { VirtualCurrency = code, Amount = amount };
            PlayFabClientAPI.AddUserVirtualCurrency(request, result =>
            {
                setBalance(result.Balance);
                Log.Info($"Added {amount} {code}. New balance: {result.Balance}", Tag);
                onSuccess?.Invoke();
            }, error =>
            {
                Log.Error($"Add {code} failed: {error.ErrorMessage}", Tag);
                onError?.Invoke(error.ErrorMessage);
            });
        }

        private void SubtractCurrency(string code, int amount, Action<int> setBalance, Action onSuccess, Action<string> onError)
        {
            var request = new SubtractUserVirtualCurrencyRequest { VirtualCurrency = code, Amount = amount };
            PlayFabClientAPI.SubtractUserVirtualCurrency(request, result =>
            {
                setBalance(result.Balance);
                Log.Info($"Deducted {amount} {code}. New balance: {result.Balance}", Tag);
                onSuccess?.Invoke();
            }, error =>
            {
                Log.Error($"Deduct {code} failed: {error.ErrorMessage}", Tag);
                onError?.Invoke(error.ErrorMessage);
            });
        }

        #endregion

        private void SetCoins(int value)
        {
            currentCoins = value;
            OnCoinsChanged?.Invoke(currentCoins);
        }

        private void SetGems(int value)
        {
            currentGems = value;
            OnGemsChanged?.Invoke(currentGems);
        }
    }
}
