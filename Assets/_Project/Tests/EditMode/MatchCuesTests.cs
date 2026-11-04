using System.Linq;
using MafiaNight.MatchRules;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    public class MatchCuesTests
    {
        [Test]
        public void EveryCueHasAnIdAnAudibleKeyAndALineOfText()
        {
            foreach (var c in CueCatalog.All)
            {
                Assert.IsNotEmpty(c.Id);
                Assert.Greater(c.Duration, 0f, c.Id);
                var voice = c.Steps.FirstOrDefault(s => s.Kind == CueStepKind.Voice);
                Assert.IsNotNull(voice, c.Id + ": no place for the voice-over");
                Assert.AreEqual(CueCatalog.VoiceKey("ar", c.Id), voice.Value);
                foreach (var s in c.Steps) Assert.LessOrEqual(s.At, c.Duration, c.Id + ": a step starts after the cue ends");
                if (!c.Id.StartsWith("card.")) Assert.IsNotEmpty(c.Subtitle, c.Id + ": without audio the cue would be silent");      // a card flip is an animation only
            }
            Assert.AreEqual(CueCatalog.All.Count, CueCatalog.All.Select(c => c.Id).Distinct().Count(), "an id is used twice");
        }

        [Test]
        public void ARoleCallAndTheIdleLineLastTheSame_SoNobodyCanTellWhoIsAwake()
        {
            var idle = CueCatalog.Find(CueIds.NightIdle);
            foreach (var role in new[] { Role.Mafia, Role.Doctor, Role.Sheriff })
            {
                var call = CueCatalog.Find(CueIds.Call(role));
                Assert.IsNotNull(call, role + " has no call");
                Assert.AreEqual(idle.Duration, call.Duration, role + ": the length of the call would tell who is awake");
                Assert.IsNotNull(CueCatalog.Find(CueIds.Sleep(role)), role + " has no 'goes to sleep' line");
            }
        }

        [Test]
        public void TheCallGoesOnForADeadRolesTownToo_ButOnlyTheLivingOwnerHearsIt()
        {
            var mine = CueCatalog.NightCallFor(Role.Doctor, Role.Doctor, true);
            Assert.AreEqual(CueIds.Call(Role.Doctor), mine.Id);
            Assert.AreEqual(CueIds.NightIdle, CueCatalog.NightCallFor(Role.Doctor, Role.Doctor, false).Id, "a dead doctor hears the idle line, as everybody else");
            Assert.AreEqual(CueIds.NightIdle, CueCatalog.NightCallFor(Role.Doctor, Role.Civilian, true).Id);
            Assert.AreEqual(CueIds.NightIdle, CueCatalog.NightCallFor(Role.Doctor, null, false).Id, "a spectator");
            Assert.AreEqual(CueCatalog.NightCallFor(Role.Doctor, Role.Doctor, true).Duration, CueCatalog.NightCallFor(Role.Doctor, null, false).Duration);
        }

        [Test]
        public void EveryRoleHasACardCueAndEveryStateAnId()
        {
            foreach (Role r in System.Enum.GetValues(typeof(Role))) Assert.IsTrue(CueCatalog.Has(CueIds.CardReveal(r)), r + ": no card cue");
            foreach (var id in new[] { CueIds.MatchStart, CueIds.NightFalls, CueIds.DawnQuiet, CueIds.DawnKill, CueIds.DawnSaved, CueIds.DayStart, CueIds.VoteStart, CueIds.VerdictMafia, CueIds.VerdictTown, CueIds.VerdictNobody, CueIds.OverTown, CueIds.OverMafia })
                Assert.IsTrue(CueCatalog.Has(id), id);
            Assert.IsNull(CueCatalog.Find("a.state.nobody.wrote.yet"), "an unknown state is silent, not an error");
        }

        [Test]
        public void ANameIsPutIntoTheLine()
        {
            Assert.AreEqual("طلع الصبح. هالليلة… قامت المافيا بقتل نضال.", CueCatalog.Format(CueCatalog.Find(CueIds.DawnKill), "نضال"));
            Assert.AreEqual("", CueCatalog.Format(null, "x"));
        }
    }
}
