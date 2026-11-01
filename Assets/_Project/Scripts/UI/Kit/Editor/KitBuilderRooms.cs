using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Create-room screen, code sheet and toast (Docs/mockups/create-room.html), saved as prefabs and added to the rooms scene.</summary>
    public static partial class KitBuilder
    {
        private static void SetRefs(Object comp, string field, Object[] values)
        {
            var so = new SerializedObject(comp);
            var arr = so.FindProperty(field);
            if (arr == null) { Debug.LogWarning("[UIKit] missing field " + field + " on " + comp.GetType().Name); return; }
            arr.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject LayoutChild(string name, Transform parent, float height)
        {
            var g = Go(name, parent);
            g.AddComponent<LayoutElement>().preferredHeight = height;
            return g;
        }

        /// <summary>A leather card that grows with its content. Returns the card (children are laid out top to bottom).</summary>
        private static GameObject MakeRoomCard(Transform parent, KitTheme th, string title, string small, out ArabicText smallLabel)
        {
            var card = Go("Card_" + title, parent);
            Img(card, th.leatherDark, true, 1f);
            var v = card.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(30, 30, 24, 30); v.spacing = 22;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var row = LayoutChild("Title", card.transform, 60);
            var t = Txt(row.transform, "T", title, th.fontHeading, 42, th.leatherText, TextAlignmentOptions.MidlineRight);
            Stretch(Rt(t.gameObject), 0, 0, 0, 0);
            smallLabel = null;
            if (small != null)
            {
                var s = Txt(row.transform, "S", small, th.fontBody, 26, th.ivoryDim, TextAlignmentOptions.MidlineLeft);
                Stretch(Rt(s.gameObject), 0, 0, 0, 0);
                smallLabel = s.GetComponent<ArabicText>();
            }
            return card;
        }

        private static Button MakeChip(Transform parent, KitTheme th, string label, float width, float height, float fontSize)
        {
            var g = Go("Chip_" + label, parent);
            var le = g.AddComponent<LayoutElement>(); le.preferredWidth = width; le.preferredHeight = height;
            Rt(g).sizeDelta = new Vector2(width, height);
            var img = Img(g, th.slotDark, true, 2f, true);
            var b = g.AddComponent<Button>(); b.targetGraphic = img; b.transition = Selectable.Transition.None;
            var fx = g.AddComponent<KitButtonFx>(); SetRef(fx, "face", Rt(g));
            var t = Txt(g.transform, "T", label, th.fontBody, fontSize, th.leatherText); Stretch(Rt(t.gameObject), 6, 4, 6, 6);
            return b;
        }

        private static GameObject MakeToggleRow(Transform parent, KitTheme th, string title, string sub, bool on, out KitToggle toggle)
        {
            var row = LayoutChild("Toggle_" + title, parent, 118);
            var t = Txt(row.transform, "T", title, th.fontBody, 36, th.leatherText, TextAlignmentOptions.MidlineRight); Place(Rt(t.gameObject), new Vector2(1, 0.5f), new Vector2(-300, 22), new Vector2(600, 50));
            var s2 = Txt(row.transform, "S", sub, th.fontBody, 26, th.ivoryDim, TextAlignmentOptions.MidlineRight); Place(Rt(s2.gameObject), new Vector2(1, 0.5f), new Vector2(-300, -26), new Vector2(600, 36));
            var tg = Go("Toggle", row.transform); Place(Rt(tg), new Vector2(0, 0.5f), new Vector2(90, 0), new Vector2(140, 74));
            var ti = Img(tg, th.togOff, false, 1f, true); ti.preserveAspect = false;
            var tb = tg.AddComponent<Button>(); tb.targetGraphic = ti; tb.transition = Selectable.Transition.None;
            var litGo = Go("Lit", tg.transform); Stretch(Rt(litGo)); var litImg = Img(litGo, th.togOn); litImg.preserveAspect = false; litImg.raycastTarget = false; litImg.color = new Color(1, 1, 1, 0);
            var kn = Go("Knob", tg.transform); Place(Rt(kn), new Vector2(0.5f, 0.5f), new Vector2(32, 0), new Vector2(60, 60)); Img(kn, th.togKnob).raycastTarget = false;
            toggle = tg.AddComponent<KitToggle>();
            SetRef(toggle, "button", tb); SetRef(toggle, "track", ti); SetRef(toggle, "knob", Rt(kn)); SetRef(toggle, "lit", litImg);
            var so = new SerializedObject(toggle); so.FindProperty("isOn").boolValue = on; so.FindProperty("travel").floatValue = 32f; so.ApplyModifiedPropertiesWithoutUndo();
            toggle.IsOn = on;
            return row;
        }

        private static void BuildCreateRoomPrefabs(KitTheme th)
        {
            BuildCreateRoomScreen(th);
            BuildCodeSheet(th);
            BuildToast(th);
        }

        private static void BuildCreateRoomScreen(KitTheme th)
        {
            var root = new GameObject("CreateRoomScreen", typeof(RectTransform));
            Stretch(Rt(root));
            var bg = Go("Bg", root.transform); Stretch(Rt(bg));
            var bgi = bg.AddComponent<Image>(); bgi.color = new Color(0.075f, 0.055f, 0.045f); bgi.raycastTarget = true;

            // ================= form
            var form = Go("Form", root.transform); Stretch(Rt(form));
            var back = InstChild("KitButton_Secondary", form.transform, "Back");
            var br = Rt(back); br.anchorMin = br.anchorMax = new Vector2(1, 1); br.pivot = new Vector2(1, 1); br.anchoredPosition = new Vector2(-32, -78); br.sizeDelta = new Vector2(260, 130);
            back.GetComponentInChildren<ArabicText>().Text = "رجوع"; back.GetComponentInChildren<TextMeshProUGUI>().fontSize = 44;
            var title = Txt(form.transform, "Title", "افتح طاولتك", th.fontHeading, 76, th.leatherText, TextAlignmentOptions.MidlineRight);
            Place(Rt(title.gameObject), new Vector2(1, 1), new Vector2(-620, -128), new Vector2(560, 100));
            var sub = Txt(form.transform, "Sub", "غرفة جديدة", th.fontBody, 30, th.brass, TextAlignmentOptions.MidlineRight);
            Place(Rt(sub.gameObject), new Vector2(1, 1), new Vector2(-620, -196), new Vector2(560, 44));

            // scroll
            var scrollGo = Go("Scroll", form.transform);
            var sr = Rt(scrollGo); sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = new Vector2(0, 420); sr.offsetMax = new Vector2(0, -260);
            var scroll = scrollGo.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 40f;
            var vp = Go("Viewport", scrollGo.transform); Stretch(Rt(vp)); vp.AddComponent<RectMask2D>();
            var vpi = vp.AddComponent<Image>(); vpi.color = Color.clear; vpi.raycastTarget = true;
            var content = Go("Content", vp.transform);
            var cRt = Rt(content); cRt.anchorMin = new Vector2(0, 1); cRt.anchorMax = new Vector2(1, 1); cRt.pivot = new Vector2(0.5f, 1); cRt.anchoredPosition = Vector2.zero; cRt.sizeDelta = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(30, 30, 10, 40); vlg.spacing = 28; vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = Rt(vp); scroll.content = cRt;

            // ---- card 1: name
            var c1 = MakeRoomCard(content.transform, th, "اسم الغرفة", "", out var counter);
            var field = LayoutChild("NameField", c1.transform, 130);
            var fimg = Img(field, th.paper, true, 2.2f, true);
            var area = Go("Area", field.transform); Stretch(Rt(area), 36, 10, 36, 10); area.AddComponent<RectMask2D>();
            var ph = Txt(area.transform, "Placeholder", "اسم الغرفة", th.fontCalligraphy, 56, new Color32(0x7A, 0x6C, 0x4E, 255), TextAlignmentOptions.MidlineRight); Stretch(Rt(ph.gameObject));
            var mirror = Txt(area.transform, "Mirror", "", th.fontCalligraphy, 58, th.paperInk, TextAlignmentOptions.MidlineRight); Stretch(Rt(mirror.gameObject));
            var raw = Txt(area.transform, "Raw", "", th.fontBody, 50, th.paperInk, TextAlignmentOptions.MidlineLeft);
            Object.DestroyImmediate(raw.GetComponent<ArabicText>()); Stretch(Rt(raw.gameObject));
            var caret = Go("Caret", area.transform); var cr = Rt(caret); cr.anchorMin = cr.anchorMax = new Vector2(1, 0.5f); cr.pivot = new Vector2(1, 0.5f); cr.anchoredPosition = new Vector2(-6, 0); cr.sizeDelta = new Vector2(5, 72);
            var cimg = caret.AddComponent<Image>(); cimg.color = th.crimson; cimg.raycastTarget = false;
            var tin = field.AddComponent<TMP_InputField>();
            tin.textViewport = Rt(area); tin.textComponent = raw; tin.fontAsset = th.fontBody; tin.targetGraphic = fimg;
            tin.characterLimit = 24; tin.lineType = TMP_InputField.LineType.SingleLine;
            var ain = field.AddComponent<KitArabicInput>();
            SetRef(ain, "input", tin); SetRef(ain, "mirror", mirror.GetComponent<ArabicText>()); SetRef(ain, "placeholder", ph.gameObject); SetRef(ain, "caret", cr);

            var sugRow = LayoutChild("Suggestions", c1.transform, 76);
            var hs = sugRow.AddComponent<HorizontalLayoutGroup>(); hs.spacing = 14; hs.childAlignment = TextAnchor.MiddleRight; hs.reverseArrangement = true;
            hs.childControlWidth = false; hs.childControlHeight = false; hs.childForceExpandWidth = false; hs.childForceExpandHeight = false;
            string[] sugs = { "سهرة الشباب", "مباراة سريعة", "على راحتنا", "طاولة العائلة" };
            var sugBtns = new Button[sugs.Length];
            for (int i = 0; i < sugs.Length; i++) { sugBtns[i] = MakeChip(sugRow.transform, th, sugs[i], 228, 76, 27); }

            // ---- card 2: type
            var c2 = MakeRoomCard(content.transform, th, "نوع الغرفة", null, out _);
            var types = LayoutChild("Types", c2.transform, 200);
            var ht = types.AddComponent<HorizontalLayoutGroup>(); ht.reverseArrangement = true; ht.spacing = 14; ht.childControlWidth = true; ht.childControlHeight = true; ht.childForceExpandWidth = true; ht.childForceExpandHeight = true;
            string[] tn = { "عامة", "خاصة", "VIP" }; string[] ts = { "تظهر للجميع", "بكود أو دعوة", "شكل مميّز" };
            var typeBtns = new Button[3]; var typeSubs = new ArabicText[3]; var typeIcons = new Image[3]; var typeLocks = new Image[3];
            Sprite[] typeIconSprites = { th.iconGlobe, th.iconKey, th.iconCrown };
            for (int i = 0; i < 3; i++)
            {
                var g = Go("Type_" + tn[i], types.transform);
                var img = Img(g, th.typeOff, true, 3f, true); img.preserveAspect = false;
                var b = g.AddComponent<Button>(); b.targetGraphic = img; b.transition = Selectable.Transition.None; typeBtns[i] = b;
                var fx = g.AddComponent<KitButtonFx>(); SetRef(fx, "face", Rt(g));
                var ic = Go("Icon", g.transform); Place(Rt(ic), new Vector2(0.5f, 0.5f), new Vector2(0, 54), new Vector2(66, 66));
                typeIcons[i] = Img(ic, typeIconSprites[i]); typeIcons[i].raycastTarget = false;
                var lk = Go("Lock", g.transform); Place(Rt(lk), new Vector2(0f, 1f), new Vector2(30, -30), new Vector2(34, 34));
                typeLocks[i] = Img(lk, th.iconLock); typeLocks[i].raycastTarget = false; typeLocks[i].color = new Color32(0xB9, 0xA6, 0x7E, 255); lk.SetActive(false);
                var n = Txt(g.transform, "Name", tn[i], th.fontHeading, 46, th.ivoryDim); Place(Rt(n.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, -14), new Vector2(300, 60));
                var s = Txt(g.transform, "Sub", ts[i], th.fontBody, 25, th.ivoryDim); Place(Rt(s.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, -64), new Vector2(300, 40));
                typeSubs[i] = s.GetComponent<ArabicText>();
            }
            var hint = LayoutChild("Hint", c2.transform, 160);
            Img(hint, th.slotDark, true, 2f);
            var hintTxt = Txt(hint.transform, "T", "", th.fontBody, 30, th.ivoryDim, TextAlignmentOptions.MidlineRight, true);
            Stretch(Rt(hintTxt.gameObject), 24, 10, 24, 10);

            // ---- card 3: mode
            var c3 = MakeRoomCard(content.transform, th, "شو بدكم تعملوا؟", null, out _);
            var segRow = LayoutChild("Mode", c3.transform, 112);
            Img(segRow, th.segBar, true, 2.4f);
            var hl = segRow.AddComponent<HorizontalLayoutGroup>(); hl.reverseArrangement = true; hl.padding = new RectOffset(10, 10, 10, 10); hl.spacing = 8; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
            string[] modes = { "سهرة وحكي", "مباراة مافيا" }; var modeBtns = new Button[2];
            for (int i = 0; i < 2; i++)
            {
                var o = Go("Opt" + i, segRow.transform); var oi = Img(o, th.segOn, true, 2.6f, true); oi.color = new Color(1, 1, 1, 0);
                var ob = o.AddComponent<Button>(); ob.targetGraphic = oi; ob.transition = Selectable.Transition.None; modeBtns[i] = ob;
                var ot = Txt(o.transform, "T", modes[i], th.fontHeading, 34, th.ivoryDim); Stretch(Rt(ot.gameObject), 4, 6, 4, 8);
            }
            var seg = segRow.AddComponent<KitSegmented>();
            SetRefs(seg, "options", modeBtns);
            seg.Paint();

            // ---- card 4: seats
            var c4 = MakeRoomCard(content.transform, th, "عدد الكراسي", "الصوت لحد ١٦", out _);
            var stepRow = LayoutChild("Stepper", c4.transform, 150);
            var hsr = stepRow.AddComponent<HorizontalLayoutGroup>(); hsr.reverseArrangement = true; hsr.spacing = 36; hsr.childAlignment = TextAnchor.MiddleCenter;
            hsr.childControlWidth = false; hsr.childControlHeight = false; hsr.childForceExpandWidth = false; hsr.childForceExpandHeight = false;
            Button Round(string name, string glyph)
            {
                var g = Go(name, stepRow.transform); var le = g.AddComponent<LayoutElement>(); le.preferredWidth = 130; le.preferredHeight = 130;
                Rt(g).sizeDelta = new Vector2(130, 130);
                var img = Img(g, th.leatherRed, true, 1.4f, true);
                var b = g.AddComponent<Button>(); b.targetGraphic = img; b.transition = Selectable.Transition.None;
                var fx = g.AddComponent<KitButtonFx>(); SetRef(fx, "face", Rt(g));
                var t = Txt(g.transform, "T", glyph, th.fontHeading, 84, th.ivory); Stretch(Rt(t.gameObject), 0, 8, 0, 8);
                return b;
            }
            var plus = Round("Plus", "+");
            var plate = Go("Plate", stepRow.transform); var pl = plate.AddComponent<LayoutElement>(); pl.preferredWidth = 280; pl.preferredHeight = 130; Rt(plate).sizeDelta = new Vector2(280, 130);
            Img(plate, th.slotDark, true, 1.8f);
            var seatsTxt = Txt(plate.transform, "N", "١٢", th.fontHeading, 100, new Color32(0xFF, 0xB3, 0x5A, 255)); Stretch(Rt(seatsTxt.gameObject), 0, 4, 0, 14);
            var minus = Round("Minus", "−");
            var dotsRow = LayoutChild("Dots", c4.transform, 56);
            var hd = dotsRow.AddComponent<HorizontalLayoutGroup>(); hd.spacing = 10; hd.childAlignment = TextAnchor.MiddleCenter;
            hd.childControlWidth = false; hd.childControlHeight = false; hd.childForceExpandWidth = false; hd.childForceExpandHeight = false;
            var dots = new Image[16];
            for (int i = 0; i < 16; i++)
            {
                var d = Go("Dot" + i, dotsRow.transform); var le = d.AddComponent<LayoutElement>(); le.preferredWidth = 44; le.preferredHeight = 44; Rt(d).sizeDelta = new Vector2(44, 44);
                dots[i] = Img(d, th.dotWhite); dots[i].raycastTarget = false;
            }

            // ---- card 5: switches
            var c5 = MakeRoomCard(content.transform, th, "الخيارات", null, out _);
            MakeToggleRow(c5.transform, th, "الدردشة النصية", "للّي ما بدّه يحكي", true, out var chatTg);
            MakeToggleRow(c5.transform, th, "المايك مقفول عند الدخول", "اللاعب يفتحه بنفسه", false, out var micTg);

            // ---- warning
            var warn = LayoutChild("Warn", content.transform, 130);
            Img(warn, th.slotDark, true, 2f);
            var warnTxt = Txt(warn.transform, "T", "لكل لاعب غرفة وحدة. إذا فتحت غرفة جديدة، غرفتك القديمة بتنسكّر تلقائياً.", th.fontBody, 28, new Color32(0xE6, 0xB3, 0xB6, 255), TextAlignmentOptions.MidlineRight, true);
            Stretch(Rt(warnTxt.gameObject), 24, 10, 24, 10);

            // ---- bottom bar
            var errTxt = Txt(form.transform, "Error", "", th.fontBody, 32, new Color32(0xE6, 0x7A, 0x80, 255));
            Place(Rt(errTxt.gameObject), new Vector2(0.5f, 0f), new Vector2(0, 322), new Vector2(980, 56));
            var createSub = Txt(form.transform, "CreateSub", "غرفة خاصة · ١٢ كرسي", th.fontBody, 30, th.brass);
            Place(Rt(createSub.gameObject), new Vector2(0.5f, 0f), new Vector2(0, 262), new Vector2(980, 44));
            var create = InstChild("KitButton_Primary", form.transform, "Create");
            var crr = Rt(create); crr.anchorMin = crr.anchorMax = new Vector2(0.5f, 0f); crr.pivot = new Vector2(0.5f, 0.5f); crr.anchoredPosition = new Vector2(0, 130); crr.sizeDelta = new Vector2(980, 180);
            create.GetComponentInChildren<ArabicText>().Text = "افتح الطاولة";

            // ================= ticket (after creation)
            var ok = Go("Success", root.transform); Stretch(Rt(ok));
            var sBack = InstChild("KitButton_Secondary", ok.transform, "Back");
            var sbr = Rt(sBack); sbr.anchorMin = sbr.anchorMax = new Vector2(1, 1); sbr.pivot = new Vector2(1, 1); sbr.anchoredPosition = new Vector2(-32, -78); sbr.sizeDelta = new Vector2(260, 130);
            sBack.GetComponentInChildren<ArabicText>().Text = "رجوع"; sBack.GetComponentInChildren<TextMeshProUGUI>().fontSize = 44;
            var sTitle = Txt(ok.transform, "Title", "الطاولة جاهزة", th.fontHeading, 76, th.leatherText, TextAlignmentOptions.MidlineRight);
            Place(Rt(sTitle.gameObject), new Vector2(1, 1), new Vector2(-620, -128), new Vector2(560, 100));
            // the red "ready" stamp above the ticket
            var stamp = Go("Stamp", ok.transform); PlaceTop(Rt(stamp), 0, -230, 560, 170);
            var sti = Img(stamp, th.stampFrame, true, 1f); sti.color = new Color32(0xE0, 0x58, 0x5F, 255); sti.raycastTarget = false;
            stamp.transform.localRotation = Quaternion.Euler(0, 0, 6f);
            var stt = Txt(stamp.transform, "T", "جاهزة", th.fontCalligraphy, 130, new Color32(0xE0, 0x58, 0x5F, 255)); Stretch(Rt(stt.gameObject), 8, 10, 8, 24);

            // the paper ticket: a red bookmark on the corner, dashed lines around the code
            var ticket = Go("Ticket", ok.transform); PlaceTop(Rt(ticket), 0, -440, 1020, 700);
            Img(ticket, th.paper, true, 2.2f); ticket.transform.localRotation = Quaternion.Euler(0, 0, 1.2f);
            var ribbon = Go("Bookmark", ticket.transform); Place(Rt(ribbon), new Vector2(1, 1), new Vector2(-110, 2), new Vector2(76, 62)); Img(ribbon, th.ribbonRed).raycastTarget = false;
            var tKind = Txt(ticket.transform, "Kind", "غرفة خاصة", th.fontBody, 36, new Color32(0x6A, 0x5A, 0x3A, 255)); Place(Rt(tKind.gameObject), new Vector2(0.5f, 1f), new Vector2(0, -84), new Vector2(900, 54));
            var tName = Txt(ticket.transform, "Name", "سهرة الأصحاب", th.fontCalligraphy, 104, th.paperInk); Place(Rt(tName.gameObject), new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(940, 140));
            Image DashLine(string name, float y)
            {
                var d = Go(name, ticket.transform); Place(Rt(d), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(900, 6));
                var di = Img(d, th.dashH); di.type = Image.Type.Tiled; di.color = new Color(0.11f, 0.09f, 0.06f, 0.4f); di.raycastTarget = false; return di;
            }
            DashLine("DashTop", -300);
            var tCode = Txt(ticket.transform, "Code", "K7Q2", th.fontHeading, 210, th.crimson); tCode.characterSpacing = 50; Place(Rt(tCode.gameObject), new Vector2(0.5f, 1f), new Vector2(0, -410), new Vector2(940, 230));
            DashLine("DashBottom", -530);
            var tNote = Txt(ticket.transform, "Note", "الكود بيدخّل أي لاعب.", th.fontBody, 32, new Color32(0x6A, 0x5A, 0x3A, 255), TextAlignmentOptions.Center, true); Place(Rt(tNote.gameObject), new Vector2(0.5f, 0f), new Vector2(0, 76), new Vector2(920, 90));

            Button ShareBtn(string name, string label, float x, Sprite icon, Color iconColor)
            {
                var g = Go(name, ok.transform); var r = Rt(g); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f); r.pivot = new Vector2(0.5f, 0f); r.anchoredPosition = new Vector2(x, 295); r.sizeDelta = new Vector2(316, 190);   // above the enter button, so short screens do not overlap
                var img = Img(g, th.leatherDark, true, 1.4f, true);
                var b = g.AddComponent<Button>(); b.targetGraphic = img; b.transition = Selectable.Transition.None;
                var fx = g.AddComponent<KitButtonFx>(); SetRef(fx, "face", Rt(g));
                var ic = Go("Icon", g.transform); Place(Rt(ic), new Vector2(0.5f, 0.5f), new Vector2(0, 38), new Vector2(72, 72));
                var ii = Img(ic, icon); ii.color = iconColor; ii.raycastTarget = false;
                var t = Txt(g.transform, "T", label, th.fontHeading, 38, th.leatherText); Place(Rt(t.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, -44), new Vector2(296, 60));
                return b;
            }
            var wa = ShareBtn("WhatsApp", "واتساب", 328, th.iconWhatsapp, new Color32(0x4F, 0xD0, 0x8B, 255));
            var copyInv = ShareBtn("CopyInvite", "نسخ الدعوة", 0, th.iconLink, new Color32(0xCF, 0xC6, 0xB2, 255));
            var copyCode = ShareBtn("CopyCode", "نسخ الكود", -328, th.iconCopy, new Color32(0xCF, 0xC6, 0xB2, 255));
            var enter = InstChild("KitButton_Primary", ok.transform, "Enter");
            var er = Rt(enter); er.anchorMin = er.anchorMax = new Vector2(0.5f, 0f); er.pivot = new Vector2(0.5f, 0.5f); er.anchoredPosition = new Vector2(0, 150); er.sizeDelta = new Vector2(980, 190);
            enter.GetComponentInChildren<ArabicText>().Text = "ادخل الغرفة";
            ok.SetActive(false);

            var screen = root.AddComponent<KitCreateRoomScreen>();
            SetRef(screen, "formRoot", form); SetRef(screen, "backButton", back.GetComponent<Button>()); SetRef(screen, "createButton", create.GetComponent<Button>());
            SetRef(screen, "nameInput", ain); SetRef(screen, "nameCounter", counter);
            SetRefs(screen, "suggestionButtons", sugBtns); SetRefs(screen, "typeButtons", typeBtns); SetRefs(screen, "typeSubLabels", typeSubs); SetRefs(screen, "typeIcons", typeIcons); SetRefs(screen, "typeLocks", typeLocks);
            SetRef(screen, "typeHint", hintTxt.GetComponent<ArabicText>()); SetRef(screen, "modeSegment", seg);
            SetRef(screen, "seatsMinus", minus); SetRef(screen, "seatsPlus", plus); SetRef(screen, "seatsLabel", seatsTxt.GetComponent<ArabicText>());
            SetRefs(screen, "seatDots", dots);
            SetRef(screen, "chatToggle", chatTg); SetRef(screen, "micToggle", micTg);
            SetRef(screen, "createSub", createSub.GetComponent<ArabicText>()); SetRef(screen, "errorLabel", errTxt.GetComponent<ArabicText>());
            SetRef(screen, "successRoot", ok); SetRef(screen, "ticketKind", tKind.GetComponent<ArabicText>()); SetRef(screen, "ticketName", tName.GetComponent<ArabicText>());
            SetRef(screen, "ticketCode", tCode.GetComponent<ArabicText>()); SetRef(screen, "ticketNote", tNote.GetComponent<ArabicText>());
            SetRef(screen, "whatsappButton", wa); SetRef(screen, "copyInviteButton", copyInv); SetRef(screen, "copyCodeButton", copyCode);
            SetRef(screen, "enterButton", enter.GetComponent<Button>()); SetRef(screen, "successBack", sBack.GetComponent<Button>());
            Save(root, "CreateRoomScreen");
        }

        private static void BuildCodeSheet(KitTheme th)
        {
            var root = new GameObject("CodeSheet", typeof(RectTransform)); Stretch(Rt(root));
            var dimGo = Go("Dim", root.transform); Stretch(Rt(dimGo));
            var dimImg = dimGo.AddComponent<Image>(); dimImg.color = new Color(0, 0, 0, 0.62f); dimImg.raycastTarget = true;
            dimGo.AddComponent<Button>().targetGraphic = dimImg;
            var dim = dimGo.AddComponent<CanvasGroup>();
            var sheet = Go("Sheet", root.transform); var sr = Rt(sheet);
            sr.anchorMin = new Vector2(0, 0); sr.anchorMax = new Vector2(1, 0); sr.pivot = new Vector2(0.5f, 0f); sr.anchoredPosition = Vector2.zero; sr.sizeDelta = new Vector2(0, 760);
            Img(sheet, th.leatherDark, true, 1f, true);
            var t = Txt(sheet.transform, "Title", "ادخل بالكود", th.fontHeading, 64, th.leatherText, TextAlignmentOptions.MidlineRight); Place(Rt(t.gameObject), new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(900, 90));
            var s = Txt(sheet.transform, "Sub", "اكتب الكود اللي أرسله لك صاحب الغرفة", th.fontBody, 30, th.ivoryDim, TextAlignmentOptions.MidlineRight); Place(Rt(s.gameObject), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(900, 50));
            var field = Go("Field", sheet.transform); Place(Rt(field), new Vector2(0.5f, 1f), new Vector2(0, -290), new Vector2(760, 150));
            var fimg = Img(field, th.paper, true, 2.2f, true);
            var area = Go("Area", field.transform); Stretch(Rt(area), 30, 8, 30, 8); area.AddComponent<RectMask2D>();
            var ph = Txt(area.transform, "Placeholder", "K7Q2", th.fontHeading, 96, new Color32(0xA0, 0x92, 0x72, 255)); ph.characterSpacing = 40; Stretch(Rt(ph.gameObject));
            var txt = Txt(area.transform, "Text", "", th.fontHeading, 96, th.crimson); txt.characterSpacing = 40; Object.DestroyImmediate(txt.GetComponent<ArabicText>()); Stretch(Rt(txt.gameObject));
            var tin = field.AddComponent<TMP_InputField>();
            tin.textViewport = Rt(area); tin.textComponent = txt; tin.placeholder = ph; tin.fontAsset = th.fontHeading; tin.targetGraphic = fimg;
            tin.characterLimit = 4; tin.lineType = TMP_InputField.LineType.SingleLine; tin.caretColor = th.crimson; tin.customCaretColor = true;
            var msg = Txt(sheet.transform, "Msg", "", th.fontBody, 30, new Color32(0xE6, 0x7A, 0x80, 255)); Place(Rt(msg.gameObject), new Vector2(0.5f, 1f), new Vector2(0, -400), new Vector2(900, 50));
            var join = InstChild("KitButton_Primary", sheet.transform, "Join");
            var jr = Rt(join); jr.anchorMin = jr.anchorMax = new Vector2(0.5f, 0f); jr.pivot = new Vector2(0.5f, 0.5f); jr.anchoredPosition = new Vector2(120, 130); jr.sizeDelta = new Vector2(540, 160);
            join.GetComponentInChildren<ArabicText>().Text = "ادخل";
            var close = InstChild("KitButton_Secondary", sheet.transform, "Close");
            var clr = Rt(close); clr.anchorMin = clr.anchorMax = new Vector2(0.5f, 0f); clr.pivot = new Vector2(0.5f, 0.5f); clr.anchoredPosition = new Vector2(-300, 130); clr.sizeDelta = new Vector2(300, 160);
            close.GetComponentInChildren<ArabicText>().Text = "لاحقاً"; close.GetComponentInChildren<TextMeshProUGUI>().fontSize = 44;
            var comp = root.AddComponent<KitCodeSheet>();
            SetRef(comp, "dim", dim); SetRef(comp, "sheet", sr); SetRef(comp, "input", tin); SetRef(comp, "message", msg.GetComponent<ArabicText>());
            SetRef(comp, "joinButton", join.GetComponent<Button>()); SetRef(comp, "closeButton", close.GetComponent<Button>());
            Save(root, "CodeSheet");
        }

        private static void BuildToast(KitTheme th)
        {
            var root = new GameObject("Toast", typeof(RectTransform));
            var r = Rt(root); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f); r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = new Vector2(0, 470); r.sizeDelta = new Vector2(960, 150);
            var cg = root.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
            var img = Img(root, th.paper, true, 2.2f); img.raycastTarget = false;
            var t = Txt(root.transform, "T", "", th.fontBody, 34, th.paperInk, TextAlignmentOptions.Center, true); Stretch(Rt(t.gameObject), 40, 14, 40, 14);
            var comp = root.AddComponent<KitToast>();
            SetRef(comp, "label", t.GetComponent<ArabicText>()); SetRef(comp, "group", cg);
            Save(root, "Toast");
        }

        /// <summary>Adds the code link, the empty-list note, the create screen, the code sheet and the toast to the rooms scene.</summary>
        private static void AddRoomsFlowUi(GameObject canvasGo, KitTheme th, GameObject roomsScreen, KitRoomsScreen rs)
        {
            // "got a code?" link under the two buttons
            var link = Go("CodeLink", roomsScreen.transform);
            var lr = Rt(link); lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 1f); lr.pivot = new Vector2(0.5f, 1f); lr.anchoredPosition = new Vector2(0, -486); lr.sizeDelta = new Vector2(900, 84);
            var limg = Img(link, th.slotDark, true, 2f, true);
            var lb = link.AddComponent<Button>(); lb.targetGraphic = limg; lb.transition = Selectable.Transition.None;
            var lfx = link.AddComponent<KitButtonFx>(); SetRef(lfx, "face", lr);
            var lt = Txt(link.transform, "T", "عندك كود غرفة؟ اضغط هون", th.fontBody, 34, th.leatherText); Stretch(Rt(lt.gameObject), 10, 4, 10, 8);

            // shown when the list is empty
            var empty = Go("EmptyNote", roomsScreen.transform);
            var er = Rt(empty); er.anchorMin = er.anchorMax = new Vector2(0.5f, 0.5f); er.pivot = new Vector2(0.5f, 0.5f); er.anchoredPosition = new Vector2(0, -120); er.sizeDelta = new Vector2(900, 260);
            var et = Txt(empty.transform, "T", "ما في غرف مفتوحة هلأ.\nافتح طاولتك وادعُ أصحابك!", th.fontBody, 40, th.ivoryDim, TextAlignmentOptions.Center, true); Stretch(Rt(et.gameObject));
            empty.SetActive(false);

            // the scroll starts lower now
            var scroll = roomsScreen.transform.Find("Scroll");
            if (scroll != null) { var sr = (RectTransform)scroll; sr.offsetMax = new Vector2(0, -590); }

            var create = InstChild("CreateRoomScreen", canvasGo.transform, "CreateRoomScreen"); create.SetActive(false);
            var sheet = InstChild("CodeSheet", canvasGo.transform, "CodeSheet"); sheet.SetActive(false);
            var toast = InstChild("Toast", canvasGo.transform, "Toast");
            SetRef(rs, "codeButton", lb); SetRef(rs, "emptyNote", empty);
        }
    }
}
