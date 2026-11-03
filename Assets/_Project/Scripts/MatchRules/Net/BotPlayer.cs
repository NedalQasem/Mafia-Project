using System;
using System.Collections.Generic;
using System.Linq;

namespace MafiaNight.MatchRules.Net
{
    /// <summary>
    /// A made-up player inside a match: it has its own <see cref="ClientMatchController"/>, hears what the host sends to it and acts after a human-like pause
    /// (readies, picks a victim, votes). Used by the practice match and by a host who fills his room with bots.
    /// </summary>
    public sealed class BotPlayer
    {
        public string Id { get; }
        public ClientMatchController Client { get; }

        private readonly IRandom random;
        private readonly Random jitter;
        private float actIn = -1f;
        private Phase lastPhase = Phase.Lobby;
        private int lastRound = -1;

        /// <param name="toHost">what the bot sends to the host (the host reads it as coming from <paramref name="id"/>)</param>
        public BotPlayer(string id, Action<WireMessage> toHost, IRandom random, Random jitter)
        {
            Id = id; this.random = random; this.jitter = jitter;
            Client = new ClientMatchController(id, m => toHost(WireMessage.FromBytes(m.ToBytes())));
        }

        /// <summary>A message from the host.</summary>
        public void Receive(WireMessage message) => Client.Apply(WireMessage.FromBytes(message.ToBytes()));

        private float Delay(float min, float max) => min + (float)jitter.NextDouble() * (max - min);

        /// <summary>Call every frame.</summary>
        public void Tick(float dt)
        {
            var c = Client;
            c.Tick(dt);
            if (c.Phase != lastPhase || c.Round != lastRound)
            {
                lastPhase = c.Phase; lastRound = c.Round;
                actIn = c.Phase == Phase.RoleReveal ? Delay(1f, 4f)
                      : c.Phase == Phase.Night ? Delay(3f, 14f)
                      : c.Phase == Phase.Day ? Delay(10f, 40f)
                      : c.Phase == Phase.Voting ? Delay(4f, 20f) : -1f;
            }
            if (actIn < 0f) return;
            actIn -= dt;
            if (actIn > 0f) return;
            actIn = -1f;
            if (!c.IAmAlive && c.Phase != Phase.RoleReveal) return;

            var alive = c.Seats.Where(s => s.Alive && !s.Left && !s.IsMe).ToList();
            if (alive.Count == 0 && c.Phase != Phase.RoleReveal) return;
            switch (c.Phase)
            {
                case Phase.RoleReveal: c.Ready(); break;
                case Phase.Night:
                    if (c.MyRole == Role.Mafia) { var t = alive.Where(s => !s.IsMate).ToList(); if (t.Count > 0) c.MafiaPick(t[random.Next(t.Count)].Id); }
                    else if (c.MyRole == Role.Doctor) { var all = c.Seats.Where(s => s.Alive && !s.Left).ToList(); c.DoctorSave(all[random.Next(all.Count)].Id); }
                    else if (c.MyRole == Role.Sheriff) c.SheriffCheck(alive[random.Next(alive.Count)].Id);
                    break;
                case Phase.Day: c.SkipDiscussion(); break;
                case Phase.Voting:
                    if (random.Next(5) == 0) c.Vote(null);
                    else
                    {   // mafia bots never vote for a mate
                        var t = alive.Where(s => !s.IsMate).ToList();
                        if (t.Count > 0) c.Vote(t[random.Next(t.Count)].Id);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// The host's transport with bots in it: a message for a bot goes to the bot's own controller, everything else to the real network, and what the real network
    /// hears is passed on. The bots act on the host's frame, so the host must call <see cref="Tick"/>.
    /// </summary>
    public sealed class BotMatchTransport : IMatchTransport
    {
        private readonly IMatchTransport inner;
        private readonly IRandom random;
        private readonly Random jitter = new Random();
        private readonly Dictionary<string, BotPlayer> bots = new Dictionary<string, BotPlayer>();

        /// <summary>What a bot sends to the host: bot id, message.</summary>
        public Action<string, WireMessage> ToHost;

        public BotMatchTransport(IMatchTransport inner, IRandom random) { this.inner = inner; this.random = random; }

        public int Count => bots.Count;
        public bool Has(string id) => id != null && bots.ContainsKey(id);

        /// <summary>The bots of the next match are exactly these.</summary>
        public void Set(IEnumerable<string> ids)
        {
            bots.Clear();
            foreach (var id in ids) { string bid = id; bots[bid] = new BotPlayer(bid, m => ToHost?.Invoke(bid, m), random, jitter); }
        }

        public void Send(string peerId, WireMessage message)
        {
            if (peerId != null && bots.TryGetValue(peerId, out var b)) b.Receive(message); else inner.Send(peerId, message);
        }

        public event Action<string, WireMessage> Received
        {
            add { inner.Received += value; }
            remove { inner.Received -= value; }
        }

        public void Tick(float dt) { if (bots.Count > 0) foreach (var b in bots.Values.ToList()) b.Tick(dt); }
    }
}
