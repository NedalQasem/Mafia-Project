using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// Builds the match screens (Docs/mockups/match.html) in Unity from what the browser computed: sprites rendered from the real CSS
    /// (Art/UI/Match/*.png, manifest.json) and the layout of every frame (frames.json). Tools/ui-gen/serve.js + export-match.js +
    /// extract-match.js produce those files. Menu: Mafia Night / UI Kit / Build Match Screens.
    /// </summary>
    public static partial class KitBuilder
    {
        private const string MatchArt = "Assets/_Project/Art/UI/Match/";
        private const string MatchScenePath = "Assets/_Project/Scenes/UIKitMatch.unity";
        private const float PhoneW = 390f, PhoneH = 790f;
        private static readonly float MatchK = 1080f / PhoneW;   // canvas units per CSS pixel

        [Serializable] private class MSprite { public float w, h, pad, scale, slice; }
        [Serializable] private class MArc { public float p; public string c; }
        [Serializable]
        private class MNode
        {
            public int i, p; public string name, kind; public float x, y, w, h, rot, op, z;
            public string sprite, tint; public bool stretch, bottom, dashed; public MArc arc;
            public string text, color, align, dir, font; public float fs, fw, lh; public bool wrap;
        }
        [Serializable] private class MFrame { public string name; public float w, h; public List<MNode> nodes; }
        [Serializable] private class MFrames { public List<MFrame> frames; }

        // ---------------------------------------------------------------- sprite import
        private static Dictionary<string, MSprite> ReadMatchManifest()
        {
            var d = new Dictionary<string, MSprite>();
            string path = MatchArt + "manifest.json";
            if (!File.Exists(path)) return d;
            // the manifest is { "name": {...}, ... }: split it by hand because JsonUtility has no dictionaries
            var text = File.ReadAllText(path);
            int depth = 0, start = -1; string key = null;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && depth == 1) { int e = text.IndexOf('"', i + 1); key = text.Substring(i + 1, e - i - 1); i = e; }
                else if (c == '{') { depth++; if (depth == 2) start = i; }
                else if (c == '}') { if (depth == 2 && key != null) d[key] = JsonUtility.FromJson<MSprite>(text.Substring(start, i - start + 1)); depth--; }
            }
            return d;
        }

        private static void ImportMatchSprites(Dictionary<string, MSprite> manifest)
        {
            foreach (var kv in manifest)
            {
                string path = MatchArt + kv.Key + ".png";
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) { AssetDatabase.ImportAsset(path); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
                if (ti == null) continue;
                var m = kv.Value;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = 4096;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.spritePixelsPerUnit = 100f * m.scale / MatchK;   // keeps 9-slice borders at the same size as in the HTML
                float b = (m.pad + m.slice) * m.scale;
                ti.spriteBorder = m.slice > 0 ? new Vector4(b, b, b, b) : Vector4.zero;
                ti.SaveAndReimport();
            }
        }

        private const float TextImgPad = 12f, TextImgScale = 4f;   // from export-frames.js exportTextSprites

        /// <summary>Lines drawn from the web font (Aref Ruqaa) as white PNGs; Unity tints them.</summary>
        private static void ImportTextSprites(List<MFrame> frames)
        {
            var done = new HashSet<string>();
            foreach (var f in frames) foreach (var n in f.nodes)
            {
                if (n.kind != "textimg" || !done.Add(n.sprite)) continue;
                string path = MatchArt + n.sprite + ".png";
                if (!File.Exists(path)) { Debug.LogWarning("[UIKit] missing text image " + path + " (run exportTextSprites)"); continue; }
                AssetDatabase.ImportAsset(path);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter; if (ti == null) continue;
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true; ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.spritePixelsPerUnit = 100f * TextImgScale / MatchK;
                ti.SaveAndReimport();
            }
        }

        // ---------------------------------------------------------------- build
        [MenuItem("Mafia Night/UI Kit/Build Match Screens")]
        public static void BuildMatchScreens()
        {
            var manifest = ReadMatchManifest();
            if (manifest.Count == 0 || !File.Exists(MatchArt + "frames.json")) { Debug.LogError("[UIKit] run export-match.js and extract-match.js first (Tools/ui-gen/serve.js)"); return; }
            ImportMatchSprites(manifest);
            AssetDatabase.Refresh();
            var th = BuildTheme();
            var frames = JsonUtility.FromJson<MFrames>(File.ReadAllText(MatchArt + "frames.json")).frames;
            ImportTextSprites(frames);

            var canvasGo = NewAppCanvas(th, new Color(0.04f, 0.03f, 0.03f));
            var roots = new GameObject[frames.Count]; var names = new string[frames.Count];
            for (int f = 0; f < frames.Count; f++)
            {
                roots[f] = BuildMatchFrame(frames[f], canvasGo.transform, th, manifest);
                names[f] = frames[f].name;
                roots[f].SetActive(f == 0);
            }
            var prev = canvasGo.AddComponent<KitMatchPreview>();
            var so = new SerializedObject(prev);
            var fr = so.FindProperty("frames"); fr.arraySize = roots.Length;
            var nm = so.FindProperty("names"); nm.arraySize = names.Length;
            for (int i = 0; i < roots.Length; i++) { fr.GetArrayElementAtIndex(i).objectReferenceValue = roots[i]; nm.GetArrayElementAtIndex(i).stringValue = names[i]; }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), MatchScenePath);
            Debug.Log("[UIKit] built " + frames.Count + " match frames");
        }

        private static Color ParseColor(string hex, Color fallback)
        {
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        }

        private static GameObject BuildMatchFrame(MFrame frame, Transform parent, KitTheme th, Dictionary<string, MSprite> manifest)
        {
            float k = MatchK;
            var objs = new GameObject[frame.nodes.Count];
            for (int n = 0; n < frame.nodes.Count; n++)
            {
                var nd = frame.nodes[n];
                var go = new GameObject("f_" + nd.name.Replace("div.", "").Replace("span.", ""), typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                objs[n] = go;
                if (nd.p < 0)
                {
                    go.name = "Frame_" + frame.name;
                    go.transform.SetParent(parent, false);
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                }
                else
                {
                    var pn = frame.nodes[nd.p];
                    go.transform.SetParent(objs[nd.p].transform, false);
                    if (nd.p == 0 && nd.stretch)
                    {   // full-screen layers (dim, backgrounds) follow the real screen size
                        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                    }
                    else if (nd.p == 0 && nd.bottom)
                    {   // the bottom panel sticks to the bottom edge
                        float fromBottom = PhoneH - (nd.y + nd.h);
                        rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.sizeDelta = new Vector2(nd.w * k, nd.h * k);
                        rt.anchoredPosition = new Vector2((nd.x + nd.w / 2f) * k, (fromBottom + nd.h / 2f) * k);
                    }
                    else
                    {
                        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.sizeDelta = new Vector2(nd.w * k, nd.h * k);
                        rt.anchoredPosition = new Vector2((nd.x + nd.w / 2f) * k, -(nd.y + nd.h / 2f) * k);
                    }
                    if (Mathf.Abs(nd.rot) > 0.01f) rt.localRotation = Quaternion.Euler(0, 0, -nd.rot);
                    if (nd.op < 0.999f) go.AddComponent<CanvasGroup>().alpha = Mathf.Pow(nd.op, 2.2f);   // Linear color space blends alpha in linear light
                }

                if (nd.kind == "textimg") MakeMatchTextImage(go, nd);
                else if (nd.kind == "text") MakeMatchText(go, nd, th);
                else if (nd.kind == "line") MakeMatchLine(go, nd);
                else MakeMatchVisual(go, nd, th, manifest);
            }
            return objs[0];
        }

        private static void MakeMatchVisual(GameObject go, MNode nd, KitTheme th, Dictionary<string, MSprite> manifest)
        {
            if (nd.kind == "icon")
            {
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>(MatchArt + nd.sprite + ".png");
                if (spr == null) { Debug.LogWarning("[UIKit] missing match sprite " + nd.sprite); return; }
                var im = go.AddComponent<Image>(); im.sprite = spr; im.raycastTarget = false; im.preserveAspect = false;
                var m = manifest.TryGetValue(nd.sprite, out var mm) ? mm : null;
                if (m != null && m.pad > 0)
                {   // icons carry a little padding; grow the rect so the glyph keeps its size
                    var rt = (RectTransform)go.transform; float grow = m.pad * 2f * MatchK;
                    rt.sizeDelta += new Vector2(grow, grow);
                }
                im.color = ParseColor(nd.tint, Color.white);
                return;
            }
            if (!string.IsNullOrEmpty(nd.sprite) || !string.IsNullOrEmpty(nd.tint) && nd.stretch)
            {
                var v = Go("V", go.transform); var vr = Rt(v);
                var img = v.AddComponent<Image>(); img.raycastTarget = false;
                MSprite m = null;
                if (!string.IsNullOrEmpty(nd.sprite))
                {
                    img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MatchArt + nd.sprite + ".png");
                    manifest.TryGetValue(nd.sprite, out m);
                    if (img.sprite == null) Debug.LogWarning("[UIKit] missing match sprite " + nd.sprite);
                }
                img.type = m != null && m.slice > 0 ? Image.Type.Sliced : Image.Type.Simple;
                img.preserveAspect = false;
                img.color = ParseColor(nd.tint, Color.white);
                if (string.IsNullOrEmpty(nd.sprite)) { var fc = img.color; fc.a = 1f - Mathf.Pow(1f - fc.a, 2.2f); img.color = fc; }   // flat dark overlays: same linear-light correction
                float pad = m != null ? m.pad * MatchK : 0f;
                vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
                vr.offsetMin = new Vector2(-pad, -pad); vr.offsetMax = new Vector2(pad, pad);
                vr.SetAsFirstSibling();
                if (nd.arc != null && nd.arc.p > 0f)
                {
                    var a = Go("Arc", go.transform); var ar = Rt(a);
                    var ai = a.AddComponent<Image>(); ai.raycastTarget = false;
                    ai.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MatchArt + "ring_arc.png");
                    ai.type = Image.Type.Filled; ai.fillMethod = Image.FillMethod.Radial360; ai.fillOrigin = (int)Image.Origin360.Top; ai.fillClockwise = true;
                    ai.fillAmount = nd.arc.p; ai.color = ParseColor(nd.arc.c, Color.white);
                    var am = manifest["ring_arc"]; float ap = am.pad * MatchK;
                    ar.anchorMin = Vector2.zero; ar.anchorMax = Vector2.one; ar.offsetMin = new Vector2(-ap, -ap); ar.offsetMax = new Vector2(ap, ap);
                    ar.SetSiblingIndex(1);
                }
            }
        }

        /// <summary>White 6px dash + 6px gap (CSS px at 6 texels per px), tiled for dashed rules.</summary>
        private static Sprite EnsureDashSprite()
        {
            string path = MatchArt + "dash_tile.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(72, 12, TextureFormat.RGBA32, false);
                for (int y = 0; y < 12; y++) for (int x = 0; x < 72; x++) tex.SetPixel(x, y, x < 36 ? Color.white : new Color(1, 1, 1, 0));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true; ti.filterMode = FilterMode.Bilinear; ti.wrapMode = TextureWrapMode.Repeat;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.spritePixelsPerUnit = 72f * 100f / (12f * MatchK);
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void MakeMatchTextImage(GameObject go, MNode nd)
        {
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>(MatchArt + nd.sprite + ".png");
            if (spr == null) return;
            var v = Go("V", go.transform); var vr = Rt(v);
            var im = v.AddComponent<Image>(); im.sprite = spr; im.raycastTarget = false; im.preserveAspect = false;
            im.color = ParseColor(nd.tint, Color.white);
            float pad = TextImgPad * MatchK;
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = new Vector2(-pad, -pad); vr.offsetMax = new Vector2(pad, pad);
        }

        private static void MakeMatchLine(GameObject go, MNode nd)
        {
            var im = go.AddComponent<Image>(); im.raycastTarget = false;
            im.color = ParseColor(nd.tint, Color.white);
            if (nd.dashed) { im.sprite = EnsureDashSprite(); im.type = Image.Type.Tiled; }
        }

        /// <summary>A copy of the font material with a thicker face (Cairo is one variable font, so the heavy weights are faked with the SDF dilate).</summary>
        private static Material EnsureBoldMaterial(TMP_FontAsset font)
        {
            string path = "Assets/_Project/Art/Fonts/" + font.name + " Bold.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                if (font.material == null) return null;
                mat = new Material(font.material) { name = font.name + " Bold" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.22f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void MakeMatchText(GameObject go, MNode nd, KitTheme th)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = nd.font == "head" ? (th.fontLalezar != null ? th.fontLalezar : th.fontHeading) : nd.font == "calli" ? (th.fontAref != null ? th.fontAref : th.fontCalligraphy) : th.fontBody;
            if (font != null) t.font = font;
            t.fontSize = nd.fs * MatchK;
            t.color = ParseColor(nd.color, Color.white);
            t.raycastTarget = false;
            // Lalezar and Aref Ruqaa are already heavy; the body font (Cairo) gets a thicker material for the 700/800 weights of the mockup
            t.fontStyle = FontStyles.Normal;
            if (nd.font == "body" && nd.fw >= 700 && font != null) { var bold = EnsureBoldMaterial(font); if (bold != null) t.fontSharedMaterial = bold; }
            t.enableWordWrapping = nd.wrap; t.overflowMode = TextOverflowModes.Overflow;
            bool rtl = nd.dir == "rtl";
            string al = nd.align;
            bool right = al == "right" || al == "justify" || (rtl && al == "start") || (!rtl && al == "end");
            bool left = al == "left" || (!rtl && al == "start") || (rtl && al == "end");
            t.alignment = left ? TextAlignmentOptions.MidlineLeft : right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.Midline;
            if (nd.wrap && nd.lh > 0) t.lineSpacing = 0f;
            // the browser's text box is the tightest one; give the Unity box some slack on the open side
            var rt = (RectTransform)go.transform;
            float slack = nd.wrap ? nd.w * 0.06f : Mathf.Max(8f, nd.w * 0.25f);
            float dx = slack * MatchK;
            var pos = rt.anchoredPosition; var size = rt.sizeDelta;
            if (left) { rt.sizeDelta = new Vector2(size.x + dx, size.y); rt.anchoredPosition = pos + new Vector2(dx / 2f, 0); }
            else if (right) { rt.sizeDelta = new Vector2(size.x + dx, size.y); rt.anchoredPosition = pos - new Vector2(dx / 2f, 0); }
            else rt.sizeDelta = new Vector2(size.x + dx, size.y);
            var a = go.AddComponent<ArabicText>(); a.ArabicDigits = false; a.Wrap = nd.wrap; a.Text = nd.text;   // digits stay as written in the mockup
        }
    }
}
