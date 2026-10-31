using System.Collections.Generic;
using MafiaNight.Core;
using MafiaNight.UI.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// English mode: the whole interface is the Arabic one drawn in a mirror, so every screen reads left to right without a second layout.
    /// Everything of a root canvas is put under one full-size "KitFlip" object that is scaled -1 on x (touch needs nothing, the buttons are where they
    /// are drawn), and every <see cref="ArabicText"/> is flipped back so its letters read normally and aligned the other way
    /// (see <see cref="ArabicText.SetMirrored"/>). What a script adds to the canvas later is moved under the flip object the same frame.
    /// In Arabic nothing here does anything.
    /// </summary>
    public sealed class KitLocale : MonoBehaviour
    {
        public const string FlipName = "KitFlip";

        private static KitLocale instance;
        private static readonly Dictionary<Canvas, RectTransform> mirrored = new Dictionary<Canvas, RectTransform>();
        private int frame;

        public static bool IsMirrored(Canvas c) => c != null && mirrored.ContainsKey(c);

        /// <summary>True when this object is drawn flipped (English mode, under a mirrored canvas).</summary>
        public static bool IsFlipped(Transform t)
        {
            if (!Loc.English || t == null) return false;
            var c = t.GetComponentInParent<Canvas>();
            return c != null && IsMirrored(c.rootCanvas);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("KitLocale"); DontDestroyOnLoad(go);
            instance = go.AddComponent<KitLocale>();
            SceneManager.sceneLoaded += (s, m) => instance.Sync();
            instance.Sync();
        }

        /// <summary>
        /// A picture of Arabic lettering (an Aref Ruqaa name drawn as a sprite) cannot be translated: in English a live text of the Arabic words' English line takes its
        /// place, in the picture's colour and box, and the picture is hidden. Returns that text (null in Arabic).
        /// </summary>
        public static ArabicText TextInPlaceOf(UnityEngine.UI.Image baked, string arabic, TMPro.TMP_FontAsset font, float size, float minWidth = 0f)
        {
            if (!Loc.English || baked == null) return null;
            baked.enabled = false;
            var existing = baked.transform.Find("En");
            ArabicText label;
            if (existing != null) label = existing.GetComponent<ArabicText>();
            else
            {
                var go = new GameObject("En", typeof(RectTransform));
                var r = (RectTransform)go.transform; r.SetParent(baked.transform, false);      // a child of the picture: whatever fades or moves the picture does the same to it
                var src = baked.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(Mathf.Max(src.rect.width, minWidth), Mathf.Max(src.rect.height, size * 1.4f));
                var tm = go.AddComponent<TMPro.TextMeshProUGUI>();
                tm.font = font; tm.fontStyle = TMPro.FontStyles.Bold; tm.fontSize = size; tm.color = baked.color; tm.raycastTarget = false; tm.alignment = TMPro.TextAlignmentOptions.Center;
                label = go.AddComponent<ArabicText>();
            }
            label.Text = arabic;
            return label;
        }

        /// <summary>A canvas the code has just made: mirrored at once (otherwise it would wait for the next check).</summary>
        public static void Prepare(Canvas c) { if (instance != null && c != null) instance.Convert(c.rootCanvas, Loc.English); }

        private void LateUpdate()
        {
            if (!Loc.English && mirrored.Count == 0) return;
            if (++frame % 6 == 0) Sync();
            // what scripts added to a mirrored canvas since the last frame goes under the flip object
            foreach (var kv in mirrored)
            {
                var c = kv.Key; var flip = kv.Value;
                if (c == null || flip == null) continue;
                var ct = c.transform;
                for (int i = ct.childCount - 1; i >= 0; i--)
                {
                    var child = ct.GetChild(i);
                    if (child != flip) Reparent(child, flip);
                }
            }
        }

        private void Sync()
        {
            bool en = Loc.English;
            if (!en && mirrored.Count == 0) return;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.isRootCanvas) Convert(c, en);
            foreach (var kv in mirrored) if (kv.Key != null) ReplaceMasks(kv.Key);
            var gone = new List<Canvas>(); foreach (var k in mirrored.Keys) if (k == null) gone.Add(k);
            foreach (var k in gone) mirrored.Remove(k);
        }

        /// <summary>
        /// A RectMask2D cuts everything away under a mirrored parent (it reads its corners the wrong way round), so it becomes a stencil Mask, which does not mind.
        /// A mask whose picture is invisible (clear) gets a white one that is not drawn.
        /// </summary>
        private static void ReplaceMasks(Canvas c)
        {
            foreach (var rm in c.GetComponentsInChildren<RectMask2D>(true))
            {
                var go = rm.gameObject;
                Object.DestroyImmediate(rm);
                var img = go.GetComponent<Image>();
                bool visible = img != null && img.color.a > 0.01f && img.sprite != null;
                if (img == null) { img = go.AddComponent<Image>(); img.raycastTarget = false; }
                if (!visible) img.color = Color.white;
                var mask = go.AddComponent<Mask>(); mask.showMaskGraphic = visible;
            }
        }

        private static Dictionary<string, KeyValuePair<float, string>> pictures;

        /// <summary>The mockup frames print some lines as pictures (Aref Ruqaa): a mirror would make them unreadable and they are Arabic, so a live English text takes their place.</summary>
        private static void ReplaceTextPictures(Canvas c)
        {
            if (pictures == null)
            {
                pictures = new Dictionary<string, KeyValuePair<float, string>>();
                var asset = Resources.Load<TextAsset>("Kit/textpics");
                if (asset != null)
                    foreach (var raw in asset.text.Split('\n'))
                    {
                        var p = raw.TrimEnd('\r').Split('\t');
                        if (p.Length < 3 || p[0].StartsWith("#")) continue;
                        float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fs);
                        pictures[p[0]] = new KeyValuePair<float, string>(fs, p[2]);
                    }
            }
            if (pictures.Count == 0) return;
            var th = KitTheme.Current;
            foreach (var img in c.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite == null || !pictures.TryGetValue(img.sprite.name, out var info)) continue;
                var font = info.Key >= 36f ? (th != null && th.fontLalezar != null ? th.fontLalezar : th != null ? th.fontHeading : null) : (th != null ? th.fontBody : null);
                TextInPlaceOf(img, info.Value, font, info.Key * 1080f / 390f, 0f);
            }
        }

        private static void Reparent(Transform child, Transform flip)
        {
            // the child keeps its place in the order of its siblings: it goes to the end, like everything a script adds
            child.SetParent(flip, false);
        }

        private void Convert(Canvas c, bool en)
        {
            if (c == null) return;
            if (en && !mirrored.ContainsKey(c))
            {
                if (c.renderMode == RenderMode.WorldSpace || c.GetComponent<BurnTransition>() != null) return;
                var go = new GameObject(FlipName, typeof(RectTransform));
                var flip = (RectTransform)go.transform; flip.SetParent(c.transform, false);
                flip.anchorMin = Vector2.zero; flip.anchorMax = Vector2.one; flip.pivot = new Vector2(0.5f, 0.5f); flip.offsetMin = flip.offsetMax = Vector2.zero;
                flip.localRotation = Quaternion.Euler(0f, 180f, 0f);      // turned half way round the vertical axis: a mirror that masks and touch handle better than a negative scale
                var kids = new List<Transform>(); foreach (Transform t in c.transform) if (t != flip) kids.Add(t);
                foreach (var k in kids) k.SetParent(flip, false);
                mirrored[c] = flip;
                var gr = c.GetComponent<GraphicRaycaster>(); if (gr != null) gr.ignoreReversedGraphics = false;      // the turned picture faces away, but it is the one to touch
                foreach (var t in c.GetComponentsInChildren<ArabicText>(true)) t.SetMirrored(true);
                ReplaceMasks(c);
                ReplaceTextPictures(c);
            }
            else if (!en && mirrored.TryGetValue(c, out var flip))
            {
                if (flip != null)
                {
                    foreach (var t in c.GetComponentsInChildren<ArabicText>(true)) t.SetMirrored(false);
                    var kids = new List<Transform>(); foreach (Transform t in flip) kids.Add(t);
                    foreach (var k in kids) k.SetParent(c.transform, false);
                    Destroy(flip.gameObject);
                }
                mirrored.Remove(c);
            }
        }
    }
}
