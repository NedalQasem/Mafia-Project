using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Renders the table with the little role cards beside the seats (faces for the roles a player may know, backs for the rest) at a tall, a short and a wide screen. The match scene must be open.</summary>
    public static class MatchCardsPreview
    {
        private static string Shot(KitMatchView v, string frame, List<SeatSpec> seats, string path, int w, int h)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var old = cam.targetTexture; cam.targetTexture = rt;
            cam.Render();
            Canvas.ForceUpdateCanvases();
            v.Show(frame);
            var stage = v.Frame(frame).Find("f_stage");
            for (int i = stage.childCount - 1; i >= 0; i--) { var c = stage.GetChild(i); if (c.name.StartsWith("f_seat") || c.name == "Seat") Object.DestroyImmediate(c.gameObject); }      // Destroy waits for a frame, which edit mode never gives
            v.SetSeats(frame, seats, null);
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
            SeatSpec S(string name, string role, bool me = false, SeatLook look = SeatLook.Normal, SeatTag tag = SeatTag.None) =>
                new SeatSpec { Id = name, Name = name, Me = me, RoleId = role, Look = look, Tag = tag };
            string log = "";
            var six = new List<SeatSpec> { S("أنت", "mafia", true), S("سامر", "mafia", false, SeatLook.Normal, SeatTag.Mafia), S("ريم", null), S("لينا", null), S("خالد", "doctor", false, SeatLook.Dead), S("مازن", null) };
            var ten = new List<SeatSpec> { S("أنت", "sheikh", true), S("سامر", null), S("ريم", null), S("لينا", "civilian", false, SeatLook.Dead), S("خالد", null), S("مازن", null), S("هديل", "silencer", false, SeatLook.Dead), S("نور", null), S("ليث", null), S("سلمى", "avenger", false, SeatLook.Dead) };
            foreach (var size in new[] { new Vector2Int(540, 1200), new Vector2Int(540, 760), new Vector2Int(800, 1200) })
            {
                string tag = size.x + "x" + size.y;
                log += Shot(v, "p4", six, System.IO.Path.Combine(folder, "cards-six-" + tag + ".png"), size.x, size.y);
                log += Shot(v, "p4", ten, System.IO.Path.Combine(folder, "cards-ten-" + tag + ".png"), size.x, size.y);
            }
            return log;
        }

        private static string Snap(string path, int w, int h)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var old = cam.targetTexture; cam.targetTexture = rt;
            cam.Render(); Canvas.ForceUpdateCanvases(); cam.Render(); Canvas.ForceUpdateCanvases(); cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = null; cam.targetTexture = old;
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release();
            return path + "\n";
        }

        /// <summary>The full-screen role card (the reveal), the big card of a player, and the narrator's slip over the table with the dock.</summary>
        public static string RenderScreens(string folder)
        {
            var v = Object.FindFirstObjectByType<KitMatchView>();
            if (v == null) return "open the match scene first";
            string log = "";
            foreach (var size in new[] { new Vector2Int(540, 1200), new Vector2Int(540, 760) })
            {
                string tag = size.x + "x" + size.y;
                var cam = Camera.main; var rt0 = new RenderTexture(size.x, size.y, 24); cam.targetTexture = rt0; cam.Render(); Canvas.ForceUpdateCanvases(); cam.targetTexture = null; rt0.Release();
                foreach (var id in new[] { "sheikh", "mafia" })
                {
                    KitRoleReveal.Show(v.transform, MafiaNight.Data.Cards.RoleCards.Find(id), new MafiaNight.Data.Cards.CardLayers(), id == "mafia" ? "رفقاؤك بالمافيا: سامر، ريم" : "كبير الحارة · الجولة الأولى · ٦ لاعبين", "فهمت، ابدأ", () => { });
                    log += Snap(System.IO.Path.Combine(folder, "reveal-" + id + "-" + tag + ".png"), size.x, size.y);
                    var r = v.transform.Find("RoleReveal"); if (r != null) Object.DestroyImmediate(r.gameObject);
                }
                KitCardViewer.Show(v.transform, MafiaNight.Data.Cards.RoleCards.Find("doctor"), new MafiaNight.Data.Cards.CardLayers(), "ريم");
                log += Snap(System.IO.Path.Combine(folder, "viewer-" + tag + ".png"), size.x, size.y);
                var cv = v.transform.Find("CardViewer"); if (cv != null) Object.DestroyImmediate(cv.gameObject);
            }
            return log;
        }

        private static void Step(int n, params System.Collections.IEnumerator[] routines)
        {
            for (int i = 0; i < n; i++)
            {
                foreach (var r in routines) if (r != null) r.MoveNext();
                Canvas.ForceUpdateCanvases();
            }
        }

        /// <summary>The table's moments, stepped by hand (edit mode has no frames): the close-up with the shot's mark, the doctor's shield, and the "out" stamp.</summary>
        public static string RenderMoments(string folder)
        {
            var v = Object.FindFirstObjectByType<KitMatchView>();
            if (v == null) return "open the match scene first";
            string log = "";
            KitMomentFx.StepOverride = 1f / 30f;
            try
            {
                foreach (var size in new[] { new Vector2Int(540, 1200), new Vector2Int(540, 760) })
                {
                    string tag = size.x + "x" + size.y;
                    var cam = Camera.main; var rt0 = new RenderTexture(size.x, size.y, 24); cam.targetTexture = rt0; cam.Render(); Canvas.ForceUpdateCanvases(); cam.targetTexture = null; rt0.Release();
                    foreach (var kind in new[] { "shot", "shield", "stamp_out", "card_flip" })
                    {
                        var seats = new List<SeatSpec>
                        {
                            new SeatSpec { Id = "me", Name = "أنت", Me = true, RoleId = "civilian" }, new SeatSpec { Id = "a", Name = "سامر" }, new SeatSpec { Id = "b", Name = "ريم" },
                            new SeatSpec { Id = "c", Name = "لينا" }, new SeatSpec { Id = "d", Name = "خالد" }, new SeatSpec { Id = "e", Name = "مازن" },
                        };
                        v.Show("p4");
                        var stage = v.Frame("p4").Find("f_stage");
                        for (int i = stage.childCount - 1; i >= 0; i--) { var c = stage.GetChild(i); if (c.name.StartsWith("f_seat") || c.name == "Seat") Object.DestroyImmediate(c.gameObject); }
                        v.SetSeats("p4", seats, null);
                        foreach (var g in v.GetComponentsInChildren<KitFitGroup>(true)) g.Apply();
                        Canvas.ForceUpdateCanvases();
                        var fxGo = KitMomentFx.Create(v, (RectTransform)v.transform);
                        try
                        {
                            var zoom = fxGo.Routine("zoom_seat", "b", 9f);
                            Step(16, zoom);                                                       // the camera is close
                            fxGo.NameOf = id => "ريم"; var effect = fxGo.Routine(kind, "b", 9f, "doctor");
                            Step(kind == "shot" ? 9 : kind == "shield" ? 26 : kind == "card_flip" ? 14 : 14, zoom, effect);
                            log += Snap(System.IO.Path.Combine(folder, "moment-" + kind + "-" + tag + ".png"), size.x, size.y);
                        }
                        finally { Object.DestroyImmediate(fxGo.gameObject); }
                    }
                }
            }
            finally { KitMomentFx.StepOverride = 0f; }
            return log;
        }

        [MenuItem("MafiaNight/Match/Render the table cards to the project folder")]
        public static void Menu() { Debug.Log(RenderAll(System.IO.Path.GetFullPath("Library"))); }
    }
}
