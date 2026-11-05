using System;
using System.Collections.Generic;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Paints plates, discs and frames of the kit in code (rounded shapes with gradients, rings, a drop shadow and a dashed inner line) at the exact size they are used,
    /// so a card whose height comes from its text still looks like the HTML mockup. Sizes are canvas units; the texture is drawn at <see cref="Q"/> of them.
    /// Colors are the mockup's (sRGB); translucent black layers are corrected for the project's linear color space.
    /// </summary>
    public static class KitProc
    {
        public const float Q = 0.7f;

        public sealed class Spec
        {
            public float W, H, Radius;
            /// <summary>Space around the shape for rings and the shadow (canvas units).</summary>
            public float Margin;
            public Color Top = Color.clear, Mid = Color.clear, Bottom = Color.clear;
            public float MidAt = 0.55f;
            public bool Horizontal;
            // a soft ellipse of color near the top (radial-gradient(ellipse at 70% 0, ...))
            public Color Glow = Color.clear; public Vector2 GlowAt = new Vector2(0.7f, 0f); public float GlowReach = 0.6f;
            // a round highlight (radial-gradient(circle at 35% 30%, a, b 60%, c))
            public bool Radial; public Vector2 RadialAt = new Vector2(0.5f, 0.3f); public Color RA, RB, RC; public float RBAt = 0.6f; public float RadialReach = 0.75f;
            public Color TopLight = Color.clear; public float TopLightPx;      // inset 0 1px 0 #fff3
            public Color Edge = Color.clear; public float EdgePx;              // an inner ring (box-shadow inset 0 0 0 1px ...)
            public readonly List<KeyValuePair<Color, float>> Rings = new List<KeyValuePair<Color, float>>();   // outside rings, from the shape outwards: (color, width reaching outwards)
            public Color Shadow = Color.clear; public float ShadowDy, ShadowBlur;
            public Color Dash = Color.clear; public float DashInset, DashPx, DashLen = 5f, DashGap = 4f, DashRadius = 8f;
            public float Alpha = 1f;
            /// <summary>Paint a small plate that stretches by its borders (flat fill, a ring and rounded corners only): use it with Image.Type.Sliced for shapes whose size is not known when they are built.</summary>
            public bool Sliced;
        }

        // The painted sprites are kept so a screen opens fast the second time, but only up to a memory budget by device tier (DevicePerf.SpriteCacheMb): the least recently used go first.
        // An evicted sprite that a screen still shows stays alive through that screen; it is only painted again if somebody asks for it later.
        private sealed class Entry { public Sprite Sprite; public long Bytes, Tick; }
        private static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
        private static long clock, cachedBytes;

        private static bool TryGet(string key, out Sprite sprite)
        {
            if (cache.TryGetValue(key, out var e) && e.Sprite != null) { e.Tick = ++clock; sprite = e.Sprite; return true; }
            sprite = null; return false;
        }

        private static void Put(string key, Sprite sprite)
        {
            if (cache.TryGetValue(key, out var old)) cachedBytes -= old.Bytes;
            long bytes = sprite != null && sprite.texture != null ? (long)sprite.texture.width * sprite.texture.height * 4 : 0;
            cache[key] = new Entry { Sprite = sprite, Bytes = bytes, Tick = ++clock };
            cachedBytes += bytes;
            long cap = (long)MafiaNight.Core.DevicePerf.SpriteCacheMb * 1048576L;
            if (cachedBytes > cap) Trim((long)(cap * 0.8));
        }

        /// <summary>Drops the least recently used sprites until the cache holds at most <paramref name="keepBytes"/> (0 = everything). Also called when the phone runs low on memory.</summary>
        public static void Trim(long keepBytes)
        {
            if (cachedBytes <= keepBytes) return;
            var order = new List<KeyValuePair<string, Entry>>(cache);
            order.Sort((a, b) => a.Value.Tick.CompareTo(b.Value.Tick));
            foreach (var kv in order)
            {
                if (cachedBytes <= keepBytes) break;
                cache.Remove(kv.Key); cachedBytes -= kv.Value.Bytes;
            }
        }

        public static long CachedBytes => cachedBytes;
        public static int CachedCount => cache.Count;

        /// <summary>A black overlay with this alpha, as it must be drawn in linear space to look like it does in the browser.</summary>
        public static float LinA(float a) => 1f - Mathf.Pow(1f - Mathf.Clamp01(a), 2.2f);

        public static Sprite Paint(string key, Spec s)
        {
            if (TryGet(key, out var hit)) return hit;
            var sp = Make(s);
            Put(key, sp);
            return sp;
        }

        private static float SdRound(float px, float py, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px) - (hx - r), qy = Mathf.Abs(py) - (hy - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Min(Mathf.Max(qx, qy), 0f) + Mathf.Sqrt(ox * ox + oy * oy) - r;
        }

        private static float Cov(float d) => Mathf.Clamp01(0.5f - d * Q);      // d in canvas units, one texture pixel = 1/Q units

        private static Color Over(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 0f) return Color.clear;
            var c = (src * src.a + dst * dst.a * (1f - src.a)) / a;
            c.a = a;
            return c;
        }

        // arc length along a rounded rectangle path (clockwise from the top edge's left end), for the dashes
        private static float PathS(float px, float py, float ax, float ay, float r)
        {
            float lx = 2f * ax, ly = 2f * ay, arc = Mathf.PI * r * 0.5f;
            float apx = Mathf.Abs(px), apy = Mathf.Abs(py);
            bool cx = apx > ax, cy = apy > ay;
            if (cx && cy)
            {
                float ang = Mathf.Atan2(apy - ay, apx - ax);
                if (px > 0 && py > 0) return lx + (Mathf.PI / 2f - ang) * r;
                if (px > 0 && py <= 0) return lx + arc + ly + ang * r;
                if (px <= 0 && py <= 0) return 2f * lx + 2f * arc + ly + (Mathf.PI / 2f - ang) * r;
                return 2f * lx + 3f * arc + 2f * ly + ang * r;
            }
            bool topBottom = cy || (!cx && (ay - apy) < (ax - apx));
            if (topBottom) return py > 0 ? px + ax : lx + 2f * arc + ly + (ax - px);
            return px > 0 ? lx + arc + (ay - py) : 2f * lx + 3f * arc + ly + (py + ay);
        }

        private static Sprite Make(Spec s)
        {
            if (s.Sliced) { s.W = s.H = 2f * s.Radius + 4f; }
            float m = s.Margin;
            float totalW = s.W + 2f * m, totalH = s.H + 2f * m;
            int tw = Mathf.Max(4, Mathf.CeilToInt(totalW * Q)), th = Mathf.Max(4, Mathf.CeilToInt(totalH * Q));
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[tw * th];
            float hx = s.W * 0.5f, hy = s.H * 0.5f, r = Mathf.Min(s.Radius, Mathf.Min(hx, hy));
            float dr = Mathf.Min(s.DashRadius, Mathf.Min(hx, hy) - s.DashInset);
            for (int y = 0; y < th; y++)
            {
                for (int x = 0; x < tw; x++)
                {
                    // canvas units from the shape's centre; py up
                    float cx = (x + 0.5f) / Q - totalW * 0.5f, cy = (y + 0.5f) / Q - totalH * 0.5f;
                    float d = SdRound(cx, cy, hx, hy, r);
                    var c = Color.clear;

                    if (s.Shadow.a > 0f && s.ShadowBlur > 0f)
                    {
                        float ds = SdRound(cx, cy + s.ShadowDy, hx, hy, r);
                        float k = Mathf.Clamp01(1f - ds / s.ShadowBlur);
                        k = k * k * (3f - 2f * k);
                        c = Over(c, new Color(s.Shadow.r, s.Shadow.g, s.Shadow.b, LinA(s.Shadow.a * k)));
                    }
                    for (int i = s.Rings.Count - 1; i >= 0; i--)
                    {
                        float reach = 0f; for (int j = 0; j <= i; j++) reach += s.Rings[j].Value;
                        float cv = Cov(d - reach);
                        if (cv > 0f) c = Over(c, new Color(s.Rings[i].Key.r, s.Rings[i].Key.g, s.Rings[i].Key.b, s.Rings[i].Key.a * cv));
                    }

                    float body = Cov(d);
                    if (body > 0f)
                    {
                        float u = Mathf.Clamp01((cx + hx) / s.W), v = Mathf.Clamp01(1f - (cy + hy) / s.H);   // v: 0 top .. 1 bottom
                        float t = s.Horizontal ? u : v;
                        Color f;
                        if (s.Mid.a > 0f) f = t < s.MidAt ? Color.Lerp(s.Top, s.Mid, t / s.MidAt) : Color.Lerp(s.Mid, s.Bottom, (t - s.MidAt) / (1f - s.MidAt));
                        else f = Color.Lerp(s.Top, s.Bottom, t);
                        if (s.Radial)
                        {
                            float dx = (u - s.RadialAt.x) * s.W, dy = (v - s.RadialAt.y) * s.H;
                            float rt = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / (Mathf.Max(s.W, s.H) * s.RadialReach));
                            f = rt < s.RBAt ? Color.Lerp(s.RA, s.RB, rt / s.RBAt) : Color.Lerp(s.RB, s.RC, (rt - s.RBAt) / (1f - s.RBAt));
                        }
                        if (s.Glow.a > 0f)
                        {
                            float gx = (u - s.GlowAt.x) * s.W, gy = (v - s.GlowAt.y) * s.H;
                            float g = Mathf.Clamp01(1f - Mathf.Sqrt(gx * gx / (s.W * s.W * 0.25f) + gy * gy / (s.H * s.H * 0.5f)) / s.GlowReach / 1.6f);
                            f = Over(f, new Color(s.Glow.r, s.Glow.g, s.Glow.b, s.Glow.a * g));
                        }
                        if (s.TopLight.a > 0f && cy > hy - s.TopLightPx) f = Over(f, s.TopLight);
                        if (s.Edge.a > 0f && d > -s.EdgePx) f = Over(f, s.Edge);
                        if (s.Dash.a > 0f)
                        {
                            float ax = hx - s.DashInset - dr, ay = hy - s.DashInset - dr;
                            float dd = Mathf.Abs(SdRound(cx, cy, hx - s.DashInset, hy - s.DashInset, dr));
                            float line = Mathf.Clamp01(0.5f - (dd - s.DashPx * 0.5f) * Q);
                            if (line > 0f)
                            {
                                float sPath = PathS(cx, cy, Mathf.Max(0.01f, ax), Mathf.Max(0.01f, ay), dr);
                                float cyc = s.DashLen + s.DashGap;
                                if (Mathf.Repeat(sPath, cyc) < s.DashLen) f = Over(f, new Color(s.Dash.r, s.Dash.g, s.Dash.b, s.Dash.a * line));
                            }
                        }
                        f.a *= body;
                        c = Over(c, f);
                    }
                    c.a *= s.Alpha;
                    px[y * tw + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            if (s.Sliced)
            {
                float b = (s.Margin + s.Radius + 2f) * Q;
                return Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            }
            return Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 100f);
        }

        // ---------------------------------------------------------------- the ribbon (a banner with notched ends)

        public sealed class RibbonSpec
        {
            public float H = 240f, Notch = 28f, Margin = 40f;
            public Color Top, Mid, Bottom; public float MidAt = 0.6f;
            public Color Lip; public float LipDy;                          // the hard edge under the ribbon (drop-shadow 0 5px 0)
            public float ShadowA = 0.65f, ShadowDy = 12f, ShadowBlur = 16f; // the soft shadow
            public Color Edge; public float EdgePx;                        // a ring just inside the edge
            public Color Line; public float LineInset, LinePx;             // a fine engraved line following the shape, inside
            public Color TopLight; public float TopLightPx;
            /// <summary>Each end that keeps its shape when the ribbon is stretched (the middle is the same everywhere along it).</summary>
            public float EndW => Notch + 18f;
        }

        private static readonly Vector2[] ribbonPoly = new Vector2[6];

        private static float SdPoly(float px, float py, Vector2[] v)
        {
            float best = float.MaxValue; bool inside = false;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
            {
                Vector2 a = v[j], b = v[i], e = b - a, w = new Vector2(px, py) - a;
                float t = Mathf.Clamp01(Vector2.Dot(w, e) / e.sqrMagnitude);
                best = Mathf.Min(best, (w - e * t).magnitude);
                if ((a.y > py) != (b.y > py) && px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? -best : best;
        }

        /// <summary>
        /// A banner whose ends are notched like a ribbon, painted once as a small strip that stretches in the middle (use it with Image.Type.Sliced and pixelsPerUnitMultiplier = 1 / <see cref="Q"/>).
        /// The image must be as tall as <see cref="RibbonSpec.H"/> plus the margin on both sides, so only its width stretches.
        /// </summary>
        public static Sprite Ribbon(string key, RibbonSpec s)
        {
            if (TryGet(key, out var hit)) return hit;
            float endW = s.EndW, W = 2f * endW + 8f, m = s.Margin, hx = W * 0.5f, hy = s.H * 0.5f, n = s.Notch;
            float totalW = W + 2f * m, totalH = s.H + 2f * m;
            int tw = Mathf.Max(4, Mathf.CeilToInt(totalW * Q)), th = Mathf.Max(4, Mathf.CeilToInt(totalH * Q));
            var poly = ribbonPoly;
            poly[0] = new Vector2(-hx, hy); poly[1] = new Vector2(hx, hy); poly[2] = new Vector2(hx - n, 0f); poly[3] = new Vector2(hx, -hy); poly[4] = new Vector2(-hx, -hy); poly[5] = new Vector2(-hx + n, 0f);
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[tw * th];
            for (int y = 0; y < th; y++)
            {
                for (int x = 0; x < tw; x++)
                {
                    float cx = (x + 0.5f) / Q - totalW * 0.5f, cy = (y + 0.5f) / Q - totalH * 0.5f;
                    var c = Color.clear;
                    if (s.ShadowA > 0f)
                    {
                        float ds = SdPoly(cx, cy + s.ShadowDy, poly);
                        float k = Mathf.Clamp01(1f - ds / s.ShadowBlur); k = k * k * (3f - 2f * k);
                        c = Over(c, new Color(0f, 0f, 0f, LinA(s.ShadowA * k)));
                    }
                    if (s.Lip.a > 0f) { float cl = Cov(SdPoly(cx, cy + s.LipDy, poly)); if (cl > 0f) c = Over(c, new Color(s.Lip.r, s.Lip.g, s.Lip.b, s.Lip.a * cl)); }
                    float d = SdPoly(cx, cy, poly), body = Cov(d);
                    if (body > 0f)
                    {
                        float v = Mathf.Clamp01(1f - (cy + hy) / s.H);
                        Color f = v < s.MidAt ? Color.Lerp(s.Top, s.Mid, v / s.MidAt) : Color.Lerp(s.Mid, s.Bottom, (v - s.MidAt) / (1f - s.MidAt));
                        if (s.TopLight.a > 0f && cy > hy - s.TopLightPx) f = Over(f, s.TopLight);
                        if (s.Edge.a > 0f && d > -s.EdgePx) f = Over(f, s.Edge);
                        if (s.Line.a > 0f) { float ln = Mathf.Clamp01(0.5f - (Mathf.Abs(d + s.LineInset) - s.LinePx * 0.5f) * Q); if (ln > 0f) f = Over(f, new Color(s.Line.r, s.Line.g, s.Line.b, s.Line.a * ln)); }
                        f.a *= body;
                        c = Over(c, f);
                    }
                    px[y * tw + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = (m + endW) * Q;
            var sp = Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, 0f, b, 0f));
            Put(key, sp);
            return sp;
        }

        /// <summary>A tiny map pin (the head and the needle) for the "pinned" tag.</summary>
        public static Sprite Pin(Color color)
        {
            string key = "pin" + ColorUtility.ToHtmlStringRGBA(color);
            if (TryGet(key, out var hit)) return hit;
            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / n * 24f, fy = (n - y - 0.5f) / n * 24f;     // 24-unit space, y down
                    float head = Mathf.Sqrt((fx - 12f) * (fx - 12f) + (fy - 8.5f) * (fy - 8.5f)) - 5.2f;
                    float plate = Mathf.Max(Mathf.Abs(fx - 12f) - 8f, Mathf.Abs(fy - 13f) - 1.3f);
                    float needle = Mathf.Max(Mathf.Abs(fx - 12f) - 1f, Mathf.Max(fy - 22f, 12f - fy));
                    float dmin = Mathf.Min(head, Mathf.Min(plate, needle));
                    var c = color; c.a = Mathf.Clamp01(0.5f - dmin * (n / 24f));
                    px[y * n + x] = c;
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 48f);
            Put(key, sp);
            return sp;
        }
    }
}
