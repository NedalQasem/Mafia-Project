using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// The "my rank" scene (Docs/mockups/myrank.html). Pieces exported by Docs/mockups/export-myrank.js into Art/UI/Rank (manifest.json:
    /// size and shadow room of each); places are that page's measurements (rank-layout.json there) in CSS px on a 390px phone, y down from
    /// the parent's top-left; one CSS px is NK canvas units. The screen is drawn by KitRankScreen at run time.
    /// </summary>
    public static partial class KitBuilder
    {
        private const string RankArtDir = "Assets/_Project/Art/UI/Rank/";
        private const string RankScenePath = "Assets/_Project/Scenes/UIKitRank.unity";
        private static Dictionary<string, MSprite> rankMan;

        [MenuItem("Mafia Night/UI Kit/Build Rank Screen")]
        public static void BuildRankScene()
        {
            var th = BuildTheme();
            MatchArt = RankArtDir; rankMan = ReadMatchManifest(); ImportMatchSprites(rankMan); AssetDatabase.Refresh();
            var canvasRoot = NewAppCanvas(th, new Color(0.06f, 0.04f, 0.03f));
            var bgi = GameObject.Find("Canvas/Background").GetComponent<Image>();
            bgi.sprite = RankSprite("rk_bg"); bgi.color = Color.white;
            BuildRank(canvasRoot.transform, th);
            SaveScene(RankScenePath);
            AddToBuild(RankScenePath);
            Debug.Log("[UIKit] built the rank screen");
        }

        private static Sprite RankSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(RankArtDir + name + ".png");

        /// <summary>A sprite drawn on a part's rect, growing by the room it keeps for its shadow (manifest pad).</summary>
        private static Image RankImg(Transform parent, string name, string sprite, float x, float y, float w, float h, bool sliced = false)
        {
            float pad = rankMan.TryGetValue(sprite, out var m) ? m.pad : 0f;
            var im = BagImg(parent, name, RankSprite(sprite), x - pad, y - pad, w + 2f * pad, h + 2f * pad, sliced);
            return im;
        }

        private static TextMeshProUGUI RankTxt(Transform parent, string name, string text, TMP_FontAsset font, float css, string hex, TextAlignmentOptions align, float x, float y, float w, float h, bool bold)
        {
            var t = BagTxt(parent, name, text, font, css, hex, align, bold);
            BagRect(t.gameObject, x, y, w, h);
            return t;
        }

        private static void BuildRank(Transform root, KitTheme th)
        {
            TMP_FontAsset cairo = th.fontBody, lalezar = th.fontLalezar != null ? th.fontLalezar : th.fontHeading, aref = th.fontAref != null ? th.fontAref : th.fontCalligraphy;
            Sprite bagSheet = BagSpr("bag_sheet", 3f, 50, 0, 50, 50), bagBtn = BagSpr("bag_btn", 4f, 18, 18, 18, 18), bagBtnDark = BagSpr("bag_btn_dark", 4f, 18, 18, 18, 18);

            var screenGo = Go("KitRankScreen", root); Stretch(Rt(screenGo));
            var screen = screenGo.AddComponent<KitRankScreen>();

            // ---- the scroll: the whole screen, content from 84px down (under the title's fade), 14 on the sides, 12 between the parts
            var scrollGo = Go("Scroll", screenGo.transform); Stretch(Rt(scrollGo)); scrollGo.AddComponent<RectMask2D>();
            var scroll = scrollGo.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 30f;
            var hit = scrollGo.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var listGo = Go("List", scrollGo.transform); var lr = Rt(listGo);
            lr.anchorMin = new Vector2(0, 1); lr.anchorMax = new Vector2(1, 1); lr.pivot = new Vector2(0.5f, 1); lr.anchoredPosition = Vector2.zero; lr.sizeDelta = Vector2.zero;
            var vlg = listGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(Mathf.RoundToInt(14 * NK), Mathf.RoundToInt(14 * NK), Mathf.RoundToInt(84 * NK), Mathf.RoundToInt(30 * NK));
            vlg.spacing = 12f * NK; vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            listGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = lr; scroll.viewport = Rt(scrollGo);

            // ---- the membership card (362 x 204, turned 0.6 degrees)
            var card = Go("Card", listGo.transform); card.AddComponent<LayoutElement>().preferredHeight = 204f * NK;
            card.transform.localEulerAngles = new Vector3(0, 0, 0.6f);
            var cardBg = RankImg(card.transform, "Bg", "rk_card", 0, 0, 362, 204);
            RankTxt(card.transform, "Title", "بطاقة العضوية", lalezar, 15.2f, "#2a1a0e", TextAlignmentOptions.Right, 274f - 40f, 14f, 114f, 24f, false);
            var cardNo = RankTxt(card.transform, "Number", "رقم ٠", cairo, 9.92f, "#8a1c24", TextAlignmentOptions.Left, 14f, 16.5f, 120f, 19f, true);
            var rule = BagImg(card.transform, "Line", null, 14f, 43f, 334f, 2f); rule.color = ParseColor("#2a1a0e", Color.black);
            var medal = BagImg(card.transform, "Medal", null, 256f - 9.58f, 55f - 9.58f, 111.17f, 111.17f);
            var pn = RankTxt(card.transform, "Name", "أبو سالم", cairo, 12.8f, "#5a4330", TextAlignmentOptions.Right, 14f, 57.52f, 230f, 24f, true);
            var rk = BagImg(card.transform, "RankName", null, 244f - 60f, 84.52f, 60f, 34.95f); rk.color = ParseColor("#2a1a0e", Color.black);
            var rkr = Rt(rk.gameObject); rkr.pivot = new Vector2(1f, 0.5f); rkr.anchoredPosition = new Vector2((244f + 4f) * NK, -(84.52f + 17.475f) * NK);      // right edge on the card's text edge; the width follows the picture
            var bullets = new Image[3];
            for (int i = 0; i < 3; i++) { bullets[i] = BagImg(card.transform, "Bullet" + i, th.bulletOn, 235f - 12f * i, 122.47f, 9f, 22f); }
            var tierTxt = RankTxt(card.transform, "Tier", "الدرجة الثانية من ٣", cairo, 11.2f, "#5a4330", TextAlignmentOptions.Right, 14f, 122.97f, 189f, 21f, true);
            var track = RankImg(card.transform, "Track", "rk_track", 14, 157, 334, 10);
            var fill = BagImg(track.transform, "Fill", RankSprite("rk_fill"), 0, 0, 100, 10, true);
            var fr = Rt(fill.gameObject);       // grows from the right edge; width set at run time
            fr.anchorMin = fr.anchorMax = new Vector2(1f, 0.5f); fr.pivot = new Vector2(1f, 0.5f); fr.anchoredPosition = new Vector2(-2f * NK, 0f); fr.sizeDelta = new Vector2(100f * NK, 14f * NK);
            fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = 1f;
            var progL = RankTxt(card.transform, "ProgLeft", "", cairo, 10.56f, "#5a4330", TextAlignmentOptions.Right, 14f, 172f, 334f, 20f, true);
            var progR = RankTxt(card.transform, "ProgRight", "", cairo, 10.56f, "#8a1c24", TextAlignmentOptions.Left, 14f, 172f, 150f, 20f, true);
            var stampRt = Rt(BagImg(card.transform, "Stamp", RankSprite("rk_stamp"), 252.41f - 4f, 122f - 4f, 51.59f, 39f, true).gameObject);
            stampRt.localEulerAngles = new Vector3(0, 0, 12f);
            var stampTxt = RankTxt(stampRt, "Text", "عضو", lalezar, 13.6f, "#8a1c24", TextAlignmentOptions.Center, 0, 0, 10, 10, false);
            Pad(Rt(stampTxt.gameObject), 6, 6, 6, 6);
            var stampCol = stampTxt.color; stampCol.a = 0.53f; stampTxt.color = stampCol;       // opacity .75 in the page, in linear light

            // ---- how points come: three tiles
            var rules = Go("Rules", listGo.transform); rules.AddComponent<LayoutElement>().preferredHeight = 50.66f * NK;
            string[] rv = { "+٥٠", "−٢٠", "٢٥٠" }, rl = { "نقطة لكل فوز", "لكل خسارة", "نقطة لكل درجة" }, rc = { "#8fd4b4", "#e0646a", "#e8cc8c" };
            float[] rx = { 245.34f, 122.67f, 0f };
            for (int i = 0; i < 3; i++)
            {
                var t = Go("Rule" + i, rules.transform); BagRect(t, rx[i], 0, 116.66f, 50.66f);
                RankImg(t.transform, "Bg", "rk_rule", 0, 0, 116.66f, 50.66f);
                RankTxt(t.transform, "Value", rv[i], lalezar, 18.4f, rc[i], TextAlignmentOptions.Center, 0, 8f, 116.66f, 20.23f, false);
                RankTxt(t.transform, "Label", rl[i], cairo, 9.6f, "#a79d88", TextAlignmentOptions.Center, 0, 29.23f, 116.66f, 13.42f, true);
            }

            // ---- the section head
            var sec = Go("Section", listGo.transform); sec.AddComponent<LayoutElement>().preferredHeight = 26f * NK;
            RankTxt(sec.transform, "Title", "سلّم العيلة", lalezar, 16.32f, "#a98f62", TextAlignmentOptions.Right, 262f, 0, 100f, 26f, false);
            RankTxt(sec.transform, "Small", "اضغط على رتبة لتشوف شو بتفتحلك", cairo, 10.24f, "#6f685c", TextAlignmentOptions.Right, 100f, 3.5f, 154f, 19f, true);
            var ln = BagImg(sec.transform, "Line", null, 0, 12.5f, 92f, 1f); ln.color = new Color(1f, 1f, 1f, 0.0706f);

            // ---- the ladder: the brass rail behind, eight rungs (the Godfather first), 62 high, 8 apart
            var ladder = Go("Ladder", listGo.transform); ladder.AddComponent<LayoutElement>().preferredHeight = 552f * NK;
            RankImg(ladder.transform, "Rail", "rk_rail", 327f, 24f, 4f, 504f).type = Image.Type.Simple;
            var rungsP = new List<KitRankScreen.Rung>();
            for (int i = 0; i < 8; i++)
            {
                var rg = Go("Rung" + i, ladder.transform); BagRect(rg, 0, i * 70f, 362, 62);
                var bg = RankImg(rg.transform, "Bg", "rk_rung", 0, 0, 362, 62); bg.preserveAspect = false;
                var hitImg = rg.AddComponent<Image>(); hitImg.color = Color.clear; hitImg.raycastTarget = true;
                var btn = BagButton(rg);
                float mp = 10f * 44f / 96f;
                var md = BagImg(rg.transform, "Medal", null, 308f - mp, 9f - mp, 44f + 2f * mp, 44f + 2f * mp);
                var title = RankTxt(rg.transform, "Title", "", lalezar, 16.8f, "#d9d0bd", TextAlignmentOptions.Right, 61.8f, 10.42f, 236.2f, 20.16f, false);
                var desc = RankTxt(rg.transform, "Desc", "", cairo, 10.56f, "#a79d88", TextAlignmentOptions.Right, 61.8f, 31.58f, 236.2f, 20f, true);
                var top = RankTxt(rg.transform, "EndTop", "", cairo, 9.6f, "#8f8777", TextAlignmentOptions.Left, 12f, 10f, 56f, 21f, true);
                var bot = RankTxt(rg.transform, "EndBottom", "", cairo, 9.6f, "#8f8777", TextAlignmentOptions.Left, 12f, 30.5f, 56f, 18f, true);
                var lk = BagImg(rg.transform, "Lock", RankSprite("rk_lock"), 12f - 2f, 13.5f - 2f, 18f, 18f);
                var pin = RankImg(rg.transform, "Pin", "rk_pin", 74f, -8f, 55.72f, 21f);
                Rt(pin.gameObject).localEulerAngles = new Vector3(0, 0, 6f);
                var pt = RankTxt(pin.transform, "Text", "إنت هون", cairo, 9.6f, "#2a1a0e", TextAlignmentOptions.Center, 0, 0, 10, 10, true); Pad(Rt(pt.gameObject), 8, 8, 8, 8);
                pin.gameObject.SetActive(false);
                rungsP.Add(new KitRankScreen.Rung { button = btn, bg = bg, medal = md, lockIcon = lk, title = title, desc = desc, endTop = top, endBottom = bot, pin = pin.gameObject });
            }

            // ---- the title bar over the scroll: fade, back button, title, line under it
            RankImg(screenGo.transform, "TopFade", "rk_top", 0, 0, 390, 78.77f);
            var icb = RankImg(screenGo.transform, "Back", "rk_ic", 338, 29.38f, 38, 38); var backBtn = BagButton(icb.gameObject);
            RankImg(icb.transform, "Arrow", "rk_back", 18f, 18f, 18f, 18f);      // centred in the button's 54px sprite
            RankTxt(screenGo.transform, "Title", "رتبتي", lalezar, 27.2f, "#d9d0bd", TextAlignmentOptions.Right, 14f, 20f, 314f, 43f, false);
            RankTxt(screenGo.transform, "Sub", "من الصبي للعرّاب · ٨ رتب، بكل وحدة ٣ درجات", cairo, 10.56f, "#a98f62", TextAlignmentOptions.Right, 14f, 58.2f, 314f, 14f, true);
            var header = screenGo.AddComponent<KitScreenHeader>(); SetRef(header, "backButton", backBtn);

            // ---- the rank sheet over a dim
            var layer = Go("Sheet", screenGo.transform); Stretch(Rt(layer));
            var dimGo = Go("Dim", layer.transform); Stretch(Rt(dimGo));
            var dimi = dimGo.AddComponent<Image>(); dimi.color = new Color(0f, 0f, 0f, 0.91f); dimi.raycastTarget = true;
            var dimBtn = dimGo.AddComponent<Button>(); dimBtn.transition = Selectable.Transition.None; dimBtn.targetGraphic = dimi;
            var sheet = Go("Panel", layer.transform); var shr = Rt(sheet);
            shr.anchorMin = new Vector2(0f, 0f); shr.anchorMax = new Vector2(1f, 0f); shr.pivot = new Vector2(0.5f, 0f); shr.anchoredPosition = Vector2.zero; shr.sizeDelta = new Vector2(0f, 416f * NK);
            var shbg = Go("Bg", sheet.transform); var sbr = Rt(shbg); sbr.anchorMin = Vector2.zero; sbr.anchorMax = Vector2.one; sbr.offsetMin = new Vector2(-30f * NK, 0f); sbr.offsetMax = new Vector2(30f * NK, 30f * NK);
            var sbi = shbg.AddComponent<Image>(); sbi.sprite = bagSheet; sbi.type = Image.Type.Sliced; sbi.raycastTarget = true; shbg.AddComponent<LayoutElement>().ignoreLayout = true;
            var svl = sheet.AddComponent<VerticalLayoutGroup>();
            svl.padding = new RectOffset(Mathf.RoundToInt(18 * NK), Mathf.RoundToInt(18 * NK), Mathf.RoundToInt(12 * NK), Mathf.RoundToInt(24 * NK));
            svl.spacing = 7f * NK; svl.childAlignment = TextAnchor.UpperCenter; svl.childControlWidth = true; svl.childControlHeight = true; svl.childForceExpandWidth = false; svl.childForceExpandHeight = false;
            sheet.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var hd = Go("Handle", sheet.transform); var hdl = hd.AddComponent<LayoutElement>(); hdl.preferredWidth = 44f * NK; hdl.preferredHeight = 8f * NK;
            var hdb = Go("Bar", hd.transform); var hbr = Rt(hdb); hbr.anchorMin = new Vector2(0f, 1f); hbr.anchorMax = new Vector2(1f, 1f); hbr.pivot = new Vector2(0.5f, 1f); hbr.anchoredPosition = Vector2.zero; hbr.sizeDelta = new Vector2(0f, 4f * NK);
            var hbi = hdb.AddComponent<Image>(); hbi.color = new Color(1f, 1f, 1f, 0.2f); hbi.raycastTarget = false;
            var mh = Go("MedalBox", sheet.transform); var mhl = mh.AddComponent<LayoutElement>(); mhl.preferredWidth = 96f * NK; mhl.preferredHeight = 96f * NK;
            var smedal = BagImg(mh.transform, "Medal", null, -10f, -10f, 116f, 116f);
            var sname = BagTxt(sheet.transform, "Name", "الريّس", lalezar, 24f, "#d9d0bd", TextAlignmentOptions.Center, false);
            var sdesc = BagTxt(sheet.transform, "Desc", "", cairo, 12.8f, "#cdbf9f", TextAlignmentOptions.Center, false);
            sdesc.GetComponent<ArabicText>().Wrap = true; sdesc.enableWordWrapping = true; sdesc.gameObject.AddComponent<LayoutElement>().preferredWidth = 354f * NK;
            var srange = BagTxt(sheet.transform, "Range", "", cairo, 11.2f, "#a98f62", TextAlignmentOptions.Center, true);
            var set = Go("Set", sheet.transform); var sle = set.AddComponent<LayoutElement>(); sle.preferredWidth = 354f * NK; sle.preferredHeight = 79.63f * NK;
            var gl = set.AddComponent<GridLayoutGroup>(); gl.cellSize = new Vector2(66f * NK, 79.63f * NK); gl.spacing = new Vector2(6f * NK, 6f * NK);
            gl.startCorner = GridLayoutGroup.Corner.UpperRight; gl.childAlignment = TextAnchor.UpperRight; gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 5;
            var tiles = new RectTransform[5];
            for (int k = 0; k < 5; k++)
            {
                var tg = Go("Tile" + k, set.transform); var tr = Rt(tg); tiles[k] = tr;
                RankImg(tg.transform, "Bg", "rk_it", 0, 0, 66, 79.63f);
                var th2 = Go("Thumb", tg.transform); BagRect(th2, 17f - 2f, 8f, 36f, 32f);       // a little wider than the mockup's 32px square: the room, strip and plate pictures are wide
                RankTxt(tg.transform, "Name", "", cairo, 8.96f, "#d9d0bd", TextAlignmentOptions.Center, 2f, 43f, 62f, 11.63f, true);
                RankTxt(tg.transform, "Type", "", cairo, 8.32f, "#a98f62", TextAlignmentOptions.Center, 2f, 57.63f, 62f, 16f, true);
            }
            var sneed = BagTxt(sheet.transform, "Need", "", cairo, 11.84f, "#e0646a", TextAlignmentOptions.Center, true);
            var acts = Go("Acts", sheet.transform); acts.AddComponent<LayoutElement>().preferredWidth = 354f * NK;
            var avl = acts.AddComponent<VerticalLayoutGroup>(); avl.childControlWidth = true; avl.childControlHeight = true; avl.childForceExpandWidth = true; avl.childForceExpandHeight = false; avl.padding = new RectOffset(0, 0, Mathf.RoundToInt(4 * NK), 0);
            var store = Go("Store", acts.transform); var sti = store.AddComponent<Image>(); sti.sprite = bagBtn; sti.type = Image.Type.Sliced; store.AddComponent<LayoutElement>().preferredHeight = 46f * NK;
            var storeBtn = BagButton(store);
            var stt = BagTxt(store.transform, "Text", "شوفها بالمتجر", cairo, 13.76f, "#2a1a08", TextAlignmentOptions.Center, true); Pad(Rt(stt.gameObject), 0, 2, 0, 0);
            var close = Go("Close", acts.transform); var ci = close.AddComponent<Image>(); ci.sprite = bagBtnDark; ci.type = Image.Type.Sliced; close.AddComponent<LayoutElement>().preferredHeight = 46f * NK;
            var closeBtn = BagButton(close);
            var ct = BagTxt(close.transform, "Text", "تمام", cairo, 13.76f, "#e8cc8c", TextAlignmentOptions.Center, true); Pad(Rt(ct.gameObject), 0, 2, 0, 0);
            layer.SetActive(false);

            // ---- the references
            var so = new SerializedObject(screen);
            void R(string f, Object v) { so.FindProperty(f).objectReferenceValue = v; }
            R("cardNumber", cardNo); R("playerName", pn); R("tierText", tierTxt); R("progLeft", progL); R("progRight", progR); R("stampText", stampTxt);
            R("medal", medal); R("nameImg", rk); R("fill", fr); R("stamp", stampRt);
            Fill(so, "bullets", bullets);
            var sp = new Sprite[8]; var spl = new Sprite[8]; var spn = new Sprite[8];
            for (int i = 0; i < 8; i++) { sp[i] = RankSprite("rk_m" + (i + 1)); spl[i] = RankSprite("rk_m" + (i + 1) + "_lock"); spn[i] = RankSprite("rk_n" + (i + 1)); }
            Fill(so, "medals", sp); Fill(so, "medalsLocked", spl); Fill(so, "names", spn);
            R("bulletOn", th.bulletOn); R("bulletOff", th.bulletOff); R("rungNormal", RankSprite("rk_rung")); R("rungLock", RankSprite("rk_rung_lock")); R("rungNow", RankSprite("rk_rung_now"));
            R("tileOpen", RankSprite("rk_it")); R("tileLocked", RankSprite("rk_it_lk"));
            var rp = so.FindProperty("rungs"); rp.arraySize = 8;
            for (int i = 0; i < 8; i++)
            {
                var e = rp.GetArrayElementAtIndex(i); var v = rungsP[i];
                e.FindPropertyRelative("button").objectReferenceValue = v.button; e.FindPropertyRelative("bg").objectReferenceValue = v.bg; e.FindPropertyRelative("medal").objectReferenceValue = v.medal;
                e.FindPropertyRelative("lockIcon").objectReferenceValue = v.lockIcon; e.FindPropertyRelative("title").objectReferenceValue = v.title; e.FindPropertyRelative("desc").objectReferenceValue = v.desc;
                e.FindPropertyRelative("endTop").objectReferenceValue = v.endTop; e.FindPropertyRelative("endBottom").objectReferenceValue = v.endBottom; e.FindPropertyRelative("pin").objectReferenceValue = v.pin;
            }
            R("sheetLayer", layer); R("dim", dimBtn); R("closeButton", closeBtn); R("storeButton", storeBtn); R("sheetMedal", smedal);
            R("sheetName", sname); R("sheetDesc", sdesc); R("sheetRange", srange); R("sheetNeed", sneed);
            Fill(so, "tiles", tiles);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Fill(SerializedObject so, string field, Object[] items)
        {
            var p = so.FindProperty(field); p.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }
}
