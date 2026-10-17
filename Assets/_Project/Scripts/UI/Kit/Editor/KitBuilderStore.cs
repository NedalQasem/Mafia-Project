using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    public static partial class KitBuilder
    {
        private const string StoreScenePath = "Assets/_Project/Scenes/UIKitStore.unity";

        private static void BuildStore(KitTheme th)
        {
            var ink = th.paperInk; var soft = new Color32(0x6A, 0x5A, 0x3A, 255);
            var crim = new Color32(0x8A, 0x1C, 0x24, 255);

            // ---------- item standing on a shelf
            {
                var root = new GameObject("StoreItem", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(300, 440);
                var hit = root.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
                var btn = root.AddComponent<Button>(); btn.targetGraphic = hit; btn.transition = Selectable.Transition.None;
                var cg = root.AddComponent<CanvasGroup>();
                var le = root.AddComponent<LayoutElement>(); le.preferredWidth = 300; le.preferredHeight = 440;
                var fx = root.AddComponent<KitButtonFx>(); SetRef(fx, "face", Rt(root));

                var cone = Go("Cone", root.transform); Place(Rt(cone), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(290, 417)); var coneImg = Img(cone, th.itemCone); coneImg.preserveAspect = false; coneImg.raycastTarget = false;
                var glow = Go("Glow", root.transform); Place(Rt(glow), new Vector2(0.5f, 0.5f), new Vector2(0, -88), new Vector2(330, 84));
                var glowImg = Img(glow, th.rarGlow); glowImg.preserveAspect = false; glowImg.raycastTarget = false;
                var art = Go("Art", root.transform); Place(Rt(art), new Vector2(0.5f, 0.5f), new Vector2(0, 52), new Vector2(240, 240));
                var nm = Txt(root.transform, "Name", "اسم الغرض", th.fontHeading, 32, th.leatherText, TextAlignmentOptions.Top);
                Place(Rt(nm.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0, -158), new Vector2(290, 90));

                var tag = Go("Tag", root.transform); Place(Rt(tag), new Vector2(0.5f, 0.5f), new Vector2(76, 172), new Vector2(172, 78));
                Rt(tag).pivot = new Vector2(1f, 1f); Rt(tag).anchoredPosition = new Vector2(76f + 86f, 172f + 39f);   // swings around its top-right corner
                tag.transform.localRotation = Quaternion.Euler(0, 0, -5f);
                tag.AddComponent<KitSwing>();
                Img(tag, th.priceTag, true, 1f);
                var coin = Go("Coin", tag.transform); Place(Rt(coin), new Vector2(0, 0.5f), new Vector2(34, 0), new Vector2(34, 34)); Img(coin, th.coin);
                var price = Txt(tag.transform, "Price", "٢٥٠", th.fontHeading, 34, ink);
                Stretch(Rt(price.gameObject), 54, 8, 50, 8);

                var owned = Go("Owned", root.transform); Place(Rt(owned), new Vector2(0.5f, 0.5f), new Vector2(0, 66), new Vector2(180, 74));
                owned.transform.localRotation = Quaternion.Euler(0, 0, 12f);
                var oi = Img(owned, th.stampFrame, true, 1f); oi.color = th.teal;
                var ot = Txt(owned.transform, "T", "ملكك", th.fontCalligraphy, 46, th.teal); Stretch(Rt(ot.gameObject), 4, 4, 4, 4);

                var nw = Go("New", root.transform); Place(Rt(nw), new Vector2(0.5f, 0.5f), new Vector2(-96, 184), new Vector2(104, 40));
                var nwi = nw.AddComponent<Image>(); nwi.color = th.teal; nwi.raycastTarget = false;
                var nwt = Txt(nw.transform, "T", "جديد", th.fontHeading, 24, new Color32(0x06, 0x23, 0x1A, 255)); Stretch(Rt(nwt.gameObject), 0, 2, 0, 2);

                var lk = Go("Lock", root.transform); Place(Rt(lk), new Vector2(0.5f, 0.5f), new Vector2(0, 162), new Vector2(220, 54));
                Img(lk, th.slotDark, true, 2f);
                var lkt = Txt(lk.transform, "T", "رتبة", th.fontBody, 26, th.leatherText); Stretch(Rt(lkt.gameObject), 4, 4, 4, 8);

                var v = root.AddComponent<StoreItemView>();
                SetRef(v, "artRoot", Rt(art)); SetRef(v, "glow", glowImg);
                SetRef(v, "nameLabel", nm.GetComponent<ArabicText>()); SetRef(v, "priceLabel", price.GetComponent<ArabicText>()); SetRef(v, "lockLabel", lkt.GetComponent<ArabicText>());
                SetRef(v, "priceTag", tag); SetRef(v, "ownedStamp", owned); SetRef(v, "newFlag", nw); SetRef(v, "lockBadge", lk);
                SetRef(v, "button", btn); SetRef(v, "group", cg);
                Save(root, "StoreItem");
            }

            // ---------- shelf row (plank + row of items)
            {
                var root = new GameObject("StoreShelf", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(1080, 470);
                var le = root.AddComponent<LayoutElement>(); le.preferredWidth = 1080; le.preferredHeight = 470;
                var plank = Go("Plank", root.transform);
                var pr = Rt(plank); pr.anchorMin = new Vector2(0, 0); pr.anchorMax = new Vector2(1, 0); pr.pivot = new Vector2(0.5f, 0); pr.offsetMin = new Vector2(10, 0); pr.offsetMax = new Vector2(-10, 74);
                Img(plank, th.plank, true, 1f);
                var row = Go("Row", root.transform);
                var rr = Rt(row); rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = new Vector2(0, 40); rr.offsetMax = Vector2.zero;
                var h = row.AddComponent<HorizontalLayoutGroup>(); h.spacing = 20; h.childAlignment = TextAnchor.LowerCenter;
                h.childControlWidth = false; h.childControlHeight = false; h.childForceExpandWidth = false; h.childForceExpandHeight = false; h.reverseArrangement = true;
                Save(root, "StoreShelf");
            }

            // ---------- category drawer, shelf header, ad page, ad dot
            {
                var root = new GameObject("StoreCat", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(250, 112);
                var img = Img(root, th.leatherDark, true, 1.5f, true);
                var b = root.AddComponent<Button>(); b.targetGraphic = img; b.transition = Selectable.Transition.None;
                var le = root.AddComponent<LayoutElement>(); le.preferredWidth = 250; le.preferredHeight = 112;
                var t = Txt(root.transform, "Label", "الإطارات", th.fontHeading, 34, th.ivoryDim); Stretch(Rt(t.gameObject), 8, 14, 8, 10);
                t.enableAutoSizing = true; t.fontSizeMin = 22; t.fontSizeMax = 34; t.textWrappingMode = TextWrappingModes.NoWrap;
                Save(root, "StoreCat");
            }
            {
                var root = new GameObject("StoreHeader", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(1080, 90);
                var le = root.AddComponent<LayoutElement>(); le.preferredWidth = 1080; le.preferredHeight = 90;
                var t = Txt(root.transform, "Label", "الإطارات", th.fontHeading, 36, th.brass, TextAlignmentOptions.MidlineRight);
                Stretch(Rt(t.gameObject), 40, 0, 50, 0);
                Save(root, "StoreHeader");
            }
            {
                var root = new GameObject("StoreAdDot", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(30, 30);
                Img(root, th.dotWhite).color = new Color(0.3f, 0.26f, 0.2f);
                var le = root.AddComponent<LayoutElement>(); le.preferredWidth = 30; le.preferredHeight = 30;
                Save(root, "StoreAdDot");
            }
            {
                var root = new GameObject("StoreAdPage", typeof(RectTransform));
                Rt(root).sizeDelta = new Vector2(1000, 420);
                Img(root, th.paper, true, 1f);
                root.AddComponent<CanvasGroup>();
                var mast = Go("Mast", root.transform); PlaceTop(Rt(mast), 0, -22, 920, 50);
                var mr = Txt(mast.transform, "Right", "عرض الليلة فقط", th.fontBody, 28, crim, TextAlignmentOptions.MidlineLeft); mr.gameObject.name = "Right";
                Stretch(Rt(mr.gameObject), 0, 0, 0, 0);
                var ml = Txt(mast.transform, "Left", "جريدة الليل", th.fontHeading, 28, ink, TextAlignmentOptions.MidlineRight); Stretch(Rt(ml.gameObject), 0, 0, 0, 0);
                var ln = Go("Line", mast.transform); var lnr = Rt(ln); lnr.anchorMin = new Vector2(0, 0); lnr.anchorMax = new Vector2(1, 0); lnr.pivot = new Vector2(0.5f, 0); lnr.offsetMin = Vector2.zero; lnr.offsetMax = new Vector2(0, 3);
                var lni = ln.AddComponent<Image>(); lni.color = ink; lni.raycastTarget = false;
                var hl = Txt(root.transform, "Headline", "عنوان العرض", th.fontCalligraphy, 40, ink, TextAlignmentOptions.TopRight); PlaceTop(Rt(hl.gameObject), 215, -76, 520, 124);
                var bd = Txt(root.transform, "Body", "نص العرض", th.fontBody, 28, soft, TextAlignmentOptions.TopRight, true); PlaceTop(Rt(bd.gameObject), 215, -206, 520, 44);
                var cd = Go("Countdown", root.transform); PlaceTop(Rt(cd), 215, -258, 520, 44);
                var cl = Txt(cd.transform, "Label", "ينتهي خلال", th.fontBody, 28, crim, TextAlignmentOptions.MidlineRight); Stretch(Rt(cl.gameObject), 220, 0, 0, 0);
                var cv = Txt(cd.transform, "Value", "٠٢:١٤:٣٣", th.fontHeading, 40, ink, TextAlignmentOptions.MidlineLeft); cv.gameObject.name = "Value"; Stretch(Rt(cv.gameObject), 0, 0, 300, 0);
                var buy = InstChild("KitButton_Primary", root.transform, "Buy");
                Place(Rt(buy), new Vector2(0.5f, 0f), new Vector2(210, 62), new Vector2(480, 168)); buy.transform.localScale = Vector3.one * 0.6f;
                var art = Go("Art", root.transform); Place(Rt(art), new Vector2(0.5f, 0.5f), new Vector2(-300, -24), new Vector2(300, 300));
                var st = Go("Stamp", root.transform); Place(Rt(st), new Vector2(0.5f, 1f), new Vector2(-380, -120), new Vector2(190, 80));
                st.transform.localRotation = Quaternion.Euler(0, 0, 12f);
                var sti = Img(st, th.stampFrame, true, 1f); sti.color = new Color32(0xC0, 0x32, 0x3A, 255);
                var stt = Txt(st.transform, "T", "−٣٠٪", th.fontHeading, 44, new Color32(0xC0, 0x32, 0x3A, 255)); Stretch(Rt(stt.gameObject), 6, 6, 6, 6);
                Save(root, "StoreAdPage");
            }

            // ---------- the screen, in its own scene
            var canvasGo = NewAppCanvas(th, new Color(0.075f, 0.055f, 0.045f));
            var screen = Go("StoreScreen", canvasGo.transform); Stretch(Rt(screen));
            var scrollGo = Go("Scroll", screen.transform);
            var sr = Rt(scrollGo); sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = new Vector2(0, 0); sr.offsetMax = new Vector2(0, -150);
            var scroll = scrollGo.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 40f;
            var vp = Go("Viewport", scrollGo.transform); Stretch(Rt(vp)); vp.AddComponent<RectMask2D>();
            var vpi = vp.AddComponent<Image>(); vpi.color = Color.clear;
            var content = Go("Content", vp.transform);
            var cRt = Rt(content); cRt.anchorMin = new Vector2(0, 1); cRt.anchorMax = new Vector2(1, 1); cRt.pivot = new Vector2(0.5f, 1); cRt.anchoredPosition = Vector2.zero; cRt.sizeDelta = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>(); vlg.spacing = 14; vlg.padding = new RectOffset(0, 0, 24, 90); vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = false; vlg.childControlHeight = false; vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = Rt(vp); scroll.content = cRt;

            var adSec = Go("AdSection", content.transform); Rt(adSec).sizeDelta = new Vector2(1000, 450);
            adSec.AddComponent<LayoutElement>().preferredHeight = 450;
            var pagesRoot = Go("Pages", adSec.transform); var pg = Rt(pagesRoot); pg.anchorMin = new Vector2(0.5f, 1); pg.anchorMax = new Vector2(0.5f, 1); pg.pivot = new Vector2(0.5f, 1); pg.sizeDelta = new Vector2(1000, 420);
            var dotsRoot = Go("Dots", adSec.transform); var dr = Rt(dotsRoot); dr.anchorMin = new Vector2(0.5f, 0); dr.anchorMax = new Vector2(0.5f, 0); dr.pivot = new Vector2(0.5f, 0); dr.sizeDelta = new Vector2(300, 30); dr.anchoredPosition = new Vector2(0, 0);
            var dl = dotsRoot.AddComponent<HorizontalLayoutGroup>(); dl.spacing = 16; dl.childAlignment = TextAnchor.MiddleCenter; dl.childControlWidth = false; dl.childControlHeight = false; dl.childForceExpandWidth = false;
            var adHit = adSec.AddComponent<Image>(); adHit.color = Color.clear;
            var barTrack = Go("Progress", adSec.transform); var bt = Rt(barTrack); bt.anchorMin = new Vector2(0, 1); bt.anchorMax = new Vector2(1, 1); bt.pivot = new Vector2(0.5f, 1); bt.offsetMin = new Vector2(38, -12); bt.offsetMax = new Vector2(-38, 0); bt.sizeDelta = new Vector2(bt.sizeDelta.x, 8);
            var btI = barTrack.AddComponent<Image>(); btI.color = new Color(1, 1, 1, 0.08f); btI.raycastTarget = false;
            var barFill = Go("Fill", barTrack.transform); var bf = Rt(barFill); bf.anchorMin = Vector2.zero; bf.anchorMax = Vector2.one; bf.offsetMin = bf.offsetMax = Vector2.zero;
            var bfI = barFill.AddComponent<Image>(); bfI.color = new Color32(0xC0, 0x32, 0x3A, 255); bfI.raycastTarget = false;
            var carousel = adSec.AddComponent<StoreAdCarousel>(); SetRef(carousel, "progress", bf);
            SetRef(carousel, "pagesRoot", pg); SetRef(carousel, "dotsRoot", dr); SetRef(carousel, "pagePrefab", LoadPrefab("StoreAdPage")); SetRef(carousel, "dotPrefab", LoadPrefab("StoreAdDot"));

            var catsScroll = Go("CatsScroll", content.transform); Rt(catsScroll).sizeDelta = new Vector2(1080, 140);
            catsScroll.AddComponent<LayoutElement>().preferredHeight = 140;
            var csr = catsScroll.AddComponent<ScrollRect>(); csr.vertical = false; csr.movementType = ScrollRect.MovementType.Elastic;
            var cvp = Go("Viewport", catsScroll.transform); Stretch(Rt(cvp)); cvp.AddComponent<RectMask2D>();
            var cvpi = cvp.AddComponent<Image>(); cvpi.color = Color.clear;
            var catsRoot = Go("Cats", cvp.transform);
            var ccr = Rt(catsRoot); ccr.anchorMin = new Vector2(0, 0); ccr.anchorMax = new Vector2(0, 1); ccr.pivot = new Vector2(1, 0.5f); ccr.anchoredPosition = Vector2.zero; ccr.sizeDelta = new Vector2(0, 0);
            ccr.anchorMin = new Vector2(1, 0); ccr.anchorMax = new Vector2(1, 1); ccr.pivot = new Vector2(1, 0.5f);   // grows to the left (RTL)
            var chl = catsRoot.AddComponent<HorizontalLayoutGroup>(); chl.spacing = 14; chl.padding = new RectOffset(30, 30, 10, 10); chl.childAlignment = TextAnchor.MiddleRight;
            chl.childControlWidth = false; chl.childControlHeight = false; chl.childForceExpandWidth = false; chl.reverseArrangement = true;
            catsRoot.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csr.viewport = Rt(cvp); csr.content = ccr;

            var shelves = Go("Shelves", content.transform);
            var shr = Rt(shelves); shr.sizeDelta = new Vector2(1080, 100);
            var svl = shelves.AddComponent<VerticalLayoutGroup>(); svl.spacing = 0; svl.childAlignment = TextAnchor.UpperCenter;
            svl.childControlWidth = false; svl.childControlHeight = false; svl.childForceExpandWidth = false; svl.childForceExpandHeight = false;
            var shLe = shelves.AddComponent<LayoutElement>(); shLe.preferredWidth = 1080; shLe.preferredHeight = 100;

            var store = screen.AddComponent<KitStoreScreen>();
            SetRef(store, "carousel", carousel); SetRef(store, "catsRoot", ccr); SetRef(store, "shelvesRoot", shr);
            SetRef(store, "catPrefab", LoadPrefab("StoreCat")); SetRef(store, "shelfPrefab", LoadPrefab("StoreShelf"));
            SetRef(store, "itemPrefab", LoadPrefab("StoreItem")); SetRef(store, "headerPrefab", LoadPrefab("StoreHeader"));

            AddScreenHeader(canvasGo, th, "المتجر", "زينة وهدايا، بدون مزايا لعب", true);

            // ---------- buy sheet (above the bars)
            var overlay = Go("StoreOverlay", canvasGo.transform); Stretch(Rt(overlay));
            var dim = Go("Dim", overlay.transform); Stretch(Rt(dim));
            var dimImg = dim.AddComponent<Image>(); dimImg.color = new Color(0, 0, 0, 0.67f);
            dim.AddComponent<Button>().targetGraphic = dimImg;
            var dcg = dim.AddComponent<CanvasGroup>(); dcg.alpha = 0f; dcg.blocksRaycasts = false;
            var sheet = Go("Sheet", dim.transform);
            var shRt = Rt(sheet); shRt.anchorMin = new Vector2(0, 0); shRt.anchorMax = new Vector2(1, 0); shRt.pivot = new Vector2(0.5f, 0); shRt.sizeDelta = new Vector2(0, 980); shRt.anchoredPosition = new Vector2(0, -1060);
            var sheetImg = Img(sheet, th.sheetBg, true, 1f, true);
            var handle = Go("Handle", sheet.transform); PlaceTop(Rt(handle), 0, -22, 122, 11); Img(handle, th.sheetHandle).raycastTarget = false;
            // stage: a cone of light with the item floating in it
            var stageCone = Go("StageCone", sheet.transform); PlaceTop(Rt(stageCone), 0, -64, 650, 384); var scI = Img(stageCone, th.itemCone); scI.preserveAspect = false; scI.raycastTarget = false; scI.color = new Color(1f, 1f, 1f, 1f);
            var prev = Go("Preview", sheet.transform); PlaceTop(Rt(prev), 0, -112, 300, 300); prev.AddComponent<KitBob>(); prev.transform.localScale = Vector3.one * 1.42f;
            var sn = Txt(sheet.transform, "Name", "اسم", th.fontCalligraphy, 76, th.leatherText); PlaceTop(Rt(sn.gameObject), 0, -452, 900, 100);
            var chip = Go("Chip", sheet.transform); PlaceTop(Rt(chip), 0, -566, 190, 54); var chipImg = Img(chip, th.chipSheet, true, 1f); chipImg.raycastTarget = false;
            var chipT = Txt(chip.transform, "T", "نادر", th.fontHeading, 26, th.leatherText); Stretch(Rt(chipT.gameObject), 4, 2, 4, 6);
            var sd = Txt(sheet.transform, "Desc", "وصف", th.fontBody, 32, new Color32(0xA7, 0x9D, 0x88, 255), TextAlignmentOptions.Top, true); PlaceTop(Rt(sd.gameObject), 0, -640, 860, 100);
            var sm = Txt(sheet.transform, "Msg", "", th.fontHeading, 28, new Color32(0xE6, 0xA0, 0xA4, 255)); PlaceTop(Rt(sm.gameObject), 0, -745, 860, 50);
            // buttons: dark "later" on the right, wide gold buy on the left (RTL), like the mockup
            var close = Go("Close", sheet.transform); var clRt = Rt(close); clRt.anchorMin = clRt.anchorMax = new Vector2(0.5f, 0f); clRt.pivot = new Vector2(0.5f, 0f); clRt.anchoredPosition = new Vector2(300, 50); clRt.sizeDelta = new Vector2(380, 150);
            var closeImg = Img(close, th.btnDark, true, 1f, true);
            var closeBtn = close.AddComponent<Button>(); closeBtn.targetGraphic = closeImg; closeBtn.transition = Selectable.Transition.None;
            var closeFx = close.AddComponent<KitButtonFx>(); SetRef(closeFx, "face", clRt);
            var closeT = Txt(close.transform, "Label", "لاحقاً", th.fontHeading, 42, new Color32(0xE6, 0xD9, 0xBD, 255)); Stretch(Rt(closeT.gameObject), 6, 22, 6, 8);
            var sbuy = Go("Buy", sheet.transform); var bl = Rt(sbuy); bl.anchorMin = bl.anchorMax = new Vector2(0.5f, 0f); bl.pivot = new Vector2(0.5f, 0f); bl.anchoredPosition = new Vector2(-205, 50); bl.sizeDelta = new Vector2(570, 150);
            var buyImg = Img(sbuy, th.btnGold, true, 1f, true);
            var buyBtn = sbuy.AddComponent<Button>(); buyBtn.targetGraphic = buyImg; buyBtn.transition = Selectable.Transition.None;
            var buyFx = sbuy.AddComponent<KitButtonFx>(); SetRef(buyFx, "face", bl);
            var buyT = Txt(sbuy.transform, "Label", "٤٠٠", th.fontHeading, 46, new Color32(0x2A, 0x1A, 0x08, 255)); Stretch(Rt(buyT.gameObject), 6, 22, 6, 8);
            var buyCoin = Go("Coin", sbuy.transform); Place(Rt(buyCoin), new Vector2(0.5f, 0.55f), new Vector2(60, 0), new Vector2(54, 54)); Img(buyCoin, th.coin).raycastTarget = false;
            buyT.alignment = TextAlignmentOptions.Center; Rt(buyT.gameObject).offsetMax = new Vector2(-60, Rt(buyT.gameObject).offsetMax.y);

            SetRef(store, "dim", dcg); SetRef(store, "sheet", shRt); SetRef(store, "preview", Rt(prev));
            SetRef(store, "sheetName", sn.GetComponent<ArabicText>()); SetRef(store, "sheetDesc", sd.GetComponent<ArabicText>());
            SetRef(store, "sheetChip", chipT.GetComponent<ArabicText>()); SetRef(store, "sheetPrice", buyT.GetComponent<ArabicText>());
            SetRef(store, "sheetMsg", sm.GetComponent<ArabicText>()); SetRef(store, "sheetChipBg", chipImg); SetRef(store, "sheetBuyImage", buyImg);
            SetRef(store, "sheetBuy", buyBtn); SetRef(store, "sheetClose", closeBtn);
            store.Build();
            LayoutRebuilder.ForceRebuildLayoutImmediate(cRt);

            SaveScene(StoreScenePath);
        }
    }
}
