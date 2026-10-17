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
                var nm = Txt(root.transform, "Name", "الاسم", th.fontBody, 38, th.leatherText, TextAlignmentOptions.MidlineRight);
                Place(Rt(nm.gameObject), new Vector2(0.5f, 0.5f), new Vector2(115, 20), new Vector2(380, 50));
                var rk = Txt(root.transform, "Rank", "الجندي", th.fontBody, 26, th.ivoryDim, TextAlignmentOptions.MidlineRight);
                Place(Rt(rk.gameObject), new Vector2(0.5f, 0.5f), new Vector2(115, -28), new Vector2(380, 36));
                var val = Txt(root.transform, "Value", "٢٬٥٠٠", th.fontHeading, 46, gold, TextAlignmentOptions.MidlineLeft);
                Place(Rt(val.gameObject), new Vector2(0.5f, 0.5f), new Vector2(-150, 0), new Vector2(260, 70));
                var arrow = Go("Arrow", root.transform); Place(Rt(arrow), new Vector2(0.5f, 0.5f), new Vector2(-330, 8), new Vector2(34, 34)); var ai = Img(arrow, th.triUp);
                var dl = Txt(root.transform, "Delta", "٣", th.fontHeading, 30, th.teal); Place(Rt(dl.gameObject), new Vector2(0.5f, 0.5f), new Vector2(-330, -26), new Vector2(60, 36));
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
                var t = Txt(root.transform, "Label", "النقاط", th.fontHeading, 32, th.ivoryDim); Stretch(Rt(t.gameObject), 8, 6, 8, 8);
                Save(root, "LbChip");
            }

            // ---------- scene
            var canvasGo = NewAppCanvas(th, new Color(0.075f, 0.055f, 0.045f));
            var screen = Go("LeaderboardsScreen", canvasGo.transform); Stretch(Rt(screen));
            var scrollGo = Go("Scroll", screen.transform);
            var sr = Rt(scrollGo); sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = new Vector2(0, 490); sr.offsetMax = new Vector2(0, -250);
            var scroll = scrollGo.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 40f;
            var vp = Go("Viewport", scrollGo.transform); Stretch(Rt(vp)); vp.AddComponent<RectMask2D>();
            vp.AddComponent<Image>().color = Color.clear;
            var content = Go("Content", vp.transform);
            var cRt = Rt(content); cRt.anchorMin = new Vector2(0, 1); cRt.anchorMax = new Vector2(1, 1); cRt.pivot = new Vector2(0.5f, 1); cRt.anchoredPosition = Vector2.zero; cRt.sizeDelta = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>(); vlg.spacing = 18; vlg.padding = new RectOffset(0, 0, 22, 40); vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = false; vlg.childControlHeight = false; vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = Rt(vp); scroll.content = cRt;

            // header: title, subtitle and the reset countdown (the HTML top strip)
            var hdr = Go("Header", content.transform); Rt(hdr).sizeDelta = new Vector2(1040, 120);
            hdr.AddComponent<LayoutElement>().preferredHeight = 120;
            var hTitle = Txt(hdr.transform, "Title", "غرفة التصنيفات", th.fontHeading, 64, th.ivory, TextAlignmentOptions.MidlineRight); Place(Rt(hTitle.gameObject), new Vector2(1, 0.5f), new Vector2(-300, 18), new Vector2(600, 76));
            var hSub = Txt(hdr.transform, "Sub", "من الأول؟ الليلة تُحسم", th.fontBody, 28, th.brass, TextAlignmentOptions.MidlineRight); Place(Rt(hSub.gameObject), new Vector2(1, 0.5f), new Vector2(-300, -34), new Vector2(600, 40));
            var resetPill = Go("ResetPill", hdr.transform); Place(Rt(resetPill), new Vector2(0, 0.5f), new Vector2(230, 0), new Vector2(440, 72)); Img(resetPill, th.chipOff, true, 1.4f).color = new Color(1f, 1f, 1f, 0.03f);
            var resetText = Txt(resetPill.transform, "T", "يتجدد خلال ٠٣:٤٢:١٠", th.fontBody, 28, new Color32(0xFF, 0xB3, 0x5A, 255)); Stretch(Rt(resetText.gameObject), 10, 4, 10, 4);

            // doors
            var doorsRow = Go("Doors", content.transform); Rt(doorsRow).sizeDelta = new Vector2(1040, 200);
            doorsRow.AddComponent<LayoutElement>().preferredHeight = 200;
            var dh = doorsRow.AddComponent<HorizontalLayoutGroup>(); dh.spacing = 24; dh.childAlignment = TextAnchor.MiddleCenter; dh.reverseArrangement = true;
            dh.childControlWidth = false; dh.childControlHeight = false; dh.childForceExpandWidth = false;
            var doors = new Button[3]; string[] dn = { "اليوم", "الأسبوع", "قاعة المشاهير" };
            Sprite[] ds = { th.boardDay, th.paper, th.vipFrame };
            Sprite[] di2 = { th.iconDay, th.iconWeek, th.iconWall };
            Color[] dc = { new Color32(0xF2, 0xEF, 0xE4, 255), th.paperInk, th.brassLight };
            for (int i = 0; i < 3; i++)
            {
                var d = Go("Door" + i, doorsRow.transform); Rt(d).sizeDelta = new Vector2(i == 2 ? 360 : 310, 160);
                var di = Img(d, ds[i], true, 1.2f, true);
                var db = d.AddComponent<Button>(); db.targetGraphic = di; db.transition = Selectable.Transition.None; doors[i] = db;
                d.AddComponent<LayoutElement>().preferredWidth = i == 2 ? 360 : 310;
                var dIcon = Go("Icon", d.transform); Place(Rt(dIcon), new Vector2(0.5f, 1f), new Vector2(0, -52), new Vector2(76, 76)); Img(dIcon, di2[i]).raycastTarget = false;
                var dt = Txt(d.transform, "Label", dn[i], th.fontHeading, 36, dc[i]); Place(Rt(dt.gameObject), new Vector2(0.5f, 0f), new Vector2(0, 36), new Vector2(300, 52));
            }

            // chips
            var chipsRowGo = Go("ChipsRow", content.transform); Rt(chipsRowGo).sizeDelta = new Vector2(1080, 110);
            chipsRowGo.AddComponent<LayoutElement>().preferredHeight = 110;
            var csr = chipsRowGo.AddComponent<ScrollRect>(); csr.vertical = false; csr.movementType = ScrollRect.MovementType.Elastic;
            var cvp = Go("Viewport", chipsRowGo.transform); Stretch(Rt(cvp)); cvp.AddComponent<RectMask2D>(); cvp.AddComponent<Image>().color = Color.clear;
            var chipsRoot = Go("Chips", cvp.transform); var ccr = Rt(chipsRoot);
            ccr.anchorMin = new Vector2(1, 0); ccr.anchorMax = new Vector2(1, 1); ccr.pivot = new Vector2(1, 0.5f); ccr.sizeDelta = Vector2.zero; ccr.anchoredPosition = Vector2.zero;
            var chl = chipsRoot.AddComponent<HorizontalLayoutGroup>(); chl.spacing = 14; chl.padding = new RectOffset(30, 30, 10, 10); chl.childAlignment = TextAnchor.MiddleRight; chl.reverseArrangement = true;
            chl.childControlWidth = false; chl.childControlHeight = false; chl.childForceExpandWidth = false;
            chipsRoot.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csr.viewport = Rt(cvp); csr.content = ccr;

            // board (chalkboard / newspaper)
            var board = Go("BoardView", content.transform); Rt(board).sizeDelta = new Vector2(1010, 2150);
            board.AddComponent<LayoutElement>().preferredHeight = 2150;
            var boardBg = Img(board, th.boardDay, true, 1f, false);
            var title = Txt(board.transform, "Title", "سبورة الليلة · النقاط", th.fontCalligraphy, 56, new Color32(0xE9, 0xE6, 0xDC, 255), TextAlignmentOptions.MidlineRight);
            PlaceTop(Rt(title.gameObject), 80, -70, 700, 80);
            var unit = Txt(board.transform, "Unit", "نقطة", th.fontBody, 34, gold, TextAlignmentOptions.Midline); PlaceTop(Rt(unit.gameObject), -350, -70, 200, 80);
            var reset = resetText;   // the countdown lives in the header pill

            var slots = new LeaderPodiumSlot[3];
            float[] sx = { 0, -310, 310 }; float[] top = { -230, -300, -300 }; float[] avSize = { 210, 160, 160 };
            for (int i = 0; i < 3; i++)
            {
                var slot = Go("Podium" + (i + 1), board.transform); PlaceTop(Rt(slot), sx[i], top[i], 300, 560);
                var crown = Go("Crown", slot.transform); PlaceTop(Rt(crown), 0, 14, 90, 90); Img(crown, th.giftCrown);
                var a = InstChild("AvatarFrame", slot.transform, "Avatar"); PlaceTop(Rt(a), 0, -70, avSize[i], avSize[i]);
                var seal = Go("Place", slot.transform); PlaceTop(Rt(seal), 0, -70 - avSize[i] + 26, 56, 56); Img(seal, th.levelSeal);
                var pt = Txt(seal.transform, "N", "١", th.fontHeading, 28, th.brassText); Stretch(Rt(pt.gameObject), 0, 3, 0, 0);
                var n = Txt(slot.transform, "Name", "الاسم", th.fontBody, 36, th.leatherText); PlaceTop(Rt(n.gameObject), 0, -70 - avSize[i] - 36, 300, 50);
                var vv = Txt(slot.transform, "Value", "٠", th.fontHeading, 44, gold); PlaceTop(Rt(vv.gameObject), 0, -70 - avSize[i] - 92, 300, 56);
                var r = Txt(slot.transform, "Rank", "", th.fontBody, 26, th.ivoryDim); PlaceTop(Rt(r.gameObject), 0, -70 - avSize[i] - 146, 300, 36);
                var ps = slot.AddComponent<LeaderPodiumSlot>();
                SetRef(ps, "avatar", a.GetComponent<AvatarFrame>()); SetRef(ps, "nameLabel", n.GetComponent<ArabicText>()); SetRef(ps, "valueLabel", vv.GetComponent<ArabicText>());
                SetRef(ps, "rankLabel", r.GetComponent<ArabicText>()); SetRef(ps, "placeLabel", pt.GetComponent<ArabicText>()); SetRef(ps, "crown", crown);
                slots[i] = ps;
            }
            var rowsRoot = Go("Rows", board.transform); PlaceTop(Rt(rowsRoot), 0, -720, 980, 1230);
            var rvl = rowsRoot.AddComponent<VerticalLayoutGroup>(); rvl.spacing = 8; rvl.childAlignment = TextAnchor.UpperCenter;
            rvl.childControlWidth = false; rvl.childControlHeight = false; rvl.childForceExpandWidth = false; rvl.childForceExpandHeight = false;
            var rewardBg = Go("RewardBg", board.transform); PlaceTop(Rt(rewardBg), 0, -2005, 920, 110); Img(rewardBg, th.slotDark, true, 1.4f).raycastTarget = false;
            var reward = Txt(board.transform, "Reward", "جائزة", th.fontBody, 30, gold, TextAlignmentOptions.Center, true); PlaceTop(Rt(reward.gameObject), 0, -2015, 880, 90);

            // hall of fame
            var wall = Go("WallView", content.transform); Rt(wall).sizeDelta = new Vector2(1080, 2200);
            wall.AddComponent<LayoutElement>().preferredHeight = 2200;
            var wallBg = wall.AddComponent<Image>(); wallBg.sprite = th.wallTile; wallBg.type = Image.Type.Tiled; wallBg.raycastTarget = false;
            void Heading(string text, float y) { var h = Txt(wall.transform, "H", text, th.fontCalligraphy, 64, th.brassLight); PlaceTop(Rt(h.gameObject), 0, y, 900, 90); }
            void Framed(string name, string sub, float x, float y, float w, float hgt, float plaqueW)
            {
                var f = Go("Frame", wall.transform); PlaceTop(Rt(f), x, y, w, hgt); Img(f, th.goldFrame, true, 1f);
                var p = Go("Pic", f.transform); Place(Rt(p), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(w * 0.62f, w * 0.62f)); Img(p, th.avatarDefault).color = new Color(1, 1, 1, 0.9f);
                var pl = Go("Plaque", wall.transform); PlaceTop(Rt(pl), x, y - hgt - 14, plaqueW, 120); Img(pl, th.brassPlate, true, 2.2f);
                var pn = Txt(pl.transform, "N", name, th.fontHeading, 38, th.brassText); Place(Rt(pn.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(plaqueW - 30, 50));
                var ps = Txt(pl.transform, "S", sub, th.fontBody, 24, th.brassText); Place(Rt(ps.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, -24), new Vector2(plaqueW - 30, 36));
            }
            Heading("العرّاب", -40);
            Framed("أبو فارس", "العرّاب الحالي · ١٨٬٤٠٠ نقطة", 0, -160, 400, 500, 640);
            Heading("أبطال المواسم", -880);
            Framed("الحج حسن", "الموسم الثالث", 330, -1000, 270, 340, 300);
            Framed("أم سعيد", "الموسم الثاني", 0, -1000, 270, 340, 300);
            Framed("البيك", "الموسم الأول", -330, -1000, 270, 340, 300);
            Heading("الأرقام القياسية", -1500);
            string[,] recs = { { "أطول سلسلة فوز", "٢٢", "أبو خليل" }, { "أكثر هدايا مستلمة", "٤٬٨١٠", "لينا" }, { "أطول جلسة صوتية", "١٤ ساعة", "سارة" }, { "أكبر غرفة مستضافة", "١٦ لاعباً", "مازن" }, { "أسرع وصول للعرّاب", "١٩ يوماً", "الدكتور" }, { "أكثر كشف صحيح", "٣١٢", "نور" } };
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 2 == 0 ? 250 : -250), y = -1620 - (i / 2) * 190;
                var rc = Go("Rec" + i, wall.transform); PlaceTop(Rt(rc), x, y, 470, 170); Img(rc, th.leatherDark, true, 1.4f);
                var l1 = Txt(rc.transform, "L", recs[i, 0], th.fontBody, 26, th.brass); Place(Rt(l1.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, 46), new Vector2(430, 36));
                var l2 = Txt(rc.transform, "V", recs[i, 1], th.fontHeading, 56, th.brassLight); Place(Rt(l2.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(430, 70));
                var l3 = Txt(rc.transform, "H", recs[i, 2], th.fontBody, 28, th.leatherText); Place(Rt(l3.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, -42), new Vector2(430, 36));
            }
            wall.SetActive(false);

            // pinned "my rank" bar
            var mine = Go("MineBar", screen.transform);
            var mr = Rt(mine); mr.anchorMin = new Vector2(0, 0); mr.anchorMax = new Vector2(1, 0); mr.pivot = new Vector2(0.5f, 0); mr.offsetMin = new Vector2(30, 340); mr.offsetMax = new Vector2(-30, 480);
            Img(mine, th.leatherDark, true, 1f, true);
            var mrank = Txt(mine.transform, "Rank", "#٤٢", th.fontHeading, 72, th.brassLight, TextAlignmentOptions.MidlineRight); Place(Rt(mrank.gameObject), new Vector2(1, 0.5f), new Vector2(-170, 6), new Vector2(280, 90));
            var mneed = Txt(mine.transform, "Need", "تحتاج", th.fontBody, 28, th.leatherText, TextAlignmentOptions.MidlineRight); Place(Rt(mneed.gameObject), new Vector2(0.5f, 0.5f), new Vector2(-10, 22), new Vector2(560, 40));
            var mwall = Txt(mine.transform, "WallNote", "", th.fontBody, 26, th.leatherText, TextAlignmentOptions.MidlineRight, true); Place(Rt(mwall.gameObject), new Vector2(0.5f, 0.5f), new Vector2(-10, 4), new Vector2(560, 100));
            var mbar = Go("Bar", mine.transform); Place(Rt(mbar), new Vector2(0.5f, 0.5f), new Vector2(-10, -30), new Vector2(520, 16));
            var mbi = mbar.AddComponent<Image>(); mbi.color = new Color(0, 0, 0, 0.6f); mbi.raycastTarget = false;
            var mfill = Go("Fill", mbar.transform); var mfr = Rt(mfill); mfr.anchorMin = new Vector2(0.36f, 0); mfr.anchorMax = Vector2.one; mfr.offsetMin = mfr.offsetMax = Vector2.zero;
            var mfi = mfill.AddComponent<Image>(); mfi.color = new Color32(0xC0, 0x32, 0x3A, 255); mfi.raycastTarget = false;
            var mval = Txt(mine.transform, "Value", "٥٧٦", th.fontHeading, 46, gold, TextAlignmentOptions.MidlineLeft); Place(Rt(mval.gameObject), new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(200, 70));

            var lb = screen.AddComponent<KitLeaderboardsScreen>();
            var so = new SerializedObject(lb);
            var da = so.FindProperty("doors"); da.arraySize = 3; for (int i = 0; i < 3; i++) da.GetArrayElementAtIndex(i).objectReferenceValue = doors[i];
            var pa = so.FindProperty("podium"); pa.arraySize = 3; for (int i = 0; i < 3; i++) pa.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            SetRef(lb, "boardView", board); SetRef(lb, "wallView", wall); SetRef(lb, "chipsRow", chipsRowGo); SetRef(lb, "boardBg", boardBg);
            SetRef(lb, "boardTitle", title.GetComponent<ArabicText>()); SetRef(lb, "boardUnit", unit.GetComponent<ArabicText>());
            SetRef(lb, "rewardLabel", reward.GetComponent<ArabicText>()); SetRef(lb, "resetLabel", reset.GetComponent<ArabicText>());
            SetRef(lb, "rowsRoot", Rt(rowsRoot)); SetRef(lb, "chipsRoot", ccr); SetRef(lb, "rowPrefab", LoadPrefab("LbRow")); SetRef(lb, "chipPrefab", LoadPrefab("LbChip"));
            SetRef(lb, "mineRank", mrank.GetComponent<ArabicText>()); SetRef(lb, "mineNeed", mneed.GetComponent<ArabicText>()); SetRef(lb, "mineValue", mval.GetComponent<ArabicText>());
            SetRef(lb, "mineFill", mfr); SetRef(lb, "mineBar", mine); SetRef(lb, "mineWall", mwall.GetComponent<ArabicText>());
            lb.Build();

            AddBars(canvasGo, 3);
            LayoutRebuilder.ForceRebuildLayoutImmediate(cRt);
            SaveScene(LbScenePath);
        }
    }
}
