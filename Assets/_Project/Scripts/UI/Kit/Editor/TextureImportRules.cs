using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// One rule for every picture of the game, so the art can grow by any amount without the game getting heavier (Docs/13-performance-rules.md).
    /// Everything under Assets/_Project/Art is stored compressed on Android: ASTC 4x4 (one byte a pixel, a quarter of the uncompressed size, measured 54-58 dB against the original,
    /// i.e. not visible) and ASTC 6x6 for the full-screen backgrounds (about 2 MP and up, smooth art, 52 dB). Uncompressed pictures cost four times the memory, four times the load time
    /// and four times the bandwidth every frame they are drawn. Runs on every new or changed texture; <see cref="ApplyToAll"/> brings the existing ones in line.
    /// </summary>
    public sealed class TextureImportRules : AssetPostprocessor
    {
        public const string Root = "Assets/_Project/Art/";
        private const long BigPixels = 2_000_000;
        private const int MaxSize = 2048;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root) || assetPath.Contains("/_Unused/")) return;
            Apply((TextureImporter)assetImporter);
        }

        /// <summary>Sets the Android format of the picture by its size. Returns true when something changed.</summary>
        public static bool Apply(TextureImporter imp)
        {
            if (imp == null || imp.textureType == TextureImporterType.NormalMap || imp.textureType == TextureImporterType.Cursor || imp.textureType == TextureImporterType.SingleChannel) return false;
            imp.GetSourceTextureWidthAndHeight(out int w, out int h);
            var format = (long)w * h >= BigPixels ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4;
            bool changed = false;
            var android = imp.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.format != format || android.maxTextureSize != MaxSize || android.compressionQuality != 50)
            {
                android.overridden = true;
                android.format = format;
                android.maxTextureSize = MaxSize;
                android.compressionQuality = 50;
                android.textureCompression = TextureImporterCompression.Compressed;
                imp.SetPlatformTextureSettings(android);
                changed = true;
            }
            if (imp.mipmapEnabled) { imp.mipmapEnabled = false; changed = true; }       // flat pictures on a flat screen: a mip chain is a third more memory for nothing
            if (imp.isReadable) { imp.isReadable = false; changed = true; }              // a readable copy would keep the picture twice in memory
            return changed;
        }

        [MenuItem("MafiaNight/Textures/Apply the import rules to all art")]
        public static void ApplyToAll()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root.TrimEnd('/') });
            var paths = new Queue<string>();
            foreach (var g in guids) { string p = AssetDatabase.GUIDToAssetPath(g); if (!p.Contains("/_Unused/")) paths.Enqueue(p); }
            int total = paths.Count, changed = 0, done = 0;
            Debug.Log("[Textures] applying the import rules to " + total + " pictures…");
            // a few pictures per editor tick: the editor stays alive while the (slow) compression runs
            void Tick()
            {
                int batch = 0;
                AssetDatabase.StartAssetEditing();
                try
                {
                    while (paths.Count > 0 && batch < 12)
                    {
                        string p = paths.Dequeue(); batch++; done++;
                        var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                        if (imp != null && Apply(imp)) { changed++; imp.SaveAndReimport(); }
                    }
                }
                finally { AssetDatabase.StopAssetEditing(); }
                if (paths.Count == 0) { EditorApplication.update -= Tick; Debug.Log("[Textures] done: " + changed + " of " + total + " pictures changed"); }
            }
            EditorApplication.update += Tick;
        }
    }
}
