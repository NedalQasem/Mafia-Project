using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    /// <summary>Bots inside a real room's match: the host's transport keeps their messages to itself and they play to the end.</summary>
    public class BotMatchTests
    {
        private sealed class Wire : IMatchTransport
        {
            public readonly List<string> Sent = new List<string>();
            public event Action<string, WireMessage> Received;
            public void Send(string peerId, WireMessage message) { Sent.Add(peerId); }
            public void Raise(string from, WireMessage m) => Received?.Invoke(from, m);
        }

        [Test]
        public void MessagesForBotsNeverReachTheNetwork_AndTheyHearTheRealOnesBack()
        {
            var wire = new Wire();
            var net = new BotMatchTransport(wire, new SystemRandom());
            net.Set(new[] { "bot:b1", "bot:b2" });
            net.Send("bot:b1", new WireMessage("state", "Night"));
            net.Send("realPeer", new WireMessage("state", "Night"));
            CollectionAssert.AreEqual(new[] { "realPeer" }, wire.Sent);
            Assert.IsTrue(net.Has("bot:b1")); Assert.IsFalse(net.Has("realPeer")); Assert.AreEqual(2, net.Count);

            string heard = null;
            net.Received += (from, m) => heard = from + ":" + m.Type;
            wire.Raise("realPeer", new WireMessage("act", "vote"));
            Assert.AreEqual("realPeer:act", heard);                    // what the network hears still reaches the host
        }

        [Test]
        public void AMatchWithOneRealPlayerAndThreeBotsPlaysToTheEnd()
        {
            var wire = new Wire();
            var net = new BotMatchTransport(wire, new SystemRandom());
            net.Set(new[] { "bot:b1", "bot:b2", "bot:b3" });
            var players = new[] { "me", "bot:b1", "bot:b2", "bot:b3" }.Select(id => new KeyValuePair<string, string>(id, id)).ToList();
            var match = new MafiaMatch(new MatchConfig(), players, new SystemRandom());
            BotPlayer me = null;
            HostMatchController host = null;
            me = new BotPlayer("me", m => host.Receive("me", m), new SystemRandom(), new Random(7));       // the human's seat is played by a bot too, so the test needs no input
            host = new HostMatchController(match, net, "me", m => me.Receive(m));
            net.ToHost = (id, m) => host.Receive(id, m);
            bool finished = false;
            host.Finished += () => finished = true;

            Assert.IsTrue(host.Begin("m1", players.ToDictionary(p => p.Key, p => "")));
            for (int i = 0; i < 20000 && !finished; i++) { host.Tick(0.5f); net.Tick(0.5f); me.Tick(0.5f); }

            Assert.IsTrue(finished, "the match never ended");
            Assert.IsEmpty(wire.Sent, "a message for a bot went out to the network");
        }
    }
}
