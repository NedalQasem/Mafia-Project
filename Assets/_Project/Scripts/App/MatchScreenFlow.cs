using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.Data.Cards;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using MafiaNight.Services.Match;
using MafiaNight.UI.Kit;
using UnityEngine;
using SeatView = MafiaNight.MatchRules.Net.SeatView;

namespace MafiaNight.App
{
    /// <summary>
    /// Puts the live match on the screens of Docs/mockups/match.html: it reads one player's <see cref="ClientMatchController"/>, picks the
    /// frame for the phase and the player's role, fills the numbered texts, lays out the seats and turns the buttons into acts.
    /// The part numbers below are the node numbers of Art/UI/Match/frames.json.
    /// </summary>
    public sealed class MatchScreenFlow : MonoBehaviour
    {
        private struct Bar { public int Timer, Title, Sub; public Bar(int t, int ti, int s) { Timer = t; Title = ti; Sub = s; } }
        private static readonly Dictionary<string, Bar> Bars = new Dictionary<string, Bar>
        {
            { "p2_mafia", new Bar(58, 60, 62) }, { "p2_doc", new Bar(52, 54, 56) }, { "p2_sher", new Bar(54, 56, 58) }, { "p2_civ", new Bar(60, 62, 64) },
            { "p3_kill", new Bar(47, 49, 51) }, { "p3_save", new Bar(47, 49, 51) }, { "p4", new Bar(56, 58, 60) }, { "p5", new Bar(56, 58, 60) },
            { "p6_civ", new Bar(47, 49, 51) }, { "p6_mafia", new Bar(47, 49, 51) }, { "p7", new Bar(52, 54, 56) },
        };

        private static readonly Color CivChip = new Color32(0xB9, 0xA6, 0x7E, 255), MafiaChip = new Color32(0xE0, 0x58, 0x5F, 255),
                                      DocChip = new Color32(0x4F, 0xA5, 0x8B, 255), SherChip = new Color32(0x6F, 0xA0, 0xD6, 255),
                                      SniperChip = new Color32(0xB8, 0x8A, 0x3C, 255), AvengerChip = new Color32(0xD9, 0x7A, 0x3A, 255);

        private KitMatchView view;
        private ClientMatchController c;
        private Action tick;
        private string frame = "";
        private string picked;              // the player this device has selected (night target / vote)
        private bool confirmed;             // sent for this phase
        private Phase shownPhase = Phase.Lobby;
        private int shownRound = -1;
        private int lastSeconds = -1;
        private int confirmedRound = -1;
        private RewardResult reward;
        // the second choices of the new roles, made in a sheet: the silencer's target, the sniper's shot
        private string silencePicked, sniperPicked;
        private bool silenceConfirmed, sniperDecided, sheetWanted;

        /// <summary>What the rewards server paid; the end screen shows it once it arrives (until then the block stays hidden).</summary>
        public void SetReward(RewardResult r) { reward = r; if (c != null && c.Phase == Phase.GameOver) Redraw(); }

        /// <summary>The player asked to leave the table screens (back to the room).</summary>
        public event Action LeaveRequested;
        /// <summary>The player asked for another match with the same room.</summary>
        public event Action PlayAgainRequested;

        /// <param name="tickSource">Called every frame before drawing (a local match advances its host and bots here).</param>
        public void Attach(KitMatchView matchView, ClientMatchController client, Action tickSource = null)
        {
            if (c != null) { c.Changed -= Redraw; c.PhaseChanged -= OnPhase; }
            view = matchView; c = client; tick = tickSource;
            c.Changed += Redraw; c.PhaseChanged += OnPhase;
            Redraw();
        }

        private void OnDestroy() { if (c != null) { c.Changed -= Redraw; c.PhaseChanged -= OnPhase; } }

        private void Update()
        {
            if (c == null) return;
            tick?.Invoke();
            if (KitPickSheet.IsOpen) KitPickSheet.SetTimer(Clock(Mathf.CeilToInt(c.SecondsLeft)));
            if (Bars.TryGetValue(frame, out var bar))
            {
                int s = Mathf.CeilToInt(c.SecondsLeft);
                if (s != lastSeconds)
                {
                    lastSeconds = s;
                    view.SetText(frame, bar.Timer, Clock(s));
                    UpdateCountdownLines(s);
                }
            }
        }

        private void OnPhase(Phase p) { picked = null; confirmed = false; }

        // ---------------------------------------------------------------- helpers

        private static string Clock(int seconds) { seconds = Mathf.Max(0, seconds); return (seconds / 60) + ":" + (seconds % 60).ToString("00"); }

