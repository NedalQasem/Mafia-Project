using System;
using System.Collections.Generic;
using MafiaNight.Data.Player;
using UnityEngine;

namespace MafiaNight.Data.Store
{
    /// <summary>What kind of picture an item is; it picks the renderer. A Sprite on the item overrides the built-in drawing.</summary>
    public enum StoreArtKind
    {
        Frame,          // ring around the avatar
        RoomThumb,      // room background (preview)
        ChatBackground, // chat background (preview)
        ChatBubble,     // message bubble look
        EntryStrip,     // "X entered the room" strip
        Gift,           // gift object
        CardBack,       // back of the playing cards
        Title,          // title plate under the name
        CoinPack,       // real-money coin pack
        CardFrame,      // the frame of a role card (what the player wears; see CardLayers)
        CardBackground, // the background of a role card
        CardMotion,     // the motion over a role card (shine, embers, smoke)
        // CardBack (above) is also the back of a role card now: the one everybody sees while a role is secret
    }

    public enum StoreCategoryKind
    {
        Items,      // shelves of the items that belong to this category
        RankSets,   // the rank-exclusive cards (see RankSetDef)
    }

    /// <summary>One thing the store sells or hands out. <see cref="id"/> is also the PlayFab catalog ItemId.</summary>
    [Serializable]
    public class StoreItemDef
    {
        [Tooltip("Stable id, e.g. frame_ember. Never change it after release: it is the PlayFab ItemId and what players own.")]
        public string id;
        public string categoryId;
        public string nameAr;
        [TextArea(1, 3)] public string descAr;

        [Header("Price & access")]
        [Tooltip("Price in Coins. 0 + Owned By Default = everyone has it from the start.")]
        public int price;
        public ItemRarity rarity;
        [Tooltip("Rank number (1-8) needed to buy it. 0 = no rank needed.")]
        public int lockRank;
        public bool isNew;
        public bool ownedByDefault;

        [Header("Look")]
        public StoreArtKind artKind;
        [Tooltip("Built-in drawing to use when there is no Sprite (frame number 1-6, bubble number...).")]
        public int variant;
        [Tooltip("Optional picture. When set it replaces the built-in drawing.")]
        public Sprite sprite;
        public Color tint = Color.white;
        [Tooltip("Text colour for titles, bubbles and entry strips.")]
        public Color textColor = Color.white;
        [Tooltip("Title plates: the words shown under the player's name.")]
        public string titleText;

        [Header("Role cards")]
        [Tooltip("Role card layers: the id of the layer this item puts on the card (frame brass/gold/silver/black, background leather/velvet/..., motion none/shine/..., back diamond/lattice/...).")]
        public string layerId;
        [Tooltip("Price in Gems when it is above 0 (the rare, epic ones); the Coin price is then ignored.")]
        public int gemPrice;

        [Header("Coin packs (real money)")]
        [Tooltip("Shown instead of a coin price, e.g. \"٠٫٩٩ $\".")]
        public string priceText;
        [Tooltip("Store product id (Google Play).")]
        public string productId;
        public int coinsGranted;

        public bool IsPack => artKind == StoreArtKind.CoinPack;
        public bool IsGem => gemPrice > 0;
        /// <summary>One of the four layers of a role card (the store's card shelves).</summary>
        public bool IsCardLayer => artKind == StoreArtKind.CardFrame || artKind == StoreArtKind.CardBackground || artKind == StoreArtKind.CardMotion || artKind == StoreArtKind.CardBack;
    }

    [Serializable]
    public class StoreCategoryDef
    {
        public string id;
        public StoreCategoryKind kind;
        [Tooltip("Text on the tab.")] public string tabName;
        [Tooltip("Bold title above the shelf.")] public string headTitle;
        [Tooltip("Small grey line next to the title.")] public string hint;
        [Tooltip("Index of the built-in tab icon (0 rank, 1 frame, 2 room, 3 chat bg, 4 bubble, 5 entry, 6 gift, 7 table, 8 title, 9 coin).")]
        public int builtInIcon;
        [Tooltip("Optional picture for the tab icon.")] public Sprite icon;
        [Tooltip("Items per shelf row.")] public int perRow = 3;
        [Tooltip("Does equipping replace the previous item of this category? (frames, titles, bubbles...). Off for gifts and packs.")]
        public bool equippable = true;
        public bool hidden;
    }

    /// <summary>A page of the rotating newspaper at the top of the store.</summary>
    [Serializable]
    public class StoreAdDef
    {
        public string id;
        public bool enabled = true;
        public string mast;
        [TextArea(1, 2)] public string headline;
        public string body;
        public string stamp;
        [Tooltip("Item shown and sold. Its price (or price text) is on the button.")]
        public string itemId;
        [Tooltip("Seconds left of the offer (0 = no countdown).")]
        public int countdownSeconds;
        [Tooltip("Offer price in Coins (0 = the normal price of the item). In PlayFab the offer lives in a Store with this price.")]
        public int offerPrice;
        [Tooltip("PlayFab StoreId that holds the offer price.")]
        public string offerStoreId = "offers";
    }

    [Serializable]
    public class RankSetDef
    {
        public string rankNameAr;
        public Color m1 = Color.white, m2 = Color.gray, e = Color.white;
        [Tooltip("Names of the 5 exclusive items: frame, room, chat, entry, title.")]
        public string[] itemNames = new string[5];
    }

    /// <summary>
    /// Everything the store shows, as data: categories (tabs), items, ads and rank sets. Edit it in the Inspector or replace
    /// it from PlayFab later; the store builds itself from it. Lives at Resources/Config/StoreCatalog.
    /// </summary>
    [CreateAssetMenu(fileName = "StoreCatalog", menuName = "Mafia Night/Store Catalog")]
    public sealed class StoreCatalog : ScriptableObject
    {
        public List<StoreCategoryDef> categories = new List<StoreCategoryDef>();
        public List<StoreItemDef> items = new List<StoreItemDef>();
        public List<StoreAdDef> ads = new List<StoreAdDef>();
        public List<RankSetDef> rankSets = new List<RankSetDef>();

        private Dictionary<string, StoreItemDef> byId;

        public StoreItemDef GetItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (byId == null || byId.Count != items.Count)
            {
                byId = new Dictionary<string, StoreItemDef>(items.Count);
                foreach (var it in items) if (it != null && !string.IsNullOrEmpty(it.id)) byId[it.id] = it;
            }
            byId.TryGetValue(id, out var def);
            return def;
        }

        public StoreCategoryDef GetCategory(string id)
        {
            foreach (var c in categories) if (c.id == id) return c;
            return null;
        }

        public List<StoreItemDef> ItemsIn(string categoryId)
        {
            var list = new List<StoreItemDef>();
            foreach (var it in items) if (it != null && it.categoryId == categoryId) list.Add(it);
            return list;
        }

        private void OnEnable() { byId = null; }

        private static StoreCatalog cached;

        /// <summary>The catalog asset (null when it was not created yet).</summary>
        public static StoreCatalog Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<StoreCatalog>("Config/StoreCatalog");
                return cached;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;
    }
}
