using System.Collections.Generic;
using System.Linq;

namespace MafiaNight.MatchRules
{
    /// <summary>What one step of a cue does. The screens and the audio player read these; the rules only say what exists and for how long.</summary>
    public enum CueStepKind
    {
        /// <summary>The narrator's line as text (the telegram slip). Every cue has one, so a cue works without any audio.</summary>
        Subtitle,
        /// <summary>The narrator's voice-over: <see cref="CueStep.Value"/> is an audio key (e.g. "ar/call.mafia"); a missing clip is skipped, the subtitle still shows.</summary>
        Voice,
        /// <summary>A named animation of the table (e.g. "zoom_seat", "shot", "shield").</summary>
        Animation,
        /// <summary>A named sound effect (e.g. "gunshot", "stamp").</summary>
        Sound,
    }

    public sealed class CueStep
    {
        public CueStepKind Kind;
        public string Value = "";
        /// <summary>Seconds after the start of the cue.</summary>
        public float At;
    }

    /// <summary>
    /// A moment of the match that can carry a narrator's voice, a line of text, an animation and sounds. Its <see cref="Duration"/> is fixed: the same for everybody
    /// whatever the player's role and whether the role's owner is still alive, so nobody can tell from the length of the silence who woke up.
    /// </summary>
    public sealed class Cue
    {
        public string Id = "";
        public float Duration;
        public List<CueStep> Steps = new List<CueStep>();
        public string Subtitle => Steps.FirstOrDefault(s => s.Kind == CueStepKind.Subtitle)?.Value ?? "";
        /// <summary>What the voice-over says: the line with no player name (a name changes every match and cannot be recorded; the slip of text still shows it).</summary>
        public string VoiceText => Subtitle.Replace("{name}", CueCatalog.SomeoneInVoice);
    }

    /// <summary>The ids of the cues. A new role adds <c>call.&lt;role&gt;</c> and its own events; a new state adds its id here.</summary>
    public static class CueIds
    {
        public const string MatchStart = "match.start";
        public const string NightFalls = "night.falls";
        /// <summary>What the people who are NOT called hear while a role is called (same length as the call).</summary>
        public const string NightIdle = "night.idle";
        public const string DawnQuiet = "dawn.quiet";
        public const string DawnKill = "dawn.kill";
        public const string DawnSaved = "dawn.saved";
        public const string DayStart = "day.start";
        public const string VoteStart = "vote.start";
        public const string VerdictMafia = "verdict.mafia";
        public const string VerdictTown = "verdict.town";
        public const string VerdictNobody = "verdict.nobody";
        public const string OverTown = "over.town";
        public const string OverMafia = "over.mafia";
        public const string DawnShot = "dawn.shot";
        public const string DawnSilenced = "dawn.silenced";
        public const string AvengeStart = "avenge.start";
        public const string AvengeDone = "avenge.done";

        public static string Call(Role role) => "call." + role.ToString().ToLowerInvariant();
        public static string Sleep(Role role) => "sleep." + role.ToString().ToLowerInvariant();
        /// <summary>The card of a role is turned over (revealed at the verdict, at death, at the end).</summary>
        public static string CardReveal(Role role) => "card." + role.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// The cues the match has now (text and timing; the audio files come later and are found by the <see cref="CueStepKind.Voice"/> keys).
    /// To add a voice-over, put the clip under the key; to add an action to a state or a card, add a step to its cue here. Nothing else has to change.
    /// </summary>
    public static class CueCatalog
    {
        /// <summary>How long a night call lasts for everybody, called or not.</summary>
        public const float NightCallSeconds = 20f;     // the owner's night choice time

        public static readonly string[] Languages = { "ar", "en" };

        /// <summary>Said instead of a player's name in the recorded voice.</summary>
        public const string SomeoneInVoice = "أحد اللاعبين";
        /// <summary>The two narrator voices: a man's ("m") and a woman's ("f"); every cue is recorded in both.</summary>
        public static readonly string[] Voices = { "m", "f" };

