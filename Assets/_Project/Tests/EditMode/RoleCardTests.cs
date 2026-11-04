using System.Linq;
using MafiaNight.Data.Cards;
using MafiaNight.MatchRules;
using MafiaNight.UI.Kit;
using NUnit.Framework;
using UnityEngine;

namespace MafiaNight.Tests
{
    public class RoleCardTests
    {
        private static string CardId(Role r)
        {
            switch (r) { case Role.Sheriff: return "sheikh"; case Role.Civilian: return "civilian"; default: return r.ToString().ToLowerInvariant(); }
        }

        [Test]
        public void EveryRoleOfTheMatchHasACard_WithItsTextAndDanger()
        {
            foreach (Role r in System.Enum.GetValues(typeof(Role)))
            {
                var d = RoleCards.Find(CardId(r));
                Assert.IsNotNull(d, r + " has no card");
                Assert.IsNotEmpty(d.Name); Assert.IsNotEmpty(d.English); Assert.IsNotEmpty(d.Win);
                Assert.GreaterOrEqual(d.Lines.Length, 2, r + ": the back of the card says what the role does");
                Assert.That(d.Danger, Is.InRange(1, 4));
            }
            Assert.AreEqual(RoleCards.All.Length, RoleCards.All.Select(d => d.Id).Distinct().Count(), "an id is used twice");
        }

        // ------------------------------------------------------------------ the store's card shelves

        private static readonly (string shelf, MafiaNight.Data.Store.StoreArtKind art, string[] layers)[] Shelves =
        {
            (CardWear.Frames, MafiaNight.Data.Store.StoreArtKind.CardFrame, CardStyles.Frames),
            (CardWear.Backgrounds, MafiaNight.Data.Store.StoreArtKind.CardBackground, CardStyles.Backgrounds),
            (CardWear.Motions, MafiaNight.Data.Store.StoreArtKind.CardMotion, CardStyles.Motions),
            (CardWear.Backs, MafiaNight.Data.Store.StoreArtKind.CardBack, CardStyles.Backs),
        };

        [Test]
        public void EveryLayerOfACardIsSoldOnItsShelf_TheFirstIsFree_AndTheEpicOnesCostGems()
        {
            var catalog = MafiaNight.Data.Store.StoreCatalog.Current;
            Assert.IsNotNull(catalog, "the store catalog asset is missing");
            foreach (var (shelf, art, layers) in Shelves)
            {
                var cat = catalog.GetCategory(shelf);
                Assert.IsNotNull(cat, shelf + ": no tab for this shelf"); Assert.IsTrue(cat.equippable, shelf + ": one worn at a time");
                var items = catalog.ItemsIn(shelf);
                foreach (var layer in layers) Assert.IsTrue(items.Any(i => i.layerId == layer), shelf + ": nothing sells the layer " + layer);
                foreach (var it in items)
                {
                    Assert.AreEqual(art, it.artKind, it.id);
                    Assert.Contains(it.layerId, layers, it.id + ": a layer the card does not know");
                    Assert.IsTrue(it.price > 0 ^ it.gemPrice > 0 || it.ownedByDefault, it.id + ": priced in Coins or in Gems, not both, unless it is the free one");
                    if (it.ownedByDefault) Assert.AreEqual(0, it.price + it.gemPrice, it.id + ": the free item has no price");
                }
                Assert.AreEqual(1, items.Count(i => i.ownedByDefault), shelf + ": exactly one free item, worn from the start");
                Assert.IsTrue(items.Single(i => i.ownedByDefault).layerId == layers[0], shelf + ": the free item is the card's default layer");
            }
            Assert.IsNull(catalog.GetCategory("cards"), "the old table-cards shelf was replaced by the card shelves");
        }

        [Test]
        public void WhatAPlayerWearsOnTheShelvesBecomesTheLayersOfHisCard_AndTravelsAsOneString()
        {
            var catalog = MafiaNight.Data.Store.StoreCatalog.Current;
            var none = CardWear.Of(shelf => null, catalog);
            Assert.AreEqual("brass|leather|none|diamond", CardWear.Encode(none), "wearing nothing is the free layers");
            var worn = CardWear.Of(shelf => shelf == CardWear.Frames ? "cframe_gold" : shelf == CardWear.Backs ? "cback_skull" : shelf == CardWear.Backgrounds ? "cbg_fire" : "cmo_embers", catalog);
            Assert.AreEqual("gold|fire|embers|skull", CardWear.Encode(worn));
            var back = CardWear.Decode(CardWear.Encode(worn));
            Assert.AreEqual("gold", back.Frame); Assert.AreEqual("fire", back.Background); Assert.AreEqual("embers", back.Motion); Assert.AreEqual("skull", back.Back);
            Assert.AreEqual("brass|leather|none|diamond", CardWear.Encode(CardWear.Decode("gold|from-the-future|none|diamond|extra")), "a string this client does not understand is the free layers");
            Assert.AreEqual("gold|leather|none|diamond", CardWear.Encode(CardWear.Decode("gold|a-new-background|none|diamond")), "an unknown layer falls back alone");
            Assert.AreEqual("brass|leather|none|diamond", CardWear.Encode(CardWear.Decode(null)));
        }