        private static string RoleKey(Role r)
        {
            switch (r) { case Role.Mafia: case Role.Silencer: return "mafia"; case Role.Doctor: return "doc"; case Role.Sheriff: return "sher"; default: return "civ"; }   // the frames of the four first roles; the other three share them and put their own card and sheet over
        }

        private static string RoleName(Role r)
        {
            switch (r) { case Role.Mafia: return "مافيا"; case Role.Silencer: return "كاتم الصوت"; case Role.Doctor: return "طبيب"; case Role.Sheriff: return "شرطي"; case Role.Sniper: return "قنّاص"; case Role.Avenger: return "الثأري"; default: return "مواطن"; }
        }

        private static Color RoleColor(Role? r)
        {
            switch (r) { case Role.Mafia: case Role.Silencer: return MafiaChip; case Role.Doctor: return DocChip; case Role.Sheriff: return SherChip; case Role.Sniper: return SniperChip; case Role.Avenger: return AvengerChip; default: return CivChip; }
        }

        private static string Digits(int n) => n.ToString();

        /// <summary>+٤٥ / −٢٠ in Arabic-Indic digits, as the mockup prints its reward numbers.</summary>
        private static string Signed(int n)
        {
            var sb = new System.Text.StringBuilder(n < 0 ? "−" : "+");
            foreach (char ch in Mathf.Abs(n).ToString()) sb.Append((char)('٠' + (ch - '0')));
            return sb.ToString();
        }

        private string Name(string id) => c.Seat(id)?.Name ?? "";

        private int AliveCount => c.Seats.Count(s => s.Alive && !s.Left);

        private static string Reason(ActionResult r)
        {
            switch (r)
            {
                case ActionResult.SelfSaveUsed: return "أنقذت نفسك من قبل، اختر غيرك.";
                case ActionResult.MafiaTeammate: return "ما بتقدر تختار واحد من المافيا.";
                case ActionResult.AlreadyDone: return "فعلتها هالليلة.";
                case ActionResult.AbilityUsed: return "أطلقت طلقتك الوحيدة.";
                case ActionResult.InvalidTarget: return "اختيار غير صالح.";
                case ActionResult.WrongPhase: return "فات الوقت.";
                default: return "";
            }
        }

        // ---------------------------------------------------------------- picking the frame

        private void Redraw()
        {
            if (c == null || view == null) return;
            if (c.Round != confirmedRound && c.Phase == Phase.Night) { confirmedRound = c.Round; confirmed = false; picked = null; silencePicked = sniperPicked = null; silenceConfirmed = sniperDecided = false; }
            lastSeconds = -1;
            sheetWanted = false;
            if (c.Phase != Phase.RoleReveal) KitRoleReveal.CloseCurrent();
            switch (c.Phase)
            {
                case Phase.RoleReveal: DrawReveal(); break;
                case Phase.Avenge: DrawAvenge(); break;
                case Phase.Night: if (c.IAmAlive) DrawNight(); else DrawSpectator(); break;
                case Phase.Dawn: if (c.IAmAlive) DrawDawn(); else DrawSpectator(); break;
                case Phase.Day: if (c.IAmAlive) DrawDay(); else DrawSpectator(); break;
                case Phase.Voting: if (c.IAmAlive) DrawVoting(); else DrawSpectator(); break;
                case Phase.Verdict: if (c.IAmAlive) DrawVerdict(); else DrawSpectator(); break;
                case Phase.GameOver: DrawOver(); break;
            }
            if (!sheetWanted) KitPickSheet.CloseCurrent();
            shownPhase = c.Phase; shownRound = c.Round;
        }

        private void UpdateCountdownLines(int s)
        {
            if (frame == "p3_kill" || frame == "p3_save") view.SetText(frame, 57, "يبدأ تلقائياً بعد " + s + " ثوانٍ");
            else if (frame == "p6_civ" || frame == "p6_mafia") view.SetText(frame, 57, "تبدأ تلقائياً بعد " + s + " ثوانٍ");
        }

        private void Use(string name) { frame = name; view.Show(name); }

        private List<SeatSpec> MakeSeats(Func<SeatView, SeatSpec, SeatSpec> shape)
        {
            var list = new List<SeatSpec>();
            foreach (var s in c.Seats)
            {
                var spec = new SeatSpec { Id = s.Id, Name = s.Name, Me = s.IsMe, Look = !s.Alive || s.Left ? SeatLook.Dead : SeatLook.Normal };
                if (s.IsMate) spec.Tag = SeatTag.Mafia;
                list.Add(shape != null ? shape(s, spec) : spec);
            }
            return list;
        }

        // ---------------------------------------------------------------- role reveal

        private static bool HasOwnCard(Role r) => r == Role.Silencer || r == Role.Sniper || r == Role.Avenger;

