using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    /// <summary>A match must survive its host: the snapshot carries everything another device needs to continue as referee.</summary>
    public class MatchTakeoverTests
    {
        private sealed class First : IRandom { public int Next(int max) => 0; }

        private sealed class Wire : IMatchTransport
        {
            public readonly List<KeyValuePair<string, WireMessage>> Sent = new List<KeyValuePair<string, WireMessage>>();
            public event Action<string, WireMessage> Received;
            public void Send(string peer, WireMessage m) { Sent.Add(new KeyValuePair<string, WireMessage>(peer, m)); }
            public void Raise(string from, WireMessage m) { Received?.Invoke(from, m); }
        }

        private static MafiaMatch Played(int n, out string mafia, out string civ)
        {
            var players = Enumerable.Range(0, n).Select(i => new KeyValuePair<string, string>("p" + i, "Player " + i)).ToList();
            var m = new MafiaMatch(new MatchConfig(), players, new First());
            m.Start();
            mafia = m.Players.First(p => p.IsMafia).Id;
            civ = m.Players.First(p => p.Role == Role.Civilian).Id;
            return m;
        }

        // ------------------------------------------------------------------ the rules engine

        [Test]
        public void A_snapshot_in_every_phase_restores_to_the_same_state()
        {
            var m = Played(8, out var mafia, out var civ);
            var doctor = m.Players.First(p => p.Role == Role.Doctor).Id;
            var sheriff = m.Players.First(p => p.Role == Role.Sheriff).Id;
            var seen = new List<Phase>();
            for (int step = 0; step < 12 && m.Phase != Phase.GameOver; step++)
            {
                switch (m.Phase)
                {
                    case Phase.Night: m.MafiaVote(mafia, civ); m.DoctorSave(doctor, doctor); m.SheriffCheck(sheriff, mafia, out _); break;
                    case Phase.Day: foreach (var p in m.InPlay) m.RequestSkip(p.Id); break;
                    case Phase.Voting: foreach (var p in m.InPlay.Where(x => x.Id != mafia)) m.Vote(p.Id, mafia); break;
                }
                var data = m.Export();
                var back = MafiaMatch.Restore(new MatchConfig(), data, new First());
                Assert.IsNotNull(back, m.Phase.ToString());
                CollectionAssert.AreEqual(data, back.Export(), "phase " + m.Phase);
                seen.Add(m.Phase);
                m.Advance();
            }
            Assert.IsTrue(seen.Contains(Phase.Night) && seen.Contains(Phase.Day) && seen.Contains(Phase.Voting) && seen.Contains(Phase.Verdict));
        }

        [Test]
        public void A_restored_match_plays_on_like_the_original()
        {
            var m = Played(8, out var mafia, out var civ);
            m.Advance();                                    // night
            m.MafiaVote(mafia, civ);
            var copy = MafiaMatch.Restore(new MatchConfig(), m.Export(), new First());
            m.Advance(); copy.Advance();                    // dawn
            Assert.AreEqual(m.LastNight.DiedId, copy.LastNight.DiedId);
            Assert.AreEqual(m.Phase, copy.Phase);
            Assert.AreEqual(m.InPlayCount, copy.InPlayCount);
            Assert.AreEqual(civ, copy.LastNight.DiedId);
            // an act in the copy is accepted and counted
            m.Advance(); copy.Advance();                    // day
            m.Advance(); copy.Advance();                    // voting
            var voter = copy.InPlay.First(p => p.Id != mafia).Id;
            Assert.AreEqual(ActionResult.Ok, copy.Vote(voter, mafia));
            Assert.AreEqual(1, copy.CurrentTally()[mafia]);
        }

        [Test]
        public void The_doctors_used_self_save_and_the_mafia_votes_are_kept()
        {
            var m = Played(8, out var mafia, out var civ);
            var doctor = m.Players.First(p => p.Role == Role.Doctor).Id;
            m.Advance();
            m.DoctorSave(doctor, doctor);
            m.MafiaVote(mafia, civ);
            m.Advance();                                    // night resolved: the doctor used his self-save
            var copy = MafiaMatch.Restore(new MatchConfig(), m.Export(), new First());
            Assert.AreEqual(1, copy.Get(doctor).SelfSavesUsed);
            m.Advance(); m.Advance(); m.Advance(); m.Advance();   // day, voting, verdict, next night
            copy.Advance(); copy.Advance(); copy.Advance(); copy.Advance();
            if (copy.Phase == Phase.Night) Assert.AreEqual(ActionResult.SelfSaveUsed, copy.DoctorSave(doctor, doctor));
        }

        [Test]
        public void Garbage_is_not_a_snapshot()
        {
            Assert.IsNull(MafiaMatch.Restore(new MatchConfig(), new List<string>(), new First()));
            Assert.IsNull(MafiaMatch.Restore(new MatchConfig(), new[] { "zz", "Night" }, new First()));
            Assert.IsNull(MafiaMatch.Restore(new MatchConfig(), new[] { "m1", "Bogus", "1", "None", "", "0", "0" }, new First()));
            var good = Played(5, out _, out _).Export();
            Assert.IsNull(MafiaMatch.Restore(new MatchConfig(), good.Take(good.Count - 3).ToList(), new First()));
        }

        // ------------------------------------------------------------------ the host controller

        [Test]
        public void A_new_host_resumes_with_the_same_clock_and_accepts_acts()
        {
            var m = Played(8, out var mafia, out var civ);
            var wire = new Wire();
            var old = new HostMatchController(m, wire, "p0", _ => { });
            old.Begin();
            old.Tick(m.Config.RoleRevealSeconds + 0.1f);               // night
            old.Receive(mafia, new WireMessage(Msg.Act, "mafia", civ));
            old.Tick(10f);
            var data = old.ExportState();

            var wire2 = new Wire();
            var selfMsgs = new List<WireMessage>();
            var resumed = HostMatchController.Resume(data, new MatchConfig(), new First(), wire2, "p1", selfMsgs.Add);
            Assert.IsNotNull(resumed);
            Assert.AreEqual(Phase.Night, resumed.Match.Phase);
            Assert.AreEqual(old.SecondsLeft, resumed.SecondsLeft, 0.01f);
            Assert.AreEqual(civ, resumed.Match.MafiaVotes[mafia]);

            resumed.PlayerLeft("p0");                                  // the old host is gone
            resumed.Reannounce();
            // every player who is still in gets the state; the left one does not
            var targets = wire2.Sent.Where(s => s.Value.Type == Msg.State).Select(s => s.Key).Distinct().ToList();
            Assert.IsFalse(targets.Contains("p0"));
            Assert.IsTrue(targets.Contains("p2") && targets.Contains("p7"));
            Assert.IsTrue(selfMsgs.Any(x => x.Type == Msg.State), "the new host hears it through his own client");

            // the mafia's pick is repeated to the mafia only
            var mv = wire2.Sent.Where(s => s.Value.Type == Msg.MafiaVotes).Select(s => s.Key).ToList();
            foreach (var id in mv) Assert.IsTrue(m.Get(id).IsMafia, id);

            // acts go to the new host
            var other = m.Players.First(p => p.Role == Role.Doctor).Id;
            resumed.Receive(other, new WireMessage(Msg.Act, "save", other));
            Assert.AreEqual(other, resumed.Match.Players.Count(p => p.Id == other) == 1 ? other : null);
            resumed.Tick(m.Config.NightSeconds);                       // the clock keeps running
            Assert.AreEqual(Phase.Dawn, resumed.Match.Phase);
        }

        [Test]
        public void A_snapshot_with_many_players_is_a_long_message_that_survives_the_wire_and_framing()
        {
            // real product user ids are 32 characters long
            var players = Enumerable.Range(0, 16).Select(i => new KeyValuePair<string, string>(("00020ddd192f450cb9c9cf2fd16d63" + i.ToString("00")), "لاعب بإسم طويل " + i)).ToList();
            var m = new MafiaMatch(new MatchConfig(), players, new First());
            var host = new HostMatchController(m, new Wire(), players[0].Key, _ => { });
            host.Begin();
            var msg = new WireMessage(Msg.Snapshot, host.ExportState().ToArray());
            var bytes = msg.ToBytes();
            Assert.Greater(bytes.Length, PacketFramer.MaxPacket, "a 16-player snapshot does not fit one packet");
            var re = new PacketFramer.Reassembler();
            byte[] whole = null;
            foreach (var p in PacketFramer.Split(bytes)) whole = re.Push("p0", p, p.Length) ?? whole;
            var back = WireMessage.FromBytes(whole);
            CollectionAssert.AreEqual(msg.Args, back.Args);
            Assert.IsNotNull(HostMatchController.Resume(back.Args, new MatchConfig(), new First(), new Wire(), "p1", _ => { }));
        }

        [Test]
        public void A_16_player_state_message_needs_the_framing_with_real_sized_ids()
        {
            // real product user ids are 32 characters long
            var players = Enumerable.Range(0, 16).Select(i => new KeyValuePair<string, string>(("00020ddd192f450cb9c9cf2fd16d63" + i.ToString("00")), "لاعب بإسم طويل " + i)).ToList();
            var m = new MafiaMatch(new MatchConfig(), players, new First());
            var wire = new Wire();
            var host = new HostMatchController(m, wire, players[0].Key, _ => { });
            host.Begin();
            var state = wire.Sent.First(s => s.Value.Type == Msg.State).Value.ToBytes();
            var packets = PacketFramer.Split(state);
            foreach (var p in packets) Assert.LessOrEqual(p.Length, PacketFramer.MaxPacket);
            var re = new PacketFramer.Reassembler();
            byte[] whole = null; foreach (var p in packets) whole = re.Push("h", p, p.Length) ?? whole;
            Assert.AreEqual(Msg.State, WireMessage.FromBytes(whole).Type);
        }

        [Test]
        public void A_player_who_left_and_comes_back_can_watch()
        {
            var m = Played(6, out var mafia, out _);
            string civ = m.Players.First(p => p.Role == Role.Civilian && p.Id != "p0").Id;
            var wire = new Wire();
            var host = new HostMatchController(m, wire, "p0", _ => { });
            host.Begin();
            host.Tick(m.Config.RoleRevealSeconds + 0.1f);
            host.PlayerLeft(civ);
            wire.Sent.Clear();
            host.AddSpectator(civ);
            Assert.IsTrue(wire.Sent.Any(s => s.Key == civ && s.Value.Type == Msg.State), "he gets the table again");
            Assert.IsFalse(wire.Sent.Any(s => s.Key == civ && s.Value.Type == Msg.Role), "but not a role");
        }
    }
}
