using System;
using MafiaNight.Data.Store;

namespace MafiaNight.Data.Cards
{
    /// <summary>
    /// What a player wears on his role card, worked out from the store items he wears (one item per shelf: frames, backgrounds, motions, backs) and put into one short string that travels
    /// with his room member attributes, so every player sees his card dressed the same. An unknown layer (a new item on an old client) falls back to the free one.
    /// </summary>
    public static class CardWear
    {
        public const string Frames = "card_frames", Backgrounds = "card_bgs", Motions = "card_motions", Backs = "card_backs";

        /// <summary>The layers of the items <paramref name="equippedItemOf"/> says are worn on each shelf (it gets a shelf id and gives an item id or null).</summary>
        public static CardLayers Of(Func<string, string> equippedItemOf, StoreCatalog catalog)
        {
            var l = new CardLayers();
            if (equippedItemOf == null || catalog == null) return l;
            l.Frame = LayerOf(catalog, equippedItemOf(Frames), l.Frame);
            l.Background = LayerOf(catalog, equippedItemOf(Backgrounds), l.Background);
            l.Motion = LayerOf(catalog, equippedItemOf(Motions), l.Motion);
            l.Back = LayerOf(catalog, equippedItemOf(Backs), l.Back);
            return l;
        }

        private static string LayerOf(StoreCatalog catalog, string itemId, string free)
        {
            var d = catalog.GetItem(itemId);
            return d != null && !string.IsNullOrEmpty(d.layerId) ? d.layerId : free;
        }

        /// <summary>"frame|background|motion|back", for example "gold|velvet|none|diamond".</summary>
        public static string Encode(CardLayers l) => l == null ? "" : l.Frame + "|" + l.Background + "|" + l.Motion + "|" + l.Back;

        public static CardLayers Decode(string s)
        {
            var l = new CardLayers();
            if (string.IsNullOrEmpty(s)) return l;
            var p = s.Split('|');
            if (p.Length != 4) return l;
            l.Frame = Known(CardStyles.Frames, p[0], l.Frame);
            l.Background = Known(CardStyles.Backgrounds, p[1], l.Background);
            l.Motion = Known(CardStyles.Motions, p[2], l.Motion);
            l.Back = Known(CardStyles.Backs, p[3], l.Back);
            return l;
        }

        private static string Known(string[] all, string id, string free) => Array.IndexOf(all, id) >= 0 ? id : free;
    }
}
