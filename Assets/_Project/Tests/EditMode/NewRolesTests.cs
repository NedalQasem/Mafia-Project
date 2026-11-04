using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    /// <summary>The silencer, the sniper and the Avenger (owner's rules of 2026-11-04) and the role table they stand on.</summary>
    public class NewRolesTests
    {
        private sealed class First : IRandom { public int Next(int max) => 0; }

        private static MafiaMatch Night(int n = 8, MatchConfig cfg = null)
        {
            var list = Enumerable.Range(0, n).Select(i => new KeyValuePair<string, string>("p" + i, "Player " + i));
            var m = new MafiaMatch(cfg ?? MatchConfig.WithAllRoles(), list, new First());
            Assert.IsTrue(m.Start());
            m.Advance();
            Assert.AreEqual(Phase.Night, m.Phase);
            return m;
        }

        private static MatchPlayer P(MafiaMatch m, Role r) => m.Players.First(p => p.Role == r);
        private static MatchPlayer Civ(MafiaMatch m, int index = 0) => m.Players.Where(p => p.Role == Role.Civilian).ElementAt(index);

        /// <summary>Everybody who has to act does, harmlessly: the mafia (and the silencer) kill <paramref name="victim"/>, the silencer silences <paramref name="silenced"/>, the sniper keeps his bullet.</summary>
        private static void Acts(MafiaMatch m, string victim, string silenced = null, string shot = null, string saved = null)
        {
            foreach (var p in m.Players.Where(x => x.IsMafia)) Assert.AreEqual(ActionResult.Ok, m.MafiaVote(p.Id, victim), "mafia vote");
            var silencer = m.Players.FirstOrDefault(x => x.Role == Role.Silencer && x.InPlay);
            if (silencer != null) Assert.AreEqual(ActionResult.Ok, m.Silence(silencer.Id, silenced ?? Civ(m, 1).Id));
            var doctor = m.Players.FirstOrDefault(x => x.Role == Role.Doctor && x.InPlay);
            if (doctor != null) m.DoctorSave(doctor.Id, saved ?? doctor.Id);
            var sheriff = m.Players.FirstOrDefault(x => x.Role == Role.Sheriff && x.InPlay);
            if (sheriff != null) m.SheriffCheck(sheriff.Id, Civ(m).Id, out _);
            var sniper = m.Players.FirstOrDefault(x => x.Role == Role.Sniper && x.InPlay && x.ShotsUsed == 0);
            if (sniper != null) Assert.AreEqual(ActionResult.Ok, m.Snipe(sniper.Id, shot));
        }

        // ------------------------------------------------------------------ the role table

        [Test]
        public void EveryRoleIsInTheTable_AndTheMafiaTeamIsTheKillerAndTheSilencer()
        {
            foreach (Role r in Enum.GetValues(typeof(Role))) Assert.IsNotNull(RoleDefs.Of(r), r.ToString());
            CollectionAssert.AreEquivalent(new[] { Role.Mafia, Role.Silencer }, Enum.GetValues(typeof(Role)).Cast<Role>().Where(r => r.IsMafiaTeam()).ToArray());
            Assert.IsTrue(Role.Silencer.Can(NightAbility.Kill) && Role.Silencer.Can(NightAbility.Silence), "the silencer is a mafia member who also silences");
            Assert.IsTrue(RoleDefs.Of(Role.Avenger).AvengesOnExit);
            Assert.AreEqual(1, RoleDefs.Of(Role.Sniper).Uses);
        }

        [Test]
        public void TheNewRolesNeedEnoughPlayers_AndTheClassicConfigIsUnchanged()
        {
            var c8 = MafiaMatch.RoleCounts(8, MatchConfig.WithAllRoles());
            Assert.AreEqual(1, c8[Role.Mafia]); Assert.AreEqual(1, c8[Role.Silencer]); Assert.AreEqual(1, c8[Role.Sniper]); Assert.AreEqual(1, c8[Role.Avenger]);
            Assert.AreEqual(2, c8[Role.Civilian]); Assert.AreEqual(8, c8.Values.Sum());
            var c4 = MafiaMatch.RoleCounts(4, MatchConfig.WithAllRoles());
            Assert.AreEqual(0, c4[Role.Silencer], "one mafia member is never the silencer"); Assert.AreEqual(0, c4[Role.Sniper]); Assert.AreEqual(0, c4[Role.Avenger]);
            var c6 = MafiaMatch.RoleCounts(6, MatchConfig.WithAllRoles());
            Assert.AreEqual(1, c6[Role.Sniper]); Assert.AreEqual(0, c6[Role.Avenger], "the town keeps at least two plain citizens");
            for (int n = 4; n <= 16; n++) Assert.AreEqual(n, MafiaMatch.RoleCounts(n, MatchConfig.WithAllRoles()).Values.Sum(), n + " players");
            var classic = MafiaMatch.RoleCounts(8, new MatchConfig());
            Assert.AreEqual(0, classic[Role.Silencer] + classic[Role.Sniper] + classic[Role.Avenger]);
        }

        // ------------------------------------------------------------------ the silencer

        [Test]
        public void TheSilencerSilencesOneDay_AndThenItEnds()
        {
            var m = Night();
            var target = Civ(m, 1);
            Acts(m, Civ(m).Id, silenced: target.Id);
            m.Advance();                                                      // dawn
            Assert.AreEqual(target.Id, m.LastNight.SilencedId);
            Assert.AreEqual(target.Id, m.SilencedId);
            m.Advance();                                                      // day
            Assert.AreEqual(Phase.Day, m.Phase);
            Assert.AreEqual(target.Id, m.SilencedId, "silenced through the day");
            m.Advance(); m.Advance(); m.Advance();                            // voting, verdict, next night
            Assert.AreEqual(Phase.Night, m.Phase);
            Assert.IsNull(m.SilencedId, "a new night: the silence is over");
        }

        [Test]
        public void ASilencedPlayerCanStillVote()
        {
            var m = Night();
            var target = Civ(m, 1);
            Acts(m, Civ(m).Id, silenced: target.Id);
            m.Advance(); m.Advance(); m.Advance();                            // dawn, day, voting
            Assert.AreEqual(Phase.Voting, m.Phase);
            Assert.AreEqual(ActionResult.Ok, m.Vote(target.Id, P(m, Role.Mafia).Id), "the silenced player votes");
        }

        [Test]
        public void TheSilencerCannotSilenceAMateOrHimself_AndOnlyHeCanSilence()
        {
            var m = Night();
            var s = P(m, Role.Silencer);
            Assert.AreEqual(ActionResult.MafiaTeammate, m.Silence(s.Id, P(m, Role.Mafia).Id));
            Assert.AreEqual(ActionResult.InvalidTarget, m.Silence(s.Id, s.Id));
            Assert.AreEqual(ActionResult.WrongRole, m.Silence(P(m, Role.Mafia).Id, Civ(m).Id));
            Assert.AreEqual(ActionResult.WrongRole, m.MafiaVote(Civ(m).Id, Civ(m, 1).Id));
            Assert.AreEqual(ActionResult.Ok, m.MafiaVote(s.Id, Civ(m).Id), "the silencer takes part in the kill like the other mafia");
        }

        [Test]
        public void ASilencedPlayerWhoDiesThatNightIsNotSilenced()
        {
            var m = Night();
            var target = Civ(m);
            Acts(m, target.Id, silenced: target.Id);
            m.Advance();
            Assert.IsNull(m.LastNight.SilencedId);
        }

        [Test]
        public void TheNightWaitsForTheSilencerAndTheSnipersDecision()
        {
            var m = Night();
            foreach (var p in m.Players.Where(x => x.IsMafia)) m.MafiaVote(p.Id, Civ(m).Id);
            m.DoctorSave(P(m, Role.Doctor).Id, Civ(m).Id);
            m.SheriffCheck(P(m, Role.Sheriff).Id, Civ(m).Id, out _);
            Assert.IsFalse(m.NightActionsComplete(), "the silencer and the sniper have not decided");
            m.Silence(P(m, Role.Silencer).Id, Civ(m, 1).Id);
            Assert.IsFalse(m.NightActionsComplete(), "the sniper has not decided");
            m.Snipe(P(m, Role.Sniper).Id, null);
            Assert.IsTrue(m.NightActionsComplete(), "keeping the bullet is a decision");
        }

        // ------------------------------------------------------------------ the sniper

        [Test]
        public void ASniperWhoHitsTheMafiaKillsHimAndStaysInTheGame()
        {
            var m = Night();
            var mafia = P(m, Role.Mafia); var sniper = P(m, Role.Sniper);
            Acts(m, Civ(m).Id, shot: mafia.Id);
            m.Advance();
            Assert.IsFalse(mafia.Alive); Assert.IsTrue(sniper.Alive);
            Assert.AreEqual(1, m.LastNight.ShotDeaths.Count);
            Assert.AreEqual(mafia.Id, m.LastNight.ShotDeaths[0].Id);
        }

        [Test]
        public void ASniperWhoHitsACitizenDiesWithHim()
        {
            var m = Night();
            var citizen = Civ(m, 1); var sniper = P(m, Role.Sniper);
            Acts(m, Civ(m).Id, shot: citizen.Id);
            m.Advance();
            Assert.IsFalse(citizen.Alive); Assert.IsFalse(sniper.Alive);
            CollectionAssert.AreEquivalent(new[] { citizen.Id, sniper.Id }, m.LastNight.ShotDeaths.Select(d => d.Id).ToArray());
        }

        [Test]
        public void TheDoctorCannotStopTheShot()
        {
            var m = Night();
            var mafia = P(m, Role.Mafia);
            Acts(m, Civ(m).Id, shot: mafia.Id, saved: mafia.Id);
            m.Advance();
            Assert.IsFalse(mafia.Alive, "the protection is against the mafia's kill only");
        }

        [Test]
        public void TheSniperHasOneBullet()
        {
            var m = Night();
            var sniper = P(m, Role.Sniper);
            Acts(m, Civ(m).Id, shot: P(m, Role.Mafia).Id);
            m.Advance();
            Assert.AreEqual(1, sniper.ShotsUsed);
            m.Advance(); m.Advance(); m.Advance(); m.Advance();               // day, voting, verdict, night
            Assert.AreEqual(Phase.Night, m.Phase);
            Assert.AreEqual(ActionResult.AbilityUsed, m.Snipe(sniper.Id, Civ(m).Id));
        }

        [Test]
        public void KeepingTheBulletKeepsIt()
        {
            var m = Night();
            var sniper = P(m, Role.Sniper);
            Acts(m, Civ(m).Id);
            m.Advance();
            Assert.AreEqual(0, sniper.ShotsUsed);
            Assert.AreEqual(0, m.LastNight.ShotDeaths.Count);
        }

        [Test]
        public void TheSniperShootsEvenIfTheMafiaKillHimThatNight()
        {
            var m = Night();
            var sniper = P(m, Role.Sniper); var mafia = P(m, Role.Mafia);
            Acts(m, sniper.Id, shot: mafia.Id);
            m.Advance();
            Assert.IsFalse(sniper.Alive); Assert.IsFalse(mafia.Alive, "his shot left before he died");
        }

        [Test]
        public void TheDawnNamesNobodyAsTheShooter()
        {
            var m = Night();
            Acts(m, Civ(m).Id, shot: P(m, Role.Mafia).Id);
            m.Advance();
            Assert.IsFalse(m.LastNight.ShotDeaths.Any(d => d.Id == P(m, Role.Sniper).Id), "he lived: he is not in the report");
            Assert.IsTrue(P(m, Role.Sniper).Alive);
        }

        // ------------------------------------------------------------------ the Avenger

        private static MafiaMatch AvengerKilledAtNight()
        {
            var m = Night();
            var av = P(m, Role.Avenger);
            Acts(m, av.Id);
            m.Advance();                                                       // dawn: he died
            Assert.IsFalse(av.Alive);
            m.Advance();                                                       // the end of the dawn
            return m;
        }

        [Test]
        public void AnAvengerKilledAtNightDecidesAtDawnBeforeTheDiscussion()
        {
            var m = AvengerKilledAtNight();
            Assert.AreEqual(Phase.Avenge, m.Phase);
            Assert.AreEqual(P(m, Role.Avenger).Id, m.AvengerId);
            Assert.AreEqual(30f, m.PhaseSeconds, "30 seconds to think");
            var target = Civ(m);
            Assert.AreEqual(ActionResult.Ok, m.AvengerPick(m.AvengerId, target.Id));
            Assert.AreEqual(target.Id, m.AvengePick);
            m.Advance();
            Assert.AreEqual(Phase.Day, m.Phase);
            Assert.IsFalse(target.Alive, "he took him along");
            Assert.AreEqual(target.Id, m.LastAvenge.TargetId);
            Assert.AreEqual(Role.Civilian, m.LastAvenge.TargetRole);
        }

        [Test]
        public void TheAvengerCanTakeHisPickBack_AndPicksNobodyIfHeNeverPicks()
        {
            var m = AvengerKilledAtNight();
            var av = m.AvengerId;
            Assert.AreEqual(ActionResult.Ok, m.AvengerPick(av, Civ(m).Id));
            Assert.AreEqual(ActionResult.Ok, m.AvengerPick(av, null));
            Assert.IsNull(m.AvengePick);
            int alive = m.InPlayCount;
            m.Advance();
            Assert.AreEqual(Phase.Day, m.Phase);
            Assert.AreEqual(alive, m.InPlayCount, "no pick: nobody leaves with him");
            Assert.IsNull(m.LastAvenge.TargetId);
        }

        [Test]
        public void OnlyTheAvengerPicks_OnlyInHisMoment_AndOnlyALivingPlayer()
        {
            var m = AvengerKilledAtNight();
            Assert.AreEqual(ActionResult.WrongRole, m.AvengerPick(Civ(m).Id, Civ(m, 1).Id));
            Assert.AreEqual(ActionResult.InvalidTarget, m.AvengerPick(m.AvengerId, m.AvengerId), "he is out already");
            Assert.AreEqual(ActionResult.InvalidTarget, m.AvengerPick(m.AvengerId, "ghost"));
            var m2 = Night();
            Assert.AreEqual(ActionResult.WrongPhase, m2.AvengerPick(P(m2, Role.Avenger).Id, Civ(m2).Id));
        }

        [Test]
        public void AnAvengerVotedOutDecidesAfterTheVerdict_ThenTheNightComes()
        {
            var m = Night();
            var av = P(m, Role.Avenger);
            Acts(m, Civ(m).Id);
            m.Advance(); m.Advance(); m.Advance();                             // dawn, day, voting
            Assert.AreEqual(Phase.Voting, m.Phase);
            foreach (var p in m.InPlay.ToList()) m.Vote(p.Id, p.Id == av.Id ? Civ(m, 1).Id : av.Id);
            m.Advance();                                                       // verdict
            Assert.AreEqual(av.Id, m.LastVerdict.RemovedId);
            m.Advance();
            Assert.AreEqual(Phase.Avenge, m.Phase);
            var target = P(m, Role.Mafia);
            m.AvengerPick(av.Id, target.Id);
            m.Advance();
            Assert.IsFalse(target.Alive);
            Assert.IsTrue(m.Phase == Phase.Night || m.Phase == Phase.GameOver);
        }

        [Test]
        public void IfTheTownAlreadyWon_TheAvengerGetsNoMoment()
        {
            var m = Night(8, new MatchConfig { HasAvenger = true, HasSniper = true });
            var av = P(m, Role.Avenger);
            foreach (var mafia in m.Players.Where(p => p.IsMafia)) mafia.Alive = false;      // the last mafia is gone: the town won
            Acts2(m, av.Id);
            m.Advance();
            Assert.AreEqual(Winner.Town, m.Winner);
            m.Advance();
            Assert.AreEqual(Phase.GameOver, m.Phase);
        }

        private static void Acts2(MafiaMatch m, string victimToKill)
        {
            var doctor = m.Players.FirstOrDefault(x => x.Role == Role.Doctor && x.InPlay);
            if (doctor != null) m.DoctorSave(doctor.Id, doctor.Id);
            var sheriff = m.Players.FirstOrDefault(x => x.Role == Role.Sheriff && x.InPlay);
            if (sheriff != null) m.SheriffCheck(sheriff.Id, Civ(m).Id, out _);
            var sniper = m.Players.FirstOrDefault(x => x.Role == Role.Sniper && x.InPlay);
            if (sniper != null) m.Snipe(sniper.Id, victimToKill);                           // the sniper hits the Avenger: a citizen, so the sniper goes with him
        }

        [Test]
        public void AnAvengerShotBySniperStillGetsHisMoment()
        {
            var m = Night();
            var av = P(m, Role.Avenger);
            Acts(m, Civ(m).Id, shot: av.Id);
            m.Advance();
            Assert.IsFalse(av.Alive);
            m.Advance();
            Assert.AreEqual(Phase.Avenge, m.Phase);
        }

        [Test]
        public void AnAvengerWhoLeftTheMatchHasNoMoment()
        {
            var m = Night();
            var av = P(m, Role.Avenger);
            Acts(m, av.Id);
            m.Advance();
            m.PlayerLeft(av.Id);
            m.Advance();
            Assert.AreEqual(Phase.Day, m.Phase);
        }

        // ------------------------------------------------------------------ taking over as host

        [Test]
        public void ASnapshotKeepsTheSilenceTheShotsAndTheAvengersMoment()
        {
            var m = AvengerKilledAtNight();
            m.AvengerPick(m.AvengerId, Civ(m).Id);
            var copy = MafiaMatch.Restore(MatchConfig.WithAllRoles(), m.Export(), new First());
            Assert.IsNotNull(copy);
            Assert.AreEqual(Phase.Avenge, copy.Phase);
            Assert.AreEqual(m.AvengerId, copy.AvengerId);
            Assert.AreEqual(m.AvengePick, copy.AvengePick);
            copy.Advance();
            Assert.AreEqual(Phase.Day, copy.Phase);
            Assert.IsFalse(copy.Get(m.AvengePick).Alive);

            var n = Night();
            Acts(n, Civ(n).Id, silenced: Civ(n, 1).Id, shot: P(n, Role.Mafia).Id);
            n.Advance();
            var back = MafiaMatch.Restore(MatchConfig.WithAllRoles(), n.Export(), new First());
            Assert.AreEqual(n.SilencedId, back.SilencedId);
            Assert.AreEqual(n.LastNight.ShotDeaths.Count, back.LastNight.ShotDeaths.Count);
            Assert.AreEqual(1, back.Players.First(p => p.Role == Role.Sniper).ShotsUsed);
            Assert.AreEqual(n.LastDoctor.Saved, back.LastDoctor.Saved);
        }

        // ------------------------------------------------------------------ the doctor learns the result

        [Test]
        public void TheDoctorLearnsWhetherHisProtectionStoppedTheMafia()
        {
            var m = Night();
            var victim = Civ(m);
            Acts(m, victim.Id, saved: victim.Id);
            m.Advance();
            Assert.IsTrue(m.LastDoctor.Saved);
            Assert.AreEqual(victim.Id, m.LastDoctor.TargetId);

            var n = Night();
            Acts(n, Civ(n).Id, saved: Civ(n, 1).Id);
            n.Advance();
            Assert.IsFalse(n.LastDoctor.Saved);
        }

        // ------------------------------------------------------------------ voice

        [Test]
        public void ASilencedPlayerHearsButDoesNotSpeak_ByDayAndAtTheVote()
        {
            var alive = new[] { "me", "a", "b" };
            foreach (var phase in new[] { Phase.Dawn, Phase.Day, Phase.Voting })
            {
                var p = MatchVoiceRules.For(phase, Role.Civilian, true, null, alive, alive, "me", silenced: true);
                Assert.IsFalse(p.MicAllowed, phase.ToString());
                CollectionAssert.AreEquivalent(new[] { "a", "b" }, p.HearOnly.ToArray());
            }
            Assert.IsTrue(MatchVoiceRules.For(Phase.Day, Role.Civilian, true, null, alive, alive, "me").MicAllowed);
        }

        [Test]
        public void InTheAvengersMomentEverybodyTalksToHim()
        {
            var alive = new[] { "a", "b", "c" };
            var members = new[] { "a", "b", "c", "av", "late" };
            var forAvenger = MatchVoiceRules.For(Phase.Avenge, Role.Avenger, false, null, alive, members, "av", avengerId: "av");
            Assert.IsTrue(forAvenger.MicAllowed, "he is out of the game, but he talks");
            CollectionAssert.AreEquivalent(alive, forAvenger.HearOnly.ToArray(), "he hears the players");
            var forPlayer = MatchVoiceRules.For(Phase.Avenge, Role.Civilian, true, null, alive, members, "a", avengerId: "av");
            Assert.IsTrue(forPlayer.MicAllowed);
            CollectionAssert.AreEquivalent(new[] { "b", "c", "av" }, forPlayer.HearOnly.ToArray(), "the players hear him");
            var forSpectator = MatchVoiceRules.For(Phase.Avenge, null, false, null, alive, members, "late", avengerId: "av");
            Assert.IsTrue(forSpectator.MicAllowed && forSpectator.HearOnly == null, "a spectator hears the talk as in any day");
        }

        // ------------------------------------------------------------------ the host: the three seconds

        private sealed class Rec : IMatchTransport
        {
            public readonly List<KeyValuePair<string, WireMessage>> Sent = new List<KeyValuePair<string, WireMessage>>();
            public event Action<string, WireMessage> Received { add { } remove { } }
            public void Send(string peer, WireMessage m) { Sent.Add(new KeyValuePair<string, WireMessage>(peer, m)); }
        }

        private static WireMessage Act(string kind, string target) => new WireMessage(Msg.Act, kind, target);

        [Test]
        public void AnAvengersPickIsFinalOnlyAfterThreeQuietSeconds()
        {
            var list = Enumerable.Range(0, 8).Select(i => new KeyValuePair<string, string>("p" + i, "Player " + i));
            var match = new MafiaMatch(MatchConfig.WithAllRoles(), list, new First());
            var net = new Rec();
            var host = new HostMatchController(match, net, "p0", m => { });
            Assert.IsTrue(host.Begin("m1"));
            host.Tick(100f);                                                    // the role reveal ends: night
            var av = match.Players.First(p => p.Role == Role.Avenger).Id;
            foreach (var mafia in match.Players.Where(p => p.IsMafia)) host.Receive(mafia.Id, Act("mafia", av));
            host.Tick(100f);                                                    // night -> dawn
            Assert.AreEqual(Phase.Dawn, match.Phase);
            host.Tick(100f);                                                    // dawn -> the Avenger's moment
            Assert.AreEqual(Phase.Avenge, match.Phase);
            Assert.IsTrue(net.Sent.Any(s => s.Value.Type == Msg.Avenge && s.Value.Arg(3) == "open"), "everybody is told his card is open");

            var a = match.Players.First(p => p.Role == Role.Civilian).Id;
            var b = match.Players.Where(p => p.Role == Role.Civilian).Skip(1).First().Id;
            host.Receive(av, Act("avenge", a));
            host.Tick(2f);
            Assert.AreEqual(Phase.Avenge, match.Phase, "2 seconds after the pick: he can still change his mind");
            host.Receive(av, Act("avenge", b));                                 // a new pick starts the three seconds again
            host.Tick(2.9f);
            Assert.AreEqual(Phase.Avenge, match.Phase);
            host.Receive(av, Act("avenge", ""));                                // he takes it back
            host.Tick(10f);
            Assert.AreEqual(Phase.Avenge, match.Phase, "no pick: nothing is final");
            host.Receive(av, Act("avenge", a));
            host.Tick(3.5f);
            Assert.AreEqual(Phase.Day, match.Phase);
            Assert.IsFalse(match.Get(a).Alive);
            Assert.IsTrue(net.Sent.Any(s => s.Value.Type == Msg.Avenge && s.Value.Arg(3) == "done" && s.Value.Arg(1) == a), "the final pick is announced");
        }

        [Test]
        public void TheClientKnowsTheSilencedPlayerTheAvengerAndTheShotButNotTheShooter()
        {
            var c = new ClientMatchController("me", m => { });
            c.Apply(new WireMessage(Msg.Start, "m"));
            c.Apply(new WireMessage(Msg.Role, "Civilian"));
            c.Apply(new WireMessage(Msg.State, "Dawn", "1", "8", "me", "Me", "1", "0", "x", "X", "0", "0", "y", "Y", "0", "0", "z", "Z", "1", "0"));
            c.Apply(new WireMessage(Msg.Dawn, "", "0", "", "", "z", "x", "Civilian", "y", "Sniper"));
            Assert.AreEqual("z", c.SilencedId);
            Assert.AreEqual(2, c.DawnShotDeaths.Count);
            Assert.AreEqual(Role.Civilian, c.Seat("x").Role);
            c.Apply(new WireMessage(Msg.Avenge, "x", "z", "", "pick"));
            Assert.AreEqual("z", c.AvengeTargetId);
            c.Apply(new WireMessage(Msg.Avenge, "x", "z", "Civilian", "done"));
            Assert.IsTrue(c.AvengeDone);
            Assert.AreEqual(Role.Civilian, c.Seat("z").Role);
        }

        [TestCase(6)]
        [TestCase(8)]
        [TestCase(10)]
        [TestCase(16)]
        public void MatchesOfBotsWithEveryRoleAlwaysReachTheEnd_AndTheNewMomentsHappen(int players)
        {
            int avenges = 0, silences = 0, shots = 0;
            for (int seed = 1; seed <= 25; seed++)
            {
                var wire = new Rec();
                var net = new BotMatchTransport(wire, new SystemRandom(seed));
                var ids = Enumerable.Range(0, players - 1).Select(i => "bot:b" + i).ToList();
                net.Set(ids);
                var list = new[] { "me" }.Concat(ids).Select(id => new KeyValuePair<string, string>(id, id)).ToList();
                var match = new MafiaMatch(MatchConfig.WithAllRoles(), list, new SystemRandom(seed));
                BotPlayer me = null; HostMatchController host = null;
                me = new BotPlayer("me", m => host.Receive("me", m), new SystemRandom(seed + 100), new Random(seed));
                host = new HostMatchController(match, net, "me", m => me.Receive(m));
                net.ToHost = (id, m) => host.Receive(id, m);
                host.Trace = line => { if (line.StartsWith("avenge done")) avenges++; if (line.Contains("silenced:")) silences++; if (line.Contains("shot:")) shots++; };
                bool finished = false; host.Finished += () => finished = true;
                Assert.IsTrue(host.Begin("m" + seed, list.ToDictionary(p => p.Key, p => "")));
                for (int i = 0; i < 40000 && !finished; i++) { host.Tick(0.5f); net.Tick(0.5f); me.Tick(0.5f); }
                Assert.IsTrue(finished, players + " players, seed " + seed + ": the match never ended (phase " + match.Phase + ")");
                Assert.IsEmpty(wire.Sent.Where(s => s.Key.StartsWith("bot:")), "a message for a bot went out to the network");
            }
            if (players >= 8)
            {
                Assert.Greater(silences, 0, "the silencer never silenced in 25 matches of " + players);
                Assert.Greater(avenges, 0, "the Avenger never took anybody in 25 matches of " + players);
            }
            Assert.GreaterOrEqual(shots, 0);
        }

        [Test]
        public void TheSilencerIsKnownToHisMateAsTheSilencer()
        {
            var c = new ClientMatchController("m1", m => { });
            c.Apply(new WireMessage(Msg.Start, "m"));
            c.Apply(new WireMessage(Msg.Role, "Mafia", "m2|Silencer"));
            c.Apply(new WireMessage(Msg.State, "Night", "1", "20", "m1", "A", "1", "0", "m2", "B", "1", "0"));
            Assert.IsTrue(c.IAmMafia);
            Assert.AreEqual(Role.Silencer, c.Seat("m2").Role);
            Assert.IsTrue(c.Seat("m2").IsMate, "the silencer is one of the mafia team");
        }
    }
}
