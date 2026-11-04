using System.Collections.Generic;
using MafiaNight.Data.Cards;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Creates the portrait asset of the role cards and renders a contact sheet of the cards (fronts, backs, hidden faces, a few layer combinations) to a PNG, to compare with Docs/mockups/role-cards.html.</summary>
    public static class RoleCardPreview
    {
        private const string ArtPath = "Assets/_Project/Resources/Kit/RoleCardArt.asset";
        private static readonly string[] Ids = { "mafia", "sheikh", "doctor", "civilian", "silencer", "sniper", "avenger" };

        [MenuItem("MafiaNight/Cards/Create or refresh the portrait asset")]
        public static RoleCardArt EnsureArt()
        {
            var art = AssetDatabase.LoadAssetAtPath<RoleCardArt>(ArtPath);
            if (art == null) { art = ScriptableObject.CreateInstance<RoleCardArt>(); AssetDatabase.CreateAsset(art, ArtPath); }
            var list = new List<RoleCardArt.Entry>();
            foreach (var id in Ids) list.Add(new RoleCardArt.Entry { id = id, portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Cards/Portraits/portrait_" + id + ".jpg") });
            art.portraits = list.ToArray();
            EditorUtility.SetDirty(art); AssetDatabase.SaveAssets();
            return art;
        }

        /// <summary>Renders the sheet to <paramref name="pngPath"/> (1080x1500). Returns a short report.</summary>
        public static string Render(string pngPath, CardLayers special = null)
        {
            EnsureArt();
            var camGo = new GameObject("tmpCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(0x0B, 0x07, 0x05, 255); cam.orthographic = true;
            var rt = new RenderTexture(1080, 1500, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            var cgo = new GameObject("tmpCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1080, 1500); sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 0f;
            try
            {
                float w = 240f, gap = 30f, x0 = 40f, rowH = 395f;
                var layers = new CardLayers();
                for (int i = 0; i < 4; i++)
                {
                    var def = RoleCards.All[i];
                    Put(KitRoleCard.Create(cgo.transform, def, layers, w), x0 + i * (w + gap), 40f);
                    Put(KitRoleCard.Create(cgo.transform, def, layers, w, KitRoleCard.Face.Back), x0 + i * (w + gap), 40f + rowH);
                }
                Put(KitRoleCard.CreateHidden(cgo.transform, layers, w), x0, 40f + 2 * rowH);
                Put(KitRoleCard.Create(cgo.transform, RoleCards.All[0], special ?? new CardLayers { Frame = "gold", Background = "fire", Motion = "none", Back = "skull" }, w), x0 + 1 * (w + gap), 40f + 2 * rowH);
                Put(KitRoleCard.CreateHidden(cgo.transform, new CardLayers { Frame = "black", Background = "carbon", Back = "skull" }, w), x0 + 2 * (w + gap), 40f + 2 * rowH);
                Put(KitRoleCard.Create(cgo.transform, RoleCards.All[2], new CardLayers { Frame = "silver", Background = "night", Motion = "none", Back = "rose" }, w), x0 + 3 * (w + gap), 40f + 2 * rowH);
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1080, 1500, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1080, 1500), 0, 0); tex.Apply();
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return "rendered " + pngPath + " (cached sprites " + KitProc.CachedCount + ", " + KitProc.CachedBytes / 1024 + " KB)";
            }
            finally { Object.DestroyImmediate(cgo); Object.DestroyImmediate(camGo); rt.Release(); }
        }

        private static void Put(KitRoleCard c, float x, float y)
        {
            var r = (RectTransform)c.transform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>Renders the five card backs in two sets of layers (1080x1000).</summary>
        public static string RenderBacks(string pngPath)
        {
            var camGo = new GameObject("tmpCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(0x0B, 0x07, 0x05, 255); cam.orthographic = true;
            var rt = new RenderTexture(1080, 1000, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            var cgo = new GameObject("tmpCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1080, 1000); sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 0f;
            try
            {
                var ids = KitEmblem.Ids; float w = 190f, gap = 17f, x0 = 20f;
                for (int i = 0; i < ids.Length; i++)
                {
                    Put(KitRoleCard.CreateHidden(cgo.transform, new CardLayers { Frame = "brass", Background = "leather", Back = ids[i] }, w), x0 + i * (w + gap), 30f);
                    Put(KitRoleCard.CreateHidden(cgo.transform, new CardLayers { Frame = i % 2 == 0 ? "gold" : "silver", Background = i % 2 == 0 ? "fire" : "night", Back = ids[i] }, w), x0 + i * (w + gap), 30f + 300f);
                    Put(KitRoleCard.CreateHidden(cgo.transform, new CardLayers { Frame = "black", Background = "carbon", Back = ids[i] }, w), x0 + i * (w + gap), 30f + 600f);
                }
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1080, 1000, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1080, 1000), 0, 0); tex.Apply();
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return "rendered " + pngPath;
            }
            finally { Object.DestroyImmediate(cgo); Object.DestroyImmediate(camGo); rt.Release(); }
        }

        /// <summary>Renders one hidden card, big (1080x860 holds two side by side), to see an emblem's detail.</summary>
        public static string RenderBig(string pngPath, string backA, string backB, string frame = "brass", string bg = "leather")
        {
            var camGo = new GameObject("tmpCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(0x0B, 0x07, 0x05, 255); cam.orthographic = true;
            var rt = new RenderTexture(1080, 860, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            var cgo = new GameObject("tmpCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1080, 860); sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 0f;
            try
            {
                float w = 500f;
                Put(KitRoleCard.CreateHidden(cgo.transform, new CardLayers { Frame = frame, Background = bg, Back = backA }, w), 20f, 40f);
                Put(KitRoleCard.CreateHidden(cgo.transform, new CardLayers { Frame = frame, Background = bg, Back = backB }, w), 560f, 40f);
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1080, 860, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1080, 860), 0, 0); tex.Apply();
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return "rendered " + pngPath;
            }
            finally { Object.DestroyImmediate(cgo); Object.DestroyImmediate(camGo); rt.Release(); }
        }

        [MenuItem("MafiaNight/Cards/Render the card backs to the project folder")]
        public static void RenderBacksMenu() { Debug.Log(RenderBacks(System.IO.Path.GetFullPath("Library/role-cards-backs.png"))); }

        [MenuItem("MafiaNight/Cards/Render the card sheet to the project folder")]
        public static void RenderMenu() { Debug.Log(Render(System.IO.Path.GetFullPath("Library/role-cards-sheet.png"))); }
    }
}
