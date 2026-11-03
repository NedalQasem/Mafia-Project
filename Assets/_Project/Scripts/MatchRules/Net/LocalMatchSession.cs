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

        private readonly Dictionary<string, BotPlayer> bots = new Dictionary<string, BotPlayer>();
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
                bots[id] = new BotPlayer(id, m => Host.Receive(id, m), this.random, jitter);
            }
            Host = new HostMatchController(Match, this, MeId, m => Me.Apply(WireMessage.FromBytes(m.ToBytes())));
        }

        public bool Begin() => Host.Begin();

        void IMatchTransport.Send(string peerId, WireMessage message)
        {
            if (bots.TryGetValue(peerId, out var b)) b.Receive(message);
        }

        /// <summary>Call every frame.</summary>
        public void Tick(float dt)
        {
            Host.Tick(dt);
            Me.Tick(dt);
            foreach (var b in bots.Values) b.Tick(dt);
        }

    }
}
