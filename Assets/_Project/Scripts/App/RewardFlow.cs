using System;
using System.Collections.Generic;
using MafiaNight.Core;
using MafiaNight.Data.Player;
using MafiaNight.Data.Store;
using MafiaNight.Services.Economy;
using MafiaNight.Services.Store;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Shows "شنطة الغنيمة" (KitRewardPopup) for whatever the player just received: a telegram, all telegrams, an event prize.
    /// The wallet is asked again when he presses "استلم" (so the pills count up from the old balance), and "البسه هلأ" wears the frame.
    /// </summary>
    public static class RewardFlow
    {
        /// <summary>The loot of a package: coins, gems and store items (by id). Empty when there is nothing to show.</summary>
        public static List<RewardItem> FromPack(int coins, int gems, IList<string> itemIds)
        {
            var list = new List<RewardItem>();
            if (coins > 0) list.Add(new RewardItem { kind = "coins", amount = coins, rarity = coins >= 10000 ? RewardRarity.Legend : coins >= 3000 ? RewardRarity.Rare : RewardRarity.Common });
            if (gems > 0) list.Add(new RewardItem { kind = "gems", amount = gems, rarity = gems >= 100 ? RewardRarity.Legend : RewardRarity.Rare });
            var catalog = StoreData.Catalog;
            if (itemIds != null)
                foreach (var id in itemIds)
                {
                    var def = catalog != null ? catalog.GetItem(id) : null;
                    var cat = def != null && catalog != null ? catalog.GetCategory(def.categoryId) : null;
                    list.Add(new RewardItem
                    {
                        id = id,
                        kind = def == null ? "item" : def.artKind == StoreArtKind.Frame ? "frame" : def.artKind == StoreArtKind.ChatBubble ? "bubble" : "item",
                        name = def != null && !string.IsNullOrEmpty(def.nameAr) ? def.nameAr : id,
                        sub = cat != null ? cat.tabName : "",
                        rarity = def == null || def.rarity == ItemRarity.Common ? RewardRarity.Common : def.rarity == ItemRarity.Rare ? RewardRarity.Rare : RewardRarity.Legend,
                        art = def != null ? def.sprite : null,
                    });
                }
            return list;
        }

        /// <summary>Opens the briefcase over the current screen; false when it cannot (no prefab, no canvas, nothing to give).</summary>
        public static bool Show(string kicker, string title, List<RewardItem> items, Action closed = null)
        {
            var th = KitTheme.Current;
            var canvas = InviteFlow.FindCanvas();
            if (items == null || items.Count == 0 || th == null || th.rewardPopupPrefab == null || canvas == null) return false;
            ServiceLocator.TryGet(out ICurrencyService wallet);
            var go = UnityEngine.Object.Instantiate(th.rewardPopupPrefab, canvas, false);
            var pop = go.GetComponent<KitRewardPopup>();
            pop.OnCollect += wear =>
            {
                string frame = null;
                if (wear) foreach (var it in items) if (it.kind == "frame" && !string.IsNullOrEmpty(it.id)) frame = it.id;
                if (wallet == null) return;
                // the server already paid: the wallet and the inventory are read again, then the frame can be worn (it must be owned first)
                wallet.FetchCurrencies(_ => { if (frame != null && ServiceLocator.TryGet(out IStoreService store)) store.Equip(frame); });
            };
            pop.OnClosed += () => { UnityEngine.Object.Destroy(go); closed?.Invoke(); };
            pop.Show(kicker, title, items, wallet != null ? wallet.Coins : 0, wallet != null ? wallet.Gems : 0);
            return true;
        }
    }
}
