using System;
using System.Collections.Generic;
using System.Linq;

namespace MafiaNight.MatchRules.Net
{
    /// <summary>
    /// Runs a match on the host: owns the <see cref="MafiaMatch"/>, the phase timer, and the messages. Players only ever send "act" requests;
    /// everything they learn comes from here, and secrets (roles, sheriff answers, the mafia's votes) are sent to the one who may know them.
    /// The host is also a player: messages to himself are handed to <c>toSelf</c> instead of the network.
    /// </summary>
    public sealed class HostMatchController
    {
        private const float EarlyEndDelay = 2f;       // everybody acted: show it for a moment, then move on
        private const float RoleRevealMin = 3f;

        private readonly MafiaMatch match;
        private readonly IMatchTransport net;
        private readonly string selfId;
        private readonly Action<WireMessage> toSelf;
        private readonly HashSet<string> ready = new HashSet<string>();
        private readonly HashSet<string> spectators = new HashSet<string>();   // room members who arrived after the start: they watch
        private float timeLeft;
        private float endIn = -1f;                    // countdown to an early end, -1 = none
        private float elapsedInPhase;

        public MafiaMatch Match => match;
        public float SecondsLeft => timeLeft;
        public bool Running => match.Phase != Phase.Lobby && match.Phase != Phase.GameOver;
        /// <summary>The match reached its end (winner decided).</summary>
        public event Action Finished;

        public HostMatchController(MafiaMatch match, IMatchTransport net, string selfId, Action<WireMessage> toSelf)
        {
            this.match = match; this.net = net; this.selfId = selfId; this.toSelf = toSelf;
            net.Received += Receive;
        }

        public void Dispose() { net.Received -= Receive; }

        // ---------------------------------------------------------------- sending

        private void Send(string peer, WireMessage m)
        {
            if (peer == selfId) toSelf?.Invoke(m); else net.Send(peer, m);
        }

        private void ToAll(WireMessage m)
        {
            foreach (var p in match.Players.Where(x => !x.Left)) Send(p.Id, m);
            foreach (var id in spectators) Send(id, m);
        }
        private void ToMafia(WireMessage m) { foreach (var p in match.Players.Where(x => x.IsMafia && !x.Left)) Send(p.Id, m); }

        private WireMessage StateMessage()
        {
            var a = new List<string> { match.Phase.ToString(), match.Round.ToString(), WireMessage.F(timeLeft) };
            foreach (var p in match.Players) { a.Add(p.Id); a.Add(WireMessage.Clean(p.Name)); a.Add(p.Alive ? "1" : "0"); a.Add(p.Left ? "1" : "0"); }
            return new WireMessage(Msg.State, a.ToArray());
        }

        // ---------------------------------------------------------------- flow

        /// <summary>Hands out the roles and tells everybody the match begins. False when the player count is not allowed.</summary>
        public bool Begin()
        {
            if (!match.Start()) return false;
            ToAll(new WireMessage(Msg.Start));
            foreach (var p in match.Players)
            {
                var args = new List<string> { p.Role.ToString() };
                if (p.IsMafia) args.AddRange(match.MafiaTeam.Where(m => m.Id != p.Id).Select(m => m.Id));   // the mafia know each other
                Send(p.Id, new WireMessage(Msg.Role, args.ToArray()));
            }
            EnterPhase();
            return true;
        }

        private void EnterPhase()
        {
            timeLeft = match.PhaseSeconds; endIn = -1f; elapsedInPhase = 0f; ready.Clear();
            ToAll(StateMessage());
            switch (match.Phase)
            {
                case Phase.Dawn:
                    var n = match.LastNight;
                    ToAll(new WireMessage(Msg.Dawn, n.VictimId ?? "", n.Saved ? "1" : "0", n.DiedId ?? "", n.DiedRole?.ToString() ?? ""));
                    break;
                case Phase.Verdict:
                    var v = match.LastVerdict;
                    ToAll(new WireMessage(Msg.Verdict, v.RemovedId ?? "", v.RemovedRole?.ToString() ?? "", v.Votes.ToString(), v.Abstained.ToString()));
                    break;
                case Phase.GameOver:
                    SendOver();
                    Finished?.Invoke();
                    break;
            }
        }

