using System;
using MafiaNight.Core;
using MafiaNight.Data.Player;
using MafiaNight.Data.Store;
using MafiaNight.Services.Economy;
using MafiaNight.Services.Profile;
using UnityEngine;

namespace MafiaNight.Services.Store
{
    public sealed class StoreService : IStoreService
    {
        private const string Tag = "Store";

        private IPlayerProfileService profile;
        private ICurrencyService currency;

        public event Action OnChanged;

        public StoreCatalog Catalog => StoreCatalog.Current;

        public StoreService()
        {
            // Wallet and inventory changes come from the currency service; forward them so the UI refreshes once.
            if (ServiceLocator.TryGet(out ICurrencyService c)) c.OnInventoryChanged += () => OnChanged?.Invoke();
        }

        private bool Resolve()
        {
            if (profile == null) ServiceLocator.TryGet(out profile);
            if (currency == null) ServiceLocator.TryGet(out currency);
            return profile != null && currency != null;
        }

        public bool Owns(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            var def = Catalog != null ? Catalog.GetItem(itemId) : null;
            if (def != null && def.ownedByDefault) return true;
            var cat = def != null ? Catalog.GetCategory(def.categoryId) : null;
            if (cat != null && !cat.equippable) return false;   // gifts and packs are used up
            if (!Resolve()) return false;
            if (currency.OwnedItemIds != null)
                foreach (var id in currency.OwnedItemIds) if (id == itemId) return true;
            var inv = profile.LocalProfile?.Inventory;
            return inv != null && inv.OwnsStoreItem(itemId);   // local record: development fallback only (see Purchase)
        }

        public string GetEquipped(string categoryId)
        {
            string worn = Resolve() ? profile.LocalProfile?.Customization?.GetEquipped(categoryId) : null;
            if (!string.IsNullOrEmpty(worn) || Catalog == null || categoryId == null || !categoryId.StartsWith("card_")) return worn;
            foreach (var it in Catalog.ItemsIn(categoryId)) if (it.ownedByDefault) return it.id;       // the role card shelves wear their free item until the player picks another
            return null;
        }

        public bool IsEquipped(string itemId)
        {
            var def = Catalog != null ? Catalog.GetItem(itemId) : null;
            return def != null && GetEquipped(def.categoryId) == itemId;
        }

        public void Purchase(string itemId, int price, string storeId, Action<bool, string> done)
        {
            var def = Catalog != null ? Catalog.GetItem(itemId) : null;
            if (def == null || !Resolve()) { done(false, "تعذّر الشراء الآن."); return; }
            var p = profile.LocalProfile;
            if (p == null) { done(false, "تعذّر تحميل ملفك. حاول لاحقاً."); return; }
            if (Owns(itemId)) { done(true, "تملكه مسبقاً"); return; }
            if (def.lockRank > RankLadder.FromPoints(p.RankPoints).RankIndex + 1) { done(false, "هذا الغرض مقفل لرتبتك."); return; }
            if (def.IsPack) { done(false, "الشراء بالمال الحقيقي غير متاح بعد."); return; }
            bool gems = def.IsGem;
            if (gems) price = def.gemPrice;                                   // an epic card item costs Gems; the caller's coin price does not apply
            if (gems ? !currency.HasEnoughGems(price) : !currency.HasEnoughCoins(price)) { done(false, gems ? "رصيدك من الماس لا يكفي." : "رصيدك لا يكفي."); return; }

            currency.PurchaseCatalogItem(itemId, price, storeId, () =>
            {
                done(true, "أُضيف إلى مقتنياتك");
                OnChanged?.Invoke();
            }, (code, message) =>
            {
                if (code == "not_in_catalog" && (Application.isEditor || Debug.isDebugBuild))
                {
                    // The title's PlayFab catalog does not have this item yet: dev builds charge the wallet and keep a local record.
                    Log.Warning($"{itemId} is not in the PlayFab catalog - local development purchase", Tag);
                    LocalPurchase(def, price, p, done, gems);
                    return;
                }
                done(false, code == "funds" ? (gems ? "رصيدك من الماس لا يكفي." : "رصيدك لا يكفي.") : code == "not_in_catalog" ? "هذا الغرض غير متاح حالياً." : "تعذّر الشراء، حاول لاحقاً.");
            }, gems);
        }

        private void LocalPurchase(StoreItemDef def, int price, PlayerProfile p, Action<bool, string> done, bool gems = false)
        {
            Action<int, Action, Action<string>> charge = gems ? (Action<int, Action, Action<string>>)currency.DeductGems : currency.DeductCoins;
            charge(price, () =>
            {
                if (p.Inventory == null) p.Inventory = new PlayerInventory();
                var cat = Catalog.GetCategory(def.categoryId);
                if (cat != null && !cat.equippable) p.Inventory.AddGift(def.id); else p.Inventory.AddStoreItem(def.id);
                profile.SaveProfile();
                done(true, "أُضيف إلى مقتنياتك");
                OnChanged?.Invoke();
            }, err => done(false, "تعذّر الشراء، حاول لاحقاً."));
        }

        public bool Equip(string itemId)
        {
            var def = Catalog != null ? Catalog.GetItem(itemId) : null;
            if (def == null || !Resolve() || !Owns(itemId)) return false;
            var cat = Catalog.GetCategory(def.categoryId);
            if (cat == null || !cat.equippable) return false;
            profile.Equip(def.categoryId, itemId);
            OnChanged?.Invoke();
            return true;
        }

        public void Unequip(string categoryId)
        {
            if (!Resolve()) return;
            profile.Equip(categoryId, null);
            OnChanged?.Invoke();
        }
    }
}
