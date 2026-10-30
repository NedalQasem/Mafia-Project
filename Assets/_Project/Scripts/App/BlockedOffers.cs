using System;
using System.Collections.Generic;
using System.Globalization;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// The offers the player pressed "block" on (the quick menu's sheet and the banner's pages). A blocked offer stays hidden until it ends:
    /// the end of its countdown, or a week for offers that have none. Kept on the device.
    /// </summary>
    public static class BlockedOffers
    {
        private const string Key = "mafia.quick.blocked";
        private const long NoEnd = 7 * 24 * 3600;

        /// <summary>An offer was blocked (true) or its block was undone (false).</summary>
        public static event Action<string, bool> Changed;

        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static Dictionary<string, long> Read()
        {
            var map = new Dictionary<string, long>();
            string raw = PlayerPrefs.GetString(Key, "");
            long now = Now;
            foreach (var part in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int at = part.LastIndexOf('|');
                if (at <= 0 || !long.TryParse(part.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long until)) continue;
                if (until > now) map[part.Substring(0, at)] = until;           // the ones that ended drop out here
            }
            return map;
        }

        private static void Write(Dictionary<string, long> map)
        {
            var parts = new List<string>();
            foreach (var kv in map) parts.Add(kv.Key + "|" + kv.Value.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(Key, string.Join(";", parts));
            PlayerPrefs.Save();
        }

        public static bool IsBlocked(string adId) => !string.IsNullOrEmpty(adId) && Read().ContainsKey(adId);

        public static void Block(StoreAdCarousel.Ad ad)
        {
            if (ad == null || string.IsNullOrEmpty(ad.id)) return;
            var map = Read();
            map[ad.id] = Now + (ad.countdown && ad.countdownSeconds > 0 ? ad.countdownSeconds : NoEnd);
            Write(map);
            Changed?.Invoke(ad.id, true);
        }

        public static void Unblock(string adId)
        {
            var map = Read();
            if (!map.Remove(adId)) return;
            Write(map);
            Changed?.Invoke(adId, false);
        }

        /// <summary>The offers the player has not blocked.</summary>
        public static List<StoreAdCarousel.Ad> Filter(IEnumerable<StoreAdCarousel.Ad> ads)
        {
            var map = Read();
            var list = new List<StoreAdCarousel.Ad>();
            foreach (var a in ads) if (a != null && !map.ContainsKey(a.id)) list.Add(a);
            return list;
        }
    }
}
