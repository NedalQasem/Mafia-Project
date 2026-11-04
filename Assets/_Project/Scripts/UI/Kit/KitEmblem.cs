using System;
using System.Collections.Generic;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Paints the emblems of the card backs (Docs/mockups/role-cards.html: diamond, lattice star, rose, crossed pistols, skull with a hat) as embossed metal:
    /// every emblem is a stack of shapes (metal pieces with a rim light, a dark outline and a gradient; cavities; engraved lines; gleams) rasterised once into
    /// a sprite in the colour of the card's frame ornament, with a soft shadow under the whole thing. Drawn once per emblem and colour, then cached.
    /// </summary>
    public static class KitEmblem
    {
        public enum Kind { Metal, Cut, Line, Gleam }

        /// <summary>Places a layer: scaled, mirrored, turned (degrees, clockwise on screen) about <see cref="Pivot"/> and moved.</summary>
        public struct Xf
        {
            public float Scale, Rot, Dx, Dy, PivotX, PivotY; public bool Mirror;
            public static Xf At(float scale, float rot, float dx, float dy, bool mirror = false, float pivotX = 50f, float pivotY = 50f)
            { return new Xf { Scale = scale, Rot = rot, Dx = dx, Dy = dy, Mirror = mirror, PivotX = pivotX, PivotY = pivotY }; }
            public Vector2 Apply(Vector2 p)
            {
                p -= new Vector2(PivotX, PivotY);
                if (Mirror) p.x = -p.x;
                p *= Scale;
                float c = Mathf.Cos(Rot * Mathf.Deg2Rad), s = Mathf.Sin(Rot * Mathf.Deg2Rad);
                p = new Vector2(p.x * c - p.y * s, p.x * s + p.y * c);
                return p + new Vector2(PivotX + Dx, PivotY + Dy);
            }
        }

        public sealed class Layer
        {
            public Kind Kind; public int Tone; public string Spec = ""; public bool EvenOdd, Sym; public float Width = 1f; public Xf? Xf;
        }

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static readonly string[] Ids = { "diamond", "lattice", "rose", "pistols", "skull" };

        /// <summary>The emblem of a card back in the metal of <paramref name="orn"/>. 100x100 units drawn at <paramref name="px"/> pixels.</summary>
        public static Sprite Paint(string id, Color orn, int px = 288)
        {
            string key = id + "|" + ColorUtility.ToHtmlStringRGB(orn) + "|" + px;
            if (cache.TryGetValue(key, out var hit) && hit != null) return hit;
            var layers = Design(id);
            float scale = px / 100f;
            var buf = new Vector4[px * px];                       // premultiplied rgb + alpha
            int bevel = Mathf.Max(1, Mathf.RoundToInt(px / 110f)), outline = Mathf.Max(1, Mathf.RoundToInt(px / 100f * 1.05f));

            // the shadow of everything that is metal
            var all = new List<List<Vector2>>();
            foreach (var l in layers) if (l.Kind == Kind.Metal) all.AddRange(Outlines(l, false));
            var sh = Blur(Blur(Fill(all, scale, px, false, 14), 3), 3);
            int sdy = Mathf.RoundToInt(2.4f * scale);
            for (int y = 0; y < sh.H; y++)
                for (int x = 0; x < sh.W; x++)
                {
                    float a = sh.A[y * sh.W + x] * 0.55f;
                    int tx = sh.X0 + x, ty = sh.Y0 + y + sdy;
                    if (a > 0.003f && tx >= 0 && tx < px && ty >= 0 && ty < px) Over(buf, ty * px + tx, Color.black, a);
                }

            Color top0 = Color.Lerp(orn, Color.white, 0.55f), bot0 = Color.Lerp(orn, Color.black, 0.45f);
            Color top1 = Color.Lerp(orn, Color.white, 0.16f), bot1 = Color.Lerp(orn, Color.black, 0.62f);
            Color top2 = Color.Lerp(orn, Color.black, 0.45f), bot2 = Color.Lerp(orn, Color.black, 0.82f);
            Color rim = Color.Lerp(orn, Color.white, 0.5f);
            var cavity = new Color(0.04f, 0.025f, 0.015f);

            foreach (var l in layers)
            {
                if (l.Kind == Kind.Metal || l.Kind == Kind.Cut)
                {
                    var polys = Outlines(l, !l.EvenOdd);
                    var m = Fill(polys, scale, px, l.EvenOdd, outline + bevel + 2);
                    if (m.W <= 0) continue;
                    if (l.Kind == Kind.Metal)
                    {
                        var ring = Dilate(m, outline);
                        for (int y = 0; y < ring.H; y++) for (int x = 0; x < ring.W; x++) { float a = ring.A[y * ring.W + x] * 0.96f; if (a > 0.003f) Over(buf, (ring.Y0 + y) * px + ring.X0 + x, Color.black, a); }
                        Bounds(polys, out _, out float minY, out _, out float maxY);
                        Color t = l.Tone == 0 ? top0 : l.Tone == 1 ? top1 : top2, b = l.Tone == 0 ? bot0 : l.Tone == 1 ? bot1 : bot2;
                        for (int y = 0; y < m.H; y++)
                            for (int x = 0; x < m.W; x++)
                            {
                                float a = m.A[y * m.W + x];
                                if (a <= 0.003f) continue;
                                int gx = m.X0 + x, gy = m.Y0 + y;
                                float k = Mathf.Clamp01(((gy + 0.5f) / scale - minY) / Mathf.Max(1f, maxY - minY));
                                Color c = Color.Lerp(t, b, k * k * (3f - 2f * k));
                                float hi = a * (1f - m.At(gx - bevel, gy - bevel)), sd = a * (1f - m.At(gx + bevel, gy + bevel));
                                c = Color.Lerp(c, Color.white, Mathf.Clamp01(hi) * 0.6f);
                                c = Color.Lerp(c, Color.black, Mathf.Clamp01(sd) * 0.4f);
                                Over(buf, gy * px + gx, c, a);
                            }
                    }
                    else
                    {
                        for (int y = 0; y < m.H; y++)
                            for (int x = 0; x < m.W; x++)
                            {
                                float a = m.A[y * m.W + x];
                                if (a <= 0.003f) continue;
                                int gx = m.X0 + x, gy = m.Y0 + y;
                                Over(buf, gy * px + gx, cavity, a * 0.9f);
                                float lit = a * (1f - m.At(gx + bevel, gy + bevel));
                                if (lit > 0.01f) Over(buf, gy * px + gx, rim, Mathf.Clamp01(lit) * 0.4f);
                            }
                    }
                }
                else
                {
                    var m = Stroke(Outlines(l, false), l.Width, scale, px);
                    bool gleam = l.Kind == Kind.Gleam;
                    int off = Mathf.Max(1, Mathf.RoundToInt(0.55f * scale));
                    for (int y = 0; y < m.H; y++)
                        for (int x = 0; x < m.W; x++)
                        {
                            float a = m.A[y * m.W + x];
                            if (a <= 0.003f) continue;
                            int gx = m.X0 + x, gy = m.Y0 + y;
                            if (gleam) Over(buf, gy * px + gx, Color.white, a * 0.7f);
                            else
                            {
                                if (gx + off < px && gy + off < px) Over(buf, (gy + off) * px + gx + off, rim, a * 0.3f);       // the light catches the lower lip of the groove
                                Over(buf, gy * px + gx, Color.black, a * 0.7f);
                            }
                        }
                }
            }

            var pix = new Color32[px * px];
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                {
                    var v = buf[(px - 1 - y) * px + x];                // texture rows run bottom-up
                    if (v.w <= 0.002f) { pix[y * px + x] = new Color32(0, 0, 0, 0); continue; }
                    pix[y * px + x] = new Color(Mathf.Clamp01(v.x / v.w), Mathf.Clamp01(v.y / v.w), Mathf.Clamp01(v.z / v.w), Mathf.Clamp01(v.w));
                }
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(pix); tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 100f);
            cache[key] = sp;
            return sp;
        }

        // ------------------------------------------------------------------ rasterising

        private sealed class Mask
        {
            public int X0, Y0, W, H; public float[] A;
            public Mask(int x0, int y0, int w, int h) { X0 = x0; Y0 = y0; W = Math.Max(0, w); H = Math.Max(0, h); A = new float[W * H]; }
            public float At(int gx, int gy) { int x = gx - X0, y = gy - Y0; return x < 0 || y < 0 || x >= W || y >= H ? 0f : A[y * W + x]; }
        }

        private static void Over(Vector4[] buf, int i, Color c, float a)
        {
            var d = buf[i]; float k = 1f - a;
            buf[i] = new Vector4(c.r * a + d.x * k, c.g * a + d.y * k, c.b * a + d.z * k, a + d.w * k);
        }

        private static List<List<Vector2>> Outlines(Layer l, bool unite)
        {
            var shapes = KitSvg.Shapes(l.Spec);
            var res = new List<List<Vector2>>();
            foreach (var s in shapes)
            {
                var a = Moved(s, l.Xf, false);
                if (unite && Area(a) < 0f) a.Reverse();
                res.Add(a);
                if (l.Sym) { var b = Moved(s, l.Xf, true); if (unite && Area(b) < 0f) b.Reverse(); res.Add(b); }
            }
            return res;
        }

        private static List<Vector2> Moved(List<Vector2> s, Xf? xf, bool mirror)
        {
            var r = new List<Vector2>(s.Count);
            foreach (var p in s) { var q = mirror ? new Vector2(100f - p.x, p.y) : p; r.Add(xf.HasValue ? xf.Value.Apply(q) : q); }
            return r;
        }

        private static float Area(List<Vector2> p)
        {
            float a = 0f;
            for (int i = 0; i < p.Count; i++) { var u = p[i]; var v = p[(i + 1) % p.Count]; a += u.x * v.y - v.x * u.y; }
            return a * 0.5f;
        }

        private static void Bounds(List<List<Vector2>> polys, out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = minY = float.MaxValue; maxX = maxY = float.MinValue;
            foreach (var p in polys) foreach (var v in p) { minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y); }
        }

        private const int SS = 4;

        /// <summary>Coverage of the polygons (non-zero winding, or even-odd), exact across a row and 4 samples down.</summary>
        private static Mask Fill(List<List<Vector2>> polys, float scale, int px, bool evenOdd, int pad)
        {
            if (polys.Count == 0) return new Mask(0, 0, 0, 0);
            Bounds(polys, out float minX, out float minY, out float maxX, out float maxY);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX * scale) - pad), x1 = Mathf.Min(px - 1, Mathf.CeilToInt(maxX * scale) + pad);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY * scale) - pad), y1 = Mathf.Min(px - 1, Mathf.CeilToInt(maxY * scale) + pad);
            var m = new Mask(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            if (m.W <= 0 || m.H <= 0) return m;
            var xs = new List<Vector2>(32);
            for (int row = y0; row <= y1; row++)
                for (int s = 0; s < SS; s++)
                {
                    float y = (row + (s + 0.5f) / SS) / scale;
                    xs.Clear();
                    foreach (var poly in polys)
                        for (int i = 0, n = poly.Count; i < n; i++)
                        {
                            var a = poly[i]; var b = poly[(i + 1) % n];
                            if ((a.y <= y) == (b.y <= y)) continue;
                            xs.Add(new Vector2((a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y)) * scale, b.y > a.y ? 1f : -1f));
                        }
                    if (xs.Count < 2) continue;
                    xs.Sort((p, q) => p.x.CompareTo(q.x));
                    int wind = 0;
                    for (int k = 0; k + 1 < xs.Count; k++)
                    {
                        wind += evenOdd ? 1 : (int)xs[k].y;
                        bool inside = evenOdd ? (wind & 1) == 1 : wind != 0;
                        if (inside) Span(m, row - y0, xs[k].x, xs[k + 1].x, 1f / SS);
                    }
                }
            return m;
        }

        private static void Span(Mask m, int row, float xa, float xb, float weight)
        {
            float a = Mathf.Max(xa, m.X0), b = Mathf.Min(xb, m.X0 + m.W);
            if (b <= a) return;
            for (int ix = (int)a; ix < m.X0 + m.W && ix < b; ix++)
            {
                float cov = Mathf.Min(b, ix + 1) - Mathf.Max(a, ix);
                if (cov > 0f) { int i = row * m.W + (ix - m.X0); m.A[i] = Mathf.Min(1f, m.A[i] + cov * weight); }
            }
        }

        /// <summary>The mask grown by <paramref name="r"/> pixels (a square reach, enough for a black outline).</summary>
        private static Mask Dilate(Mask m, int r)
        {
            var t = new Mask(m.X0, m.Y0, m.W, m.H);
            for (int y = 0; y < m.H; y++)
                for (int x = 0; x < m.W; x++)
                {
                    float v = 0f;
                    for (int k = -r; k <= r; k++) { int xx = x + k; if (xx >= 0 && xx < m.W) v = Mathf.Max(v, m.A[y * m.W + xx]); }
                    t.A[y * m.W + x] = v;
                }
            var o = new Mask(m.X0, m.Y0, m.W, m.H);
            for (int y = 0; y < m.H; y++)
                for (int x = 0; x < m.W; x++)
                {
                    float v = 0f;
                    for (int k = -r; k <= r; k++) { int yy = y + k; if (yy >= 0 && yy < m.H) v = Mathf.Max(v, t.A[yy * m.W + x]); }
                    o.A[y * m.W + x] = v;
                }
            return o;
        }

        private static Mask Blur(Mask m, int r)
        {
            if (m.W <= 0) return m;
            var t = new Mask(m.X0, m.Y0, m.W, m.H); var o = new Mask(m.X0, m.Y0, m.W, m.H);
            float inv = 1f / (2 * r + 1);
            for (int y = 0; y < m.H; y++)
                for (int x = 0; x < m.W; x++) { float v = 0f; for (int k = -r; k <= r; k++) { int xx = x + k; if (xx >= 0 && xx < m.W) v += m.A[y * m.W + xx]; } t.A[y * m.W + x] = v * inv; }
            for (int y = 0; y < m.H; y++)
                for (int x = 0; x < m.W; x++) { float v = 0f; for (int k = -r; k <= r; k++) { int yy = y + k; if (yy >= 0 && yy < m.H) v += t.A[yy * m.W + x]; } o.A[y * m.W + x] = v * inv; }
            return o;
        }

        /// <summary>A stroke of the given width (units) along the polylines.</summary>
        private static Mask Stroke(List<List<Vector2>> lines, float width, float scale, int px)
        {
            if (lines.Count == 0) return new Mask(0, 0, 0, 0);
            Bounds(lines, out float minX, out float minY, out float maxX, out float maxY);
            int pad = Mathf.CeilToInt(width * scale) + 2;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX * scale) - pad), x1 = Mathf.Min(px - 1, Mathf.CeilToInt(maxX * scale) + pad);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY * scale) - pad), y1 = Mathf.Min(px - 1, Mathf.CeilToInt(maxY * scale) + pad);
            var m = new Mask(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            float half = width * 0.5f * scale;
            foreach (var l in lines)
                for (int i = 0; i + 1 < l.Count; i++)
                {
                    var a = l[i] * scale; var b = l[i + 1] * scale;
                    int ax = Mathf.Max(x0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - half) - 1), bx = Mathf.Min(x1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + half) + 1);
                    int ay = Mathf.Max(y0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - half) - 1), by = Mathf.Min(y1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + half) + 1);
                    var ab = b - a; float len2 = ab.sqrMagnitude;
                    for (int y = ay; y <= by; y++)
                        for (int x = ax; x <= bx; x++)
                        {
                            var p = new Vector2(x + 0.5f, y + 0.5f);
                            float t = len2 <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                            float d = (p - (a + ab * t)).magnitude;
                            float c = Mathf.Clamp01(0.5f - (d - half));
                            int idx = (y - y0) * m.W + (x - x0);
                            if (c > m.A[idx]) m.A[idx] = c;
                        }
                }
            return m;
        }

        // ------------------------------------------------------------------ the drawings (100x100 units, light from the top left)

        private static Layer M(string spec, int tone = 0, bool sym = false, bool evenOdd = false, Xf? xf = null) { return new Layer { Kind = Kind.Metal, Spec = spec, Tone = tone, Sym = sym, EvenOdd = evenOdd, Xf = xf }; }
        private static Layer C(string spec, bool sym = false, Xf? xf = null) { return new Layer { Kind = Kind.Cut, Spec = spec, Sym = sym, Xf = xf }; }
        private static Layer L(string spec, float w = 0.9f, bool sym = false, Xf? xf = null) { return new Layer { Kind = Kind.Line, Spec = spec, Width = w, Sym = sym, Xf = xf }; }
        private static Layer G(string spec, float w = 0.8f, bool sym = false, Xf? xf = null) { return new Layer { Kind = Kind.Gleam, Spec = spec, Width = w, Sym = sym, Xf = xf }; }

        private static string F(float v) { return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }

        private static string Star(float cx, float cy, float big, float small, int n, float startDeg)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < n * 2; i++)
            {
                float r = (i & 1) == 0 ? big : small, a = (startDeg + i * 180f / n) * Mathf.Deg2Rad;
                sb.Append(i == 0 ? "M" : "L").Append(F(cx + Mathf.Cos(a) * r)).Append(' ').Append(F(cy + Mathf.Sin(a) * r));
            }
            return sb.Append('Z').ToString();
        }

        /// <summary>A bone: a shaft with two knobs at each end, as one united shape.</summary>
        private static string Bone(float x0, float y0, float x1, float y1, float w = 6f, float knob = 4.6f)
        {
            var d = new Vector2(x1 - x0, y1 - y0).normalized; var n = new Vector2(-d.y, d.x);
            Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1), h = n * (w * 0.5f);
            string shaft = "M" + F((a + h).x) + " " + F((a + h).y) + "L" + F((b + h).x) + " " + F((b + h).y) + "L" + F((b - h).x) + " " + F((b - h).y) + "L" + F((a - h).x) + " " + F((a - h).y) + "Z";
            var sb = new System.Text.StringBuilder(shaft);
            foreach (var end in new[] { a, b })
                foreach (float s in new[] { 1f, -1f })
                {
                    var c = end + n * (s * (w * 0.5f + knob * 0.15f)) + d * (end == a ? -knob * 0.35f : knob * 0.35f);
                    sb.Append("|O").Append(F(c.x)).Append(' ').Append(F(c.y)).Append(' ').Append(F(knob));
                }
            return sb.ToString();
        }

        private static List<Layer> Gun(Xf xf)
        {
            return new List<Layer>
            {
                M("M10 32L26 35L23 56Q22 61 16 61L9 59Q3 57 5 51Z", 2, xf: xf),                                    // the wooden grip
                L("M9 40L22 43M8 45L21 48M7 50L20 53", 0.8f, xf: xf),
                M("M8 18Q8 14 12 14H38V35H30Q25 35 23 39L21 42L10 38Q7 36 7 32Z", 1, xf: xf),                      // the frame
                M("M36 17H93Q97 17 97 21V26Q97 30 93 30H36Z", 0, xf: xf),                                          // the barrel
                G("M38 19.5H92", 0.9f, xf: xf),
                C("E95.5 23.5 1.4 3", xf: xf),
                M("M87 11.5H93V17H87Z", 0, xf: xf),                                                                // the front sight
                M("R38 14 17 20 4", 0, xf: xf),                                                                    // the cylinder
                L("M42 16V32M46.5 16V32M51 16V32", 0.8f, xf: xf),
                M("M9 15L4 8Q7 5 11 8L15 14Z", 1, xf: xf),                                                         // the hammer
                M("M31 40Q33 46 37 45L36 40Z", 1, xf: xf),                                                         // the trigger
                L("M26 40Q27 50 36 49Q40 48 39 41", 1.5f, xf: xf),
            };
        }

        private static List<Layer> Design(string id)
        {
            var ls = new List<Layer>();
            switch (id)
            {
                case "lattice":
                    {
                        ls.Add(M("O50 50 42|O50 50 36.5", 1, evenOdd: true));
                        ls.Add(L("O50 50 31", 0.7f));
                        for (int k = 0; k < 8; k++) { float a = (22.5f + k * 45f) * Mathf.Deg2Rad; ls.Add(C("O" + F(50 + Mathf.Cos(a) * 39.2f) + " " + F(50 + Mathf.Sin(a) * 39.2f) + " 1.5")); }
                        for (int pass = 0; pass < 2; pass++)
                            for (int k = 0; k < 8; k++)
                            {
                                bool card = k % 2 == 0;
                                if ((pass == 0) == card) continue;                                  // the short diagonal points first, the long ones over them
                                float a = (-90f + k * 45f), tip = card ? 35f : 24f, bs = 8.5f;
                                Vector2 c = new Vector2(50, 50), t = c + Dir(a) * tip, l = c + Dir(a - 45f) * bs, r = c + Dir(a + 45f) * bs;
                                ls.Add(M("M50 50L" + F(t.x) + " " + F(t.y) + "L" + F(l.x) + " " + F(l.y) + "Z", 1));
                                ls.Add(M("M50 50L" + F(t.x) + " " + F(t.y) + "L" + F(r.x) + " " + F(r.y) + "Z", 0));
                            }
                        ls.Add(C("O50 50 6.5")); ls.Add(M("O50 50 3.4", 0));
                        break;
                    }
                case "rose":
                    {
                        ls.Add(M("M48.2 62C47 74 49.5 86 47 98L53.4 98C51 86 53.4 74 51.8 62Z", 2));                // the stem
                        ls.Add(M("M50 77C40 69 27 71 22 80C33 87 44 85 50 77Z", 1)); ls.Add(L("M49 78C41 76 32 77 26 80", 0.7f));
                        ls.Add(M("M50 87C59 78 71 79 77 87C67 93 56 92 50 87Z", 1)); ls.Add(L("M51 87C58 85 67 85 73 87", 0.7f));
                        ls.Add(M("M52 70L57 68L55 74Z", 2)); ls.Add(M("M48 84L43 82L45 88Z", 2));              // thorns
                        const string petal = "M0 0C-17 -4 -21 -22 -10 -30C-4 -35 4 -35 10 -30C21 -22 17 -4 0 0Z";
                        for (int k = 0; k < 5; k++) { var xf = Xf.At(1f, k * 72f, 50, 40, false, 0, 0); ls.Add(M(petal, k % 2 == 0 ? 1 : 2, xf: xf)); ls.Add(L("M0 -8C-3 -15 -3 -23 0 -28", 0.7f, xf: xf)); }
                        for (int k = 0; k < 5; k++) { var xf = Xf.At(0.66f, 36f + k * 72f, 50, 40, false, 0, 0); ls.Add(M(petal, k % 2 == 0 ? 0 : 1, xf: xf)); ls.Add(L("M0 -8C-3 -15 -3 -23 0 -28", 0.8f, xf: xf)); }
                        for (int k = 0; k < 4; k++) { var xf = Xf.At(0.36f, 20f + k * 90f, 50, 40, false, 0, 0); ls.Add(M(petal, k % 2 == 0 ? 0 : 1, xf: xf)); }
                        ls.Add(L("M47 40C47 36 53 36 53 40C53 44 48 45 46.5 42", 0.8f)); ls.Add(G("M41 28C44 24 49 22 53 22", 0.9f));
                        break;
                    }
                case "pistols":
                    {
                        ls.AddRange(Gun(Xf.At(0.86f, 28f, 0, 24, true, 50, 33)));
                        ls.AddRange(Gun(Xf.At(0.86f, -28f, 0, 24, false, 50, 33)));
                        ls.Add(M(Star(50, 17, 11.5f, 5f, 5, -90), 0)); ls.Add(G("M45.5 14L50 8.5", 0.9f));
                        break;
                    }
                case "skull":
                    {
                        ls.Add(M(Bone(17, 63, 83, 87), 1)); ls.Add(M(Bone(83, 63, 17, 87), 1));
                        ls.Add(M("M25 56C21 42 30 31 50 31C70 31 79 42 75 56C74 61 71 64 67 65.5L67 73C67 77 64 79 61 79L39 79C36 79 33 77 33 73L33 65.5C29 64 26 61 25 56Z", 0));      // the skull
                        ls.Add(C("M31 53C31 47 37 44 42 46C47 48 48 54 46 58C44 62 36 63 33 59C31.5 57.5 31 55.5 31 53Z", sym: true));
                        ls.Add(C("M50 62L46 71L50 69.5L54 71Z"));
                        ls.Add(L("M36 72H64M41 72V79M46 72V79M50 72V79M54 72V79M59 72V79", 0.8f));               // the teeth
                        ls.Add(L("M61 36L57 43L60 47", 0.7f)); ls.Add(G("M31 46C33 40 39 36 46 35", 0.9f));
                        ls.Add(M("E50 31 38 7", 1));                                                             // the brim
                        ls.Add(M("M32 31C31 17 38 9 50 9C62 9 69 17 68 31C60 36 40 36 32 31Z", 1));              // the crown
                        ls.Add(L("M42 11.5C46 15 54 15 58 11.5", 0.9f));
                        ls.Add(M("M31.7 23C42 27 58 27 68.3 23L68 30C58 35 42 35 32 30Z", 2));                   // the band
                        ls.Add(M("R46.5 27 7 5 1", 0)); ls.Add(C("R48.6 28.6 2.8 1.8 0.5"));
                        ls.Add(G("M36 17C38 12 43 10 48 9.6", 0.9f));
                        break;
                    }
                default:                                                                                           // diamond
                    {
                        ls.Add(M("M50 5L95 50L50 95L5 50Z", 1));
                        ls.Add(C("M50 16L84 50L50 84L16 50Z"));
                        ls.Add(M("M50 24L50 50L26 50Z", 0)); ls.Add(M("M50 24L74 50L50 50Z", 1));
                        ls.Add(M("M50 76L50 50L74 50Z", 2)); ls.Add(M("M50 76L26 50L50 50Z", 1));
                        ls.Add(M("M50 38L62 50L50 62L38 50Z", 0)); ls.Add(G("M44 46L50 40", 0.9f));
                        foreach (var p in new[] { new Vector2(69, 31), new Vector2(31, 31), new Vector2(69, 69), new Vector2(31, 69) }) ls.Add(M("O" + F(p.x) + " " + F(p.y) + " 2.6", 0));
                        break;
                    }
            }
            return ls;
        }

        private static Vector2 Dir(float deg) { return new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)); }
    }
}
