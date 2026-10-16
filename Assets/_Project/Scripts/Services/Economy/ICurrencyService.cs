using System;

namespace MafiaNight.Services.Economy
{
    /// <summary>
    /// Player wallet (Coins / Gems) backed by PlayFab virtual currency.
    /// </summary>
    public interface ICurrencyService
    {
        int Coins { get; }
        int Gems { get; }

        event Action<int> OnCoinsChanged;
        event Action<int> OnGemsChanged;

        /// <param name="onComplete">Called with true on success, false on failure.</param>
        void FetchCurrencies(Action<bool> onComplete = null);

        // TODO(security): Add* must move to CloudScript/Azure Functions - clients must not mint currency.
        void AddCoins(int amount, Action onSuccess = null, Action<string> onError = null);
        void AddGems(int amount, Action onSuccess = null, Action<string> onError = null);
        void DeductCoins(int amount, Action onSuccess = null, Action<string> onError = null);
        void DeductGems(int amount, Action onSuccess = null, Action<string> onError = null);

        bool HasEnoughCoins(int amount);
        bool HasEnoughGems(int amount);
    }
}