        /// <summary>The clip's file name for a cue (no dots, so Resources.Load never mistakes the end of the id for an extension): "dawn.kill" -> "dawn_kill".</summary>
        public static string FileName(string cueId) => cueId.Replace('.', '_');

        /// <summary>Where a clip lives under Resources: Narrator/&lt;language&gt;/&lt;m or f&gt;/&lt;file name&gt;.</summary>
        public static string ClipPath(string lang, string voice, string cueId) => "Narrator/" + lang + "/" + voice + "/" + FileName(cueId);

        public static string VoiceKey(string lang, string cueId) => lang + "/" + cueId;

        private static Cue Make(string id, float duration, string subtitle, params CueStep[] extra)
        {
            var c = new Cue { Id = id, Duration = duration };
            c.Steps.Add(new CueStep { Kind = CueStepKind.Subtitle, Value = subtitle, At = 0f });
            c.Steps.Add(new CueStep { Kind = CueStepKind.Voice, Value = VoiceKey("ar", id), At = 0f });       // the Arabic narrator; other languages use the same id under their code
            c.Steps.AddRange(extra);
            return c;
        }

        private static CueStep Anim(string name, float at) => new CueStep { Kind = CueStepKind.Animation, Value = name, At = at };
        private static CueStep Sfx(string name, float at) => new CueStep { Kind = CueStepKind.Sound, Value = name, At = at };

        private static readonly Dictionary<string, Cue> all = Build();

