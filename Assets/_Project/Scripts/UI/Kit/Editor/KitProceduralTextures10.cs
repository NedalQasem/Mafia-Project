using UnityEngine;
using static MafiaNight.UI.Kit.Editor.KitProceduralTextures;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// Create-room type cards from Docs/mockups/create-room.html (.ty): the flat brass plate with grain when chosen,
    /// the dark recessed groove when not, and the four line icons (globe, key, crown, lock) drawn white so they can be tinted.
    /// </summary>
    public static class KitProceduralTextures10
    {
        private static float Seg(float x, float y, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(x, y); var a = new Vector2(ax, ay); var ab = new Vector2(bx, by) - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        private static float RingD(float x, float y, float cx, float cy, float r) => Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r);

        private static float EllipseD(float x, float y, float cx, float cy, float a, float b)
        {
            float k = Mathf.Sqrt((x - cx) * (x - cx) / (a * a) + (y - cy) * (y - cy) / (b * b));
            return Mathf.Abs(k - 1f) * Mathf.Min(a, b);
        }

        private static Color White(float d, float w) => d <= w * 0.5f ? Color.white : Color.clear;

        public static void Generate()
        {
            // chosen: flat brass plate (gradient f0d896 -> c9a050 -> 8a6a30) with grain, black outline and a hard drop edge below
            Make("type_on", 240, 240, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 120f, 112f, 112f, 104f, 24f);
                float dShadow = RoundRectSdf(x, y, 120f, 120f, 112f, 104f, 24f);   // the 3 px black edge under the plate (box-shadow 0 3px 0)
                if (d > 4f) return dShadow <= 4f ? C(0x000000) : Color.clear;
                if (d > 0f) return C(0x000000);
                float t = Mathf.Clamp01((y - 8f) / 208f);
                Color top = C(0xf0d896), mid = C(0xc9a050), bot = C(0x8a6a30);
                Color c = t < 0.6f ? Color.Lerp(top, mid, t / 0.6f) : Color.Lerp(mid, bot, (t - 0.6f) / 0.4f);
                float grain = (Hash((int)x, (int)y) - 0.5f) * 0.10f;
                c = new Color(c.r + grain, c.g + grain, c.b + grain, 1f);
                if (d > -2.5f && y < 120f) c = Color.Lerp(c, Color.white, 0.55f);      // inner top highlight
                return c;
            });

            // not chosen: dark groove with an inner shadow and a thin brown rim
            Make("type_off", 240, 240, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 120f, 120f, 114f, 114f, 24f);
                if (d > 0f) return Color.clear;
                if (d > -4f) return C(0x3a2e22);
                float shade = Mathf.Clamp01(1f - (y - 8f) / 40f) * 0.55f;           // inset shadow along the top
                float grain = (Hash((int)x, (int)y) - 0.5f) * 0.03f;
                float v = Mathf.Clamp01(0.03f + grain - shade * 0.03f);
                return new Color(v + 0.015f, v + 0.01f, v, 0.94f);
            });

            // segmented control (HTML .seg): dark groove with an inset shadow, and the flat brass chosen plate with a black edge under it
            Make("seg_bar", 240, 240, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 120f, 120f, 120f, 120f, 30f);
                if (d > 0f) return Color.clear;
                float shade = Mathf.Clamp01(1f - (y - 4f) / 50f) * 0.5f;
                float v = 0.012f - shade * 0.012f;
                return new Color(v + 0.012f, v + 0.008f, v + 0.004f, 0.93f);
            });
            Make("seg_on", 240, 240, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 120f, 112f, 118f, 106f, 24f);
                float dEdge = RoundRectSdf(x, y, 120f, 118f, 118f, 106f, 24f);   // 2 px black edge below (box-shadow 0 2px 0)
                if (d > 0f) return dEdge <= 0f ? C(0x000000) : Color.clear;
                float t = Mathf.Clamp01((y - 6f) / 212f);
                Color top = C(0xf5e2a8), mid = C(0xc9a050), bot = C(0x8a6a30);
                Color c = t < 0.6f ? Color.Lerp(top, mid, t / 0.6f) : Color.Lerp(mid, bot, (t - 0.6f) / 0.4f);
                if (d > -2.5f && y < 120f) c = Color.Lerp(c, Color.white, 0.6f);   // inner top highlight
                return c;
            });

            const float W = 5.2f;
            Make("icon_globe", 96, 96, (x, y) =>
            {
                float d = RingD(x, y, 48f, 48f, 36f);
                d = Mathf.Min(d, Seg(x, y, 12f, 48f, 84f, 48f));
                d = Mathf.Min(d, EllipseD(x, y, 48f, 48f, 15f, 36f));
                return White(d, W);
            });

            Make("icon_key", 96, 96, (x, y) =>
            {
                float d = RingD(x, y, 32f, 62f, 15f);
                d = Mathf.Min(d, Seg(x, y, 44f, 50f, 82f, 12f));
                d = Mathf.Min(d, Seg(x, y, 66f, 28f, 78f, 40f));
                d = Mathf.Min(d, Seg(x, y, 56f, 38f, 64f, 46f));
                return White(d, W);
            });

            Make("icon_crown", 96, 96, (x, y) =>
            {
                Vector2[] p = { new Vector2(12, 32), new Vector2(30, 48), new Vector2(48, 20), new Vector2(66, 48), new Vector2(84, 32), new Vector2(76, 76), new Vector2(20, 76) };
                float d = 999f;
                for (int i = 0; i < p.Length; i++) { var a = p[i]; var b = p[(i + 1) % p.Length]; d = Mathf.Min(d, Seg(x, y, a.x, a.y, b.x, b.y)); }
                return White(d, W);
            });

            Make("icon_whatsapp", 96, 96, (x, y) =>
            {
                float d = RingD(x, y, 50f, 46f, 34f);
                if (y > 56f && x < 36f) d = 999f;                                     // gap where the tail leaves the bubble
                d = Mathf.Min(d, Seg(x, y, 20f, 66f, 14f, 84f));
                d = Mathf.Min(d, Seg(x, y, 14f, 84f, 33f, 77f));
                d = Mathf.Min(d, Seg(x, y, 20f, 66f, 23f, 70f));
                float h = Seg(x, y, 36f, 34f, 38f, 46f);                              // the handset
                h = Mathf.Min(h, Seg(x, y, 38f, 46f, 52f, 60f));
                h = Mathf.Min(h, Seg(x, y, 52f, 60f, 64f, 62f));
                return White(Mathf.Min(d, h - 2f), W);
            });

            Make("icon_link", 96, 96, (x, y) =>
            {
                float a = Mathf.Abs(Seg(x, y, 52f, 44f, 68f, 28f) - 11f);
                float b = Mathf.Abs(Seg(x, y, 28f, 68f, 44f, 52f) - 11f);
                return White(Mathf.Min(a, b), W);
            });

            Make("icon_copy", 96, 96, (x, y) =>
            {
                float front = RoundRectSdf(x, y, 56f, 56f, 24f, 24f, 8f);
                if (front <= 0f) return White(Mathf.Abs(front), W);                    // inside the front sheet only its own outline is drawn
                float back = Mathf.Abs(RoundRectSdf(x, y, 40f, 40f, 24f, 24f, 8f));
                return White(Mathf.Min(back, Mathf.Abs(front)), W);
            });

            Make("icon_lock", 96, 96, (x, y) =>
            {
                float body = Mathf.Abs(RoundRectSdf(x, y, 48f, 62f, 28f, 18f, 7f));
                float d = body;
                d = Mathf.Min(d, Seg(x, y, 32f, 44f, 32f, 32f));
                d = Mathf.Min(d, Seg(x, y, 64f, 44f, 64f, 32f));
                if (y <= 32f) d = Mathf.Min(d, RingD(x, y, 48f, 32f, 16f));
                return White(d, W);
            });
        }
    }
}
