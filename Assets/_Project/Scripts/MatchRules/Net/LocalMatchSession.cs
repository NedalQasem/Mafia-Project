using System;
using System.Collections.Generic;
using System.Linq;

namespace MafiaNight.MatchRules.Net
{
    /// <summary>
    /// A whole match on one device: the human is the host and player "me", everybody else is a bot. Messages take the same wire encoding
    /// as the real network, so the screens see exactly what they will see online. Used for practice, for the editor and for testing the screens.
    /// </summary>
    public sealed class LocalMatchSession : IMatchTransport
    {
        public const string MeId = "me";

        private sealed class Bot
        {
            public string Id; public ClientMatchController Client;
            public float actIn = -1f; public Phase lastPhase = Phase.Lobby; public int lastRound = -1;
        }

        private readonly Dictionary<string, Bot> bots = new Dictionary<string, Bot>();
        private readonly IRandom random;
        private readonly Random jitter;
        public ClientMatchController Me { get; }
        public HostMatchController Host { get; }
        public MafiaMatch Match { get; }

        public event Action<string, WireMessage> Received;     // acts from the bots, heard by the host

        public LocalMatchSession(string myName, int players, MatchConfig config = null, IRandom random = null, int seed = 0)
        {
            this.random = random ?? new SystemRandom();
            jitter = seed == 0 ? new Random() : new Random(seed);
            if (players < 4) players = 4;
            var list = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>(MeId, myName) };
            string[] names = { "أم سعيد", "البيك", "لينا", "سارة", "الحج حسن", "أبو فارس", "مازن", "ريم", "عادل", "نور", "سامر", "هديل", "خالد", "رنا", "يزن" };
            for (int i = 0; i < players - 1; i++) list.Add(new KeyValuePair<string, string>("bot" + i, names[i % names.Length]));
            Match = new MafiaMatch(config ?? new MatchConfig(), list, this.random);
            Me = new ClientMatchController(MeId, m => Host.Receive(MeId, WireMessage.FromBytes(m.ToBytes())));
            foreach (var p in list.Skip(1))
            {
                string id = p.Key;
                bots[id] = new Bot { Id = id, Client = new ClientMatchController(id, m => Host.Receive(id, WireMessage.FromBytes(m.ToBytes()))) };
            }
            Host = new HostMatchController(Match, this, MeId, m => Me.Apply(WireMessage.FromBytes(m.ToBytes())));
        }

        public bool Begin() => Host.Begin();

        void IMatchTransport.Send(string peerId, WireMessage message)
        {
            if (bots.TryGetValue(peerId, out var b)) b.Client.Apply(WireMessage.FromBytes(message.ToBytes()));
        }

        /// <summary>Call every frame.</summary>
        public void Tick(float dt)
        {
            Host.Tick(dt);
            Me.Tick(dt);
            foreach (var b in bots.Values) Think(b, dt);
        }

        private float Delay(float min, float max) => min + (float)jitter.NextDouble() * (max - min);

        private void Think(Bot b, float dt)
        {
            var c = b.Client;
            c.Tick(dt);
            if (c.Phase != b.lastPhase || c.Round != b.lastRound)
            {
                b.lastPhase = c.Phase; b.lastRound = c.Round;
                b.actIn = c.Phase == Phase.RoleReveal ? Delay(1f, 4f)
                        : c.Phase == Phase.Night ? Delay(3f, 14f)
                        : c.Phase == Phase.Day ? Delay(10f, 40f)
                        : c.Phase == Phase.Voting ? Delay(4f, 20f) : -1f;
            }
            if (b.actIn < 0f) return;
            b.actIn -= dt;
            if (b.actIn > 0f) return;
            b.actIn = -1f;
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
}
