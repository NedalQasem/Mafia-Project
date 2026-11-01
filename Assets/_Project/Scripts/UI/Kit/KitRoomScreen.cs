using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The voice-room screen of every room (Docs/mockups/office-room.html, section 2): name, ID and member count on top; under them the mic seats
    /// (a big seat for the host or admin and a few ordinary ones); a strip with the raised hands; everything below is the room's chat; the bottom bar has
    /// the message field, the hand / mic button and the small buttons. Everything is drawn in code with <see cref="KitProc"/> and <see cref="KitSvg"/> at the mockup's
    /// sizes (its CSS pixels times <c>K</c>), so the screen is the HTML page. The app layer owns the state and calls <see cref="Show"/> again whenever it changes;
    /// this class only draws and reports taps.
    /// </summary>
    internal static class RoomTextExt { public static RectTransform Rt(this ArabicText a) => (RectTransform)a.transform; }

    /// <summary>Repaints a plate to the size its rectangle has (panels whose height depends on their content).</summary>
    [ExecuteAlways]
    internal sealed class KitPlate : MonoBehaviour
    {
        public Func<float, float, KitProc.Spec> Maker;
        public string Key = "";
        private Image img;
        private Vector2 painted;

        private void Awake() { img = GetComponent<Image>(); }
        private void OnEnable() { Repaint(); }
        public void Reset2() { painted = Vector2.zero; Repaint(); }
        private void LateUpdate() { Repaint(); }
        private void OnRectTransformDimensionsChange() { if (isActiveAndEnabled) Repaint(); }

        private void Repaint()
        {
            if (img == null) img = GetComponent<Image>();
            var r = ((RectTransform)transform).rect;
            float w = Mathf.Round(r.width / 4f) * 4f, h = Mathf.Round(r.height / 4f) * 4f;
            if (w < 8f || h < 8f || (painted.x == w && painted.y == h) || Maker == null) return;
            painted = new Vector2(w, h);
            img.sprite = KitProc.Paint(Key + Mathf.RoundToInt(w) + "x" + Mathf.RoundToInt(h), Maker(w, h));
        }
    }

    public sealed class KitRoomScreen : MonoBehaviour
    {
        // ---- what the app hands over -------------------------------------------------------------------------------

        public enum SeatKind { Empty, Locked, Taken }
        public enum MicButton { Sit, Hand, HandUp, Mic, Hidden }

        public sealed class SeatVm
        {
            public string Id = "", Name = "", Tag = "", Title = "";
            public SeatKind Kind = SeatKind.Empty;
            public bool Talking, Muted, Me, Staff, IsNew;
            /// <summary>What the player wears from the store: the ring around his picture and the title under his name.</summary>
            public Sprite Frame;
        }

        public sealed class ChatVm
        {
            public string Id = "", Name = "", Tag = "", Text = "", Time = "";
            public bool Mine, System, Pinned, Staff;
            /// <summary>The store bubble the sender wears (0 = the plain leather one).</summary>
            public int Bubble;
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
            /// <summary>The player sits on a seat but his microphone is closed.</summary>
            public bool MicOff;
            public bool CanType = true;
            /// <summary>The big red button of a match room (the host starts the match); empty hides it.</summary>
            public string StartLabel = "";
            public bool StartEnabled;
            public bool CanLeaveSeat;
            /// <summary>The host's store room background and the player's own chat background (colour washes; clear = none).</summary>
            public Color RoomTint = Color.clear, ChatTint = Color.clear;
        }

        // ---- what the screen reports -------------------------------------------------------------------------------

        public event Action OnBack, OnMembers, OnMic, OnLeaveSeat, OnStart, OnGames, OnTitle;
        /// <summary>The speaker button: true = hear the room, false = silence it on this device.</summary>
        public event Action<bool> OnSpeaker;
        /// <summary>A seat was tapped: its index, or -1 for the big seat.</summary>
        public event Action<int> OnSeat;
        public event Action<string> OnSend;
        /// <summary>A chat message was tapped (the sender's id; for the moderation sheet).</summary>
        public event Action<ChatVm> OnMessage;

        private const float W = 1080f;
        private const float K = 2.765f;                       // the mockup's phone is 366 CSS pixels wide inside its padding
        private static readonly Color Backdrop = new Color32(0x0D, 0x09, 0x07, 255);
        private static readonly Color Gold = new Color32(0xF5, 0xE2, 0xA8, 255), Brass = new Color32(0xB9, 0x90, 0x48, 255), Teal = new Color32(0x4F, 0xA5, 0x8B, 255),
            Soft = new Color32(0xCF, 0xC6, 0xB2, 255), Dim = new Color32(0x8F, 0x87, 0x77, 255), Faint = new Color32(0x6F, 0x67, 0x5A, 255), Red = new Color32(0xE0, 0x52, 0x5A, 255);

        private KitTheme th;
        private RectTransform root, seatsPanel, chatContent, stripBox;
        private ScrollRect chatScroll;
        private ArabicText nameText, subText, countText, stripText, stripSmall, startLabel;
        private Image micImage, micIcon, startImage, volIcon, badgeBg;
        private ArabicText badgeText;
        private GameObject startBox, leaveBox, micBox, stripGo, sendBox, badgeGo;
        private KitArabicInput field;
        private LayoutElement seatsLayout;
        private readonly List<ChatVm> chat = new List<ChatVm>();
        private ViewVm view;
        private bool stickToBottom = true, speakerOn = true;
        private Image backdrop, chatBg;
        private Color chatTint = Color.clear;

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

        public void ClearChat() { chat.Clear(); DropBubbles(); }

        // the lines on screen are counted here, not by the children of the list: Destroy() is not instant, and counting the children twice in a frame drew every line again
        private readonly List<GameObject> bubbles = new List<GameObject>();

        private void DropBubbles()
        {
            foreach (var b in bubbles) if (b != null) { b.SetActive(false); Destroy(b); }
            bubbles.Clear();
        }

        public void AddChat(ChatVm m)
        {
            chat.Add(m);
            if (chat.Count > 120) { chat.RemoveAt(0); if (bubbles.Count > 0) { var first = bubbles[0]; bubbles.RemoveAt(0); if (first != null) { first.SetActive(false); Destroy(first); } } }
            if (gameObject.activeSelf) AddBubble(m);
        }

        public void FocusField() { if (field != null) field.Focus(); }

        private void Update()
        {
            if (sendBox != null && field != null) { bool has = field.Text.Trim().Length > 0; if (sendBox.activeSelf != has) sendBox.SetActive(has); }
        }

        // ---- building ------------------------------------------------------------------------------------------------

        private void Build()
        {
            th = KitTheme.Current;
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            backdrop = Img(root, "Backdrop", null, Backdrop, false, true);
            var safe = Rect(root, "Safe", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<KitSafeArea>();

            var col = safe.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(34, 34, 24, 24); col.spacing = Mathf.RoundToInt(8 * K);
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
            Fixed(top, 44 * K);
            float back = 34 * K;

            // back: a round button at the right
            var b = Img(top, "Back", Plate("back", back, back, back * 0.5f, new Color(0, 0, 0, KitProc.LinA(0.53f)), Color.clear, new Color32(0x6B, 0x5A, 0x3F, 255), 1.5f * K), Color.white, false);
            var br = b.rectTransform; br.anchorMin = br.anchorMax = new Vector2(1, 0.5f); br.pivot = new Vector2(1, 0.5f); br.anchoredPosition = Vector2.zero; br.sizeDelta = new Vector2(back, back);
            var arrow = Img(b.transform, "Arrow", KitSvg.Icon(KitSvg.Arrow, 2.2f), Soft, false); Place(arrow.rectTransform, Vector2.zero, new Vector2(18 * K, 18 * K));
            KitTap.On(b.transform, () => OnBack?.Invoke());

            // members pill: at the left
            var pill = Img(top, "Members", null, Color.white, false);
            var pr = pill.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(0, 0.5f); pr.pivot = new Vector2(0, 0.5f); pr.anchoredPosition = Vector2.zero;
            var ic = Img(pill.transform, "Icon", KitSvg.Icon(KitSvg.Users, 2f), Gold, false);
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(1, 0.5f); ic.rectTransform.pivot = new Vector2(1, 0.5f); ic.rectTransform.anchoredPosition = new Vector2(-12 * K, 0); ic.rectTransform.sizeDelta = new Vector2(18 * K, 18 * K);
            countText = Txt(pill.transform, "Count", "", th.fontLalezar, 16.8f * K, Gold, TextAlignmentOptions.MidlineRight);
            var cr = countText.Rt(); cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = new Vector2(10 * K, 0); cr.offsetMax = new Vector2(-(12 * K + 18 * K + 5 * K), 0);
            pillImage = pill;
            KitTap.On(pill.transform, () => OnMembers?.Invoke());

            var titleBox = Rect(top, "Title", new Vector2(0, 0), new Vector2(1, 1), new Vector2(150 * K, 0), new Vector2(-(back + 10 * K), 0));
            nameText = Txt(titleBox, "Name", "", th.fontLalezar, 20.8f * K, th.ivory, TextAlignmentOptions.BottomRight);
            Anchor(nameText.Rt(), new Vector2(0, 0.38f), new Vector2(1, 1));
            subText = Txt(titleBox, "Sub", "", th.fontBody, 11.5f * K, new Color32(0x8D, 0x74, 0x4A, 255), TextAlignmentOptions.TopRight);
            subText.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            var sr = subText.Rt(); sr.anchorMin = new Vector2(0, 0); sr.anchorMax = new Vector2(1, 0.4f); sr.offsetMin = sr.offsetMax = Vector2.zero;
            // the "official" / "match" tag next to the ID
            var tag = Img(titleBox, "Badge", null, Color.white, false);
            badgeBg = tag; badgeGo = tag.gameObject;
            var tr = tag.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(1, 0); tr.pivot = new Vector2(1, 0);
            badgeText = Txt(tag.transform, "T", "", th.fontLalezar, 10 * K, new Color32(0x2A, 0x1A, 0x08, 255), TextAlignmentOptions.Midline); Anchor(badgeText.Rt(), Vector2.zero, Vector2.one);
            KitTap.On(titleBox, () => OnTitle?.Invoke());
        }

        private Image pillImage, chatBgImage;

        private void BuildSeatsPanel(Transform parent)
        {
            seatsPanel = Rect(parent, "Seats", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            seatsLayout = seatsPanel.gameObject.AddComponent<LayoutElement>(); seatsLayout.preferredHeight = 800;
            var bg = Img(seatsPanel, "Bg", null, Color.white, false, true);
            var plate = bg.gameObject.AddComponent<KitPlate>();
            plate.Key = "seats";
            plate.Maker = (w, h) => new KitProc.Spec
            {
                W = w, H = h, Radius = 14 * K, Top = new Color32(0x12, 0x0D, 0x0A, 255), Bottom = new Color32(0x12, 0x0D, 0x0A, 255),
                Glow = new Color32(0xE8, 0xC2, 0x7A, 0x24), GlowAt = new Vector2(0.5f, 0f), GlowReach = 0.7f, Edge = new Color32(0x2A, 0x20, 0x18, 255), EdgePx = 1f * K,
            };
        }

        private void BuildStrip(Transform parent)
        {
            stripBox = Rect(parent, "Strip", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            stripGo = stripBox.gameObject; Fixed(stripBox, 36 * K);
            var bg = Img(stripBox, "Bg", Sliced("strip", 9 * K, new Color(0, 0, 0, KitProc.LinA(0.53f)), new Color32(0xE8, 0xC2, 0x7A, 0x55), 1f * K), Color.white, true, true);
            stripText = Txt(stripBox, "Text", "", th.fontBody, 13f * K, Gold, TextAlignmentOptions.MidlineRight);
            stripText.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            Anchor(stripText.Rt(), Vector2.zero, Vector2.one); stripText.Rt().offsetMin = new Vector2(12 * K, 0); stripText.Rt().offsetMax = new Vector2(-12 * K, 0);
            stripSmall = Txt(stripBox, "Small", "", th.fontBody, 11.2f * K, Dim, TextAlignmentOptions.MidlineLeft);
            stripSmall.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            Anchor(stripSmall.Rt(), Vector2.zero, Vector2.one); stripSmall.Rt().offsetMin = new Vector2(12 * K, 0); stripSmall.Rt().offsetMax = new Vector2(-12 * K, 0);
        }

        private void BuildChat(Transform parent)
        {
            var area = Rect(parent, "Chat", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var le = area.gameObject.AddComponent<LayoutElement>(); le.flexibleHeight = 1; le.minHeight = 200;
            var bg = Img(area, "Bg", null, Color.white, false, true);
            var plate = bg.gameObject.AddComponent<KitPlate>();
            plate.Key = "chat";
            plate.Maker = (w, h) => { var c = Color.Lerp(new Color32(0x17, 0x11, 0x0D, 255), chatTint.a > 0f ? chatTint : new Color32(0x17, 0x11, 0x0D, 255), 0.3f); c.a = 1f; return new KitProc.Spec { W = w, H = h, Radius = 12 * K, Top = c, Bottom = c, Edge = new Color32(0x2A, 0x20, 0x18, 255), EdgePx = 1f * K }; };
            plate.Key = "chat" + "_";
            chatBgImage = bg;

            var viewport = Rect(area, "Viewport", Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            viewport.gameObject.AddComponent<RectMask2D>();
            chatContent = Rect(viewport, "Content", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, Vector2.zero);
            chatContent.pivot = new Vector2(0.5f, 0);
            var v = chatContent.gameObject.AddComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(10 * K);
            v.padding = new RectOffset(pad, pad, pad, pad); v.spacing = Mathf.RoundToInt(7 * K); v.childAlignment = TextAnchor.LowerCenter;
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
            Fixed((RectTransform)startBox.transform, 50 * K);
            startImage = Img(startBox.transform, "Bg", null, Color.white, false, true);
            float sm = 4 * K;
            startImage.rectTransform.offsetMin = new Vector2(-sm, -sm); startImage.rectTransform.offsetMax = new Vector2(sm, sm);
            var sp = startImage.gameObject.AddComponent<KitPlate>();
            sp.Key = "start";
            sp.Maker = (w, hh) => new KitProc.Spec
            {
                W = w - 2 * sm, H = hh - 2 * sm, Radius = 9 * K, Margin = sm, Top = new Color32(0xA2, 0x26, 0x2F, 255), Mid = new Color32(0x8A, 0x1C, 0x24, 255), Bottom = new Color32(0x56, 0x10, 0x16, 255), MidAt = 0.55f,
                TopLight = new Color(1, 1, 1, 0.13f), TopLightPx = 1f * K, Shadow = Color.black, ShadowDy = 3 * K, ShadowBlur = 1.2f,
            };
            startLabel = Txt(startBox.transform, "Label", "", th.fontLalezar, 23 * K, new Color32(0xF0, 0xDC, 0xD8, 255), TextAlignmentOptions.Midline); Anchor(startLabel.Rt(), Vector2.zero, Vector2.one);
            KitTap.On(startBox.transform, () => { if (view != null && view.StartEnabled) OnStart?.Invoke(); });
        }

        private void BuildBottom(Transform parent)
        {
            float ib = 40 * K, pad = 8 * K;
            var bottom = Rect(parent, "Bar", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Fixed(bottom, ib + 2 * pad);
            var bg = Img(bottom, "Bg", null, Color.white, false, true);
            var plate = bg.gameObject.AddComponent<KitPlate>();
            plate.Key = "bar";
            plate.Maker = (w, h) => new KitProc.Spec
            {
                W = w, H = h, Radius = 14 * K, Top = new Color32(0x3B, 0x2A, 0x20, 255), Bottom = new Color32(0x1A, 0x11, 0x0D, 255), TopLight = new Color(1, 1, 1, 0.11f), TopLightPx = 1f * K,
                Edge = Color.black, EdgePx = 1f * K,
            };
            var row = Rect(bottom, "Row", Vector2.zero, Vector2.one, new Vector2(pad, pad), new Vector2(-pad, -pad));
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 7 * K; h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = true;

            // left to right: send (only with text), games, speaker, leave the seat, hand / mic, emoji, the message field (the field is at the right, as in the mockup)
            sendBox = RoundButton(row, "Send", ib, "send", Spec3(new Color32(0xA2, 0x26, 0x2F, 255), new Color32(0x8A, 0x1C, 0x24, 255), new Color32(0x56, 0x10, 0x16, 255)), KitSvg.Send, 2.2f, new Color32(0xF0, 0xDC, 0xD8, 255)).gameObject;
            KitTap.On(sendBox.transform, () => Send(field.Text));
            sendBox.SetActive(false);

            var games = RoundButton(row, "Games", ib, "ring", RingSpec(), KitSvg.Grid, 2f, Soft);
            var soon = Txt(games, "Soon", "قريباً", th.fontLalezar, 8 * K, new Color32(0x06, 0x23, 0x1A, 255), TextAlignmentOptions.Midline);
            var sb = Img(games, "SoonBg", Sliced("soon", 3 * K, new Color32(0x2C, 0x8D, 0x73, 255), Color.clear, 0), Color.white, true);
            sb.rectTransform.anchorMin = sb.rectTransform.anchorMax = new Vector2(0.5f, 1f); sb.rectTransform.sizeDelta = new Vector2(34 * K, 11 * K); sb.rectTransform.anchoredPosition = new Vector2(0, 2 * K);
            soon.Rt().SetParent(sb.rectTransform, false); Anchor(soon.Rt(), Vector2.zero, Vector2.one);
            KitTap.On(games, () => OnGames?.Invoke());

            var vol = RoundButton(row, "Speaker", ib, "ring", RingSpec(), KitSvg.Volume, 2f, Soft);
            volIcon = vol.Find("Icon").GetComponent<Image>();
            KitTap.On(vol, () => { speakerOn = !speakerOn; volIcon.sprite = KitSvg.Icon(speakerOn ? KitSvg.Volume : KitSvg.VolumeOff, 2f); OnSpeaker?.Invoke(speakerOn); });

            leaveBox = RoundButton(row, "Leave", ib, "off", Spec3(new Color32(0xA2, 0x26, 0x2F, 255), new Color32(0x8A, 0x1C, 0x24, 255), new Color32(0x56, 0x10, 0x16, 255)), KitSvg.Eject, 2f, new Color32(0xF0, 0xDC, 0xD8, 255)).gameObject;
            KitTap.On(leaveBox.transform, () => OnLeaveSeat?.Invoke());

            var mic = RoundButton(row, "Mic", ib, "mic", Spec3(new Color32(0xF0, 0xD7, 0x8F, 255), new Color32(0xC9, 0x9A, 0x3C, 255), new Color32(0x7D, 0x5A, 0x1C, 255)), KitSvg.Hand, 2f, new Color32(0x2A, 0x1A, 0x08, 255));
            micBox = mic.gameObject; micImage = mic.Find("Face").GetComponent<Image>(); micIcon = mic.Find("Icon").GetComponent<Image>();
            KitTap.On(mic, () => OnMic?.Invoke());

            var smile = RoundButton(row, "Emoji", ib, "ring", RingSpec(), KitSvg.Smile, 2f, Soft);
            KitTap.On(smile, () => OnGames?.Invoke());

            var input = Img(row, "Field", Sliced("input", 9 * K, new Color32(0x0B, 0x08, 0x07, 255), new Color32(0x3A, 0x2F, 0x24, 255), 1.5f * K), Color.white, true);
            var inLe = input.gameObject.AddComponent<LayoutElement>(); inLe.flexibleWidth = 1; inLe.minWidth = 200;
            var ph = Txt(input.transform, "Placeholder", "اكتب رسالتك…", th.fontBody, 13.6f * K, Faint, TextAlignmentOptions.MidlineRight);
            ph.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            Anchor(ph.Rt(), Vector2.zero, Vector2.one); ph.Rt().offsetMin = new Vector2(12 * K, 0); ph.Rt().offsetMax = new Vector2(-12 * K, 0);
            var mirror = Txt(input.transform, "Mirror", "", th.fontBody, 13.6f * K, th.ivory, TextAlignmentOptions.MidlineRight);
            Anchor(mirror.Rt(), Vector2.zero, Vector2.one); mirror.Rt().offsetMin = new Vector2(12 * K, 0); mirror.Rt().offsetMax = new Vector2(-12 * K, 0);
            field = KitArabicInput.Attach(input.rectTransform, mirror, ph.gameObject, TMP_InputField.ContentType.Standard, 160);
            field.OnSubmit += Send;
            // the row is built left to right: send, games, speaker, leave, hand / mic, emoji, field
            sendBox.transform.SetSiblingIndex(0); games.SetSiblingIndex(1); vol.SetSiblingIndex(2); leaveBox.transform.SetSiblingIndex(3); micBox.transform.SetSiblingIndex(4); smile.SetSiblingIndex(5); input.transform.SetSiblingIndex(6);
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
            KitText.Fit(nameText, v.Name, 30, 20.8f * K * 0.55f);
            backdrop.color = v.RoomTint.a > 0f ? Color.Lerp(Backdrop, v.RoomTint, 0.22f) : Backdrop;
            if (chatTint != v.ChatTint) { chatTint = v.ChatTint; var cp = chatBgImage.GetComponent<KitPlate>(); cp.Key = "chat" + ColorUtility.ToHtmlStringRGBA(chatTint); cp.Reset2(); }
            subText.Text = "ID " + v.RoomId;
            countText.Text = CoinPill.Format(v.Members);
            PlacePill();
            PlaceBadge(v.Badge);

            RenderSeats();

            bool strip = !string.IsNullOrEmpty(v.Strip);
            stripGo.SetActive(strip);
            if (strip) { stripText.Text = v.Strip; stripSmall.Text = v.StripSmall; }

            micBox.SetActive(v.Mic != MicButton.Hidden);
            if (v.Mic != MicButton.Hidden)
            {
                var mic = v.Mic == MicButton.Mic;
                if (mic && v.MicOff) { SetFace(micImage, ButtonSprite("off", Spec3(new Color32(0xA2, 0x26, 0x2F, 255), new Color32(0x8A, 0x1C, 0x24, 255), new Color32(0x56, 0x10, 0x16, 255)))); micIcon.sprite = KitSvg.Icon(KitSvg.MicOff, 2f); micIcon.color = new Color32(0xF0, 0xDC, 0xD8, 255); }
                else if (mic) { SetFace(micImage, ButtonSprite("mic2", GreenSpec())); micIcon.sprite = KitSvg.Icon(KitSvg.Mic, 2f); micIcon.color = new Color32(0x06, 0x23, 0x1A, 255); }
                else
                {
                    SetFace(micImage, ButtonSprite(v.Mic == MicButton.HandUp ? "handup" : "mic", Spec3(new Color32(0xF0, 0xD7, 0x8F, 255), new Color32(0xC9, 0x9A, 0x3C, 255), new Color32(0x7D, 0x5A, 0x1C, 255))));
                    micIcon.sprite = KitSvg.Icon(v.Mic == MicButton.Sit ? KitSvg.Mic : KitSvg.Hand, 2f); micIcon.color = new Color32(0x2A, 0x1A, 0x08, 255);
                }
            }
            leaveBox.SetActive(v.CanLeaveSeat);

            bool start = !string.IsNullOrEmpty(v.StartLabel);
            startBox.SetActive(start);
            if (start) { startLabel.Text = v.StartLabel; startImage.color = v.StartEnabled ? Color.white : new Color(0.55f, 0.5f, 0.5f, 1f); }
            field.gameObject.SetActive(true);

            // the chat is rebuilt after a closed and opened screen
            if (bubbles.Count != chat.Count) { DropBubbles(); foreach (var m in chat) AddBubble(m); }
        }

        private void PlacePill()
        {
            var t = countText.GetComponent<TMP_Text>(); t.ForceMeshUpdate();
            float w = t.preferredWidth + 10 * K + 12 * K + 18 * K + 5 * K, h = 30 * K;
            var r = pillImage.rectTransform; r.sizeDelta = new Vector2(w, h);
            pillImage.sprite = KitProc.Paint("pill" + Mathf.RoundToInt(w), new KitProc.Spec
            {
                W = w, H = h, Radius = 16 * K, Top = new Color(0, 0, 0, KitProc.LinA(0.6f)), Bottom = new Color(0, 0, 0, KitProc.LinA(0.6f)), Edge = new Color32(0x6B, 0x5A, 0x3F, 255), EdgePx = 1.5f * K,
            });
        }

        private void PlaceBadge(string text)
        {
            badgeGo.SetActive(!string.IsNullOrEmpty(text));
            if (string.IsNullOrEmpty(text)) return;
            badgeText.Text = text;
            var t = badgeText.GetComponent<TMP_Text>(); t.ForceMeshUpdate();
            float w = t.preferredWidth + 14 * K, h = 15 * K;
            var r = badgeBg.rectTransform; r.sizeDelta = new Vector2(w, h);
            // after "ID ١٠٠١": the ID text is on the right, the tag to its left
            var idText = subText.GetComponent<TMP_Text>(); idText.ForceMeshUpdate();
            r.anchoredPosition = new Vector2(-(idText.preferredWidth + 7 * K), 1 * K);
            badgeBg.sprite = KitProc.Paint("tagGold" + Mathf.RoundToInt(w) + "x" + Mathf.RoundToInt(h), TagSpec(w, h, false, false));
        }

        private static KitProc.Spec TagSpec(float w, float h, bool green, bool red)
        {
            var s = new KitProc.Spec { W = w, H = h, Radius = 3 * K };
            if (red) { s.Top = new Color32(0xE0, 0x52, 0x5A, 255); s.Bottom = new Color32(0x8A, 0x1C, 0x24, 255); }
            else if (green) { s.Top = new Color32(0x6F, 0xD4, 0xB3, 255); s.Bottom = new Color32(0x2C, 0x8D, 0x73, 255); }
            else { s.Top = new Color32(0xE8, 0xCC, 0x8C, 255); s.Bottom = new Color32(0x9A, 0x76, 0x32, 255); }
            return s;
        }

        private void RenderSeats()
        {
            for (int i = seatsPanel.childCount - 1; i >= 1; i--) Destroy(seatsPanel.GetChild(i).gameObject);
            var v = view;
            float padT = 10 * K, big = 76 * K, small = 56 * K;
            float nameH = 12 * K, labH = 14 * K, gap = 3 * K;
            float headH = big + gap + nameH + gap + labH;
            bool anyTitle = false; foreach (var st in v.Seats) if (st.Kind == SeatKind.Taken && !string.IsNullOrEmpty(st.Title)) anyTitle = true;
            float rowH = small + gap + nameH + 3 * K + (anyTitle ? 10 * K : 0f);
            float rowGap = 8 * K, adminMb = 8 * K, padB = 8 * K;
            int count = v.Seats.Count, cols = 4, rows = Mathf.Max(1, (count + cols - 1) / cols);
            float total = padT + headH + adminMb + rows * rowH + (rows - 1) * rowGap + padB;
            seatsLayout.preferredHeight = total;

            // the big seat at the top
            var head = SeatObject(seatsPanel, v.Head, big, true, v.HeadLabel);
            head.anchorMin = head.anchorMax = new Vector2(0.5f, 1f); head.pivot = new Vector2(0.5f, 1f);
            head.anchoredPosition = new Vector2(0, -padT); head.sizeDelta = new Vector2(300, headH);
            KitTap.On(head, () => OnSeat?.Invoke(-1));

            float usable = W - 34 * 2 - 2 * 6 * K, cell = usable / cols;
            for (int i = 0; i < count; i++)
            {
                int index = i, r = i / cols, c = i % cols;
                var seat = SeatObject(seatsPanel, v.Seats[i], small, false, "");
                seat.anchorMin = seat.anchorMax = new Vector2(0.5f, 1f); seat.pivot = new Vector2(0.5f, 1f);
                float x = ((cols - 1) / 2f - c) * cell;          // the first seat is at the right (Arabic); English mirrors the whole screen
                seat.anchoredPosition = new Vector2(x, -(padT + headH + adminMb + r * (rowH + rowGap))); seat.sizeDelta = new Vector2(cell, rowH);
                KitTap.On(seat, () => OnSeat?.Invoke(index));
            }
        }

        /// <summary>One seat: the round avatar in its ring (teal glow while talking), the name under it and small tags.</summary>
        private RectTransform SeatObject(Transform parent, SeatVm s, float d, bool head, string headLabel)
        {
            var go = Rect(parent, "Seat", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var disc = new Vector2(0, -(d / 2f));
            bool taken = s.Kind == SeatKind.Taken;
            bool staff = taken && (head || s.Staff);

            Sprite circle = taken ? KitProc.Paint("seat" + Mathf.RoundToInt(d) + (s.Talking ? "t" : "") + (s.Me ? "m" : "") + (staff ? "a" : "") + (head ? "h" : ""), SeatSpec(d, s, head, staff)) : KitProc.Paint("seatE" + Mathf.RoundToInt(d), EmptySpec(d));
            var ci = Img(go, "Circle", circle, Color.white, false);
            AnchorTop(ci.rectTransform, disc, d);
            ci.rectTransform.sizeDelta = new Vector2(circle.rect.width / KitProc.Q, circle.rect.height / KitProc.Q);       // the sprite has room around the circle for its glow

            if (taken)
            {
                var sil = Img(go, "Silhouette", th.fedSil, staff ? new Color32(0xD9, 0xC4, 0x8A, 255) : new Color32(0x9A, 0x8E, 0x78, 255), false); AnchorTop(sil.rectTransform, disc, d * (head ? 0.71f : 0.71f));
                if (s.Frame != null && !head)
                {
                    var fr = Img(go, "Frame", s.Frame, Color.white, false); AnchorTop(fr.rectTransform, disc, d * 1.3f);
                }
                if (s.Muted)
                {
                    float m = 20 * K;
                    var mk = Img(go, "Mute", KitProc.Paint("mk" + Mathf.RoundToInt(m), MuteSpec(m)), Color.white, false);
                    AnchorTop(mk.rectTransform, disc + new Vector2(-d * 0.5f + m * 0.35f, -d * 0.5f + m * 0.35f), m + 8 * K);
                    var mi = Img(go, "MuteIc", KitSvg.Icon(KitSvg.MicOff, 2.4f), new Color32(0xE6, 0xB3, 0xB6, 255), false); AnchorTop(mi.rectTransform, disc + new Vector2(-d * 0.5f + m * 0.35f, -d * 0.5f + m * 0.35f), 12 * K);
                }
                if (head) { var cr = Img(go, "Crown", th.iconCrown, Gold, false); AnchorTop(cr.rectTransform, disc + new Vector2(0, d * 0.5f + 0.5f * 22 * K - 3 * K), 22 * K); }
                string tag = s.Me ? (head ? "" : "أنت") : s.Staff && !head ? "أدمن" : s.IsNew ? "جديد" : s.Tag;
                if (!head && !string.IsNullOrEmpty(tag)) Chip(go, tag, disc + new Vector2(d * 0.5f - 2 * K, d * 0.5f - 2 * K), s.Me, s.IsNew && !s.Staff && !s.Me);
            }
            else
            {
                var icon = Img(go, "Icon", KitSvg.Icon(s.Kind == SeatKind.Locked ? KitSvg.Lock : KitSvg.Plus, 2f), new Color32(0x6F, 0x60, 0x4A, 255), false);
                AnchorTop(icon.rectTransform, disc, 22 * K);
            }
            string label = taken ? KitText.Clip(s.Name, head ? 14 : 9) : s.Kind == SeatKind.Locked ? "مقفل" : head ? headLabel : "فاضي";
            var n = Txt(go, "Name", label, th.fontBody, 11.3f * K, taken ? Soft : Faint, TextAlignmentOptions.Top);
            n.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            var nr = n.Rt(); nr.anchorMin = new Vector2(0, 1); nr.anchorMax = new Vector2(1, 1); nr.pivot = new Vector2(0.5f, 1); nr.sizeDelta = new Vector2(0, 14 * K); nr.anchoredPosition = new Vector2(0, -(d + 3 * K));
            if (taken && !head && !string.IsNullOrEmpty(s.Title))
            {
                var ti = Txt(go, "Title", KitText.Clip(s.Title, 14), th.fontBody, 9f * K, new Color32(0xB9, 0x90, 0x48, 255), TextAlignmentOptions.Top);
                var tr = ti.Rt(); tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(0.5f, 1); tr.sizeDelta = new Vector2(0, 11 * K); tr.anchoredPosition = new Vector2(0, -(d + 3 * K + 13 * K));
            }
            if (head && taken && !string.IsNullOrEmpty(headLabel))
            {
                var t = Txt(go, "HeadLabelText", headLabel, th.fontLalezar, 10.5f * K, new Color32(0x2A, 0x1A, 0x08, 255), TextAlignmentOptions.Midline);
                t.GetComponent<TMP_Text>().ForceMeshUpdate();
                float lw = t.GetComponent<TMP_Text>().preferredWidth + 16 * K, lh = 14 * K;
                var lab = Img(go, "HeadLabel", KitProc.Paint("tagGold" + Mathf.RoundToInt(lw) + "x" + Mathf.RoundToInt(lh), TagSpec(lw, lh, false, false)), Color.white, false);
                var lr = lab.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 1f); lr.pivot = new Vector2(0.5f, 1f); lr.anchoredPosition = new Vector2(0, -(d + 3 * K + 14 * K + 3 * K)); lr.sizeDelta = new Vector2(lw, lh);
                t.transform.SetParent(lab.transform, false); Anchor(t.Rt(), Vector2.zero, Vector2.one);
            }
            return go;
        }

        private static KitProc.Spec SeatSpec(float d, SeatVm s, bool head, bool staff)
        {
            var sp = new KitProc.Spec
            {
                W = d, H = d, Radius = d * 0.5f, Margin = 20 * K, Radial = true, RadialAt = new Vector2(0.5f, 0.3f), RBAt = 1f, RadialReach = 0.7f,
                RA = new Color32(0x2A, 0x26, 0x23, 255), RB = new Color32(0x0C, 0x0B, 0x0A, 255), RC = new Color32(0x0C, 0x0B, 0x0A, 255),
            };
            Color ring = new Color32(0x6B, 0x5A, 0x3F, 255); float ringW = 1f * K;
            if (staff) { ring = Brass; ringW = head ? 2f * K : 1f * K; }
            if (s.Talking) { ring = Teal; ringW = head ? 2f * K : 1f * K; sp.Shadow = new Color32(0x4F, 0xA5, 0x8B, 0x99); sp.ShadowBlur = 14 * K; }
            if (s.Me) { ring = Red; ringW = 1f * K; sp.Shadow = new Color32(0xE0, 0x52, 0x5A, 0x77); sp.ShadowBlur = 14 * K; }
            sp.Rings.Add(new KeyValuePair<Color, float>(Color.black, 2 * K));
            sp.Rings.Add(new KeyValuePair<Color, float>(ring, ringW));
            return sp;
        }

        private static KitProc.Spec EmptySpec(float d)
        {
            return new KitProc.Spec
            {
                W = d, H = d, Radius = d * 0.5f, Margin = 6 * K, Top = new Color32(0x0B, 0x08, 0x07, 255), Bottom = new Color32(0x0B, 0x08, 0x07, 255),
                Edge = new Color32(0x3A, 0x2F, 0x24, 255), EdgePx = 1.5f * K, Dash = new Color32(0x5A, 0x4A, 0x36, 255), DashInset = 0.75f * K, DashPx = 1.5f * K, DashLen = 4.5f * K, DashGap = 3.2f * K, DashRadius = d * 0.5f - 0.75f * K,
            };
        }

        private static KitProc.Spec MuteSpec(float m)
        {
            var s = new KitProc.Spec { W = m, H = m, Radius = m * 0.5f, Margin = 4 * K, Top = new Color32(0x26, 0x20, 0x1E, 255), Bottom = new Color32(0x26, 0x20, 0x1E, 255) };
            s.Rings.Add(new KeyValuePair<Color, float>(Color.black, 1.5f * K));
            return s;
        }

        private void Chip(Transform parent, string text, Vector2 pos, bool me, bool green)
        {
            var t = Txt(parent, "ChipText", text, th.fontLalezar, 9.3f * K, me ? Color.white : green ? new Color32(0x06, 0x23, 0x1A, 255) : new Color32(0x2A, 0x1A, 0x08, 255), TextAlignmentOptions.Midline);
            var tmp = t.GetComponent<TMP_Text>(); tmp.ForceMeshUpdate();
            float w = tmp.preferredWidth + 12 * K, h = 12.5f * K;
            var c = Img(parent, "Chip", KitProc.Paint("tg" + Mathf.RoundToInt(w) + (me ? "m" : green ? "g" : "a"), TagSpec(w, h, green, me)), Color.white, false);
            var r = c.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(1f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = new Vector2(w, h);
            t.transform.SetParent(c.transform, false); Anchor(t.Rt(), Vector2.zero, Vector2.one);
        }

        private void AddBubble(ChatVm m)
        {
            if (m.System)
            {
                var row0 = Rect(chatContent, "System", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                bubbles.Add(row0.gameObject);
                var sl = row0.gameObject.AddComponent<HorizontalLayoutGroup>(); sl.childAlignment = TextAnchor.MiddleCenter; sl.childControlWidth = true; sl.childControlHeight = true; sl.childForceExpandWidth = false; sl.childForceExpandHeight = false;
                var pill = Img(row0, "Pill", Sliced("sys", 9 * K, new Color(0, 0, 0, KitProc.LinA(0.4f)), Color.clear, 0), Color.white, true);
                var pl = pill.gameObject.AddComponent<VerticalLayoutGroup>(); pl.padding = new RectOffset(Mathf.RoundToInt(10 * K), Mathf.RoundToInt(10 * K), Mathf.RoundToInt(1 * K), Mathf.RoundToInt(2 * K)); pl.childControlWidth = true; pl.childControlHeight = true; pl.childForceExpandWidth = true; pl.childForceExpandHeight = false;
                var s = Txt(pill.transform, "Text", m.Text, th.fontBody, 10.6f * K, Faint, TextAlignmentOptions.Midline);
                s.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
                var le = s.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 14 * K;
                var tt = s.GetComponent<TMP_Text>(); tt.ForceMeshUpdate();
                pill.gameObject.AddComponent<LayoutElement>().preferredWidth = Mathf.Min(900, tt.preferredWidth + 22 * K);
                ScrollDown();
                return;
            }
            var row = Rect(chatContent, "Msg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bubbles.Add(row.gameObject);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = true; rowLayout.childControlHeight = true; rowLayout.childForceExpandWidth = false; rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = m.Pinned ? TextAnchor.UpperCenter : m.Mine ? TextAnchor.UpperLeft : TextAnchor.UpperRight;     // other people's lines sit at the right (Arabic), mine at the left

            Color bodyColor = new Color32(0xE4, 0xDB, 0xC7, 255), nameColor = m.Staff ? Gold : Soft;
            Sprite sprite;
            if (m.Pinned) sprite = Sliced("bpin", 10 * K, new Color32(0x33, 0x26, 0x1A, 255), Brass, 1.5f * K);
            else if (KitBubbleLooks.Has(m.Bubble))
            {
                var look = KitBubbleLooks.Of(m.Bubble);
                sprite = Sliced("bv" + m.Bubble, 10 * K, look.Fill, look.Edge, 1f * K); bodyColor = look.Text; nameColor = look.Name;
            }
            else sprite = m.Mine ? Sliced("bme", 10 * K, new Color32(0x4A, 0x13, 0x19, 255), new Color32(0x8A, 0x1C, 0x24, 0x88), 1f * K)
                : m.Staff ? Sliced("badm", 10 * K, new Color32(0x2B, 0x22, 0x14, 255), new Color32(0xB9, 0x90, 0x48, 0x66), 1f * K)
                : Sliced("bmsg", 10 * K, new Color32(0x24, 0x1B, 0x15, 255), new Color(1, 1, 1, 0.07f), 1f * K);
            var bubble = Img(row, "Bubble", sprite, Color.white, true);
            var v = bubble.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(Mathf.RoundToInt(10 * K), Mathf.RoundToInt(10 * K), Mathf.RoundToInt(6 * K), Mathf.RoundToInt(7 * K)); v.spacing = 2 * K;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var bl = bubble.gameObject.AddComponent<LayoutElement>(); bl.preferredWidth = 800; bl.flexibleWidth = 0;      // the time label inside is flexible: without this the bubble would grow to the full width

            // the sender line: name, tag, the pin note and the time
            var headRow = Rect(bubble.transform, "Head", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hl = headRow.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 6 * K; hl.childAlignment = TextAnchor.MiddleRight; hl.reverseArrangement = true;
            hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            headRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 16 * K;
            float headWidth = 0f;
            var nm = Txt(headRow, "Name", m.Name, th.fontBody, 11.5f * K, nameColor, TextAlignmentOptions.MidlineRight);
            nm.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            var nmt = nm.GetComponent<TMP_Text>(); nmt.ForceMeshUpdate();
            nm.gameObject.AddComponent<LayoutElement>().preferredWidth = Mathf.Min(nmt.preferredWidth + 2, 420); headWidth += Mathf.Min(nmt.preferredWidth + 2, 420);
            if (!string.IsNullOrEmpty(m.Tag))
            {
                bool newTag = m.Tag == "جديد" || m.Tag == "جديدة";
                var tg = Txt(headRow, "Tag", m.Tag, th.fontLalezar, 10.2f * K, newTag ? new Color32(0x06, 0x23, 0x1A, 255) : new Color32(0x2A, 0x1A, 0x08, 255), TextAlignmentOptions.Midline);
                var tgt = tg.GetComponent<TMP_Text>(); tgt.ForceMeshUpdate();
                float tw = tgt.preferredWidth + 14 * K, th2 = 13.5f * K;
                var plate = Img(headRow, "TagPlate", KitProc.Paint("tagc" + Mathf.RoundToInt(tw) + "x" + Mathf.RoundToInt(th2) + (newTag ? "n" : ""), TagSpec(tw, th2, newTag, false)), Color.white, false);
                var tl = plate.gameObject.AddComponent<LayoutElement>(); tl.preferredWidth = tw; tl.preferredHeight = th2;
                tg.transform.SetParent(plate.transform, false); Anchor(tg.Rt(), Vector2.zero, Vector2.one); headWidth += tw + 6 * K;
            }
            if (m.Pinned)
            {
                var pn = Txt(headRow, "Pin", "مثبّتة", th.fontBody, 10.2f * K, new Color32(0xE8, 0xC2, 0x7A, 255), TextAlignmentOptions.MidlineLeft);
                pn.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
                var pnt = pn.GetComponent<TMP_Text>(); pnt.ForceMeshUpdate();
                var pl = pn.gameObject.AddComponent<LayoutElement>(); pl.flexibleWidth = 1; pl.preferredWidth = pnt.preferredWidth; headWidth += pnt.preferredWidth + 6 * K;
            }
            else if (!string.IsNullOrEmpty(m.Time))
            {
                var tm = Txt(headRow, "Time", m.Time, th.fontBody, 10f * K, Faint, TextAlignmentOptions.MidlineLeft);
                tm.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
                var tmt = tm.GetComponent<TMP_Text>(); tmt.ForceMeshUpdate();
                var tl = tm.gameObject.AddComponent<LayoutElement>(); tl.flexibleWidth = 1; tl.preferredWidth = tmt.preferredWidth + 12 * K; headWidth += tmt.preferredWidth + 18 * K;
            }

            var body = Txt(bubble.transform, "Text", m.Text, th.fontBody, 13.1f * K, bodyColor, TextAlignmentOptions.TopRight);
            // a short line gets a narrow bubble: measure it on one line, then let it wrap inside the bubble
            var measure = body.GetComponent<TMP_Text>(); measure.ForceMeshUpdate();
            float maxW = (W - 68f - 2 * 10 * K) * 0.88f;
            float want = Mathf.Max(measure.preferredWidth, headWidth) + 20 * K + 6;
            bl.preferredWidth = m.Pinned ? maxW / 0.88f : Mathf.Clamp(want, 120 * K, maxW);
            body.Wrap = true;
            body.gameObject.AddComponent<LayoutElement>().minHeight = 13.1f * K * 1.55f;
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

        private static KitProc.Spec Spec3(Color top, Color mid, Color bottom) => new KitProc.Spec { Top = top, Mid = mid, Bottom = bottom, MidAt = 0.55f };

        private static KitProc.Spec GreenSpec()
        {
            var s = Spec3(new Color32(0x8F, 0xE0, 0xC6, 255), new Color32(0x2C, 0x8D, 0x73, 255), new Color32(0x14, 0x46, 0x3A, 255));
            s.Shadow = new Color32(0x4F, 0xA5, 0x8B, 0x88); s.ShadowBlur = 14 * K; return s;
        }

        private static KitProc.Spec RingSpec() => new KitProc.Spec { Top = new Color(0, 0, 0, KitProc.LinA(0.6f)), Bottom = new Color(0, 0, 0, KitProc.LinA(0.6f)), Edge = new Color32(0x6B, 0x5A, 0x3F, 255), EdgePx = 1.5f * K };

        /// <summary>A round icon button of the bottom bar: its sprite is painted from <paramref name="spec"/> (colors only), the icon on top.</summary>
        private RectTransform RoundButton(Transform parent, string name, float size, string key, KitProc.Spec spec, string icon, float stroke, Color iconColor)
        {
            var box = Img(parent, name, null, new Color(0, 0, 0, 0), false);
            var le = box.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = size; le.preferredHeight = size; le.minWidth = size; le.minHeight = size;
            var face = Img(box.transform, "Face", null, Color.white, false);
            SetFace(face, ButtonSprite(key, spec));
            var ic = Img(box.transform, "Icon", KitSvg.Icon(icon, stroke), iconColor, false); Place(ic.rectTransform, Vector2.zero, new Vector2(21 * K, 21 * K));
            return box.rectTransform;
        }

        /// <summary>Puts a painted face on a button: the sprite is a little bigger than the button (its glow and shadow), so the face reaches out of it.</summary>
        private static void SetFace(Image face, Sprite sprite)
        {
            face.sprite = sprite;
            float size = 40 * K;
            float m = (sprite.rect.width / KitProc.Q - size) * 0.5f;
            face.rectTransform.anchorMin = Vector2.zero; face.rectTransform.anchorMax = Vector2.one; face.rectTransform.offsetMin = new Vector2(-m, -m); face.rectTransform.offsetMax = new Vector2(m, m);
        }

        private static Sprite ButtonSprite(string key, KitProc.Spec spec)
        {
            float size = 40 * K;
            spec.W = spec.H = size; spec.Radius = size * 0.5f; spec.Margin = spec.Shadow.a > 0f && spec.ShadowBlur > 3 * K ? 18 * K : 4 * K;
            if (spec.Shadow.a == 0f) { spec.Shadow = Color.black; spec.ShadowDy = 2 * K; spec.ShadowBlur = 1.2f; if (key == "ring") spec.Shadow = Color.clear; }
            return KitProc.Paint("btn" + key, spec);
        }

        private static Sprite Plate(string key, float w, float h, float radius, Color fill, Color unused, Color edge, float edgePx, bool gradientRed = false)
        {
            var s = new KitProc.Spec { W = w, H = h, Radius = radius, Top = fill, Bottom = fill };
            if (edge.a > 0f) { s.Edge = edge; s.EdgePx = edgePx; }
            return KitProc.Paint("plate" + key + Mathf.RoundToInt(w) + "x" + Mathf.RoundToInt(h) + ColorUtility.ToHtmlStringRGBA(fill), s);
        }

        private static Sprite Sliced(string key, float radius, Color fill, Color edge, float edgePx)
        {
            var s = new KitProc.Spec { Radius = radius, Top = fill, Bottom = fill, Sliced = true };
            if (edge.a > 0f) { s.Edge = edge; s.EdgePx = edgePx; }
            return KitProc.Paint("sl" + key, s);
        }

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
            if (sliced && sprite != null && sprite.border.sqrMagnitude > 0f) { i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = KitProc.Q; }     // painted sprites are 100 pixels per unit (the canvas reference): with the multiplier Q a border of b pixels is b / Q canvas units
            
            if (stretch) Anchor(i.rectTransform, Vector2.zero, Vector2.one);
            return i;
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
