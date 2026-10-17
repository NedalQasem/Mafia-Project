using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    public static partial class KitBuilder
    {
        private const string ChatScenePath = "Assets/_Project/Scenes/UIKitChat.unity";

        private static GameObject MakeMsgPrefab(KitTheme th, string name, string kind)
        {
            var root = new GameObject(name, typeof(RectTransform));
            Rt(root).sizeDelta = new Vector2(1040, 130);
            var le = root.AddComponent<LayoutElement>(); le.preferredWidth = 1040; le.preferredHeight = 130;
            bool other = kind == "other", mine = kind == "mine", sys = kind == "sys", day = kind == "day";

            AvatarFrame avatar = null; ArabicText nameLbl = null;
            if (other)
            {
                var av = InstChild("AvatarFrame", root.transform, "Avatar");
                Place(Rt(av), new Vector2(1, 1), new Vector2(-62, -64), new Vector2(96, 96));
                avatar = av.GetComponent<AvatarFrame>();
                var nm = Txt(root.transform, "Sender", "الاسم", th.fontHeading, 28, new Color32(0xC9, 0xA8, 0x6A, 255), TextAlignmentOptions.MidlineRight);
                var nr = Rt(nm.gameObject); nr.anchorMin = nr.anchorMax = new Vector2(1, 1); nr.pivot = new Vector2(1, 1); nr.anchoredPosition = new Vector2(-124, -2); nr.sizeDelta = new Vector2(620, 38);
                nameLbl = nm.GetComponent<ArabicText>();
            }

            Sprite spr = other ? th.leatherDark : mine ? th.leatherRed : sys ? th.paper : th.slotDark;
            var bubble = Go("Bubble", root.transform);
            var br = Rt(bubble);
            if (other) { br.anchorMin = br.anchorMax = new Vector2(1, 1); br.pivot = new Vector2(1, 1); br.anchoredPosition = new Vector2(-124, -44); }
            else if (mine) { br.anchorMin = br.anchorMax = new Vector2(0, 1); br.pivot = new Vector2(0, 1); br.anchoredPosition = new Vector2(40, -4); }
            else { br.anchorMin = br.anchorMax = new Vector2(0.5f, 1); br.pivot = new Vector2(0.5f, 1); br.anchoredPosition = new Vector2(0, -4); }
            br.sizeDelta = new Vector2(400, 100);
            Img(bubble, spr, true, other || mine ? 1.4f : 1.1f);

            Color textColor = other ? th.leatherText : mine ? new Color32(0xF6, 0xE4, 0xE0, 255) : sys ? th.paperInk : th.ivoryDim;
            var label = Txt(bubble.transform, "Text", "نص", th.fontBody, other || mine ? 34 : (sys ? 28 : 26), textColor, TextAlignmentOptions.TopRight);
            var lr = Rt(label.gameObject);
            if (sys || day) { lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 1); lr.pivot = new Vector2(0.5f, 1); lr.anchoredPosition = new Vector2(0, other ? -22 : -14); }
            else { lr.anchorMin = lr.anchorMax = new Vector2(1, 1); lr.pivot = new Vector2(1, 1); lr.anchoredPosition = new Vector2(-34, -22); }
            lr.sizeDelta = new Vector2(300, 40);

            ArabicText timeLbl = null;
            if (other || mine)
            {
                var tm = Txt(bubble.transform, "Time", "٩:٣٠", th.fontBody, 22, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.BottomLeft);
                var tr = Rt(tm.gameObject); tr.anchorMin = tr.anchorMax = new Vector2(0, 0); tr.pivot = new Vector2(0, 0); tr.anchoredPosition = new Vector2(26, 8); tr.sizeDelta = new Vector2(140, 30);
                timeLbl = tm.GetComponent<ArabicText>();
            }

            var view = root.AddComponent<ChatMessageView>();
            SetRef(view, "bubble", br); SetRef(view, "label", label.GetComponent<ArabicText>()); SetRef(view, "layout", le);
            if (timeLbl != null) SetRef(view, "timeLabel", timeLbl);
            if (nameLbl != null) SetRef(view, "nameLabel", nameLbl);
            if (avatar != null) SetRef(view, "avatar", avatar);
            var so = new SerializedObject(view);
            so.FindProperty("padTop").floatValue = (sys || day) ? 14f : 22f;
            so.FindProperty("padBottom").floatValue = (sys || day) ? 16f : 46f;
            so.FindProperty("padX").floatValue = (sys || day) ? 40f : 34f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void BuildChat(KitTheme th)
        {
            var soft = new Color32(0x6A, 0x5A, 0x3A, 255);
            var gold = new Color32(0xC9, 0xA8, 0x6A, 255);

            Save(MakeMsgPrefab(th, "ChatMsgOther", "other"), "ChatMsgOther");
            Save(MakeMsgPrefab(th, "ChatMsgMine", "mine"), "ChatMsgMine");
            Save(MakeMsgPrefab(th, "ChatMsgSys", "sys"), "ChatMsgSys");
            Save(MakeMsgPrefab(th, "ChatMsgDay", "day"), "ChatMsgDay");

            {   // conversation row
                var root = new GameObject("ChatListItem", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(980, 170);
                var img = Img(root, th.leatherDark, true, 1f, true);
                var btn = root.AddComponent<Button>(); btn.targetGraphic = img; btn.transition = Selectable.Transition.None;
                var le = root.AddComponent<LayoutElement>(); le.preferredWidth = 980; le.preferredHeight = 170;
                var fx = root.AddComponent<KitButtonFx>(); SetRef(fx, "face", Rt(root));
                var av = InstChild("AvatarFrame", root.transform, "Avatar"); Place(Rt(av), new Vector2(0.5f, 0.5f), new Vector2(385, 6), new Vector2(116, 116));
                var gi = Go("GroupIcon", root.transform); Place(Rt(gi), new Vector2(0.5f, 0.5f), new Vector2(385, 6), new Vector2(112, 112));
                Img(gi, th.slotDark, true, 1.6f);
                var gic = Go("Icon", gi.transform); Place(Rt(gic), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78, 78)); Img(gic, th.navIcons[1]);
                var nm = Txt(root.transform, "Name", "الاسم", th.fontHeading, 40, th.leatherText, TextAlignmentOptions.MidlineRight);
                Place(Rt(nm.gameObject), new Vector2(0.5f, 0.5f), new Vector2(110, 34), new Vector2(420, 56));
                var rk = Txt(root.transform, "Rank", "الجندي", th.fontBody, 24, th.brass, TextAlignmentOptions.MidlineLeft);
                Place(Rt(rk.gameObject), new Vector2(0.5f, 0.5f), new Vector2(-165, 36), new Vector2(160, 40));
                var last = Txt(root.transform, "Last", "آخر رسالة", th.fontBody, 28, th.ivoryDim, TextAlignmentOptions.MidlineRight);
                Place(Rt(last.gameObject), new Vector2(0.5f, 0.5f), new Vector2(60, -26), new Vector2(560, 44));
                var tm = Txt(root.transform, "Time", "٩:٣١", th.fontBody, 24, th.ivoryDim, TextAlignmentOptions.MidlineLeft);
                Place(Rt(tm.gameObject), new Vector2(0.5f, 0.5f), new Vector2(-385, 40), new Vector2(120, 34));
                var bd = Go("Badge", root.transform); Place(Rt(bd), new Vector2(0.5f, 0.5f), new Vector2(-385, -20), new Vector2(58, 58)); Img(bd, th.badgeDot);
                var bt = Txt(bd.transform, "N", "٢", th.fontHeading, 26, Color.white); Stretch(Rt(bt.gameObject), 0, 4, 0, 0);
                var v = root.AddComponent<ChatListItemView>();
                SetRef(v, "avatar", av.GetComponent<AvatarFrame>()); SetRef(v, "groupIcon", gi);
                SetRef(v, "nameLabel", nm.GetComponent<ArabicText>()); SetRef(v, "lastLabel", last.GetComponent<ArabicText>());
                SetRef(v, "timeLabel", tm.GetComponent<ArabicText>()); SetRef(v, "rankLabel", rk.GetComponent<ArabicText>());
                SetRef(v, "badgeLabel", bt.GetComponent<ArabicText>()); SetRef(v, "badge", bd); SetRef(v, "button", btn);
                Save(root, "ChatListItem");
            }

            // ---------- scene
            var canvasGo = NewAppCanvas(th, new Color(0.075f, 0.055f, 0.045f));
            var screen = Go("ChatScreen", canvasGo.transform); Stretch(Rt(screen));

            // tabs row
            var tabsRow = Go("TabsRow", screen.transform);
            var trr = Rt(tabsRow); trr.anchorMin = new Vector2(0, 1); trr.anchorMax = new Vector2(1, 1); trr.pivot = new Vector2(0.5f, 1); trr.anchoredPosition = new Vector2(0, -290); trr.sizeDelta = new Vector2(0, 120);
            var tabsBtn = new Button[3]; var tabBadge = new GameObject[3]; var tabBadgeLbl = new ArabicText[3];
            string[] tn = { "العام", "الخاص", "المجموعات" };
            for (int i = 0; i < 3; i++)
            {
                var tb = Go("Tab" + i, tabsRow.transform); Place(Rt(tb), new Vector2(0.5f, 0.5f), new Vector2((1 - i) * 340, 0), new Vector2(320, 100));
                var ti = Img(tb, th.leatherDark, true, 1.5f, true);
                var b = tb.AddComponent<Button>(); b.targetGraphic = ti; b.transition = Selectable.Transition.None; tabsBtn[i] = b;
                var tt = Txt(tb.transform, "Label", tn[i], th.fontHeading, 36, th.ivoryDim); Stretch(Rt(tt.gameObject), 8, 10, 8, 14);
                var bd = Go("Badge", tb.transform); Place(Rt(bd), new Vector2(1, 1), new Vector2(-24, -14), new Vector2(54, 54)); Img(bd, th.badgeDot);
                var bt = Txt(bd.transform, "N", "٣", th.fontHeading, 24, Color.white); Stretch(Rt(bt.gameObject), 0, 4, 0, 0);
                tabBadge[i] = bd; tabBadgeLbl[i] = bt.GetComponent<ArabicText>();
            }

            // conversation header (hidden unless inside a DM / group)
            var header = Go("ConvHeader", screen.transform);
            var hr = Rt(header); hr.anchorMin = new Vector2(0, 1); hr.anchorMax = new Vector2(1, 1); hr.pivot = new Vector2(0.5f, 1); hr.anchoredPosition = new Vector2(0, -290); hr.sizeDelta = new Vector2(0, 150);
            var back = InstChild("KitButton_Secondary", header.transform, "Back");
            Place(Rt(back), new Vector2(1, 0.5f), new Vector2(-150, 0), new Vector2(260, 130)); back.GetComponentInChildren<ArabicText>().Text = "رجوع"; back.GetComponentInChildren<TextMeshProUGUI>().fontSize = 44;
            var ctitle = Txt(header.transform, "Title", "الاسم", th.fontCalligraphy, 66, th.leatherText); Place(Rt(ctitle.gameObject), new Vector2(0.5f, 0.5f), new Vector2(-40, 22), new Vector2(560, 84));
            var csub = Txt(header.transform, "Sub", "متصل الآن", th.fontBody, 28, th.brass); Place(Rt(csub.gameObject), new Vector2(0.5f, 0.5f), new Vector2(-40, -36), new Vector2(560, 40));
            header.SetActive(false);

            // public notice (pinned paper)
            var notice = Go("PublicNotice", screen.transform);
            var nr = Rt(notice); nr.anchorMin = new Vector2(0, 1); nr.anchorMax = new Vector2(1, 1); nr.pivot = new Vector2(0.5f, 1); nr.anchoredPosition = new Vector2(0, -420); nr.sizeDelta = new Vector2(-60, 92);
            Img(notice, th.paper, true, 1.3f);
            var pin = Go("Pin", notice.transform); Place(Rt(pin), new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(36, 36)); Img(pin, th.dotWhite).color = new Color32(0xB0, 0x2A, 0x32, 255);
            var nt = Txt(notice.transform, "T", "قواعد الساحة: احترم الجميع. المخالفة ممنوعة.", th.fontBody, 30, th.paperInk, TextAlignmentOptions.MidlineRight); Stretch(Rt(nt.gameObject), 140, 14, 90, 22);
            var nlb = Txt(notice.transform, "Pinned", "مثبّتة", th.fontHeading, 26, th.crimson, TextAlignmentOptions.MidlineLeft); Stretch(Rt(nlb.gameObject), 40, 14, 700, 22);

            RectTransform MakeScroll(string nm, out RectTransform contentRt, out ScrollRect sr, float spacing, int padTop)
            {
                var g = Go(nm, screen.transform);
                var r = Rt(g); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(0, 490); r.offsetMax = new Vector2(0, -520);
                sr = g.AddComponent<ScrollRect>(); sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 40f;
                var vp = Go("Viewport", g.transform); Stretch(Rt(vp)); vp.AddComponent<RectMask2D>(); vp.AddComponent<Image>().color = Color.clear;
                var c = Go("Content", vp.transform);
                contentRt = Rt(c); contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1); contentRt.pivot = new Vector2(0.5f, 1); contentRt.anchoredPosition = Vector2.zero; contentRt.sizeDelta = Vector2.zero;
                var v = c.AddComponent<VerticalLayoutGroup>(); v.spacing = spacing; v.padding = new RectOffset(0, 0, padTop, 24); v.childAlignment = TextAnchor.UpperCenter;
                v.childControlWidth = false; v.childControlHeight = false; v.childForceExpandWidth = false; v.childForceExpandHeight = false;
                c.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                sr.viewport = Rt(vp); sr.content = contentRt;
                return r;
            }
            MakeScroll("ListScroll", out var listContent, out var listSr, 14, 20);
            MakeScroll("MsgScroll", out var msgContent, out var msgSr, 6, 20);

            // composer
            var comp = Go("Composer", screen.transform);
            var cr = Rt(comp); cr.anchorMin = new Vector2(0, 0); cr.anchorMax = new Vector2(1, 0); cr.pivot = new Vector2(0.5f, 0); cr.anchoredPosition = new Vector2(0, 340); cr.sizeDelta = new Vector2(0, 140);
            var gift = Go("Gift", comp.transform); Place(Rt(gift), new Vector2(0, 0.5f), new Vector2(90, 0), new Vector2(120, 120)); Img(gift, th.navSeal);
            var gi2 = Go("Icon", gift.transform); Place(Rt(gi2), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66, 66)); Img(gi2, th.iconGift);
            var send = Go("Send", comp.transform); Place(Rt(send), new Vector2(1, 0.5f), new Vector2(-90, 0), new Vector2(120, 120));
            var sendImg = Img(send, th.navSeal, false, 1f, true);
            var sendBtn = send.AddComponent<Button>(); sendBtn.targetGraphic = sendImg; sendBtn.transition = Selectable.Transition.None;
            var sendFx = send.AddComponent<KitButtonFx>(); SetRef(sendFx, "face", Rt(send));
            var sIcon = Go("Icon", send.transform); Place(Rt(sIcon), new Vector2(0.5f, 0.5f), new Vector2(-4, 0), new Vector2(60, 60)); Img(sIcon, th.iconSend);
            sIcon.transform.localRotation = Quaternion.Euler(0, 0, 180f);
            sIcon.GetComponent<Image>().color = new Color(1f, 0.95f, 0.8f, 1f);
            var cool = Txt(send.transform, "Cool", "", th.fontHeading, 52, th.brassLight); Stretch(Rt(cool.gameObject), 0, 0, 0, 0);

            var field = Go("InputField", comp.transform);
            var fr = Rt(field); fr.anchorMin = new Vector2(0, 0.5f); fr.anchorMax = new Vector2(1, 0.5f); fr.pivot = new Vector2(0.5f, 0.5f); fr.offsetMin = new Vector2(170, -54); fr.offsetMax = new Vector2(-170, 54);
            var fimg = Img(field, th.paper, true, 2.2f, true);
            var area = Go("TextArea", field.transform); Stretch(Rt(area), 40, 10, 40, 10); area.AddComponent<RectMask2D>();
            var ph = Txt(area.transform, "Placeholder", "اكتب رسالة...", th.fontBody, 36, new Color32(0x7A, 0x6C, 0x50, 255), TextAlignmentOptions.MidlineRight);
            ph.fontStyle = FontStyles.Italic; Stretch(Rt(ph.gameObject), 0, 0, 0, 0);
            var it = Txt(area.transform, "Text", "", th.fontBody, 36, th.paperInk, TextAlignmentOptions.MidlineRight);
            var itArabic = it.GetComponent<ArabicText>(); Object.DestroyImmediate(itArabic);
            it.isRightToLeftText = true; Stretch(Rt(it.gameObject), 0, 0, 0, 0);
            var input = field.AddComponent<TMP_InputField>();
            input.textViewport = Rt(area); input.textComponent = it; input.placeholder = ph; input.fontAsset = th.fontBody;
            input.characterLimit = 200; input.lineType = TMP_InputField.LineType.SingleLine; input.targetGraphic = fimg;
            comp.SetActive(false);

            var chat = screen.AddComponent<KitChatScreen>();
            var so = new SerializedObject(chat);
            var ta = so.FindProperty("tabs"); var tba = so.FindProperty("tabBadges"); var tbl = so.FindProperty("tabBadgeLabels");
            ta.arraySize = tba.arraySize = tbl.arraySize = 3;
            for (int i = 0; i < 3; i++) { ta.GetArrayElementAtIndex(i).objectReferenceValue = tabsBtn[i]; tba.GetArrayElementAtIndex(i).objectReferenceValue = tabBadge[i]; tbl.GetArrayElementAtIndex(i).objectReferenceValue = tabBadgeLbl[i]; }
            so.ApplyModifiedPropertiesWithoutUndo();
            SetRef(chat, "tabsRow", tabsRow); SetRef(chat, "convHeader", header); SetRef(chat, "publicNotice", notice);
            SetRef(chat, "backButton", back.GetComponent<Button>()); SetRef(chat, "convTitle", ctitle.GetComponent<ArabicText>()); SetRef(chat, "convSub", csub.GetComponent<ArabicText>());
            SetRef(chat, "listScroll", listSr.gameObject); SetRef(chat, "msgScroll", msgSr.gameObject);
            SetRef(chat, "listContent", listContent); SetRef(chat, "msgContent", msgContent); SetRef(chat, "msgScrollRect", msgSr);
            SetRef(chat, "listItemPrefab", LoadPrefab("ChatListItem")); SetRef(chat, "msgOtherPrefab", LoadPrefab("ChatMsgOther")); SetRef(chat, "msgMinePrefab", LoadPrefab("ChatMsgMine"));
            SetRef(chat, "msgSysPrefab", LoadPrefab("ChatMsgSys")); SetRef(chat, "msgDayPrefab", LoadPrefab("ChatMsgDay"));
            SetRef(chat, "composer", comp); SetRef(chat, "input", input); SetRef(chat, "sendButton", sendBtn);
            SetRef(chat, "cooldownLabel", cool.GetComponent<ArabicText>()); SetRef(chat, "sendIcon", sIcon);
            listSr.gameObject.SetActive(false);
            chat.Init();
            AddBars(canvasGo, 1);
            LayoutRebuilder.ForceRebuildLayoutImmediate(msgContent);
            SaveScene(ChatScenePath);
        }
    }
}
