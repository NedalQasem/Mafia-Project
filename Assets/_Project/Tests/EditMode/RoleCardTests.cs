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
