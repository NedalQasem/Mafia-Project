using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Renders the four role card shelves of the store (frames, backgrounds, motions, backs) as the store tiles, to compare with Docs/mockups/role-cards.html section 3.</summary>
    public static class StoreCardsPreview
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UIKit/StoreItem.prefab";

        public static string Render(string folder)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return "no StoreItem prefab";
            string log = "";
            foreach (var shelf in new[] { "card_frames", "card_bgs", "card_motions", "card_backs" })
            {
                var items = StoreData.Items(shelf);
                int rows = Mathf.CeilToInt(items.Count / 3f);
                int w = 1080, h = 120 + rows * 470;
                var camGo = new GameObject("tmpCam", typeof(Camera));
                var cam = camGo.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(0x14, 0x0E, 0x0A, 255); cam.orthographic = true;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;
                var cgo = new GameObject("tmpCanvas", typeof(Canvas), typeof(CanvasScaler));
                var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10;
                var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(w, h); sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                try
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cgo.transform);
                        var r = (RectTransform)go.transform;
                        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 1f);
                        r.anchoredPosition = new Vector2((i % 3 - 1) * 340f, -60f - (i / 3) * 470f);
                        var it = items[i];
                        it.owned = i == 0; it.equipped = i == 0;
                        go.GetComponent<StoreItemView>().Bind(it, 0);
                    }
                    Canvas.ForceUpdateCanvases(); cam.Render(); Canvas.ForceUpdateCanvases(); cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                    RenderTexture.active = null;
                    string path = System.IO.Path.Combine(folder, "store-" + shelf + ".png");
                    System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    log += path + "\n";
                }
                finally { cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(cgo); Object.DestroyImmediate(camGo); }
            }
            return log;
        }

        [MenuItem("MafiaNight/Store/Render the role card shelves to the project folder")]
        public static void Menu() { Debug.Log(Render(System.IO.Path.GetFullPath("Library"))); }
    }
}
