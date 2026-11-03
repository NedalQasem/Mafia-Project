using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MafiaNight.Tests
{
    /// <summary>
    /// The performance rules of Docs/13-performance-rules.md as checks: the game must stay light however much art and however many screens are added.
    /// A new picture that is not compressed, a screen that grows past its budget, or texts that wait for clicks they cannot take fail here, not on a player's phone.
    /// The scene numbers are a ratchet: when a screen gets lighter, lower its number.
    /// </summary>
    public class PerformanceBudgetTests
    {
        private const string ArtRoot = "Assets/_Project/Art";
        private const long MaxSourcePixels = 3_000_000;
        private const int MaxAndroidTextureSize = 2048;

        // ---- pictures

        private static IEnumerable<string> ArtTextures() =>
            AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }).Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.Contains("/_Unused/"));

        [Test]
        public void Every_picture_is_stored_compressed_on_android()
        {
            var bad = new List<string>();
            foreach (var p in ArtTextures())
            {
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null || imp.textureType == TextureImporterType.NormalMap) continue;
                var a = imp.GetPlatformTextureSettings("Android");
                bool astc = a.overridden && (a.format == TextureImporterFormat.ASTC_4x4 || a.format == TextureImporterFormat.ASTC_6x6 || a.format == TextureImporterFormat.ASTC_5x5 || a.format == TextureImporterFormat.ASTC_8x8);
                if (!astc || a.maxTextureSize > MaxAndroidTextureSize) bad.Add(p + " (" + (a.overridden ? a.format.ToString() : "not set") + ", max " + a.maxTextureSize + ")");
            }
            Assert.IsEmpty(bad, bad.Count + " pictures are not ASTC / too big on Android (run MafiaNight > Textures > Apply the import rules to all art):\n" + string.Join("\n", bad.Take(12)));
        }

        [Test]
        public void No_picture_has_a_mip_chain_or_a_readable_copy()
        {
            var bad = ArtTextures().Where(p => { var i = AssetImporter.GetAtPath(p) as TextureImporter; return i != null && (i.mipmapEnabled || i.isReadable); }).ToList();
            Assert.IsEmpty(bad, "mipmaps or Read/Write on (a third / double the memory for nothing on a flat screen):\n" + string.Join("\n", bad.Take(12)));
        }

        [Test]
        public void No_picture_is_bigger_than_a_screen_needs()
        {
            var bad = new List<string>();
            foreach (var p in ArtTextures())
            {
                var imp = AssetImporter.GetAtPath(p) as TextureImporter; if (imp == null) continue;
                imp.GetSourceTextureWidthAndHeight(out int w, out int h);
                if ((long)w * h > MaxSourcePixels) bad.Add(p + " " + w + "x" + h);
            }
            Assert.IsEmpty(bad, "pictures over " + MaxSourcePixels + " pixels (the canvas is 1080 wide):\n" + string.Join("\n", bad));
        }

        // ---- screens

        // total objects, objects active at start, texts, and pictures/texts that take taps nobody listens to (per scene). Lower them when a screen gets lighter.
        private static readonly Dictionary<string, int[]> Budgets = new Dictionary<string, int[]>
        {
            //                       total  active texts  idleRaycasts
            { "Bootstrap",           new[] { 200,   150,   40,  30 } },
            { "UIKitLoading",        new[] { 150,   120,   30,  10 } },
            { "UIKitRooms",          new[] { 800,   400,  260,  15 } },
            { "UIKitStore",          new[] { 400,   300,  130,  10 } },
            { "UIKitSettings",       new[] { 500,   400,  150,  10 } },
            { "UIKitFamily",         new[] { 2600,  300,  700,  25 } },
            { "UIKitChat",           new[] { 200,   150,   60,  10 } },
            { "UIKitLeaderboards",   new[] { 450,   300,  160,  10 } },
            { "UIKitBag",            new[] { 150,   100,   40,  10 } },
            { "UIKitRank",           new[] { 300,   200,  110,  10 } },
            { "UIKitMatch",          new[] { 1800,  100,  450,  10 } },
        };

        [Test]
        public void Every_screen_stays_inside_its_budget()
        {
            var over = new List<string>();
            foreach (var kv in Budgets)
            {
                var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + kv.Key + ".unity", OpenSceneMode.Additive);
                try
                {
                    int total = 0, active = 0, texts = 0, idle = 0;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var t in root.GetComponentsInChildren<Transform>(true)) { total++; if (t.gameObject.activeInHierarchy) active++; }
                        foreach (var g in root.GetComponentsInChildren<Graphic>(true))
                        {
                            if (g is TMP_Text) texts++;
                            if (!g.raycastTarget) continue;
                            bool listens = g.GetComponentInParent<Selectable>(true) != null || g.GetComponentInParent<IPointerClickHandler>(true) != null
                                           || g.GetComponentInParent<IDragHandler>(true) != null || g.GetComponentInParent<IScrollHandler>(true) != null;
                            if (!listens) idle++;
                        }
                    }
                    var b = kv.Value;
                    if (total > b[0]) over.Add(kv.Key + ": " + total + " objects (budget " + b[0] + ")");
                    if (active > b[1]) over.Add(kv.Key + ": " + active + " active at start (budget " + b[1] + ")");
                    if (texts > b[2]) over.Add(kv.Key + ": " + texts + " texts (budget " + b[2] + ")");
                    if (idle > b[3]) over.Add(kv.Key + ": " + idle + " graphics take taps nobody listens to (budget " + b[3] + "): set raycastTarget off");
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            Assert.IsEmpty(over, "screens over budget:\n" + string.Join("\n", over));
        }
    }
}
