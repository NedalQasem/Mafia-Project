using System.Linq;
using MafiaNight.MatchRules;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    public class MatchVoiceRulesTests
    {
        private static readonly string[] Mates = { "m1", "m2" };

        [Test]
        public void Everybody_talks_and_hears_everybody_before_and_after_the_match()
        {
            foreach (var phase in new[] { Phase.Lobby, Phase.RoleReveal, Phase.GameOver })
            {
                var p = MatchVoiceRules.For(phase, Role.Civilian, false, Mates);
                Assert.IsTrue(p.MicAllowed, phase.ToString());
                Assert.IsNull(p.HearOnly, phase.ToString());
            }
        }

        [Test]
        public void At_night_the_town_is_silent_and_hears_nothing()
        {
            foreach (var role in new[] { Role.Civilian, Role.Doctor, Role.Sheriff })
            {
                var p = MatchVoiceRules.For(Phase.Night, role, true, Mates);
                Assert.IsFalse(p.MicAllowed, role.ToString());
                Assert.IsNotNull(p.HearOnly);
                Assert.AreEqual(0, p.HearOnly.Count);
            }
        }

        [Test]
        public void At_night_the_mafia_talk_and_hear_only_each_other()
        {
            var p = MatchVoiceRules.For(Phase.Night, Role.Mafia, true, Mates);
            Assert.IsTrue(p.MicAllowed);
            CollectionAssert.AreEquivalent(Mates, p.HearOnly.ToArray());
        }

        [Test]
        public void A_mafia_without_mates_hears_nobody_at_night()
        {
            var p = MatchVoiceRules.For(Phase.Night, Role.Mafia, true, null);
            Assert.IsTrue(p.MicAllowed);
            Assert.AreEqual(0, p.HearOnly.Count);
        }

        [Test]
        public void By_day_the_living_talk_to_everybody()
        {
            foreach (var phase in new[] { Phase.Dawn, Phase.Day, Phase.Voting, Phase.Verdict })
            {
                var p = MatchVoiceRules.For(phase, Role.Mafia, true, Mates);
                Assert.IsTrue(p.MicAllowed, phase.ToString());
                Assert.IsNull(p.HearOnly, phase.ToString());
            }
        }

        [Test]
        public void The_dead_only_listen_by_day_and_hear_nothing_at_night()
        {
            var day = MatchVoiceRules.For(Phase.Day, Role.Civilian, false, Mates);
            Assert.IsFalse(day.MicAllowed);
            Assert.IsNull(day.HearOnly);
            var night = MatchVoiceRules.For(Phase.Night, Role.Mafia, false, Mates);
            Assert.IsFalse(night.MicAllowed);
            Assert.AreEqual(0, night.HearOnly.Count);
        }
    }
}