        /// <summary>The roles that came after the four first screens show their card full screen (it turns over to what the role does).</summary>
        private void DrawRevealCard(Role role)
        {
            Use("p1_civ");
            if (KitRoleReveal.IsOpen) { KitRoleReveal.SetButton(confirmed ? "بانتظار البقية" : "فهمت، ابدأ"); return; }      // already on screen: do not turn the card back over
            var def = RoleCards.Find(role.ToString().ToLowerInvariant());
            string line = def != null ? def.Sub : "";
            if (role == Role.Silencer)
            {
                var mates = c.Seats.Where(s => s.IsMate).Select(s => s.Name).ToList();
                line = mates.Count > 0 ? "رفقاؤك بالمافيا: " + string.Join("، ", mates) : "أنت المافيا الوحيد";
            }
            KitRoleReveal.Show(view.transform, def, new CardLayers(), line, confirmed ? "بانتظار البقية" : "فهمت، ابدأ", () => { if (confirmed) return; confirmed = true; c.Ready(); KitRoleReveal.SetButton("بانتظار البقية"); });
        }

        private void DrawReveal()
        {
            var role = c.MyRole ?? Role.Civilian;
            if (HasOwnCard(role)) { DrawRevealCard(role); return; }
            string f = "p1_" + RoleKey(role);
            Use(f);
            view.SetText(f, 4, "الجولة الأولى · " + c.Seats.Count + " لاعبين");
            int readyBtn = role == Role.Mafia ? 30 : 19, readyText = role == Role.Mafia ? 32 : 21, readySmall = role == Role.Mafia ? 34 : 23;
            if (role == Role.Mafia)
            {
                var mates = c.Seats.Where(s => s.IsMate).Select(s => s.Name).ToList();
                view.SetText(f, 14, mates.Count > 0 ? "شركاؤك في المافيا" : "أنت المافيا الوحيد");
                SetMate(f, 18, mates.Count > 0 ? mates[0] : null);
                SetMate(f, 21, mates.Count > 1 ? string.Join("، ", mates.Skip(1)) : null);
            }
            view.SetText(f, readyText, confirmed ? "بانتظار البقية" : "فهمت، ابدأ");
            view.SetText(f, readySmall, "تبدأ الليلة الأولى بعد أن يجهز الجميع");
            view.SetTap(f, readyBtn, () => { if (confirmed) return; confirmed = true; c.Ready(); view.SetText(f, readyText, "بانتظار البقية"); });
        }

        private void SetMate(string f, int textNode, string name)
        {
            var t = view.Node(f, textNode);
            if (t == null) return;
            var chip = t.parent;     // the little name plate around the text
            if (chip != null) chip.gameObject.SetActive(!string.IsNullOrEmpty(name));
            if (!string.IsNullOrEmpty(name)) view.SetText(f, textNode, name);
        }

        // ---------------------------------------------------------------- night