        [Test]
        public void ARoomMemberCarriesTheCardWear_ForTheTable()
        {
            var m = new MafiaNight.Data.Rooms.RoomMember { CardLayers = "silver|velvet|shine|rose" };
            Assert.AreEqual("rose", CardWear.Decode(m.CardLayers).Back);
            Assert.AreEqual("", new MafiaNight.Data.Rooms.RoomMember().CardLayers, "a member who wears nothing sends nothing");
        }

        [Test]
        public void AnUnknownLayerFallsBackToTheFreeOne_SoAnOldClientNeverBreaksOnANewItem()
        {
            Assert.AreSame(CardStyles.Frame("brass"), CardStyles.Frame("a-frame-from-the-future"));
            Assert.AreSame(CardStyles.Background("leather"), CardStyles.Background(null));
            Assert.IsFalse(CardStyles.IsMotion("something-new"));
            foreach (var f in CardStyles.Frames) Assert.IsNotNull(CardStyles.Frame(f));
            foreach (var b in CardStyles.Backgrounds) Assert.IsNotNull(CardStyles.Background(b));
        }

        [Test]
        public void ACardCostsAHandfulOfObjectsAndLittleMemory()
        {
            var canvas = new GameObject("cardTestCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                long before = KitProc.CachedBytes;
                var layers = new CardLayers();
                int objects = 0;
                foreach (var d in RoleCards.All)
                {
                    var c = KitRoleCard.Create(canvas.transform, d, layers, 240f);
                    objects += c.GetComponentsInChildren<Transform>(true).Length;
                    var back = KitRoleCard.Create(canvas.transform, d, layers, 240f, KitRoleCard.Face.Back);
                    Assert.AreEqual(KitRoleCard.Face.Back, back.Current);
                }
                var hidden = KitRoleCard.CreateHidden(canvas.transform, layers, 240f);
                Assert.AreEqual(KitRoleCard.Face.Hidden, hidden.Current);
                Assert.Less(objects / RoleCards.All.Length, 70, "objects per card (three faces)");
                Assert.Less(KitProc.CachedBytes - before, 16L * 1024 * 1024, "painted sprites of the four cards, both faces and the hidden one");
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void EveryCardBackEmblemPaintsOnceIntoACachedSprite_InTheMetalOfTheFrame()
        {
            foreach (var id in KitEmblem.Ids)
            {
                var a = KitEmblem.Paint(id, new Color32(0xE8, 0xC8, 0x6A, 255));
                Assert.IsNotNull(a, id);
                Assert.AreEqual(288, a.texture.width, id);
                Assert.AreSame(a, KitEmblem.Paint(id, new Color32(0xE8, 0xC8, 0x6A, 255)), id + " is painted again for the same colour");
                Assert.AreNotSame(a, KitEmblem.Paint(id, new Color32(0xE8, 0xEC, 0xF2, 255)), id + " should take the colour of a silver frame");
            }
            foreach (var b in CardStyles.Backs) CollectionAssert.Contains(KitEmblem.Ids, b, "a card back of the store has no emblem painter");
        }

        [Test]
        public void TheVerticalTierTextStaysInsideTheEdge_WhateverItsLength()
        {
            var canvas = new GameObject("cardTestCanvas3", typeof(RectTransform), typeof(Canvas));
            try
            {
                var longDef = new RoleCardDef { Id = "mafia", Name = "x", English = "X", Ttl = "يقتل بالليل ويخدع الجميع في النهار ويغطي على رفقائه كل مرة", Win = "w", Danger = 4, Lines = new[] { "a", "b" } };
                var c = KitRoleCard.Create(canvas.transform, longDef, new CardLayers(), 300f);
                var tier = c.transform.Find("Front/Tier");
                Assert.IsNotNull(tier, "the tier text is written down the edge");
                Assert.AreEqual(-90f, Mathf.DeltaAngle(0f, tier.localEulerAngles.z) , 0.01f);
                var tmp = tier.Find("TierText").GetComponent<TMPro.TMP_Text>();
                tmp.ForceMeshUpdate();
                Assert.LessOrEqual(tmp.textBounds.size.x, ((RectTransform)tier).rect.width + 0.5f, "a long tier must shrink, not run off the portrait");
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void ACardCanBeFlippedOnlyWhenItHasABack_AndAHiddenOneHasNone()
        {
            var canvas = new GameObject("cardTestCanvas2", typeof(RectTransform), typeof(Canvas));
            try
            {
                var c = KitRoleCard.Create(canvas.transform, RoleCards.Find("mafia"), new CardLayers(), 200f);
                c.SetFace(KitRoleCard.Face.Back, false);
                Assert.AreEqual(KitRoleCard.Face.Back, c.Current);
                var h = KitRoleCard.CreateHidden(canvas.transform, new CardLayers(), 200f);
                h.SetFace(KitRoleCard.Face.Front, false);
                Assert.AreEqual(KitRoleCard.Face.Hidden, h.Current, "no role: no front to show");
            }
            finally { Object.DestroyImmediate(canvas); }
        }
    }
}
