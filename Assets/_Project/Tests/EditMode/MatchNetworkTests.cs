using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    /// <summary>The host and the players talking through an in-memory network, to check the protocol and who learns what.</summary>
    public class MatchNetworkTests
    {
        private sealed class First : IRandom { public int Next(int max) => 0; }

        private sealed class Node : IMatchTransport
        {
            public readonly string Id; public readonly Hub Hub;
            public Node(string id, Hub hub) { Id = id; Hub = hub; }
            public event Action<string, WireMessage> Received;
            public void Send(string peer, WireMessage m) { Hub.Log.Add(Id + "->" + peer + " " + m); Hub.Deliver(Id, peer, m); }
            public void Raise(string from, WireMessage m) { Received?.Invoke(from, m); }
        }

        private sealed class Hub
        {
            public readonly Dictionary<string, Node> Nodes = new Dictionary<string, Node>();
            public readonly Dictionary<string, ClientMatchController> Clients = new Dictionary<string, ClientMatchController>();
            public readonly List<string> Log = new List<string>();
            public HostMatchController Host;
            public readonly Dictionary<string, List<WireMessage>> Inbox = new Dictionary<string, List<WireMessage>>();

            public void Deliver(string from, string to, WireMessage m)
            {
                // pass through the wire encoding like the real network would
                var wire = WireMessage.FromBytes(m.ToBytes());
                if (to == "p0" && Host != null) Host.Receive(from, wire);                 // acts go to the host
                else { Inbox[to].Add(wire); Clients[to].Apply(wire); }
            }
        }

        private static (Hub hub, HostMatchController host) Setup(int n, MatchConfig cfg = null)
        {
            var hub = new Hub();
            var players = Enumerable.Range(0, n).Select(i => new KeyValuePair<string, string>("p" + i, "Player " + i)).ToList();
            var match = new MafiaMatch(cfg ?? new MatchConfig(), players, new First());
            foreach (var p in players)
            {
                hub.Nodes[p.Key] = new Node(p.Key, hub);
                hub.Inbox[p.Key] = new List<WireMessage>();
                string id = p.Key;
                hub.Clients[id] = new ClientMatchController(id, m => { if (id == "p0") hub.Host.Receive("p0", WireMessage.FromBytes(m.ToBytes())); else hub.Host.Receive(id, WireMessage.FromBytes(m.ToBytes())); });
            }
            // the host is player p0: his own messages are handed to his client directly
            var host = new HostMatchController(match, hub.Nodes["p0"], "p0", m => { hub.Inbox["p0"].Add(m); hub.Clients["p0"].Apply(WireMessage.FromBytes(m.ToBytes())); });
            hub.Host = host;
            return (hub, host);
        }

        private static string IdOf(MafiaMatch m, Role r) => m.Players.First(p => p.Role == r).Id;

        // ------------------------------------------------------------------ wire format

        [Test]
        public void A_message_survives_the_wire()
        {
            var m = new WireMessage("state", "Night", "2", "30.5", "p1", WireMessage.Clean("Abu\u001fSalem\nx"), "1");
            var back = WireMessage.FromBytes(m.ToBytes());
            Assert.AreEqual("state", back.Type);
            Assert.AreEqual(6, back.Args.Length);
            Assert.AreEqual("Abu Salem x", back.Arg(4));
            Assert.AreEqual(30.5f, back.Float(2));
            Assert.AreEqual(2, back.Int(1));
        }

        [Test]
        public void Arabic_names_and_empty_arguments_survive_the_wire()
        {
            var back = WireMessage.FromBytes(new WireMessage("dawn", "", "0", "", "").ToBytes());
            Assert.AreEqual("dawn", back.Type);
            Assert.AreEqual(4, back.Args.Length);
            var name = WireMessage.FromBytes(new WireMessage("x", "أبو سالم").ToBytes());
            Assert.AreEqual("أبو سالم", name.Arg(0));
        }

        // ------------------------------------------------------------------ secrets

        [Test]
        public void Begin_tells_every_player_only_his_own_role()
        {
            var (hub, host) = Setup(8);
            Assert.IsTrue(host.Begin());
            var match = host.Match;
            foreach (var p in match.Players)
            {
                var c = hub.Clients[p.Id];
                Assert.AreEqual(p.Role, c.MyRole, p.Id);
                // what he knows about the others: nothing, unless he is mafia (then the other mafia)
                foreach (var seat in c.Seats.Where(s => !s.IsMe))
                {
                    bool knows = p.IsMafia && match.Get(seat.Id).IsMafia;
                    Assert.AreEqual(knows ? (Role?)Role.Mafia : null, seat.Role, p.Id + " about " + seat.Id);
                }
            }
        }

        [Test]
        public void No_role_message_ever_reaches_the_wrong_player()
        {
            var (hub, host) = Setup(8);
            host.Begin();
            foreach (var kv in hub.Inbox)
            {
                var roles = kv.Value.Where(m => m.Type == Msg.Role).ToList();
                Assert.AreEqual(1, roles.Count, kv.Key);
                Assert.AreEqual(host.Match.Get(kv.Key).Role.ToString(), roles[0].Arg(0));
            }
        }

        [Test]
        public void Only_the_sheriff_gets_the_sheriffs_answer()
        {
            var (hub, host) = Setup(8);
            host.Begin(); host.Match.Advance(); host.Tick(0f);   // jump straight to the night
            // the controller's own phase moved with the engine call above only for the engine; re-enter through the controller instead
            var (hub2, host2) = Setup(8);
            host2.Begin();
            host2.Tick(host2.Match.Config.RoleRevealSeconds + 0.1f);   // role reveal ends -> night
            Assert.AreEqual(Phase.Night, host2.Match.Phase);
            string sheriff = IdOf(host2.Match, Role.Sheriff), mafia = IdOf(host2.Match, Role.Mafia);
            HostNightKit.ToStep(host2, NightStep.Sheriff);
            hub2.Clients[sheriff].SheriffCheck(mafia);
            Assert.AreEqual(1, hub2.Clients[sheriff].SheriffReports.Count);
            Assert.IsTrue(hub2.Clients[sheriff].SheriffReports[0].Value);
            foreach (var kv in hub2.Inbox.Where(k => k.Key != sheriff)) Assert.IsFalse(kv.Value.Any(m => m.Type == Msg.Sheriff), kv.Key);
            Assert.AreEqual(Role.Mafia, hub2.Clients[sheriff].Seat(mafia).Role);
        }

        [Test]
        public void The_mafia_votes_are_shown_to_the_mafia_only()
        {
            var (hub, host) = Setup(8);
            host.Begin(); host.Tick(host.Match.Config.RoleRevealSeconds + 0.1f);
            var mafia = host.Match.Players.Where(p => p.IsMafia).ToList();
            string victim = IdOf(host.Match, Role.Civilian);
            HostNightKit.ToStep(host, NightStep.Mafia);
            hub.Clients[mafia[0].Id].MafiaPick(victim);
            foreach (var m in mafia) Assert.AreEqual(victim, hub.Clients[m.Id].MafiaVotes[mafia[0].Id]);
            foreach (var p in host.Match.Players.Where(p => !p.IsMafia)) Assert.IsFalse(hub.Inbox[p.Id].Any(x => x.Type == Msg.MafiaVotes), p.Id);
        }

        // ------------------------------------------------------------------ flow

        [Test]
        public void An_illegal_act_gets_an_answer_and_changes_nothing()
        {
            var (hub, host) = Setup(8);
            host.Begin(); host.Tick(host.Match.Config.RoleRevealSeconds + 0.1f);
            string civ = IdOf(host.Match, Role.Civilian), doctor = IdOf(host.Match, Role.Doctor);
            hub.Clients[civ].MafiaPick(doctor);
            Assert.AreEqual("mafia", hub.Clients[civ].LastResultKind);
            Assert.AreEqual(ActionResult.WrongRole, hub.Clients[civ].LastResult);
            hub.Clients[civ].Vote(doctor);   // not the voting phase
            Assert.AreEqual(ActionResult.WrongPhase, hub.Clients[civ].LastResult);
        }

        [Test]
        public void The_timer_moves_the_match_and_the_players_follow()
        {
            var (hub, host) = Setup(8);
            host.Begin();
            Assert.AreEqual(Phase.RoleReveal, hub.Clients["p3"].Phase);
            host.Tick(host.Match.Config.RoleRevealSeconds + 0.1f);
            Assert.AreEqual(Phase.Night, hub.Clients["p3"].Phase);
            Assert.AreEqual(1, hub.Clients["p3"].Round);
            HostNightKit.FinishNight(host);
            Assert.AreEqual(Phase.Dawn, hub.Clients["p3"].Phase);
            Assert.IsNull(hub.Clients["p3"].DawnDiedId);   // nobody voted for a victim
        }

        [Test]
        public void Each_role_acts_in_its_own_call_and_the_night_never_ends_early()
        {
            var (hub, host) = Setup(8);
            host.Begin(); host.Tick(host.Match.Config.RoleRevealSeconds + 0.1f);
            var match = host.Match;
            string victim = IdOf(match, Role.Civilian);
            string doctor = IdOf(match, Role.Doctor), sheriff = IdOf(match, Role.Sheriff);
            hub.Clients[doctor].DoctorSave(doctor);                               // the doctor is asleep: refused
            Assert.AreEqual(ActionResult.WrongStep, hub.Clients[doctor].LastResult);
            HostNightKit.ToStep(host, NightStep.Mafia);
            foreach (var m in match.Players.Where(p => p.IsMafia)) hub.Clients[m.Id].MafiaPick(victim);
            HostNightKit.ToStep(host, NightStep.Doctor);
            hub.Clients[doctor].DoctorSave(doctor);
            Assert.AreEqual(ActionResult.Ok, hub.Clients[doctor].LastResult);
            HostNightKit.ToStep(host, NightStep.Sheriff);
            hub.Clients[sheriff].SheriffCheck(IdOf(match, Role.Mafia));
            host.Tick(1f); host.Tick(1.5f);
            Assert.AreEqual(Phase.Night, match.Phase, "everybody who could act has acted, and still the night goes on to the end of the calls");
            HostNightKit.FinishNight(host);
            Assert.AreEqual(Phase.Dawn, match.Phase);
            Assert.AreEqual(victim, hub.Clients["p5"].DawnDiedId);
            Assert.AreEqual(Role.Civilian, hub.Clients["p5"].DawnDiedRole);
            Assert.AreEqual(Role.Civilian, hub.Clients["p5"].Seat(victim).Role);   // the role became public
        }

        [Test]
        public void Everybody_sees_which_role_is_called_and_when_everybody_sleeps()
        {
            var (hub, host) = Setup(8);
            host.Begin(); host.Tick(host.Match.Config.RoleRevealSeconds + 0.1f);
            var cl = hub.Clients["p2"];
            Assert.IsNull(cl.ActiveStep, "the night opens: nobody is called yet");
            HostNightKit.ToStep(host, NightStep.Doctor);
            foreach (var c in hub.Clients.Values) Assert.AreEqual(NightStep.Doctor, c.ActiveStep, "the banner is public");
            CollectionAssert.AreEqual(host.Match.NightSteps, cl.NightSteps);
            for (int i = 0; i < 40 && cl.ActiveStep == NightStep.Doctor; i++) host.Tick(0.5f);
            Assert.IsNull(cl.ActiveStep, "after the call everybody sleeps for a moment");
            Assert.IsFalse(cl.MyTurn);
            string doctor = IdOf(host.Match, Role.Doctor);
            HostNightKit.ToStep(host, NightStep.Sheriff);
            Assert.IsFalse(hub.Clients[doctor].MyTurn, "his call is over");
            Assert.IsTrue(hub.Clients[IdOf(host.Match, Role.Sheriff)].MyTurn);
        }

        [Test]
        public void The_roles_of_the_match_are_told_to_everybody_at_the_start()
        {
            var (hub, host) = Setup(8);
            host.Begin();
            foreach (var c in hub.Clients.Values)
            {
                Assert.AreEqual(8, c.RoleCounts.Sum(r => r.Value), "the counts cover the whole table");
                Assert.IsTrue(c.RoleCounts.Any(r => r.Key == Role.Doctor && r.Value == 1));
            }
        }

        [Test]
        public void Voting_shows_a_live_tally_and_the_verdict_reaches_everybody()
        {
            var (hub, host) = Setup(8);
            host.Begin();
            var match = host.Match;
            HostNightKit.ToDay(host);
            Assert.AreEqual(Phase.Day, match.Phase);
            host.Tick(match.PhaseSeconds + 0.1f);
            Assert.AreEqual(Phase.Voting, match.Phase);
            string target = IdOf(match, Role.Civilian);
            foreach (var p in match.Players.Where(p => p.Id != target).Take(5)) hub.Clients[p.Id].Vote(target);
            Assert.AreEqual(5, hub.Clients["p1"].Tally[target]);
            host.Tick(match.PhaseSeconds + 0.1f);
            Assert.AreEqual(Phase.Verdict, hub.Clients["p6"].Phase);
            Assert.AreEqual(target, hub.Clients["p6"].VerdictRemovedId);
            Assert.AreEqual(Role.Civilian, hub.Clients["p6"].VerdictRemovedRole);
        }

        [Test]
        public void Skipping_the_discussion_by_majority_moves_on_to_voting()
        {
            var (hub, host) = Setup(8);
            host.Begin();
            var match = host.Match;
            HostNightKit.ToDay(host);
            Assert.AreEqual(Phase.Day, match.Phase);
            for (int i = 0; i < 5; i++) hub.Clients["p" + i].SkipDiscussion();
            Assert.AreEqual(5, hub.Clients["p7"].SkipCount);
            host.Tick(2.5f);
            Assert.AreEqual(Phase.Voting, match.Phase);
        }

        [Test]
        public void The_match_ends_and_everybody_learns_all_the_roles()
        {
            var (hub, host) = Setup(4);
            host.Begin();
            host.Tick(host.Match.PhaseSeconds + 0.1f);                              // night
            var match = host.Match;
            string mafia = IdOf(match, Role.Mafia);
            host.PlayerLeft(mafia);                                                 // the last mafia leaves
            Assert.AreEqual(Phase.GameOver, match.Phase);
            foreach (var p in match.Players.Where(p => p.Id != mafia))
            {
                var c = hub.Clients[p.Id];
                Assert.AreEqual(Winner.Town, c.Winner, p.Id);
                foreach (var other in match.Players) Assert.AreEqual(other.Role, c.Seat(other.Id).Role, p.Id + " about " + other.Id);
            }
        }

        [Test]
        public void The_host_plays_through_the_same_path_as_everybody()
        {
            var (hub, host) = Setup(4);
            host.Begin();
            Assert.AreEqual(host.Match.Get("p0").Role, hub.Clients["p0"].MyRole);
            Assert.AreEqual(Phase.RoleReveal, hub.Clients["p0"].Phase);
            hub.Clients["p0"].Ready();
            Assert.AreEqual("ready", hub.Clients["p0"].LastResultKind);
            Assert.AreEqual(ActionResult.Ok, hub.Clients["p0"].LastResult);
        }

        private static ClientMatchController AddWatcher(Hub hub, HostMatchController host, string id)
        {
            hub.Nodes[id] = new Node(id, hub);
            hub.Inbox[id] = new List<WireMessage>();
            hub.Clients[id] = new ClientMatchController(id, m => host.Receive(id, WireMessage.FromBytes(m.ToBytes())));
            host.AddSpectator(id);
            return hub.Clients[id];
        }

        [Test]
        public void A_late_joiner_watches_the_table_without_a_role()
        {
            var (hub, host) = Setup(6);
            host.Begin();
            host.Tick(host.Match.Config.RoleRevealSeconds + 0.1f);   // night
            var w = AddWatcher(hub, host, "late");
            Assert.AreEqual(Phase.Night, w.Phase);
            Assert.IsNull(w.MyRole);
            Assert.IsFalse(w.IAmAlive);
            Assert.AreEqual(6, w.Seats.Count);
            Assert.IsFalse(hub.Inbox["late"].Any(m => m.Type == Msg.Role || m.Type == Msg.Sheriff || m.Type == Msg.MafiaVotes));
        }

        [Test]
        public void A_watcher_hears_the_rest_of_the_match_but_cannot_act()
        {
            var (hub, host) = Setup(4);
            host.Begin();
            host.Tick(host.Match.Config.RoleRevealSeconds + 0.1f);   // night
            var w = AddWatcher(hub, host, "late");
            string victim = host.Match.Players.First(p => !p.IsMafia).Id;
            HostNightKit.ToStep(host, NightStep.Mafia);
            w.MafiaPick(victim); w.DoctorSave(victim); w.Vote(victim); w.SkipDiscussion();
            Assert.IsFalse(host.Match.MafiaVotes.Any(), "a watcher's act must be ignored");
            HostNightKit.FinishNight(host);                              // dawn
            Assert.AreEqual(Phase.Dawn, w.Phase);
            Assert.AreEqual(1, w.Round);
        }

        [Test]
        public void A_watcher_who_leaves_does_not_end_anything()
        {
            var (hub, host) = Setup(4);
            host.Begin();
            host.Tick(host.Match.Config.RoleRevealSeconds + 0.1f);
            AddWatcher(hub, host, "late");
            int alive = host.Match.InPlayCount;
            host.PlayerLeft("late");
            Assert.AreEqual(alive, host.Match.InPlayCount);
            Assert.AreEqual(Phase.Night, host.Match.Phase);
        }

        // ------------------------------------------------------------------ a player who left comes back

        /// <summary>The same account on a fresh device session: a new client that knows nothing yet.</summary>
        private static ClientMatchController FreshClient(Hub hub, HostMatchController host, string id)
        {
            hub.Inbox[id] = new List<WireMessage>();
            hub.Clients[id] = new ClientMatchController(id, m => host.Receive(id, WireMessage.FromBytes(m.ToBytes())));
            return hub.Clients[id];
        }

        private static (Hub hub, HostMatchController host) AtNight(int n)
        {
            var (hub, host) = Setup(n);
            host.Begin();
            host.Tick(host.Match.Config.RoleRevealSeconds + 0.1f);
            return (hub, host);
        }

        [Test]
        public void A_player_who_comes_back_gets_his_seat_role_and_life_again()
        {
            var (hub, host) = AtNight(6);
            string id = host.Match.Players.First(p => p.Role == Role.Civilian && p.Id != "p0").Id;
            var role = host.Match.Get(id).Role;
            host.PlayerLeft(id);
            Assert.IsTrue(host.Match.Get(id).Left);
            var back = FreshClient(hub, host, id);
            host.AddSpectator(id);
            Assert.IsFalse(host.Match.Get(id).Left);
            Assert.IsTrue(host.Match.Get(id).Alive);
            Assert.AreEqual(role, back.MyRole);
            Assert.IsTrue(back.IAmAlive);
            Assert.AreEqual(Phase.Night, back.Phase);
            Assert.AreEqual(6, back.Seats.Count);
            Assert.IsFalse(hub.Clients["p0"].Seat(id).Left, "the others see him back");
        }

        [Test]
        public void A_mafia_player_who_comes_back_knows_his_mates_and_his_votes()
        {
            var (hub, host) = AtNight(8);
            var mafia = host.Match.Players.Where(p => p.IsMafia).Select(p => p.Id).ToList();
            string victim = host.Match.Players.First(p => !p.IsMafia).Id;
            string id = mafia.Last(x => x != "p0");
            HostNightKit.ToStep(host, NightStep.Mafia);
            hub.Clients[mafia.First()].MafiaPick(victim);
            host.PlayerLeft(id);
            var back = FreshClient(hub, host, id);
            host.AddSpectator(id);
            Assert.AreEqual(Role.Mafia, back.MyRole);
            Assert.IsTrue(back.Seats.Any(s => s.IsMate), "his mates are known again");
            Assert.IsTrue(back.MafiaVotes.ContainsKey(mafia.First()), "he sees what his mates picked");
        }

        [Test]
        public void A_player_who_was_dead_comes_back_dead_and_sees_who_died()
        {
            var (hub, host) = AtNight(8);
            var civs = host.Match.Players.Where(p => p.Role == Role.Civilian && p.Id != "p0").Select(p => p.Id).ToList();
            string dead = civs[0], other = civs[1];
            host.Match.Get(dead).Alive = false;          // killed earlier in the match
            host.Match.Get(other).Alive = false;
            host.PlayerLeft(other);
            var back = FreshClient(hub, host, other);
            host.AddSpectator(other);
            Assert.IsFalse(host.Match.Get(other).Left);
            Assert.IsFalse(host.Match.Get(other).Alive, "dead stays dead");
            Assert.IsFalse(back.IAmAlive);
            Assert.IsNotNull(back.MyRole);
            Assert.AreEqual(Role.Civilian, back.Seat(dead).Role, "the roles of the dead are public");
        }

        [Test]
        public void A_kicked_player_does_not_get_his_seat_back()
        {
            var (hub, host) = AtNight(6);
            string id = host.Match.Players.First(p => p.Role == Role.Civilian && p.Id != "p0").Id;
            host.Forbid(id);
            host.PlayerLeft(id);
            var back = FreshClient(hub, host, id);
            host.AddSpectator(id);
            Assert.IsTrue(host.Match.Get(id).Left);
            Assert.IsNull(back.MyRole);
            Assert.IsFalse(back.IAmAlive);
        }

        [Test]
        public void A_player_whose_connection_broke_and_came_back_is_back_when_he_acts()
        {
            var (hub, host) = AtNight(6);
            string id = host.Match.Players.First(p => p.Role == Role.Civilian && p.Id != "p0").Id;
            host.PlayerLeft(id);                          // the connection closed: he counts as gone
            Assert.IsTrue(host.Match.Get(id).Left);
            var c = hub.Clients[id];
            host.Receive(id, new WireMessage(Msg.Act, "ready", ""));
            Assert.IsFalse(host.Match.Get(id).Left);
            Assert.IsTrue(c.IAmAlive, "the host told him where the match stands");
        }

        [Test]
        public void Nobody_comes_back_into_a_finished_match()
        {
            var (hub, host) = AtNight(4);
            foreach (var m in host.Match.Players.Where(p => p.IsMafia).Select(p => p.Id).ToList()) if (m != "p0") host.PlayerLeft(m);
            if (host.Match.Get("p0").IsMafia) host.Match.PlayerLeft("p0");
            Assert.AreEqual(Phase.GameOver, host.Match.Phase);
            string left = host.Match.Players.First(p => p.Left).Id;
            Assert.IsFalse(host.Match.PlayerReturned(left));
        }

        [Test]
        public void The_snapshot_remembers_whether_a_player_who_left_was_alive()
        {
            var (hub, host) = AtNight(6);
            string id = host.Match.Players.First(p => p.Role == Role.Civilian && p.Id != "p0").Id;
            host.PlayerLeft(id);
            var back = MafiaMatch.Restore(new MatchConfig(), host.Match.Export(), new First());
            Assert.IsNotNull(back);
            Assert.IsTrue(back.PlayerReturned(id));
            Assert.IsTrue(back.Get(id).Alive);
        }

        [Test]
        public void A_host_cannot_start_a_match_that_is_too_small()
        {
            var (hub, host) = Setup(3);
            Assert.IsFalse(host.Begin());
            Assert.AreEqual(0, hub.Inbox["p1"].Count);
        }
    }
}