        private void DrawNight()
        {
            var role = c.MyRole ?? Role.Civilian;
            string f = "p2_" + RoleKey(role);
            Use(f);
            var bar = Bars[f];
            string sub = "الجولة " + c.Round;

            if (role == Role.Civilian || role == Role.Avenger || role == Role.Sniper)
            {
                view.SetText(f, bar.Sub, sub);
                view.SetSeats(f, MakeSeats((s, spec) => { if (s.Alive && !s.Left) { spec.Look = SeatLook.Sleep; spec.Dim = !s.IsMe; } return spec; }), null);
                if (role == Role.Sniper && !c.ShotUsed && !sniperDecided) ShowSniperSheet();
                return;
            }

            bool mafia = c.IAmMafia, doc = role == Role.Doctor;
            view.SetText(f, bar.Sub, sub + (confirmed ? " · تم" : " · دورك الآن"));
            var votes = new Dictionary<string, int>();
            if (mafia) foreach (var v in c.MafiaVotes.Values) { votes.TryGetValue(v, out var n); votes[v] = n + 1; }

            view.SetSeats(f, MakeSeats((s, spec) =>
            {
                if (!s.Alive || s.Left) return spec;
                if (s.Id == picked) spec.Look = mafia ? SeatLook.Pick : doc ? SeatLook.PickDoc : SeatLook.PickSher;
                if (mafia && votes.TryGetValue(s.Id, out var n) && n > 0) { spec.Tag = SeatTag.Count; spec.TagText = n.ToString(); }
                if (!mafia && !doc)
                {   // the sheriff sees the answers he got
                    foreach (var rep in c.SheriffReports) if (rep.Key == s.Id) { spec.Tag = rep.Value ? SeatTag.Mafia : SeatTag.Check; spec.TagText = "✓"; }
                }
                return spec;
            }), OnNightSeatTapped);

            string pickedName = picked != null ? Name(picked) : null;
            int goBtn, goSmall, ghost, hint;
            if (mafia) { goBtn = 74; goSmall = 78; ghost = 79; hint = 8; }
            else if (doc) { goBtn = 68; goSmall = 72; ghost = 73; hint = 8; }
            else { goBtn = 78; goSmall = 82; ghost = 83; hint = 8; }
            view.SetActive(f, ghost, false);
            view.SetText(f, goSmall, pickedName != null ? "المختار: " + pickedName : "اضغط على لاعب أولاً");
            string reason = Reason(c.LastResult);
            if (c.LastResult != ActionResult.Ok && !string.IsNullOrEmpty(reason) && c.LastResultKind != "ready") view.SetText(f, hint, reason);
            else view.SetText(f, hint, "اضغط على لاعب");

            if (!mafia && !doc)
            {   // last answer of the sheriff
                if (c.SheriffReports.Count > 0)
                {
                    var last = c.SheriffReports[c.SheriffReports.Count - 1];
                    view.SetText(f, 66, "نتيجة آخر فحص");
                    view.SetText(f, 68, Name(last.Key) + ":");
                    view.SetText(f, 70, last.Value ? "مافيا" : "بريء");
                }
                else
                {
                    view.SetText(f, 66, "لا نتيجة بعد");
                    view.SetText(f, 68, "ما تحققت من أحد");
                    view.SetText(f, 70, "");
                }
            }
            if (doc) view.SetText(f, 66, "اللي تنقذه ما يموت هالليلة حتى لو اختاره المافيا. تقدر تنقذ نفسك مرة وحدة بالمباراة.");

            view.SetTap(f, goBtn, () => ConfirmNight(role));
            if (role == Role.Silencer && confirmed && !silenceConfirmed) ShowSilenceSheet();
        }

        private void OnNightSeatTapped(string id)
        {
            var s = c.Seat(id);
            if (s == null || !s.Alive || s.Left) return;
            if (c.IAmMafia && (s.IsMate || s.IsMe)) return;
            picked = id;
            Redraw();
        }

        private void ConfirmNight(Role role)
        {
            if (picked == null) return;
            switch (role)
            {
                case Role.Mafia: case Role.Silencer: c.MafiaPick(picked); break;
                case Role.Doctor: c.DoctorSave(picked); break;
                case Role.Sheriff: c.SheriffCheck(picked); break;
            }
            confirmed = true;
            Redraw();
        }

        // ---------------------------------------------------------------- sheets of the new roles

        private List<KitPickSheet.Row> LivingOthers(bool withoutMates)
        {
            var rows = new List<KitPickSheet.Row>();
            foreach (var s in c.Seats) if (s.Alive && !s.Left && !s.IsMe && !(withoutMates && s.IsMate)) rows.Add(new KitPickSheet.Row { Id = s.Id, Name = s.Name });
            return rows;
        }

        private void OpenSheet(KitPickSheet.Spec spec) { sheetWanted = true; spec.Timer = Clock(Mathf.CeilToInt(c.SecondsLeft)); KitPickSheet.Show(view.transform, spec); }

        /// <summary>The silencer's second choice, after the kill: who may not speak or write tomorrow.</summary>
        private void ShowSilenceSheet()
        {
            OpenSheet(new KitPickSheet.Spec
            {
                Title = "كاتم الصوت · مين تسكّت؟",
                Body = "المُسكَت ما بيحكي ولا بيكتب طول النهار، وبيقدر يصوّت بس. الكل بيعرف إنه انسكت، بس ما حدا بيعرف مين سكّته.",
                Rows = LivingOthers(true), Selected = silencePicked,
                Picked = id => { silencePicked = id; Redraw(); },
                Buttons = new List<KitPickSheet.Button2> { new KitPickSheet.Button2 { Label = silencePicked == null ? "اختر لاعباً" : "أسكِت " + Name(silencePicked), Disabled = silencePicked == null, Run = () => { c.Silence(silencePicked); silenceConfirmed = true; Redraw(); } } },
            });
        }

