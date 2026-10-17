using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    public static partial class KitBuilder
    {
        private const string LbScenePath = "Assets/_Project/Scenes/UIKitLeaderboards.unity";

        private static void BuildLeaderboards(KitTheme th)
        {
            var ink = th.paperInk; var gold = new Color32(0xFF, 0xD9, 0x8A, 255);

            // ---------- prefabs
            {
                var root = new GameObject("LbRow", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(980, 118);
                var bg = root.AddComponent<Image>(); bg.sprite = th.rowBg; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1.6f; bg.color = new Color(1, 1, 1, 0.012f); bg.raycastTarget = false;
                var le = root.AddComponent<LayoutElement>(); le.preferredWidth = 980; le.preferredHeight = 118;
                var pos = Txt(root.transform, "Pos", "٤", th.fontHeading, 44, th.leatherText); Place(Rt(pos.gameObject), new Vector2(0.5f, 0.5f), new Vector2(430, 0), new Vector2(90, 80));
                var av = InstChild("AvatarFrame", root.transform, "Avatar"); Place(Rt(av), new Vector2(0.5f, 0.5f), new Vector2(335, 0), new Vector2(94, 94));
                var nm = Txt(root.transform, "Name", "الاسم", th.fontHeading, 37, th.leatherText, TextAlignmentOptions.MidlineRight);
                Place(Rt(nm.gameObject), new Vector2(0.5f, 0.5f), new Vector2(72, 20), new Vector2(380, 52));
                var rk = Txt(root.transform, "Rank", "الجندي", th.fontHeading, 25, th.ivoryDim, TextAlignmentOptions.MidlineRight);
                Place(Rt(rk.gameObject), new Vector2(0.5f, 0.5f), new Vector2(72, -26), new Vector2(380, 36));
                var val = Txt(root.transform, "Value", "٢٬٥٠٠", th.fontHeading, 46, gold, TextAlignmentOptions.MidlineLeft);
                Place(Rt(val.gameObject), new Vector2(0.5f, 0.5f), new Vector2(-150, 0), new Vector2(260, 70));
                var arrow = Go("Arrow", root.transform); Place(Rt(arrow), new Vector2(0.5f, 0.5f), new Vector2(-312, 0), new Vector2(26, 26)); var ai = Img(arrow, th.triUp);
                var dl = Txt(root.transform, "Delta", "٣", th.fontHeading, 30, th.teal); Place(Rt(dl.gameObject), new Vector2(0.5f, 0.5f), new Vector2(-356, 0), new Vector2(60, 44));
                var v = root.AddComponent<LeaderRowView>();
                SetRef(v, "bg", bg); SetRef(v, "posLabel", pos.GetComponent<ArabicText>()); SetRef(v, "nameLabel", nm.GetComponent<ArabicText>());
                SetRef(v, "rankLabel", rk.GetComponent<ArabicText>()); SetRef(v, "valueLabel", val.GetComponent<ArabicText>()); SetRef(v, "deltaLabel", dl.GetComponent<ArabicText>());
                SetRef(v, "avatar", av.GetComponent<AvatarFrame>()); SetRef(v, "arrow", ai);
                Save(root, "LbRow");
            }
            {
                var root = new GameObject("LbChip", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(200, 76);
                var img = Img(root, th.chipOff, true, 1.6f, true); img.color = new Color(1f, 1f, 1f, 0.03f);
                var b = root.AddComponent<Button>(); b.targetGraphic = img; b.transition = Selectable.Transition.None;
                var le = root.AddComponent<LayoutElement>(); le.preferredWidth = 200; le.preferredHeight = 76;
                var t = Txt(root.transform, "Label", "النقاط", th.fontHeading, 33, th.ivoryDim); Stretch(Rt(t.gameObject), 6, 6, 6, 9);
                Save(root, "LbChip");
            }

            // ---------- scene
            var canvasGo = NewAppCanvas(th, new Color(0.075f, 0.055f, 0.045f));
            var screen = Go("LeaderboardsScreen", canvasGo.transform); Stretch(Rt(screen));
            var scrollGo = Go("Scroll", screen.transform);
            var sr = Rt(scrollGo); sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = new Vector2(0, 0); sr.offsetMax = new Vector2(0, -60);   // runs down to the screen edge; the "my rank" bar floats over it
            var scroll = scrollGo.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 40f;
            var vp = Go("Viewport", scrollGo.transform); Stretch(Rt(vp)); vp.AddComponent<RectMask2D>();
            vp.AddComponent<Image>().color = Color.clear;
            var content = Go("Content", vp.transform);
            var cRt = Rt(content); cRt.anchorMin = new Vector2(0, 1); cRt.anchorMax = new Vector2(1, 1); cRt.pivot = new Vector2(0.5f, 1); cRt.anchoredPosition = Vector2.zero; cRt.sizeDelta = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>(); vlg.spacing = 18; vlg.padding = new RectOffset(0, 0, 22, 250); vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = false; vlg.childControlHeight = false; vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = Rt(vp); scroll.content = cRt;

            // header: title, subtitle and the reset countdown (the HTML top strip)
            var hdr = Go("Header", content.transform); Rt(hdr).sizeDelta = new Vector2(1040, 120);
            hdr.AddComponent<LayoutElement>().preferredHeight = 120;
            var hTitle = Txt(hdr.transform, "Title", "غرفة التصنيفات", th.fontHeading, 64, th.ivory, TextAlignmentOptions.MidlineRight); Place(Rt(hTitle.gameObject), new Vector2(1, 0.5f), new Vector2(-440, 18), new Vector2(600, 76));
            var hSub = Txt(hdr.transform, "Sub", "من الأول؟ الليلة تُحسم", th.fontBody, 28, th.brass, TextAlignmentOptions.MidlineRight); Place(Rt(hSub.gameObject), new Vector2(1, 0.5f), new Vector2(-440, -34), new Vector2(600, 40));
            var backGo = Go("Back", hdr.transform); Place(Rt(backGo), new Vector2(1, 0.5f), new Vector2(-62, 0), new Vector2(92, 92));
            var backImg = Img(backGo, th.leatherDark, true, 1.4f, true);
            var backBtn = backGo.AddComponent<Button>(); backBtn.targetGraphic = backImg; backBtn.transition = Selectable.Transition.None;
            var backFx = backGo.AddComponent<KitButtonFx>(); SetRef(backFx, "face", Rt(backGo));
            var backArrow = Go("Arrow", backGo.transform); Place(Rt(backArrow), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34)); backArrow.transform.localRotation = Quaternion.Euler(0, 0, -90f);
            var baI = Img(backArrow, th.triUp); baI.color = th.leatherText; baI.raycastTarget = false;
            var resetPill = Go("ResetPill", hdr.transform); Place(Rt(resetPill), new Vector2(0, 0.5f), new Vector2(230, 0), new Vector2(440, 72)); Img(resetPill, th.chipOff, true, 1.4f).color = new Color(1f, 1f, 1f, 0.03f);
            var resetText = Txt(resetPill.transform, "T", "يتجدد خلال ٠٣:٤٢:١٠", th.fontBody, 28, new Color32(0xFF, 0xB3, 0x5A, 255)); Stretch(Rt(resetText.gameObject), 10, 4, 10, 4);

            // doors
            var doorsRow = Go("Doors", content.transform); Rt(doorsRow).sizeDelta = new Vector2(1040, 200);
            doorsRow.AddComponent<LayoutElement>().preferredHeight = 200;
            var dh = doorsRow.AddComponent<HorizontalLayoutGroup>(); dh.spacing = 22; dh.childAlignment = TextAnchor.MiddleCenter; dh.reverseArrangement = true;
            dh.childControlWidth = false; dh.childControlHeight = false; dh.childForceExpandWidth = false;
            var doors = new Button[3]; string[] dn = { "اليوم", "الأسبوع", "قاعة المشاهير" };
            Sprite[] ds = { th.doorD1, th.doorD2, th.doorD3 };
            Sprite[] di2 = { th.iconDay, th.iconWeek, th.iconWall };
            Color[] dc = { new Color32(0x8F, 0x87, 0x77, 255), new Color32(0x4A, 0x40, 0x30, 255), new Color32(0xC9, 0xA8, 0x6A, 255) };   // unselected label colours (selected ones live in the screen script)
            float[] dw = { 306f, 306f, 383f };
            for (int i = 0; i < 3; i++)
            {
                var slot = Go("DoorSlot" + i, doorsRow.transform); Rt(slot).sizeDelta = new Vector2(dw[i], 166);
                slot.AddComponent<LayoutElement>().preferredWidth = dw[i];
                var d = Go("Door" + i, slot.transform); Stretch(Rt(d));
                var di = Img(d, ds[i], true, 1f, true);
                var db = d.AddComponent<Button>(); db.targetGraphic = di; db.transition = Selectable.Transition.None; doors[i] = db;
                var dIcon = Go("Icon", d.transform); Place(Rt(dIcon), new Vector2(0.5f, 1f), new Vector2(0, -58), new Vector2(72, 72)); Img(dIcon, di2[i]).raycastTarget = false;
                var dt = Txt(d.transform, "Label", dn[i], th.fontHeading, 35, dc[i]); Place(Rt(dt.gameObject), new Vector2(0.5f, 0f), new Vector2(0, 38), new Vector2(dw[i] - 10f, 50));
                var ring = Go("Ring", d.transform); var rr2 = Rt(ring); rr2.anchorMin = Vector2.zero; rr2.anchorMax = Vector2.one; rr2.offsetMin = new Vector2(-5.5f, -5.5f); rr2.offsetMax = new Vector2(5.5f, 5.5f);
                var rimg = Img(ring, th.doorRing, true, 1f); rimg.color = new Color32(0xF5, 0xE2, 0xA8, 255); rimg.raycastTarget = false; ring.SetActive(false);
            }

            // chips
            var chipsRowGo = Go("ChipsRow", content.transform); Rt(chipsRowGo).sizeDelta = new Vector2(1080, 110);
            chipsRowGo.AddComponent<LayoutElement>().preferredHeight = 110;
            var csr = chipsRowGo.AddComponent<ScrollRect>(); csr.vertical = false; csr.movementType = ScrollRect.MovementType.Elastic;
            var cvp = Go("Viewport", chipsRowGo.transform); Stretch(Rt(cvp)); cvp.AddComponent<RectMask2D>(); cvp.AddComponent<Image>().color = Color.clear;
            var chipsRoot = Go("Chips", cvp.transform); var ccr = Rt(chipsRoot);
            ccr.anchorMin = new Vector2(1, 0); ccr.anchorMax = new Vector2(1, 1); ccr.pivot = new Vector2(1, 0.5f); ccr.sizeDelta = Vector2.zero; ccr.anchoredPosition = Vector2.zero;
            var chl = chipsRoot.AddComponent<HorizontalLayoutGroup>(); chl.spacing = 17; chl.padding = new RectOffset(30, 30, 10, 10); chl.childAlignment = TextAnchor.MiddleRight; chl.reverseArrangement = true;
            chl.childControlWidth = false; chl.childControlHeight = false; chl.childForceExpandWidth = false;
            chipsRoot.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csr.viewport = Rt(cvp); csr.content = ccr;

            // board (chalkboard / newspaper)
            var board = Go("BoardView", content.transform); Rt(board).sizeDelta = new Vector2(1010, 2280);
            board.AddComponent<LayoutElement>().preferredHeight = 2280;
            var boardBg = Img(board, th.boardDay, true, 1f, false);
            var title = Txt(board.transform, "Title", "سبورة الليلة · النقاط", th.fontCalligraphy, 56, new Color32(0xE9, 0xE6, 0xDC, 255), TextAlignmentOptions.MidlineRight);
            PlaceTop(Rt(title.gameObject), 80, -70, 700, 80);
            var unit = Txt(board.transform, "Unit", "نقطة", th.fontBody, 34, gold, TextAlignmentOptions.Midline); PlaceTop(Rt(unit.gameObject), -350, -70, 200, 80);
            // dashed line under the board title (HTML .bh) and the podium stands
            var dashLine = Go("HeaderLine", board.transform); PlaceTop(Rt(dashLine), 0, -158, 930, 4);
            var dashImg = Img(dashLine, th.dashH); dashImg.type = Image.Type.Tiled; dashImg.color = new Color(0.91f, 0.9f, 0.86f, 0.3f); dashImg.raycastTarget = false;
            float[] standH = { 116f, 72f, 44f }, standW = { 347f, 289f, 289f }, standTop = { -714f, -758f, -786f };
            for (int i = 0; i < 3; i++)
            {
                var st = Go("Stand" + (i + 1), board.transform); PlaceTop(Rt(st), new float[] { 0f, 310f, -310f }[i], standTop[i], standW[i], standH[i]);
                var sti = Img(st, th.rowBg, true, 1.6f); sti.color = new Color(1f, 1f, 1f, 0.01f);
                var hl = Go("Hl", st.transform); var hlr = Rt(hl); hlr.anchorMin = new Vector2(0, 1); hlr.anchorMax = Vector2.one; hlr.pivot = new Vector2(0.5f, 1); hlr.offsetMin = new Vector2(4, -4); hlr.offsetMax = new Vector2(-4, 0); var hli = hl.AddComponent<Image>(); hli.color = new Color(1f, 1f, 1f, 0.025f); hli.raycastTarget = false; sti.raycastTarget = false;
            }
            var reset = resetText;   // the countdown lives in the header pill

            var slots = new LeaderPodiumSlot[3];
            float[] sx = { 0, 310, -310 }; float[] top = { -230, -334, -362 }; float[] avSize = { 232, 172, 172 };
            for (int i = 0; i < 3; i++)
            {
                var slot = Go("Podium" + (i + 1), board.transform); PlaceTop(Rt(slot), sx[i], top[i], 300, 560);
                var crown = Go("Crown", slot.transform); PlaceTop(Rt(crown), 0, 14, 90, 90); Img(crown, th.giftCrown);
                                var a = InstChild("AvatarFrame", slot.transform, "Avatar"); PlaceTop(Rt(a), 0, -70, avSize[i], avSize[i]);
                var apic = a.transform.Find("Avatar"); if (apic != null) { var ar2 = (RectTransform)apic; float ins = avSize[i] * 0.075f; ar2.offsetMin = new Vector2(ins, ins); ar2.offsetMax = new Vector2(-ins, -ins); }
                var seal = Go("Place", slot.transform); PlaceTop(Rt(seal), 0, -70 - avSize[i] + 30, 64, 64); Img(seal, th.levelSeal);
                var pt = Txt(seal.transform, "N", "١", th.fontHeading, 34, th.brassText); Stretch(Rt(pt.gameObject), 0, 3, 0, 0);
                var n = Txt(slot.transform, "Name", "الاسم", th.fontHeading, 36, th.leatherText); PlaceTop(Rt(n.gameObject), 0, -70 - avSize[i] - 36, 300, 50);
                var vv = Txt(slot.transform, "Value", "٠", th.fontHeading, 44, gold); PlaceTop(Rt(vv.gameObject), 0, -70 - avSize[i] - 92, 300, 56);
                var r = Txt(slot.transform, "Rank", "", th.fontBody, 26, th.ivoryDim); PlaceTop(Rt(r.gameObject), 0, -70 - avSize[i] - 146, 300, 36);
                var ps = slot.AddComponent<LeaderPodiumSlot>();
                SetRef(ps, "avatar", a.GetComponent<AvatarFrame>()); SetRef(ps, "nameLabel", n.GetComponent<ArabicText>()); SetRef(ps, "valueLabel", vv.GetComponent<ArabicText>());
                SetRef(ps, "rankLabel", r.GetComponent<ArabicText>()); SetRef(ps, "placeLabel", pt.GetComponent<ArabicText>()); SetRef(ps, "crown", crown);
                slots[i] = ps;
            }
            var rowsRoot = Go("Rows", board.transform); PlaceTop(Rt(rowsRoot), 0, -852, 980, 1230);
            var rvl = rowsRoot.AddComponent<VerticalLayoutGroup>(); rvl.spacing = 8; rvl.childAlignment = TextAnchor.UpperCenter;
            rvl.childControlWidth = false; rvl.childControlHeight = false; rvl.childForceExpandWidth = false; rvl.childForceExpandHeight = false;
            var rewardBg = Go("RewardBg", board.transform); PlaceTop(Rt(rewardBg), 0, -2135, 920, 110); Img(rewardBg, th.slotDark, true, 1.4f).raycastTarget = false;
            var reward = Txt(board.transform, "Reward", "جائزة", th.fontBody, 30, gold, TextAlignmentOptions.Center, true); PlaceTop(Rt(reward.gameObject), 0, -2145, 880, 90);

            // hall of fame (HTML .wall): lamps with a cone of light, gold frames, brass plaques, record cards
            var wall = Go("WallView", content.transform); Rt(wall).sizeDelta = new Vector2(1080, 2650);
            wall.AddComponent<LayoutElement>().preferredHeight = 2650;
            var wallBg = wall.AddComponent<Image>(); wallBg.sprite = th.wallBg; wallBg.type = Image.Type.Simple; wallBg.raycastTarget = false;
            var wLines = Go("Lines", wall.transform); Stretch(Rt(wLines)); var wli = Img(wLines, th.wallLines); wli.type = Image.Type.Tiled; wli.preserveAspect = false; wli.raycastTarget = false;
            var wVig = Go("Vignette", wall.transform); Stretch(Rt(wVig)); var wvi = Img(wVig, th.wallVignette, true, 1f); wvi.raycastTarget = false;
            var wBase = Go("Baseboard", wall.transform); var wbr = Rt(wBase); wbr.anchorMin = new Vector2(0, 0); wbr.anchorMax = new Vector2(1, 0); wbr.pivot = new Vector2(0.5f, 0); wbr.offsetMin = Vector2.zero; wbr.offsetMax = new Vector2(0, 50);
            var wbi = wBase.AddComponent<Image>(); wbi.color = new Color32(0x2B, 0x19, 0x0D, 255); wbi.raycastTarget = false;
            var wBaseLine = Go("BaseLine", wBase.transform); var wbl = Rt(wBaseLine); wbl.anchorMin = new Vector2(0, 1); wbl.anchorMax = Vector2.one; wbl.pivot = new Vector2(0.5f, 1); wbl.offsetMin = Vector2.zero; wbl.offsetMax = new Vector2(0, 0); wbl.sizeDelta = new Vector2(0, 4);
            var wbli = wBaseLine.AddComponent<Image>(); wbli.color = new Color32(0x6A, 0x4A, 0x2A, 255); wbli.raycastTarget = false;
            void Heading(string text, float y)
            {
                var h = Txt(wall.transform, "H", text, th.fontHeading, 46, th.brassLight); PlaceTop(Rt(h.gameObject), 0, y, 520, 90);
                h.ForceMeshUpdate();
                float half = h.preferredWidth * 0.5f + 34f;                 // the rules run from the text out to the wall edges (HTML flex:1)
                float lineW = 500f - half, lineC = half + lineW * 0.5f;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    var ln = Go("Line", wall.transform); PlaceTop(Rt(ln), sgn * lineC, y - 44f, lineW, 4);
                    var li = Img(ln, th.hairGrad); li.raycastTarget = false; li.preserveAspect = false;
                    ln.transform.localScale = new Vector3(sgn < 0 ? 1f : -1f, 1f, 1f);   // the line fades out towards the screen edge
                }
            }
            int hangCount = 0;
            void Framed(string name, string sub, float x, float y, float w, float hgt, float plaqueW)
            {
                var hang = Go("Hang", wall.transform); PlaceTop(Rt(hang), x, y, Mathf.Max(w, plaqueW), hgt + 14 + 110);
                var sway = hang.AddComponent<KitSway>(); var swo = new SerializedObject(sway); swo.FindProperty("phase").floatValue = (hangCount++ % 2 == 0) ? 0f : -3f; swo.ApplyModifiedPropertiesWithoutUndo();
                // light: cone (behind everything), lamp cap and the cord it hangs from
                var cone = Go("Cone", hang.transform); PlaceTop(Rt(cone), 0, 92, 360, 415); Img(cone, th.lampCone).raycastTarget = false;
                var cord = Go("Cord", hang.transform); PlaceTop(Rt(cord), 0, 84, 5, 84); var cordImg = cord.AddComponent<Image>(); cordImg.color = new Color32(0xB9, 0x90, 0x48, 255); cordImg.raycastTarget = false;
                var cap = Go("Lamp", hang.transform); PlaceTop(Rt(cap), 0, 128, 150, 33); Img(cap, th.lampCap).raycastTarget = false;
                var f = Go("Frame", hang.transform); PlaceTop(Rt(f), 0, 0, w, hgt); Img(f, th.goldFrame, true, 1f);
                var pic = Go("Pic", f.transform); Stretch(Rt(pic), 26, 26, 26, 26); Img(pic, th.picBg).raycastTarget = false;
                var sil = Go("Fedora", pic.transform); float sw = (w - 52f) * 0.84f; Place(Rt(sil), new Vector2(0.5f, 0f), new Vector2(0, 0), new Vector2(sw, sw)); Rt(sil).pivot = new Vector2(0.5f, 0f); Rt(sil).anchoredPosition = Vector2.zero;
                var silImg = Img(sil, th.fedSil); silImg.color = new Color32(0xA8, 0x9A, 0x7C, 255); silImg.raycastTarget = false;
                var pl = Go("Plaque", hang.transform); PlaceTop(Rt(pl), 0, -hgt - 14, plaqueW, 110); Img(pl, th.plaqueGold, true, 1.2f);
                var pn = Txt(pl.transform, "N", name, th.fontHeading, 38, th.brassText); Place(Rt(pn.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, 17), new Vector2(plaqueW - 30, 50));
                var ps = Txt(pl.transform, "S", sub, th.fontHeading, 24, new Color32(0x4A, 0x32, 0x12, 255)); Place(Rt(ps.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, -22), new Vector2(plaqueW - 30, 34));
            }
            Heading("العرّاب", -40);
            Framed("أبو فارس", "العرّاب الحالي · ١٨٬٤٠٠ نقطة", 0, -300, 366, 445, 470);
            Heading("أبطال المواسم", -940);
            Framed("الحج حسن", "الموسم الثالث", 345, -1202, 240, 296, 250);
            Framed("أم سعيد", "الموسم الثاني", 0, -1202, 240, 296, 250);
            Framed("البيك", "الموسم الأول", -345, -1202, 240, 296, 250);
            Heading("الأرقام القياسية", -1690);
            string[,] recs = { { "أطول سلسلة فوز", "٢٢", "أبو خليل" }, { "أكثر هدايا مستلمة", "٤٬٨١٠", "لينا" }, { "أطول جلسة صوتية", "١٤ ساعة", "سارة" }, { "أكبر غرفة مستضافة", "١٦ لاعباً", "مازن" }, { "أسرع وصول للعرّاب", "١٩ يوماً", "الدكتور" }, { "أكثر كشف صحيح", "٣١٢", "نور" } };
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 2 == 0 ? 250 : -250), y = -1810 - (i / 2) * 254;
                var rc = Go("Rec" + i, wall.transform); PlaceTop(Rt(rc), x, y, 470, 232); Img(rc, th.recCard, true, 1f).raycastTarget = false;
                var l1 = Txt(rc.transform, "L", recs[i, 0], th.fontHeading, 28, new Color32(0xA9, 0x8F, 0x62, 255)); Place(Rt(l1.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, 72), new Vector2(430, 40));
                var l2 = Txt(rc.transform, "V", recs[i, 1], th.fontHeading, 66, new Color32(0xF5, 0xE2, 0xA8, 255)); Place(Rt(l2.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(430, 90));
                var l3 = Txt(rc.transform, "H", recs[i, 2], th.fontHeading, 32, new Color32(0xD9, 0xD0, 0xBD, 255)); Place(Rt(l3.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, -68), new Vector2(430, 42));
            }
            wall.SetActive(false);

            // pinned "my rank" bar
            var mine = Go("MineBar", screen.transform);
            var mr = Rt(mine); mr.anchorMin = new Vector2(0, 0); mr.anchorMax = new Vector2(1, 0); mr.pivot = new Vector2(0.5f, 0); mr.offsetMin = new Vector2(30, 28); mr.offsetMax = new Vector2(-30, 168);
            Img(mine, th.mineBar, true, 1f, true);
            var mrank = Txt(mine.transform, "Rank", "#٤٢", th.fontHeading, 72, th.brassLight, TextAlignmentOptions.MidlineRight); Place(Rt(mrank.gameObject), new Vector2(1, 0.5f), new Vector2(-170, 20), new Vector2(280, 90));
            var mcap = Txt(mine.transform, "RankCaption", "ترتيبك", th.fontBody, 22, new Color32(0x8D, 0x74, 0x4A, 255), TextAlignmentOptions.MidlineRight); Place(Rt(mcap.gameObject), new Vector2(1, 0.5f), new Vector2(-170, -44), new Vector2(280, 30));
            var mneed = Txt(mine.transform, "Need", "تحتاج", th.fontBody, 28, th.leatherText, TextAlignmentOptions.MidlineRight); Place(Rt(mneed.gameObject), new Vector2(0.5f, 0.5f), new Vector2(-10, 22), new Vector2(560, 40));
            var mwall = Txt(mine.transform, "WallNote", "", th.fontCalligraphy, 34, new Color32(0xF2, 0xEC, 0xDC, 255), TextAlignmentOptions.MidlineRight, true); Place(Rt(mwall.gameObject), new Vector2(0.5f, 0.5f), new Vector2(-10, 24), new Vector2(580, 76)); mwall.lineSpacing = -8f;
            var mbar = Go("Bar", mine.transform); Place(Rt(mbar), new Vector2(0.5f, 0.5f), new Vector2(-10, -44), new Vector2(520, 16));
            var mbi = mbar.AddComponent<Image>(); mbi.color = new Color(0, 0, 0, 0.5f); mbi.raycastTarget = false;
            var mfill = Go("Fill", mbar.transform); var mfr = Rt(mfill); mfr.anchorMin = new Vector2(0.36f, 0); mfr.anchorMax = Vector2.one; mfr.offsetMin = mfr.offsetMax = Vector2.zero;
            var mfi = mfill.AddComponent<Image>(); mfi.sprite = th.barRed; mfi.color = Color.white; mfi.raycastTarget = false;
            var mval = Txt(mine.transform, "Value", "٥٧٦", th.fontHeading, 46, gold, TextAlignmentOptions.MidlineLeft); Place(Rt(mval.gameObject), new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(200, 70));

            var lb = screen.AddComponent<KitLeaderboardsScreen>();
            var so = new SerializedObject(lb);
            var da = so.FindProperty("doors"); da.arraySize = 3; for (int i = 0; i < 3; i++) da.GetArrayElementAtIndex(i).objectReferenceValue = doors[i];
            var pa = so.FindProperty("podium"); pa.arraySize = 3; for (int i = 0; i < 3; i++) pa.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            SetRef(lb, "backButton", backBtn); SetRef(lb, "mineBarTrack", Rt(mbar)); SetRef(lb, "mineCaption", mcap.GetComponent<ArabicText>()); SetRef(lb, "headerLine", dashImg); SetRef(lb, "boardView", board); SetRef(lb, "wallView", wall); SetRef(lb, "chipsRow", chipsRowGo); SetRef(lb, "boardBg", boardBg);
            SetRef(lb, "boardTitle", title.GetComponent<ArabicText>()); SetRef(lb, "boardUnit", unit.GetComponent<ArabicText>());
            SetRef(lb, "rewardLabel", reward.GetComponent<ArabicText>()); SetRef(lb, "resetLabel", reset.GetComponent<ArabicText>());
            SetRef(lb, "rowsRoot", Rt(rowsRoot)); SetRef(lb, "chipsRoot", ccr); SetRef(lb, "rowPrefab", LoadPrefab("LbRow")); SetRef(lb, "chipPrefab", LoadPrefab("LbChip"));
            SetRef(lb, "mineRank", mrank.GetComponent<ArabicText>()); SetRef(lb, "mineNeed", mneed.GetComponent<ArabicText>()); SetRef(lb, "mineValue", mval.GetComponent<ArabicText>());
            SetRef(lb, "mineFill", mfr); SetRef(lb, "mineBar", mine); SetRef(lb, "mineWall", mwall.GetComponent<ArabicText>());
            lb.Build();

            // no top bar or nav here: the room has its own header with a back button
            LayoutRebuilder.ForceRebuildLayoutImmediate(cRt);
            SaveScene(LbScenePath);
        }
    }
}
