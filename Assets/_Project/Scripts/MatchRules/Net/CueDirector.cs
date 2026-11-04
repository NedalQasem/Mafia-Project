using System;
using System.Collections.Generic;

namespace MafiaNight.MatchRules.Net
{
    /// <summary>One narration to play: the cue, its line with the name filled in, and how many seconds after the moment it starts.</summary>
    public sealed class CuePlay
    {
        public Cue Cue;
        public string Subtitle = "";
        public float Delay;
    }

    /// <summary>
    /// Listens to one player's view of the match and says which narration belongs to each moment (the night falls, the dawn, the verdict, the Avenger's turn, the end).
    /// It is plain logic: the screen that plays the cues (slip of text, voice clip, animation) listens to <see cref="Play"/>. A cue is chosen by what this player may know
    /// (the night call is his own role's, or the idle line for everybody else, both of the same length), and a moment without a cue simply stays silent.
    /// </summary>
    public sealed class CueDirector : IDisposable
    {
        private readonly ClientMatchController c;

        /// <summary>Raised with the cues of a moment, in order (each has its own delay).</summary>
        public event Action<CuePlay> Play;

        public CueDirector(ClientMatchController client)
        {
            c = client;
            c.PhaseChanged += OnPhase;
            c.Moment += OnMoment;
        }

        public void Dispose() { c.PhaseChanged -= OnPhase; c.Moment -= OnMoment; }

        private string Name(string id) => id == null ? "" : c.Seat(id)?.Name ?? "";

        private float Emit(string cueId, string name, float at)
        {
            var cue = CueCatalog.Find(cueId);
            if (cue == null) return at;
            Play?.Invoke(new CuePlay { Cue = cue, Subtitle = CueCatalog.Format(cue, name), Delay = at });
            return at + cue.Duration;
        }

        private void OnPhase(Phase p)
        {
            switch (p)
            {
                case Phase.RoleReveal: Emit(CueIds.MatchStart, "", 0f); break;
                case Phase.Night:
                    float t = Emit(CueIds.NightFalls, "", 0f);
                    var mine = c.MyRole ?? Role.Civilian;
                    var call = CueCatalog.Has(CueIds.Call(mine)) ? CueCatalog.NightCallFor(mine, mine, c.IAmAlive) : CueCatalog.Find(CueIds.NightIdle);
                    if (call != null) Emit(call.Id, "", t);
                    break;
                case Phase.Day: Emit(CueIds.DayStart, "", 0f); break;
                case Phase.Voting: Emit(CueIds.VoteStart, "", 0f); break;
            }
        }

        private void OnMoment(string moment)
        {
            float t = 0f;
            switch (moment)
            {
                case "dawn":
                    if (c.DawnSaved) t = Emit(CueIds.DawnSaved, Name(c.DawnVictimId), t);
                    else if (c.DawnDiedId != null) t = Emit(CueIds.DawnKill, Name(c.DawnDiedId), t);
                    else t = Emit(CueIds.DawnQuiet, "", t);
                    foreach (var shot in c.DawnShotDeaths) t = Emit(CueIds.DawnShot, Name(shot.Key), t);
                    if (c.SilencedId != null) Emit(CueIds.DawnSilenced, Name(c.SilencedId), t);
                    break;
                case "verdict":
                    if (c.VerdictRemovedId == null) Emit(CueIds.VerdictNobody, "", 0f);
                    else Emit(c.VerdictRemovedRole.HasValue && c.VerdictRemovedRole.Value.IsMafiaTeam() ? CueIds.VerdictMafia : CueIds.VerdictTown, Name(c.VerdictRemovedId), 0f);
                    break;
                case "avenge.open": Emit(CueIds.AvengeStart, "", 0f); break;
                case "avenge.done": if (c.AvengeTargetId != null) Emit(CueIds.AvengeDone, Name(c.AvengeTargetId), 0f); break;
                case "over":
                    if (c.Winner == Winner.Town) Emit(CueIds.OverTown, "", 0f);
                    else if (c.Winner == Winner.Mafia) Emit(CueIds.OverMafia, "", 0f);
                    break;
            }
        }
    }
}
