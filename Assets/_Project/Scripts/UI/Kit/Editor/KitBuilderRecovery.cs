using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// Account recovery in the settings screen: the "RecoverySheet" prefab (user name + password fields, message, "استرجع" and "لاحقاً") and the
    /// "استرجاع حساب قديم" row of the account section that opens it. The full kit build makes both; the menu below adds them to the settings scene
    /// that is already there without building the whole kit again.
    /// </summary>
    public static partial class KitBuilder
    {
        private const string RecoveryRowTitle = "استرجاع حساب قديم";
        private const string RecoveryRowName = "Link_" + RecoveryRowTitle;

        [MenuItem("Mafia Night/UI Kit/Add Account Recovery To Settings")]
        public static void AddRecoveryToSettingsMenu()
        {
            var th = BuildTheme();
            BuildRecoverySheet(th);
            var scene = EditorSceneManager.OpenScene(SettingsScenePath, OpenSceneMode.Single);
            Transform canvas = null, linkRow = null, oldRow = null, oldSheet = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Link_ربط الحساب (حفظ تقدمك)") linkRow = t;
                    else if (t.name == RecoveryRowName) oldRow = t;
                    else if (t.name == "RecoverySheet") oldSheet = t;
                    if (canvas == null && t.GetComponent<Canvas>() != null) canvas = t;
                }
            if (linkRow == null || canvas == null) { Debug.LogError("[UIKit] the settings scene has no account section; run Build Kit + Test Scene"); return; }
            if (oldRow != null) Object.DestroyImmediate(oldRow.gameObject);
            if (oldSheet != null) Object.DestroyImmediate(oldSheet.gameObject);

            // the new row is a copy of "ربط الحساب" without the grey note on the left
            var row = Object.Instantiate(linkRow.gameObject, linkRow.parent); row.name = RecoveryRowName;
            row.transform.SetSiblingIndex(linkRow.GetSiblingIndex() + 1);
            var extra = row.transform.Find("E"); if (extra != null) Object.DestroyImmediate(extra.gameObject);
            row.transform.Find("T").GetComponent<ArabicText>().Text = RecoveryRowTitle;
            AddRecoverySheet(row, canvas);
            SaveScene(SettingsScenePath);
            Debug.Log("[UIKit] account recovery added to the settings scene");
        }

        /// <summary>Makes a settings row clickable and puts the recovery sheet (closed) on the canvas, opened by that row.</summary>
        private static void AddRecoverySheet(GameObject row, Transform canvas)
        {
            var img = row.GetComponent<Image>(); img.raycastTarget = true;
            var btn = row.GetComponent<Button>(); if (btn == null) btn = row.AddComponent<Button>();
            btn.targetGraphic = img; btn.transition = Selectable.Transition.ColorTint;
            var sheet = InstChild("RecoverySheet", canvas, "RecoverySheet");
            SetRef(sheet.GetComponent<KitRecoverySheet>(), "opener", btn);
            sheet.SetActive(false);
        }

        private static TMP_InputField RecoveryField(KitTheme th, Transform parent, string name, float y, string placeholder, bool password)
        {
            var field = Go(name, parent); Place(Rt(field), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(860, 130));
            var fimg = Img(field, th.paper, true, 2.2f, true);
            var area = Go("Area", field.transform); Stretch(Rt(area), 34, 8, 34, 8); area.AddComponent<RectMask2D>();
            var ph = Txt(area.transform, "Placeholder", placeholder, th.fontBody, 40, new Color32(0xA0, 0x92, 0x72, 255), TextAlignmentOptions.MidlineRight); Stretch(Rt(ph.gameObject));
            // user names and passwords are English: the text is left to right and not shaped
            var txt = Txt(area.transform, "Text", "", th.fontBody, 48, th.crimson, TextAlignmentOptions.MidlineLeft); Object.DestroyImmediate(txt.GetComponent<ArabicText>()); Stretch(Rt(txt.gameObject));
            var tin = field.AddComponent<TMP_InputField>();
            tin.textViewport = Rt(area); tin.textComponent = txt; tin.placeholder = ph; tin.fontAsset = th.fontBody; tin.targetGraphic = fimg;
            tin.characterLimit = password ? 64 : 24; tin.lineType = TMP_InputField.LineType.SingleLine; tin.caretColor = th.crimson; tin.customCaretColor = true;
            // Standard rather than Alphanumeric: dots, dashes and underscores are allowed in user names
            tin.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            tin.keyboardType = TouchScreenKeyboardType.ASCIICapable;
            return tin;
        }

        private static void BuildRecoverySheet(KitTheme th)
        {
            var root = new GameObject("RecoverySheet", typeof(RectTransform)); Stretch(Rt(root));
            var dimGo = Go("Dim", root.transform); Stretch(Rt(dimGo));
            var dimImg = dimGo.AddComponent<Image>(); dimImg.color = new Color(0, 0, 0, 0.62f); dimImg.raycastTarget = true;
            dimGo.AddComponent<Button>().targetGraphic = dimImg;
            var dim = dimGo.AddComponent<CanvasGroup>();
            var sheet = Go("Sheet", root.transform); var sr = Rt(sheet);
            sr.anchorMin = new Vector2(0, 0); sr.anchorMax = new Vector2(1, 0); sr.pivot = new Vector2(0.5f, 0f); sr.anchoredPosition = Vector2.zero; sr.sizeDelta = new Vector2(0, 1000);
            Img(sheet, th.leatherDark, true, 1f, true);
            var t = Txt(sheet.transform, "Title", RecoveryRowTitle, th.fontHeading, 64, th.leatherText, TextAlignmentOptions.MidlineRight); Place(Rt(t.gameObject), new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(900, 90));
            var s = Txt(sheet.transform, "Sub", "اكتب اسم المستخدم وكلمة السر اللي أعطاك ياهم الدعم", th.fontBody, 30, th.ivoryDim, TextAlignmentOptions.MidlineRight); Place(Rt(s.gameObject), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(900, 50));
            var user = RecoveryField(th, sheet.transform, "Username", -270, "اسم المستخدم", false);
            var pass = RecoveryField(th, sheet.transform, "Password", -420, "كلمة السر", true);
            var msg = Txt(sheet.transform, "Msg", "", th.fontBody, 32, new Color32(0xE6, 0x7A, 0x80, 255), TextAlignmentOptions.Center, true); Place(Rt(msg.gameObject), new Vector2(0.5f, 1f), new Vector2(0, -545), new Vector2(900, 90));
            var note = Txt(sheet.transform, "Note", "حسابك الحالي على هالجهاز بيتبدّل بالحساب القديم.", th.fontBody, 26, th.ivoryDim, TextAlignmentOptions.Center); Place(Rt(note.gameObject), new Vector2(0.5f, 1f), new Vector2(0, -630), new Vector2(900, 44));
            var help = Txt(sheet.transform, "Help", "نسيت بياناتك؟ راسل الدعم: " + MafiaNight.Core.GameConstants.SUPPORT_EMAIL, th.fontBody, 28, th.ivory, TextAlignmentOptions.Center); Place(Rt(help.gameObject), new Vector2(0.5f, 1f), new Vector2(0, -705), new Vector2(940, 48));
            var go = InstChild("KitButton_Primary", sheet.transform, "Submit");
            var jr = Rt(go); jr.anchorMin = jr.anchorMax = new Vector2(0.5f, 0f); jr.pivot = new Vector2(0.5f, 0.5f); jr.anchoredPosition = new Vector2(120, 130); jr.sizeDelta = new Vector2(540, 160);
            var goLbl = go.GetComponentInChildren<ArabicText>(); goLbl.Text = "استرجع";
            var close = InstChild("KitButton_Secondary", sheet.transform, "Close");
            var clr = Rt(close); clr.anchorMin = clr.anchorMax = new Vector2(0.5f, 0f); clr.pivot = new Vector2(0.5f, 0.5f); clr.anchoredPosition = new Vector2(-300, 130); clr.sizeDelta = new Vector2(300, 160);
            close.GetComponentInChildren<ArabicText>().Text = "لاحقاً"; close.GetComponentInChildren<TextMeshProUGUI>().fontSize = 44;
            var comp = root.AddComponent<KitRecoverySheet>();
            SetRef(comp, "dim", dim); SetRef(comp, "sheet", sr); SetRef(comp, "usernameInput", user); SetRef(comp, "passwordInput", pass);
            SetRef(comp, "message", msg.GetComponent<ArabicText>()); SetRef(comp, "submitLabel", goLbl);
            SetRef(comp, "submitButton", go.GetComponent<Button>()); SetRef(comp, "closeButton", close.GetComponent<Button>());
            Save(root, "RecoverySheet");
        }
    }
}
