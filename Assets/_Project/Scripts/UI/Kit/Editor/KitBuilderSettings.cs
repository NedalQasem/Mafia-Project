using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    public static partial class KitBuilder
    {
        private const string SettingsScenePath = "Assets/_Project/Scenes/UIKitSettings.unity";

        private static void BuildSettings(KitTheme th)
        {
            var arrow = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
            var soft = new Color32(0x6A, 0x5A, 0x3A, 255);
            var amber = new Color32(0xFF, 0xB3, 0x5A, 255);

            var canvasGo = NewAppCanvas(th, new Color(0.075f, 0.055f, 0.045f));
            var screen = Go("SettingsScreen", canvasGo.transform); Stretch(Rt(screen));
            var scrollGo = Go("Scroll", screen.transform);
            var sr = Rt(scrollGo); sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = new Vector2(0, 0); sr.offsetMax = new Vector2(0, -150);
            var scroll = scrollGo.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 40f;
            var vp = Go("Viewport", scrollGo.transform); Stretch(Rt(vp)); vp.AddComponent<RectMask2D>(); vp.AddComponent<Image>().color = Color.clear;
            var content = Go("Content", vp.transform);
            var cRt = Rt(content); cRt.anchorMin = new Vector2(0, 1); cRt.anchorMax = new Vector2(1, 1); cRt.pivot = new Vector2(0.5f, 1); cRt.anchoredPosition = Vector2.zero; cRt.sizeDelta = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>(); vlg.spacing = 20; vlg.padding = new RectOffset(0, 0, 26, 90); vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = false; vlg.childControlHeight = true; vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = Rt(vp); scroll.content = cRt;

            // ---- player ID strip (name, level and avatar are already in the top bar)
            {
                var me = Go("IdStrip", content.transform); Rt(me).sizeDelta = new Vector2(1000, 110);
                me.AddComponent<LayoutElement>().preferredHeight = 110;
                Img(me, th.slotDark, true, 2f);
                var idl = Txt(me.transform, "L", "رقم الهوية", th.fontBody, 34, th.ivoryDim, TextAlignmentOptions.MidlineRight); Place(Rt(idl.gameObject), new Vector2(1, 0.5f), new Vector2(-230, 0), new Vector2(380, 60));
                var idv = Txt(me.transform, "V", "٠٤٢٧٧١", th.fontHeading, 52, th.brassLight, TextAlignmentOptions.MidlineLeft); Place(Rt(idv.gameObject), new Vector2(0, 0.5f), new Vector2(230, 0), new Vector2(380, 70));
            }

            var sections = new System.Collections.Generic.List<KitSettingsSection>();
            KitKnob micKnob = null, spkKnob = null; Image[] meter = null; Button micTest = null; ArabicText micTestLbl = null;

            KitSettingsSection MakeSection(string title, string sub, Sprite icon, bool open, out RectTransform contentRt)
            {
                var root = Go("Section_" + title, content.transform); Rt(root).sizeDelta = new Vector2(1000, 140);
                var rv = root.AddComponent<VerticalLayoutGroup>(); rv.childControlWidth = false; rv.childControlHeight = true; rv.childForceExpandWidth = false; rv.childForceExpandHeight = false; rv.spacing = 0;
                var hd = Go("Header", root.transform); Rt(hd).sizeDelta = new Vector2(1000, 140);
                hd.AddComponent<LayoutElement>().preferredHeight = 140;
                var hi = Img(hd, th.leatherDark, true, 1f, true);
                var hb = hd.AddComponent<Button>(); hb.targetGraphic = hi; hb.transition = Selectable.Transition.None;
                var ic = Go("Icon", hd.transform); Place(Rt(ic), new Vector2(1, 0.5f), new Vector2(-84, 4), new Vector2(84, 84)); Img(ic, icon);
                var tt = Txt(hd.transform, "Title", title, th.fontHeading, 46, th.leatherText, TextAlignmentOptions.MidlineRight); Place(Rt(tt.gameObject), new Vector2(1, 0.5f), new Vector2(-520, 26), new Vector2(620, 60));
                var ss = Txt(hd.transform, "Sub", sub, th.fontBody, 26, th.ivoryDim, TextAlignmentOptions.MidlineRight); Place(Rt(ss.gameObject), new Vector2(1, 0.5f), new Vector2(-520, -26), new Vector2(620, 40));
                var chev = Go("Chevron", hd.transform); Place(Rt(chev), new Vector2(0, 0.5f), new Vector2(64, 6), new Vector2(46, 46)); var ci = Img(chev, arrow); ci.color = th.brass;
                var body = Go("Body", root.transform); Rt(body).sizeDelta = new Vector2(1000, 0);
                var bl = body.AddComponent<LayoutElement>(); bl.preferredHeight = 0;
                body.AddComponent<RectMask2D>();
                var bg = body.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0.28f); bg.raycastTarget = false;
                var ct = Go("Content", body.transform); contentRt = Rt(ct);
                contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1); contentRt.pivot = new Vector2(0.5f, 1); contentRt.anchoredPosition = Vector2.zero; contentRt.sizeDelta = new Vector2(0, 100);
                var cv = ct.AddComponent<VerticalLayoutGroup>(); cv.padding = new RectOffset(30, 30, 22, 26); cv.spacing = 10; cv.childControlWidth = true; cv.childControlHeight = true; cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;
                ct.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var sec = root.AddComponent<KitSettingsSection>();
                SetRef(sec, "header", hb); SetRef(sec, "body", Rt(body)); SetRef(sec, "content", contentRt); SetRef(sec, "chevron", Rt(chev)); SetRef(sec, "bodyLayout", bl);
                var so = new SerializedObject(sec); so.FindProperty("openAtStart").boolValue = open; so.ApplyModifiedPropertiesWithoutUndo();
                sections.Add(sec);
                return sec;
            }

            KitToggle ToggleRow(RectTransform parent, string title, string sub, bool on)
            {
                var row = Go("Toggle_" + title, parent); Rt(row).sizeDelta = new Vector2(940, sub == null ? 100 : 118);
                row.AddComponent<LayoutElement>().preferredHeight = sub == null ? 100 : 118;
                var t = Txt(row.transform, "T", title, th.fontBody, 36, th.leatherText, TextAlignmentOptions.MidlineRight); Place(Rt(t.gameObject), new Vector2(1, 0.5f), new Vector2(-330, sub == null ? 0 : 22), new Vector2(620, 50));
                if (sub != null) { var s2 = Txt(row.transform, "S", sub, th.fontBody, 26, th.ivoryDim, TextAlignmentOptions.MidlineRight); Place(Rt(s2.gameObject), new Vector2(1, 0.5f), new Vector2(-330, -26), new Vector2(620, 36)); }
                var tg = Go("Toggle", row.transform); Place(Rt(tg), new Vector2(0, 0.5f), new Vector2(90, 0), new Vector2(140, 74));
                var ti = Img(tg, th.togOff, false, 1f, true); ti.preserveAspect = false;
                var tb = tg.AddComponent<Button>(); tb.targetGraphic = ti; tb.transition = Selectable.Transition.None;
                var litGo = Go("Lit", tg.transform); Stretch(Rt(litGo)); var litImg = Img(litGo, th.togOn); litImg.preserveAspect = false; litImg.raycastTarget = false; litImg.color = new Color(1, 1, 1, 0);
                var kn = Go("Knob", tg.transform); Place(Rt(kn), new Vector2(0.5f, 0.5f), new Vector2(32, 0), new Vector2(60, 60)); Img(kn, th.togKnob).raycastTarget = false;
                var comp = tg.AddComponent<KitToggle>();
                SetRef(comp, "button", tb); SetRef(comp, "track", ti); SetRef(comp, "knob", Rt(kn)); SetRef(comp, "lit", litImg);
                var so = new SerializedObject(comp); so.FindProperty("isOn").boolValue = on; so.FindProperty("travel").floatValue = 32f; so.ApplyModifiedPropertiesWithoutUndo();
                comp.IsOn = on;
                return comp;
            }

            KitSegmented SegRow(RectTransform parent, string label, string[] opts, int selected)
            {
                var row = Go("Seg_" + label, parent); Rt(row).sizeDelta = new Vector2(940, 170);
                row.AddComponent<LayoutElement>().preferredHeight = 170;
                var l = Txt(row.transform, "L", label, th.fontHeading, 32, th.brass, TextAlignmentOptions.MidlineRight); Place(Rt(l.gameObject), new Vector2(1, 1), new Vector2(-470, -30), new Vector2(940, 44));
                var bar = Go("Bar", row.transform); var br = Rt(bar); br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0); br.pivot = new Vector2(0.5f, 0); br.offsetMin = new Vector2(0, 8); br.offsetMax = new Vector2(0, 108);
                Img(bar, th.segBar, true, 2.4f);
                var hl = bar.AddComponent<HorizontalLayoutGroup>(); hl.padding = new RectOffset(10, 10, 10, 10); hl.spacing = 8; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = true; hl.childForceExpandHeight = true; hl.reverseArrangement = true;
                var buttons = new Button[opts.Length];
                for (int i = 0; i < opts.Length; i++)
                {
                    var o = Go("Opt" + i, bar.transform); var oi = Img(o, th.segOn, true, 2.6f, true); oi.color = new Color(1, 1, 1, 0);
                    var ob = o.AddComponent<Button>(); ob.targetGraphic = oi; ob.transition = Selectable.Transition.None; buttons[i] = ob;
                    var ot = Txt(o.transform, "T", opts[i], th.fontHeading, 32, th.ivoryDim); Stretch(Rt(ot.gameObject), 4, 6, 4, 8);
                }
                var comp = bar.AddComponent<KitSegmented>();
                var so = new SerializedObject(comp); var arr = so.FindProperty("options"); arr.arraySize = opts.Length;
                for (int i = 0; i < opts.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                so.FindProperty("selected").intValue = selected; so.ApplyModifiedPropertiesWithoutUndo();
                comp.Paint();
                return comp;
            }

            GameObject LinkRow(RectTransform parent, string title, string extra)
            {
                var row = Go("Link_" + title, parent); Rt(row).sizeDelta = new Vector2(940, 96);
                row.AddComponent<LayoutElement>().preferredHeight = 96;
                var img = row.AddComponent<Image>(); img.color = new Color(1, 1, 1, 0.04f);
                var t = Txt(row.transform, "T", title, th.fontBody, 36, th.leatherText, TextAlignmentOptions.MidlineRight); Place(Rt(t.gameObject), new Vector2(1, 0.5f), new Vector2(-320, 0), new Vector2(600, 56));
                if (extra != null) { var e = Txt(row.transform, "E", extra, th.fontBody, 26, th.ivoryDim, TextAlignmentOptions.MidlineLeft); Place(Rt(e.gameObject), new Vector2(0, 0.5f), new Vector2(190, 0), new Vector2(300, 40)); }
                var ch = Go("Arrow", row.transform); Place(Rt(ch), new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(36, 36)); var chi = Img(ch, arrow); chi.color = th.ivoryDim; ch.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                return row;
            }

            RectTransform c;
            // ---- voice (radio panel)
            MakeSection("الصوت", "المايك والسماعة والكتم", th.iconMic, true, out c);
            {
                var panel = Go("RadioPanel", c); Rt(panel).sizeDelta = new Vector2(940, 700);
                panel.AddComponent<LayoutElement>().preferredHeight = 700;
                var pi = Img(panel, th.leatherDark, true, 1f); pi.color = new Color(1.25f, 0.95f, 0.8f, 1f);
                KitKnob MakeKnob(string name, string label, float x, int start, out ArabicText val)
                {
                    var d = Go("Dial_" + name, panel.transform); Place(Rt(d), new Vector2(0.5f, 1), new Vector2(x, -170), new Vector2(250, 250));
                    var hit = d.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
                    var tk = Go("Ticks", d.transform); Stretch(Rt(tk)); Img(tk, th.dialTicks);
                    var fc = Go("Face", d.transform); Stretch(Rt(fc)); Img(fc, th.knobFace);
                    var pt = Go("Pointer", fc.transform); var pr = Rt(pt); pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0.5f, 0f); pr.sizeDelta = new Vector2(12, 74); pr.anchoredPosition = Vector2.zero;
                    var pim = pt.AddComponent<Image>(); pim.color = new Color32(0x1A, 0x0F, 0x08, 255); pim.raycastTarget = false;
                    var vb = Go("ValueBox", panel.transform); PlaceTop(Rt(vb), x, -320, 190, 72); Img(vb, th.slotDark, true, 2f);
                    var vt = Txt(vb.transform, "V", "٧٠٪", th.fontHeading, 46, amber); Stretch(Rt(vt.gameObject), 4, 4, 4, 8);
                    var lt = Txt(panel.transform, "L", label, th.fontHeading, 34, th.leatherText); PlaceTop(Rt(lt.gameObject), x, -398, 260, 46);
                    var k = d.AddComponent<KitKnob>(); SetRef(k, "pointer", pr); SetRef(k, "valueLabel", vt.GetComponent<ArabicText>());
                    var so = new SerializedObject(k); so.FindProperty("value").intValue = start; so.ApplyModifiedPropertiesWithoutUndo(); k.Set(start);
                    val = vt.GetComponent<ArabicText>();
                    return k;
                }
                micKnob = MakeKnob("Mic", "المايك", 230, 50, out _);
                spkKnob = MakeKnob("Speaker", "السماعة", -230, 50, out _);
                var vu = Go("Meter", panel.transform); PlaceTop(Rt(vu), 0, -470, 880, 76); Img(vu, th.slotDark, true, 2f);
                meter = new Image[24];
                for (int i = 0; i < 24; i++)
                {
                    var b = Go("Bar" + i, vu.transform); Place(Rt(b), new Vector2(0.5f, 0.5f), new Vector2(-(11.5f - i) * 34.5f, 0), new Vector2(24, 40));
                    var bi = b.AddComponent<Image>(); bi.color = new Color32(0x22, 0x22, 0x22, 255); bi.raycastTarget = false; meter[i] = bi;
                }
                var mt = InstChild("KitButton_Brass", panel.transform, "MicTest");
                Place(Rt(mt), new Vector2(0.5f, 1), new Vector2(0, -610), new Vector2(520, 160)); mt.transform.localScale = Vector3.one * 0.8f;
                micTest = mt.GetComponent<Button>(); micTestLbl = mt.GetComponentInChildren<ArabicText>(); micTestLbl.Text = "جرّب المايك";
                micTestLbl.GetComponent<TextMeshProUGUI>().fontSize = 48;
            }
            var sensSeg = SegRow(c, "حساسية المايك", new[] { "عالية", "متوسطة", "منخفضة" }, 1);
            var noiseTg = ToggleRow(c, "عزل الضجيج", "يخفف أصوات الخلفية", true);
            var pttTg = ToggleRow(c, "الضغط للتحدث", "المايك مغلق حتى تضغط الزر", false);
            var muteJoinTg = ToggleRow(c, "كتم المايك عند الدخول", "تدخل الروم صامتاً", false);
            var narratorTg = ToggleRow(c, "صوت الراوي", "تعليق الليل والنهار", true);

            MakeSection("الإشعارات", "ما يصلك وما لا يصلك", th.iconGift, false, out c);
            ToggleRow(c, "دعوات الغرف", null, true); ToggleRow(c, "طلبات الصداقة", null, true);
            ToggleRow(c, "أصدقائي دخلوا غرفة", "تنبيه عند دخول صديق", false); ToggleRow(c, "هدية أو جائزة تصنيف", null, true); ToggleRow(c, "عروض المتجر", null, false);

            MakeSection("الخصوصية والأمان", "من يقدر يوصلك", th.navIcons[3], false, out c);
            SegRow(c, "من يستطيع دعوتك للغرف", new[] { "الجميع", "الأصدقاء", "لا أحد" }, 0);
            SegRow(c, "من يستطيع مراسلتك", new[] { "الجميع", "الأصدقاء", "لا أحد" }, 1);
            ToggleRow(c, "إخفاء رتبتي وإحصائياتي", "تظهر \"مخفي\" لغير الأصدقاء", false); ToggleRow(c, "أظهر أني متصل", "يراك أصدقاؤك بالقائمة", true);
            LinkRow(c, "اللاعبون المحظورون", "٣"); LinkRow(c, "بلاغاتي", "٢ قيد المراجعة");

            MakeSection("العرض والأداء", "البطارية والحركة", th.navIcons[4], false, out c);
            SegRow(c, "جودة الرسوم", new[] { "منخفضة", "متوسطة", "عالية" }, 1);
            ToggleRow(c, "توفير البطارية", "يخفض الحركة وسرعة الرسم", false); ToggleRow(c, "تقليل الحركة", "يوقف التأرجح والومضات", false); ToggleRow(c, "الاهتزاز", null, true);

            MakeSection("الحساب", "الاسم واللغة والربط", th.avatarDefault, false, out c);
            LinkRow(c, "تغيير الاسم", "مرة كل ٣٠ يوماً"); LinkRow(c, "ربط الحساب (حفظ تقدمك)", "غير مربوط");
            var recoveryRow = LinkRow(c, RecoveryRowTitle, null); LinkRow(c, "استعادة المشتريات", null);
            SegRow(c, "اللغة", new[] { "العربية", "English" }, 0);

            MakeSection("المساعدة", "القواعد والدعم", th.levelSeal, false, out c);
            foreach (var t in new[] { "كيف تُلعب المافيا؟", "قواعد السهرة وآدابها", "تواصل مع الدعم", "الإبلاغ عن مشكلة", "الشروط والخصوصية", "عن اللعبة والفريق" }) LinkRow(c, t, null);

            // ---- logout + hold-to-delete + version
            var logout = InstChild("KitButton_Secondary", content.transform, "Logout");
            Rt(logout).sizeDelta = new Vector2(900, 150); logout.AddComponent<LayoutElement>().preferredHeight = 150;
            logout.GetComponentInChildren<ArabicText>().Text = "تسجيل الخروج";
            var del = Go("DeleteAccount", content.transform); Rt(del).sizeDelta = new Vector2(900, 130);
            del.AddComponent<LayoutElement>().preferredHeight = 130;
            var di = del.AddComponent<Image>(); di.color = new Color32(0x1A, 0x08, 0x09, 255);
            var dfill = Go("Fill", del.transform); var dfr = Rt(dfill); dfr.anchorMin = new Vector2(1, 0); dfr.anchorMax = new Vector2(1, 1); dfr.offsetMin = dfr.offsetMax = Vector2.zero;
            var dfi = dfill.AddComponent<Image>(); dfi.color = new Color(0.75f, 0.2f, 0.23f, 0.7f); dfi.raycastTarget = false;
            var dframe = Go("Frame", del.transform); Stretch(Rt(dframe)); var dfm = Img(dframe, th.stampFrame, true, 1f); dfm.color = new Color32(0x8A, 0x1C, 0x24, 255); dfm.raycastTarget = false;
            var dt = Txt(del.transform, "Label", "حذف الحساب: اضغط مطولاً ٣ ثوانٍ", th.fontHeading, 36, new Color32(0xE6, 0xA0, 0xA4, 255)); Stretch(Rt(dt.gameObject), 10, 6, 10, 10);
            var hold = del.AddComponent<KitHoldButton>(); SetRef(hold, "fill", dfr); SetRef(hold, "label", dt.GetComponent<ArabicText>());
            var ver = Go("Version", content.transform); Rt(ver).sizeDelta = new Vector2(900, 60); ver.AddComponent<LayoutElement>().preferredHeight = 60;
            var vtx = Txt(ver.transform, "T", "المافيا · الإصدار ١٫٠٫٠ · الحساب ٠٤٢٧٧١", th.fontBody, 26, new Color32(0x5F, 0x58, 0x4D, 255)); Stretch(Rt(vtx.gameObject), 0, 0, 0, 0);

            var ss = screen.AddComponent<KitSettingsScreen>();
            var sso = new SerializedObject(ss);
            var sa = sso.FindProperty("sections"); sa.arraySize = sections.Count; for (int i = 0; i < sections.Count; i++) sa.GetArrayElementAtIndex(i).objectReferenceValue = sections[i];
            var ma = sso.FindProperty("meter"); ma.arraySize = meter.Length; for (int i = 0; i < meter.Length; i++) ma.GetArrayElementAtIndex(i).objectReferenceValue = meter[i];
            sso.ApplyModifiedPropertiesWithoutUndo();
            SetRef(ss, "micKnob", micKnob); SetRef(ss, "speakerKnob", spkKnob); SetRef(ss, "micTestButton", micTest); SetRef(ss, "micTestLabel", micTestLbl);
            SetRef(ss, "sensitivity", sensSeg); SetRef(ss, "noiseToggle", noiseTg); SetRef(ss, "pttToggle", pttTg); SetRef(ss, "muteJoinToggle", muteJoinTg); SetRef(ss, "narratorToggle", narratorTg);

            foreach (var s in sections) { var o = new SerializedObject(s); s.SetOpen(o.FindProperty("openAtStart").boolValue, false); }
            BuildRecoverySheet(th);
            AddRecoverySheet(recoveryRow, canvasGo.transform);
            AddScreenHeader(canvasGo, th, "الإعدادات", "الإصدار ١٫٠٫٠", false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(cRt);
            SaveScene(SettingsScenePath);
        }
    }
}
