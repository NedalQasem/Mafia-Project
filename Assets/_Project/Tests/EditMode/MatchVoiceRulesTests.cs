using System.Linq;
using MafiaNight.MatchRules;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    public class MatchVoiceRulesTests
    {
        private static readonly string[] Mates = { "m1", "m2" };
        private static readonly string[] Alive = { "me", "a", "b", "m1", "m2" };            // the living players
        private static readonly string[] Members = { "me", "a", "b", "m1", "m2", "dead1", "late1" };      // the room: the living, a dead player and a late member

        private static VoicePlan Plan(Phase phase, Role? role, bool alive, string[] mates = null) => MatchVoiceRules.For(phase, role, alive, mates ?? Mates, Alive, Members, "me");

        [Test]
        public void Everybody_talks_and_hears_everybody_before_and_after_the_match()
        {
            foreach (var phase in new[] { Phase.Lobby, Phase.RoleReveal, Phase.GameOver })
            {
                var p = Plan(phase, Role.Civilian, false);
                Assert.IsTrue(p.MicAllowed, phase.ToString());
                Assert.IsNull(p.HearOnly, phase.ToString());
            }
        }

        [Test]
        public void At_night_the_town_is_silent_and_hears_nothing()
        {
            foreach (var role in new[] { Role.Civilian, Role.Doctor, Role.Sheriff })
            {
                var p = Plan(Phase.Night, role, true);
                Assert.IsFalse(p.MicAllowed, role.ToString());
                Assert.IsNotNull(p.HearOnly);
                Assert.AreEqual(0, p.HearOnly.Count);
            }
        }

        [Test]
        public void At_night_the_mafia_talk_and_hear_only_each_other()
        {
            var p = Plan(Phase.Night, Role.Mafia, true);
            Assert.IsTrue(p.MicAllowed);
            CollectionAssert.AreEquivalent(Mates, p.HearOnly.ToArray());
        }

        [Test]
        public void At_night_a_mafia_member_talks_only_in_his_own_call()
        {
            var asleep = MatchVoiceRules.For(Phase.Night, Role.Mafia, true, Mates, Alive, Members, "me", false, null, false);
            Assert.IsFalse(asleep.MicAllowed, "the mafia call is not open: everybody sleeps in silence");
            CollectionAssert.AreEquivalent(Mates, asleep.HearOnly.ToArray());
            var awake = MatchVoiceRules.For(Phase.Night, Role.Mafia, true, Mates, Alive, Members, "me", false, null, true);
            Assert.IsTrue(awake.MicAllowed);
            Assert.IsTrue(MatchVoiceRules.For(Phase.Day, Role.Mafia, true, Mates, Alive, Members, "me", false, null, false).MicAllowed, "by day the turn does not matter");
        }

        [Test]
        public void A_mafia_without_mates_hears_nobody_at_night()
        {
            var p = MatchVoiceRules.For(Phase.Night, Role.Mafia, true, null, Alive, Members, "me");
            Assert.IsTrue(p.MicAllowed);
            Assert.AreEqual(0, p.HearOnly.Count);
        }

        [Test]
        public void By_day_a_living_player_talks_and_hears_the_living_players_only()
        {
            foreach (var phase in new[] { Phase.Dawn, Phase.Day, Phase.Voting, Phase.Verdict })
            {
                var p = Plan(phase, Role.Mafia, true);
                Assert.IsTrue(p.MicAllowed, phase.ToString());
                CollectionAssert.AreEquivalent(new[] { "a", "b", "m1", "m2" }, p.HearOnly.ToArray(), phase + ": the players hear the other players, not the spectators and not themselves");
                Assert.IsFalse(p.HearOnly.Contains("dead1")); Assert.IsFalse(p.HearOnly.Contains("late1"));
            }
        }

        [Test]
        public void A_spectator_talks_freely_and_hears_everybody_by_day()
        {
            foreach (var phase in new[] { Phase.Dawn, Phase.Day, Phase.Voting, Phase.Verdict })
            {
                var p = MatchVoiceRules.For(phase, Role.Civilian, false, Mates, Alive.Where(x => x != "me").ToArray(), Members, "me");
                Assert.IsTrue(p.MicAllowed, phase + ": the spectators may talk");
                Assert.IsNull(p.HearOnly, phase + ": and they hear the players' discussion");
            }
        }

        [Test]
        public void At_night_a_spectator_hears_only_the_other_spectators()
        {
            var p = MatchVoiceRules.For(Phase.Night, Role.Mafia, false, Mates, Alive.Where(x => x != "me").ToArray(), Members, "me");
            Assert.IsTrue(p.MicAllowed);
            CollectionAssert.AreEquivalent(new[] { "dead1", "late1" }, p.HearOnly.ToArray());          // not the mafia's talk, not the sleeping town
        }

        [Test]
        public void A_late_member_who_is_not_a_player_is_a_spectator_too()
        {
            var p = MatchVoiceRules.For(Phase.Day, null, false, Mates, Alive, Members, "late1");
            Assert.IsTrue(p.MicAllowed);
            Assert.IsNull(p.HearOnly);
        }

        [Test]
        public void No_plan_ever_lets_a_player_hear_a_spectator_or_a_spectator_reach_a_player()
        {
            foreach (var phase in new[] { Phase.Night, Phase.Dawn, Phase.Day, Phase.Voting, Phase.Verdict })
                foreach (var role in new Role?[] { Role.Civilian, Role.Mafia, Role.Doctor, Role.Sheriff })
                {
                    var p = Plan(phase, role, true);
                    Assert.IsFalse(p.HearOnly != null && (p.HearOnly.Contains("dead1") || p.HearOnly.Contains("late1")), phase + " " + role);
                    Assert.IsNotNull(p.HearOnly, "a living player never hears 'everybody' during the match: " + phase + " " + role);
                }
        }
    }
}
