using System.Collections.Generic;
using System.Linq;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    /// <summary>Test helper: moves the night forward to a role's call (calls only go forward, like the host's clock).</summary>
    public static class NightStepKit
    {
        public static MafiaMatch At(this MafiaMatch m, NightStep step)
        {
            int guard = 0;
            while (m.ActiveStep != step)
            {
                if (m.ActiveStep != null) m.EndStep();
                Assert.IsTrue(m.BeginNextStep(), "the night has no call " + step + " after " + (m.NightStepIndex < 0 ? "its opening" : m.NightSteps[m.NightStepIndex].ToString()));
                Assert.Less(++guard, 12);
            }
            return m;
        }
    }

    /// <summary>Test helper for a running host: moves the host's clock to a role's call, or through the whole night.</summary>
    public static class HostNightKit
    {
        public static void ToStep(HostMatchController host, NightStep step)
        {
            for (int i = 0; i < 400 && host.Match.ActiveStep != step; i++) host.Tick(0.5f);
            Assert.AreEqual(step, host.Match.ActiveStep, "the host never called " + step);
        }

        public static void FinishNight(HostMatchController host)
        {
            for (int i = 0; i < 400 && host.Match.Phase == Phase.Night; i++) host.Tick(0.5f);
            Assert.AreNotEqual(Phase.Night, host.Match.Phase, "the night never ended");
        }

        /// <summary>The role reveal, the whole night and the dawn pass: the table is at the day.</summary>
        public static void ToDay(HostMatchController host)
        {
            host.Tick(host.Match.Config.RoleRevealSeconds + 0.1f);
            FinishNight(host);
            host.Tick(host.Match.Config.DawnSeconds + 0.1f);
        }
    }

    /// <summary>The night is called one role at a time (owner's rule, 2026-11-05): who is called, who may act, and that a call never tells who is dead.</summary>
    public class NightStepsTests
    {
        private sealed class First : IRandom { public int Next(int max) => 0; }

        private static MafiaMatch Night(int n, MatchConfig cfg = null)
        {
            var list = Enumerable.Range(0, n).Select(i => new KeyValuePair<string, string>("p" + i, "Player " + i));
            var m = new MafiaMatch(cfg ?? MatchConfig.WithAllRoles(), list, new First());
            Assert.IsTrue(m.Start());
            m.Advance();
            Assert.AreEqual(Phase.Night, m.Phase);
            return m;
        }

        private static MatchPlayer P(MafiaMatch m, Role r) => m.Players.First(p => p.Role == r);

        [TestCase(4, 3)]
        [TestCase(6, 3)]
        [TestCase(7, 4)]
        [TestCase(8, 5)]
        public void TheCallsFollowThePlayerCount_AndTheNightLastsAsTheMockupSays(int players, int calls)
        {
            var m = Night(players);
            Assert.AreEqual(calls, m.NightSteps.Count);
            Assert.AreEqual(NightStep.Mafia, m.NightSteps[0], "the mafia are always called first");
            var cfg = m.Config;
            float total = cfg.NightOpenSeconds + calls * (cfg.NightStepSeconds + cfg.NightSleepSeconds);
            Assert.AreEqual(total, cfg.NightTotalSeconds(calls));
            Assert.AreEqual(55f, MatchConfig.WithAllRoles().NightTotalSeconds(3), "4 to 6 players: 4 + 3 x 17 seconds");
            Assert.AreEqual(89f, MatchConfig.WithAllRoles().NightTotalSeconds(5), "8 and more: 4 + 5 x 17 seconds");
        }

        [Test]
        public void TheOrderIsMafiaSilencerDoctorSheriffSniper_AndAbsentRolesAreSkipped()
        {
            var m = Night(8);
            CollectionAssert.AreEqual(new[] { NightStep.Mafia, NightStep.Silencer, NightStep.Doctor, NightStep.Sheriff, NightStep.Sniper }, m.NightSteps);
            var classic = Night(8, new MatchConfig());
            CollectionAssert.AreEqual(new[] { NightStep.Mafia, NightStep.Doctor, NightStep.Sheriff }, classic.NightSteps, "no silencer and no sniper in this match: no call for them");
        }

        [Test]
        public void NobodyActsInTheOpeningOrBetweenTwoCalls()
        {
            var m = Night(8);
            Assert.IsNull(m.ActiveStep);
            Assert.AreEqual(-1, m.NightStepIndex);
            Assert.AreEqual(ActionResult.WrongStep, m.MafiaVote(P(m, Role.Mafia).Id, m.Players.First(p => !p.IsMafia).Id), "the night has not begun to call anybody");
            m.At(NightStep.Mafia);
            m.EndStep();
            Assert.IsNull(m.ActiveStep, "between two calls everybody sleeps");
            Assert.AreEqual(ActionResult.WrongStep, m.MafiaVote(P(m, Role.Mafia).Id, m.Players.First(p => !p.IsMafia).Id));
        }

        [Test]
        public void ARoleMayActOnlyInItsOwnCall()
        {
            var m = Night(8);
            var civ = m.Players.First(p => p.Role == Role.Civilian).Id;
            var mafia = P(m, Role.Mafia).Id; var silencer = P(m, Role.Silencer).Id; var doctor = P(m, Role.Doctor).Id; var sheriff = P(m, Role.Sheriff).Id; var sniper = P(m, Role.Sniper).Id;
            m.At(NightStep.Mafia);
            Assert.AreEqual(ActionResult.Ok, m.MafiaVote(mafia, civ));
            Assert.AreEqual(ActionResult.Ok, m.MafiaVote(silencer, civ), "the silencer wakes with the mafia to advise");
            Assert.AreEqual(ActionResult.WrongStep, m.DoctorSave(doctor, doctor), "the doctor is asleep");
            Assert.AreEqual(ActionResult.WrongStep, m.SheriffCheck(sheriff, civ, out _));
            Assert.AreEqual(ActionResult.WrongStep, m.Snipe(sniper, null));
            Assert.AreEqual(ActionResult.WrongStep, m.Silence(silencer, civ), "the silencer silences in his own call, not the mafia's");
            m.At(NightStep.Silencer);
            Assert.AreEqual(ActionResult.Ok, m.Silence(silencer, civ));
            Assert.AreEqual(ActionResult.WrongStep, m.MafiaVote(mafia, civ), "the mafia call is over");
            m.At(NightStep.Doctor);
            Assert.AreEqual(ActionResult.Ok, m.DoctorSave(doctor, civ));
            Assert.AreEqual(ActionResult.WrongStep, m.Silence(silencer, civ));
            m.At(NightStep.Sheriff);
            Assert.AreEqual(ActionResult.Ok, m.SheriffCheck(sheriff, civ, out _));
            m.At(NightStep.Sniper);
            Assert.AreEqual(ActionResult.Ok, m.Snipe(sniper, null));
            Assert.IsFalse(m.BeginNextStep(), "that was the last call: the host moves on to the dawn");
        }

        [Test]
        public void ADeadRolesCallStillHappens_SoTheLengthOfTheNightTellsNothing()
        {
            var m = Night(8);
            P(m, Role.Doctor).Alive = false;
            Assert.IsTrue(m.NightSteps.Contains(NightStep.Doctor), "the doctor is dead but still called, as long as the role is in the match");
            m.At(NightStep.Doctor);
            Assert.AreEqual(ActionResult.DeadActor, m.DoctorSave(P(m, Role.Doctor).Id, m.Players.First(p => p.Role == Role.Civilian).Id), "he cannot act, he just does not know it matters");
        }

        [Test]
        public void TheCallsStartAgainEveryNight()
        {
            var m = Night(6);
            m.At(NightStep.Sheriff);
            m.Advance();                                                                  // night -> dawn
            Assert.IsNull(m.ActiveStep, "no call is open outside the night");
            while (m.Phase != Phase.Night && m.Phase != Phase.GameOver) m.Advance();
            if (m.Phase == Phase.Night) { Assert.AreEqual(-1, m.NightStepIndex); Assert.IsNull(m.ActiveStep); }
        }

        [Test]
        public void ASnapshotKeepsTheCallThatIsOpen()
        {
            var m = Night(8);
            m.At(NightStep.Doctor);
            var back = MafiaMatch.Restore(m.Config, m.Export(), new First());
            Assert.IsNotNull(back);
            Assert.AreEqual(NightStep.Doctor, back.ActiveStep);
            Assert.AreEqual(m.NightStepIndex, back.NightStepIndex);
            CollectionAssert.AreEqual(m.NightSteps, back.NightSteps);
            m.EndStep();
            Assert.IsNull(MafiaMatch.Restore(m.Config, m.Export(), new First()).ActiveStep, "asleep stays asleep");
        }

        [Test]
        public void IsAwakeCoversEveryStep_SoANewRoleCannotBeForgotten()
        {
            foreach (NightStep s in System.Enum.GetValues(typeof(NightStep)))
                Assert.IsTrue(System.Enum.GetValues(typeof(Role)).Cast<Role>().Any(r => MafiaMatch.IsAwake(s, r)), s + ": nobody is awake in this step");
            foreach (Role r in System.Enum.GetValues(typeof(Role)))
                if (r.ActsAtNight()) Assert.IsTrue(System.Enum.GetValues(typeof(NightStep)).Cast<NightStep>().Any(s => MafiaMatch.IsAwake(s, r)), r + " acts at night but is never called");
        }
    }
}
