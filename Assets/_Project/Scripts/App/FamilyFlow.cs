using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.Services.Chat;
using MafiaNight.Services.Friends;
using MafiaNight.Services.Rooms;
using MafiaNight.Services.Scenes;
using MafiaNight.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.App
{
    /// <summary>
    /// Puts the family (friends) on the screens of Docs/mockups/family.html: the list grouped by where everybody is, the requests, adding
    /// somebody by his id number, the sheet of one friend, and the report sheet. It fills the frames the editor builder made, node by node
    /// (numbers are the parts of Art/UI/Family/frames.json), and keeps the lists fresh from <see cref="FriendsService"/>.
    /// Added by <see cref="KitAppBinder"/> in the family scene.
    /// </summary>
    public sealed class FamilyFlow : MonoBehaviour
    {
        private const float K = 1080f / 390f;     // canvas units per CSS pixel of the mockup
        private const float RowH = 66f, Gap = 10f, SecH = 25f, ListTop = 144f;

        // seg tabs and top bar per frame: friends, requests, add buttons; badge group and its number; title, subtitle; back button
        private struct Chrome { public int Friends, Requests, Add, Badge, BadgeNum, Sub, Back; public Chrome(int f, int r, int a, int b, int bn, int s, int back) { Friends = f; Requests = r; Add = a; Badge = b; BadgeNum = bn; Sub = s; Back = back; } }
        private static readonly Chrome ChromeList = new Chrome(103, 105, 109, 107, 108, 117, 112);   // f1 and the sheets over it (f4, f5, f6, f6_after)
        private static readonly Chrome ChromeReq = new Chrome(58, 60, 64, 62, 63, 72, 67);           // f2
        private static readonly Chrome ChromeAdd = new Chrome(33, 35, 39, 37, 38, 47, 42);           // f3

        private static readonly string[] ReasonCodes = { "abuse", "harassment", "cheat", "voice", "name", "other" };
        private static readonly int[] ReasonNodes = { 127, 132, 136, 141, 146, 150 };

        private KitMatchView view;
        private FriendsService fs;
        private ChatService chat;
        private IRoomService rooms;
        private ISceneLoader scenes;
        private KitToast toast;

        private string frame = "f1";
        private Friend sheetFriend;
        private int reportReason;
        private bool reportBlock = true;
        private KitArabicInput addInput, noteInput;
        private string lookedUpNum;
        private LookupResult lookedUp;
        private float nextRefresh;

        private void Start()
        {
            view = FindAnyObjectByType<KitMatchView>(FindObjectsInactive.Include);
            if (view == null || !view.Has("f1")) { Destroy(this); return; }
            toast = FindAnyObjectByType<KitToast>(FindObjectsInactive.Include);
            ServiceLocator.TryGet(out rooms);
            ServiceLocator.TryGet(out scenes);
            fs = FriendsService.Ensure();
            chat = ChatService.Ensure();
            fs.OnChanged += Redraw;
            fs.OnNews += OnNews;
            fs.Refresh();
            ShowTab("f1");
        }

        private void OnDestroy() { if (fs != null) { fs.OnChanged -= Redraw; fs.OnNews -= OnNews; } }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 15f;
            fs.Refresh();
        }

        private void Say(string text) { if (toast != null) toast.Show(text); }

        private void OnNews(ServerEvent e)
        {
            if (e.Type == "friend_request") Say(e.Name + " يريد يضيفك لعائلته.");
            else if (e.Type == "friend_accepted") Say(e.Name + " قبل طلبك. صرتوا أصدقاء.");
        }

        private static string Message(string code)
        {
            switch (code)
            {
                case "unknown_number": return "ما لقينا حدا بهالرقم.";
                case "self": return "هذا رقمك أنت.";
                case "already_friends": return "هو صديقك أصلاً.";
                case "you_full": return "عائلتك مليانة.";
                case "target_full": return "عائلته مليانة.";
                case "too_many_requests": return "طلبات كتيرة اليوم، جرّب بكرة.";
                case "bad_number": return "الرقم ٦ خانات.";
                case "no_room": return "ادخل غرفة أول، وبعدين ادعُ أصحابك.";
                case "slow": return "انتظر شوي قبل ما تدعوه من جديد.";
                case "not_friends": return "مش صديقك.";
                case "no_request": return "الطلب ما عاد موجود.";
                default: return "تعذّر الاتصال، حاول مرة ثانية.";
            }
        }

        // ---------------------------------------------------------------- small helpers

        private static string Ar(int n) => Ar(n.ToString());
        private static string Ar(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c >= '0' && c <= '9' ? (char)('٠' + (c - '0')) : c);
            return sb.ToString();
        }

        private static float TextW(ArabicText a)
        {
            var t = a.GetComponent<TMP_Text>();
            t.ForceMeshUpdate();
            return t.preferredWidth / K;
        }

        /// <summary>Sets where a part sits horizontally (left edge and width in mockup pixels), keeping its height and vertical place.</summary>
        private static void X(Transform tr, float left, float width)
        {
            var rt = (RectTransform)tr;
            rt.sizeDelta = new Vector2(width * K, rt.sizeDelta.y);
            rt.anchoredPosition = new Vector2((left + width / 2f) * K, rt.anchoredPosition.y);
        }

        /// <summary>Sets where a part sits vertically (top edge and height in mockup pixels), keeping its horizontal place.</summary>
        private static void Y(Transform tr, float top, float height)
        {
            var rt = (RectTransform)tr;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height * K);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(top + height / 2f) * K);
        }

        private static string Ago(long ms)
        {
            if (ms <= 0) return "من فترة";
            double mins = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ms) / 60000.0;
            if (mins < 2) return "قبل لحظات";
            if (mins < 60) { int m = (int)mins; return "قبل " + (m <= 10 ? Ar(m) + (m == 1 ? " دقيقة" : " دقائق") : Ar(m) + " دقيقة"); }
            double hours = mins / 60;
            if (hours < 24) { int h = (int)hours; return h == 1 ? "قبل ساعة" : h == 2 ? "قبل ساعتين" : "قبل " + Ar(h) + (h <= 10 ? " ساعات" : " ساعة"); }
            int d = (int)(hours / 24);
            return d == 1 ? "أمس" : d == 2 ? "قبل يومين" : "قبل " + Ar(d) + " أيام";
        }

        private static string Clip(string name, int max) => string.IsNullOrEmpty(name) ? "" : name.Length > max ? name.Substring(0, max - 1) + "…" : name;

        // ---------------------------------------------------------------- the frame and its chrome (tabs, top bar)

        private void ShowTab(string f)
        {
            frame = f;
            view.Show(f);
            Redraw();
        }

        private void Redraw()
        {
            switch (frame)
            {
                case "f1": DrawFriends("f1"); break;
                case "f2": DrawRequests(); break;
                case "f3": DrawAdd(); break;
                case "f4": DrawFriends("f4"); FillSheet(); break;
                case "f6": DrawFriends("f6"); FillReport(); break;
                case "f6_after": DrawFriends("f6_after"); break;
                default: DrawFriends(frame); break;
            }
        }

        private void WireChrome(string f, Chrome c)
        {
            view.SetTap(f, c.Friends, () => ShowTab("f1"));
            view.SetTap(f, c.Requests, () => ShowTab("f2"));
            view.SetTap(f, c.Add, () => ShowTab("f3"));
            view.SetTap(f, c.Back, GoHome);
            int n = fs.Incoming.Count;
            view.SetActive(f, c.Badge, n > 0);
            if (n > 0) view.SetText(f, c.BadgeNum, Ar(n));
        }

        private void GoHome()
        {
            if (scenes != null) scenes.LoadScene(AppScenes.Home); else UnityEngine.SceneManagement.SceneManager.LoadScene(AppScenes.Home);
        }

        private void OpenChat(string pf = null, string name = null, int rank = 0)
        {
            if (!string.IsNullOrEmpty(pf)) { ChatFlow.PendingDirectPf = pf; ChatFlow.PendingDirectName = name; ChatFlow.PendingDirectRank = rank; }
            if (scenes != null) scenes.LoadScene(AppScenes.Chat); else UnityEngine.SceneManagement.SceneManager.LoadScene(AppScenes.Chat);
        }

        // ---------------------------------------------------------------- row and section building

        private Transform scrollOf;

        private void ClearDynamic(Transform scroll)
        {
            for (int i = scroll.childCount - 1; i >= 0; i--) { var c = scroll.GetChild(i); if (c.name.StartsWith("Dyn")) Destroy(c.gameObject); }
        }

        private Transform Clone(string f, int templateNode, Transform scroll, string name)
        {
            var src = view.Node(f, templateNode);
            var go = Instantiate(src.gameObject, scroll);
            go.name = name; go.SetActive(true);
            return go.transform;
        }

        private void HideTemplates(string f, Transform scroll, params int[] nodes)
        {
            foreach (int n in nodes) { var t = view.Node(f, n); if (t != null) t.gameObject.SetActive(false); }
            ClearDynamic(scroll);
        }

        private Transform Section(string f, Transform scroll, int template, float top, string title, int count)
        {
            var sec = Clone(f, template, scroll, "DynSec");
            Y(sec, top, SecH);
            var texts = sec.GetComponentsInChildren<ArabicText>(true);
            texts[0].Text = title; texts[1].Text = Ar(count);
            float titleW = TextW(texts[0]) + 4f, countW = Mathf.Max(6f, TextW(texts[1]));
            X(texts[0].transform, 362f - titleW, titleW);
            float badgeLeft = 362f - titleW - 8f - countW;
            X(texts[1].transform.parent, badgeLeft, countW);
            var line = sec.Find("f_secline");
            if (line != null) X(line, 0f, Mathf.Max(20f, badgeLeft - 8f));
            return sec;
        }

        /// <summary>Fills the name, level and status of a row (or of the friend sheet's head) and lays them out as the mockup's flex rows do.</summary>
        private static void Info(ArabicText name, ArabicText level, ArabicText status, float infoW, string n, int lv, string st)
        {
            name.Text = Clip(n, 14); level.Text = Ar(lv); status.Text = st;
            float nw = TextW(name) + 4f, lw = TextW(level), sw = TextW(status) + 4f;
            var b = name.transform.parent; var info = b.parent;
            X(name.transform, infoW - nw, nw);
            float pill = lw + 12f;
            X(level.transform.parent, infoW - nw - 6f - pill, pill);
            X(level.transform, 6f, lw + 2f);
            var small = status.transform.parent;
            X(status.transform, infoW - sw, sw);
            X(b, 0f, infoW); X(small, 0f, infoW);
            var irt = (RectTransform)info; float right = irt.anchoredPosition.x / K + irt.sizeDelta.x / K / 2f;   // the info box keeps its right edge
            X(info, right - infoW, infoW);
        }

        private static void ButtonLabel(ArabicText label, string text)
        {
            label.Text = text;
            float w = TextW(label);
            X(label.transform, 14f, w + 4f);
            X(label.transform.parent, 12f, w + 28f);
        }

        // ---------------------------------------------------------------- friends (f1 and the sheets over it)

        private void DrawFriends(string f)
        {
            WireChrome(f, ChromeList);
            var scroll = view.Node(f, 1); if (scroll == null) return;
            HideTemplates(f, scroll, 2, 7, 19, 31, 36, 48, 60, 72, 77, 89);

            int online = fs.Friends.Count(x => x.Online);
            view.SetText(f, 117, fs.Friends.Count == 0 ? "ما عندك أصدقاء بعد" : Ar(online) + " من " + Ar(fs.Friends.Count) + " متصلين");
            view.SetSprite(f, 119, "icon_chat");                       // the top-left button opens the chat
            view.SetTap(f, 118, () => OpenChat());

            bool iAmInRoom = rooms != null && rooms.InRoom;
            var inRoom = fs.Friends.Where(x => x.Online && x.Room != null).ToList();
            var onlineOnly = fs.Friends.Where(x => x.Online && x.Room == null).ToList();
            var offline = fs.Friends.Where(x => !x.Online).ToList();

            float y = ListTop;
            if (inRoom.Count > 0)
            {
                Section(f, scroll, 2, y, "بغرف الآن", inRoom.Count); y += SecH + Gap;
                foreach (var fr in inRoom) { Row(f, scroll, fr, y, true, iAmInRoom); y += RowH + Gap; }
            }
            if (onlineOnly.Count > 0)
            {
                Section(f, scroll, 2, y, "متصلين", onlineOnly.Count); y += SecH + Gap;
                foreach (var fr in onlineOnly) { Row(f, scroll, fr, y, true, iAmInRoom); y += RowH + Gap; }
            }
            if (offline.Count > 0)
            {
                Section(f, scroll, 2, y, "غير متصلين", offline.Count); y += SecH + Gap;
                foreach (var fr in offline) { Row(f, scroll, fr, y, false, iAmInRoom); y += RowH + Gap; }
            }
            view.MakeScrollable(f, (RectTransform)scroll, Mathf.Max(790f, y + 110f));
        }

        private void Row(string f, Transform scroll, Friend fr, float top, bool online, bool iAmInRoom)
        {
            // which of the mockup's rows to copy: in a joinable room (gold button), in a closed room (ghost), online (invite), online (menu), offline (menu)
            var room = fr.Room;
            bool closed = room != null && (room.Locked || room.IsPrivate || room.InMatch || room.Cap > 0 && room.Count >= room.Cap);
            int tpl = !online ? 77 : room != null ? (closed ? 19 : 7) : (iAmInRoom ? 36 : 60);
            var row = Clone(f, tpl, scroll, "DynRow");
            Y(row, top, RowH);
            var texts = row.GetComponentsInChildren<ArabicText>(true);
            string status = !online ? "آخر ظهور " + Ago(fr.Seen) : room != null ? (room.IsPrivate ? "بغرفة خاصة · مغلقة" : room.InMatch ? "بغرفة «" + room.Name + "» · بالمباراة" : "بغرفة «" + room.Name + "» · " + Ar(room.Count) + " من " + Ar(room.Cap)) : "متصل";
            Info(texts[0], texts[1], texts[2], 216f, fr.Name, fr.Level, status);
            var friend = fr;
            KitTap.On(row, () => OpenSheet(friend));
            if (texts.Length > 3)
            {   // the button on the left
                var btn = texts[3].transform.parent;
                if (tpl == 7) { ButtonLabel(texts[3], "انضم"); KitTap.On(btn, () => JoinFriend(friend)); }
                else if (tpl == 19) { ButtonLabel(texts[3], room != null && room.Cap > 0 && room.Count >= room.Cap ? "ممتلئة" : "مغلقة"); KitTap.On(btn, () => OpenSheet(friend)); }
                else if (tpl == 36) { ButtonLabel(texts[3], "دعوة"); KitTap.On(btn, () => InviteFriend(friend)); }
            }
            else
            {   // the "more" button
                foreach (var t in row.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("f_button.more")) { KitTap.On(t, () => OpenSheet(friend)); break; }
            }
        }

        private void JoinFriend(Friend fr)
        {
            if (fr.Room == null) return;
            fs.JoinRoom(fr.Room, r =>
            {
                if (this == null) return;
                if (!r.Ok) { Say(RoomResult.Message(r.Error, RoomRules.PublicMinLevel)); fs.Refresh(); return; }
                if (scenes != null) scenes.LoadScene(AppScenes.Rooms); else UnityEngine.SceneManagement.SceneManager.LoadScene(AppScenes.Rooms);
            });
        }

        private void InviteFriend(Friend fr)
        {
            fs.Invite(fr.Pf, err => { if (this == null) return; Say(err == null ? "أرسلنا الدعوة لـ" + fr.Name + "." : Message(err)); });
        }

        // ---------------------------------------------------------------- the sheet of one friend (f4)

        private void OpenSheet(Friend fr)
        {
            sheetFriend = fr;
            frame = "f4";
            view.Show("f4");
            Redraw();
        }

        private void CloseSheet() { sheetFriend = null; ShowTab("f1"); }

        private void FillSheet()
        {
            var fr = sheetFriend; if (fr == null) { ShowTab("f1"); return; }
            const string f = "f4";
            view.SetTap(f, 120, CloseSheet);
            var room = fr.Room;
            string status = !fr.Online ? "آخر ظهور " + Ago(fr.Seen) : room != null ? "بغرفة «" + room.Name + "» · " + Ar(room.Count) + " من " + Ar(room.Cap) : "متصل";
            var texts = new[] { view.Node(f, 128).GetComponent<ArabicText>(), view.Node(f, 130).GetComponent<ArabicText>(), view.Node(f, 132).GetComponent<ArabicText>() };
            Info(texts[0], texts[1], texts[2], 298f, fr.Name, fr.Level, status);

            bool canJoin = fr.Online && room != null && !room.Locked && !room.IsPrivate && !room.InMatch && !(room.Cap > 0 && room.Count >= room.Cap);
            bool myRoom = rooms != null && rooms.InRoom;
            var mine = myRoom ? rooms.CurrentRoom : null;
            view.SetText(f, 144, mine != null ? "تدعوه لغرفتك «" + mine.Name + "»" : "");
            view.SetActive(f, 133, canJoin);
            view.SetActive(f, 139, myRoom);
            var items = new List<(int node, float h)>();
            if (canJoin) items.Add((133, 67f));
            if (myRoom) items.Add((139, 67f));
            items.Add((145, 48f)); items.Add((149, 48f)); items.Add((153, 48f));
            float top = 93f;
            foreach (var it in items) { Y(view.Node(f, it.node), top, it.h); top += it.h + 10f; }
            float sheetH = top + 14f;
            var sheet = (RectTransform)view.Node(f, 121);
            sheet.sizeDelta = new Vector2(sheet.sizeDelta.x, sheetH * K);
            if (sheet.anchorMin.y < 0.5f) sheet.anchoredPosition = new Vector2(sheet.anchoredPosition.x, sheetH * K / 2f);   // sits on the bottom edge
            else sheet.anchoredPosition = new Vector2(sheet.anchoredPosition.x, -(790f - sheetH / 2f) * K);

            view.SetTap(f, 133, () => { CloseSheet(); JoinFriend(fr); });
            view.SetTap(f, 139, () => { InviteFriend(fr); CloseSheet(); });
            view.SetTap(f, 145, () => OpenChat(fr.Pf, fr.Name, fr.Rank));
            view.SetTap(f, 149, () => fs.Remove(fr.Pf, err => { Say(err == null ? "شلنا " + fr.Name + " من عائلتك." : Message(err)); CloseSheet(); }));
            view.SetTap(f, 153, () => OpenReport(fr));
        }

        // ---------------------------------------------------------------- report (f6, f6_after)

        private void OpenReport(Friend fr)
        {
            sheetFriend = fr; reportReason = 0; reportBlock = true;
            frame = "f6";
            view.Show("f6");
            Redraw();
        }

        private void FillReport()
        {
            var fr = sheetFriend; if (fr == null) { ShowTab("f1"); return; }
            const string f = "f6";
            view.SetTap(f, 120, CloseSheet);
            view.SetText(f, 124, "إبلاغ عن «" + Clip(fr.Name, 14) + "»");
            for (int i = 0; i < ReasonNodes.Length; i++)
            {
                int idx = i; bool on = i == reportReason;
                view.SetSprite(f, ReasonNodes[i], on ? "opt_on" : "opt_bg");
                view.SetSprite(f, ReasonNodes[i] + 1, on ? "radio_on" : "radio");
                view.SetTap(f, ReasonNodes[i], () => { reportReason = idx; FillReport(); });
            }
            // the note box is a picture: an input is laid over it
            var area = view.Node(f, 154) as RectTransform;
            if (noteInput == null && area != null)
            {
                var placeholder = view.Node(f, 155);
                var mirror = Instantiate(placeholder.gameObject, placeholder.parent).GetComponent<ArabicText>();
                mirror.name = "Mirror"; mirror.Text = "";
                var tmp = mirror.GetComponent<TMP_Text>(); tmp.color = new Color32(0xE8, 0xDF, 0xC9, 255);
                noteInput = KitArabicInput.Attach(area, mirror, placeholder.gameObject, TMP_InputField.ContentType.Standard, 100);
            }
            view.SetSprite(f, 161, reportBlock ? "tg_on" : "tg_off");
            view.SetTap(f, 156, () => { reportBlock = !reportBlock; FillReport(); });
            view.SetTap(f, 165, CloseSheet);
            view.SetTap(f, 163, () =>
            {
                string reason = ReasonCodes[reportReason];
                string note = noteInput != null ? noteInput.Text : "";
                fs.ReportPlayer(fr.Pf, reason, note, ok =>
                {
                    if (this == null) return;
                    if (!ok) { Say("تعذّر إرسال البلاغ، حاول من جديد."); return; }
                    if (reportBlock) chat.Block(fr.Pf, true, null);
                    ReportDone(fr);
                });
            });
        }

        private void ReportDone(Friend fr)
        {
            frame = "f6_after";
            view.Show("f6_after");
            Redraw();
            const string f = "f6_after";
            view.SetActive(f, 130, reportBlock);
            if (reportBlock) view.SetText(f, 133, "حظرنا «" + Clip(fr.Name, 14) + "» عندك. تقدر تشيل الحظر من الإعدادات ← الخصوصية والأمان.");
            view.SetTap(f, 120, CloseSheet);
            view.SetTap(f, 134, CloseSheet);
        }

        // ---------------------------------------------------------------- requests (f2)

        private void DrawRequests()
        {
            const string f = "f2";
            WireChrome(f, ChromeReq);
            var scroll = view.Node(f, 1); if (scroll == null) return;
            HideTemplates(f, scroll, 2, 7, 21, 35, 40, 52);
            int n = fs.Incoming.Count;
            view.SetText(f, 72, n == 0 ? "ما في طلبات" : n == 1 ? "طلب جديد" : n == 2 ? "طلبان جديدان" : Ar(n) + (n <= 10 ? " طلبات جديدة" : " طلباً جديداً"));

            float y = ListTop;
            if (n > 0)
            {
                Section(f, scroll, 2, y, "واردة", n); y += SecH + Gap;
                foreach (var req in fs.Incoming)
                {
                    var row = Clone(f, 21, scroll, "DynRow"); Y(row, y, RowH);
                    var t = row.GetComponentsInChildren<ArabicText>(true);
                    Info(t[0], t[1], t[2], 155f, req.Name, req.Level, "يريد يضيفك لعائلته");
                    var r = req;
                    foreach (var tr in row.GetComponentsInChildren<Transform>(true))
                    {
                        if (tr.name.StartsWith("f_button.go")) KitTap.On(tr, () => fs.Respond(r.Pf, true, err => Say(err == null ? "صرتوا أصدقاء مع " + r.Name + "." : Message(err))));
                        else if (tr.name.StartsWith("f_button.ghost")) KitTap.On(tr, () => fs.Respond(r.Pf, false, err => { if (err != null) Say(Message(err)); }));
                    }
                    y += RowH + Gap;
                }
            }
            if (fs.Outgoing.Count > 0)
            {
                Section(f, scroll, 35, y, "أرسلتها", fs.Outgoing.Count); y += SecH + Gap;
                foreach (var req in fs.Outgoing)
                {
                    var row = Clone(f, 40, scroll, "DynRow"); Y(row, y, RowH);
                    var t = row.GetComponentsInChildren<ArabicText>(true);
                    Info(t[0], t[1], t[2], 221f, req.Name, req.Level, "بانتظار الرد · " + Ago(req.At));
                    var r = req;
                    foreach (var tr in row.GetComponentsInChildren<Transform>(true)) if (tr.name.StartsWith("f_button.ghost")) KitTap.On(tr, () => fs.Cancel(r.Pf, err => { if (err != null) Say(Message(err)); }));
                    y += RowH + Gap;
                }
            }
            var hint = view.Node(f, 52);
            hint.gameObject.SetActive(true);
            Y(hint, y, 50f);
            view.MakeScrollable(f, (RectTransform)scroll, Mathf.Max(790f, y + 50f + 110f));
        }

        // ---------------------------------------------------------------- add by id number (f3)

        private void DrawAdd()
        {
            const string f = "f3";
            WireChrome(f, ChromeAdd);
            var scroll = view.Node(f, 1);
            view.SetText(f, 7, Ar(fs.Me != null ? fs.Me.Num : "······"));
            view.SetTap(f, 8, () => { if (fs.Me != null) { GUIUtility.systemCopyBuffer = fs.Me.Num; Say("نسخنا رقم هويتك."); } });
            if (addInput == null)
            {
                var area = (RectTransform)view.Node(f, 14);
                var mirror = view.Node(f, 15).GetComponent<ArabicText>();
                mirror.Text = "";
                var ph = Instantiate(mirror.gameObject, area); ph.name = "Placeholder";
                var pa = ph.GetComponent<ArabicText>(); pa.Text = "اكتب رقم هوية صديقك (٦ خانات)";
                var pt = ph.GetComponent<TMP_Text>(); var c = pt.color; c.a = 0.45f; pt.color = c;
                addInput = KitArabicInput.Attach(area, mirror, ph, TMP_InputField.ContentType.IntegerNumber, 6);
                addInput.OnChanged += OnNumberTyped;
            }
            var row = view.Node(f, 16);
            bool show = lookedUp != null && lookedUp.Found && lookedUp.Num == lookedUpNum;
            row.gameObject.SetActive(show);
            if (show)
            {
                var t = row.GetComponentsInChildren<ArabicText>(true);
                string st = lookedUp.Self ? "هذا رقمك أنت" : lookedUp.Friend ? "صديقك أصلاً" : lookedUp.Pending ? "طلبك بانتظار الرد" : "لقيناه · مستواه " + Ar(lookedUp.Level);
                Info(t[0], t[1], t[2], 187f, lookedUp.Name, lookedUp.Level, st);
                var btn = t[3].transform.parent;
                bool can = !lookedUp.Self && !lookedUp.Friend && !lookedUp.Pending;
                btn.gameObject.SetActive(can);
                if (can) { ButtonLabel(t[3], "أرسل طلب"); KitTap.On(btn, SendLookedUpRequest); }
            }
            Y(view.Node(f, 27), show ? 395f : 319f, 50f);
        }

        private void OnNumberTyped(string text)
        {
            string digits = new string((text ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length != 6) { lookedUp = null; lookedUpNum = null; if (frame == "f3") DrawAdd(); return; }
            lookedUpNum = digits;
            fs.Lookup(digits, r =>
            {
                if (this == null || lookedUpNum != digits) return;
                if (!r.Found) { lookedUp = null; Say(Message(r.Error ?? "unknown_number")); }
                else lookedUp = r;
                if (frame == "f3") DrawAdd();
            });
        }

        private void SendLookedUpRequest()
        {
            if (lookedUp == null) return;
            string num = lookedUp.Num, name = lookedUp.Name;
            fs.SendRequest(num, (err, accepted) =>
            {
                if (this == null) return;
                if (err != null) { Say(Message(err)); return; }
                Say(accepted ? "صرتوا أصدقاء مع " + name + "." : "أرسلنا طلبك لـ" + name + ".");
                lookedUp = null; lookedUpNum = null;
                if (addInput != null) addInput.Text = "";
                if (frame == "f3") DrawAdd();
            });
        }
    }
}
