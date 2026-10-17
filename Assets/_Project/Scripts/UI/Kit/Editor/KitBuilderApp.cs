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
            return canvasGo;
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

        private static void SaveScene(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
        }
    }
}
