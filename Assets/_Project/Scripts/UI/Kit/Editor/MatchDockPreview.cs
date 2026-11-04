using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Renders the table dock (chat lines, the box, the lock, the mic button and its plate) over the table frames at a tall, a short and a wide screen, to look at before a build. The match scene must be open.</summary>
    public static class MatchDockPreview
    {
        private static string Shot(KitMatchView v, KitMatchDock dock, string frame, KitMatchDock.Vm vm, string path, int w, int h)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var old = cam.targetTexture; cam.targetTexture = rt;
            cam.Render();                                      // a first render: the canvas takes the size of this picture before the dock reads it
            Canvas.ForceUpdateCanvases();
            v.Show(frame);
            foreach (var n in frame == "p4" ? new[] { 77, 82 } : frame == "p2_mafia" ? new[] { 63 } : frame == "p2_civ" ? new[] { 65 } : new int[0]) v.SetActive(frame, n, false);     // the old mic and chat parts the flow takes off
            dock.Render(vm);
            v.ReserveBottom(dock.CurrentHeight);
            foreach (var g in v.GetComponentsInChildren<KitFitGroup>(true)) g.Apply();
            Canvas.ForceUpdateCanvases();
            foreach (var g in v.GetComponentsInChildren<KitFitGroup>(true)) g.Apply();
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = null; cam.targetTexture = old;
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release();
            return path + "\n";
        }

        public static string RenderAll(string folder)
        {
            var v = Object.FindFirstObjectByType<KitMatchView>();
            if (v == null) return "open the match scene first";
            var dock = KitMatchDock.Create((RectTransform)v.transform);
            string log = "";
            try
            {
                var lines = new List<KitMatchDock.Line>
                {
                    new KitMatchDock.Line { Name = "ريم", Text = "كان ساكت طول الليلة" },
                    new KitMatchDock.Line { Name = "لينا", Text = "ليش؟ شو شفتي؟ كلامك ما بيمشي علينا يا ريم لأنك دايماً بتقولي هيك وما في دليل" },
                    new KitMatchDock.Line { Name = "أنت", Text = "خلينا نسمع منه", Mine = true },
                };
                var day = new KitMatchDock.Vm { Lines = lines, Tab = "الطاولة", Prompt = "اكتب رسالة…", MicLook = KitMatchDock.Mic.Open, MicReason = "مفتوح", MicTappable = true };
                var mafia = new KitMatchDock.Vm { Lines = lines, Tab = "المافيا · سري", Prompt = "اكتب للمافيا فقط…", Secret = true, MicLook = KitMatchDock.Mic.Open, MicReason = "مفتوح · للمافيا فقط", MicTappable = true };
                var night = new KitMatchDock.Vm { Locked = true, LockedText = "الدردشة مقفلة بالليل", MicLook = KitMatchDock.Mic.Closed, MicReason = "مغلق · الليل" };
                var tape = new KitMatchDock.Vm { Locked = true, Tape = true, LockedText = "انسكتّ اليوم: كاتم الصوت سكّتك لحد الفجر", MicLook = KitMatchDock.Mic.Closed, MicReason = "مُسكَت اليوم" };
                foreach (var size in new[] { new Vector2Int(540, 1200), new Vector2Int(540, 760), new Vector2Int(800, 1200) })
                {
                    string tag = size.x + "x" + size.y;
                    log += Shot(v, dock, "p4", day, System.IO.Path.Combine(folder, "dock-day-" + tag + ".png"), size.x, size.y);
                    log += Shot(v, dock, "p2_mafia", mafia, System.IO.Path.Combine(folder, "dock-mafia-" + tag + ".png"), size.x, size.y);
                    log += Shot(v, dock, "p2_civ", night, System.IO.Path.Combine(folder, "dock-night-" + tag + ".png"), size.x, size.y);
                    log += Shot(v, dock, "p4", tape, System.IO.Path.Combine(folder, "dock-tape-" + tag + ".png"), size.x, size.y);
                }
            }
            finally { Object.DestroyImmediate(dock.gameObject); v.ReserveBottom(0f); foreach (var g in v.GetComponentsInChildren<KitFitGroup>(true)) g.Apply(); }
            return log;
        }

        [MenuItem("MafiaNight/Match/Render the table dock to the project folder")]
        public static void Menu() { Debug.Log(RenderAll(System.IO.Path.GetFullPath("Library"))); }
    }
}
