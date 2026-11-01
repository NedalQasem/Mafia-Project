using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The voice-room screen of every room (Docs/mockups/office-room.html): name, ID and member count on top; under them the mic seats
    /// (a big seat for the host or admin and a few ordinary ones); a strip with the raised hands; everything below is the room's chat; the bottom bar has
    /// the message field, the hand / mic button and the games button. It builds itself from the kit theme, so <see cref="Create"/> is all a screen needs.
    /// The app layer owns the state and calls <see cref="Show"/> again whenever it changes; this class only draws and reports taps.
    /// </summary>
    internal static class RoomTextExt { public static RectTransform Rt(this ArabicText a) => (RectTransform)a.transform; }

    public sealed class KitRoomScreen : MonoBehaviour
    {
        // ---- what the app hands over -------------------------------------------------------------------------------

        public enum SeatKind { Empty, Locked, Taken }
        public enum MicButton { Sit, Hand, HandUp, Mic, Hidden }

        public sealed class SeatVm
        {
            public string Id = "", Name = "", Tag = "";
            public SeatKind Kind = SeatKind.Empty;
            public bool Talking, Muted, Me, Staff, IsNew;
        }

        public sealed class ChatVm
        {
            public string Id = "", Name = "", Tag = "", Text = "", Time = "";
            public bool Mine, System, Pinned, Staff;
        }

        public sealed class ViewVm
        {
            public string Name = "", RoomId = "", Badge = "", HeadLabel = "المضيف";
            public int Members;
            public SeatVm Head = new SeatVm();
            public List<SeatVm> Seats = new List<SeatVm>();
            /// <summary>The text of the strip under the seats ("٣ ينتظروا المايك"), empty hides it.</summary>
            public string Strip = "", StripSmall = "";
            public MicButton Mic = MicButton.Hidden;
            public bool CanType = true;
            /// <summary>The big red button of a match room (the host starts the match); empty hides it.</summary>
            public string StartLabel = "";
            public bool StartEnabled;
            public bool CanLeaveSeat;
        }

        // ---- what the screen reports -------------------------------------------------------------------------------

        public event Action OnBack, OnMembers, OnMic, OnLeaveSeat, OnStart, OnGames, OnTitle;
        /// <summary>A seat was tapped: its index, or -1 for the big seat.</summary>
        public event Action<int> OnSeat;
        public event Action<string> OnSend;
        /// <summary>A chat message was tapped (the sender's id; for the moderation sheet).</summary>
        public event Action<ChatVm> OnMessage;

        private const float W = 1080f;
        private static readonly Color Panel = new Color32(0x12, 0x0D, 0x0A, 255);

        private KitTheme th;
        private RectTransform root, seatsPanel, chatContent, stripBox;
        private ScrollRect chatScroll;
        private ArabicText nameText, subText, countText, stripText, stripSmall, micLabel, startLabel;
        private Image micImage, startImage;
        private GameObject startBox, leaveBox, micBox, stripGo;
        private KitArabicInput field;
        private LayoutElement seatsLayout;
        private readonly List<ChatVm> chat = new List<ChatVm>();
        private ViewVm view;
        private bool stickToBottom = true;

        public static KitRoomScreen Create(Transform canvasRoot)
        {
            var go = new GameObject("RoomScreen", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            var s = go.AddComponent<KitRoomScreen>();
            s.Build();
            go.SetActive(false);
            return s;
        }

        public bool IsOpen => gameObject.activeSelf;

        public void Show(ViewVm v)
        {
            view = v;
            if (!gameObject.activeSelf) { gameObject.SetActive(true); transform.SetAsLastSibling(); }
            Render();
        }

        public void Hide() { gameObject.SetActive(false); }

        public void ClearChat() { chat.Clear(); if (chatContent != null) for (int i = chatContent.childCount - 1; i >= 0; i--) Destroy(chatContent.GetChild(i).gameObject); }

        public void AddChat(ChatVm m)
        {
            chat.Add(m);
            if (chat.Count > 120) { chat.RemoveAt(0); if (chatContent.childCount > 0) Destroy(chatContent.GetChild(0).gameObject); }
            if (gameObject.activeSelf) AddBubble(m);
        }

        public void FocusField() { if (field != null) field.Focus(); }

        // ---- building ------------------------------------------------------------------------------------------------

        private void Build()
        {
            th = KitTheme.Current;
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            Img(root, "Backdrop", null, Panel, false, true);
            var safe = Rect(root, "Safe", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<KitSafeArea>();

            var col = safe.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(34, 34, 24, 24); col.spacing = 22;
            col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandWidth = true; col.childForceExpandHeight = false;

            BuildHeader(safe);
            BuildSeatsPanel(safe);
            BuildStrip(safe);
            BuildChat(safe);
            BuildStartButton(safe);
            BuildBottom(safe);
        }

        private void BuildHeader(Transform parent)
        {
            var top = Rect(parent, "Header", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Fixed(top, 150);

            var back = Circle(top, "Back", new Vector2(1, 0.5f), new Vector2(-62, 0), 112, new Color(0, 0, 0, 0.55f), new Color32(0x6B, 0x5A, 0x3F, 255));
            var arrow = Img(back, "Arrow", th.triUp, new Color32(0xCF, 0xC6, 0xB2, 255), false); Place(arrow.rectTransform, Vector2.zero, new Vector2(40, 40)); arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            KitTap.On(back, () => OnBack?.Invoke());

            var titleBox = Rect(top, "Title", new Vector2(0, 0), new Vector2(1, 1), new Vector2(250, 0), new Vector2(-140, 0));
            nameText = Txt(titleBox, "Name", "", th.fontLalezar, 68, th.ivory, TextAlignmentOptions.BottomRight);
            Anchor(nameText.Rt(), new Vector2(0, 0.45f), new Vector2(1, 1));
            subText = Txt(titleBox, "Sub", "", th.fontBody, 38, th.brass, TextAlignmentOptions.TopRight);
            Anchor(subText.Rt(), new Vector2(0, 0), new Vector2(1, 0.45f));
            KitTap.On(titleBox, () => OnTitle?.Invoke());

            var pill = Img(top, "Members", th.leatherDark, Color.white, true);
            var pr = pill.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(0, 0.5f); pr.pivot = new Vector2(0, 0.5f); pr.anchoredPosition = new Vector2(8, 0); pr.sizeDelta = new Vector2(230, 104);
            var ic = Img(pill.transform, "Icon", th.fedSil, th.brassLight, false); Place(ic.rectTransform, new Vector2(70, 0), new Vector2(60, 60));
            countText = Txt(pill.transform, "Count", "", th.fontLalezar, 56, th.brassLight, TextAlignmentOptions.Midline);
            Anchor(countText.Rt(), new Vector2(0, 0), new Vector2(1, 1)); countText.Rt().offsetMin = new Vector2(20, 0); countText.Rt().offsetMax = new Vector2(-110, 0);
            KitTap.On(pill.transform, () => OnMembers?.Invoke());
        }

        private void BuildSeatsPanel(Transform parent)
        {
            seatsPanel = Rect(parent, "Seats", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            seatsLayout = seatsPanel.gameObject.AddComponent<LayoutElement>(); seatsLayout.preferredHeight = 700;
            var bg = Img(seatsPanel, "Bg", th.leatherDark, new Color(0.62f, 0.55f, 0.5f, 1f), true);
            Anchor(bg.rectTransform, Vector2.zero, Vector2.one);
        }

        private void BuildStrip(Transform parent)
        {
            stripBox = Rect(parent, "Strip", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            stripGo = stripBox.gameObject; Fixed(stripBox, 104);
            var bg = Img(stripBox, "Bg", th.leatherDark, new Color(0.7f, 0.6f, 0.4f, 1f), true); Anchor(bg.rectTransform, Vector2.zero, Vector2.one);
            stripText = Txt(stripBox, "Text", "", th.fontBody, 42, th.brassLight, TextAlignmentOptions.MidlineRight);
            Anchor(stripText.Rt(), Vector2.zero, Vector2.one); stripText.Rt().offsetMin = new Vector2(30, 0); stripText.Rt().offsetMax = new Vector2(-34, 0);
            stripSmall = Txt(stripBox, "Small", "", th.fontBody, 34, th.ivoryDim, TextAlignmentOptions.MidlineLeft);
            Anchor(stripSmall.Rt(), Vector2.zero, Vector2.one); stripSmall.Rt().offsetMin = new Vector2(34, 0); stripSmall.Rt().offsetMax = new Vector2(-30, 0);
        }

        private void BuildChat(Transform parent)
        {
            var area = Rect(parent, "Chat", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var le = area.gameObject.AddComponent<LayoutElement>(); le.flexibleHeight = 1; le.minHeight = 200;
            var bg = Img(area, "Bg", th.leatherDark, new Color(0.45f, 0.4f, 0.38f, 1f), true); Anchor(bg.rectTransform, Vector2.zero, Vector2.one);

            var viewport = Rect(area, "Viewport", Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
            viewport.gameObject.AddComponent<RectMask2D>();
            chatContent = Rect(viewport, "Content", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, Vector2.zero);
            chatContent.pivot = new Vector2(0.5f, 0);
            var v = chatContent.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(18, 18, 18, 18); v.spacing = 18; v.childAlignment = TextAnchor.LowerCenter;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var fit = chatContent.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            chatScroll = area.gameObject.AddComponent<ScrollRect>();
            chatScroll.viewport = viewport; chatScroll.content = chatContent; chatScroll.horizontal = false; chatScroll.vertical = true;
            chatScroll.movementType = ScrollRect.MovementType.Clamped; chatScroll.scrollSensitivity = 40f;
            chatScroll.onValueChanged.AddListener(_ => stickToBottom = chatScroll.verticalNormalizedPosition <= 0.02f);
        }

        private void BuildStartButton(Transform parent)
        {
            startBox = Rect(parent, "Start", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            Fixed((RectTransform)startBox.transform, 150);
            startImage = Img(startBox.transform, "Bg", th.leatherRed, Color.white, true); Anchor(startImage.rectTransform, Vector2.zero, Vector2.one);
            startLabel = Txt(startBox.transform, "Label", "", th.fontLalezar, 70, th.leatherText, TextAlignmentOptions.Midline); Anchor(startLabel.Rt(), Vector2.zero, Vector2.one);
            KitTap.On(startBox.transform, () => { if (view != null && view.StartEnabled) OnStart?.Invoke(); });
        }

        private void BuildBottom(Transform parent)
        {
            var bottom = Rect(parent, "Bar", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Fixed(bottom, 170);
            var bg = Img(bottom, "Bg", th.leatherDark, Color.white, true); Anchor(bg.rectTransform, Vector2.zero, Vector2.one);
            var row = Rect(bottom, "Row", Vector2.zero, Vector2.one, new Vector2(22, 20), new Vector2(-22, -20));
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16; h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = true;

            // left to right: games, leave seat, hand / mic, message field (the field is at the right in Arabic)
            var games = RoundButton(row, "Games", 124, new Color(0, 0, 0, 0.55f));
            for (int i = 0; i < 4; i++)
            {
                var sq = Img(games, "Sq" + i, th.dotWhite, new Color32(0xCF, 0xC6, 0xB2, 255), false);
                Place(sq.rectTransform, new Vector2((i % 2 == 0 ? -14 : 14), (i < 2 ? 14 : -14)), new Vector2(22, 22));
            }
            var soon = Txt(games, "Soon", "قريباً", th.fontLalezar, 28, new Color32(0x06, 0x23, 0x1A, 255), TextAlignmentOptions.Midline);
            var sb = Img(games, "SoonBg", th.dotWhite, th.teal, false); sb.transform.SetSiblingIndex(games.childCount - 2);
            sb.rectTransform.anchorMin = sb.rectTransform.anchorMax = new Vector2(0.5f, 1f); sb.rectTransform.sizeDelta = new Vector2(98, 34); sb.rectTransform.anchoredPosition = new Vector2(0, 4);
            soon.Rt().SetParent(sb.rectTransform, false); Anchor(soon.Rt(), Vector2.zero, Vector2.one);
            KitTap.On(games, () => OnGames?.Invoke());

            leaveBox = RoundButton(row, "Leave", 124, new Color32(0x56, 0x10, 0x16, 255)).gameObject;
            var lt = Txt(leaveBox.transform, "T", "نزول", th.fontLalezar, 34, th.leatherText, TextAlignmentOptions.Midline); Anchor(lt.Rt(), Vector2.zero, Vector2.one);
            KitTap.On(leaveBox.transform, () => OnLeaveSeat?.Invoke());

            var mic = RoundButton(row, "Mic", 150, th.brass);
            micBox = mic.gameObject; micImage = mic.GetComponent<Image>();
            micLabel = Txt(mic, "Label", "", th.fontLalezar, 48, th.brassText, TextAlignmentOptions.Midline); Anchor(micLabel.Rt(), Vector2.zero, Vector2.one);
            KitTap.On(mic, () => OnMic?.Invoke());

            var input = Img(row, "Field", th.leatherDark, new Color(0.35f, 0.3f, 0.28f, 1f), true);
            var inLe = input.gameObject.AddComponent<LayoutElement>(); inLe.flexibleWidth = 1; inLe.minWidth = 200;
            var ph = Txt(input.transform, "Placeholder", "اكتب رسالتك…", th.fontBody, 44, th.ivoryDim, TextAlignmentOptions.MidlineRight);
            Anchor(ph.Rt(), Vector2.zero, Vector2.one); ph.Rt().offsetMin = new Vector2(24, 0); ph.Rt().offsetMax = new Vector2(-28, 0);
            var mirror = Txt(input.transform, "Mirror", "", th.fontBody, 44, th.ivory, TextAlignmentOptions.MidlineRight);
            Anchor(mirror.Rt(), Vector2.zero, Vector2.one); mirror.Rt().offsetMin = new Vector2(24, 0); mirror.Rt().offsetMax = new Vector2(-28, 0);
            field = KitArabicInput.Attach(input.rectTransform, mirror, ph.gameObject, TMP_InputField.ContentType.Standard, 160);
            field.OnSubmit += Send;

            var send = RoundButton(row, "Send", 124, new Color32(0x8A, 0x1C, 0x24, 255));
            var si = Img(send, "Icon", th.iconSend, th.leatherText, false); Place(si.rectTransform, Vector2.zero, new Vector2(60, 60));
            KitTap.On(send, () => Send(field.Text));
            // the row is built left to right: put the field first so it ends up at the right edge in Arabic
            input.transform.SetAsFirstSibling(); send.SetSiblingIndex(1);
            micBox.transform.SetSiblingIndex(2); leaveBox.transform.SetSiblingIndex(3); games.SetSiblingIndex(4);
        }

        private void Send(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || view == null || !view.CanType) return;
            field.Text = "";
            OnSend?.Invoke(text);
        }

        // ---- drawing -------------------------------------------------------------------------------------------------

        private void Render()
        {
            var v = view;
            nameText.Text = v.Name;
            subText.Text = "ID " + v.RoomId + (string.IsNullOrEmpty(v.Badge) ? "" : "  ·  " + v.Badge);
            countText.Text = CoinPill.Format(v.Members);

            RenderSeats();

            bool strip = !string.IsNullOrEmpty(v.Strip);
            stripGo.SetActive(strip);
            if (strip) { stripText.Text = v.Strip; stripSmall.Text = v.StripSmall; }

            micBox.SetActive(v.Mic != MicButton.Hidden);
            if (v.Mic != MicButton.Hidden)
            {
                bool mic = v.Mic == MicButton.Mic;
                micImage.color = mic ? new Color32(0x2C, 0x8D, 0x73, 255) : v.Mic == MicButton.HandUp ? new Color32(0xE8, 0xC2, 0x7A, 255) : th.brass;
                micLabel.Text = mic ? "ميك" : v.Mic == MicButton.HandUp ? "نزّل إيدك" : v.Mic == MicButton.Sit ? "اقعد" : "ارفع إيدك";
                micLabel.GetComponent<TMP_Text>().fontSize = v.Mic == MicButton.HandUp ? 34 : 44;
            }
            leaveBox.SetActive(v.CanLeaveSeat);

            bool start = !string.IsNullOrEmpty(v.StartLabel);
            startBox.SetActive(start);
            if (start) { startLabel.Text = v.StartLabel; startImage.color = v.StartEnabled ? Color.white : new Color(0.55f, 0.5f, 0.5f, 1f); }
            field.gameObject.SetActive(true);

            // the chat is rebuilt after a closed and opened screen
            if (chatContent.childCount != chat.Count) { for (int i = chatContent.childCount - 1; i >= 0; i--) Destroy(chatContent.GetChild(i).gameObject); foreach (var m in chat) AddBubble(m); }
        }

        private void RenderSeats()
        {
            for (int i = seatsPanel.childCount - 1; i >= 1; i--) Destroy(seatsPanel.GetChild(i).gameObject);
            var v = view;
            const float pad = 24f, headH = 360f, rowH = 290f, gridTop = 22f;
            int count = v.Seats.Count, cols = 4, rows = Mathf.Max(1, (count + cols - 1) / cols);
            float total = pad + headH + gridTop + rows * rowH + pad;
            seatsLayout.preferredHeight = total;

            // the big seat at the top
            var head = SeatObject(seatsPanel, v.Head, 1.42f, true, v.HeadLabel);
            head.anchorMin = head.anchorMax = new Vector2(0.5f, 1f); head.pivot = new Vector2(0.5f, 1f);
            head.anchoredPosition = new Vector2(0, -pad); head.sizeDelta = new Vector2(420, headH);
            KitTap.On(head, () => OnSeat?.Invoke(-1));

            float usable = W - 34 * 2 - 2 * pad, cell = usable / cols;
            for (int i = 0; i < count; i++)
            {
                int index = i, r = i / cols, c = i % cols;
                var seat = SeatObject(seatsPanel, v.Seats[i], 1f, false, "");
                seat.anchorMin = seat.anchorMax = new Vector2(0.5f, 1f); seat.pivot = new Vector2(0.5f, 1f);
                float x = ((cols - 1) / 2f - c) * cell;          // the first seat is at the right (Arabic); English mirrors the whole screen
                seat.anchoredPosition = new Vector2(x, -(pad + headH + gridTop + r * rowH)); seat.sizeDelta = new Vector2(cell, rowH - 10);
                KitTap.On(seat, () => OnSeat?.Invoke(index));
            }
        }

        /// <summary>One seat: glow when talking, a ring, the player's silhouette, the name under it and small badges.</summary>
        private RectTransform SeatObject(Transform parent, SeatVm s, float scale, bool head, string headLabel)
        {
            var go = Rect(parent, "Seat", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            float d = 150f * scale;
            var disc = new Vector2(0, -(d / 2f + 8f));
            Color ring = s.Kind == SeatKind.Taken ? (head || s.Staff ? th.brass : s.Me ? new Color32(0xE0, 0x52, 0x5A, 255) : new Color32(0x6B, 0x5A, 0x3F, 255)) : new Color32(0x5A, 0x4A, 0x36, 255);
            if (s.Kind == SeatKind.Taken && s.Talking) ring = th.teal;

            if (s.Kind == SeatKind.Taken && s.Talking)
            {
                var glow = Img(go, "Glow", th.dotWhite, new Color(th.teal.r, th.teal.g, th.teal.b, 0.35f), false); AnchorTop(glow.rectTransform, disc, d + 70f);
            }
            if (s.Kind == SeatKind.Taken)
            {
                var r = Img(go, "Ring", th.dotWhite, ring, false); AnchorTop(r.rectTransform, disc, d + 12f);
                var f = Img(go, "Disc", th.dotWhite, new Color32(0x1A, 0x15, 0x12, 255), false); AnchorTop(f.rectTransform, disc, d);
                var sil = Img(go, "Silhouette", th.fedSil, head || s.Staff ? new Color32(0xD9, 0xC4, 0x8A, 255) : new Color32(0x9A, 0x8E, 0x78, 255), false); AnchorTop(sil.rectTransform, disc, d * 0.72f);
                if (s.Muted)
                {
                    var mk = Img(go, "Mute", th.dotWhite, new Color32(0x26, 0x20, 0x1E, 255), false); AnchorTop(mk.rectTransform, disc + new Vector2(-d * 0.38f, -d * 0.36f), 54f);
                    var mi = Img(go, "MuteIc", th.iconMicOff, new Color32(0xE6, 0xB3, 0xB6, 255), false); AnchorTop(mi.rectTransform, disc + new Vector2(-d * 0.38f, -d * 0.36f), 34f);
                }
                if (head) { var cr = Img(go, "Crown", th.iconCrown, th.brassLight, false); AnchorTop(cr.rectTransform, disc + new Vector2(0, d * 0.6f), 60f); }
                string tag = s.Me ? "أنت" : s.Staff && !head ? "أدمن" : s.IsNew ? "جديد" : s.Tag;
                if (!string.IsNullOrEmpty(tag)) Chip(go, tag, disc + new Vector2(d * 0.42f, d * 0.4f), s.Me ? new Color32(0xE0, 0x52, 0x5A, 255) : s.IsNew && !s.Staff ? th.teal : th.brass, s.Me ? Color.white : th.brassText);
                var n = Txt(go, "Name", KitText.Clip(s.Name, head ? 14 : 9), th.fontBody, head ? 48 : 38, th.ivory, TextAlignmentOptions.Top);
                var nr = n.Rt(); nr.anchorMin = new Vector2(0, 1); nr.anchorMax = new Vector2(1, 1); nr.pivot = new Vector2(0.5f, 1); nr.sizeDelta = new Vector2(0, 60); nr.anchoredPosition = new Vector2(0, -(d + 20f));
            }
            else
            {
                var ring2 = Img(go, "Dashed", th.seatDashed, new Color32(0x6F, 0x60, 0x4A, 255), false); AnchorTop(ring2.rectTransform, disc, d + 4f);
                var inner = s.Kind == SeatKind.Locked ? Img(go, "Lock", th.iconLock, new Color32(0x6F, 0x60, 0x4A, 255), false) : null;
                if (inner != null) AnchorTop(inner.rectTransform, disc, d * 0.34f);
                else { var plus = Txt(go, "Plus", "+", th.fontLalezar, 72 * scale, new Color32(0x6F, 0x60, 0x4A, 255), TextAlignmentOptions.Midline); AnchorTop(plus.Rt(), disc, d); }
                var n = Txt(go, "Name", s.Kind == SeatKind.Locked ? "مقفل" : head ? headLabel : "فاضي", th.fontBody, 36, new Color32(0x6F, 0x67, 0x5A, 255), TextAlignmentOptions.Top);
                var nr = n.Rt(); nr.anchorMin = new Vector2(0, 1); nr.anchorMax = new Vector2(1, 1); nr.pivot = new Vector2(0.5f, 1); nr.sizeDelta = new Vector2(0, 60); nr.anchoredPosition = new Vector2(0, -(d + 20f));
            }
            if (head && s.Kind == SeatKind.Taken && !string.IsNullOrEmpty(headLabel))
            {
                var lab = Img(go, "HeadLabel", th.brassPlate, Color.white, true);
                var lr = lab.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 1f); lr.pivot = new Vector2(0.5f, 1f); lr.anchoredPosition = new Vector2(0, -(d + 74f)); lr.sizeDelta = new Vector2(170, 46);
                var lt = Txt(lab.transform, "T", headLabel, th.fontLalezar, 34, th.brassText, TextAlignmentOptions.Midline); Anchor(lt.Rt(), Vector2.zero, Vector2.one);
            }
            return go;
        }

        private void Chip(Transform parent, string text, Vector2 pos, Color bg, Color fg)
        {
            var c = Img(parent, "Chip", th.dotWhite, bg, false);
            var r = c.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = new Vector2(96, 40);
            var t = Txt(c.transform, "T", text, th.fontLalezar, 30, fg, TextAlignmentOptions.Midline); Anchor(t.Rt(), Vector2.zero, Vector2.one);
        }

        private void AddBubble(ChatVm m)
        {
            if (m.System)
            {
                var s = Txt(chatContent, "System", m.Text, th.fontBody, 34, th.ivoryDim, TextAlignmentOptions.Midline);
                var le = s.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 56;
                ScrollDown();
                return;
            }
            var row = Rect(chatContent, "Msg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = true; rowLayout.childControlHeight = true; rowLayout.childForceExpandWidth = false; rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = m.Mine ? TextAnchor.UpperLeft : TextAnchor.UpperRight;     // other people's lines sit at the right (Arabic), mine at the left

            var bubble = Img(row, "Bubble", m.Mine ? th.leatherRed : th.leatherDark, m.Pinned ? new Color32(0xC9, 0x9A, 0x3C, 255) : m.Staff ? new Color(0.85f, 0.7f, 0.4f, 1f) : Color.white, true);
            var v = bubble.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(30, 30, 18, 20); v.spacing = 6; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var bl = bubble.gameObject.AddComponent<LayoutElement>(); bl.preferredWidth = 800;

            if (!m.Mine || m.Pinned)
            {
                var head = Rect(bubble.transform, "Head", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var hl = head.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 12; hl.childAlignment = TextAnchor.MiddleRight;
                hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false; hl.reverseArrangement = false;
                head.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
                var nm = Txt(head, "Name", m.Name, th.fontBody, 38, m.Staff ? th.brassLight : th.ivory, TextAlignmentOptions.MidlineRight);
                nm.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                if (!string.IsNullOrEmpty(m.Tag)) { var tg = Txt(head, "Tag", m.Tag, th.fontLalezar, 30, th.brassText, TextAlignmentOptions.Midline); var tl = tg.gameObject.AddComponent<LayoutElement>(); tl.preferredWidth = 96; tl.preferredHeight = 40; var tb = Img(tg.transform, "Bg", th.dotWhite, th.brass, false); tb.transform.SetAsFirstSibling(); Anchor(tb.rectTransform, Vector2.zero, Vector2.one); }
                if (!string.IsNullOrEmpty(m.Time)) { var tm = Txt(head, "Time", m.Time, th.fontBody, 28, th.ivoryDim, TextAlignmentOptions.MidlineLeft); var tl = tm.gameObject.AddComponent<LayoutElement>(); tl.preferredWidth = 90; }
            }
            var body = Txt(bubble.transform, "Text", m.Text, th.fontBody, 44, m.Pinned ? new Color32(0x2A, 0x1A, 0x08, 255) : th.ivory, TextAlignmentOptions.TopRight);
            // a short line gets a narrow bubble: measure it on one line, then let it wrap inside the bubble
            var measure = body.GetComponent<TMP_Text>(); measure.ForceMeshUpdate();
            bl.preferredWidth = Mathf.Clamp(measure.preferredWidth + 70f, 380f, 800f);
            body.Wrap = true;
            body.gameObject.AddComponent<LayoutElement>().minHeight = 60;
            var vm = m;
            KitTap.On(bubble.transform, () => OnMessage?.Invoke(vm));
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            ScrollDown();
        }

        private void ScrollDown()
        {
            if (!stickToBottom && chatContent.childCount > 1) return;
            Canvas.ForceUpdateCanvases();
            chatScroll.verticalNormalizedPosition = 0f;
        }

        // ---- small helpers -------------------------------------------------------------------------------------------

        private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform; r.SetParent(parent, false);
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = offMin; r.offsetMax = offMax;
            return r;
        }

        private static void Fixed(RectTransform r, float h) { var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = h; le.minHeight = h; }
        private static void Anchor(RectTransform r, Vector2 min, Vector2 max) { r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static void Place(RectTransform r, Vector2 pos, Vector2 size) { r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = size; }
        private static void AnchorTop(RectTransform r, Vector2 pos, float size) { r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = new Vector2(size, size); }

        private static Image Img(Transform parent, string name, Sprite sprite, Color color, bool sliced, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = sprite; i.color = color; i.raycastTarget = false;
            if (sliced && sprite != null) { i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = 1.4f; }
            if (stretch) Anchor(i.rectTransform, Vector2.zero, Vector2.one);
            return i;
        }

        private RectTransform Circle(Transform parent, string name, Vector2 anchor, Vector2 pos, float size, Color fill, Color rim)
        {
            var r = Img(parent, name, th.dotWhite, rim, false);
            var rr = r.rectTransform; rr.anchorMin = rr.anchorMax = anchor; rr.pivot = new Vector2(0.5f, 0.5f); rr.anchoredPosition = pos; rr.sizeDelta = new Vector2(size, size);
            var f = Img(r.transform, "Fill", th.dotWhite, fill, false); Anchor(f.rectTransform, Vector2.zero, Vector2.one); f.rectTransform.offsetMin = new Vector2(4, 4); f.rectTransform.offsetMax = new Vector2(-4, -4);
            return rr;
        }

        private RectTransform RoundButton(Transform parent, string name, float size, Color color)
        {
            var b = Img(parent, name, th.dotWhite, color, false);
            var le = b.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = size; le.preferredHeight = size; le.minWidth = size; le.minHeight = size;
            return b.rectTransform;
        }

        private static ArabicText Txt(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false; t.overflowMode = TextOverflowModes.Overflow;
            var a = go.AddComponent<ArabicText>(); a.ArabicDigits = true; a.Text = text;
            return a;
        }
    }
}
