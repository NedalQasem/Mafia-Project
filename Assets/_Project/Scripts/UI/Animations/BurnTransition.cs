using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Animations
{
    public enum BurnCorner { BottomRight, BottomLeft, TopRight, TopLeft }

    /// <summary>How the player wants screens to change: the burning page (default), a short fade, or nothing. PlayerPrefs "mafia.fx".</summary>
    public static class BurnSettings
    {
        public const string Key = "mafia.fx";
        public const string Fire = "fire", Simple = "simple", Off = "off";

        public static string Mode
        {
            get { var m = PlayerPrefs.GetString(Key, Fire); return m == Simple || m == Off ? m : Fire; }
            set { PlayerPrefs.SetString(Key, value == Simple || value == Off ? value : Fire); PlayerPrefs.Save(); }
        }

        /// <summary>The corner the NEXT change of screen starts from; it goes back to bottom right after it is used.</summary>
        public static BurnCorner NextCorner = BurnCorner.BottomRight;

        public static BurnCorner TakeCorner() { var c = NextCorner; NextCorner = BurnCorner.BottomRight; return c; }
    }

    /// <summary>
    /// The burning-page transition between screens (reference: Docs/mockups/burn.html, its Burn.frame / spawn / drawParts, constants copied from there).
    /// Use: <c>yield return BurnTransition.Capture(t =&gt; snap = t)</c> at the end of a frame, <c>var b = BurnTransition.Begin(snap)</c> (it covers the screen with
    /// the frozen old one and blocks touches), load the new screen under it, then <c>yield return b.Play(corner, seconds)</c>; the overlay removes itself.
    /// The old screen burns on a fire front (UIBurn shader); sparks and smoke are small UI images pooled here.
    /// </summary>
    public sealed class BurnTransition : MonoBehaviour
    {
        private const int MW = 195, MH = 395;       // the grid of the mockup (noise texels)
        private const float Band = 0.17f;
        private const int MaxSparks = 140, MaxSmoke = 36;

        private static Texture2D noiseTex;
        private static float[] noise;
        private static Sprite softDot;
        private static Material baseMaterial;

        private RawImage view;
        private Texture2D snapshot;
        private Material mat;
        private RectTransform sparkLayer;
        private readonly List<Particle> live = new List<Particle>();
        private readonly Stack<Image> pool = new Stack<Image>();
        private int sparkCount, smokeCount;

        private sealed class Particle { public Image img; public float x, y, vx, vy, life, age, size; public bool smoke; }

        /// <summary>A snapshot of what is on the screen now (call it from a coroutine; it waits for the end of the frame).</summary>
        public static IEnumerator Capture(Action<Texture2D> done)
        {
            yield return new WaitForEndOfFrame();
            Texture2D t = null;
            try { t = ScreenCapture.CaptureScreenshotAsTexture(); } catch (Exception) { }
            done?.Invoke(t);
        }

        /// <summary>Covers the screen with the frozen snapshot. Takes ownership of the texture (it is destroyed with the overlay).</summary>
        public static BurnTransition Begin(Texture2D snapshot)
        {
            EnsureNoise();
            var go = new GameObject("BurnTransition", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            go.AddComponent<GraphicRaycaster>();
            var b = go.AddComponent<BurnTransition>();
            b.snapshot = snapshot;

            var viewGo = new GameObject("Snapshot", typeof(RectTransform));
            viewGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)viewGo.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            b.view = viewGo.AddComponent<RawImage>(); b.view.texture = snapshot; b.view.raycastTarget = true;
            if (baseMaterial == null) baseMaterial = Resources.Load<Material>("Kit/UIBurn");
            if (baseMaterial == null) { var sh = Shader.Find("Mafia/UIBurn"); if (sh != null) baseMaterial = new Material(sh); }
            if (baseMaterial != null)
            {
                b.mat = new Material(baseMaterial);
                b.mat.SetTexture("_Noise", noiseTex);
                b.mat.SetVector("_Size", new Vector4(Screen.width, Screen.height, 0, 0));
                b.SetFront(-0.5f, BurnCorner.BottomRight, 0);
                b.view.material = b.mat;
            }

            var layer = new GameObject("Sparks", typeof(RectTransform));
            layer.transform.SetParent(go.transform, false);
            b.sparkLayer = (RectTransform)layer.transform; b.sparkLayer.anchorMin = Vector2.zero; b.sparkLayer.anchorMax = Vector2.one; b.sparkLayer.offsetMin = b.sparkLayer.offsetMax = Vector2.zero;
            return b;
        }

        /// <summary>Burns the old screen away to the live one under it. seconds = 0.9 normal, 0.5 fast, 1.6 slow. Ends with the overlay destroyed.</summary>
        public IEnumerator Play(BurnCorner corner, float seconds = 0.9f)
        {
            string mode = BurnSettings.Mode;
            if (mode == BurnSettings.Off || mat == null) { Finish(); yield break; }
            if (mode == BurnSettings.Simple || Application.isBatchMode)
            {
                float fade = 0.2f, e = 0f;
                while (e < fade) { e += Time.unscaledDeltaTime; if (view != null) view.color = new Color(1, 1, 1, 1f - Mathf.Clamp01(e / fade)); yield return null; }
                Finish(); yield break;
            }

            float quality = SystemInfo.graphicsMemorySize < 2048 || SystemInfo.processorCount <= 4 ? 0.5f : 1f;
            const float p0 = -0.03f, p1 = 1f + Band + 0.03f;
            float t0 = Time.unscaledTime, last = t0, dur = Mathf.Max(0.2f, seconds);
            while (true)
            {
                float now = Time.unscaledTime;
                float x = Mathf.Clamp01((now - t0) / dur), dt = Mathf.Min(0.05f, now - last); last = now;
                float eased = x < 0.5f ? 2f * x * x : 1f - Mathf.Pow(-2f * x + 2f, 2f) / 2f;
                float p = p0 + (p1 - p0) * eased;
                SetFront(p, corner, Mathf.Floor(now * 12f));
                Emit(p, dt, quality, corner);
                Move(dt);
                if (x >= 1f) break;
                yield return null;
            }
            Finish();
        }

        private void SetFront(float p, BurnCorner c, float tick)
        {
            bool right = c == BurnCorner.BottomRight || c == BurnCorner.TopRight, bottom = c == BurnCorner.BottomRight || c == BurnCorner.BottomLeft;
            mat.SetFloat("_P", p); mat.SetVector("_Corner", new Vector4(right ? 1f : 0f, bottom ? 1f : 0f, 0, 0)); mat.SetFloat("_Tick", tick);
        }

        private void Finish()
        {
            for (int i = 0; i < live.Count; i++) if (live[i].img != null) Destroy(live[i].img.gameObject);
            live.Clear();
            if (snapshot != null) Destroy(snapshot);
            if (mat != null) Destroy(mat);
            Destroy(gameObject);       // the sparks and the smoke go with it: they must never hang over the new screen
        }

        // ---- sparks and smoke (the mockup's spawn / drawParts)

        private void Emit(float p, float dt, float quality, BurnCorner corner)
        {
            bool right = corner == BurnCorner.BottomRight || corner == BurnCorner.TopRight, bottom = corner == BurnCorner.BottomRight || corner == BurnCorner.BottomLeft;
            float cx = right ? 1f : 0f, cy = bottom ? 1f : 0f, W = Screen.width, H = Screen.height, far = Mathf.Sqrt(W * W + H * H);
            int tries = Mathf.RoundToInt(70f * dt * 60f * quality);
            float k = W / 390f;
            for (int n = 0; n < tries; n++)
            {
                int i = UnityEngine.Random.Range(0, MW * MH), x = i % MW, yJ = i / MW;       // yJ counts from the top
                float u = (float)x / MW, w = (float)yJ / MH;
                float d = Mathf.Sqrt(((u - cx) * W) * ((u - cx) * W) + ((w - cy) * H) * ((w - cy) * H)) / far;
                float t = d * 0.8f + NoiseAt(x, yJ) * 0.2f, s = (p - t) / Band;
                if (s > 0.03f && s < 0.3f && UnityEngine.Random.value < 0.5f)
                    Spawn(u * W, (1f - w) * H, (UnityEngine.Random.value - 0.5f) * 30f * k, (30f + UnityEngine.Random.value * 90f) * k, 0.5f + UnityEngine.Random.value * 0.9f, (1f + UnityEngine.Random.value * 2.4f) * 2f * k, false);
                else if (s > 0.35f && s < 0.7f && UnityEngine.Random.value < 0.04f)
                    Spawn(u * W, (1f - w) * H, (UnityEngine.Random.value - 0.5f) * 12f * k, (14f + UnityEngine.Random.value * 20f) * k, 1.4f + UnityEngine.Random.value * 0.8f, (14f + UnityEngine.Random.value * 22f) * 2f * k, true);
            }
        }

        private void Spawn(float x, float y, float vx, float vy, float life, float size, bool smoke)
        {
            if (smoke ? smokeCount >= MaxSmoke : sparkCount >= MaxSparks) return;
            var img = pool.Count > 0 ? pool.Pop() : NewImage();
            img.gameObject.SetActive(true);
            if (smoke) { smokeCount++; img.transform.SetAsFirstSibling(); } else { sparkCount++; img.transform.SetAsLastSibling(); }
            live.Add(new Particle { img = img, x = x, y = y, vx = vx, vy = vy, life = life, size = size, smoke = smoke });
        }

        private Image NewImage()
        {
            var go = new GameObject("p", typeof(RectTransform));
            go.transform.SetParent(sparkLayer, false);
            var img = go.AddComponent<Image>(); img.sprite = SoftDot(); img.raycastTarget = false;
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        private void Move(float dt)
        {
            float k = Screen.width / 390f;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var q = live[i]; q.age += dt;
                if (q.age >= q.life)
                {
                    q.img.gameObject.SetActive(false); pool.Push(q.img); if (q.smoke) smokeCount--; else sparkCount--;
                    live.RemoveAt(i); continue;
                }
                q.x += q.vx * dt; q.y += q.vy * dt; q.vx += (UnityEngine.Random.value - 0.5f) * 60f * k * dt;
                float f = q.age / q.life;
                var rt = (RectTransform)q.img.transform; rt.anchoredPosition = new Vector2(q.x, q.y);
                if (q.smoke) { float sz = q.size * (1f + f) * 2f; rt.sizeDelta = new Vector2(sz, sz); q.img.color = new Color(90 / 255f, 80 / 255f, 75 / 255f, 0.16f * (1f - f)); }
                else
                {
                    float sz = q.size * (1f - f * 0.5f) * 2f; rt.sizeDelta = new Vector2(sz, sz);
                    q.img.color = f < 0.4f ? new Color(1f, 225 / 255f, 130 / 255f, 1f - f) : new Color(1f, 120 / 255f, 30 / 255f, 1f - f);
                }
            }
        }

        // ---- noise (same function as the mockup: two octaves of value noise, 0..1)

        private static float Hash(double n) { n = Math.Sin(n) * 43758.5453; return (float)(n - Math.Floor(n)); }

        private static float NoiseAt(int x, int yJ) => noise[yJ * MW + x];

        private static void EnsureNoise()
        {
            if (noiseTex != null) return;
            const int g = 24;
            var grid = new float[(g + 2) * (g + 2)];
            for (int i = 0; i < grid.Length; i++) grid[i] = Hash(i * 12.9898 + 4.1);
            Func<float, float> smooth = t => t * t * (3f - 2f * t);
            Func<float, float, float> v = (u, w) =>
            {
                float x = u * g, y = w * g; int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = smooth(x - xi), fy = smooth(y - yi);
                Func<int, int, float> at = (a, b) => grid[b * (g + 2) + a];
                return at(xi, yi) * (1 - fx) * (1 - fy) + at(xi + 1, yi) * fx * (1 - fy) + at(xi, yi + 1) * (1 - fx) * fy + at(xi + 1, yi + 1) * fx * fy;
            };
            noise = new float[MW * MH];
            var px = new Color32[MW * MH];
            for (int y = 0; y < MH; y++)
                for (int x = 0; x < MW; x++)
                {
                    float u = (float)x / MW, w = (float)y / MH;
                    float val = 0.65f * v(u * 0.6f, w * 0.6f) + 0.35f * v(u * 0.9f + 0.05f, w * 0.9f + 0.05f);
                    noise[y * MW + x] = val;
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(val * 255f), 0, 255);
                    px[(MH - 1 - y) * MW + x] = new Color32(b, b, b, 255);     // texture rows go bottom to top; the mockup's go top to bottom
                }
            noiseTex = new Texture2D(MW, MH, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "BurnNoise" };
            noiseTex.SetPixels32(px); noiseTex.Apply(false, true);
            noiseTex.hideFlags = HideFlags.HideAndDontSave;
        }

        private static Sprite SoftDot()
        {
            if (softDot != null) return softDot;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "BurnDot" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d * 0.15f)));
                }
            tex.SetPixels32(px); tex.Apply(false, true); tex.hideFlags = HideFlags.HideAndDontSave;
            softDot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            softDot.hideFlags = HideFlags.HideAndDontSave;
            return softDot;
        }
    }
}
