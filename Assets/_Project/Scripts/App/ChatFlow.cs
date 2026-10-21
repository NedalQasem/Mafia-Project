using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.Core;
using MafiaNight.Services.Chat;
using MafiaNight.Services.Match;
using MafiaNight.Services.Rooms;
using MafiaNight.Services.Profile;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Connects the chat screen to the chat service: it turns the service's messages and conversations into what the screen shows,
    /// sends what the player types, and offers the actions on a message (message the sender, report, block) and on a group (leave, new group).
    /// Added by <see cref="KitAppBinder"/> when the scene has a chat screen.
    /// </summary>
    public sealed class ChatFlow : MonoBehaviour
    {
        private const string Tag = "ChatFlow";
        private const int MaxPicks = 6;

        private ChatService service;
        private KitChatScreen screen;
        private KitToast toast;
        private Transform canvas;
        private IPlayerProfileService profile;
        private IRoomService rooms;
        private ChatConversation roomConv;      // the chat of the room the player sits in (peer to peer, not stored anywhere)
        private MatchRunner runnerHooked;

        private ChatConversation pub;
        private readonly Dictionary<string, ChatConversation> byChannel = new Dictionary<string, ChatConversation>();
        private readonly List<ChatConversation> dms = new List<ChatConversation>(), groups = new List<ChatConversation>();

        private void Start()
        {
            screen = FindAnyObjectByType<KitChatScreen>(FindObjectsInactive.Include);
            if (screen == null) { Destroy(this); return; }
            toast = FindAnyObjectByType<KitToast>(FindObjectsInactive.Include);
            canvas = screen.GetComponentInParent<Canvas>() != null ? screen.GetComponentInParent<Canvas>().rootCanvas.transform : screen.transform.root;
            ServiceLocator.TryGet(out profile);
            ServiceLocator.TryGet(out rooms);
            service = ChatService.Ensure();

            pub = new ChatConversation { id = ChatService.Public, kind = ChatKind.Public, name = "ساحة الحارة" };
            byChannel[pub.id] = pub;
            screen.OnSend += OnSend;
            screen.OnOpened += c => { if (!string.IsNullOrEmpty(c.id)) service.Open(c.id); };
            screen.OnClosed += () => service.Close();
            screen.OnMessageTapped += OnMessageTapped;
            screen.OnHeaderTapped += OnHeaderTapped;
            screen.OnNewGroup += StartNewGroup;
            service.OnMessage += OnMessage;
            service.OnConversationsChanged += Rebuild;
            if (rooms != null) { rooms.OnRoomChanged += Rebuild; rooms.OnRoomLeft += OnRoomLeft; rooms.OnRoomJoined += OnRoomJoined; }
            Rebuild();
            service.RefreshInbox();
        }

        private void OnDestroy()
        {
            if (service == null) return;
            service.OnMessage -= OnMessage;
            service.OnConversationsChanged -= Rebuild;
            service.Close();
            if (rooms != null) { rooms.OnRoomChanged -= Rebuild; rooms.OnRoomLeft -= OnRoomLeft; rooms.OnRoomJoined -= OnRoomJoined; }
            if (runnerHooked != null) runnerHooked.RoomChatReceived -= OnRoomLine;
        }

        private void OnRoomLeft(string _) { Rebuild(); }
        private void OnRoomJoined(MafiaNight.Data.Rooms.RoomListing _) { Rebuild(); }

        private void Say(string text) { if (toast != null) toast.Show(text); }

        // ---------------------------------------------------------------- from the service to the screen

        private static string TimeText(long ts) => DateTimeOffset.FromUnixTimeMilliseconds(ts).ToLocalTime().ToString("h:mm");
        private static string DayText(long ts)
        {
            var d = DateTimeOffset.FromUnixTimeMilliseconds(ts).ToLocalTime().Date;
            if (d == DateTime.Now.Date) return "اليوم";
            if (d == DateTime.Now.Date.AddDays(-1)) return "أمس";
            return d.ToString("d/M");
        }

        private void Append(ChatConversation c, ChatMsg m)
        {
            var list = c.messages;
            string day = DayText(m.Ts);
            var lastDay = list.LastOrDefault(x => x.kind == MsgKind.Day);
            if (lastDay == null || lastDay.text != day) list.Add(new ChatMessage { kind = MsgKind.Day, text = day });
            var ui = new ChatMessage
            {
                id = m.Id, fromId = m.FromPf, text = m.Text, time = TimeText(m.Ts), rank = m.Rank,
                kind = m.System ? MsgKind.System : MsgKind.Normal, from = m.Mine ? "me" : m.Name,
            };
            list.Add(ui);
            c.last = (m.Mine ? "أنا: " : (c.kind == ChatKind.Group && !m.System ? m.Name + ": " : "")) + m.Text;
            c.time = ui.time;
            if (screen != null && screen.Current == c) screen.Push(c, ui);
        }

        private ChatConversation For(ChatConv sc)
        {
            if (!byChannel.TryGetValue(sc.Channel, out var c))
            {
                c = new ChatConversation { id = sc.Channel };
                byChannel[sc.Channel] = c;
                foreach (var m in service.Messages(sc.Channel)) Append(c, m);
            }
            c.kind = sc.IsGroup ? ChatKind.Group : ChatKind.Direct;
            c.name = sc.Name; c.rank = sc.Rank; c.online = sc.Online; c.peerId = sc.OtherPf;
            c.members = sc.Members.Select(m => m.Value).ToArray();
            c.unread = sc.Unread;
            if (sc.Last != null && c.messages.Count == 0) { c.last = sc.Last.Text; c.time = TimeText(sc.Last.Ts); }
            return c;
        }

        private void Rebuild()
        {
            if (pub.messages.Count == 0) foreach (var m in service.Messages(ChatService.Public)) Append(pub, m);
            dms.Clear(); groups.Clear();
            foreach (var sc in service.Conversations) (sc.IsGroup ? groups : dms).Add(For(sc));
            SyncRoomConversation();
            if (roomConv != null) groups.Insert(0, roomConv);
            screen.SetData(pub, dms, groups);
        }

        // ---------------------------------------------------------------- the room chat

        private void SyncRoomConversation()
        {
            var room = rooms != null && rooms.InRoom ? rooms.CurrentRoom : null;
            var runner = MatchRunner.Instance;
            if (runner != null && runnerHooked != runner) { runnerHooked = runner; runner.RoomChatReceived += OnRoomLine; }
            if (room == null || runner == null || !room.ChatEnabled) { roomConv = null; return; }
            string id = "room:" + room.Id;
            if (roomConv == null || roomConv.id != id)
            {
                roomConv = new ChatConversation { id = id, kind = ChatKind.Group };
                foreach (var line in runner.RoomChatHistory) AppendRoom(roomConv, line, false);
            }
            roomConv.name = "غرفتي · " + room.Name;
            roomConv.members = rooms.Members.Select(m => m.Name).ToArray();
        }

        private void AppendRoom(ChatConversation c, RoomChatLine line, bool live)
        {
            string day = DayText(line.Ts);
            var lastDay = c.messages.LastOrDefault(x => x.kind == MsgKind.Day);
            if (lastDay == null || lastDay.text != day) c.messages.Add(new ChatMessage { kind = MsgKind.Day, text = day });
            var ui = new ChatMessage { fromId = line.PlayFabId, text = line.Text, time = TimeText(line.Ts), rank = line.Rank, kind = MsgKind.Normal, from = line.Mine ? "me" : line.Name };
            c.messages.Add(ui);
            c.last = (line.Mine ? "أنا: " : line.Name + ": ") + line.Text; c.time = ui.time;
            if (!live) return;
            if (screen != null && screen.Current == c) screen.Push(c, ui);
            else if (!line.Mine) c.unread++;
        }

        private void OnRoomLine(RoomChatLine line)
        {
            SyncRoomConversation();
            if (roomConv == null) return;
            AppendRoom(roomConv, line, true);
            screen.SetData(pub, dms, groups.Contains(roomConv) ? groups : Prepend(roomConv, groups));
        }

        private static List<ChatConversation> Prepend(ChatConversation c, List<ChatConversation> list) { var l = new List<ChatConversation>(list); l.Insert(0, c); return l; }

        private void OnMessage(ChatMsg m)
        {
            if (m.Channel == ChatService.Public) { Append(pub, m); return; }
            if (!byChannel.TryGetValue(m.Channel, out var c))
            {   // a conversation we have not listed yet: the inbox call brings it
                service.RefreshInbox();
                return;
            }
            Append(c, m);
            var sc = service.Conversations.FirstOrDefault(x => x.Channel == m.Channel);
            if (sc != null) c.unread = sc.Unread;
            screen.SetData(pub, dms, groups);
        }

        // ---------------------------------------------------------------- from the screen to the service

        private void OnSend(ChatConversation c, string text)
        {
            if (c == roomConv && roomConv != null)
            {
                string err = MatchRunner.Instance != null ? MatchRunner.Instance.SendRoomChat(text) : "لست بغرفة.";
                if (!string.IsNullOrEmpty(err)) Say(err);
                return;
            }
            string channel = c.kind == ChatKind.Public ? ChatService.Public : c.kind == ChatKind.Direct ? "dm:" + c.peerId : c.id;
            service.Send(channel, text, (res, retryMs) =>
            {
                if (this == null) return;
                switch (res)
                {
                    case ChatSendResult.Ok: break;
                    case ChatSendResult.Slow: screen.StartCooldown(Mathf.Max(1, Mathf.CeilToInt(retryMs / 1000f))); Say("مهلاً، رسائلك سريعة."); break;
                    case ChatSendResult.Muted: Say("أنت مكتوم مؤقتاً بسبب بلاغات عليك."); break;
                    case ChatSendResult.YouBlocked: Say("حاظر هالشخص. ارفع الحظر لتراسله."); break;
                    case ChatSendResult.NotMember: Say("لست عضواً بهالمحادثة."); break;
                    case ChatSendResult.Empty: break;
                    default: Say("تعذّر إرسال الرسالة. تحقق من الاتصال."); break;
                }
            });
        }

        private void OnMessageTapped(ChatConversation c, ChatMessage m)
        {
            if (string.IsNullOrEmpty(m.fromId) || m.fromId == service.MyPf) return;
            string who = m.from;
            if (c == roomConv)
            {   // the room chat is not stored on the server, so there is nothing to report: only start a private chat
                KitActionSheet.Show(canvas, who, new KitActionSheet.Choice("مراسلة " + who, () => MessagePlayer(m.fromId, who, m.rank)), new KitActionSheet.Choice("إغلاق", null, quiet: true));
                return;
            }
            var choices = new List<KitActionSheet.Choice>();
            if (c.kind != ChatKind.Direct)
                choices.Add(new KitActionSheet.Choice("مراسلة " + who, () => MessagePlayer(m.fromId, who, m.rank)));
            choices.Add(new KitActionSheet.Choice("إبلاغ عن الرسالة", () => Report(c, m), danger: true));
            choices.Add(new KitActionSheet.Choice(service.IsBlocked(m.fromId) ? "رفع الحظر عن " + who : "حظر " + who, () => ToggleBlock(m.fromId, who), danger: false));
            choices.Add(new KitActionSheet.Choice("إغلاق", null, quiet: true));
            KitActionSheet.Show(canvas, who, choices.ToArray());
        }

        private void OnHeaderTapped(ChatConversation c)
        {
            var choices = new List<KitActionSheet.Choice>();
            if (c.kind == ChatKind.Group)
                choices.Add(new KitActionSheet.Choice("مغادرة المجموعة", () => service.LeaveGroup(c.id, ok => { if (this == null) return; Say(ok ? "غادرت المجموعة." : "تعذّرت المغادرة."); if (ok) { byChannel.Remove(c.id); screen.SelectTab(2); Rebuild(); } }), danger: true));
            else if (c.kind == ChatKind.Direct && !string.IsNullOrEmpty(c.peerId))
                choices.Add(new KitActionSheet.Choice(service.IsBlocked(c.peerId) ? "رفع الحظر عن " + c.name : "حظر " + c.name, () => ToggleBlock(c.peerId, c.name)));
            if (choices.Count == 0) return;
            choices.Add(new KitActionSheet.Choice("إغلاق", null, quiet: true));
            KitActionSheet.Show(canvas, c.name, choices.ToArray());
        }

        private void MessagePlayer(string pf, string name, int rank)
        {
            var sc = service.EnsureDirect(pf, name, rank);
            Rebuild();
            if (byChannel.TryGetValue(sc.Channel, out var c)) screen.Show(c);
        }

        private void Report(ChatConversation c, ChatMessage m)
        {
            var msg = new ChatMsg { Id = m.id, Channel = c.id };
            service.Report(msg, "", ok => { if (this != null) Say(ok ? "وصل البلاغ. شكراً لك." : "تعذّر إرسال البلاغ."); });
        }

        private void ToggleBlock(string pf, string name)
        {
            bool on = !service.IsBlocked(pf);
            service.Block(pf, on, ok =>
            {
                if (this == null) return;
                Say(ok ? (on ? "حظرت " + name + ". ما رح تشوف رسائله." : "رفعت الحظر عن " + name + ".") : "تعذّر تنفيذ الطلب.");
                if (ok) { pub.messages.Clear(); Rebuild(); }
            });
        }

        // ---------------------------------------------------------------- a new group

        private void StartNewGroup() { PickMembers(new List<ChatConv>()); }

        private void PickMembers(List<ChatConv> picked)
        {
            var left = service.Conversations.Where(c => !c.IsGroup && !string.IsNullOrEmpty(c.OtherPf) && picked.All(p => p.Channel != c.Channel)).Take(MaxPicks).ToList();
            var choices = new List<KitActionSheet.Choice>();
            foreach (var c in left) { var cc = c; choices.Add(new KitActionSheet.Choice(cc.Name, () => { picked.Add(cc); PickMembers(picked); })); }
            if (picked.Count > 0) choices.Add(new KitActionSheet.Choice("إنشاء المجموعة (" + picked.Count + ")", () => CreateGroup(picked), danger: true));
            choices.Add(new KitActionSheet.Choice("إلغاء", null, quiet: true));
            string title = left.Count == 0 && picked.Count == 0 ? "ما عندك محادثات خاصة بعد" : "أضف أعضاء (" + picked.Count + ")";
            KitActionSheet.Show(canvas, title, choices.ToArray());
        }

        private void CreateGroup(List<ChatConv> picked)
        {
            string me = profile != null && profile.LocalProfile != null && !string.IsNullOrEmpty(profile.LocalProfile.DisplayName) ? profile.LocalProfile.DisplayName : "لاعب";
            string name = ("مجموعة " + me);
            if (name.Length > 24) name = name.Substring(0, 24);
            service.CreateGroup(name, picked.Select(p => p.OtherPf), ch =>
            {
                if (this == null) return;
                if (ch == null) { Say("تعذّر إنشاء المجموعة."); return; }
                Say("أُنشئت المجموعة.");
                screen.SelectTab(2);
            });
        }
    }
}
