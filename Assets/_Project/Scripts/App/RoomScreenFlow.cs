using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.Data.Store;
using MafiaNight.MatchRules.Stage;
using MafiaNight.Services.Chat;
using MafiaNight.Services.Match;
using MafiaNight.Services.Net;
using MafiaNight.Services.Rooms;
using MafiaNight.Services.Store;
using MafiaNight.Services.Voice;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Everything inside a room, on the screen of Docs/mockups/office-room.html (<see cref="KitRoomScreen"/>): the mic seats and the raised hands
    /// (<see cref="StageSync"/> of the match session; the room's host is the authority), the room chat, the member list, the host's settings,
    /// the start button of a match room and the voice (only a member who sits on a seat has his mic open). Created by <see cref="RoomsFlow"/>.
    /// </summary>
    public sealed class RoomScreenFlow : MonoBehaviour
    {
        private const string Tag = "RoomScreen";
        private const int MatchMinPlayers = 4;

        private IRoomService rooms;
        private IVoiceService audio;
        private KitToast toast;
        private Transform canvas;
        private KitRoomScreen screen;

        private MatchSession bound;
        private MatchRunner boundRunner;
        private readonly HashSet<string> talking = new HashSet<string>();
        private HashSet<string> known = new HashSet<string>();
        private bool wasSeated, active;
        private string openedRoom = "";
        private readonly HashSet<int> shownLines = new HashSet<int>();      // server chat lines already on the screen (by id)
        private string officialId = "";        // the room is an official one: its chat is the service's, its admins come from the admin board
        private ChatService serverChat;

        private bool IsOfficial => !string.IsNullOrEmpty(officialId);
        private string ChatChannel => "room:" + officialId;
        /// <summary>Everybody in the room: the real members (not the room bot that keeps an official room open) and the made-up ones the host listed.</summary>
        private IEnumerable<RoomMember> People
        {
            get
            {
                foreach (var m in rooms.Members) if (!m.IsKeeper) yield return m;
                if (Stage != null) foreach (var v in Stage.Virtuals) yield return new RoomMember { Puid = v.Id, PlayFabId = v.Id, Name = v.Name, Rank = v.Rank, Level = v.Level };
            }
        }
        private RoomMember Find(string puid) => puid == null ? null : People.FirstOrDefault(m => m.Puid == puid);

        /// <summary>The player left the room with the back button.</summary>
        public event Action Left;

        public bool IsOpen => active && screen != null && screen.IsOpen;

        public void Init(IRoomService rooms, IVoiceService audio, KitToast toast, Transform canvas)
        {
            this.rooms = rooms; this.audio = audio; this.toast = toast; this.canvas = canvas;
            screen = KitRoomScreen.Create(canvas);
            screen.OnBack += Back;
            screen.OnMembers += ShowMembers;
            screen.OnTitle += ShowRoomMenu;
            screen.OnSeat += OnSeat;
            screen.OnMic += OnMic;
            screen.OnLeaveSeat += () => Stage?.Stand();
            screen.OnSend += Send;
            screen.OnMessage += OnMessage;
            screen.OnStart += StartMatch;
            screen.OnVoiceInfo += SwitchVoiceEngine;
            screen.OnSpeaker += on => { if (audio == null) return; audio.SetOutputVolume(on ? MafiaNight.Data.Settings.VoicePrefs.Speaker / 100f : 0f); };
            screen.OnGames += () => Say("الإيموجي والألعاب السريعة قريباً.");
            if (audio != null) { audio.OnSpeaking += OnSpeaking; audio.OnChanged += Redraw; audio.OnConnectionChanged += OnVoiceConnection; }
            rooms.OnRoomChanged += OnRoomChanged;
            rooms.OnRoomLeft += OnRoomLeft;
        }

        private void OnDestroy()
        {
            if (rooms != null) { rooms.OnRoomChanged -= OnRoomChanged; rooms.OnRoomLeft -= OnRoomLeft; }
            if (audio != null) { audio.OnSpeaking -= OnSpeaking; audio.OnChanged -= Redraw; audio.OnConnectionChanged -= OnVoiceConnection; }
            Unbind();
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        // ---- what players wear from the store (their ids come with their member attributes)
        private static IStoreService Store => ServiceLocator.TryGet(out IStoreService s) ? s : null;
        private static StoreItemDef Item(string id) => string.IsNullOrEmpty(id) ? null : Store?.Catalog?.GetItem(id);

        private static Sprite FrameOf(string id)
        {
            var d = Item(id);
            if (d == null) return null;
            if (d.sprite != null) return d.sprite;
            var th = KitTheme.Current;
            return th != null && th.storeFrames != null && d.variant >= 1 && d.variant <= th.storeFrames.Length ? th.storeFrames[d.variant - 1] : null;
        }

        private static string TitleOf(string id) { var d = Item(id); return d != null ? d.titleText : ""; }
        private static int BubbleOf(string id) { var d = Item(id); return d != null && d.artKind == StoreArtKind.ChatBubble ? d.variant : 0; }
        private static Color TintOf(string id) { var d = Item(id); return d != null ? d.tint : Color.clear; }

        private StageSync Stage => bound != null ? bound.Stage : null;
        private RoomMember Me => rooms.Members.FirstOrDefault(m => m.IsMe);
        private void Say(string text) { if (toast != null) toast.Show(text); }

        // ------------------------------------------------------------------------------------------ opening

        public void Open()
        {
            var room = rooms.CurrentRoom;
            if (room == null || screen == null) return;
            // the screen can be asked to open more than once (joined, entered, invited...): the second time only redraws, or every line and every subscription would be doubled
            if (IsOpen && openedRoom == room.Id) { Redraw(); return; }
            openedRoom = room.Id; shownLines.Clear();
            active = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;       // talking in a room without touching the phone must not let the screen lock (the phone then cuts the network and the room drops)
            officialId = room.OfficialId ?? "";
            Bind();
            screen.ClearChat();
            var runner = MatchRunner.Instance;
            if (IsOfficial) OpenServerChat();
            else if (runner != null) foreach (var line in runner.RoomChatHistory) screen.AddChat(ToChat(line));
            known = new HashSet<string>(People.Select(m => m.Puid));
            Redraw();
            JoinVoice();
        }

        private void Bind()
        {
            var runner = MatchRunner.Instance;
            var session = runner != null ? runner.Session : null;
            if (session == bound && runner == boundRunner) return;
            Unbind();
            bound = session; boundRunner = runner;
            if (bound != null) bound.Stage.Changed += Redraw;
            if (boundRunner != null) boundRunner.RoomChatReceived += OnChatLine;
        }

        private void Unbind()
        {
            if (bound != null) bound.Stage.Changed -= Redraw;
            if (boundRunner != null) boundRunner.RoomChatReceived -= OnChatLine;
            bound = null; boundRunner = null;
        }

        // ---- the voice connection dropped while the player is still in the room (the phone lost its network for a moment): join it again by itself
        private const float RejoinDelay = 4f;
        private const int RejoinTries = 10;
        private float rejoinAt = -1f;
        private int rejoinTried;

        private void OnVoiceConnection(bool on)
        {
            if (on) { rejoinAt = -1f; rejoinTried = 0; return; }
            if (!active || rooms.CurrentRoom == null) return;
            wasSeated = false;                                  // the mic opens again by itself once the voice is back and the player is still on his seat
            rejoinAt = Time.unscaledTime + RejoinDelay;
        }

        private void RejoinVoice()
        {
            rejoinAt = -1f;
            if (!active || audio == null || rooms.CurrentRoom == null || audio.IsConnected) return;
            if (++rejoinTried > RejoinTries) return;
            JoinVoice();
            rejoinAt = Time.unscaledTime + RejoinDelay * 2f;    // if this try fails too, the next one is checked then
        }

        private void Update()
        {
            if (!active) return;
            if (recovering) { Recover(); return; }
            if (rejoinAt >= 0f && Time.unscaledTime >= rejoinAt) RejoinVoice();
            var runner = MatchRunner.Instance;
            // the match session of this room is made a moment after the room opens: draw as soon as it exists
            if (runner != null && (runner.Session != bound || runner != boundRunner || !screen.IsOpen)) { Bind(); Redraw(); }
        }

        /// <summary>
        /// Development builds: tapping the voice status line steps through the voice paths and joins again, to compare them on the same phones.
        /// An ordinary room: EOS, LiveKit with Unity audio, LiveKit with the phone's voice-call audio. An official room (LiveKit only): the last two.
        /// </summary>
        private void SwitchVoiceEngine()
        {
            if (!Debug.isDebugBuild || audio == null) return;
            bool eos = PlayerPrefs.GetString("mafia.voice", "eos") != "livekit";
            bool platform = PlayerPrefs.GetString(VoiceBackends.LiveKitModeKey, "unity") == "platform";
            string said;
            if (IsOfficial || !eos && !platform) { PlayerPrefs.SetString(VoiceBackends.LiveKitModeKey, platform ? "unity" : "platform"); said = platform ? "صوت الغرفة: LiveKit (صوت Unity)." : "صوت الغرفة: LiveKit (صوت الهاتف)."; }
            else if (eos) { PlayerPrefs.SetString("mafia.voice", "livekit"); PlayerPrefs.SetString(VoiceBackends.LiveKitModeKey, "unity"); said = "صوت الغرفة: LiveKit (صوت Unity)."; }
            else { PlayerPrefs.SetString("mafia.voice", "eos"); said = "صوت الغرفة: EOS."; }
            wasSeated = false;
            audio.Leave();
            rejoinAt = -1f; rejoinTried = 0;                     // this drop was on purpose
            JoinVoice();
            Say(said);
        }

        private void JoinVoice()
        {
            if (audio == null || audio.IsConnected) return;
            audio.Join(false, err =>     // everybody enters as a listener: the mic opens when he sits on a seat
            {
                if (this == null) return;
                if (err == VoiceError.NoPermission) Say("فعّل إذن المايك من إعدادات الجهاز لتحكي.");
                else if (err == VoiceError.Failed || err == VoiceError.NotReady) Say("تعذّر الاتصال بالصوت.");
                Redraw();
            });
        }

        // ------------------------------------------------------------------------------------------ drawing

        private void OnSpeaking(string puid, bool on) { if (on) talking.Add(puid); else talking.Remove(puid); if (IsOpen) Redraw(); }

        private void OnRoomChanged()
        {
            if (!IsOpen || rooms.CurrentRoom == null) return;
            var now = new HashSet<string>(People.Select(m => m.Puid));
            foreach (var m in People)
                if (!known.Contains(m.Puid) && !m.IsMe && !IsOfficial) screen.AddChat(new KitRoomScreen.ChatVm { System = true, Text = m.Name + " دخل الغرفة" });
            known = now;
            Redraw();
        }

        private void OnRoomLeft(string reason)
        {
            // the connection to the room was lost without the player's choice: keep the screen up and walk back in (see Recover)
            bool lost = reason == RoomLoss.Disconnected && IsOpen && lastRoom != null;
            if (IsOpen && !string.IsNullOrEmpty(reason) && !lost) { screen.Hide(); active = false; }
            CloseServerChat();
            talking.Clear(); wasSeated = false;
            if (lost)
            {
                recovering = true; recoverTries = 0; recoverAt = Time.unscaledTime + 3f; joiningAgain = false;
                openedRoom = "";                                   // the next Open() must run in full (voice, chat, bindings)
                rejoinAt = -1f;
                if (audio != null) audio.Leave();
                Say("انقطع الاتصال، عم نحاول نرجّعك للغرفة…");
                Log.Warning("Room " + lastRoom.Id + " lost: trying to walk back in", Tag);
                return;
            }
            if (!active || !string.IsNullOrEmpty(reason)) Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        // ---- a room lost to a connection problem: join the same room again every few seconds (while the phone has a network), then give up politely
        private const float RecoverEvery = 5f;
        private const int RecoverTries = 12;
        private RoomListing lastRoom;
        private bool recovering, joiningAgain;
        private float recoverAt;
        private int recoverTries;

        private void Recover()
        {
            if (rooms.InRoom) { recovering = false; return; }                 // something else already put the player in a room
            if (joiningAgain || Time.unscaledTime < recoverAt) return;
            if (!NetworkWatch.Online) { recoverAt = Time.unscaledTime + 2f; return; }      // no network: waiting does not use up a try
            if (++recoverTries > RecoverTries) { GiveUpRecovery(); return; }
            joiningAgain = true;
            Log.Info("Walking back into the room, try " + recoverTries, Tag);
            rooms.Join(lastRoom, r =>
            {
                joiningAgain = false;
                if (this == null || !recovering) return;
                if (r.Ok) { recovering = false; Open(); Say("رجعت للغرفة."); return; }
                if (r.Error == RoomError.NotFound || r.Error == RoomError.Full || r.Error == RoomError.Locked) { GiveUpRecovery(); return; }
                recoverAt = Time.unscaledTime + RecoverEvery;
            });
        }

        private void GiveUpRecovery()
        {
            recovering = false;
            if (screen != null && screen.IsOpen) screen.Hide();
            active = false;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            Say("ما قدرنا نرجّعك للغرفة.");
            Left?.Invoke();
        }

        private void Redraw()
        {
            var room = rooms.CurrentRoom;
            if (!active || room == null || Stage == null) return;
            lastRoom = room;
            var stage = Stage.Stage;
            var me = Me;
            string meId = me != null ? me.Puid : "";
            bool staff = stage.IsStaff(meId);
            bool request = stage.Mode == MicMode.Request;
            bool seated = stage.IsSeated(meId);

            var v = new KitRoomScreen.ViewVm
            {
                Name = IsOfficial ? OfficialName(room) : room.Name, RoomId = ShortId(room.Id), Members = People.Count(), HeadLabel = IsOfficial ? "الأدمن" : "المضيف",
                Badge = IsOfficial ? "رسمية" : room.Mode == RoomMode.Mafia ? "مباراة مافيا" : room.Kind == RoomKind.Private ? "خاصة" : room.Kind == RoomKind.Vip ? "VIP" : "",
                CanType = room.ChatEnabled, CanLeaveSeat = seated, RoomTint = TintOf(room.BackgroundId), ChatTint = TintOf(Store?.GetEquipped("chat_bg")), MicOff = seated && audio != null && !audio.MicOn,
                VoiceInfo = Debug.isDebugBuild && audio != null ? VoiceLine(audio.LastStatistics) : "",
            };
            v.Head = SeatOf(stage.HeadHolder, true);
            for (int i = 0; i < stage.SeatCount; i++)
            {
                string who = stage.SeatHolder(i);
                v.Seats.Add(who != null ? SeatOf(who, false) : new KitRoomScreen.SeatVm { Kind = stage.IsLocked(i) ? KitRoomScreen.SeatKind.Locked : KitRoomScreen.SeatKind.Empty });
            }

            // the strip under the seats and the hand / mic button
            int waiting = stage.Queue.Count;
            if (request && (waiting > 0 || staff))
            {
                v.Strip = waiting == 0 ? "ما حدا ينتظر المايك" : waiting + " ينتظروا المايك";
                int pos = stage.QueuePosition(meId);
                v.StripSmall = staff ? "اضغط مقعد فاضي لتطلّع واحد" : pos > 0 ? "دورك رقم " + pos : "";
            }
            else if (!request && !seated) { v.Strip = "اضغط مقعد فاضي لتقعد وتحكي"; }
            v.Mic = seated ? KitRoomScreen.MicButton.Mic : request && !staff ? (stage.HandRaised(meId) ? KitRoomScreen.MicButton.HandUp : KitRoomScreen.MicButton.Hand) : KitRoomScreen.MicButton.Sit;

            if (room.Mode == RoomMode.Mafia)
            {
                int n = People.Count();
                if (!rooms.IAmHost) { v.StartLabel = "بانتظار المضيف"; v.StartEnabled = false; }
                else { v.StartEnabled = n >= MatchMinPlayers; v.StartLabel = v.StartEnabled ? "ابدأ المباراة" : "تحتاج " + MatchMinPlayers + " لاعبين"; }
            }

            screen.Show(v);
            ApplyMic(seated);
        }

        /// <summary>EOS hands over its connection statistics as a long JSON text: the testers' line shows only that it is the EOS path (and which room when it is the LiveKit one).</summary>
        private static string VoiceLine(string stats)
        {
            stats = stats ?? "";
            if (stats.StartsWith("[") || stats.StartsWith("{")) return "EOS voice";
            return KitText.Clip(stats, 90);
        }

        private KitRoomScreen.SeatVm SeatOf(string puid, bool head)
        {
            var m = Find(puid);
            if (m == null || m.IsKeeper) return new KitRoomScreen.SeatVm { Kind = KitRoomScreen.SeatKind.Empty };
            bool silent = audio != null && (m.IsMe ? !audio.MicOn : audio.IsRemoteMicOff(m.Puid) || audio.IsPlayerMuted(m.Puid));
            return new KitRoomScreen.SeatVm
            {
                Kind = KitRoomScreen.SeatKind.Taken, Id = m.Puid, Name = m.Name ?? "", Me = m.IsMe, Staff = Stage.Stage.IsStaff(m.Puid),
                Talking = talking.Contains(m.Puid) && !silent, Muted = silent, Frame = FrameOf(m.FrameId), Title = TitleOf(m.TitleId),
            };
        }

        /// <summary>Only a member on a seat has his mic open; leaving the seat closes it.</summary>
        private void ApplyMic(bool seated)
        {
            if (audio == null) return;
            if (seated != wasSeated) { wasSeated = seated; audio.SetMic(seated); }
        }

        /// <summary>A short number for the room (the lobby id is a long code).</summary>
        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            unchecked { int h = 17; foreach (char c in id) h = h * 31 + c; return (10000 + (uint)h % 90000u).ToString(); }
        }

        // ------------------------------------------------------------------------------------------ taps

        private void OnMic()
        {
            var stage = Stage; var me = Me; if (stage == null || me == null) return;
            if (stage.Stage.IsSeated(me.Puid)) { ToggleMyMic(); return; }
            if (stage.Stage.Mode == MicMode.Request && !stage.Stage.IsStaff(me.Puid)) stage.Hand(!stage.Stage.HandRaised(me.Puid));
            else
            {
                stage.Sit();
                // the host answers in a moment; if there is no free seat nothing changes
            }
        }

        private void ToggleMyMic()
        {
            if (audio == null) return;
            audio.SetMic(!audio.MicOn);
            Redraw();
        }

        private void OnSeat(int index)
        {
            var stage = Stage; var me = Me; if (stage == null || me == null) return;
            var st = stage.Stage;
            bool staff = st.IsStaff(me.Puid);
            string holder = index < 0 ? st.HeadHolder : st.SeatHolder(index);

            if (holder != null)
            {
                if (holder == me.Puid) { ToggleMyMic(); return; }
                MemberMenu(Find(holder));
                return;
            }
            if (index < 0) { if (staff) stage.Sit(RoomStage.Head); return; }
            if (st.IsLocked(index)) { if (staff) KitActionSheet.Show(canvas, "مقعد مقفل", new KitActionSheet.Choice("افتح المقعد", () => stage.Lock(index, false)), new KitActionSheet.Choice("إلغاء", null, false, true)); return; }
            if (staff)
            {
                var queue = st.Queue.Select(id => Find(id)).Where(m => m != null).Take(6).ToList();
                var choices = new List<KitActionSheet.Choice>();
                foreach (var m in queue) { var who = m; choices.Add(new KitActionSheet.Choice("طلّع " + who.Name, () => stage.Bring(who.Puid, index))); }
                if (st.Mode == MicMode.Free || queue.Count == 0) choices.Add(new KitActionSheet.Choice("اقعد إنت هون", () => stage.Sit(index)));
                choices.Add(new KitActionSheet.Choice("اقفل المقعد", () => stage.Lock(index, true), false, true));
                choices.Add(new KitActionSheet.Choice("إلغاء", null, false, true));
                KitActionSheet.Show(canvas, queue.Count > 0 ? "مين بدك تطلّع؟" : "مقعد فاضي", choices.ToArray());
                return;
            }
            if (st.Mode == MicMode.Free) stage.Sit(index);
            else stage.Hand(!st.HandRaised(me.Puid));
        }

        /// <summary>What a member offers on another member: staff take him down, mute or kick; everybody can stop hearing him.</summary>
        private void MemberMenu(RoomMember m)
        {
            var stage = Stage; var me = Me; if (m == null || stage == null || me == null) return;
            if (m.IsMe) return;
            var st = stage.Stage;
            bool staff = st.IsStaff(me.Puid), target = st.IsStaff(m.Puid);
            var c = new List<KitActionSheet.Choice>();
            if (staff && st.IsSeated(m.Puid) && !target) c.Add(new KitActionSheet.Choice("نزّله من المايك", () => stage.Down(m.Puid)));
            if (staff && !st.IsSeated(m.Puid) && !target) c.Add(new KitActionSheet.Choice("أعطيه المايك", () => stage.Bring(m.Puid)));
            if (audio != null)
            {
                bool muted = audio.IsPlayerMuted(m.Puid);
                c.Add(new KitActionSheet.Choice(muted ? "رجّع صوته عندك" : "اكتمه عندك", () => { audio.SetPlayerMuted(m.Puid, !muted); Redraw(); }, false, true));
            }
            if (rooms.IAmHost && !target) c.Add(new KitActionSheet.Choice("اطرده من الغرفة", () => Kick(m), true));
            c.Add(new KitActionSheet.Choice("إلغاء", null, false, true));
            KitActionSheet.Show(canvas, m.Name, c.ToArray());
        }

        private void Kick(RoomMember m)
        {
            bound?.MemberKicked(m.Puid);
            rooms.Kick(m.Puid, ok => { if (this == null) return; Say(ok ? "طردت " + m.Name + " من الغرفة." : "تعذّر طرد " + m.Name + "."); Redraw(); });
        }

        private void ShowMembers()
        {
            var stage = Stage; if (stage == null) return;
            var st = stage.Stage;
            var rows = new List<KitMembersSheet.Row>();
            foreach (var m in People.OrderByDescending(x => st.IsStaff(x.Puid)).ThenByDescending(x => st.IsSeated(x.Puid)))
            {
                bool staffM = st.IsStaff(m.Puid);
                rows.Add(new KitMembersSheet.Row
                {
                    Id = m.Puid, Name = m.Name ?? "", Me = m.IsMe, Staff = staffM, Tag = staffM ? (m.IsHost && !IsOfficial ? "المضيف" : "أدمن") : "",
                    State = st.IsSeated(m.Puid) ? "على المايك" : st.HandRaised(m.Puid) ? "ينتظر المايك" : "",
                });
            }
            KitMembersSheet.Show(canvas, "الحضور", People.Count() + " من " + rooms.CurrentRoom.Capacity, rows, r =>
            {
                KitMembersSheet.CloseCurrent();
                MemberMenu(Find(r.Id));
            });
        }

        private void OnMessage(KitRoomScreen.ChatVm m)
        {
            if (string.IsNullOrEmpty(m.Id) || Me == null || m.Id == Me.Puid) return;
            MemberMenu(Find(m.Id));
        }

        // ------------------------------------------------------------------------------------------ room menu (title)

        private void ShowRoomMenu()
        {
            var room = rooms.CurrentRoom; var stage = Stage; if (room == null || stage == null) return;
            var st = stage.Stage;
            var c = new List<KitActionSheet.Choice>();
            c.Add(new KitActionSheet.Choice("انسخ دعوة للغرفة", Invite));
            if (st.IsStaff(Me != null ? Me.Puid : ""))
            {
                bool request = st.Mode == MicMode.Request;
                c.Add(new KitActionSheet.Choice(request ? "المايك: بالطلب ← خلّيه حر" : "المايك: حر ← خلّيه بالطلب", () => stage.Configure(st.SeatCount, request ? MicMode.Free : MicMode.Request)));
                int next = st.SeatCount >= 8 ? 4 : st.SeatCount + 2;
                c.Add(new KitActionSheet.Choice("مقاعد المايك: " + st.SeatCount + " ← " + next, () => stage.Configure(next, st.Mode)));
            }
            if (rooms.IAmHost)
            {
                c.Add(new KitActionSheet.Choice(room.Locked ? "افتح الباب للاعبين الجدد" : "اقفل الباب (ما حدا جديد يدخل)", () => rooms.SetLocked(!room.Locked, ok => Say(ok ? (room.Locked ? "قفلت الغرفة. ما حدا بيقدر يدخل." : "فتحت الغرفة من جديد.") : "تعذّر تغيير حالة الغرفة، حاول مرة ثانية."))));
                c.Add(new KitActionSheet.Choice(room.ChatEnabled ? "سكّر الدردشة" : "افتح الدردشة", () => rooms.SetChatEnabled(!room.ChatEnabled, ok => { Say(ok ? (room.ChatEnabled ? "فتحت الدردشة." : "سكّرت الدردشة.") : "تعذّر تغيير حالة الدردشة، حاول مرة ثانية."); Redraw(); })));
            }
            c.Add(new KitActionSheet.Choice("إلغاء", null, false, true));
            KitActionSheet.Show(canvas, IsOfficial ? OfficialName(room) : room.Name, c.ToArray());
        }

        private void Invite()
        {
            var room = rooms.CurrentRoom;
            if (room == null) return;
            GUIUtility.systemCopyBuffer = RoomRules.InviteText(room.Kind == RoomKind.Private ? room.Code : "", room.Name);
            Say("اننسخت الدعوة، الصقها لأصحابك.");
        }

        // ------------------------------------------------------------------------------------------ chat

        private void Send(string text)
        {
            if (IsOfficial) { SendServerChat(text); return; }
            var runner = MatchRunner.Instance; if (runner == null) return;
            string refused = runner.SendRoomChat(text);
            if (!string.IsNullOrEmpty(refused)) Say(refused);
        }

        private void OnChatLine(RoomChatLine line) { if (IsOpen) screen.AddChat(ToChat(line)); }

        private KitRoomScreen.ChatVm ToChat(RoomChatLine l)
        {
            bool staff = Stage != null && Stage.Stage.IsStaff(l.Puid);
            bool host = rooms.Members.Any(m => m.Puid == l.Puid && m.IsHost);
            return new KitRoomScreen.ChatVm
            {
                Id = l.Puid ?? "", Name = l.Mine ? "أنت" : l.Name ?? "", Text = l.Text ?? "", Mine = l.Mine, Staff = staff && !l.Mine, Bubble = BubbleOf(Find(l.Puid)?.BubbleId),
                Tag = staff && !l.Mine ? (host ? "المضيف" : "أدمن") : "",
                Time = DateTimeOffset.FromUnixTimeMilliseconds(l.Ts).ToLocalTime().ToString("h:mm"),
            };
        }

        // ------------------------------------------------------------------------------------------ official room: chat of the service

        private static string OfficialName(RoomListing room)
        {
            var def = OfficialRooms.Find(room.OfficialId);
            return def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : room.Name;
        }

        private bool IsRoomAdmin(string pf)
        {
            var def = OfficialRooms.Find(officialId);
            return def != null && !string.IsNullOrEmpty(pf) && def.Admins.Contains(pf);
        }

        private void OpenServerChat()
        {
            serverChat = ChatService.Ensure();
            serverChat.OnMessage -= OnServerLine;
            serverChat.OnMessage += OnServerLine;
            string id = officialId;
            OfficialRooms.Enter(id, (ok, history) =>
            {
                if (this == null || !IsOfficial || officialId != id) return;
                var def = OfficialRooms.Find(id);
                if (def != null && !string.IsNullOrEmpty(def.DisplayWelcome)) screen.AddChat(new KitRoomScreen.ChatVm { System = true, Text = def.DisplayWelcome });
                if (!ok) { Say("تعذّر تحميل دردشة الغرفة."); return; }
                foreach (var l in history) if (l.Id <= 0 || shownLines.Add(l.Id)) screen.AddChat(ToChat(l));
            });
        }

        private void CloseServerChat()
        {
            if (serverChat != null) serverChat.OnMessage -= OnServerLine;
            serverChat = null;
            if (IsOfficial) OfficialRooms.Leave();
            officialId = "";
            openedRoom = "";
        }

        private void OnServerLine(ChatMsg m)
        {
            if (!IsOpen || !IsOfficial || m.Channel != ChatChannel) return;
            if (m.Id > 0 && !shownLines.Add(m.Id)) return;
            screen.AddChat(ToChat(new OfficialChatLine { Id = m.Id, FromPf = m.FromPf, Name = m.Name, Text = m.Text, Ts = m.Ts, System = m.System }));
        }

        private void SendServerChat(string text)
        {
            if (serverChat == null) return;
            serverChat.Send(ChatChannel, text, (r, retryMs) =>
            {
                if (this == null) return;
                switch (r)
                {
                    case ChatSendResult.Ok: case ChatSendResult.Empty: break;
                    case ChatSendResult.Slow: Say("على مهلك، انتظر ثانية وابعت."); break;
                    case ChatSendResult.Muted: Say("أنت ممنوع من الدردشة."); break;
                    case ChatSendResult.NotMember: Say("لازم تكون داخل الغرفة لتكتب."); break;
                    default: Say("تعذّر إرسال الرسالة."); break;
                }
            });
        }

        private KitRoomScreen.ChatVm ToChat(OfficialChatLine l)
        {
            bool mine = !string.IsNullOrEmpty(l.FromPf) && l.FromPf == serverChat?.MyPf;
            bool admin = IsRoomAdmin(l.FromPf);
            var member = People.FirstOrDefault(x => !string.IsNullOrEmpty(x.PlayFabId) && x.PlayFabId == l.FromPf);
            return new KitRoomScreen.ChatVm
            {
                Id = member != null ? member.Puid : "", Name = mine ? "أنت" : l.Name ?? "", Text = l.Text ?? "", Mine = mine, System = l.System, Staff = admin && !mine, Bubble = BubbleOf(member?.BubbleId),
                Tag = admin && !mine ? "أدمن" : "",
                Time = DateTimeOffset.FromUnixTimeMilliseconds(l.Ts).ToLocalTime().ToString("h:mm"),
            };
        }

        // ------------------------------------------------------------------------------------------ leaving and the match

        private void Back()
        {
            screen.Hide(); active = false; wasSeated = false; recovering = false;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            CloseServerChat();
            if (audio != null) audio.Leave();
            rooms.Leave(() => { Left?.Invoke(); });
        }

        private void StartMatch()
        {
            var session = MatchRunner.Instance != null ? MatchRunner.Instance.Session : null;
            if (session == null || !rooms.IAmHost) return;
            if (!session.StartMatch()) Say("ما قدرنا نبدأ: لازم من ٤ إلى ١٦ لاعب.");
        }
    }
}