        /// <summary>The sniper's night: fire the only bullet at somebody, or keep it.</summary>
        private void ShowSniperSheet()
        {
            OpenSheet(new KitPickSheet.Spec
            {
                Title = "القنّاص · معك طلقة وحدة",
                Body = "إذا أصبت مافيا بيطلع وإنت بتضل. إذا أصبت مواطن بيطلعوا الاتنين. الطبيب ما بيقدر يوقفها وما حدا بيعرف مين قنص.",
                Rows = LivingOthers(false), Selected = sniperPicked,
                Picked = id => { sniperPicked = id; Redraw(); },
                Buttons = new List<KitPickSheet.Button2>
                {
                    new KitPickSheet.Button2 { Label = sniperPicked == null ? "اختر هدفاً" : "أطلق على " + Name(sniperPicked), Disabled = sniperPicked == null, Run = () => { c.Snipe(sniperPicked); sniperDecided = true; Redraw(); } },
                    new KitPickSheet.Button2 { Label = "مش هالليلة", Quiet = true, Run = () => { c.Snipe(null); sniperDecided = true; Redraw(); } },
                },
            });
        }

        // ---------------------------------------------------------------- the Avenger's moment

        private void DrawAvenge()
        {
            string f = "p4";
            Use(f);
            string avenger = Name(c.AvengerId);
            view.SetText(f, 6, "قرار الثأري");
            view.SetText(f, 8, avenger);
            view.SetText(f, 60, "الثأري · الجولة " + c.Round);
            view.SetText(f, 63, "المايك مفتوح");
            view.SetTap(f, 61, () => { });
            view.SetText(f, 68, "النقاش");
            var tally = view.Node(f, 71);
            if (tally != null && tally.parent != null && tally.parent.parent != null) tally.parent.parent.gameObject.SetActive(false);
            string pick = c.AvengeTargetId != null ? Name(c.AvengeTargetId) : null;
            view.SetText(f, 75, c.IAmTheAvenger
                ? "كرتك انكشف والكل بيسمعك. اختر مين بيطلع معك."
                : "تم إخراج الثأري! وعنده قرار: بياخد معه لاعب. ناقشوه بالصوت وأقنعوه." + (pick != null ? " اختار " + pick + "." : ""));
            view.SetActive(f, 79, false);
            view.SetActive(f, 82, false);
            view.SetSeats(f, MakeSeats((s, spec) => { if (s.Id == c.AvengeTargetId && s.Alive && !s.Left) spec.Look = SeatLook.Pick; return spec; }), null);
            if (c.IAmTheAvenger)
            {
                OpenSheet(new KitPickSheet.Spec
                {
                    Title = "قرارك يا ثأري",
                    Body = "اختر مين بيطلع معك، علناً. بعد اختيارك معك ٣ ثواني تغيّر رأيك، وبعدها يتثبّت.",
                    Rows = LivingOthers(false), Selected = c.AvengeTargetId,
                    Picked = id => c.AvengerPick(id),
                    Buttons = new List<KitPickSheet.Button2> { new KitPickSheet.Button2 { Label = "تراجع عن اختياري", Quiet = true, Disabled = c.AvengeTargetId == null, Run = () => c.AvengerPick(null) } },
                });
            }
        }

        // ---------------------------------------------------------------- dawn

        private void DrawDawn()
        {
            bool died = c.DawnDiedId != null;
            string f = died ? "p3_kill" : "p3_save";
            Use(f);
            view.SetText(f, 51, "الجولة " + c.Round);
            view.SetText(f, 67, "الجولة " + c.Round);
            string extra = DawnExtras();
            if (died)
            {
                string who = Name(c.DawnDiedId);
                var role = c.DawnDiedRole ?? Role.Civilian;
                view.ReplaceTextImage(f, 69, "وُجد " + who + " مقتولاً", 24f);
                view.SetText(f, 71, "سقط بيد المافيا في ليلة أمس، وكان " + (role == Role.Civilian ? "من أهل المدينة." : role == Role.Mafia ? "من المافيا." : RoleName(role) + ".") + extra);
                view.ReplaceTextImage(f, 73, RoleName(role), 18f);
                view.SetText(f, 75, "ما بقي من اللاعبين: " + Digits(AliveCount));
            }
            else
            {
                view.SetText(f, 71, (c.DawnSaved ? "حاولت المافيا القتل لكن الطبيب كان أسرع." : "مرّت الليلة بدون ضحية للمافيا.") + extra);
                view.SetText(f, 75, c.DawnShotDeaths.Count > 0 ? "ما بقي من اللاعبين: " + Digits(AliveCount) : "كلكم لسا هون: " + Digits(AliveCount) + " لاعبين");
            }
            view.SetSeats(f, MakeSeats(null), null);
        }