        private void SendOver()
        {
            var a = new List<string> { match.Winner.ToString() };
            foreach (var p in match.Players) { a.Add(p.Id); a.Add(p.Role.ToString()); }
            ToAll(new WireMessage(Msg.Over, a.ToArray()));
        }

        private void Advance()
        {
            match.Advance();
            EnterPhase();
        }

        /// <summary>Call every frame with the time since the last one.</summary>
        public void Tick(float dt)
        {
            if (!Running) return;
            elapsedInPhase += dt;
            timeLeft -= dt;
            if (endIn < 0f && EverybodyDone()) endIn = EarlyEndDelay;
            if (endIn >= 0f) { endIn -= dt; if (endIn <= 0f) { Advance(); return; } }
            if (timeLeft <= 0f) Advance();
        }

        private bool EverybodyDone()
        {
            switch (match.Phase)
            {
                case Phase.RoleReveal: return elapsedInPhase >= RoleRevealMin && match.InPlay.All(p => ready.Contains(p.Id));
                case Phase.Night: return match.NightActionsComplete();
                case Phase.Day: return match.SkipReached;
                case Phase.Voting: return match.AllVoted();
                default: return false;
            }
        }

        /// <summary>A room member arrived while the match runs: he sees the table (no role, no acts) until the next match.</summary>
        public void AddSpectator(string id)
        {
            if (!Running || string.IsNullOrEmpty(id) || id == selfId || match.Get(id) != null || !spectators.Add(id)) return;
            Send(id, new WireMessage(Msg.Start));
            Send(id, StateMessage());
        }

        /// <summary>A player left for good.</summary>
        public void PlayerLeft(string id)
        {
            if (spectators.Remove(id)) return;
            if (!Running) return;
            var before = match.Phase;
            match.PlayerLeft(id);
            if (match.Phase != before) EnterPhase(); else ToAll(StateMessage());
        }

        // ---------------------------------------------------------------- acts

        public void Receive(string sender, WireMessage m)
        {
            if (m == null || m.Type != Msg.Act || !Running) return;
            string kind = m.Arg(0), target = m.Arg(1);
            ActionResult res;
            switch (kind)
            {
                case "mafia":
                    res = match.MafiaVote(sender, target);
                    if (res == ActionResult.Ok)
                    {
                        var a = new List<string>();
                        foreach (var kv in match.MafiaVotes) { a.Add(kv.Key); a.Add(kv.Value); }
                        ToMafia(new WireMessage(Msg.MafiaVotes, a.ToArray()));
                    }
                    break;
                case "save": res = match.DoctorSave(sender, target); break;
                case "check":
                    res = match.SheriffCheck(sender, target, out var rep);
                    if (res == ActionResult.Ok) Send(sender, new WireMessage(Msg.Sheriff, rep.TargetId, rep.IsMafia ? "1" : "0"));
                    break;
                case "skip":
                    bool ok = match.Get(sender)?.InPlay == true && match.Phase == Phase.Day;
                    res = ok ? ActionResult.Ok : ActionResult.WrongPhase;
                    if (ok) { match.RequestSkip(sender); ToAll(new WireMessage(Msg.Skip, match.SkipCount.ToString(), (match.InPlayCount / 2 + 1).ToString())); }
                    break;
                case "vote":
                    res = match.Vote(sender, string.IsNullOrEmpty(target) ? null : target);
                    if (res == ActionResult.Ok)
                    {
                        var a = new List<string>();
                        foreach (var kv in match.CurrentTally()) { a.Add(kv.Key); a.Add(kv.Value.ToString()); }
                        ToAll(new WireMessage(Msg.Tally, a.ToArray()));
                    }
                    break;
                case "ready":
                    ready.Add(sender); res = ActionResult.Ok; break;
                default:
                    return;
            }
            Send(sender, new WireMessage(Msg.Result, kind, res.ToString()));
        }
    }
}
