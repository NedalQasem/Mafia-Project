using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    /// <summary>What a device tells the rewards server at the end of a match.</summary>
    public class MatchReportTests
    {
        private sealed class First : IRandom { public int Next(int max) => 0; }

        private sealed class Wire : IMatchTransport
        {
            public readonly List<KeyValuePair<string, WireMessage>> Sent = new List<KeyValuePair<string, WireMessage>>();
            public event Action<string, WireMessage> Received;
            public void Send(string peer, WireMessage m) { Sent.Add(new KeyValuePair<string, WireMessage>(peer, m)); }
        }

        private static (HostMatchController host, Wire wire, Dictionary<string, ClientMatchController> clients, MafiaMatch match) Play(int n, string matchId = "0123456789abcdef")
        {
            var players = Enumerable.Range(0, n).Select(i => new KeyValuePair<string, string>("p" + i, "Player " + i)).ToList();
            var match = new MafiaMatch(new MatchConfig(), players, new First());
            var wire = new Wire();
            var clients = players.ToDictionary(p => p.Key, p => new ClientMatchController(p.Key, _ => { }));
            var host = new HostMatchController(match, wire, "p0", m => clients["p0"].Apply(WireMessage.FromBytes(m.ToBytes())));
            var roster = players.ToDictionary(p => p.Key, p => "PF" + p.Key.ToUpper() + "000000");
            host.Begin(matchId, roster);
            // deliver everything the host sent to the other players, through the wire encoding
            Deliver(wire, clients);
            return (host, wire, clients, match);
        }

        private static void Deliver(Wire wire, Dictionary<string, ClientMatchController> clients)
        {
            foreach (var kv in wire.Sent.ToList()) if (clients.TryGetValue(kv.Key, out var c)) c.Apply(WireMessage.FromBytes(kv.Value.ToBytes()));
            wire.Sent.Clear();
        }

        private static void FinishWithTownWin(HostMatchController host, MafiaMatch match, Wire wire, Dictionary<string, ClientMatchController> clients)
        {
            foreach (var p in match.Players.Where(p => p.IsMafia)) host.PlayerLeft(p.Id);     // all mafia gone: the town wins
            for (int i = 0; i < 6 && host.Running; i++) { host.Tick(200f); Deliver(wire, clients); }
            Deliver(wire, clients);
        }

        [Test]
        public void A_table_with_too_few_real_accounts_sends_no_report()
        {
            // five players but only three of them have an account (the rest are bots with an empty PlayFab id): the server refuses fewer than four players
            var players = Enumerable.Range(0, 5).Select(i => new KeyValuePair<string, string>("p" + i, "Player " + i)).ToList();
            var match = new MafiaMatch(new MatchConfig(), players, new First());
            var wire = new Wire();
            var clients = players.ToDictionary(p => p.Key, p => new ClientMatchController(p.Key, _ => { }));
            var host = new HostMatchController(match, wire, "p0", m => clients["p0"].Apply(WireMessage.FromBytes(m.ToBytes())));
            var roster = players.ToDictionary(p => p.Key, p => int.Parse(p.Key.Substring(1)) < 3 ? "PF" + p.Key.ToUpper() + "000000" : "");
            host.Begin("0123456789abcdef", roster);
            Deliver(wire, clients);
            FinishWithTownWin(host, match, wire, clients);
            foreach (var p in match.Players.Where(p => !p.IsMafia && p.Id != "p0")) Assert.IsNull(clients[p.Id].BuildReport(), p.Id + ": only three accounts");
        }

        [Test]
        public void Every_device_learns_the_match_id_and_who_is_who_at_the_start()
        {
            var (host, wire, clients, match) = Play(6);
            foreach (var c in clients.Values)
            {
                Assert.AreEqual("0123456789abcdef", c.MatchId);
                Assert.AreEqual(6, c.Roster.Count);
                Assert.AreEqual("PFP3000000", c.Roster["p3"]);
            }
        }

        [Test]
        public void A_finished_match_gives_each_player_a_report_with_his_own_role()
        {
            var (host, wire, clients, match) = Play(6);
            FinishWithTownWin(host, match, wire, clients);
            foreach (var p in match.Players.Where(p => !p.IsMafia && p.Id != "p0"))
            {
                var r = clients[p.Id].BuildReport();
                Assert.IsNotNull(r, p.Id);
                Assert.AreEqual("0123456789abcdef", r.MatchId);
                Assert.AreEqual("Town", r.Winner);
                Assert.AreEqual(p.Role.ToString(), r.MyRole);
                Assert.AreEqual(6, r.PlayFabIds.Count);
                Assert.IsTrue(r.PlayFabIds.Contains("PF" + p.Id.ToUpper() + "000000"));
                Assert.GreaterOrEqual(r.Rounds, 1);
            }
        }

        [Test]
        public void Everybody_reports_the_same_player_list_and_winner()
        {
            var (host, wire, clients, match) = Play(8);
            FinishWithTownWin(host, match, wire, clients);
            var reports = match.Players.Where(p => !p.IsMafia).Select(p => clients[p.Id].BuildReport()).Where(r => r != null).ToList();
            Assert.GreaterOrEqual(reports.Count, 5);
            var first = reports[0];
            foreach (var r in reports)
            {
                CollectionAssert.AreEquivalent(first.PlayFabIds, r.PlayFabIds);
                Assert.AreEqual(first.Winner, r.Winner);
            }
        }

        [Test]
        public void A_player_who_was_killed_still_reports_and_is_marked_as_not_surviving()
        {
            var (host, wire, clients, match) = Play(6);
            var victim = match.Players.First(p => !p.IsMafia && p.Id != "p0").Id;
            match.Get(victim).Alive = false;
            FinishWithTownWin(host, match, wire, clients);
            var r = clients[victim].BuildReport();
            Assert.IsNotNull(r);
            Assert.IsFalse(r.Survived);
        }

        [Test]
        public void Nothing_is_reported_before_the_end_or_without_a_match_id_or_for_a_watcher()
        {
            var (host, wire, clients, match) = Play(6);
            Assert.IsNull(clients["p1"].BuildReport(), "match still running");

            var (host2, wire2, clients2, match2) = Play(6, "");
            FinishWithTownWin(host2, match2, wire2, clients2);
            Assert.IsNull(clients2["p1"].BuildReport(), "practice matches have no id");

            var (host3, wire3, clients3, match3) = Play(6);
            var watcher = new ClientMatchController("late", _ => { });
            clients3["late"] = watcher;
            host3.Tick(match3.Config.RoleRevealSeconds + 0.1f); Deliver(wire3, clients3);
            host3.AddSpectator("late"); Deliver(wire3, clients3);
            FinishWithTownWin(host3, match3, wire3, clients3);
            Assert.IsNull(watcher.BuildReport(), "a spectator has no role to report");
        }

        [Test]
        public void A_new_host_after_a_hand_over_keeps_the_match_id_and_the_roster()
        {
            var (host, wire, clients, match) = Play(6);
            var data = host.ExportState();
            var wire2 = new Wire();
            var resumed = HostMatchController.Resume(data, new MatchConfig(), new First(), wire2, "p1", _ => { });
            Assert.IsNotNull(resumed);
            resumed.AddSpectator("late");
            var start = wire2.Sent.First(s => s.Key == "late" && s.Value.Type == Msg.Start).Value;
            Assert.AreEqual("0123456789abcdef", start.Arg(0));
            Assert.AreEqual(1 + 6 * 2, start.Args.Length, "match id + a (player, PlayFab id) pair for each of the 6 players");
        }
    }
}
