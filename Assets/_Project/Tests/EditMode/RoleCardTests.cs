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
