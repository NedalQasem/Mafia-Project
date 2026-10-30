using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// "شنطة الغنيمة" of Docs/mockups/reward.html as the prefab "RewardPopup" (KitTheme.rewardPopupPrefab). The pictures are the mockup's own parts,
    /// exported by Docs/mockups/export-reward.js into Art/UI/Reward (manifest.json gives each one's size and padding); every position below is
    /// the mockup's CSS in pixels of the 390x790 phone, turned into canvas units by MatchK. Texts are live (Cairo, Lalezar, Amiri).
    /// </summary>
    public static partial class KitBuilder
    {
        private const string RewardArt = "Assets/_Project/Art/UI/Reward/";

        [MenuItem("Mafia Night/UI Kit/Build Reward Popup")]
        public static void BuildRewardPopupMenu()
        {
            var th = BuildTheme();
            headFont = th.fontHeading;
            BuildRewardPopup(th);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIKit] built the reward popup prefab");
        }

        private static void BuildRewardPopup(KitTheme th)
        {
            // the sprites: same import as the match screens (pixels per unit keep 9-slice borders at the mockup size)
            string keep = MatchArt; MatchArt = RewardArt;
            var man = ReadMatchManifest();
            if (man.Count == 0) { MatchArt = keep; Debug.LogError("[UIKit] export the reward sprites first (Tools/ui-gen/serve.js + Docs/mockups/export-reward.js)"); return; }
            ImportMatchSprites(man);
            MatchArt = keep;
            float K = MatchK;
            Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(RewardArt + n + ".png");
            Vector2 Size(string n) { var m = man[n]; return new Vector2((m.w + 2 * m.pad) * K, (m.h + 2 * m.pad) * K); }
            float Mul(string n) => 1f;      // the importer already sets pixels per unit for this canvas

            var cairo = th.fontBody; var lalezar = th.fontLalezar != null ? th.fontLalezar : th.fontHeading;
            var amiri = FindFont("Amiri") ?? th.fontHeading;
            var cairoBold = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Fonts/Cairo SDF Bold.mat");
            Color ivory = new Color32(0xD9, 0xD0, 0xBD, 255), dimText = new Color32(0x8F, 0x87, 0x77, 255);

            TextMeshProUGUI T(Transform parent, string name, string text, TMP_FontAsset font, float px, Color color, bool bold = false, bool arabic = true, TextAlignmentOptions align = TextAlignmentOptions.Center)
            {
                var t = Txt(parent, name, text, font, px * K, color, align);
                t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
                if (bold && font == cairo && cairoBold != null) t.fontSharedMaterial = cairoBold;
                if (!arabic) { Object.DestroyImmediate(t.GetComponent<ArabicText>()); t.text = text; }
                return t;
            }
            GameObject Pic(string name, Transform parent, string sprite, Vector2 center, bool sliced = false)
            {
                var g = Go(name, parent); var i = Img(g, S(sprite), sliced, Mul(sprite)); i.raycastTarget = false; i.preserveAspect = false;
                var r = Rt(g); r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.sizeDelta = Size(sprite); r.anchoredPosition = center;
                return g;
            }
            void At(GameObject g, Vector2 center, Vector2 size) { var r = Rt(g); r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = center; r.sizeDelta = size; }
            Vector2 P(float x, float y) => new Vector2((x - 195f) * K, (395f - y) * K);          // phone pixel -> stage point
            Vector2 C(float x, float y, float w, float h) => new Vector2((x - w / 2f) * K, (h / 2f - y) * K);   // pixel inside a box of w x h -> point from its center
            CanvasGroup Group(GameObject g) => g.AddComponent<CanvasGroup>();

            var root = new GameObject("RewardPopup", typeof(RectTransform)); Stretch(Rt(root));
            var pop = root.AddComponent<KitRewardPopup>();

            // ---- dim (blocks the screen) and the 390x790 stage
            var dim = Go("Dim", root.transform); Stretch(Rt(dim));
            var dimImg = Img(dim, S("rw_dim")); dimImg.preserveAspect = false; dimImg.raycastTarget = true;
            var under = Go("Black", dim.transform); Stretch(Rt(under)); var ui = under.AddComponent<Image>(); ui.color = new Color(0, 0, 0, .55f); ui.raycastTarget = false; under.transform.SetAsFirstSibling();
            var dimG = Group(dim);
            var stage = Go("Stage", root.transform); At(stage, Vector2.zero, new Vector2(390 * K, 790 * K));

            var spot = Pic("Spot", stage.transform, "rw_spot", Vector2.zero); var spotG = Group(spot);
            var rays = Pic("Rays", stage.transform, "rw_rays", P(195, 790 * .44f)); Rt(rays).sizeDelta = new Vector2(760, 760) * K;     // the mockup's 900 px reads larger on the device: its faint outer rays come out brighter in linear space
            var raysImg = rays.GetComponent<Image>(); raysImg.color = new Color(1, 1, 1, 0);
            var fx = Go("Fx", stage.transform); Stretch(Rt(fx));
            var sp = fx.AddComponent<KitSparks>(); SetRef(sp, "dot", th.dotWhite);
            { var so = new SerializedObject(sp); so.FindProperty("unit").floatValue = K; so.ApplyModifiedPropertiesWithoutUndo(); }

            // ---- where the loot comes from
            var src = Go("Source", stage.transform); At(src, P(195, 152), new Vector2(390 * K, 80 * K)); var srcG = Group(src);
            var kick = T(src.transform, "Kicker", "وصلتك غنيمة", cairo, 11.5f, new Color32(0xC9, 0xB4, 0x8A, 255), true); At(kick.gameObject, P(195, 127.5f) - P(195, 152), new Vector2(380 * K, 18 * K));
            var ttl = T(src.transform, "Title", "هدية من إدارة الحارة", amiri, 33.6f, new Color32(0xF3, 0xE6, 0xC8, 255)); At(ttl.gameObject, P(195, 155.5f) - P(195, 152), new Vector2(380 * K, 40 * K));
            ttl.enableVertexGradient = false;
            var sh = ttl.gameObject.AddComponent<Shadow>(); sh.effectColor = Color.black; sh.effectDistance = new Vector2(0, -2 * K);
            var rule = Pic("Rule", src.transform, "rw_rule", P(195, 184) - P(195, 152)); rule.GetComponent<Image>().color = new Color(1, 1, 1, .8f);

            // ---- the briefcase (210 x 150, its pixels measured from its top-left)
            var cs = Go("Case", stage.transform); At(cs, P(195, 430), new Vector2(210 * K, 150 * K)); Group(cs);
            var hit = cs.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var caseBtn = cs.AddComponent<Button>(); caseBtn.transition = Selectable.Transition.None;
            var shell = Go("Shell", cs.transform); Stretch(Rt(shell));
            Pic("Shadow", shell.transform, "rw_case_shadow", C(105, 157, 210, 150));
            Pic("Body", shell.transform, "rw_case_body", C(105, 98, 210, 150));
            var inside = Pic("Inside", shell.transform, "rw_inside", C(105, 55, 210, 150)); var insideG = Group(inside);
            var plate = Pic("Plate", shell.transform, "rw_case_plate", C(105, 120, 210, 150));
            var plateT = T(plate.transform, "Text", "مكتب الريّس", lalezar, 13.8f, new Color32(0x2A, 0x1A, 0x08, 255)); At(plateT.gameObject, new Vector2(0, -1 * K), new Vector2(96 * K, 20 * K));
            var lid = Pic("Lid", shell.transform, "rw_case_lid", Vector2.zero);
            { var r = Rt(lid); var m = man["rw_case_lid"]; r.pivot = new Vector2(.5f, m.pad / (m.h + 2 * m.pad)); r.anchoredPosition = C(105, 50, 210, 150); }
            RectTransform Latch(string n, float x)
            {
                var l = Pic(n, shell.transform, "rw_latch", Vector2.zero); var r = Rt(l); var m = man["rw_latch"];
                r.pivot = new Vector2(.5f, 1f - (m.pad + 3f) / (m.h + 2 * m.pad)); r.anchoredPosition = C(x, 39, 210, 150); return r;
            }
            var latchA = Latch("LatchRight", 163); var latchB = Latch("LatchLeft", 47);

            var tap = T(stage.transform, "TapHint", "اضغط عالشنطة لتفتحها", cairo, 14.4f, new Color32(0xE6, 0xD9, 0xBD, 255), true); At(tap.gameObject, P(195, 552), new Vector2(300 * K, 24 * K)); var tapG = Group(tap.gameObject);

            // ---- one loot card (104 x 144), kept inactive as the template
            var cards = Go("Cards", stage.transform); Stretch(Rt(cards));
            var card = Go("CardTemplate", cards.transform); At(card, Vector2.zero, new Vector2(104 * K, 144 * K)); Group(card);
            var cHit = card.AddComponent<Image>(); cHit.color = Color.clear; cHit.raycastTarget = true; var cBtn = card.AddComponent<Button>(); cBtn.transition = Selectable.Transition.None;
            Pic("Face", card.transform, "rw_card_common", Vector2.zero);
            var art = Pic("Art", card.transform, "rw_art_coins", C(52, 49, 104, 144)); art.GetComponent<Image>().preserveAspect = true;
            var amount = T(card.transform, "Amount", "+٠", lalezar, 23.2f, new Color32(0xF5, 0xE2, 0xA8, 255), false, false); At(amount.gameObject, C(52, 98, 104, 144), new Vector2(104 * K, 28 * K));
            amount.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -2 * K);
            var nm = T(card.transform, "Name", "إطار الوردة", cairo, 12.8f, new Color32(0xF3, 0xE6, 0xC8, 255), true); At(nm.gameObject, C(52, 96, 104, 144), new Vector2(96 * K, 18 * K));
            var sub = T(nm.transform, "Sub", "إطار صورة · دائم", cairo, 10.2f, dimText); At(sub.gameObject, new Vector2(0, -16 * K), new Vector2(96 * K, 16 * K));
            var rib = T(card.transform, "Ribbon", "عادي", cairo, 10.6f, new Color32(0x12, 0x0C, 0x08, 255), true); At(rib.gameObject, C(52, 132, 104, 144), new Vector2(104 * K, 24 * K));
            var sheen = Go("Sheen", card.transform); At(sheen, Vector2.zero, new Vector2(92 * K, 132 * K)); sheen.AddComponent<RectMask2D>();
            var bar = Pic("Bar", sheen.transform, "rw_sheen", Vector2.zero); Rt(bar).localRotation = Quaternion.Euler(0, 0, -18f); bar.GetComponent<Image>().color = new Color(1, 1, 1, .9f);

            // ---- the bottom panel: what you got, and the buttons
            var panel = Go("Panel", stage.transform); var pr = Rt(panel); pr.anchorMin = new Vector2(0, 0); pr.anchorMax = new Vector2(1, 0); pr.pivot = new Vector2(.5f, 0); pr.anchoredPosition = Vector2.zero; pr.sizeDelta = new Vector2(0, 136 * K);
            var panelG = Group(panel);
            var chips = Go("Chips", panel.transform); var chr = Rt(chips); chr.anchorMin = new Vector2(0, 1); chr.anchorMax = new Vector2(1, 1); chr.pivot = new Vector2(.5f, 1); chr.anchoredPosition = new Vector2(0, -16 * K); chr.sizeDelta = new Vector2(-36 * K, 26 * K);
            var hl = chips.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 8 * K; hl.childAlignment = TextAnchor.MiddleCenter; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false; hl.reverseArrangement = true;
            var chip = Go("ChipTemplate", chips.transform); var chipImg = Img(chip, S("rw_chip"), true, 1f); chipImg.raycastTarget = false;
            var chl = chip.AddComponent<HorizontalLayoutGroup>(); chl.spacing = 5 * K; chl.padding = new RectOffset(Mathf.RoundToInt(10 * K), Mathf.RoundToInt(10 * K), Mathf.RoundToInt(3 * K), Mathf.RoundToInt(3 * K)); chl.childAlignment = TextAnchor.MiddleCenter; chl.childControlWidth = true; chl.childControlHeight = false; chl.childForceExpandWidth = false; chl.reverseArrangement = true;
            chip.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            chip.AddComponent<LayoutElement>().preferredHeight = 26 * K;
            var chipIcon = Go("Icon", chip.transform); var cii = Img(chipIcon, S("rw_coin")); cii.raycastTarget = false; Rt(chipIcon).sizeDelta = new Vector2(16, 16) * K; chipIcon.AddComponent<LayoutElement>().preferredWidth = 16 * K;
            var chipT = T(chip.transform, "Label", "+٥٠٠", cairo, 12.8f, ivory, true); Rt(chipT.gameObject).sizeDelta = new Vector2(60 * K, 20 * K);
            var btns = Go("Buttons", panel.transform); var brt = Rt(btns); brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.pivot = new Vector2(.5f, 0); brt.anchoredPosition = new Vector2(0, 30 * K); brt.sizeDelta = new Vector2(-36 * K, 52 * K);
            var bl = btns.AddComponent<HorizontalLayoutGroup>(); bl.spacing = 10 * K; bl.childControlWidth = true; bl.childControlHeight = true; bl.childForceExpandWidth = false; bl.childForceExpandHeight = true; bl.reverseArrangement = true;
            var take = InstChild("KitButton_Primary", btns.transform, "Collect"); take.GetComponentInChildren<ArabicText>().Text = "استلم"; take.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var wear = InstChild("KitButton_Secondary", btns.transform, "Wear"); wear.GetComponentInChildren<ArabicText>().Text = "البسه هلأ"; wear.AddComponent<LayoutElement>().flexibleWidth = .8f;
            foreach (var t in btns.GetComponentsInChildren<TextMeshProUGUI>(true)) t.fontSize = 46;

            // ---- the wallet the coins and gems fly into (the mockup's top bar pills)
            var wallet = Go("Wallet", stage.transform); Stretch(Rt(wallet)); var walletG = Group(wallet);
            RectTransform Pill(string n, float cx, float w, string icon, float iconPx, out TextMeshProUGUI value)
            {
                var g = Pic(n, wallet.transform, "rw_pill", P(cx, 68), true); Rt(g).sizeDelta = new Vector2((w + 8) * K, 38 * K);
                var ic = Pic("Icon", g.transform, icon, new Vector2((w / 2f - 10f - iconPx / 2f) * K, 0));
                value = T(g.transform, "Value", "٠", cairo, 13.8f, ivory, true, false, TextAlignmentOptions.MidlineRight);
                At(value.gameObject, new Vector2(-(iconPx + 8f) / 2f * K, 0), new Vector2((w - iconPx - 26f) * K, 24 * K));
                return Rt(g);
            }
            var coinPill = Pill("CoinPill", 60, 92, "rw_coin", 16, out var coinV);
            var gemPill = Pill("GemPill", 149, 70, "rw_gem", 13, out var gemV);

            // ---- toast
            var toast = Go("Toast", stage.transform); At(toast, P(195, 119), new Vector2(200 * K, 32 * K)); var toastG = Group(toast);
            var tb = Img(toast, S("rw_chip"), true, 1f); tb.color = new Color(1, 1, 1, 1);
            var toastT = T(toast.transform, "Text", "", cairo, 13f, ivory, true); Stretch(Rt(toastT.gameObject));
            toast.AddComponent<HorizontalLayoutGroup>().padding = new RectOffset(Mathf.RoundToInt(14 * K), Mathf.RoundToInt(14 * K), Mathf.RoundToInt(6 * K), Mathf.RoundToInt(6 * K));
            toast.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---- wiring
            void R(string f, Object v) => SetRef(pop, f, v);
            { var so = new SerializedObject(pop); so.FindProperty("k").floatValue = K; so.ApplyModifiedPropertiesWithoutUndo(); }
            R("stage", Rt(stage)); R("dim", dimG); R("spot", spotG); R("src", srcG); R("tapHint", tapG); R("panel", panelG); R("wallet", walletG);
            R("rays", Rt(rays)); R("raysImage", raysImg); R("sparks", sp);
            R("kicker", kick.GetComponent<ArabicText>()); R("title", ttl.GetComponent<ArabicText>()); R("toast", toastT.GetComponent<ArabicText>()); R("toastGroup", toastG);
            R("caseRoot", Rt(cs)); R("shell", Rt(shell)); R("lid", Rt(lid)); R("latchA", latchA); R("latchB", latchB); R("inside", insideG);
            R("caseButton", caseBtn); R("collectButton", take.GetComponent<Button>()); R("wearButton", wear.GetComponent<Button>());
            R("cardsRoot", Rt(cards)); R("cardTemplate", Rt(card)); R("chipsRoot", Rt(chips)); R("chipTemplate", Rt(chip));
            R("coinPill", coinPill); R("gemPill", gemPill); R("coinValue", coinV); R("gemValue", gemV);
            R("coinToken", S("rw_coin")); R("gemToken", S("rw_gem"));
            R("cardCommon", S("rw_card_common")); R("cardRare", S("rw_card_rare")); R("cardLegend", S("rw_card_legend"));
            R("artCoins", S("rw_art_coins")); R("artGems", S("rw_art_gems")); R("artFrame", S("rw_art_frame")); R("artBubble", S("rw_art_bubble"));

            root.SetActive(false);
            th.rewardPopupPrefab = Save(root, "RewardPopup");
            EditorUtility.SetDirty(th);
        }
    }
}
