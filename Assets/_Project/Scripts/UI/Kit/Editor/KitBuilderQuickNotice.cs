using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// The telegram of Docs/mockups/quick-notify.html as the prefab "QuickNotice" (KitTheme.quickNoticePrefab). The paper, the five wax seals,
    /// the "+2" stamp and the dotted time line are the mockup's own parts, exported by Docs/mockups/export-quicknotify.js into Art/UI/QuickNotice.
    /// KitQuickNotice places everything for the tab's side at runtime; here every part only gets its size (mockup px times MatchK).
    /// </summary>
    public static partial class KitBuilder
    {
        private const string NoticeArt = "Assets/_Project/Art/UI/QuickNotice/";

        [MenuItem("Mafia Night/UI Kit/Build Quick Notice")]
        public static void BuildQuickNoticeMenu()
        {
            var th = BuildTheme();
            headFont = th.fontHeading;
            BuildQuickNotice(th);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIKit] built the quick notice prefab");
        }

        private static void BuildQuickNotice(KitTheme th)
        {
            string keep = MatchArt; MatchArt = NoticeArt;
            var man = ReadMatchManifest();
            if (man.Count == 0) { MatchArt = keep; Debug.LogError("[UIKit] export the notice sprites first (Tools/ui-gen/serve.js + Docs/mockups/export-quicknotify.js)"); return; }
            ImportMatchSprites(man);
            MatchArt = keep;
            float K = MatchK;
            Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(NoticeArt + n + ".png");
            Vector2 Size(string n) { var m = man[n]; return new Vector2((m.w + 2 * m.pad) * K, (m.h + 2 * m.pad) * K); }
            void Mid(GameObject g, Vector2 size) { var r = Rt(g); r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = Vector2.zero; r.sizeDelta = size; }
            Image Pic(string name, Transform parent, string sprite, bool sliced = false)
            {
                var g = Go(name, parent); var i = Img(g, S(sprite), sliced, 1f); i.raycastTarget = false; i.preserveAspect = false; Mid(g, Size(sprite)); return i;
            }
            var lalezar = th.fontLalezar != null ? th.fontLalezar : th.fontHeading; var cairo = th.fontBody;
            var cairoBold = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Fonts/Cairo SDF Bold.mat");
            TextMeshProUGUI T(Transform parent, string name, string text, TMP_FontAsset font, float px, Color color, bool arabic = true)
            {
                var t = Txt(parent, name, text, font, px * K, color, TextAlignmentOptions.Right);
                t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
                if (font == cairo && cairoBold != null) t.fontSharedMaterial = cairoBold;
                if (!arabic) { Object.DestroyImmediate(t.GetComponent<ArabicText>()); t.text = text; t.alignment = TextAlignmentOptions.Center; t.overflowMode = TextOverflowModes.Overflow; }
                return t;
            }

            var root = new GameObject("QuickNotice", typeof(RectTransform));
            root.AddComponent<RectMask2D>();                    // the slot: the telegram hides behind the tab outside it
            var notice = root.AddComponent<KitQuickNotice>();

            var slip = Go("Slip", root.transform); var sr = Rt(slip); sr.sizeDelta = new Vector2(256, 58) * K;
            var slipG = slip.AddComponent<CanvasGroup>();
            var hit = slip.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var btn = slip.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
            Pic("Paper", slip.transform, "qn_slip");
            var seal = Pic("Seal", slip.transform, "qn_seal_mail");
            var texts = Go("Texts", slip.transform); Mid(texts, new Vector2(179, 40) * K);
            var title = T(texts.transform, "Title", "برقية جديدة", lalezar, 17f, new Color32(0x1C, 0x17, 0x0F, 255));
            { var r = Rt(title.gameObject); r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = new Vector2(0, 8.5f * K); r.sizeDelta = new Vector2(179, 34) * K; }   // taller than the line: TMP's ellipsis hides everything when the line does not fit
            var detail = T(texts.transform, "Detail", "هدية من إدارة الحارة · ٥٠٠ كوينز", cairo, 10.6f, new Color32(0x4A, 0x3C, 0x24, 255));
            { var r = Rt(detail.gameObject); r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = new Vector2(0, -9.5f * K); r.sizeDelta = new Vector2(179, 24) * K; }
            var time = Pic("Time", slip.transform, "qn_time"); Rt(time.gameObject).sizeDelta = new Vector2(182 + 2, 4) * K;
            time.type = Image.Type.Filled; time.fillMethod = Image.FillMethod.Horizontal;
            var stamp = Pic("Stamp", slip.transform, "qn_stamp", true); Rt(stamp.gameObject).sizeDelta = new Vector2(30, 20) * K;
            var stampT = T(stamp.transform, "Text", "+٢", lalezar, 11.5f, new Color32(0x8A, 0x1C, 0x24, 255), false); Stretch(Rt(stampT.gameObject));

            void R(string f, Object v) => SetRef(notice, f, v);
            { var so = new SerializedObject(notice); so.FindProperty("k").floatValue = K; so.ApplyModifiedPropertiesWithoutUndo(); }
            R("slip", sr); R("seal", Rt(seal.gameObject)); R("texts", Rt(texts)); R("time", Rt(time.gameObject)); R("stamp", Rt(stamp.gameObject));
            R("sealImage", seal); R("timeImage", time); R("title", title.GetComponent<ArabicText>()); R("detail", detail.GetComponent<ArabicText>()); R("stampText", stampT);
            R("tapButton", btn); R("slipGroup", slipG);
            R("sealMail", S("qn_seal_mail")); R("sealReward", S("qn_seal_reward")); R("sealEvent", S("qn_seal_event")); R("sealEnding", S("qn_seal_ending")); R("sealOffer", S("qn_seal_offer"));

            th.quickNoticePrefab = Save(root, "QuickNotice");
            EditorUtility.SetDirty(th);
        }
    }
}
