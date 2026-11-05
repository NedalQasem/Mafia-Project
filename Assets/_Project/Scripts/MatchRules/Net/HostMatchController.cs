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
        private string matchId = "";
        private readonly Dictionary<string, string> roster = new Dictionary<string, string>();   // player id -> PlayFab id, for the reward report
        private readonly HashSet<string> spectators = new HashSet<string>();   // room members who arrived after the start: they watch
        private readonly HashSet<string> forbidden = new HashSet<string>();    // players the host kicked: they may not take their seat back
        private float timeLeft;
        private float endIn = -1f;                    // countdown to an early end, -1 = none
        private float elapsedInPhase;

        public MafiaMatch Match => match;
        /// <summary>The host's account of the match, one line at a time (phase, acts accepted or refused, deaths, the winner): the test journal writes it.</summary>
        public Action<string> Trace;
        private void T(string line) { Trace?.Invoke(line); }
        private string N(string id) => string.IsNullOrEmpty(id) ? "-" : (match.Get(id)?.Name ?? id);
        public float SecondsLeft => timeLeft;
        public bool Running => match.Phase != Phase.Lobby && match.Phase != Phase.GameOver;
        /// <summary>The match reached its end (winner decided).</summary>
        public event Action Finished;
        /// <summary>Something that a snapshot must contain changed (phase, an accepted act, somebody left).</summary>
        public event Action Changed;

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

        private WireMessage StartMessage()
        {
            var a = new List<string> { matchId };
            foreach (var kv in roster) { a.Add(kv.Key); a.Add(kv.Value); }
            return new WireMessage(Msg.Start, a.ToArray());
        }

        /// <summary>Which role is awake now (empty: the opening or the pause between two calls), the whole list of calls tonight and the position of the current one.</summary>
        private WireMessage StepMessage()
        {
            return new WireMessage(Msg.Step, match.ActiveStep?.ToString() ?? "", string.Join(",", match.NightSteps.Select(s => s.ToString())), match.NightStepIndex.ToString());
        }

        private WireMessage RolesMessage()
        {
            var a = new List<string>();
            foreach (var g in match.Players.GroupBy(p => p.Role).OrderBy(g => g.Key)) { a.Add(g.Key.ToString()); a.Add(g.Count().ToString()); }
            return new WireMessage(Msg.Roles, a.ToArray());
        }

        private WireMessage StateMessage()
        {
            var a = new List<string> { match.Phase.ToString(), match.Round.ToString(), WireMessage.F(timeLeft) };
            foreach (var p in match.Players) { a.Add(p.Id); a.Add(WireMessage.Clean(p.Name)); a.Add(p.Alive ? "1" : "0"); a.Add(p.Left ? "1" : "0"); }
            return new WireMessage(Msg.State, a.ToArray());
        }

        // ---------------------------------------------------------------- flow

        /// <summary>Hands out the roles and tells everybody the match begins. False when the player count is not allowed.</summary>
        public bool Begin(string newMatchId = null, IDictionary<string, string> playFabIds = null)
        {
            if (!match.Start()) return false;
            matchId = newMatchId ?? "";
            roster.Clear();
            if (playFabIds != null) foreach (var kv in playFabIds) roster[kv.Key] = kv.Value ?? "";
            ToAll(StartMessage());
            ToAll(RolesMessage());
            T("begin " + matchId + ": " + match.Players.Count + " players; roles: " + string.Join(", ", match.Players.Select(p => N(p.Id) + "=" + p.Role)));
            foreach (var p in match.Players) SendRole(p);
            EnterPhase();
            return true;
        }

        private void SendRole(MatchPlayer p)
        {
            var args = new List<string> { p.Role.ToString() };
            // the mafia know each other, and which of them is the silencer ("id|Silencer"; a plain id is a mafia member)
            if (p.IsMafia) args.AddRange(match.MafiaTeam.Where(m => m.Id != p.Id).Select(m => m.Role == Role.Mafia ? m.Id : m.Id + "|" + m.Role));
            Send(p.Id, new WireMessage(Msg.Role, args.ToArray()));
        }

        private WireMessage DawnMessage()
        {
            var n = match.LastNight;
            var a = new List<string> { n.VictimId ?? "", n.Saved ? "1" : "0", n.DiedId ?? "", n.DiedRole?.ToString() ?? "", n.SilencedId ?? "" };
            foreach (var d in n.ShotDeaths) { a.Add(d.Id); a.Add(d.Role?.ToString() ?? ""); }
            return new WireMessage(Msg.Dawn, a.ToArray());
        }

        private WireMessage AvengeMessage(string step)
        {
            var done = match.LastAvenge;
            string id = match.AvengerId ?? done?.AvengerId ?? "";
            string target = step == "done" ? done?.TargetId ?? "" : match.AvengePick ?? "";
            return new WireMessage(Msg.Avenge, id, target, step == "done" ? done?.TargetRole?.ToString() ?? "" : "", step);
        }

        private void EnterPhase()
        {
            timeLeft = match.PhaseSeconds; endIn = -1f; elapsedInPhase = 0f; ready.Clear();
            T("phase " + match.Phase + " round " + match.Round + " (" + timeLeft.ToString("0") + " s), " + match.InPlayCount + " in play");
            Changed?.Invoke();
            ToAll(StateMessage());
            switch (match.Phase)
            {
                case Phase.Night:
                    ToAll(StepMessage());
                    break;
                case Phase.Dawn:
                    var n = match.LastNight;
                    T("dawn: the mafia chose " + N(n.VictimId) + (n.Saved ? ", the doctor saved him" : ", died: " + N(n.DiedId) + " (" + n.DiedRole + ")")
                      + (n.SilencedId != null ? "; silenced: " + N(n.SilencedId) : "") + (n.ShotDeaths.Count > 0 ? "; shot: " + string.Join(", ", n.ShotDeaths.Select(d => N(d.Id) + " (" + d.Role + ")")) : ""));
                    ToAll(DawnMessage());
                    SendDoctorResult(null);
                    break;
                case Phase.Avenge:
                    T("avenge: " + N(match.AvengerId) + " decides who leaves with him (" + timeLeft.ToString("0") + " s)");
                    ToAll(AvengeMessage("open"));
                    break;
                case Phase.Verdict:
                    var v = match.LastVerdict;
                    T("verdict: " + (v.RemovedId == null ? "nobody removed" : N(v.RemovedId) + " removed (" + v.RemovedRole + ")") + ", votes " + v.Votes + ", abstained " + v.Abstained);
                    ToAll(new WireMessage(Msg.Verdict, v.RemovedId ?? "", v.RemovedRole?.ToString() ?? "", v.Votes.ToString(), v.Abstained.ToString()));
                    break;
                case Phase.GameOver:
                    T("over: winner " + match.Winner);
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
            var before = match.Phase;
            match.Advance();
            if (before == Phase.Avenge && match.LastAvenge != null)
            {
                T("avenge done: " + N(match.LastAvenge.AvengerId) + " took " + N(match.LastAvenge.TargetId) + (match.LastAvenge.TargetRole.HasValue ? " (" + match.LastAvenge.TargetRole + ")" : ""));
                ToAll(AvengeMessage("done"));
            }
            EnterPhase();
        }

        /// <summary>What the doctor learns about his protection: to him only. peer == null: to the doctor if he is in the match.</summary>
        private void SendDoctorResult(string peer)
        {
            var d = match.LastDoctor;
            if (d == null || (peer != null && peer != d.DoctorId) || match.Get(d.DoctorId)?.Left == true) return;
            Send(d.DoctorId, new WireMessage(Msg.DoctorResult, d.TargetId, d.Saved ? "1" : "0"));
        }

        // ---------------------------------------------------------------- hand-over to another device

        private const string RuntimeVersion = "h2";

        /// <summary>The match plus this controller's own timers, as text pieces for the member who may take over.</summary>
        public List<string> ExportState()
        {
            var o = new List<string> { RuntimeVersion, matchId, roster.Count.ToString() };
            foreach (var kv in roster) { o.Add(kv.Key); o.Add(kv.Value); }
            o.Add(WireMessage.F(timeLeft)); o.Add(WireMessage.F(endIn)); o.Add(WireMessage.F(elapsedInPhase)); o.Add(ready.Count.ToString());
            o.AddRange(ready);
            o.Add(spectators.Count.ToString());
            o.AddRange(spectators);
            o.AddRange(match.Export());
            return o;
        }

        /// <summary>Continues a match from <see cref="ExportState"/> on this device; null if the data is not valid.</summary>
        public static HostMatchController Resume(IList<string> data, MatchConfig config, IRandom random, IMatchTransport net, string selfId, Action<WireMessage> toSelf)
        {
            try
            {
                if (data == null || data.Count < 8 || data[0] != RuntimeVersion) return null;
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                int i = 1;
                string savedMatchId = data[i++];
                int nRoster = int.Parse(data[i++], inv);
                var savedRoster = new List<KeyValuePair<string, string>>();
                for (int k = 0; k < nRoster; k++) { string a = data[i++], b = data[i++]; savedRoster.Add(new KeyValuePair<string, string>(a, b)); }
                float timeLeft = float.Parse(data[i++], inv), endIn = float.Parse(data[i++], inv), elapsed = float.Parse(data[i++], inv);
                int nReady = int.Parse(data[i++], inv);
                var ready = new List<string>(); for (int k = 0; k < nReady; k++) ready.Add(data[i++]);
                int nSpect = int.Parse(data[i++], inv);
                var spect = new List<string>(); for (int k = 0; k < nSpect; k++) spect.Add(data[i++]);
                var match = MafiaMatch.Restore(config, new List<string>(data).GetRange(i, data.Count - i), random);
                if (match == null) return null;
                var c = new HostMatchController(match, net, selfId, toSelf) { timeLeft = timeLeft, endIn = endIn, elapsedInPhase = elapsed, matchId = savedMatchId };
                foreach (var kv in savedRoster) c.roster[kv.Key] = kv.Value;
                foreach (var r in ready) c.ready.Add(r);
                foreach (var sp in spect) c.spectators.Add(sp);
                return c;
            }
            catch (Exception) { return null; }
        }

        /// <summary>After a take-over: tells everybody where the match stands, so every screen matches the new host.</summary>
        public void Reannounce()
        {
            if (!Running) return;
            ToAll(StateMessage());
            SendPhaseExtras(null);
            Changed?.Invoke();
        }

        /// <summary>What belongs to the current phase besides the state: skips, the live tally, the mafia's picks. peer == null: everybody who may know.</summary>
        private void SendPhaseExtras(string peer)
        {
            void To(WireMessage m) { if (peer == null) ToAll(m); else Send(peer, m); }
            if (peer != null && (match.Phase == Phase.Day || match.Phase == Phase.Voting || match.Phase == Phase.Avenge)) SendDoctorResult(peer);
            switch (match.Phase)
            {
                case Phase.Avenge:
                    To(AvengeMessage("open"));
                    if (match.AvengePick != null) To(AvengeMessage("pick"));
                    break;
                case Phase.Day: To(new WireMessage(Msg.Skip, match.SkipCount.ToString(), (match.InPlayCount / 2 + 1).ToString())); break;
                case Phase.Voting:
                    var a = new List<string>();
                    foreach (var kv in match.CurrentTally()) { a.Add(kv.Key); a.Add(kv.Value.ToString()); }
                    To(new WireMessage(Msg.Tally, a.ToArray()));
                    break;
                case Phase.Night:
                    To(StepMessage());
                    if (match.MafiaVotes.Count > 0)
                    {
                        var mv = new List<string>();
                        foreach (var kv in match.MafiaVotes) { mv.Add(kv.Key); mv.Add(kv.Value); }
                        var msg = new WireMessage(Msg.MafiaVotes, mv.ToArray());
                        if (peer == null) ToMafia(msg); else if (match.Get(peer)?.IsMafia == true) Send(peer, msg);
                    }
                    if (peer != null && match.LastSheriff != null && match.LastSheriff.SheriffId == peer)
                        Send(peer, new WireMessage(Msg.Sheriff, match.LastSheriff.TargetId, match.LastSheriff.IsMafia ? "1" : "0"));
                    break;
            }
        }

        /// <summary>The roles of everybody who died (they are public from the moment of death): for somebody who joined or came back later.</summary>
        private void SendRevealed(string peer)
        {
            var a = new List<string>();
            foreach (var p in match.Players) if (p.Left ? !p.AliveWhenLeft : !p.Alive) { a.Add(p.Id); a.Add(p.Role.ToString()); }
            if (a.Count > 0) Send(peer, new WireMessage(Msg.Reveal, a.ToArray()));
        }

        /// <summary>Call every frame with the time since the last one.</summary>
        public void Tick(float dt)
        {
            if (!Running) return;
            elapsedInPhase += dt;
            timeLeft -= dt;
            if (match.AvengerGone) { T("the Avenger left the match: his moment is over"); Advance(); return; }
            if (endIn < 0f && EverybodyDone()) { endIn = EarlyEndFor(match.Phase); T("everybody acted in " + match.Phase + ": it ends in " + endIn.ToString("0") + " s"); }
            if (endIn >= 0f && match.Phase == Phase.Avenge && !EverybodyDone()) { endIn = -1f; T("the Avenger took his pick back"); }   // only his pick can be taken back
            if (endIn >= 0f) { endIn -= dt; if (endIn <= 0f) { Advance(); return; } }
            if (timeLeft <= 0f) { if (match.Phase == Phase.Night) NightClockEnded(); else Advance(); }
        }

        /// <summary>The night is called one role at a time: the opening, then (15 s awake, 2 s asleep) per role, then the dawn. Nobody can end a call early, so nobody learns who acted.</summary>
        private void NightClockEnded()
        {
            if (match.ActiveStep != null)
            {
                T("night: " + match.ActiveStep + " goes back to sleep");
                match.EndStep(); timeLeft = match.Config.NightSleepSeconds;
            }
            else if (match.BeginNextStep())
            {
                T("night: " + match.ActiveStep + " is called (" + match.Config.NightStepSeconds.ToString("0") + " s)");
                timeLeft = match.Config.NightStepSeconds;
            }
            else { Advance(); return; }
            ToAll(StepMessage());
            ToAll(StateMessage());
            Changed?.Invoke();
        }

        /// <summary>How long a finished phase is shown before it ends. The Avenger's pick gets the confirmation time, so a wrong tap can still be undone.</summary>
        private float EarlyEndFor(Phase phase) { return phase == Phase.Avenge ? match.Config.AvengeConfirmSeconds : EarlyEndDelay; }

        private bool EverybodyDone()
        {
            switch (match.Phase)
            {
                case Phase.RoleReveal: return elapsedInPhase >= RoleRevealMin && match.InPlay.All(p => ready.Contains(p.Id));
                case Phase.Day: return match.SkipReached;
                case Phase.Voting: return match.AllVoted();
                case Phase.Avenge: return match.AvengePick != null;
                default: return false;
            }
        }

        /// <summary>A room member arrived while the match runs: he sees the table (no role, no acts) until the next match.</summary>
        public void AddSpectator(string id)
        {
            if (!Running || string.IsNullOrEmpty(id) || id == selfId) return;
            var known = match.Get(id);
            if (known != null && !known.Left) return;                      // still sitting at the table: a repeated event
            if (known != null && TryReturn(id)) return;                    // he played before, left and is back: his seat is his again
            if (!spectators.Add(id)) return;
            T("spectator joined: " + id);
            Send(id, StartMessage());
            Send(id, RolesMessage());
            Send(id, StateMessage());
            SendRevealed(id);
        }

        /// <summary>The host kicked this player: he watches if he comes back, he does not get his seat again.</summary>
        public void Forbid(string id) { if (!string.IsNullOrEmpty(id)) forbidden.Add(id); }

        /// <summary>A player who left (he closed the app, or his connection broke) is back: the match gives him his seat and his role again.</summary>
        private bool TryReturn(string id)
        {
            if (forbidden.Contains(id) || !match.PlayerReturned(id)) return false;
            spectators.Remove(id);
            T("player is back at his seat: " + N(id));
            Send(id, StartMessage());
            Send(id, RolesMessage());
            SendRole(match.Get(id));
            ToAll(StateMessage());                                         // everybody sees him back, and he sees the table
            SendRevealed(id);
            if (match.Phase == Phase.Dawn && match.LastNight != null)
            {
                Send(id, DawnMessage());
                SendDoctorResult(id);
            }
            else if (match.Phase == Phase.Verdict && match.LastVerdict != null)
            {
                var v = match.LastVerdict;
                Send(id, new WireMessage(Msg.Verdict, v.RemovedId ?? "", v.RemovedRole?.ToString() ?? "", v.Votes.ToString(), v.Abstained.ToString()));
            }
            SendPhaseExtras(id);
            Changed?.Invoke();
            return true;
        }

        /// <summary>A player left for good.</summary>
        public void PlayerLeft(string id)
        {
            if (spectators.Remove(id)) return;
            if (!Running) return;
            var before = match.Phase;
            T("player left the room: " + N(id));
            match.PlayerLeft(id);
            if (match.Phase != before) EnterPhase(); else { Changed?.Invoke(); ToAll(StateMessage()); }
        }

        // ---------------------------------------------------------------- acts

        public void Receive(string sender, WireMessage m)
        {
            if (m == null || m.Type != Msg.Act || !Running) return;
            if (match.Get(sender)?.Left == true) TryReturn(sender);        // he is acting, so his connection is back
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
                case "silence": res = match.Silence(sender, target); break;
                case "snipe": res = match.Snipe(sender, target); break;
                case "avenge":
                    res = match.AvengerPick(sender, target);
                    if (res == ActionResult.Ok)
                    {
                        endIn = -1f;     // a changed pick starts the confirmation time again
                        ToAll(AvengeMessage("pick"));
                    }
                    break;
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
            T("act " + kind + " by " + N(sender) + (string.IsNullOrEmpty(target) ? "" : " on " + N(target)) + ": " + res);
            if (res == ActionResult.Ok) Changed?.Invoke();
            Send(sender, new WireMessage(Msg.Result, kind, res.ToString()));
        }
    }
}