        private static Dictionary<string, Cue> Build()
        {
            var list = new List<Cue>
            {
                Make(CueIds.MatchStart, 4f, "تم توزيع الأدوار. السهرة بدأت.", Anim("role_reveal", 0f)),
                Make(CueIds.NightFalls, 4f, "الكل ينام… أغمض عينيك.", Anim("night_falls", 0f)),
                Make(CueIds.NightIdle, NightCallSeconds, "الليل عم يمشي… اهدأ."),
                Make(CueIds.Call(Role.Mafia), NightCallSeconds, "يا مافيا… اصحوا. مين بدكم تقتلوا الليلة؟", Anim("wake_mafia", 0f)),
                Make(CueIds.Call(Role.Doctor), NightCallSeconds, "يا دكتور… اصحى. مين بدك تحمي الليلة؟", Anim("wake_doctor", 0f)),
                Make(CueIds.Call(Role.Sheriff), NightCallSeconds, "يا شيخ… اصحى. مين بدك تفحص الليلة؟", Anim("wake_sheikh", 0f)),
                Make(CueIds.Call(Role.Silencer), NightCallSeconds, "يا كاتم الصوت… اصحى. مين بدك تسكّت بكرة؟ ما بيحكي النهار كله.", Anim("wake_silencer", 0f)),
                Make(CueIds.Call(Role.Sniper), NightCallSeconds, "يا قنّاص… اصحى. معك طلقة وحدة. بدك تقنص الليلة ولا نأجلها؟", Anim("wake_sniper", 0f)),
                Make(CueIds.Sleep(Role.Silencer), 2f, "تمام. كاتم الصوت ينام.", Anim("sleep_silencer", 0f)),
                Make(CueIds.Sleep(Role.Sniper), 2f, "تمام. القنّاص ينام.", Anim("sleep_sniper", 0f)),
                Make(CueIds.DawnShot, 9f, "وسُمعت طلقة قنّاص بالليل… سقط {name}.", Anim("zoom_seat", 0f), Sfx("gunshot", 2f), Anim("shot", 2f)),
                Make(CueIds.DawnSilenced, 4f, "كاتم الصوت كتم صوت {name} لهالنهار."),
                Make(CueIds.AvengeStart, 6f, "تم إخراج الثأري! وعنده قرار: بياخد معه لاعب.", Anim("zoom_seat", 0f), Anim(CueIds.CardReveal(Role.Avenger), 1f)),
                Make(CueIds.AvengeDone, 7f, "الثأري اختار {name}. خرجوا الاتنين.", Anim("zoom_seat", 0f), Sfx("stamp", 1f), Anim("stamp_out", 1f)),
                Make(CueIds.Sleep(Role.Mafia), 2f, "تمام. المافيا ينامون.", Anim("sleep_mafia", 0f)),
                Make(CueIds.Sleep(Role.Doctor), 2f, "تمام. الدكتور ينام.", Anim("sleep_doctor", 0f)),
                Make(CueIds.Sleep(Role.Sheriff), 2f, "تمام. الشيخ ينام.", Anim("sleep_sheikh", 0f)),
                Make(CueIds.DawnQuiet, 6f, "طلع الصبح. ما مات أحد الليلة.", Anim("dawn", 0f)),
                Make(CueIds.DawnKill, 9f, "طلع الصبح. هالليلة… قامت المافيا بقتل {name}.", Anim("dawn", 0f), Anim("zoom_seat", 3f), Sfx("gunshot", 5.4f), Anim("shot", 5.4f)),
                Make(CueIds.DawnSaved, 9f, "طلع الصبح. المافيا حاولت تقتل {name}… بس في مين أنقذه!", Anim("dawn", 0f), Anim("zoom_seat", 3f), Sfx("gunshot", 5.4f), Anim("shield", 5.6f)),
                Make(CueIds.DayStart, 3f, "النقاش بلّش.", Anim("day", 0f)),
                Make(CueIds.VoteStart, 3f, "وقت التصويت. مين بتشكّوا فيه؟", Anim("vote", 0f)),
                Make(CueIds.VerdictMafia, 7f, "تم إخراج {name}. كان من المافيا!", Anim("zoom_seat", 0f), Sfx("stamp", 1f), Anim("stamp_out", 1f), Anim(CueIds.CardReveal(Role.Mafia), 2f)),
                Make(CueIds.VerdictTown, 7f, "تم إخراج {name}. كان مواطناً بريئاً…", Anim("zoom_seat", 0f), Sfx("stamp", 1f), Anim("stamp_out", 1f)),
                Make(CueIds.VerdictNobody, 4f, "ما استقروا على أحد. ما انطرد حدا."),
                Make(CueIds.OverTown, 8f, "انتهت السهرة. فاز أهل الحارة!", Anim("over", 0f)),
                Make(CueIds.OverMafia, 8f, "انتهت السهرة. فازت المافيا!", Anim("over", 0f)),
            };
            foreach (Role r in System.Enum.GetValues(typeof(Role)))
                list.Add(Make(CueIds.CardReveal(r), 3f, "", Anim("card_flip", 0f)));
            return list.ToDictionary(c => c.Id);
        }

        public static IReadOnlyCollection<Cue> All => all.Values;
        public static bool Has(string id) => all.ContainsKey(id);

        /// <summary>The cue, or null when there is none for this id (a state that has no narration yet simply goes on silently).</summary>
        public static Cue Find(string id) => id != null && all.TryGetValue(id, out var c) ? c : null;

        /// <summary>
        /// What this player hears during the night call of <paramref name="called"/>: the call itself when he is that role (and alive), the idle line otherwise.
        /// Both last <see cref="NightCallSeconds"/>, so the length of the call tells nobody who is awake or whether the role's owner is dead.
        /// </summary>
        public static Cue NightCallFor(Role called, Role? mine, bool iAmAlive) =>
            iAmAlive && mine == called ? Find(CueIds.Call(called)) : Find(CueIds.NightIdle);

        /// <summary>Puts a player's name into the line ({name}).</summary>
        public static string Format(Cue cue, string name) => (cue?.Subtitle ?? "").Replace("{name}", name ?? "");
    }
}
