using System;
using MafiaNight.Data.Store;

namespace MafiaNight.Services.Store
{
    /// <summary>
    /// What the player owns and wears from the store catalog, and how items are bought.
    /// Ownership comes from the PlayFab inventory (server truth); only the editor / development builds may fall back to
    /// a local record when the item is not uploaded to the PlayFab catalog yet.
    /// </summary>
    public interface IStoreService
    {
        StoreCatalog Catalog { get; }

        bool Owns(string itemId);
        string GetEquipped(string categoryId);
        bool IsEquipped(string itemId);

        /// <param name="price">What to charge: the item price, or the offer price of an ad.</param>
        /// <param name="done">(success, Arabic message)</param>
        void Purchase(string itemId, int price, string storeId, Action<bool, string> done);

        /// <summary>Wears an owned item (replaces the one worn in its category).</summary>
        bool Equip(string itemId);
        /// <summary>Takes off whatever is worn in the category.</summary>
        void Unequip(string categoryId);

        /// <summary>Fires after a purchase or an equip change.</summary>
        event Action OnChanged;
    }
}
