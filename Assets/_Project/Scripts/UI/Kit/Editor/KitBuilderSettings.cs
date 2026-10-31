using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    public static partial class KitBuilder
    {
        private const string SettingsScenePath = "Assets/_Project/Scenes/UIKitSettings.unity";

        private const string SettingsArt = "Assets/_Project/Art/UI/Settings/";

        /// <summary>Imports a settings sprite exported by Docs/mockups/export-settings.js: texels per CSS px; border (CSS px, padding included) for sliced ones.</summary>
        private static Sprite SetSpr(string name, float texPerCss, float l = 0f, float b = 0f, float r = 0f, float t = 0f)
        {
            string path = SettingsArt + name + ".png";
            AssetDatabase.ImportAsset(path);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true; ti.filterMode = FilterMode.Bilinear; ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 4096; ti.npotScale = TextureImporterNPOTScale.None;
                ti.spritePixelsPerUnit = 100f * texPerCss / NK;
                ti.spriteBorder = new Vector4(l, b, r, t) * texPerCss;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>A sprite image behind something, sticking out by <paramref name="grow"/> CSS px on every side (the shadow room of the sprite).</summary>
        private static Image Backing(Transform parent, string name, Sprite s, float growCss, bool sliced = true)
        {
            var g = Go(name, parent); g.transform.SetAsFirstSibling();
            g.AddComponent<LayoutElement>().ignoreLayout = true;
            float e = -growCss * NK; Stretch(Rt(g), e, e, e, e);
            var i = g.AddComponent<Image>(); i.sprite = s; i.type = sliced ? Image.Type.Sliced : Image.Type.Simple; i.raycastTarget = false; i.color = Color.white;
            return i;
        }

        private static void BuildSettings(KitTheme th)
        {
            var arrow = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
            var soft = new Color32(0x6A, 0x5A, 0x3A, 255);
            var amber = new Color32(0xFF, 0xB3, 0x5A, 255);

            var canvasGo = NewAppCanvas(th, new Color(0.075f, 0.055f, 0.045f));
            var pageBg = canvasGo.transform.Find("Background");
            if (pageBg != null) { var pbi = pageBg.GetComponent<Image>(); if (pbi != null) { pbi.sprite = SetSpr("set_bg", 2f); pbi.color = Color.white; pbi.type = Image.Type.Simple; } }
            var screen = Go("SettingsScreen", canvasGo.transform); Stretch(Rt(screen));
            var scrollGo = Go("Scroll", screen.transform);
            var sr = Rt(scrollGo); sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = new Vector2(0, 0); sr.offsetMax = new Vector2(0, -150);
            var scroll = scrollGo.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 40f;
            var vp = Go("Viewport", scrollGo.transform); Stretch(Rt(vp)); vp.AddComponent<RectMask2D>(); vp.AddComponent<Image>().color = Color.clear;
            var content = Go("Content", vp.transform);
            var cRt = Rt(content); cRt.anchorMin = new Vector2(0, 1); cRt.anchorMax = new Vector2(1, 1); cRt.pivot = new Vector2(0.5f, 1); cRt.anchoredPosition = Vector2.zero; cRt.sizeDelta = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>(); vlg.spacing = 33; vlg.padding = new RectOffset(0, 0, 26, 90); vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = false; vlg.childControlHeight = true; vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = Rt(vp); scroll.content = cRt;

            // ---- the ID card (settings.html ".me"): paper, ribbon, avatar, name and rank, the id number pill. The live values come from AccountIdFlow.
            {
                var me = Go("IdStrip", content.transform); Rt(me).sizeDelta = new Vector2(1000, 76 * NK);
                me.AddComponent<LayoutElement>().preferredHeight = 76 * NK;
                me.transform.localRotation = Quaternion.Euler(0f, 0f, 0.8f);
                Backing(me.transform, "Paper", SetSpr("set_me", 4f, 30, 36, 56, 36), 16f);
                var av = InstChild("AvatarFrame", me.transform, "Avatar"); Place(Rt(av), new Vector2(1, 0.5f), new Vector2(-(12f + 27.4f) * NK, 0), new Vector2(54.75f * NK, 54.75f * NK));
                var ink = new Color32(0x1C, 0x17, 0x0F, 255); var inkSoft = new Color32(0x6A, 0x5A, 0x3A, 255);
                var nm = Txt(me.transform, "Name", "لاعب", FindFont("amiri") ?? th.fontBody, 58, ink, TextAlignmentOptions.MidlineRight); Place(Rt(nm.gameObject), new Vector2(1, 0.5f), new Vector2(-(80f * NK) - 260f, 12f * NK), new Vector2(520, 70));
                var sb = Txt(me.transform, "Sub", "", th.fontBody, 28, inkSoft, TextAlignmentOptions.MidlineRight); sb.fontStyle = FontStyles.Bold; Place(Rt(sb.gameObject), new Vector2(1, 0.5f), new Vector2(-(80f * NK) - 260f, -16f * NK), new Vector2(520, 44));
                var idl = Txt(me.transform, "L", "رقم الهوية", th.fontBody, 24, inkSoft, TextAlignmentOptions.Center); idl.fontStyle = FontStyles.Bold; Place(Rt(idl.gameObject), new Vector2(0, 0.5f), new Vector2(41.6f * NK, 8.4f * NK), new Vector2(200, 40));
                var pill = Go("Pill", me.transform); Place(Rt(pill), new Vector2(0, 0.5f), new Vector2(41.6f * NK, -15f * NK), new Vector2(54.3f * NK, 27.8f * NK));
                var pi2 = pill.AddComponent<Image>(); pi2.sprite = SetSpr("set_idc", 6f, 16, 16, 16, 16); pi2.type = Image.Type.Sliced; pi2.raycastTarget = false;
                var idv = Txt(me.transform, "V", "٠٤٢٧٧١", th.fontHeading, 38, ink, TextAlignmentOptions.Center); Place(Rt(idv.gameObject), new Vector2(0, 0.5f), new Vector2(41.6f * NK, -15f * NK), new Vector2(54.3f * NK, 60));
            }

            var sections = new System.Collections.Generic.List<KitSettingsSection>();
            KitKnob micKnob = null, spkKnob = null; Image[] meter = null; Button micTest = null; ArabicText micTestLbl = null;

            KitSettingsSection MakeSection(string title, string sub, Sprite icon, bool open, out RectTransform contentRt)
            {
                var root = Go("Section_" + title, content.transform); Rt(root).sizeDelta = new Vector2(1000, 140);
                var rv = root.AddComponent<VerticalLayoutGroup>(); rv.childControlWidth = false; rv.childControlHeight = true; rv.childForceExpandWidth = false; rv.childForceExpandHeight = false; rv.spacing = 0;
                Backing(root.transform, "Leather", SetSpr("set_sec", 4f, 28, 28, 28, 28), 14f);
                var hd = Go("Header", root.transform); Rt(hd).sizeDelta = new Vector2(1000, 54 * NK);
                hd.AddComponent<LayoutElement>().preferredHeight = 54 * NK;
                var hi = hd.AddComponent<Image>(); hi.color = Color.clear;
                var hb = hd.AddComponent<Button>(); hb.targetGraphic = hi; hb.transition = Selectable.Transition.None;
                var ic = Go("Icon", hd.transform); Place(Rt(ic), new Vector2(1, 0.5f), new Vector2(-(14f + 15f) * NK, 0), new Vector2(30 * NK, 30 * NK)); Img(ic, icon);
                var tt = Txt(hd.transform, "Title", title, th.fontHeading, 46, th.leatherText, TextAlignmentOptions.MidlineRight); Place(Rt(tt.gameObject), new Vector2(1, 0.5f), new Vector2(-(14f + 30f + 10f) * NK - 310f, 8f), new Vector2(620, 60));
                var ss = Txt(hd.transform, "Sub", sub, th.fontBody, 26, th.ivoryDim, TextAlignmentOptions.MidlineRight); Place(Rt(ss.gameObject), new Vector2(1, 0.5f), new Vector2(-(14f + 30f + 10f) * NK - 310f, -34f), new Vector2(620, 40));
                var chev = Go("Chevron", hd.transform); Place(Rt(chev), new Vector2(0, 0.5f), new Vector2((14f + 9f) * NK, 0), new Vector2(18 * NK, 18 * NK)); var ci = Img(chev, arrow); ci.color = th.brass;
                var body = Go("Body", root.transform); Rt(body).sizeDelta = new Vector2(1000, 0);
                var bl = body.AddComponent<LayoutElement>(); bl.preferredHeight = 0;
                body.AddComponent<RectMask2D>();
                var ct = Go("Content", body.transform); contentRt = Rt(ct);
                contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1); contentRt.pivot = new Vector2(0.5f, 1); contentRt.anchoredPosition = Vector2.zero; contentRt.sizeDelta = new Vector2(0, 100);
                var cv = ct.AddComponent<VerticalLayoutGroup>(); cv.padding = new RectOffset(30, 30, 22, 26); cv.spacing = 10; cv.childControlWidth = true; cv.childControlHeight = true; cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;
                ct.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var lineGo = Go("OpenLine", hd.transform); var lr = Rt(lineGo); lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 0); lr.pivot = new Vector2(0.5f, 0); lr.offsetMin = new Vector2(14 * NK, 0); lr.offsetMax = new Vector2(-14 * NK, 3);
                var lineImg = lineGo.AddComponent<Image>(); lineImg.sprite = SetSpr("set_secline", 6f); lineImg.type = Image.Type.Simple; lineImg.preserveAspect = false; lineImg.raycastTarget = false;
                var sec = root.AddComponent<KitSettingsSection>();
                SetRef(sec, "openLine", lineGo);
                SetRef(sec, "header", hb); SetRef(sec, "body", Rt(body)); SetRef(sec, "content", contentRt); SetRef(sec, "chevron", Rt(chev)); SetRef(sec, "bodyLayout", bl);
                var so = new SerializedObject(sec); so.FindProperty("openAtStart").boolValue = open; so.ApplyModifiedPropertiesWithoutUndo();
                sections.Add(sec);
                return sec;
            }

            KitToggle ToggleRow(RectTransform parent, string title, string sub, bool on)
            {
                var row = Go("Toggle_" + title, parent); Rt(row).sizeDelta = new Vector2(940, sub == null ? 100 : 118);
                row.AddComponent<LayoutElement>().preferredHeight = sub == null ? 100 : 118;
                var t = Txt(row.transform, "T", title, th.fontBody, 36, th.leatherText, TextAlignmentOptions.MidlineRight); t.fontStyle = FontStyles.Bold; Place(Rt(t.gameObject), new Vector2(1, 0.5f), new Vector2(-330, sub == null ? 0 : 22), new Vector2(620, 50));
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
                    ot.enableAutoSizing = true; ot.fontSizeMin = 18; ot.fontSizeMax = 32;
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
                var t = Txt(row.transform, "T", title, th.fontBody, 36, th.leatherText, TextAlignmentOptions.MidlineRight); t.fontStyle = FontStyles.Bold; Place(Rt(t.gameObject), new Vector2(1, 0.5f), new Vector2(-320, 0), new Vector2(600, 56));
                if (extra != null) { var e = Txt(row.transform, "E", extra, th.fontBody, 26, th.ivoryDim, TextAlignmentOptions.MidlineLeft); Place(Rt(e.gameObject), new Vector2(0, 0.5f), new Vector2(190, 0), new Vector2(300, 40)); }
                var ch = Go("Arrow", row.transform); Place(Rt(ch), new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(36, 36)); var chi = Img(ch, arrow); chi.color = th.ivoryDim; ch.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                return row;
            }

            RectTransform c;
            // ---- voice (radio panel)
            MakeSection("الصوت", "المايك والسماعة والكتم", th.iconMic, true, out c);
            {
                var panel = Go("RadioPanel", c); Rt(panel).sizeDelta = new Vector2(940, 283 * NK);
                panel.AddComponent<LayoutElement>().preferredHeight = 283 * NK;
                Backing(panel.transform, "Body", SetSpr("set_radio", 4f, 30, 30, 30, 30), 14f);
                KitKnob MakeKnob(string name, string label, float x, int start, out ArabicText val)
                {
                    var d = Go("Dial_" + name, panel.transform); PlaceTop(Rt(d), x, -14f * NK, 104f * NK, 104f * NK);
                    var hit = d.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
                    var art = Go("Art", d.transform); var ar = Rt(art); ar.anchorMin = Vector2.zero; ar.anchorMax = Vector2.one; ar.offsetMin = new Vector2(-12f * NK, -14f * NK); ar.offsetMax = new Vector2(12f * NK, 10f * NK);
                    var ai = art.AddComponent<Image>(); ai.sprite = SetSpr("set_dial", 5f); ai.type = Image.Type.Simple; ai.preserveAspect = false; ai.raycastTarget = false;
                    var pt = Go("Pointer", d.transform); var pr = Rt(pt); pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0.5f, 0.5f); pr.sizeDelta = Vector2.zero; pr.anchoredPosition = Vector2.zero;
                    var pb = Go("Bar", pt.transform); Place(Rt(pb), new Vector2(0.5f, 0.5f), new Vector2(0, 23f * NK), new Vector2(10f * NK, 26f * NK));
                    var pim = pb.AddComponent<Image>(); pim.sprite = SetSpr("set_ptr", 12f); pim.type = Image.Type.Simple; pim.raycastTarget = false;
                    var vb = Go("ValueBox", panel.transform); PlaceTop(Rt(vb), x, -124f * NK, 56f * NK, 26f * NK);
                    var vbg = Go("Bg", vb.transform); Stretch(Rt(vbg), -4f * NK, -4f * NK, -4f * NK, -4f * NK); var vbi = vbg.AddComponent<Image>(); vbi.sprite = SetSpr("set_val", 6f, 10, 10, 10, 10); vbi.type = Image.Type.Sliced; vbi.raycastTarget = false;
                    var vt = Txt(vb.transform, "V", "٧٠٪", th.fontHeading, 46, amber); Stretch(Rt(vt.gameObject), 4, 0, 4, 6);
                    var lt = Txt(panel.transform, "L", label, th.fontHeading, 34, th.leatherText); PlaceTop(Rt(lt.gameObject), x, -154f * NK, 260, 22f * NK);
                    var k = d.AddComponent<KitKnob>(); SetRef(k, "pointer", pr); SetRef(k, "valueLabel", vt.GetComponent<ArabicText>());
                    var so = new SerializedObject(k); so.FindProperty("value").intValue = start; so.ApplyModifiedPropertiesWithoutUndo(); k.Set(start);
                    val = vt.GetComponent<ArabicText>();
                    return k;
                }
                micKnob = MakeKnob("Mic", "المايك", 80f * NK, 50, out _);
                spkKnob = MakeKnob("Speaker", "السماعة", -80f * NK, 50, out _);
                var vu = Go("Meter", panel.transform); PlaceTop(Rt(vu), 0, -188f * NK, 314f * NK, 30f * NK);
                var vug = Go("Bg", vu.transform); Stretch(Rt(vug), -4f * NK, -4f * NK, -4f * NK, -4f * NK); var vui = vug.AddComponent<Image>(); vui.sprite = SetSpr("set_vu", 5f, 12, 12, 12, 12); vui.type = Image.Type.Sliced; vui.raycastTarget = false;
                meter = new Image[24];
                float barW = 26f, step = (314f * NK - 16f * NK) / 24f;
                for (int i = 0; i < 24; i++)
                {
                    var b = Go("Bar" + i, vu.transform); var br = Rt(b);
                    br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f); br.pivot = new Vector2(0.5f, 0f);
                    br.anchoredPosition = new Vector2((i - 11.5f) * step, 6f * NK); br.sizeDelta = new Vector2(barW, 18f * NK * 0.3f);
                    var bi = b.AddComponent<Image>(); bi.color = new Color32(0x22, 0x22, 0x22, 255); bi.raycastTarget = false; meter[i] = bi;
                }
                var mt = Go("MicTest", panel.transform); PlaceTop(Rt(mt), 0, -222f * NK, 314f * NK, 49f * NK);
                var mbg = Go("Bg", mt.transform); Stretch(Rt(mbg), -6f * NK, -6f * NK, -6f * NK, -6f * NK); var mbi = mbg.AddComponent<Image>(); mbi.sprite = SetSpr("set_mic", 5f, 20, 20, 20, 20); mbi.type = Image.Type.Sliced; mbi.raycastTarget = false;
                var mhit = mt.AddComponent<Image>(); mhit.color = Color.clear;
                micTest = mt.AddComponent<Button>(); micTest.targetGraphic = mhit; micTest.transition = Selectable.Transition.None;
                var mlt = Txt(mt.transform, "L", "جرّب المايك", th.fontBody, 44, new Color32(0x2A, 0x1A, 0x08, 255)); mlt.fontStyle = FontStyles.Bold; Stretch(Rt(mlt.gameObject), 6, 0, 6, 6);
                micTestLbl = mlt.GetComponent<ArabicText>();
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
            var fxSeg = SegRow(c, "تأثير التنقّل بين الشاشات", new[] { "ورقة تنحرق", "تلاشي بسيط", "بدون" }, 0);
            // preview strip: the old screen (paper) lies over the new one (dark leather) and burns away when tried
            var pv = Go("FxPreview", c); Rt(pv).sizeDelta = new Vector2(940, 190); pv.AddComponent<LayoutElement>().preferredHeight = 190;
            Img(pv, th.slotDark, true, 2f); pv.AddComponent<RectMask2D>();
            var pvNew = Go("New", pv.transform); Stretch(Rt(pvNew), 6, 6, 6, 6); Img(pvNew, th.leatherDark, true, 1f).color = new Color(1.1f, 0.8f, 0.8f, 1f);
            var pvNewT = Txt(pvNew.transform, "T", "الشاشة الجديدة", th.fontHeading, 40, th.brassLight); Stretch(Rt(pvNewT.gameObject), 10, 6, 10, 10);
            var pvOld = Go("Old", pv.transform); Stretch(Rt(pvOld), 6, 6, 6, 6); var pvOldI = pvOld.AddComponent<Image>(); pvOldI.color = new Color32(0xE9, 0xDC, 0xB8, 255); pvOldI.raycastTarget = false;
            var pvOldT = Txt(pvOld.transform, "T", "الشاشة القديمة", th.fontHeading, 40, new Color32(0x2A, 0x1A, 0x08, 255)); Stretch(Rt(pvOldT.gameObject), 10, 6, 10, 10);
            var tryBtn = InstChild("KitButton_Brass", c, "TryFx"); Rt(tryBtn).sizeDelta = new Vector2(940, 120); tryBtn.AddComponent<LayoutElement>().preferredHeight = 120;
            tryBtn.GetComponentInChildren<ArabicText>().Text = "جرّب التأثير"; tryBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 40;
            var fxHint = Txt(c, "FxHint", "", th.fontBody, 28, th.ivoryDim, TextAlignmentOptions.TopRight, true);
            ToggleRow(c, "توفير البطارية", "يخفض الحركة وسرعة الرسم", false); var reduceTg = ToggleRow(c, "تقليل الحركة", "يوقف التأرجح والومضات", false); ToggleRow(c, "الاهتزاز", null, true);

            MakeSection("الحساب", "الاسم واللغة والربط", th.avatarDefault, false, out c);
            LinkRow(c, "تغيير الاسم", "مرة كل ٣٠ يوماً"); LinkRow(c, "ربط الحساب (حفظ تقدمك)", "غير مربوط");
            var recoveryRow = LinkRow(c, RecoveryRowTitle, null); LinkRow(c, "استعادة المشتريات", null);
            var langSeg = SegRow(c, "اللغة", new[] { "العربية", "English" }, 0);

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
            SetRef(ss, "sensitivity", sensSeg); SetRef(ss, "language", langSeg); SetRef(ss, "noiseToggle", noiseTg); SetRef(ss, "pttToggle", pttTg); SetRef(ss, "muteJoinToggle", muteJoinTg); SetRef(ss, "narratorToggle", narratorTg);
            SetRef(ss, "fxSegment", fxSeg); SetRef(ss, "reduceMotionToggle", reduceTg); SetRef(ss, "fxPreviewButton", tryBtn.GetComponent<Button>()); SetRef(ss, "fxPreviewArea", Rt(pv)); SetRef(ss, "fxPreviewOld", pvOld); SetRef(ss, "fxHint", fxHint.GetComponent<ArabicText>());

            foreach (var s in sections) { var o = new SerializedObject(s); s.SetOpen(o.FindProperty("openAtStart").boolValue, false); }
            BuildRecoverySheet(th);
            AddRecoverySheet(recoveryRow, canvasGo.transform);
            AddScreenHeader(canvasGo, th, "الإعدادات", "الإصدار ١٫٠٫٠", false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(cRt);
            SaveScene(SettingsScenePath);
        }
    }
}
