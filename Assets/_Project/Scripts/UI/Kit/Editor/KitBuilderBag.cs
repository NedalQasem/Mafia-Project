using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// The bag scene (Docs/mockups/bag.html). Pieces exported by Docs/mockups/export-bag.js into Art/UI/Bag; sizes and places are the
    /// mockup's measurements (bag-layout.json there) in CSS px on a 390px phone, y down from the parent's top; one CSS px is NK units.
    /// The lists (chips, compartments) are filled at run time by KitBagScreen from the templates built here.
    /// </summary>
    public static partial class KitBuilder
    {
        private const string BagArt = "Assets/_Project/Art/UI/Bag/";
        private const string BagScenePath = "Assets/_Project/Scenes/UIKitBag.unity";

        [MenuItem("Mafia Night/UI Kit/Build Bag Screen")]
        public static void BuildBagScene()
        {
            var th = BuildTheme();
            var canvasRoot = NewAppCanvas(th, new Color(0.09f, 0.02f, 0.03f));
            var bgi = GameObject.Find("Canvas/Background").GetComponent<Image>();
            bgi.sprite = BagSpr("bag_bg", 2f); bgi.color = Color.white;
            BuildBag(canvasRoot.transform, th);
            SaveScene(BagScenePath);
            AddToBuild(BagScenePath);
            Debug.Log("[UIKit] built the bag screen");
        }

        private static void AddToBuild(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Exists(s => s.path == path)) return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        /// <summary>Imports a bag sprite: texels per CSS px as exported; border (CSS px, padding included) for sliced ones.</summary>
        private static Sprite BagSpr(string name, float texPerCss, float l = 0f, float b = 0f, float r = 0f, float t = 0f)
        {
            string path = BagArt + name + ".png";
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

        /// <summary>A rect in CSS px from the parent's top-left (x, y down, w, h).</summary>
        private static RectTransform BagRect(GameObject g, float x, float y, float w, float h)
        {
            var r = Rt(g);
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2((x + w / 2f) * NK, -(y + h / 2f) * NK);
            r.sizeDelta = new Vector2(w * NK, h * NK);
            return r;
        }

        private static Image BagImg(Transform parent, string name, Sprite s, float x, float y, float w, float h, bool sliced = false)
        {
            var g = Go(name, parent); BagRect(g, x, y, w, h);
            var i = g.AddComponent<Image>(); i.sprite = s; i.raycastTarget = false; i.preserveAspect = false;
            if (sliced) { i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = 1f; }
            return i;
        }

        private static TextMeshProUGUI BagTxt(Transform parent, string name, string text, TMP_FontAsset font, float css, string hex, TextAlignmentOptions align, bool bold)
        {
            var t = Txt(parent, name, text, font, css * NK, ParseColor(hex, Color.white), align);
            t.fontStyle = FontStyles.Normal; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            if (bold) { var m = EnsureBoldMaterial(font); if (m != null) t.fontSharedMaterial = m; }
            return t;
        }

        private static void Pad(RectTransform r, float l, float b, float rr, float t) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(l * NK, b * NK); r.offsetMax = new Vector2(-rr * NK, -t * NK); }

        private static Button BagButton(GameObject g)
        {
            var img = g.GetComponent<Image>(); if (img == null) { img = g.AddComponent<Image>(); img.color = Color.clear; }
            img.raycastTarget = true;
            var b = g.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = img;
            var fx = g.AddComponent<KitButtonFx>(); SetRef(fx, "face", Rt(g));
            return b;
        }

        private static void BuildBag(Transform root, KitTheme th)
        {
            TMP_FontAsset cairo = th.fontBody, lalezar = th.fontLalezar != null ? th.fontLalezar : th.fontHeading;
            Sprite lid = BagSpr("bag_lid", 3f), clasp = BagSpr("bag_clasp", 6f), ic = BagSpr("bag_ic", 5f), back = BagSpr("bag_back", 8f);
            Sprite countS = BagSpr("bag_count", 5f, 14, 0, 14, 0), lookS = BagSpr("bag_look", 4f, 22, 22, 22, 22), bitS = BagSpr("bag_bit", 6f, 11, 0, 11, 0);
            Sprite chipS = BagSpr("bag_chip", 6f, 17, 0, 17, 0), chipOnS = BagSpr("bag_chip_on", 6f, 17, 0, 17, 0), chipN = BagSpr("bag_chip_n", 6f, 10, 0, 10, 0);
            Sprite cellS = BagSpr("bag_cell", 4f), cellOnS = BagSpr("bag_cell_on", 4f), dot = BagSpr("bag_dot", 8f), tagS = BagSpr("bag_tag", 6f);
            Sprite emptyS = BagSpr("bag_empty", 4f, 20, 20, 20, 20), caseS = BagSpr("bag_case", 4f), btnS = BagSpr("bag_btn", 4f, 18, 18, 18, 18), btnDarkS = BagSpr("bag_btn_dark", 4f, 18, 18, 18, 18);
            Sprite sheetS = BagSpr("bag_sheet", 3f, 50, 0, 50, 50), stageS = BagSpr("bag_stage", 4f, 14, 14, 14, 14), rrS = BagSpr("bag_rr", 6f, 11, 0, 11, 0);

            var screenGo = Go("KitBagScreen", root); Stretch(Rt(screenGo));
            var screen = screenGo.AddComponent<KitBagScreen>();

            // ---- the scroll under the lid (from 100px down): padding 16 14 30, 12 between the parts
            var scrollGo = Go("Scroll", screenGo.transform); var sr = Rt(scrollGo);
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = Vector2.zero; sr.offsetMax = new Vector2(0, -100f * NK);
            scrollGo.AddComponent<RectMask2D>();
            var scroll = scrollGo.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 30f;
            var hit = scrollGo.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var listGo = Go("List", scrollGo.transform); var lr = Rt(listGo);
            lr.anchorMin = new Vector2(0, 1); lr.anchorMax = new Vector2(1, 1); lr.pivot = new Vector2(0.5f, 1); lr.anchoredPosition = Vector2.zero; lr.sizeDelta = Vector2.zero;
            var vlg = listGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(Mathf.RoundToInt(14 * NK), Mathf.RoundToInt(14 * NK), Mathf.RoundToInt(16 * NK), Mathf.RoundToInt(30 * NK));
            vlg.spacing = 12f * NK; vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            listGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = lr; scroll.viewport = sr;

            // ---- what he wears (362 wide; the height follows the pills)
            var look = Go("Look", listGo.transform); var lookLe = look.AddComponent<LayoutElement>(); lookLe.preferredHeight = 116.11f * NK;
            var lookBg = Go("Bg", look.transform); Pad(Rt(lookBg), -8, -8, -8, -8); var lbi = lookBg.AddComponent<Image>(); lbi.sprite = lookS; lbi.type = Image.Type.Sliced; lbi.raycastTarget = false;
            var lookArt = Go("Art", look.transform); var lar = Rt(lookArt);
            lar.anchorMin = lar.anchorMax = new Vector2(1f, 0.5f); lar.pivot = new Vector2(0.5f, 0.5f); lar.anchoredPosition = new Vector2(-(14f + 33f) * NK, 0f); lar.sizeDelta = new Vector2(66f * NK, 66f * NK);
            var mid = Go("Mid", look.transform); var mr = Rt(mid);
            mr.anchorMin = mr.anchorMax = new Vector2(1f, 0.5f); mr.pivot = new Vector2(1f, 0.5f); mr.anchoredPosition = new Vector2(-92f * NK, 0f); mr.sizeDelta = new Vector2(256f * NK, 92f * NK);
            var lbl = BagTxt(mid.transform, "Label", "شكلك هلّق · بيشوفوه الكل", cairo, 9.92f, "#a98f62", TextAlignmentOptions.Right, true);
            TopStrip(Rt(lbl.gameObject), 0f, 19f);
            var lookName = BagTxt(mid.transform, "Name", "أبو سالم", lalezar, 19.2f, "#d9d0bd", TextAlignmentOptions.Right, false);
            TopStrip(Rt(lookName.gameObject), 23f, 24f);
            var bits = Go("Bits", mid.transform); var bir = Rt(bits); TopStrip(bir, 48.11f, 44f);
            var bit = Go("Bit", bits.transform); var bim = bit.AddComponent<Image>(); bim.sprite = bitS; bim.type = Image.Type.Sliced; bim.raycastTarget = false;
            var bt = BagTxt(bit.transform, "Text", "الدون الصغير", cairo, 9.6f, "#cdbf9f", TextAlignmentOptions.Center, true); Pad(Rt(bt.gameObject), 7, 0, 7, 1);

            // ---- the kinds: a strip that scrolls sideways and reaches the screen's edges
            var chipsRow = Go("Chips", listGo.transform); chipsRow.AddComponent<LayoutElement>().preferredHeight = 40f * NK;
            var vp = Go("Viewport", chipsRow.transform); var vpr = Rt(vp); vpr.anchorMin = Vector2.zero; vpr.anchorMax = Vector2.one; vpr.offsetMin = new Vector2(-14f * NK, 0); vpr.offsetMax = new Vector2(14f * NK, 0);
            vp.AddComponent<RectMask2D>(); var vpi = vp.AddComponent<Image>(); vpi.color = Color.clear;
            var csr = vp.AddComponent<ScrollRect>(); csr.vertical = false; csr.movementType = ScrollRect.MovementType.Elastic;
            var chipsContent = Go("Content", vp.transform); var ccr = Rt(chipsContent);
            ccr.anchorMin = ccr.anchorMax = new Vector2(1f, 0.5f); ccr.pivot = new Vector2(1f, 0.5f); ccr.anchoredPosition = Vector2.zero; ccr.sizeDelta = new Vector2(0, 40f * NK);
            var hlg = chipsContent.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(Mathf.RoundToInt(14 * NK), Mathf.RoundToInt(14 * NK), Mathf.RoundToInt(2 * NK), Mathf.RoundToInt(4 * NK));
            hlg.spacing = 6f * NK; hlg.childAlignment = TextAnchor.MiddleRight; hlg.reverseArrangement = true;
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            chipsContent.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csr.content = ccr; csr.viewport = vpr;
            var chip = Go("Chip", chipsContent.transform);
            var chipImg = chip.AddComponent<Image>(); chipImg.sprite = chipS; chipImg.type = Image.Type.Sliced;
            var chipLe = chip.AddComponent<LayoutElement>(); chipLe.preferredHeight = 34f * NK; chipLe.preferredWidth = 73f * NK;
            chip.AddComponent<CanvasGroup>();
            BagButton(chip);
            var ctx = BagTxt(chip.transform, "Text", "الكل", cairo, 11.84f, "#cdbf9f", TextAlignmentOptions.Right, true);
            var ctr = Rt(ctx.gameObject); ctr.anchorMin = ctr.anchorMax = new Vector2(1f, 0.5f); ctr.pivot = new Vector2(1f, 0.5f); ctr.anchoredPosition = new Vector2(-12f * NK, 0f); ctr.sizeDelta = new Vector2(30f * NK, 22f * NK);
            var cn = Go("N", chip.transform); var cnr = Rt(cn); cnr.anchorMin = cnr.anchorMax = new Vector2(0f, 0.5f); cnr.pivot = new Vector2(0f, 0.5f); cnr.anchoredPosition = new Vector2(12f * NK, 0f); cnr.sizeDelta = new Vector2(18f * NK, 18f * NK);
            var cni = cn.AddComponent<Image>(); cni.sprite = chipN; cni.type = Image.Type.Sliced; cni.raycastTarget = false;
            var cnt = BagTxt(cn.transform, "Text", "١١", lalezar, 11.52f, "#a98f62", TextAlignmentOptions.Center, false); Pad(Rt(cnt.gameObject), 0, 0, 0, 1);

            // ---- the empty bag (shown in place of the compartments)
            var emptyBag = Go("EmptyBag", listGo.transform); emptyBag.AddComponent<LayoutElement>().preferredHeight = 330.44f * NK;
            var ebg = Go("Bg", emptyBag.transform); Pad(Rt(ebg), -4, -4, -4, -4); var ebi = ebg.AddComponent<Image>(); ebi.sprite = emptyS; ebi.type = Image.Type.Sliced; ebi.raycastTarget = false;
            BagImg(emptyBag.transform, "Case", caseS, 133f, 26f, 96f, 96f);
            var eb = BagTxt(emptyBag.transform, "Title", "حقيبتك لسا فاضية", lalezar, 21.6f, "#d9d0bd", TextAlignmentOptions.Center, false); BagRect(eb.gameObject, 0f, 132f, 362f, 34f);
            var ep = BagTxt(emptyBag.transform, "Text", "كل شي بتشتريه من المتجر بيجي لهون،\nوبتلبسه وبتبدّله وقت ما بدك.", cairo, 12.48f, "#c2a99a", TextAlignmentOptions.Center, false); BagRect(ep.gameObject, 20f, 176f, 322f, 42.44f);
            ep.GetComponent<ArabicText>().Wrap = true; ep.enableWordWrapping = true;
            var ebtn = BagImg(emptyBag.transform, "Store", btnS, 125.16f - 6f, 228.44f - 6f, 111.7f + 12f, 46f + 14f, true); var storeBtn = BagButton(ebtn.gameObject);
            var ebt = BagTxt(ebtn.transform, "Text", "روح عالمتجر", cairo, 13.76f, "#2a1a08", TextAlignmentOptions.Center, true); Pad(Rt(ebt.gameObject), 6, 10, 6, 6);
            var es = BagTxt(emptyBag.transform, "Small", "اللي إلك من الأول تحت", cairo, 10.56f, "#8f7468", TextAlignmentOptions.Center, false); BagRect(es.gameObject, 0f, 284.44f, 362f, 20f);

            // ---- templates: section head, grid, compartment, "nothing of this kind"
            var templates = Go("Templates", listGo.transform);
            var tle = templates.AddComponent<LayoutElement>(); tle.ignoreLayout = true;

            var sec = Go("Section", templates.transform); sec.AddComponent<LayoutElement>().preferredHeight = 26f * NK;
            var stt = BagTxt(sec.transform, "Title", "الإطارات", lalezar, 16.32f, "#e8cc8c", TextAlignmentOptions.Right, false);
            var str = Rt(stt.gameObject); str.anchorMin = str.anchorMax = new Vector2(1f, 0.5f); str.pivot = new Vector2(1f, 0.5f); str.anchoredPosition = Vector2.zero; str.sizeDelta = new Vector2(44f * NK, 26f * NK);
            var ssm = BagTxt(sec.transform, "Small", "٣ · حول صورتك بكل غرفة", cairo, 10.24f, "#b08a7a", TextAlignmentOptions.Right, true);
            var smr = Rt(ssm.gameObject); smr.anchorMin = smr.anchorMax = new Vector2(1f, 0.5f); smr.pivot = new Vector2(1f, 0.5f); smr.anchoredPosition = new Vector2(-52f * NK, 0f); smr.sizeDelta = new Vector2(115f * NK, 19f * NK);
            var line = Go("Line", sec.transform); var lnr = Rt(line); lnr.anchorMin = lnr.anchorMax = new Vector2(0f, 0.5f); lnr.pivot = new Vector2(0f, 0.5f); lnr.anchoredPosition = Vector2.zero; lnr.sizeDelta = new Vector2(180f * NK, 1f * NK);
            var lni = line.AddComponent<Image>(); lni.color = new Color(1f, 1f, 1f, 0.0706f); lni.raycastTarget = false;

            var grid = Go("Grid", templates.transform);
            var gl = grid.AddComponent<GridLayoutGroup>(); gl.cellSize = new Vector2(114.66f * NK, 100.56f * NK); gl.spacing = new Vector2(9f * NK, 9f * NK);
            gl.startCorner = GridLayoutGroup.Corner.UpperRight; gl.childAlignment = TextAnchor.UpperRight; gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;

            var cell = Go("Cell", templates.transform); BagRect(cell, 0f, 0f, 114.66f, 100.56f);
            var cbg = Go("Bg", cell.transform); Pad(Rt(cbg), -6, -6, -6, -6); var cbi = cbg.AddComponent<Image>(); cbi.sprite = cellS; cbi.raycastTarget = false;
            BagButton(cell);
            var cart = Go("Art", cell.transform); BagRect(cart, 5f, 10f, 104.66f, 62f);
            var cnm = BagTxt(cell.transform, "Name", "إطار الجمر", cairo, 11.2f, "#d9d0bd", TextAlignmentOptions.Center, true); BagRect(cnm.gameObject, 5f, 75f, 104.66f, 18f);
            var rar = BagImg(cell.transform, "Rar", dot, 99.66f - 3f, 8f - 3f, 13f, 13f);
            var tag = BagImg(cell.transform, "Tag", tagS, 16.27f - 30f, 4.0f - 16f, 64f, 34f);
            tag.transform.localEulerAngles = new Vector3(0, 0, 8f);
            var tgt = BagTxt(tag.transform, "Text", "لابسه", cairo, 9.28f, "#2a1a0e", TextAlignmentOptions.Center, true); BagRect(tgt.gameObject, 8f, 8f, 44f, 16f);

            var ek = Go("EmptyKind", templates.transform); ek.AddComponent<LayoutElement>().preferredHeight = 119f * NK;
            var ekb = Go("Bg", ek.transform); Pad(Rt(ekb), -4, -4, -4, -4); var ekbi = ekb.AddComponent<Image>(); ekbi.sprite = emptyS; ekbi.type = Image.Type.Sliced; ekbi.raycastTarget = false;
            var ekt = BagTxt(ek.transform, "Title", "ما عندك خلفية الدردشة بعد", lalezar, 16.8f, "#d9d0bd", TextAlignmentOptions.Center, false); BagRect(ekt.gameObject, 0f, 18f, 362f, 27f);
            var ekbtn = BagImg(ek.transform, "Store", btnDarkS, 181f - 70f - 6f, 55f - 6f, 140f + 12f, 46f + 14f, true); BagButton(ekbtn.gameObject);
            var ekbt = BagTxt(ekbtn.transform, "Text", "شوفها بالمتجر", cairo, 13.76f, "#e8cc8c", TextAlignmentOptions.Center, true); Pad(Rt(ekbt.gameObject), 6, 10, 6, 6);

            // ---- the lid on top: shadow below it, stitch, two brass clasps; back button, title, the paper tag with the count
            BagImg(screenGo.transform, "Lid", lid, 0f, 0f, 390f, 130f);
            BagImg(screenGo.transform, "ClaspR", clasp, 290f - 6f, 93f - 6f, 42f, 30f);
            BagImg(screenGo.transform, "ClaspL", clasp, 70f - 6f, 93f - 6f, 42f, 30f);
            var icb = BagImg(screenGo.transform, "Back", ic, 330f, 21.38f, 54f, 54f); var backBtn = BagButton(icb.gameObject);
            BagImg(icb.transform, "Arrow", back, 16f, 16f, 22f, 22f);
            var title = BagTxt(screenGo.transform, "Title", "الحقيبة", lalezar, 27.2f, "#d9d0bd", TextAlignmentOptions.Right, false); BagRect(title.gameObject, 100f, 20f, 228f, 43f);
            var sub = BagTxt(screenGo.transform, "Sub", "أغراضك اللي اشتريتها، البس اللي بدك ياه", cairo, 10.56f, "#a98f62", TextAlignmentOptions.Right, false); BagRect(sub.gameObject, 60f, 55f, 268f, 17f);
            var countTag = BagImg(screenGo.transform, "Count", countS, 40.84f - 38f, 48.4f - 20f, 76f, 40f, true);
            countTag.transform.localEulerAngles = new Vector3(0, 0, 3f);
            var countTxt = BagTxt(countTag.transform, "Text", "١١ غرض", lalezar, 12.8f, "#2a1a0e", TextAlignmentOptions.Center, false); Pad(Rt(countTxt.gameObject), 8, 10, 8, 8);
            var header = screenGo.AddComponent<KitScreenHeader>(); SetRef(header, "backButton", backBtn);

            // ---- the item sheet over a dim
            var layer = Go("Sheet", screenGo.transform); Stretch(Rt(layer));
            var dimGo = Go("Dim", layer.transform); Stretch(Rt(dimGo));
            var dimi = dimGo.AddComponent<Image>(); dimi.color = new Color(0f, 0f, 0f, 0.91f); dimi.raycastTarget = true;   // #000a in the browser, in linear light
            var dimBtn = dimGo.AddComponent<Button>(); dimBtn.transition = Selectable.Transition.None; dimBtn.targetGraphic = dimi;
            var sheet = Go("Panel", layer.transform); var shr = Rt(sheet);
            shr.anchorMin = new Vector2(0f, 0f); shr.anchorMax = new Vector2(1f, 0f); shr.pivot = new Vector2(0.5f, 0f); shr.anchoredPosition = Vector2.zero; shr.sizeDelta = new Vector2(0f, 388f * NK);
            var shbg = Go("Bg", sheet.transform); var sbr = Rt(shbg); sbr.anchorMin = Vector2.zero; sbr.anchorMax = Vector2.one; sbr.offsetMin = new Vector2(-30f * NK, 0f); sbr.offsetMax = new Vector2(30f * NK, 30f * NK);
            var sbi = shbg.AddComponent<Image>(); sbi.sprite = sheetS; sbi.type = Image.Type.Sliced; sbi.raycastTarget = true;
            shbg.AddComponent<LayoutElement>().ignoreLayout = true;
            var svl = sheet.AddComponent<VerticalLayoutGroup>();
            svl.padding = new RectOffset(Mathf.RoundToInt(18 * NK), Mathf.RoundToInt(18 * NK), Mathf.RoundToInt(12 * NK), Mathf.RoundToInt(26 * NK));
            svl.spacing = 8f * NK; svl.childAlignment = TextAnchor.UpperCenter; svl.childControlWidth = true; svl.childControlHeight = true; svl.childForceExpandWidth = false; svl.childForceExpandHeight = false;
            sheet.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var hd = Go("Handle", sheet.transform); var hdl = hd.AddComponent<LayoutElement>(); hdl.preferredWidth = 44f * NK; hdl.preferredHeight = 10f * NK;
            var hdb = Go("Bar", hd.transform); var hbr = Rt(hdb); hbr.anchorMin = new Vector2(0f, 1f); hbr.anchorMax = new Vector2(1f, 1f); hbr.pivot = new Vector2(0.5f, 1f); hbr.anchoredPosition = Vector2.zero; hbr.sizeDelta = new Vector2(0f, 4f * NK);
            var hbi = hdb.AddComponent<Image>(); hbi.color = new Color(1f, 1f, 1f, 0.2f); hbi.raycastTarget = false;
            var stage = Go("Stage", sheet.transform); var stl = stage.AddComponent<LayoutElement>(); stl.preferredWidth = 354f * NK; stl.preferredHeight = 150f * NK;
            var stbg = Go("Bg", stage.transform); Pad(Rt(stbg), -2, -2, -2, -2); var stbi = stbg.AddComponent<Image>(); stbi.sprite = stageS; stbi.type = Image.Type.Sliced; stbi.raycastTarget = false;
            var stArt = Go("Art", stage.transform); Pad(Rt(stArt), 10, 10, 10, 10);
            var sname = BagTxt(sheet.transform, "Name", "إطار الحبل", lalezar, 23.2f, "#d9d0bd", TextAlignmentOptions.Center, false);
            var meta = Go("Meta", sheet.transform); var mh = meta.AddComponent<HorizontalLayoutGroup>();
            mh.spacing = 8f * NK; mh.childAlignment = TextAnchor.MiddleCenter; mh.reverseArrangement = true; mh.childControlWidth = true; mh.childControlHeight = true; mh.childForceExpandWidth = false; mh.childForceExpandHeight = false;
            var rr = Go("Rarity", meta.transform); var rri = rr.AddComponent<Image>(); rri.sprite = rrS; rri.type = Image.Type.Sliced; rri.raycastTarget = false;
            var rrl = rr.AddComponent<LayoutElement>(); rrl.preferredWidth = 32.56f * NK; rrl.preferredHeight = 22f * NK;
            var rrt = BagTxt(rr.transform, "Text", "نادر", cairo, 10.88f, "#f3dfa6", TextAlignmentOptions.Center, true); Pad(Rt(rrt.gameObject), 0, 0, 0, 1);
            var kind = BagTxt(meta.transform, "Kind", "الإطارات · حول صورتك بكل غرفة", cairo, 10.88f, "#a98f62", TextAlignmentOptions.Center, true);
            var desc = BagTxt(sheet.transform, "Desc", "حبل مجدول متل حبال المينا.", cairo, 12.8f, "#cdbf9f", TextAlignmentOptions.Center, false);
            desc.GetComponent<ArabicText>().Wrap = true; desc.enableWordWrapping = true; var dle = desc.gameObject.AddComponent<LayoutElement>(); dle.preferredWidth = 354f * NK;
            var acts = Go("Acts", sheet.transform); var avl = acts.AddComponent<VerticalLayoutGroup>();
            avl.padding = new RectOffset(0, 0, Mathf.RoundToInt(6 * NK), 0); avl.spacing = 8f * NK; avl.childAlignment = TextAnchor.UpperCenter; avl.childControlWidth = true; avl.childControlHeight = true; avl.childForceExpandWidth = true; avl.childForceExpandHeight = false;
            acts.AddComponent<LayoutElement>().preferredWidth = 354f * NK;
            var worn = BagTxt(acts.transform, "Worn", "لابسه هلّق", cairo, 12.8f, "#8fd4b4", TextAlignmentOptions.Center, true); worn.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f * NK;
            var wear = Go("Wear", acts.transform); var wi = wear.AddComponent<Image>(); wi.sprite = btnS; wi.type = Image.Type.Sliced; wear.AddComponent<LayoutElement>().preferredHeight = 46f * NK;
            var wearBtn = BagButton(wear);
            var wt = BagTxt(wear.transform, "Text", "البسه", cairo, 13.76f, "#2a1a08", TextAlignmentOptions.Center, true); Pad(Rt(wt.gameObject), 0, 2, 0, 0);
            var off = Go("Off", acts.transform); var oi = off.AddComponent<Image>(); oi.sprite = btnDarkS; oi.type = Image.Type.Sliced; off.AddComponent<LayoutElement>().preferredHeight = 46f * NK;
            var offBtn = BagButton(off);
            var ot = BagTxt(off.transform, "Text", "اشلحه", cairo, 13.76f, "#e8cc8c", TextAlignmentOptions.Center, true); Pad(Rt(ot.gameObject), 0, 2, 0, 0);
            var price = BagTxt(sheet.transform, "Price", "اشتريته من المتجر بـ ٣٠٠ كوينز", cairo, 10.88f, "#a98f62", TextAlignmentOptions.Center, true);
            layer.SetActive(false);

            var so = new SerializedObject(screen);
            void R(string f, Object v) { so.FindProperty(f).objectReferenceValue = v; }
            R("sub", sub); R("count", countTxt); R("countTag", Rt(countTag.gameObject));
            R("look", lookLe); R("lookArt", lar); R("lookName", lookName); R("bitsRoot", bir); R("bitTemplate", bit);
            R("chipsContent", ccr); R("chipTemplate", chip); R("chipOn", chipOnS); R("chipOff", chipS);
            R("list", lr); R("sectionTemplate", sec); R("gridTemplate", grid); R("cellTemplate", cell); R("emptyKindTemplate", ek); R("emptyBag", emptyBag);
            R("cellOn", cellOnS); R("cellOff", cellS);
            R("sheetLayer", layer); R("dim", dimBtn); R("stageArt", Rt(stArt)); R("sheetName", sname); R("sheetRarity", rrt); R("sheetKind", kind); R("sheetDesc", desc); R("sheetPrice", price);
            R("rarityPill", Rt(rr)); R("wearButton", wearBtn); R("offButton", offBtn); R("wornLine", worn.gameObject); R("storeButton", storeBtn);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A full-width strip at a given top (CSS px) inside a parent anchored at the top.</summary>
        private static void TopStrip(RectTransform r, float top, float h)
        {
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -top * NK); r.sizeDelta = new Vector2(0f, h * NK);
        }
    }
}
