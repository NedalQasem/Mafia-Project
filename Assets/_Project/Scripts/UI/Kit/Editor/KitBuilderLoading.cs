using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    public static partial class KitBuilder
    {
        private const string LoadingScenePath = "Assets/_Project/Scenes/UIKitLoading.unity";

        private static void BuildLoading(KitTheme th)
        {
            Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + n + ".png");
            Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(Art + n + ".png");

            var canvasGo = NewAppCanvas(th, new Color(0.04f, 0.03f, 0.05f));
            var screen = Go("LoadingScreen", canvasGo.transform); Stretch(Rt(screen));

            var sky = Go("Sky", screen.transform); Stretch(Rt(sky)); var skyImg = Img(sky, S("sky_grad")); skyImg.preserveAspect = false; skyImg.raycastTarget = false;

            // stars + moon
            var stars = new Image[48]; var rnd = new System.Random(11);
            for (int i = 0; i < stars.Length; i++)
            {
                var s = Go("Star" + i, screen.transform); var sr = Rt(s);
                sr.anchorMin = sr.anchorMax = new Vector2((float)rnd.NextDouble(), 0.42f + 0.58f * (float)rnd.NextDouble());
                float sz = 3f + (float)rnd.NextDouble() * 4f; sr.sizeDelta = new Vector2(sz, sz); sr.anchoredPosition = Vector2.zero;
                var si = Img(s, th.dotWhite); si.color = new Color(1, 1, 1, 0.6f); stars[i] = si;
            }
            var moon = Go("Moon", screen.transform); Place(Rt(moon), new Vector2(0, 1), new Vector2(850, -800), new Vector2(300, 300)); Img(moon, S("moon")).raycastTarget = false;

            // title
            var title = Txt(screen.transform, "Title", "المافيا", th.fontHeading, 210, new Color32(0xCF, 0xC5, 0xAE, 255));
            Place(Rt(title.gameObject), new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(900, 260));
            var sub = Txt(screen.transform, "Subtitle", "الليلة لا ترحم", th.fontBody, 44, th.brass);
            Place(Rt(sub.gameObject), new Vector2(0.5f, 1), new Vector2(0, -480), new Vector2(700, 70));

            RawImage Raw(string name, Texture2D tex, float y, float h, Color tint)
            {
                var g = Go(name, screen.transform); var r = Rt(g);
                r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0); r.anchoredPosition = new Vector2(0, y); r.sizeDelta = new Vector2(0, h);
                var ri = g.AddComponent<RawImage>(); ri.texture = tex; ri.color = tint; ri.raycastTarget = false;
                ri.uvRect = new Rect(0, 0, 1080f / tex.width, 1f);
                return ri;
            }
            var far = Raw("SkylineFar", T("skyline_far"), 760, 300, Color.white);
            var near = Raw("SkylineNear", T("skyline_near"), 700, 300, Color.white);

            // road
            var road = Go("Road", screen.transform); var rr = Rt(road); rr.anchorMin = new Vector2(0, 0); rr.anchorMax = new Vector2(1, 0); rr.pivot = new Vector2(0.5f, 0); rr.anchoredPosition = Vector2.zero; rr.sizeDelta = new Vector2(0, 700);
            var rimg = road.AddComponent<Image>(); rimg.color = new Color(0.07f, 0.058f, 0.07f); rimg.raycastTarget = false;
            var pool = Go("LightPool", road.transform); Place(Rt(pool), new Vector2(0.5f, 1), new Vector2(-260, -200), new Vector2(1100, 230));
            var poolImg = Img(pool, th.navSpot); poolImg.preserveAspect = false; poolImg.color = new Color(1f, 0.9f, 0.6f, 0.22f); poolImg.raycastTarget = false; pool.transform.localScale = new Vector3(1f, -1f, 1f);
            var curbRaw = Raw("Curb", T("curb_tile"), 676, 36, new Color(1, 1, 1, 0.9f));
            var dashRaw = Raw("Dash", T("road_dash"), 448, 14, new Color(1, 1, 1, 0.5f));

            // car (faces left: the container is mirrored)
            var carRoot = Go("Car", screen.transform); var cr = Rt(carRoot);
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0); cr.pivot = new Vector2(0.5f, 0.5f); cr.anchoredPosition = new Vector2(0, 610); cr.sizeDelta = new Vector2(760, 340);
            carRoot.transform.localScale = new Vector3(-1f, 1f, 1f);
            var shadow = Go("Shadow", carRoot.transform); Place(Rt(shadow), new Vector2(0.5f, 0.5f), new Vector2(0, -144), new Vector2(700, 40)); var shImg = Img(shadow, th.glowFloor); shImg.preserveAspect = false; shImg.color = new Color(0, 0, 0, 0.9f);
            var beamGo = Go("Beam", carRoot.transform); var br = Rt(beamGo); br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f); br.pivot = new Vector2(0, 0.5f); br.anchoredPosition = new Vector2(272, -18); br.sizeDelta = new Vector2(728, 300);
            var beamImg = Img(beamGo, S("car_beam")); beamImg.preserveAspect = false; beamImg.raycastTarget = false;
            var puffRoot = Go("Exhaust", carRoot.transform); Place(Rt(puffRoot), new Vector2(0.5f, 0.5f), new Vector2(-332, -110), new Vector2(10, 10));
            var puffs = new Image[3];
            for (int i = 0; i < 3; i++) { var p = Go("Puff" + i, puffRoot.transform); Place(Rt(p), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60)); puffs[i] = Img(p, S("puff")); puffs[i].raycastTarget = false; }
            var body = Go("Body", carRoot.transform); Place(Rt(body), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 340)); Img(body, S("car_body")).raycastTarget = false;
            var wr = Go("WheelRear", carRoot.transform); Place(Rt(wr), new Vector2(0.5f, 0.5f), new Vector2(-196, -90), new Vector2(100, 100)); Img(wr, S("car_wheel")).raycastTarget = false;
            var wf = Go("WheelFront", carRoot.transform); Place(Rt(wf), new Vector2(0.5f, 0.5f), new Vector2(204, -90), new Vector2(100, 100)); Img(wf, S("car_wheel")).raycastTarget = false;

            // progress
            var pct = Txt(screen.transform, "Percent", "٠٪", th.fontHeading, 92, th.brassLight, TextAlignmentOptions.MidlineRight);
            Place(Rt(pct.gameObject), new Vector2(1, 0), new Vector2(-300, 330), new Vector2(380, 110));
            var phs = Txt(screen.transform, "Phase", "نتّصل بالحارة...", th.fontBody, 38, th.ivoryDim, TextAlignmentOptions.MidlineLeft);
            Place(Rt(phs.gameObject), new Vector2(0, 0), new Vector2(330, 330), new Vector2(560, 70));
            var bar = Go("Bar", screen.transform); Place(Rt(bar), new Vector2(0.5f, 0), new Vector2(0, 260), new Vector2(920, 50)); Img(bar, th.slotDark, true, 1.6f);
            var fillMask = Go("FillArea", bar.transform); Stretch(Rt(fillMask), 12, 12, 12, 12);
            var fill = Go("Fill", fillMask.transform); var fr = Rt(fill); fr.anchorMin = new Vector2(1, 0); fr.anchorMax = new Vector2(1, 1); fr.offsetMin = fr.offsetMax = Vector2.zero;
            var fi = fill.AddComponent<Image>(); fi.color = new Color32(0xD2, 0xAD, 0x60, 255); fi.raycastTarget = false;
            var tip = Txt(screen.transform, "Tip", "المختار صوته يحسب صوتين.", th.fontBody, 34, th.ivoryDim, TextAlignmentOptions.Center, true);
            Place(Rt(tip.gameObject), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(900, 80));
            var enter = InstChild("KitButton_Brass", screen.transform, "Enter");
            Place(Rt(enter), new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(560, 170));
            enter.GetComponentInChildren<ArabicText>().Text = "تفضّل"; enter.SetActive(false);

            var load = screen.AddComponent<KitLoadingScreen>();
            SetRef(load, "farCity", far); SetRef(load, "nearCity", near); SetRef(load, "dash", dashRaw); SetRef(load, "curb", curbRaw);
            SetRef(load, "car", cr); SetRef(load, "wheelFront", Rt(wf)); SetRef(load, "wheelRear", Rt(wr)); SetRef(load, "beam", beamImg);
            var so = new SerializedObject(load);
            var sa = so.FindProperty("stars"); sa.arraySize = stars.Length; for (int i = 0; i < stars.Length; i++) sa.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            var pa = so.FindProperty("puffs"); pa.arraySize = puffs.Length; for (int i = 0; i < puffs.Length; i++) pa.GetArrayElementAtIndex(i).objectReferenceValue = puffs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            SetRef(load, "barFill", fr); SetRef(load, "percent", pct.GetComponent<ArabicText>()); SetRef(load, "phase", phs.GetComponent<ArabicText>());
            SetRef(load, "tip", tip.GetComponent<ArabicText>()); SetRef(load, "enterButton", enter); SetRef(load, "enter", enter.GetComponent<Button>());

            // error state of the boot screen: dark veil, a leather card with the message and a retry button
            var errPanel = Go("ErrorPanel", screen.transform); Stretch(Rt(errPanel));
            var veil = errPanel.AddComponent<Image>(); veil.color = new Color(0f, 0f, 0f, 0.78f);
            var card = Go("Card", errPanel.transform); Place(Rt(card), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 640)); Img(card, th.leatherDark, true, 1.6f, true);
            var errTitle = Txt(card.transform, "Title", "تعذّر الدخول", th.fontCalligraphy, 84, th.brassLight); Place(Rt(errTitle.gameObject), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(780, 110));
            var errMsg = Txt(card.transform, "Message", "حدث خطأ", th.fontBody, 40, th.leatherText, TextAlignmentOptions.Center, true); Place(Rt(errMsg.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(760, 230));
            var retry = InstChild("KitButton_Primary", card.transform, "Retry"); Place(Rt(retry), new Vector2(0.5f, 0f), new Vector2(0, 100), new Vector2(560, 170));
            retry.GetComponentInChildren<ArabicText>().Text = "حاول مجدداً";
            errPanel.SetActive(false);
            SetRef(load, "errorPanel", errPanel); SetRef(load, "errorLabel", errMsg.GetComponent<ArabicText>()); SetRef(load, "retryButton", retry.GetComponent<Button>());

            // prefab for the Bootstrap scene: the same screen on its own overlay canvas, driven from outside (no prototype run)
            {
                var bootRoot = new GameObject("BootLoading", typeof(RectTransform));
                var bc = bootRoot.AddComponent<Canvas>(); bc.renderMode = RenderMode.ScreenSpaceOverlay; bc.sortingOrder = 200;
                var bs = bootRoot.AddComponent<CanvasScaler>(); bs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; bs.referenceResolution = new Vector2(1080, 2400); bs.matchWidthOrHeight = 0f;
                bootRoot.AddComponent<GraphicRaycaster>();
                var clone = Object.Instantiate(screen, bootRoot.transform); clone.name = "LoadingScreen"; Stretch(Rt(clone));
                var cloneLoad = clone.GetComponent<KitLoadingScreen>();
                var cso = new SerializedObject(cloneLoad); cso.FindProperty("autoRun").boolValue = false; cso.ApplyModifiedPropertiesWithoutUndo();
                Save(bootRoot, "BootLoading");
            }

            // show a mid-way frame in the saved scene
            pct.GetComponent<ArabicText>().Text = "٥١٪"; phs.GetComponent<ArabicText>().Text = "نجهّز الطاولة...";
            fr.anchorMin = new Vector2(0.49f, 0); fr.anchorMax = new Vector2(1, 1);
            SaveScene(LoadingScenePath);
        }
    }
}
