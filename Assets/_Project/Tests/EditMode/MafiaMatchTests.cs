using System.Collections.Generic;
using System.Linq;
using MafiaNight.MatchRules;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    public class MafiaMatchTests
    {
        /// <summary>Always picks the first option, so shuffles and tie-breaks are repeatable.</summary>
        private sealed class First : IRandom { public int Next(int max) => 0; }

        private static MafiaMatch NewMatch(int n, MatchConfig cfg = null)
        {
            var list = Enumerable.Range(0, n).Select(i => new KeyValuePair<string, string>("p" + i, "Player " + i));
            var m = new MafiaMatch(cfg ?? new MatchConfig(), list, new First());
            Assert.IsTrue(m.Start());
            return m;
        }

        private static MatchPlayer One(MafiaMatch m, Role r) => m.Players.First(p => p.Role == r);
        private static List<MatchPlayer> All(MafiaMatch m, Role r) => m.Players.Where(p => p.Role == r).ToList();
        private static MatchPlayer Town(MafiaMatch m) => m.Players.First(p => !p.IsMafia);

        /// <summary>Plays RoleReveal into Night.</summary>
        private static void ToNight(MafiaMatch m) { Assert.AreEqual(Phase.RoleReveal, m.Phase); m.Advance(); Assert.AreEqual(Phase.Night, m.Phase); }

        private static void KillAtNight(MafiaMatch m, string victimId)
        {
            foreach (var mafia in All(m, Role.Mafia)) Assert.AreEqual(ActionResult.Ok, m.MafiaVote(mafia.Id, victimId));
            m.Advance();   // night -> dawn
        }

        // ------------------------------------------------------------------ roles

        [TestCase(4, 1, 1, 1, 1)]
        [TestCase(6, 1, 1, 1, 3)]
        [TestCase(8, 2, 1, 1, 4)]
        [TestCase(12, 3, 1, 1, 7)]
        [TestCase(16, 4, 1, 1, 10)]
        public void RoleCounts_follow_one_mafia_per_four(int n, int mafia, int doctor, int sheriff, int civilians)
        {
            var c = MafiaMatch.RoleCounts(n, new MatchConfig());
            Assert.AreEqual(mafia, c[Role.Mafia]); Assert.AreEqual(doctor, c[Role.Doctor]);
            Assert.AreEqual(sheriff, c[Role.Sheriff]); Assert.AreEqual(civilians, c[Role.Civilian]);
            Assert.AreEqual(n, c.Values.Sum());
        }

        [Test]
        public void RoleCounts_respect_a_config_without_special_roles()
        {
            var c = MafiaMatch.RoleCounts(8, new MatchConfig { HasDoctor = false, HasSheriff = false });
            Assert.AreEqual(0, c[Role.Doctor]); Assert.AreEqual(0, c[Role.Sheriff]); Assert.AreEqual(6, c[Role.Civilian]);
        }

        [Test]
        public void Start_needs_the_allowed_player_count()
        {
            var tooFew = new MafiaMatch(new MatchConfig(), Enumerable.Range(0, 3).Select(i => new KeyValuePair<string, string>("p" + i, "x")), new First());
            Assert.IsFalse(tooFew.Start());
            Assert.AreEqual(Phase.Lobby, tooFew.Phase);
            var tooMany = new MafiaMatch(new MatchConfig(), Enumerable.Range(0, 17).Select(i => new KeyValuePair<string, string>("p" + i, "x")), new First());
            Assert.IsFalse(tooMany.Start());
        }

        [Test]
        public void Start_hands_out_exactly_the_counted_roles()
        {
            var m = NewMatch(10);
            var c = MafiaMatch.RoleCounts(10, m.Config);
            foreach (Role r in new[] { Role.Mafia, Role.Doctor, Role.Sheriff, Role.Civilian }) Assert.AreEqual(c[r], All(m, r).Count, r.ToString());
            Assert.AreEqual(Phase.RoleReveal, m.Phase);
        }

        [Test]
        public void Start_with_the_system_random_still_gives_valid_roles()
        {
            var m = new MafiaMatch(new MatchConfig(), Enumerable.Range(0, 12).Select(i => new KeyValuePair<string, string>("p" + i, "x")), new SystemRandom(7));
            m.Start();
            Assert.AreEqual(3, All(m, Role.Mafia).Count);
            Assert.AreEqual(1, All(m, Role.Doctor).Count);
            Assert.AreEqual(1, All(m, Role.Sheriff).Count);
            Assert.AreEqual(7, All(m, Role.Civilian).Count);
        }

        // ------------------------------------------------------------------ night

        [Test]
        public void Night_actions_are_refused_outside_the_night()
        {
            var m = NewMatch(8);
            Assert.AreEqual(ActionResult.WrongPhase, m.MafiaVote(One(m, Role.Mafia).Id, Town(m).Id));
        }

        [Test]
        public void Only_the_right_role_can_use_a_night_action()
        {
            var m = NewMatch(8); ToNight(m);
            var civ = One(m, Role.Civilian);
            Assert.AreEqual(ActionResult.WrongRole, m.MafiaVote(civ.Id, One(m, Role.Doctor).Id));
            Assert.AreEqual(ActionResult.WrongRole, m.DoctorSave(civ.Id, civ.Id));
            Assert.AreEqual(ActionResult.WrongRole, m.SheriffCheck(civ.Id, One(m, Role.Mafia).Id, out _));
            Assert.AreEqual(ActionResult.NotAPlayer, m.MafiaVote("nobody", civ.Id));
        }

        [Test]
        public void The_mafia_cannot_target_their_own()
        {
            var m = NewMatch(8); ToNight(m);
            var mafia = All(m, Role.Mafia);
            Assert.AreEqual(ActionResult.MafiaTeammate, m.MafiaVote(mafia[0].Id, mafia[1].Id));
            Assert.AreEqual(ActionResult.InvalidTarget, m.MafiaVote(mafia[0].Id, mafia[0].Id));
            Assert.AreEqual(ActionResult.InvalidTarget, m.MafiaVote(mafia[0].Id, "ghost"));
        }

        [Test]
        public void An_unprotected_victim_dies_and_the_role_is_shown()
        {
            var m = NewMatch(8); ToNight(m);
            var victim = One(m, Role.Civilian);
            KillAtNight(m, victim.Id);
            Assert.AreEqual(Phase.Dawn, m.Phase);
            Assert.AreEqual(victim.Id, m.LastNight.DiedId);
            Assert.AreEqual(Role.Civilian, m.LastNight.DiedRole);
            Assert.IsFalse(victim.Alive);
        }

        [Test]
        public void The_role_stays_hidden_when_the_config_says_so()
        {
            var m = NewMatch(8, new MatchConfig { RevealRoleOnDeath = false }); ToNight(m);
            KillAtNight(m, One(m, Role.Civilian).Id);
            Assert.IsNotNull(m.LastNight.DiedId);
            Assert.IsNull(m.LastNight.DiedRole);
        }

        [Test]
        public void The_doctor_saving_the_victim_means_nobody_dies()
        {
            var m = NewMatch(8); ToNight(m);
            var victim = One(m, Role.Civilian);
            Assert.AreEqual(ActionResult.Ok, m.DoctorSave(One(m, Role.Doctor).Id, victim.Id));
            KillAtNight(m, victim.Id);
            Assert.IsTrue(m.LastNight.Saved);
            Assert.IsTrue(m.LastNight.NobodyDied);
            Assert.IsTrue(victim.Alive);
        }

        [Test]
        public void Saving_somebody_else_does_not_help_the_victim()
        {
            var m = NewMatch(8); ToNight(m);
            var civs = All(m, Role.Civilian);
            m.DoctorSave(One(m, Role.Doctor).Id, civs[1].Id);
            KillAtNight(m, civs[0].Id);
            Assert.AreEqual(civs[0].Id, m.LastNight.DiedId);
            Assert.IsFalse(m.LastNight.Saved);
        }

        [Test]
        public void The_doctor_can_save_himself_only_once()
        {
            var m = NewMatch(10); ToNight(m);
            var doc = One(m, Role.Doctor);
            Assert.AreEqual(ActionResult.Ok, m.DoctorSave(doc.Id, doc.Id));
            KillAtNight(m, One(m, Role.Civilian).Id);
            m.Advance(); m.Advance(); m.Advance(); m.Advance();   // dawn, day, voting, verdict
            Assert.AreEqual(Phase.Night, m.Phase);
            Assert.AreEqual(ActionResult.SelfSaveUsed, m.DoctorSave(doc.Id, doc.Id));
            Assert.AreEqual(ActionResult.Ok, m.DoctorSave(doc.Id, One(m, Role.Sheriff).Id));
        }

        [Test]
        public void Changing_the_doctors_mind_does_not_burn_the_self_save()
        {
            var m = NewMatch(10); ToNight(m);
            var doc = One(m, Role.Doctor);
            m.DoctorSave(doc.Id, doc.Id);
            m.DoctorSave(doc.Id, One(m, Role.Sheriff).Id);
            m.Advance();
            Assert.AreEqual(0, doc.SelfSavesUsed);
        }

        [Test]
        public void The_sheriff_gets_the_truth_once_per_night()
        {
            var m = NewMatch(8); ToNight(m);
            var sheriff = One(m, Role.Sheriff);
            Assert.AreEqual(ActionResult.InvalidTarget, m.SheriffCheck(sheriff.Id, sheriff.Id, out _));
            Assert.AreEqual(ActionResult.Ok, m.SheriffCheck(sheriff.Id, One(m, Role.Mafia).Id, out var r1));
            Assert.IsTrue(r1.IsMafia);
            Assert.AreEqual(ActionResult.AlreadyDone, m.SheriffCheck(sheriff.Id, One(m, Role.Civilian).Id, out _));
        }

        [Test]
        public void The_sheriff_can_check_again_the_next_night()
        {
            var m = NewMatch(10); ToNight(m);
            var sheriff = One(m, Role.Sheriff);
            m.SheriffCheck(sheriff.Id, One(m, Role.Civilian).Id, out var r1);
            Assert.IsFalse(r1.IsMafia);
            KillAtNight(m, All(m, Role.Civilian)[1].Id);
            m.Advance(); m.Advance(); m.Advance(); m.Advance();
            Assert.AreEqual(ActionResult.Ok, m.SheriffCheck(sheriff.Id, One(m, Role.Mafia).Id, out var r2));
            Assert.IsTrue(r2.IsMafia);
        }

        [Test]
        public void The_most_voted_victim_wins_among_the_mafia()
        {
            var m = NewMatch(12); ToNight(m);
            var mafia = All(m, Role.Mafia); var civs = All(m, Role.Civilian);
            m.MafiaVote(mafia[0].Id, civs[0].Id); m.MafiaVote(mafia[1].Id, civs[1].Id); m.MafiaVote(mafia[2].Id, civs[1].Id);
            m.Advance();
            Assert.AreEqual(civs[1].Id, m.LastNight.DiedId);
        }

        [Test]
        public void A_tie_among_the_mafia_is_broken_by_the_random_source()
        {
            var m = NewMatch(8); ToNight(m);
            var mafia = All(m, Role.Mafia); var civs = All(m, Role.Civilian);
            m.MafiaVote(mafia[0].Id, civs[0].Id); m.MafiaVote(mafia[1].Id, civs[1].Id);
            m.Advance();
            var expected = new[] { civs[0].Id, civs[1].Id }.OrderBy(x => x, System.StringComparer.Ordinal).First();   // First random picks index 0 of the ordered leaders
            Assert.AreEqual(expected, m.LastNight.DiedId);
        }

        [Test]
        public void Without_a_mafia_vote_nobody_is_killed()
        {
            var m = NewMatch(8); ToNight(m);
            m.Advance();
            Assert.IsTrue(m.LastNight.NobodyDied);
            Assert.IsNull(m.LastNight.VictimId);
        }

        [Test]
        public void The_night_can_end_early_only_when_every_night_role_acted()
        {
            var m = NewMatch(8); ToNight(m);
            Assert.IsFalse(m.NightActionsComplete());
            foreach (var mafia in All(m, Role.Mafia)) m.MafiaVote(mafia.Id, One(m, Role.Civilian).Id);
            Assert.IsFalse(m.NightActionsComplete());
            m.DoctorSave(One(m, Role.Doctor).Id, One(m, Role.Doctor).Id);
            Assert.IsFalse(m.NightActionsComplete());
            m.SheriffCheck(One(m, Role.Sheriff).Id, One(m, Role.Mafia).Id, out _);
            Assert.IsTrue(m.NightActionsComplete());
        }

        // ------------------------------------------------------------------ day and voting

        private static void ToVoting(MafiaMatch m)
        {
            ToNight(m); m.Advance();                       // night -> dawn
            if (m.Phase == Phase.Dawn) m.Advance();        // dawn -> day
            Assert.AreEqual(Phase.Day, m.Phase);
            m.Advance();                                   // day -> voting
            Assert.AreEqual(Phase.Voting, m.Phase);
        }

        [Test]
        public void Skipping_the_discussion_needs_more_than_half_of_the_living()
        {
            var m = NewMatch(8); ToNight(m); m.Advance(); m.Advance();
            Assert.AreEqual(Phase.Day, m.Phase);
            for (int i = 0; i < 3; i++) Assert.IsFalse(m.RequestSkip("p" + i));
            Assert.IsFalse(m.RequestSkip("p3"));     // 4 of 8 is not more than half
            Assert.IsTrue(m.RequestSkip("p4"));
            Assert.IsTrue(m.SkipReached);
        }

        [Test]
        public void Voting_removes_the_player_with_the_most_votes_and_shows_his_role()
        {
            var m = NewMatch(8); ToVoting(m);
            var target = Town(m);
            foreach (var p in m.InPlay.Where(x => x.Id != target.Id).Take(4)) Assert.AreEqual(ActionResult.Ok, m.Vote(p.Id, target.Id));
            m.Advance();
            Assert.AreEqual(Phase.Verdict, m.Phase);
            Assert.AreEqual(target.Id, m.LastVerdict.RemovedId);
            Assert.AreEqual(target.Role, m.LastVerdict.RemovedRole);
            Assert.AreEqual(4, m.LastVerdict.Votes);
            Assert.IsFalse(target.Alive);
        }

        [Test]
        public void A_tie_in_the_vote_removes_nobody()
        {
            var m = NewMatch(8); ToVoting(m);
            var live = m.InPlay.ToList();
            m.Vote(live[0].Id, live[2].Id); m.Vote(live[1].Id, live[3].Id);
            m.Advance();
            Assert.IsTrue(m.LastVerdict.NobodyRemoved);
            Assert.AreEqual(8, m.InPlayCount);
        }

        [Test]
        public void Only_abstentions_remove_nobody()
        {
            var m = NewMatch(8); ToVoting(m);
            foreach (var p in m.InPlay) m.Vote(p.Id, null);
            Assert.IsTrue(m.AllVoted());
            m.Advance();
            Assert.IsTrue(m.LastVerdict.NobodyRemoved);
            Assert.AreEqual(8, m.LastVerdict.Abstained);
        }

        [Test]
        public void A_player_can_change_his_vote_but_not_vote_for_himself_or_the_dead()
        {
            var m = NewMatch(8); ToVoting(m);
            var live = m.InPlay.ToList();
            Assert.AreEqual(ActionResult.InvalidTarget, m.Vote(live[0].Id, live[0].Id));
            Assert.AreEqual(ActionResult.InvalidTarget, m.Vote(live[0].Id, "ghost"));
            m.Vote(live[0].Id, live[1].Id); m.Vote(live[0].Id, live[2].Id);
            Assert.AreEqual(1, m.CurrentTally()[live[2].Id]);
            Assert.IsFalse(m.CurrentTally().ContainsKey(live[1].Id));
        }

        [Test]
        public void The_dead_cannot_vote()
        {
            var m = NewMatch(8); ToNight(m);
            var victim = One(m, Role.Civilian);
            KillAtNight(m, victim.Id); m.Advance(); m.Advance();
            Assert.AreEqual(Phase.Voting, m.Phase);
            Assert.AreEqual(ActionResult.DeadActor, m.Vote(victim.Id, One(m, Role.Mafia).Id));
        }

        // ------------------------------------------------------------------ the end

        [Test]
        public void The_town_wins_when_the_last_mafia_is_voted_out()
        {
            var m = NewMatch(4); ToVoting(m);
            var mafia = One(m, Role.Mafia);
            foreach (var p in m.InPlay.Where(x => x.Id != mafia.Id)) m.Vote(p.Id, mafia.Id);
            m.Advance();
            Assert.AreEqual(Phase.Verdict, m.Phase);
            Assert.AreEqual(Winner.Town, m.Winner);
            m.Advance();
            Assert.AreEqual(Phase.GameOver, m.Phase);
        }

        [Test]
        public void The_mafia_win_when_they_equal_the_rest()
        {
            var m = NewMatch(4); ToNight(m);
            KillAtNight(m, One(m, Role.Civilian).Id);          // 1 mafia vs doctor + sheriff
            Assert.AreEqual(Winner.None, m.Winner);
            m.Advance(); m.Advance();                          // dawn -> day -> voting
            Assert.AreEqual(Phase.Voting, m.Phase);
            var doctor = One(m, Role.Doctor);
            m.Vote(One(m, Role.Mafia).Id, doctor.Id);
            m.Vote(One(m, Role.Sheriff).Id, doctor.Id);
            m.Advance();                                       // verdict: doctor removed -> mafia 1 vs sheriff 1
            Assert.AreEqual(Winner.Mafia, m.Winner);
            m.Advance();
            Assert.AreEqual(Phase.GameOver, m.Phase);
        }

        [Test]
        public void A_night_kill_that_decides_the_match_still_shows_the_morning_report_first()
        {
            var m = NewMatch(4); ToNight(m);
            var civ = One(m, Role.Civilian);
            // remove the civilian and the doctor by hand to put the match one kill from the end
            One(m, Role.Doctor).Alive = false; civ.Alive = false;
            KillAtNight(m, One(m, Role.Sheriff).Id);
            Assert.AreEqual(Phase.Dawn, m.Phase);
            Assert.AreEqual(Winner.Mafia, m.Winner);
            m.Advance();
            Assert.AreEqual(Phase.GameOver, m.Phase);
        }

        [Test]
        public void Rounds_count_up_with_every_night()
        {
            var m = NewMatch(12); ToNight(m);
            Assert.AreEqual(1, m.Round);
            for (int i = 0; i < 5; i++) m.Advance();   // dawn, day, voting, verdict, night
            Assert.AreEqual(Phase.Night, m.Phase);
            Assert.AreEqual(2, m.Round);
        }

        [Test]
        public void Phase_durations_come_from_the_config()
        {
            var m = NewMatch(6, new MatchConfig { NightSeconds = 30f });
            ToNight(m);
            Assert.AreEqual(30f, m.PhaseSeconds);
        }

        // ------------------------------------------------------------------ leaving

        [Test]
        public void A_player_who_leaves_counts_as_dead_for_the_rules()
        {
            var m = NewMatch(8); ToNight(m);
            var civ = One(m, Role.Civilian);
            m.PlayerLeft(civ.Id);
            Assert.AreEqual(7, m.InPlayCount);
            Assert.IsFalse(civ.InPlay);
            m.Advance(); m.Advance(); m.Advance();   // night -> dawn -> day -> voting
            Assert.AreEqual(Phase.Voting, m.Phase);
            Assert.AreEqual(ActionResult.DeadActor, m.Vote(civ.Id, null));
        }

        [Test]
        public void The_town_wins_when_the_last_mafia_leaves()
        {
            var m = NewMatch(4); ToNight(m);
            m.PlayerLeft(One(m, Role.Mafia).Id);
            Assert.AreEqual(Winner.Town, m.Winner);
            Assert.AreEqual(Phase.GameOver, m.Phase);
        }

        [Test]
        public void A_leaving_player_cannot_be_the_mafia_victim()
        {
            var m = NewMatch(8); ToNight(m);
            var civ = One(m, Role.Civilian);
            m.PlayerLeft(civ.Id);
            Assert.AreEqual(ActionResult.InvalidTarget, m.MafiaVote(One(m, Role.Mafia).Id, civ.Id));
        }

        [Test]
        public void Summary_reports_the_winner_and_the_rounds()
        {
            var m = NewMatch(4); ToNight(m);
            m.PlayerLeft(One(m, Role.Mafia).Id);
            var s = m.Summary();
            Assert.AreEqual(Winner.Town, s.Winner);
            Assert.AreEqual(1, s.Rounds);
            Assert.AreEqual(4, s.Players.Count);
        }
    }
}
