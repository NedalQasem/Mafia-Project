using UnityEngine;
using static MafiaNight.UI.Kit.Editor.KitProceduralTextures;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Store category tab icons (HTML symbols k-rank, k-frame, k-room, ...) drawn from simple shapes in a 48 x 48 design space, plus the tab plates.</summary>
    public static class KitProceduralTextures8
    {
        private delegate bool In(float x, float y);
        private delegate Color Fill(float x, float y);

        private sealed class Shape
        {
            public In inside; public Fill fill; public bool outline;
        }

        private static Color Gold(float y) { float t = Mathf.Clamp01(y / 48f); return t < .45f ? Color.Lerp(C(0xfff4cc), C(0xd2ad60), t / .45f) : Color.Lerp(C(0xd2ad60), C(0x6a4c1c), (t - .45f) / .55f); }
        private static Color Copper(float y) { float t = Mathf.Clamp01(y / 48f); return Color.Lerp(C(0xe8a878), C(0x7a3c18), t); }
        private static Color Red(float y) { float t = Mathf.Clamp01(y / 48f); return Color.Lerp(C(0xe0535c), C(0x5c0f16), t); }
        private static Fill Flat(Color c) => (x, y) => c;

        private static In Circle(float cx, float cy, float r) => (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
        private static In Ellipse(float cx, float cy, float rx, float ry) => (x, y) => { float a = (x - cx) / rx, b = (y - cy) / ry; return a * a + b * b <= 1f; };
        private static In Rect(float x0, float y0, float x1, float y1, float r = 0f) => (x, y) => RoundRectSdf(x, y, (x0 + x1) * .5f, (y0 + y1) * .5f, (x1 - x0) * .5f, (y1 - y0) * .5f, r) <= 0f;
        private static In Poly(params float[] p)
        {
            var v = new Vector2[p.Length / 2]; for (int i = 0; i < v.Length; i++) v[i] = new Vector2(p[i * 2], p[i * 2 + 1]);
            return (x, y) => InPoly(x, y, v);
        }
        private static In RotRect(float x0, float y0, float x1, float y1, float deg, float px, float py, float r)
        {
            float a = -deg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            var box = Rect(x0, y0, x1, y1, r);
            return (x, y) => { float dx = x - px, dy = y - py; return box(px + dx * ca - dy * sa, py + dx * sa + dy * ca); };
        }
        private static In Line(float ax, float ay, float bx, float by, float w) => (x, y) =>
        {
            var p = new Vector2(x, y); var a = new Vector2(ax, ay); var ab = new Vector2(bx, by) - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude <= w * .5f;
        };
        private static In Ring(float cx, float cy, float r, float w) => (x, y) => { float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)); return Mathf.Abs(d - r) <= w * .5f; };
        private static float[] Star(float cx, float cy, float ro, float ri)
        {
            var p = new float[20];
            for (int i = 0; i < 10; i++) { float a = -Mathf.PI / 2f + i * Mathf.PI / 5f, r = i % 2 == 0 ? ro : ri; p[i * 2] = cx + Mathf.Cos(a) * r; p[i * 2 + 1] = cy + Mathf.Sin(a) * r; }
            return p;
        }

        private static Shape S(In inside, Fill fill, bool outline = false) => new Shape { inside = inside, fill = fill, outline = outline };

        private static void Icon(string name, params Shape[] shapes)
        {
            Make(name, 96, 96, (px, py) =>
            {
                float x = px / 2f, y = py / 2f;
                Color col = Color.clear;
                foreach (var s in shapes)
                {
                    if (!s.inside(x, y)) continue;
                    col = s.fill(x, y);
                    if (s.outline && (!s.inside(x + .7f, y) || !s.inside(x - .7f, y) || !s.inside(x, y + .7f) || !s.inside(x, y - .7f))) col = C(0x1a1006);
                }
                return col;
            });
        }

        public static void Generate()
        {
            var dark = Flat(C(0x1a110d));
            Icon("cat_rank",
                S(Poly(24, 3, 39, 8, 39, 22, 36, 32, 24, 43, 12, 32, 9, 22, 9, 8), (x, y) => Gold(y), true),
                S(Poly(24, 8, 34, 11.4f, 34, 21.8f, 31, 30, 24, 37.2f, 17, 30, 14, 21.8f, 14, 11.4f), Flat(C(0x1a0f0a))),
                S(Poly(Star(24, 24, 8f, 3.4f)), (x, y) => Gold(y)));
            Icon("cat_frame",
                S(Circle(24, 24, 20), (x, y) => Gold(y), true),
                S(Circle(24, 24, 13), Flat(C(0x150d08)), true),
                S(Circle(24, 21, 5), Flat(C(0x8f8573))),
                S(Poly(14, 36, 16, 31, 24, 27, 32, 31, 34, 36), Flat(C(0x8f8573))));
            Icon("cat_room",
                S(Rect(5, 8, 43, 40, 4), (x, y) => Gold(y), true),
                S(Rect(9, 12, 39, 36, 2), dark),
                S(Poly(20, 12, 28, 12, 31, 24, 17, 24), Flat(C(0xf5e2a8, .35f))),
                S(Ellipse(24, 30, 12, 5.5f), Flat(C(0xb99048))),
                S(Ellipse(24, 30, 10.8f, 4.4f), Flat(C(0x0f3a26))));
            Icon("cat_bg",
                S(Rect(6, 8, 42, 40, 4), (x, y) => Gold(y), true),
                S(Rect(10, 12, 38, 36, 2), dark),
                S(Poly(10, 30, 18, 21, 25, 28, 30, 24, 38, 32, 38, 36, 10, 36), (x, y) => Copper(y)),
                S(Circle(31, 19, 3.5f), Flat(C(0xfff0b8))));
            Icon("cat_bubble",
                S(Poly(8, 10, 40, 10, 43, 13, 43, 30, 40, 33, 25, 33, 16, 41, 16, 33, 8, 33, 5, 30, 5, 13), (x, y) => Copper(y), true),
                S(Line(12, 19, 36, 19, 2.2f), Flat(C(0xfff0b8))),
                S(Line(12, 25, 27, 25, 2.2f), Flat(C(0xfff0b8))));
            Icon("cat_entry",
                S(Poly(10, 42, 10, 12, 12, 8, 15, 7, 33, 7, 36, 8, 38, 12, 38, 42), (x, y) => Gold(y), true),
                S(Poly(14, 42, 14, 13, 34, 13, 34, 42), Flat(C(0x2a1a10))),
                S(Circle(24, 20, 4.2f), (x, y) => Gold(y)),
                S(Poly(17, 42, 18, 36, 24, 32, 30, 36, 31, 42), (x, y) => Gold(y)),
                S(Circle(31, 27, 1.4f), Flat(C(0x1a0f08))));
            Icon("cat_gift",
                S(Rect(8, 20, 40, 40, 2), (x, y) => Red(y), true),
                S(Rect(6, 14, 42, 22, 2), (x, y) => Red(y), true),
                S(Rect(21, 14, 27, 40), (x, y) => Gold(y)),
                S(Ellipse(19, 11.5f, 6.5f, 4f), (x, y) => Gold(y), true),
                S(Ellipse(29, 11.5f, 6.5f, 4f), (x, y) => Gold(y), true),
                S(Circle(24, 13, 2.4f), (x, y) => Gold(y)));
            Icon("cat_table",
                S(RotRect(12, 9, 29, 34, -12f, 20, 21, 2.5f), Flat(C(0xf0e8d2)), true),
                S(RotRect(19, 12, 36, 37, 10f, 27, 24, 2.5f), (x, y) => Red(y), true),
                S(Poly(Star(31, 24, 5f, 2.2f)), (x, y) => Gold(y)));
            Icon("cat_title",
                S(Rect(5, 14, 43, 34, 3), (x, y) => Gold(y), true),
                S(Circle(10, 24, 1.8f), Flat(C(0x3a2a0c))),
                S(Circle(38, 24, 1.8f), Flat(C(0x3a2a0c))),
                S(Line(15, 24, 33, 24, 2f), Flat(C(0x4a3510))),
                S(Line(18, 29, 30, 29, 2f), Flat(C(0x4a3510))));
            Icon("cat_coin",
                S(Circle(24, 30, 14), (x, y) => Gold(y), true),
                S(Circle(24, 22, 14), (x, y) => Gold(y), true),
                S(Ring(24, 22, 9, 1.5f), Flat(C(0x6e4b1c))));

            // tab plates (HTML .cat / .cat.on): 8px top corners, black 1px outline
            Plate("cat_off", C(0x2a1e17), C(0x140d0a), C(0x000000), false);
            Plate("cat_on", C(0xf5e2a8), C(0x8a6a30), C(0x000000), true);
            UnityEditor.AssetDatabase.Refresh();
        }

        private static void Plate(string name, Color top, Color bottom, Color edge, bool gold)
        {
            Make(name, 128, 128, (x, y) =>
            {
                float cx = x < 24f ? 24f : (x > 104f ? 104f : x);
                if (y < 24f && (x - cx) * (x - cx) + (y - 24f) * (y - 24f) > 576f) return Color.clear;
                float ex = Mathf.Min(x, 128f - x), ey = Mathf.Min(y, 128f - y);
                Color c;
                if (gold) { float t = y / 128f; c = t < .6f ? Color.Lerp(C(0xf5e2a8), C(0xc9a050), t / .6f) : Color.Lerp(C(0xc9a050), C(0x8a6a30), (t - .6f) / .4f); }
                else { c = Color.Lerp(top, bottom, y / 128f); float g = (Hash((int)x, (int)y) - .5f) * .05f; c = new Color(c.r + g, c.g + g * .8f, c.b + g * .6f, 1f); }
                if (Mathf.Min(ex, ey) < 2.6f) return edge;
                if (y < 6f) c = Over(C(0xffffff, gold ? .6f : .08f), c);
                return c;
            });
        }
    }
}
