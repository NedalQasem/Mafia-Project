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
        public string VoiceText => CueCatalog.VoiceText(this, "ar");
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
        /// <summary>The id of a role's card (the portrait file and the card text): the same word as the role, except the Sheriff whose card is the Elder ("sheikh").</summary>
        public static string CardIdOf(Role r) => r == Role.Sheriff ? "sheikh" : r.ToString().ToLowerInvariant();

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
                Make(CueIds.MatchStart, 4f, "وُزّعت الأدوار. السهرة بدت.", Anim("role_reveal", 0f)),
                Make(CueIds.NightFalls, 4f, "الكل ينام… غمّضوا عيونكم.", Anim("night_falls", 0f)),
                Make(CueIds.NightIdle, NightCallSeconds, "الليل يمشي… اهدا."),
                Make(CueIds.Call(Role.Mafia), NightCallSeconds, "يا مافيا… قوموا. منو تبون تقتلون الليلة؟", Anim("wake_mafia", 0f)),
                Make(CueIds.Call(Role.Doctor), NightCallSeconds, "يا دكتور… قوم. منو تبي تحمي الليلة؟", Anim("wake_doctor", 0f)),
                Make(CueIds.Call(Role.Sheriff), NightCallSeconds, "يا شيخ… قوم. منو تبي تفحص الليلة؟", Anim("wake_sheikh", 0f)),
                Make(CueIds.Call(Role.Silencer), NightCallSeconds, "يا كاتم الصوت… قوم. منو تبي تسكته باجر؟ ما راح يتكلم النهار كله.", Anim("wake_silencer", 0f)),
                Make(CueIds.Call(Role.Sniper), NightCallSeconds, "يا قنّاص… قوم. عندك رصاصة وحدة. تبي تقنص الليلة ولا نأجلها؟", Anim("wake_sniper", 0f)),
                Make(CueIds.Sleep(Role.Silencer), 2f, "زين. كاتم الصوت ينام.", Anim("sleep_silencer", 0f)),
                Make(CueIds.Sleep(Role.Sniper), 2f, "زين. القنّاص ينام.", Anim("sleep_sniper", 0f)),
                Make(CueIds.DawnShot, 9f, "انسمعت طلقة قنّاص بالليل… طاح {name}.", Anim("zoom_seat", 0f), Sfx("gunshot", 2f), Anim("shot", 2f), Anim("card_flip", 4.2f)),
                Make(CueIds.DawnSilenced, 4f, "كاتم الصوت كتم صوت {name} لهالنهار."),
                Make(CueIds.AvengeStart, 6f, "انطرد الثأري! وعنده قرار: ياخذ معاه لاعب.", Anim("zoom_seat", 0f), Anim("card_flip", 1.4f)),
                Make(CueIds.AvengeDone, 7f, "الثأري اختار {name}. طلعوا الثنين.", Anim("zoom_seat", 0f), Sfx("stamp", 1f), Anim("stamp_out", 1f), Anim("card_flip", 2.2f)),
                Make(CueIds.Sleep(Role.Mafia), 2f, "زين. المافيا ينامون.", Anim("sleep_mafia", 0f)),
                Make(CueIds.Sleep(Role.Doctor), 2f, "زين. الدكتور ينام.", Anim("sleep_doctor", 0f)),
                Make(CueIds.Sleep(Role.Sheriff), 2f, "زين. الشيخ ينام.", Anim("sleep_sheikh", 0f)),
                Make(CueIds.DawnQuiet, 6f, "طلع الصبح. محد مات الليلة.", Anim("dawn", 0f)),
                Make(CueIds.DawnKill, 9f, "طلع الصبح. الليلة… المافيا قتلت {name}.", Anim("dawn", 0f), Anim("zoom_seat", 3f), Sfx("gunshot", 5.4f), Anim("shot", 5.4f), Anim("card_flip", 6.6f)),
                Make(CueIds.DawnSaved, 9f, "طلع الصبح. المافيا حاولت تقتل {name}… بس فيه أحد أنقذه!", Anim("dawn", 0f), Anim("zoom_seat", 3f), Sfx("gunshot", 5.4f), Anim("shield", 5.6f)),
                Make(CueIds.DayStart, 3f, "بدا النقاش.", Anim("day", 0f)),
                Make(CueIds.VoteStart, 3f, "وقت التصويت. منو تشكون فيه؟", Anim("vote", 0f)),
                Make(CueIds.VerdictMafia, 7f, "انطرد {name}. وكان من المافيا!", Anim("zoom_seat", 0f), Sfx("stamp", 1f), Anim("stamp_out", 1f), Anim("card_flip", 2.2f)),
                Make(CueIds.VerdictTown, 7f, "انطرد {name}. وكان مواطن بريء…", Anim("zoom_seat", 0f), Sfx("stamp", 1f), Anim("stamp_out", 1f), Anim("card_flip", 2.2f)),
                Make(CueIds.VerdictNobody, 4f, "ما اتفقوا على أحد. محد انطرد."),
                Make(CueIds.OverTown, 8f, "خلصت السهرة. فاز أهل الفريج!", Anim("over", 0f)),
                Make(CueIds.OverMafia, 8f, "خلصت السهرة. فازت المافيا!", Anim("over", 0f)),
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

        /// <summary>Said instead of a player's name in the recorded English voice.</summary>
        public const string SomeoneInVoiceEn = "someone";

        /// <summary>The narrator's English lines (the Arabic ones are the cues' own subtitles). A cue with no entry here is told in Arabic. Gender-neutral about players ("they"), since a name never says it.</summary>
        private static readonly Dictionary<string, string> english = new Dictionary<string, string>
        {
            { CueIds.MatchStart, "The roles are dealt. The evening begins." },
            { CueIds.NightFalls, "Everyone, go to sleep… close your eyes." },
            { CueIds.NightIdle, "The night moves on… stay quiet." },
            { CueIds.Call(Role.Mafia), "Mafia… wake up. Who do you take out tonight?" },
            { CueIds.Call(Role.Doctor), "Doctor… wake up. Who do you protect tonight?" },
            { CueIds.Call(Role.Sheriff), "Sheriff… wake up. Who do you investigate tonight?" },
            { CueIds.Call(Role.Silencer), "Silencer… wake up. Who do you silence for tomorrow? They won't say a word all day." },
            { CueIds.Call(Role.Sniper), "Sniper… wake up. You have one bullet. Do you take the shot tonight, or wait?" },
            { CueIds.Sleep(Role.Silencer), "Good. The Silencer goes back to sleep." },
            { CueIds.Sleep(Role.Sniper), "Good. The Sniper goes back to sleep." },
            { CueIds.DawnShot, "A sniper's shot rang out in the night… {name} has fallen." },
            { CueIds.DawnSilenced, "The Silencer has taken {name}'s voice for the day." },
            { CueIds.AvengeStart, "The Avenger is out! And they have one last decision: they take someone with them." },
            { CueIds.AvengeDone, "The Avenger chose {name}. Both are out." },
            { CueIds.Sleep(Role.Mafia), "Good. The Mafia goes back to sleep." },
            { CueIds.Sleep(Role.Doctor), "Good. The Doctor goes back to sleep." },
            { CueIds.Sleep(Role.Sheriff), "Good. The Sheriff goes back to sleep." },
            { CueIds.DawnQuiet, "Morning has come. Nobody died tonight." },
            { CueIds.DawnKill, "Morning has come. Last night… the Mafia killed {name}." },
            { CueIds.DawnSaved, "Morning has come. The Mafia tried to kill {name}… but someone saved them!" },
            { CueIds.DayStart, "The discussion begins." },
            { CueIds.VoteStart, "Time to vote. Who do you suspect?" },
            { CueIds.VerdictMafia, "{name} is out. They were Mafia!" },
            { CueIds.VerdictTown, "{name} is out. They were an innocent citizen…" },
            { CueIds.VerdictNobody, "No one could agree. Nobody is out." },
            { CueIds.OverTown, "The night is over. The neighborhood wins!" },
            { CueIds.OverMafia, "The night is over. The Mafia wins!" },
        };

        /// <summary>
        /// What the Arabic voice-over says: the same Gulf line as the slip but with the diacritics (tashkeel) a voice model needs to read the dialect, and with "أحد اللاعبين" in place of a name.
        /// Taking the diacritics off must give back the slip's line exactly (a test keeps the two together).
        /// </summary>
        private static readonly Dictionary<string, string> arabicVoice = new Dictionary<string, string>
        {
            { CueIds.MatchStart, "وُزِّعَتِ الأَدْوَار. السَّهْرَةُ بَدَت." },
            { CueIds.NightFalls, "الكُلّ يِنَام… غَمِّضُوا عُيُونَكُم." },
            { CueIds.NightIdle, "اللَّيْلُ يِمْشِي… اهْدَا." },
            { CueIds.Call(Role.Mafia), "يَا مَافِيَا… قُومُوا. مِنُو تَبُّونَ تَقْتُلُونَ اللَّيْلَة؟" },
            { CueIds.Call(Role.Doctor), "يَا دُكْتُور… قُوم. مِنُو تَبِي تَحْمِي اللَّيْلَة؟" },
            { CueIds.Call(Role.Sheriff), "يَا شَيْخ… قُوم. مِنُو تَبِي تَفْحَصُ اللَّيْلَة؟" },
            { CueIds.Call(Role.Silencer), "يَا كَاتِمَ الصَّوْت… قُوم. مِنُو تَبِي تَسْكِتُه بَاجِر؟ مَا رَاح يِتْكَلَّمُ النَّهَارَ كُلَّه." },
            { CueIds.Call(Role.Sniper), "يَا قَنَّاص… قُوم. عِنْدَكَ رَصَاصَةٌ وَحْدَة. تَبِي تَقْنِصُ اللَّيْلَة وَلَّا نَأَجِّلْهَا؟" },
            { CueIds.Sleep(Role.Silencer), "زِين. كَاتِمُ الصَّوْتِ يِنَام." },
            { CueIds.Sleep(Role.Sniper), "زِين. القَنَّاصُ يِنَام." },
            { CueIds.DawnShot, "اِنْسَمَعَتْ طَلْقَةُ قَنَّاصٍ بِاللَّيْل… طَاحَ أَحَدُ اللَّاعِبِين." },
            { CueIds.DawnSilenced, "كَاتِمُ الصَّوْتِ كَتَمَ صَوْتَ أَحَدِ اللَّاعِبِين لِهَالنَّهَار." },
            { CueIds.AvengeStart, "اِنْطَرَدَ الثَّأْرِي! وَعِنْدَهُ قَرَار: يَاخُذُ مَعَاهُ لَاعِب." },
            { CueIds.AvengeDone, "الثَّأْرِي اِخْتَارَ أَحَدَ اللَّاعِبِين. طَلَعُوا الثِّنْيَن." },
            { CueIds.Sleep(Role.Mafia), "زِين. المَافِيَا يِنَامُون." },
            { CueIds.Sleep(Role.Doctor), "زِين. الدُّكْتُورُ يِنَام." },
            { CueIds.Sleep(Role.Sheriff), "زِين. الشَّيْخُ يِنَام." },
            { CueIds.DawnQuiet, "طَلَعَ الصُّبْح. مَحَدْ مَاتَ اللَّيْلَة." },
            { CueIds.DawnKill, "طَلَعَ الصُّبْح. اللَّيْلَة… المَافِيَا قَتَلَتْ أَحَدَ اللَّاعِبِين." },
            { CueIds.DawnSaved, "طَلَعَ الصُّبْح. المَافِيَا حَاوَلَتْ تَقْتُلُ أَحَدَ اللَّاعِبِين… بَسْ فِيهِ أَحَدٌ أَنْقَذَه!" },
            { CueIds.DayStart, "بَدَا النِّقَاش." },
            { CueIds.VoteStart, "وَقْتُ التَّصْوِيت. مِنُو تَشُكُّونَ فِيه؟" },
            { CueIds.VerdictMafia, "اِنْطَرَدَ أَحَدُ اللَّاعِبِين. وَكَانَ مِنَ المَافِيَا!" },
            { CueIds.VerdictTown, "اِنْطَرَدَ أَحَدُ اللَّاعِبِين. وَكَانَ مُوَاطِن بَرِيء…" },
            { CueIds.VerdictNobody, "مَا اِتَّفَقُوا عَلَى أَحَد. مَحَدْ اِنْطَرَد." },
            { CueIds.OverTown, "خَلَصَتِ السَّهْرَة. فَازَ أَهْلُ الفَرِيج!" },
            { CueIds.OverMafia, "خَلَصَتِ السَّهْرَة. فَازَتِ المَافِيَا!" },
        };

        /// <summary>The Arabic diacritics (harakat, shadda, sukun, dagger alef): what a diacritized voice line has over the slip's line.</summary>
        public static string StripDiacritics(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "[ً-ٰٟ]", "");

        /// <summary>The line of a cue in a language ("ar" or "en"), with <c>{name}</c> left in; the Arabic line when the language has none.</summary>
        public static string Text(Cue cue, string lang)
        {
            if (cue == null) return "";
            return lang == "en" && english.TryGetValue(cue.Id, out var t) ? t : StripDiacritics(cue.Subtitle);      // the slip is read, not sung: a shadda inside the kit's font landed on the wrong letter on the phone
        }

        /// <summary>What the voice-over says in a language: the line with no player name (a name cannot be recorded).</summary>
        public static string VoiceText(Cue cue, string lang)
        {
            if (cue != null && lang != "en" && arabicVoice.TryGetValue(cue.Id, out var diacritized)) return diacritized;
            string line = Text(cue, lang);
            bool en = lang == "en" && english.ContainsKey(cue?.Id ?? "");
            string said = line.Replace("{name}", en ? SomeoneInVoiceEn : SomeoneInVoice);
            return en && line.StartsWith("{name}") ? char.ToUpperInvariant(said[0]) + said.Substring(1) : said;       // "Someone is out." starts a sentence
        }

        /// <summary>Puts a player's name into the line ({name}).</summary>
        public static string Format(Cue cue, string name, string lang = "ar") => Text(cue, lang).Replace("{name}", name ?? "");
    }
}
