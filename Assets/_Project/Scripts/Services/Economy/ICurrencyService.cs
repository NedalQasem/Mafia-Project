using System;
using System.Collections.Generic;

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

        /// <summary>Ids of the catalog items the player owns (PlayFab inventory). Filled by FetchCurrencies and by purchases.</summary>
        IReadOnlyCollection<string> OwnedItemIds { get; }
        event Action OnInventoryChanged;

        /// <summary>
        /// Buys a PlayFab catalog item with Coins. PlayFab checks the price and the balance on the server; only on success
        /// the item joins <see cref="OwnedItemIds"/> and the balance drops. <paramref name="storeId"/> is for offers with their own price.
        /// onError gets (code, message); code is "not_in_catalog" when the title has no such item yet, "funds" when the balance is short.
        /// </summary>
        void PurchaseCatalogItem(string itemId, int price, string storeId, Action onSuccess, Action<string, string> onError, bool withGems = false);

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