        /// <summary>The rest of the morning report: a shot in the night (who shot is never said) and who was silenced for the day.</summary>
        private string DawnExtras()
        {
            var sb = new System.Text.StringBuilder();
            if (c.DawnShotDeaths.Count > 0)
            {
                sb.Append("\nوسُمعت طلقة قنّاص بالليل: ");
                for (int i = 0; i < c.DawnShotDeaths.Count; i++)
                {
                    var d = c.DawnShotDeaths[i];
                    if (i > 0) sb.Append("، ");
                    sb.Append(Name(d.Key)); if (d.Value.HasValue) sb.Append(" (").Append(RoleName(d.Value.Value)).Append(")");
                }
                sb.Append(".");
            }
            if (c.SilencedId != null) sb.Append("\nوأسكت كاتم الصوت ").Append(Name(c.SilencedId)).Append(" لهالنهار.");
            if (c.MyRole == Role.Doctor && c.DoctorReports.Count > 0)
            {
                var r = c.DoctorReports[c.DoctorReports.Count - 1];
                sb.Append(r.Value ? "\nحمايتك لـ" + Name(r.Key) + " نجحت." : "\nما احتاج " + Name(r.Key) + " حمايتك هالليلة.");
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- day

        private void DrawDay()
        {
            string f = "p4";
            Use(f);
            int needed = c.SkipNeeded > 0 ? c.SkipNeeded : AliveCount / 2 + 1;
            view.SetText(f, 6, "وقت النقاش");
            view.SetText(f, 8, "اكشفوا المافيا");
            view.SetText(f, 60, "نقاش · الجولة " + c.Round);
            view.SetText(f, 63, "تخطي النقاش " + c.SkipCount + "/" + needed);
            view.SetTap(f, 61, () => c.SkipDiscussion());
            view.SetText(f, 68, "النقاش");
            var tally = view.Node(f, 71);
            if (tally != null && tally.parent != null && tally.parent.parent != null) tally.parent.parent.gameObject.SetActive(false);
            string help = "تكلّموا واكشفوا المافيا. التصويت يبدأ لما ينتهي الوقت أو يوافق أغلبكم على التخطي.";
            if (c.IAmSilenced) help = "كاتم الصوت أسكتك لهالنهار: بتسمع وبتصوّت، بس ما بتحكي ولا بتكتب.";
            else if (c.SilencedId != null) help += "\nالمُسكَت اليوم: " + Name(c.SilencedId) + ".";
            if (c.MyRole == Role.Doctor && c.DoctorReports.Count > 0)
            {
                var r = c.DoctorReports[c.DoctorReports.Count - 1];
                help += r.Value ? "\nحمايتك لـ" + Name(r.Key) + " نجحت." : "\nما احتاج " + Name(r.Key) + " حمايتك هالليلة.";
            }
            view.SetText(f, 75, help);
            view.SetActive(f, 79, false);
            view.SetActive(f, 82, false);
            view.SetSeats(f, MakeSeats(null), null);
        }

        // ---------------------------------------------------------------- voting

        private void DrawVoting()
        {
            string f = "p5";
            Use(f);
            int cast = c.Tally.Values.Sum();
            view.SetText(f, 63, "صوّت " + cast + " من " + AliveCount);
            view.SetText(f, 68, picked == null ? (confirmed ? "امتنعت عن التصويت" : "اختر من تطرده") : (confirmed ? "صوتك على: " : "اخترت: ") + Name(picked));
            var top = c.Tally.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Take(3).ToList();
            int[] spans = { 71, 73, 75 };
            for (int i = 0; i < spans.Length; i++)
            {
                var t = view.Node(f, spans[i]);
                if (t != null && t.parent != null) t.parent.gameObject.SetActive(i < top.Count);
                if (i < top.Count) view.SetText(f, spans[i], Name(top[i].Key) + " · " + top[i].Value);
            }
            view.FitChips(f, spans, 334f);
            view.SetText(f, 83, picked != null ? "على " + Name(picked) : "اضغط على لاعب");
            view.SetText(f, 81, confirmed ? "غيّر صوتي" : "ثبّت صوتي");
            view.SetTap(f, 79, () => { if (picked == null) return; c.Vote(picked); confirmed = true; Redraw(); });
            view.SetTap(f, 84, () => { c.Vote(null); picked = null; confirmed = true; Redraw(); });

            view.SetSeats(f, MakeSeats((s, spec) =>
            {
                if (!s.Alive || s.Left) return spec;
                if (s.Id == picked) spec.Look = SeatLook.Pick;
                if (c.Tally.TryGetValue(s.Id, out var n) && n > 0) { spec.Tag = SeatTag.Count; spec.TagText = n.ToString(); }
                return spec;
            }), id =>
            {
                var s = c.Seat(id);
                if (s == null || !s.Alive || s.Left || s.IsMe) return;
                picked = id; confirmed = false; Redraw();
            });
        }

        // ---------------------------------------------------------------- verdict

        private void DrawVerdict()
        {
            bool removed = c.VerdictRemovedId != null;
            var role = c.VerdictRemovedRole ?? Role.Civilian;
            string f = removed && role.IsMafiaTeam() ? "p6_mafia" : "p6_civ";
            Use(f);
            view.SetText(f, 51, "الجولة " + c.Round);
            if (removed)
            {
                view.SetText(f, 62, "قرار المدينة بأغلبية " + c.VerdictVotes + " أصوات");
                view.ReplaceTextImage(f, 64, "طُرد " + Name(c.VerdictRemovedId) + " من اللعبة", 24f);
                view.ReplaceTextImage(f, 66, RoleName(role), 18f);
                view.SetText(f, 68, role.IsMafiaTeam() ? "أحسنتم! أسقطتم واحداً من المافيا." : "طُرد بريء... المافيا لسا بينكم.");
            }
            else
            {
                view.SetText(f, 62, "تعادل الأصوات");
                view.ReplaceTextImage(f, 64, "ما حدا طلع", 24f);
                view.ReplaceTextImage(f, 66, "تعادل", 18f);
                view.SetText(f, 68, "ما اتفقتم على أحد. الليلة جاية.");
            }
            var voters = view.Node(f, 71);
            if (voters != null && voters.parent != null) voters.parent.gameObject.SetActive(false);
            view.SetSeats(f, MakeSeats(null), null);
        }

        // ---------------------------------------------------------------- watching

        private void DrawSpectator()
        {
            string f = "p7";
            Use(f);
            string phase;
            switch (c.Phase) { case Phase.Night: phase = "الليل"; break; case Phase.Dawn: phase = "الفجر"; break; case Phase.Voting: phase = "التصويت"; break; case Phase.Verdict: phase = "القرار"; break; case Phase.Avenge: phase = "قرار الثأري"; break; default: phase = "النهار"; break; }
            view.SetText(f, 56, phase + " · الجولة " + c.Round);
            view.SetText(f, 74, "");
            view.SetActive(f, 75, false);
            view.SetSeats(f, MakeSeats((s, spec) => spec), null);
            view.SetTap(f, 70, () => LeaveRequested?.Invoke());
            view.SetText(f, 72, "للغرفة");
        }

        // ---------------------------------------------------------------- game over

        private void DrawOver()
        {
            bool town = c.Winner == Winner.Town;
            string f = town ? "p8_civ" : "p8_mafia";
            Use(f);
            view.SetText(f, 89, "الجولة " + c.Round);
            // the rewards come from the rewards server; until they arrive the block stays hidden and the list moves up
            bool paid = reward != null && reward.Status == RewardStatus.Granted;
            var rew = view.Node(f, 8);
            if (rew != null && rew.parent != null && rew.parent.parent != null) rew.parent.parent.gameObject.SetActive(paid);
            if (paid)
            {
                view.SetText(f, 8, Signed(reward.Xp));
                view.SetText(f, 12, Signed(reward.Coins));
                view.SetText(f, 16, Signed(reward.RankPoints));
            }

            const int firstRow = 23, rowSize = 6, templateRows = 10;
            var seats = c.Seats.ToList();
            seats.Sort((a, b) => (b.IsMe ? 1 : 0).CompareTo(a.IsMe ? 1 : 0));
            var cardRoot = view.Node(f, 19);
            if (cardRoot != null) for (int i = cardRoot.childCount - 1; i >= 0; i--) if (cardRoot.GetChild(i).name == "ExtraRow") Destroy(cardRoot.GetChild(i).gameObject);
            for (int r = 0; r < Mathf.Max(templateRows, seats.Count); r++)
            {
                var row = r < templateRows ? view.Node(f, firstRow + r * rowSize) : null;
                if (row == null && r >= templateRows)
                {   // more players than the mockup shows: copy the last row
                    var src = view.Node(f, firstRow + (templateRows - 1) * rowSize);
                    row = Instantiate(src, src.parent);
                    var rt = (RectTransform)row; var srt = (RectTransform)src;
                    rt.anchoredPosition = srt.anchoredPosition - new Vector2(0, (r - templateRows + 1) * RowPitch(f));
                    row.name = "ExtraRow";
                }
                if (row == null) continue;
                if (r >= seats.Count) { row.gameObject.SetActive(false); continue; }
                row.gameObject.SetActive(true);
                FillRow(row, seats[r]);
            }
            // keep the card where the hidden rewards were, and size it for the real number of rows
            var cardTr = view.Node(f, 19);
            if (cardTr != null)
            {
                var card = (RectTransform)cardTr;
                float k = 1080f / 390f;
                float pitch = RowPitch(f);
                float need = 37f * k + seats.Count * pitch + 6f * k;
                card.sizeDelta = new Vector2(card.sizeDelta.x, need);
                float cardTop = (paid ? 233f : 172f) * k;      // 233: below the rewards block; 172: where the card moves up when it is hidden
                card.anchoredPosition = new Vector2(card.anchoredPosition.x, -(cardTop + need / 2f));
                var scroll = view.Node(f, 1) as RectTransform;
                if (scroll != null)
                {   // the page is taller than the screen when there are many players: its top stays put and a drag scrolls it
                    float bottom = cardTop + need + 120f * k;
                    float h = Mathf.Max(790f * k, bottom);
                    scroll.sizeDelta = new Vector2(scroll.sizeDelta.x, h);
                    scroll.anchoredPosition = new Vector2(scroll.anchoredPosition.x, -h / 2f);
                    EnsureScroll(f, scroll);
                }
            }
            view.SetTap(f, 92, () => PlayAgainRequested?.Invoke());
            view.SetTap(f, 97, () => LeaveRequested?.Invoke());
        }

        private static Sprite topFade;

        /// <summary>A dark band, solid at the top and clear at the bottom (generated, one pixel wide).</summary>
        private static Sprite MakeTopFade()
        {
            if (topFade != null) return topFade;
            const int h = 64;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
            {
                float t = y / (h - 1f);                    // 0 = bottom edge of the band, 1 = top
                tex.SetPixel(0, y, new Color(0.055f, 0.04f, 0.03f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.6f))));
            }
            tex.Apply();
            topFade = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
            return topFade;
        }

