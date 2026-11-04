using System.Collections.Generic;
using MafiaNight.Data.Cards;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Renders the pick sheet (silencer, sniper, Avenger) and the full-screen role reveal to PNG files, at a tall and a short screen, to look at them before a build.</summary>
    public static class RoleSheetsPreview
    {
        private static string Render(string path, int w, int h, System.Action<Transform> build)
        {
            var camGo = new GameObject("tmpCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(0x24, 0x1B, 0x14, 255); cam.orthographic = true;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            var cgo = new GameObject("tmpCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1080, 2400); sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 0f;
            try
            {
                build(cgo.transform);
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return "rendered " + path;
            }
            finally { KitPickSheet.CloseCurrentImmediate(); Object.DestroyImmediate(cgo); Object.DestroyImmediate(camGo); rt.Release(); }
        }

        public static string RenderAll(string folder)
        {
            string log = "";
            var names = new List<KitPickSheet.Row>();
            string[] nm = { "أم سعيد", "البيك", "لينا", "سارة", "الحج حسن", "أبو فارس", "مازن", "ريم", "عادل", "نور", "سامر", "هديل", "خالد", "رنا", "اسم طويل جداً جداً جداً جداً جداً جداً" };
            for (int i = 0; i < nm.Length; i++) names.Add(new KitPickSheet.Row { Id = "p" + i, Name = nm[i] });
            foreach (var size in new[] { new Vector2Int(540, 1200), new Vector2Int(540, 760) })
            {
                string tag = size.y > 1000 ? "tall" : "short";
                log += Render(System.IO.Path.Combine(folder, "sheet-silence-" + tag + ".png"), size.x, size.y, t => KitPickSheet.Show(t, new KitPickSheet.Spec
                {
                    Title = "كاتم الصوت · مين تسكّت؟", Timer = "0:14", Selected = "p2", Rows = names,
                    Body = "المُسكَت ما بيحكي ولا بيكتب طول النهار، وبيقدر يصوّت بس. الكل بيعرف إنه انسكت، بس ما حدا بيعرف مين سكّته.",
                    Buttons = new List<KitPickSheet.Button2> { new KitPickSheet.Button2 { Label = "أسكِت لينا" } },
                })) + "\n";
                log += Render(System.IO.Path.Combine(folder, "sheet-sniper-" + tag + ".png"), size.x, size.y, t => KitPickSheet.Show(t, new KitPickSheet.Spec
                {
                    Title = "القنّاص · معك طلقة وحدة", Timer = "0:18", Rows = names,
                    Body = "إذا أصبت مافيا بيطلع وإنت بتضل. إذا أصبت مواطن بيطلعوا الاتنين. الطبيب ما بيقدر يوقفها وما حدا بيعرف مين قنص.",
                    Buttons = new List<KitPickSheet.Button2> { new KitPickSheet.Button2 { Label = "اختر هدفاً", Disabled = true }, new KitPickSheet.Button2 { Label = "مش هالليلة", Quiet = true } },
                })) + "\n";
                log += Render(System.IO.Path.Combine(folder, "reveal-" + tag + ".png"), size.x, size.y, t => KitRoleReveal.Show(t, RoleCards.Find("avenger"), new CardLayers(), "ما بيروح لحاله", "فهمت، ابدأ", () => { })) + "\n";
            }
            return log;
        }

        [MenuItem("MafiaNight/Cards/Render the role sheets to the project folder")]
        public static void Menu() { Debug.Log(RenderAll(System.IO.Path.GetFullPath("Library"))); }
    }
}
