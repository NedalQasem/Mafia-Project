using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static MafiaNight.UI.Kit.Editor.KitProceduralTextures;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Loading screen art: night skyline layers, road strips, the old mafia sedan, its wheels and headlight beam.</summary>
    public static class KitProceduralTextures5
    {
        private static float Circ(float x, float y, float cx, float cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        private static float Seg(float x, float y, Vector2 a, Vector2 b)
        {
            var p = new Vector2(x, y); var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }
        private static void Quad(List<Vector2> l, Vector2 p0, Vector2 p1, Vector2 p2, int n = 8)
        {
            for (int i = 1; i <= n; i++) { float t = i / (float)n, u = 1 - t; l.Add(u * u * p0 + 2 * u * t * p1 + t * t * p2); }
        }

        private static Vector2[] bodyPoly, glassPoly;

        private static void InitCar()
        {
            var b = new List<Vector2> { new Vector2(26, 114), new Vector2(26, 86) };
            Quad(b, new Vector2(26, 86), new Vector2(26, 75), new Vector2(38, 73));
            b.Add(new Vector2(98, 70)); b.Add(new Vector2(111, 44));
            Quad(b, new Vector2(111, 44), new Vector2(115, 35), new Vector2(128, 34));
            b.Add(new Vector2(196, 33));
            Quad(b, new Vector2(196, 33), new Vector2(209, 33), new Vector2(215, 42));
            b.Add(new Vector2(237, 68)); b.Add(new Vector2(322, 72));
            Quad(b, new Vector2(322, 72), new Vector2(343, 74), new Vector2(347, 90));
            b.Add(new Vector2(349, 114));
            bodyPoly = b.ToArray();
            var g = new List<Vector2> { new Vector2(120, 47), new Vector2(196, 45) };
            Quad(g, new Vector2(196, 45), new Vector2(204, 45), new Vector2(208, 51));
            g.Add(new Vector2(225, 69)); g.Add(new Vector2(116, 69));
            glassPoly = g.ToArray();
        }

        private static Color Chrome(float y) => Color.Lerp(Color.Lerp(C(0xffffff), C(0xa9afb5), Mathf.Clamp01(y / 6f)), C(0x3a3d40), Mathf.Clamp01((y - 3f) / 8f));

        private static Color CarPixel(float X, float Y)   // X, Y in the 400 x 150 design space
        {
            Color col = Color.clear;
            bool In(Vector2[] p, float x, float y) => InPoly(x, y, p);
            // body with a thin black edge
            if (In(bodyPoly, X, Y))
            {
                float t = Mathf.Clamp01((Y - 33f) / 82f);
                col = t < .3f ? Color.Lerp(C(0x4a4a52), C(0x1b1b20), t / .3f) : Color.Lerp(C(0x1b1b20), C(0x050506), (t - .3f) / .7f);
                const float e = 1.1f;
                if (!In(bodyPoly, X + e, Y) || !In(bodyPoly, X - e, Y) || !In(bodyPoly, X, Y + e) || !In(bodyPoly, X, Y - e)) col = C(0x000000);
                if (Seg(X, Y, new Vector2(40, 74), new Vector2(98, 71)) < 1f || Seg(X, Y, new Vector2(241, 70), new Vector2(322, 73)) < 1f) col = Over(C(0xffffff, .3f), col);
                if (Mathf.Abs(Y - 96f) < .9f && X > 30f && X < 346f) col = Chrome(2f);
                if ((Mathf.Abs(X - 162f) < .6f && Y > 46f && Y < 112f) || (Mathf.Abs(X - 236f) < .6f && Y > 70f && Y < 112f) || (Mathf.Abs(X - 112f) < .6f && Y > 70f && Y < 112f) || (Mathf.Abs(Y - 112f) < .6f && X > 112f && X < 236f)) col = Over(C(0x000000, .75f), col);
                if (X > 150f && X < 159f && Y > 85f && Y < 87.6f) col = Chrome(1f);
                if (X > 224f && X < 233f && Y > 85f && Y < 87.6f) col = Chrome(1f);
            }
            // wheel arches (the wheels themselves are separate sprites)
            foreach (float cx in new[] { 92f, 292f })
            {
                float r = Circ(X, Y, cx, 118f);
                if (r < 31f) col = C(0x000000);
                if (r > 31f && r < 33.6f && Y < 118f) col = Chrome(Mathf.Abs(X - cx) / 8f);
            }
            if (X > 120f && X < 256f && Y > 113f && Y < 119f) col = Chrome(Y - 113f);
            // windows
            if (In(glassPoly, X, Y))
            {
                float u = Mathf.Clamp01(((X - 116f) / 110f + (Y - 45f) / 24f) * .5f);
                col = Color.Lerp(C(0x5b7b8c), C(0x070d12), u);
                if (Mathf.Abs(X - 162f) < 1.75f) col = C(0x000000);
                bool edge = !In(glassPoly, X + 1f, Y) || !In(glassPoly, X - 1f, Y) || !In(glassPoly, X, Y + 1f) || !In(glassPoly, X, Y - 1f);
                if (edge) col = Chrome(2f);
                foreach (float hx in new[] { 140f, 188f })
                {
                    bool Ell(float cx, float cy, float rx, float ry) { float a = (X - cx) / rx, b = (Y - cy) / ry; return a * a + b * b <= 1f; }
                    if (Ell(hx, 60f, 5f, 5f) || Ell(hx, 54.5f, 10f, 2.2f) || (Y < 54.5f && Ell(hx, 54.5f, 5f, 7f)) || (Y > 61f && Ell(hx, 74f, 12f, 8f))) col = C(0x04060a);
                }
                if (Circ(X, Y, 195.5f, 62.5f) < 1.4f) col = C(0xff8a2a);
            }
            // front: radiator, headlight, spare wheel, bumpers
            if (X > 340f && X < 350f && Y > 80f && Y < 112f) { col = Chrome((Y - 80f) / 5f); if ((X - 340f) % 3f < .8f) col = Over(C(0x000000, .5f), col); }
            float hl = Circ(X, Y, 326f, 82f);
            if (hl < 14f) col = Over(C(0xffe9a0, .22f * (1f - hl / 14f)), col);
            if (hl < 11f) col = Chrome(Mathf.Abs(Y - 82f) / 3f);
            if (hl < 8f)
            {
                float k = Mathf.Clamp01(Circ(X, Y, 322.4f, 80.4f) / 8f);
                col = k < .5f ? Color.Lerp(C(0xffffe8), C(0xffe08a), k / .5f) : Color.Lerp(C(0xffe08a), C(0xb8832a), (k - .5f) / .5f);
            }
            float sp = Circ(X, Y, 246f, 98f);
            if (sp < 17f) col = C(0x0a0a0b);
            if (sp < 14f) col = C(0xebe5d2);
            if (sp < 10f) col = Chrome(Mathf.Abs(Y - 98f) / 3f);
            if (sp < 3f) col = C(0x222222);
            if (X > 341f && X < 361f && Y > 111f && Y < 116f) col = Chrome(Y - 111f);
            if (X > 18f && X < 32f && Y > 111f && Y < 116f) col = Chrome(Y - 111f);
            return col;
        }

        public static void Generate()
        {
            InitCar();
            Make("car_body", 760, 340, (x, y) => CarPixel(x / 2f, y / 2f - 12f));

            Make("car_wheel", 100, 100, (x, y) =>
            {
                float r = Circ(x, y, 50f, 50f);
                if (r > 50f) return Color.clear;
                if (r > 44f) return C(0x0a0a0b);
                if (r > 34f) return C(0xebe5d2);
                Color col = C(0x0a0a0b);
                if (r > 28f && r < 31f) col = Chrome(Mathf.Abs(y - 50f) / 8f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 30f * Mathf.Deg2Rad;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 28f;
                    if (Seg(x, y, new Vector2(50, 50) - d, new Vector2(50, 50) + d) < 0.9f) col = C(0xcfd3d6);
                }
                if (r < 12f) col = Chrome(Mathf.Abs(y - 50f) / 2f);
                if (r < 4.4f) col = C(0x222222);
                return col;
            });

            Make("car_beam", 700, 300, (x, y) =>
            {
                float u = x / 700f;
                float halfH = Mathf.Lerp(2f, 120f, u);
                float d = Mathf.Abs(y - 150f);
                if (d > halfH) return Color.clear;
                float edge = Mathf.Clamp01((halfH - d) / 3f);
                return C(0xfff0b0, .85f * (1f - u) * edge);
            });

            Make("puff", 64, 64, (x, y) =>
            {
                float r = Circ(x, y, 32f, 32f) / 32f;
                return new Color(0.82f, 0.8f, 0.78f, Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f));
            });

            Make("moon", 160, 160, (x, y) =>
            {
                float r = Circ(x, y, 80f, 80f);
                if (r < 46f)
                {
                    Color c = Color.Lerp(C(0xfbf3d6), C(0xa89560), Mathf.Clamp01(Circ(x, y, 66f, 64f) / 56f));
                    if (Circ(x, y, 90f, 70f) < 9f || Circ(x, y, 66f, 96f) < 6f || Circ(x, y, 100f, 98f) < 7f) c = Color.Lerp(c, C(0x8a7a50), .35f);
                    return c;
                }
                return C(0xf5e2a8, .32f * Mathf.Pow(Mathf.Clamp01(1f - (r - 46f) / 34f), 2f));
            });

            Make("sky_grad", 8, 256, (x, y) =>
            {
                float t = y / 256f;
                Color[] st = { C(0x05050a), C(0x0e0c18), C(0x1c1218), C(0x0b0807) };
                float p = Mathf.Clamp01(t) * 3f; int k = Mathf.Min(2, (int)p);
                return Color.Lerp(st[k], st[k + 1], p - k);
            });

            MakeSkyline("skyline_far", 1024, 300, C(0x15111c, .62f), false, 7);
            MakeSkyline("skyline_near", 1024, 300, C(0x09070c), true, 21);

            Make("road_dash", 130, 12, (x, y) => (x % 130f) < 60f ? C(0xc9bfa0, .85f) : Color.clear);
            Make("curb_tile", 80, 24, (x, y) => (x % 80f) < 40f ? C(0x3a343a) : C(0x1c181c));
            AssetDatabase.Refresh();
        }

        private static void MakeSkyline(string name, int w, int h, Color col, bool windows, int seed)
        {
            var rnd = new System.Random(seed);
            var xs = new List<int>(); var hs = new List<int>(); var wsz = new List<int>();
            int x0 = 0;
            while (x0 < w)
            {
                int bw = Mathf.Min(w - x0, 44 + rnd.Next(70)); if (w - x0 - bw < 40) bw = w - x0;
                xs.Add(x0); wsz.Add(bw); hs.Add(60 + rnd.Next(windows ? 190 : 130)); x0 += bw + 2;
            }
            Make(name, w, h, (x, y) =>
            {
                for (int i = 0; i < xs.Count; i++)
                {
                    if (x < xs[i] || x >= xs[i] + wsz[i]) continue;
                    float top = h - hs[i];
                    if (y < top) return Color.clear;
                    if (windows)
                    {
                        int cx = ((int)x - xs[i] - 8) / 16, cy = ((int)y - (int)top - 14) / 24;
                        float fx = (x - xs[i] - 8) % 16f, fy = (y - top - 14) % 24f;
                        if (cx >= 0 && cy >= 0 && fx < 8f && fy < 11f && x < xs[i] + wsz[i] - 8 && Hash(cx + xs[i], cy * 7 + i) < .2f)
                            return C(0xffd98a, .4f + Hash(cy + i, cx) * .5f);
                    }
                    return col;
                }
                return Color.clear;
            });
        }
    }
}