        private readonly HashSet<string> scrollReady = new HashSet<string>();

        private void EnsureScroll(string f, RectTransform content)
        {
            var root = view.Frame(f);
            if (root == null || !scrollReady.Add(f)) return;
            var hit = root.GetComponent<UnityEngine.UI.Image>();
            if (hit == null) { hit = root.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color = new Color(0, 0, 0, 0); }
            hit.raycastTarget = true;
            var sr = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            sr.content = content; sr.viewport = root; sr.horizontal = false; sr.vertical = true;
            sr.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; sr.inertia = true; sr.scrollSensitivity = 40f;
            // rows slide under the title bar: a fade behind it keeps the title readable (the bottom bar already has one)
            var top = root.Find("f_top");
            var fade = MakeTopFade();
            if (top != null && fade != null)
            {
                var go = new GameObject("TopFade", typeof(RectTransform));
                var rt = (RectTransform)go.transform; rt.SetParent(root, false);
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0, 113f * 1080f / 390f); rt.anchoredPosition = Vector2.zero;
                var im = go.AddComponent<UnityEngine.UI.Image>(); im.sprite = fade; im.raycastTarget = false; im.type = UnityEngine.UI.Image.Type.Simple;
                rt.SetSiblingIndex(top.GetSiblingIndex());
            }
        }

