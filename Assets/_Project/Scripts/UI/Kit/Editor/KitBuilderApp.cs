using TMPro;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Shared pieces for the per-screen prototype scenes (canvas, background, top/bottom bars).</summary>
    public static partial class KitBuilder
    {
        private static GameObject NewAppCanvas(KitTheme th, Color bgColor)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.04f, 0.03f, 0.03f); cam.orthographic = true;
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 2400); scaler.matchWidthOrHeight = 0f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>();
            var ism = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (ism != null) es.AddComponent(ism); else es.AddComponent<StandaloneInputModule>();
            var bg = Go("Background", canvasGo.transform); Stretch(Rt(bg));
            var bgi = bg.AddComponent<Image>(); bgi.color = bgColor; bgi.raycastTarget = false;
            return MakeSafeRoot(canvasGo, false);
        }

        /// <summary>
        /// A full-screen container fitted to the device's safe area. Screens are built inside it so nothing sits under a notch
        /// or the gesture bar; the Background stays outside. Returns the container (callers use it where they used the canvas).
        /// With bottomFill a strip in the footer's colour is painted behind the gesture bar.
        /// </summary>
        private static GameObject MakeSafeRoot(GameObject canvasGo, bool bottomFill)
        {
            RectTransform fillRt = null;
            if (bottomFill)
            {
                var fill = Go("BottomFill", canvasGo.transform); fillRt = Rt(fill);
                fillRt.anchorMin = new Vector2(0, 0); fillRt.anchorMax = new Vector2(1, 0); fillRt.pivot = new Vector2(0.5f, 0); fillRt.anchoredPosition = Vector2.zero; fillRt.sizeDelta = Vector2.zero;
                var fi = fill.AddComponent<Image>(); fi.color = new Color32(0x1A, 0x11, 0x0D, 255); fi.raycastTarget = false;
            }
            var safe = Go("SafeRoot", canvasGo.transform); Stretch(Rt(safe));
            var sa = safe.AddComponent<KitSafeArea>();
            if (fillRt != null) SetRef(sa, "bottomFill", fillRt);
            return safe;
        }

        /// <summary>Adds the top bar, bottom nav (with the given tab selected) and the hidden profile card, then the wiring script.</summary>
        private static KitBarsDemo AddBars(GameObject canvasGo, int selectedTab)
        {
            var top = InstChild("KitTopBar", canvasGo.transform, "KitTopBar");
            var tr = Rt(top); tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(0.5f, 1); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(0, 270);
            var nav = InstChild("KitNavBar", canvasGo.transform, "KitNavBar");
            var nr = Rt(nav); nr.anchorMin = new Vector2(0, 0); nr.anchorMax = new Vector2(1, 0); nr.pivot = new Vector2(0.5f, 0); nr.anchoredPosition = Vector2.zero; nr.sizeDelta = new Vector2(0, 330);
            var navComp = nav.GetComponent<KitNavBar>();
            var so = new SerializedObject(navComp); so.FindProperty("selected").intValue = selectedTab; so.ApplyModifiedPropertiesWithoutUndo();
            navComp.SnapToSelected();
            var card = InstChild("KitProfileCard", canvasGo.transform, "KitProfileCard"); Stretch(Rt(card)); card.SetActive(false);
            var demo = canvasGo.AddComponent<KitBarsDemo>();
            SetRef(demo, "topBar", top.GetComponent<KitTopBar>()); SetRef(demo, "navBar", navComp); SetRef(demo, "card", card.GetComponent<KitProfileCard>());
            return demo;
        }

        /// <summary>Header for pages that hide the player bar and the bottom nav: back button, title, subtitle, optional coin ticket.</summary>
        private static KitScreenHeader AddScreenHeader(GameObject canvasGo, KitTheme th, string title, string sub, bool wallet)
        {
            var hdr = Go("ScreenHeader", canvasGo.transform); var hr = Rt(hdr);
            hr.anchorMin = new Vector2(0, 1); hr.anchorMax = new Vector2(1, 1); hr.pivot = new Vector2(0.5f, 1); hr.anchoredPosition = Vector2.zero; hr.sizeDelta = new Vector2(0, 150);
            var backGo = Go("Back", hdr.transform); Place(Rt(backGo), new Vector2(1, 0.5f), new Vector2(-72, -4), new Vector2(92, 92));
            var backImg = Img(backGo, th.leatherDark, true, 1.4f, true);
            var backBtn = backGo.AddComponent<Button>(); backBtn.targetGraphic = backImg; backBtn.transition = Selectable.Transition.None;
            var fx = backGo.AddComponent<KitButtonFx>(); SetRef(fx, "face", Rt(backGo));
            var arrow = Go("Arrow", backGo.transform); Place(Rt(arrow), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34)); arrow.transform.localRotation = Quaternion.Euler(0, 0, -90f);
            var ai = Img(arrow, th.triUp); ai.color = th.leatherText; ai.raycastTarget = false;
            var t = Txt(hdr.transform, "Title", title, th.fontHeading, 64, th.ivory, TextAlignmentOptions.MidlineRight); Place(Rt(t.gameObject), new Vector2(1, 0.5f), new Vector2(-450, 18), new Vector2(600, 76));
            var s = Txt(hdr.transform, "Sub", sub, th.fontBody, 28, th.brass, TextAlignmentOptions.MidlineRight); Place(Rt(s.gameObject), new Vector2(1, 0.5f), new Vector2(-450, -34), new Vector2(600, 40));
            var comp = hdr.AddComponent<KitScreenHeader>(); SetRef(comp, "backButton", backBtn);
            if (wallet)
            {
                var ticket = Go("Ticket", hdr.transform); Place(Rt(ticket), new Vector2(0, 0.5f), new Vector2(190, -2), new Vector2(300, 118));
                ticket.transform.localRotation = Quaternion.Euler(0, 0, -2f);
                Img(ticket, th.paperTicket, true, 1.6f);
                var coin = Go("Coin", ticket.transform); Place(Rt(coin), new Vector2(1, 0.5f), new Vector2(-52, 0), new Vector2(60, 60)); Img(coin, th.coin);
                var val = Txt(ticket.transform, "Value", "١٬٢٥٠", th.fontHeading, 50, th.paperInk, TextAlignmentOptions.Center); Stretch(Rt(val.gameObject), 22, 8, 96, 14);
                var pill = ticket.AddComponent<CoinPill>(); SetRef(pill, "label", val.GetComponent<ArabicText>()); SetRef(pill, "coinIcon", Rt(coin));
                SetRef(comp, "coins", pill);
            }
            return comp;
        }

        private static void SaveScene(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
        }
    }
}
