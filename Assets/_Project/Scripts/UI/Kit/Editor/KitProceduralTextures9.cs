using UnityEngine;
using static MafiaNight.UI.Kit.Editor.KitProceduralTextures;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// The six store frames, drawn from the mockup's CSS (store.html .f1 .. .f6): conic gradients, a rope pattern, a barbed-wire dash ring,
    /// plus the glows. Texture is 320 px; the ring itself is 256 px outer / 215 px inner (HTML: 76 px circle, 6 px padding), the rest is shadow and glow.
    /// </summary>
    public static class KitProceduralTextures9
    {
        private const int Size = 320;
        private const float Cx = 160f, Cy = 160f, ROut = 128f, RIn = 107.8f;

        private static Color Stops(float t, Color[] c)
        {
            t = Mathf.Repeat(t, 1f) * (c.Length - 1);
            int i = Mathf.Min((int)t, c.Length - 2);
            return Color.Lerp(c[i], c[i + 1], t - i);
        }

        private static float Angle01(float x, float y, float fromDeg)
        {
            float a = Mathf.Atan2(x - Cx, -(y - Cy)) * Mathf.Rad2Deg;   // 0 = up, clockwise
            return Mathf.Repeat((a - fromDeg) / 360f, 1f);
        }

        private delegate Color RingColor(float x, float y);

        private static void Frame(string name, RingColor ring, Color glow, float glowRadius, float outline)
        {
            Make(name, Size, Size, (x, y) =>
            {
                float r = Mathf.Sqrt((x - Cx) * (x - Cx) + (y - Cy) * (y - Cy));
                Color col = Color.clear;
                // glow outside the ring
                if (glow.a > 0f && r > ROut - 4f)
                {
                    float g = Mathf.Clamp01(1f - (r - ROut) / glowRadius);
                    col = new Color(glow.r, glow.g, glow.b, glow.a * g * g);
                }
                // drop shadow: 0 6px 10px black .8
                float rs = Mathf.Sqrt((x - Cx) * (x - Cx) + (y - Cy - 20f) * (y - Cy - 20f));
                float sh = Mathf.Clamp01(1f - (rs - ROut + 16f) / 40f) * .8f;
                if (r > ROut - 2f && sh > 0f) col = Over(col, new Color(0, 0, 0, sh));
                if (outline > 0f && r > ROut && r <= ROut + outline) return C(0x000000);
                if (r <= ROut && r >= RIn)
                {
                    Color c = ring(x, y);
                    float edge = Mathf.Min(ROut - r, r - RIn);
                    if (edge < 1f) c = Color.Lerp(col, c, Mathf.Clamp01(edge));   // soft anti-aliased edges
                    return c;
                }
                return col;
            });
        }

        public static void Generate()
        {
            Color[] f1 = { C(0xf5e2a8), C(0x8a6a30), C(0xd2ad60), C(0x6a4c1c), C(0xf5e2a8) };
            Frame("frame_f1", (x, y) => Stops(Angle01(x, y, 30f), f1), Color.clear, 0f, 0f);

            // barbed wire: alternating dashes every 10 degrees
            Frame("frame_f2", (x, y) => Mathf.Repeat(Angle01(x, y, 0f) * 36f, 2f) < 1f ? C(0x3a3d40) : C(0xa9afb5), Color.clear, 0f, 6.5f);

            // rope: 45 degree stripes 4 px / 4 px in the 76 px design (13.5 px in the texture)
            Frame("frame_f3", (x, y) => Mathf.Repeat((x + y) * 0.7071f, 27f) < 13.5f ? C(0xc9a56a) : C(0x6a4a22), Color.clear, 0f, 0f);

            Color[] f4 = { C(0xf5e2a8), C(0xb99048), C(0xfff6d8), C(0x8a6a30), C(0xf5e2a8) };
            Frame("frame_f4", (x, y) => Stops(Angle01(x, y, 0f), f4), C(0xf5d070, .53f), 47f, 0f);

            Color[] f5 = { C(0xff8a2a), C(0xc9323a), C(0x5c0f16), C(0xc9323a), C(0xff8a2a) };
            Frame("frame_f5", (x, y) => Stops(Angle01(x, y, 0f), f5), C(0xff6a1a, .53f), 60f, 0f);

            Color[] f6 = { C(0x8fe0c6), C(0x2d7a60), C(0xd6fff0), C(0x2d7a60), C(0x8fe0c6) };
            Frame("frame_f6", (x, y) => Stops(Angle01(x, y, 0f), f6), C(0x4fa58b, .53f), 54f, 0f);
            UnityEditor.AssetDatabase.Refresh();
        }
    }
}