        private float RowPitch(string f)
        {
            var a = view.Node(f, 23) as RectTransform; var b = view.Node(f, 29) as RectTransform;
            return a != null && b != null ? Mathf.Abs(a.anchoredPosition.y - b.anchoredPosition.y) : 50f * 1080f / 390f;
        }

        private void FillRow(Transform row, SeatView s)
        {
            var texts = row.GetComponentsInChildren<ArabicText>(true);
            if (texts.Length >= 2)
            {
                texts[0].Text = s.IsMe ? "أنا (" + s.Name + ")" : s.Name;
                var role = s.Role ?? Role.Civilian;
                texts[1].Text = RoleName(role);
                texts[1].GetComponent<TMPro.TMP_Text>().color = RoleColor(role);
                var chip = texts[1].transform.parent as RectTransform;
                if (chip != null)
                {   // one width for every role plate so the words always fit
                    float k = 1080f / 390f;
                    chip.sizeDelta = new Vector2(53f * k, chip.sizeDelta.y);
                    chip.anchoredPosition = new Vector2((10f + 26.5f) * k, chip.anchoredPosition.y);
                    var tr = (RectTransform)texts[1].transform;
                    tr.sizeDelta = new Vector2(53f * k, tr.sizeDelta.y);
                    tr.anchoredPosition = new Vector2(26.5f * k, tr.anchoredPosition.y);
                }
                var chipV = texts[1].transform.parent != null ? texts[1].transform.parent.Find("V") : null;
                var im = chipV != null ? chipV.GetComponent<UnityEngine.UI.Image>() : null;
                if (im != null) im.color = RoleColor(role);
            }
            bool dead = !s.Alive || s.Left;
            var cg = row.GetComponent<CanvasGroup>();
            if (dead) { if (cg == null) cg = row.gameObject.AddComponent<CanvasGroup>(); cg.alpha = Mathf.Pow(0.5f, 2.2f); }
            else if (cg != null) cg.alpha = 1f;
        }
    }
}
