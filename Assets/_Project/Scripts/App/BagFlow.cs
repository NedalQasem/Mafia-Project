using System.Collections.Generic;
using MafiaNight.Core;
using MafiaNight.Services.Profile;
using MafiaNight.Services.Scenes;
using MafiaNight.Services.Store;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Fills the bag (KitBagScreen) with what the player owns from the store catalog: only the kinds that are worn and stay
    /// (gifts and coin packs are used up), in the mockup's order. Wearing and taking off go through the store service, which
    /// saves them on the profile. Added by <see cref="KitAppBinder"/> in the bag scene.
    /// </summary>
    public sealed class BagFlow : MonoBehaviour
    {
        // the kinds in the bag and the line under each title (what the item changes)
        private static readonly string[] Order = { "frames", "titles", "room_bg", "chat_bg", "bubbles", "entry", "card_frames", "card_bgs", "card_motions", "card_backs" };
        private static readonly Dictionary<string, string> Hints = new Dictionary<string, string>
        {
            { "frames", "حول صورتك بكل غرفة" }, { "titles", "تحت اسمك" }, { "room_bg", "لما تفتح غرفة" }, { "chat_bg", "وراء رسايلك" },
            { "bubbles", "شكل رسايلك" }, { "entry", "لما تدخل غرفة" }, { "card_frames", "حول كرت دورك" }, { "card_bgs", "خلف صورة الدور" }, { "card_motions", "فوق كرت دورك" }, { "card_backs", "لما يكون دورك سري" },
        };

        private KitBagScreen screen;
        private IStoreService store;
        private IPlayerProfileService profile;
        private ISceneLoader scenes;

        private void Start()
        {
            screen = FindAnyObjectByType<KitBagScreen>(FindObjectsInactive.Include);
            if (screen == null || !ServiceLocator.TryGet(out store)) { Destroy(this); return; }
            ServiceLocator.TryGet(out profile);
            ServiceLocator.TryGet(out scenes);
            screen.OnWear += it => store.Equip(it.id);
            screen.OnTakeOff += cat => store.Unequip(cat);
            screen.OnStore += () => { if (scenes != null) scenes.LoadScene(AppScenes.Store); };
            store.OnChanged += Refresh;
            if (profile != null) profile.OnProfileLoaded += OnProfile;      // the bag can open before the profile arrives (what is worn lives there)
            Refresh();
        }

        private void OnDestroy()
        {
            if (store != null) store.OnChanged -= Refresh;
            if (profile != null) profile.OnProfileLoaded -= OnProfile;
        }

        private void OnProfile(MafiaNight.Data.Player.PlayerProfile _) => Refresh();

        private void Refresh()
        {
            var catalog = store.Catalog;
            if (catalog == null) return;
            var kinds = new List<KitBagScreen.Kind>();
            var owned = new List<StoreItem>();
            bool bought = false;
            foreach (var id in Order)
            {
                var cat = catalog.GetCategory(id);
                if (cat == null || cat.hidden || !cat.equippable) continue;
                kinds.Add(new KitBagScreen.Kind { id = id, name = cat.tabName, hint = Hints.TryGetValue(id, out var h) ? h : cat.hint });
                foreach (var it in StoreData.Items(id))
                {
                    if (!store.Owns(it.id)) continue;
                    it.owned = true; it.equipped = store.IsEquipped(it.id);
                    owned.Add(it);
                    var def = catalog.GetItem(it.id);
                    if (def != null && !def.ownedByDefault) bought = true;
                    if (def != null && def.ownedByDefault) it.price = 0;      // the sheet says "yours from the start", not a price
                }
            }
            string name = profile?.LocalProfile != null && !string.IsNullOrEmpty(profile.LocalProfile.DisplayName) ? profile.LocalProfile.DisplayName : "لاعب";
            screen.Show(name, kinds, owned, !bought);
        }
    }
}
