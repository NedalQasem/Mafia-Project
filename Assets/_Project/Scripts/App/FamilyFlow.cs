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
    /// Puts the family on the screens of Docs/mockups/family.html. The tab opens on the chats (c1 private, c2 nothing yet, c3 groups, from
    /// family-chat.html); the friends button on top leads to the friends pages: the list grouped by where everybody is, the requests, adding
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

        /// <summary>Set by the chat screen before it comes back: which chats frame to open (c1 private, c3 groups).</summary>
        public static string StartFrame = "c1";

        private string frame = "c1";
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
            chat.OnConversationsChanged += Redraw;
            fs.Refresh();
            chat.RefreshInbox();
            string start = view.Has(StartFrame) ? StartFrame : "f1";
            StartFrame = "c1";
            ShowTab(start);
        }

        private void OnDestroy()
        {
            if (fs != null) { fs.OnChanged -= Redraw; fs.OnNews -= OnNews; }
            if (chat != null) chat.OnConversationsChanged -= Redraw;
        }

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

        private static string Clip(string name, int max) { name = Loc.T(name); return string.IsNullOrEmpty(name) ? "" : name.Length > max ? name.Substring(0, max - 1) + "…" : name; }

        /// <summary>Shows the text on one line no wider than <paramref name="maxCss"/> (mockup pixels): measured, and cut with "…" only when it does not fit.</summary>
        private static void FitWidth(ArabicText a, string s, float maxCss)
        {
            s = Loc.T(s ?? "");                     // in English the English line is the one that is cut
            a.Text = s;
            if (TextW(a) <= maxCss) return;
            var font = a.GetComponent<TMP_Text>().font;
            string dots = font != null && font.name.Contains("Lalezar") ? "..." : "…";      // Lalezar draws "…" as a single dot
            int lo = 0, hi = s.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                a.Text = s.Substring(0, mid).TrimEnd() + dots;
                if (TextW(a) <= maxCss) lo = mid; else hi = mid - 1;
            }
            a.Text = s.Substring(0, lo).TrimEnd() + dots;
        }

        // ---------------------------------------------------------------- the frame and its chrome (tabs, top bar)

        /// <summary>A tab pressed on the screen: the page burns away to the next one (like the bottom bar's screens, but quicker).</summary>
        private void Tab(string f)
        {
            if (f == frame || view == null) { ShowTab(f); return; }
            KitTabBurn.Swap(this, (RectTransform)view.transform, () => ShowTab(f), f == "c3" || f == "f2" || f == "f3" ? MafiaNight.UI.Animations.BurnCorner.BottomLeft : MafiaNight.UI.Animations.BurnCorner.BottomRight);
        }

        private void ShowTab(string f)
        {
            frame = f;
            view.Show(f);
            Redraw();
        }

        /// <summary>The family tab's menu in the bottom bar was used while this screen is open: "c1" the chats, "f1" the friends.</summary>
        public void OpenPlace(string f) => ShowTab(f);

        private KitNavBar[] navBars;

        private void MarkPlace()      // the crimson row in the family tab's menu: chats (0) or friends (1)
        {
            if (navBars == null) navBars = FindObjectsByType<KitNavBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int place = frame.StartsWith("c") ? 0 : 1;
            foreach (var nb in navBars) if (nb != null) nb.SetCurrentPlace(1, place);
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
                case "c1": case "c2": DrawChats(false); break;
                case "c3": DrawChats(true); break;
                default: DrawFriends(frame); break;
            }
            MarkPlace();
        }

        private void WireChrome(string f, Chrome c)
        {
            view.SetTap(f, c.Friends, () => Tab("f1"));
            view.SetTap(f, c.Requests, () => Tab("f2"));
            view.SetTap(f, c.Add, () => Tab("f3"));
            view.SetTap(f, c.Back, () => ShowTab("c1"));      // the friends pages sit behind the chats
            var title = view.Node(f, c.Sub - 2);              // the title sits two parts before the subtitle
            var ta = title != null ? title.GetComponent<ArabicText>() : null;
            if (ta != null && ta.Text != "الأصدقاء")
            {
                var rt = (RectTransform)title;
                float right = rt.anchoredPosition.x / K + rt.sizeDelta.x / K / 2f;
                ta.Text = "الأصدقاء";
                float w = TextW(ta) + 4f;
                X(title, right - w, w);
            }
            int n = fs.Incoming.Count;
            view.SetActive(f, c.Badge, n > 0);
            if (n > 0) view.SetText(f, c.BadgeNum, Ar(n));
        }

        private void OpenChat(string pf = null, string name = null, int rank = 0)
        {
            if (!string.IsNullOrEmpty(pf)) { ChatFlow.PendingDirectPf = pf; ChatFlow.PendingDirectName = name; ChatFlow.PendingDirectRank = rank; }
            LoadChat();
        }

        private void OpenConversation(string channel) { ChatFlow.PendingChannel = channel; LoadChat(); }
        private void OpenNewGroup() { ChatFlow.PendingNewGroup = true; LoadChat(); }

        private void LoadChat()
        {
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
            level.Text = Ar(lv);
            float lw = TextW(level);
            FitWidth(name, n, infoW - lw - 22f);
            FitWidth(status, st, infoW - 4f);
            float nw = TextW(name) + 4f, sw = TextW(status) + 4f;
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
            view.SetActive(f, 118, false);                             // the chats are behind the back button now

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
            var friend = fr;
            KitTap.On(row, () => OpenSheet(friend));
            if (texts.Length > 3)
            {   // the button on the left, and the message button next to it (a copy of the round "more" button)
                var btn = texts[3].transform.parent;
                if (tpl == 7) { ButtonLabel(texts[3], "انضم"); KitTap.On(btn, () => JoinFriend(friend)); }
                else if (tpl == 19) { ButtonLabel(texts[3], room != null && room.Cap > 0 && room.Count >= room.Cap ? "ممتلئة" : "مغلقة"); KitTap.On(btn, () => OpenSheet(friend)); }
                else if (tpl == 36) { ButtonLabel(texts[3], "دعوة"); KitTap.On(btn, () => InviteFriend(friend)); }
                float btnRight = 12f + TextW(texts[3]) + 28f;
                var more = FindPart(view.Node(f, 77), "f_button.more");
                if (more != null)
                {
                    var msg = Instantiate(more.gameObject, row).transform;
                    msg.name = "f_button.msg";
                    msg.gameObject.SetActive(true);
                    X(msg, btnRight + 8f, 32f);
                    MessageButton(msg, friend);
                }
                Info(texts[0], texts[1], texts[2], 295f - (btnRight + 8f + 32f + 10f), fr.Name, fr.Level, status);
            }
            else
            {   // the round button becomes the message button (the sheet opens from the row)
                Info(texts[0], texts[1], texts[2], 216f, fr.Name, fr.Level, status);
                var more = FindPart(row, "f_button.more");
                if (more != null) MessageButton(more, friend);
            }
        }

        private static Transform FindPart(Transform root, string prefix)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith(prefix)) return t;
            return null;
        }

        private void MessageButton(Transform button, Friend fr)
        {
            var icon = FindPart(button, "f_svg");
            var im = icon != null ? icon.GetComponent<Image>() : null;
            var s = view.Sprite("icon_chat");
            if (im != null && s != null) im.sprite = s;
            KitTap.On(button, () => OpenChat(fr.Pf, fr.Name, fr.Rank));
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

        // ---------------------------------------------------------------- the chats (c1 private, c2 nothing yet, c3 groups)

        // per frame: subtitle, friends button, its badge and number; the private / groups / public tabs (button, label, badge, number)
        private struct ChatsChrome
        {
            public int Sub, Friends, Dot, DotNum, Dm, DmLabel, DmBadge, DmNum, Gr, GrLabel, GrBadge, GrNum, Pub;
            public ChatsChrome(int sub, int fr, int dot, int dotNum, int dm, int dmL, int dmB, int dmN, int gr, int grL, int grB, int grN, int pub)
            { Sub = sub; Friends = fr; Dot = dot; DotNum = dotNum; Dm = dm; DmLabel = dmL; DmBadge = dmB; DmNum = dmN; Gr = gr; GrLabel = grL; GrBadge = grB; GrNum = grN; Pub = pub; }
        }
        private static readonly ChatsChrome ChromeC1 = new ChatsChrome(135, 136, 138, 139, 121, 122, 123, 124, 125, 126, 127, 128, 129);
        private static readonly ChatsChrome ChromeC2 = new ChatsChrome(27, 28, 30, 31, 15, 16, -1, -1, 17, 18, 19, 20, 21);
        private static readonly ChatsChrome ChromeC3 = new ChatsChrome(43, 44, 46, 47, 29, 30, 31, 32, 33, 34, 35, 36, 37);
        private const float TabW = 117f, ChatRowH = 66f, ChatGap = 10f;

        private List<ChatConv> Convs(bool groups) =>
            chat.Conversations.Where(c => c.IsGroup == groups).OrderByDescending(c => c.Last != null ? c.Last.Ts : 0).ToList();

        private void DrawChats(bool groups)
        {
            var dms = Convs(false); var grs = Convs(true);
            string f = groups ? "c3" : dms.Count == 0 ? "c2" : "c1";
            if (frame != f || view.Current != f) { frame = f; view.Show(f); }
            var c = f == "c1" ? ChromeC1 : f == "c2" ? ChromeC2 : ChromeC3;

            int unread = chat.UnreadTotal, online = fs.Friends.Count(x => x.Online);
            string sub = fs.Friends.Count == 0 && unread == 0 ? (dms.Count == 0 && grs.Count == 0 ? "ما عندك محادثات بعد" : "ما عندك أصدقاء بعد")
                : (unread > 0 ? UnreadText(unread) : "ما في رسائل جديدة") + (online > 0 ? " · " + Ar(online) + " متصلين" : "");
            FitWidth(view.Node(f, c.Sub).GetComponent<ArabicText>(), sub, 300f);
            int req = fs.Incoming.Count;
            view.SetActive(f, c.Dot, req > 0);
            if (req > 0) view.SetText(f, c.DotNum, KitText.Badge(req));
            view.SetTap(f, c.Friends, () => ShowTab("f1"));

            TabLabel(f, c.DmLabel, c.DmBadge, c.DmNum, dms.Sum(x => x.Unread));
            TabLabel(f, c.GrLabel, c.GrBadge, c.GrNum, grs.Sum(x => x.Unread));
            view.SetTap(f, c.Dm, () => Tab("c1"));
            view.SetTap(f, c.Gr, () => Tab("c3"));
            view.SetTap(f, c.Pub, () => OpenConversation(ChatService.Public));

            if (f == "c2") { DrawNoChats(); return; }
            var scroll = view.Node(f, 1); if (scroll == null) return;
            float y = ListTop;
            if (f == "c1")
            {
                HideTemplates(f, scroll, 35, 50, 65, 80, 93, 106);    // the six sample rows of the mockup
                y = DrawOnlineStrip(f, scroll, y);
                Y(view.Node(f, 32), y, SecH); y += SecH + ChatGap;
                foreach (var cv in dms) { ChatRow(f, scroll, cv.Unread > 0 ? 35 : 80, cv, y); y += ChatRowH + ChatGap; }
            }
            else
            {
                HideTemplates(f, scroll, 2, 14);
                foreach (var cv in grs) { ChatRow(f, scroll, cv.Unread > 0 ? 2 : 14, cv, y); y += ChatRowH + ChatGap; }
                var newg = view.Node(f, 24);
                Y(newg, y, 52f); y += 52f;
                view.SetTap(f, 24, OpenNewGroup);
            }
            view.MakeScrollable(f, (RectTransform)scroll, Mathf.Max(790f, y + 110f));
        }

        private static string UnreadText(int n) => n == 1 ? "رسالة جديدة" : n == 2 ? "رسالتان جديدتان" : KitText.Badge(n) + (n <= 10 ? " رسائل جديدة" : " رسالة جديدة");

        /// <summary>Centres a tab's words, with its red count (when there is one) on their left as in the mockup's flex row.</summary>
        private void TabLabel(string f, int labelNode, int badgeNode, int numNode, int count)
        {
            var label = view.Node(f, labelNode).GetComponent<ArabicText>();
            float lw = TextW(label) + 2f, bw = 0f;
            bool show = badgeNode >= 0 && count > 0;
            if (badgeNode >= 0) view.SetActive(f, badgeNode, show);
            if (show)
            {
                var num = view.Node(f, numNode).GetComponent<ArabicText>();
                num.Text = KitText.Badge(count);
                float nw = TextW(num) + 1f;
                bw = Mathf.Max(17f, nw + 8f);
                X(view.Node(f, badgeNode), 0f, bw);    // placed below with the label
                X(num.transform, (bw - nw) / 2f, nw);
            }
            float total = lw + (show ? 6f + bw : 0f), start = (TabW - total) / 2f;
            X(label.transform, start + (show ? bw + 6f : 0f), lw);
            if (show) X(view.Node(f, badgeNode), start, bw);
        }

        /// <summary>The friends who are on line, in rooms first, as round buttons under the tabs (up to four besides "add"); returns where the chats start.</summary>
        private float DrawOnlineStrip(string f, Transform scroll, float y)
        {
            var strip = view.Node(f, 7);
            for (int i = strip.childCount - 1; i >= 0; i--) { var ch = strip.GetChild(i); if (ch.name.StartsWith("Dyn")) Destroy(ch.gameObject); }
            foreach (int n in new[] { 12, 17, 22, 27 }) view.SetActive(f, n, false);
            var on = fs.Friends.Where(x => x.Online).OrderBy(x => x.Room == null ? 1 : 0).ToList();
            bool show = on.Count > 0;
            view.SetActive(f, 2, show); view.SetActive(f, 7, show);
            if (!show) return y;
            Y(view.Node(f, 2), y, SecH);
            var count = view.Node(f, 6).GetComponent<ArabicText>(); count.Text = Ar(on.Count);
            y += SecH + ChatGap;
            Y(strip, y, 79f);
            view.SetTap(f, 8, () => ShowTab("f3"));
            int k = 0;
            foreach (var fr in on.Take(4))
            {
                var pp = Instantiate(view.Node(f, 12).gameObject, strip).transform;
                pp.name = "DynPP"; pp.gameObject.SetActive(true);
                X(pp, 232f - 70f * k, 58f);
                var st = FindPart(pp, "f_i.st");
                if (st != null) SetPicture(st, fr.Room != null ? "st_room" : "st_on");
                var label = pp.GetComponentInChildren<ArabicText>(true);
                FitWidth(label, fr.Name, 58f);
                float w = TextW(label) + 2f; X(label.transform, (58f - w) / 2f, w);
                var friend = fr;
                KitTap.On(pp, () => OpenChat(friend.Pf, friend.Name, friend.Rank));
                k++;
            }
            return y + 79f + 14f;
        }

        private void SetPicture(Transform part, string sprite)
        {
            var v = part.Find("V");
            var im = v != null ? v.GetComponent<Image>() : part.GetComponent<Image>();
            var s = view.Sprite(sprite);
            if (im != null && s != null) im.sprite = s;
        }

        /// <summary>One conversation row: name (and rank) and the last line on the right, the time and the unread count on the left.</summary>
        private void ChatRow(string f, Transform scroll, int tpl, ChatConv cv, float top)
        {
            var row = Clone(f, tpl, scroll, "DynRow");
            Y(row, top, ChatRowH);
            var t = row.GetComponentsInChildren<ArabicText>(true);
            bool rank = !cv.IsGroup, badge = cv.Unread > 0;
            int i = 0;
            var name = t[i++]; var lv = rank ? t[i++] : null; var last = t[i++]; var time = t[i++]; var num = badge ? t[i++] : null;

            if (!cv.IsGroup)
            {   // the dot on the avatar: in a room, on line, away (the friends list knows better than the chat)
                var fr = fs.Friends.FirstOrDefault(x => x.Pf == cv.OtherPf);
                bool isOn = fr != null ? fr.Online : cv.Online;
                var st = FindPart(row, "f_i.st");
                if (st != null) SetPicture(st, fr != null && fr.Online && fr.Room != null ? "st_room" : isOn ? "st_on" : "st_off");
            }

            // the left column: time over the count
            time.Text = ChatTime(cv.Last);
            float tw = TextW(time) + 1f, bw = 0f;
            if (badge) { num.Text = KitText.Badge(cv.Unread); float nw = TextW(num) + 1f; bw = Mathf.Max(17f, nw + 8f); X(num.transform, (bw - nw) / 2f, nw); }
            float metaW = Mathf.Max(tw, bw);
            X(time.transform.parent, metaW - tw, tw); X(time.transform, 0f, tw);
            if (badge) X(num.transform.parent, metaW - bw, bw);
            X(time.transform.parent.parent, 12f, metaW);

            // the right column: name, rank and last line, as wide as the row leaves
            float infoLeft = 12f + metaW + 10f, infoW = 295f - infoLeft;
            float pill = 0f;
            if (lv != null) { lv.Text = KitTheme.RankNames[Mathf.Clamp(cv.Rank, 0, KitTheme.RankNames.Length - 1)]; float lw = TextW(lv); pill = lw + 12f; X(lv.transform, 6f, lw + 2f); }
            FitWidth(name, cv.Name, infoW - (pill > 0 ? pill + 6f : 0f));
            float nw2 = TextW(name) + 4f;
            X(name.transform, infoW - nw2, nw2);
            if (lv != null) X(lv.transform.parent, infoW - nw2 - 6f - pill, pill);
            string text = cv.Last == null ? "" : (cv.Last.Mine ? "أنا: " : cv.IsGroup && !cv.Last.System ? cv.Last.Name + ": " : "") + cv.Last.Text;
            FitWidth(last, text, infoW);
            float lw2 = TextW(last) + 4f;
            X(last.transform, infoW - lw2, lw2);
            last.GetComponent<TMP_Text>().color = badge ? new Color32(0xE6, 0xD9, 0xBD, 255) : new Color32(0x8F, 0x87, 0x77, 255);
            X(name.transform.parent, 0f, infoW); X(last.transform.parent, 0f, infoW);
            X(name.transform.parent.parent, infoLeft, infoW);

            string channel = cv.Channel;
            KitTap.On(row, () => OpenConversation(channel));
        }

        private static string ChatTime(ChatMsg m)
        {
            if (m == null || m.Ts <= 0) return "";
            var d = DateTimeOffset.FromUnixTimeMilliseconds(m.Ts).ToLocalTime();
            if (d.Date == DateTime.Now.Date) return Ar(d.ToString("h:mm"));
            if (d.Date == DateTime.Now.Date.AddDays(-1)) return "أمس";
            return Ar(d.ToString("d/M"));
        }

        /// <summary>No private chat yet: the welcome letter; the hint at the friends button only for a player without friends.</summary>
        private void DrawNoChats()
        {
            const string f = "c2";
            bool lonely = fs.Friends.Count == 0;
            view.SetActive(f, 32, lonely);
            view.SetText(f, 4, lonely ? "عائلتك لسا فاضية" : "ما في محادثات بعد");
            view.SetText(f, 6, lonely ? "ضيف أصدقاء برقم الهوية، أو تعرّف على الناس بالساحة العامة وبالسهرات. لما تحكي مع حدا بتطلع محادثته هون."
                                      : "افتح صفحة الأصدقاء من الزر فوق وابعت رسالة لأي صديق، أو احكي مع الناس بالساحة العامة. محادثاتك بتطلع هون.");
            view.SetTap(f, 7, () => ShowTab("f3"));
            view.SetTap(f, 10, () => OpenConversation(ChatService.Public));
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
