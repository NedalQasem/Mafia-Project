using UnityEditor;
using UnityEngine;
using static MafiaNight.UI.Kit.Editor.KitProceduralTextures;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Store textures: shelf plank, price tag, thumbnails, card backs, gift icons.</summary>
    public static class KitProceduralTextures3
    {
        private static float Circ(float x, float y, float cx, float cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        private static float Seg(float x, float y, Vector2 a, Vector2 b)
        {
            var p = new Vector2(x, y); var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        public static void Generate()
        {
            Make("plank", 384, 64, (x, y) =>
            {
                if (y >= 56f) return new Color(0, 0, 0, .55f * (1f - (y - 56f) / 8f));
                Color c = y < 6f ? Color.Lerp(C(0x8a6040), C(0x5a3a24), y / 6f) : Color.Lerp(C(0x5a3a24), C(0x1c0f07), (y - 6f) / 50f);
                float grain = Mathf.Sin(x * .045f + Mathf.Sin(y * .3f) * 1.4f) * .5f + .5f;
                c = Color.Lerp(c, C(0x7a4e2c), grain * .18f * (y > 6f ? 1f : 0f));
                if (y < 1.5f) c = Over(C(0xffffff, .28f), c);
                if (y > 6f && y < 8f) c = Over(C(0x000000, .35f), c);
                return c;
            });

            Make("price_tag", 160, 72, (x, y) =>
            {
                float shadow = RoundRectSdf(x, y - 4f, 80f, 34f, 78f, 32f, 10f);
                Color col = shadow <= 0f ? new Color(0, 0, 0, .45f) : Color.clear;
                float d = RoundRectSdf(x, y, 80f, 34f, 78f, 32f, 10f);
                if (d <= 0f)
                {
                    float n = (Hash((int)x, (int)y) - .5f) * .05f;
                    col = Color.Lerp(C(0xe4dabf), C(0xcbc09f), Mathf.Clamp01(y / 66f));
                    col = new Color(col.r + n, col.g + n, col.b + n, 1f);
                    if (d > -2f) col = Over(C(0x6a5a3a, .5f), col);
                    float h = Circ(x, y, 136f, 34f);
                    if (h < 8f) col = h > 5.5f ? C(0x6a5a3a) : C(0x0d0a09);
                }
                return col;
            });

            Make("thumb_fill", 120, 150, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 60f, 75f, 54f, 69f, 10f);
                if (d > 0f) return Color.clear;
                float t = Mathf.Clamp01(y / 150f);
                return new Color(1f - t * .5f, 1f - t * .5f, 1f - t * .5f, 1f);
            });

            Make("thumb_border", 120, 150, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 60f, 75f, 58f, 73f, 14f);
                if (d > 0f) return Color.clear;
                if (d > -4f) return Color.Lerp(C(0xf5e2a8), C(0x8a6a30), Mathf.Clamp01(y / 150f));
                if (d > -6f) return C(0x000000);
                return Color.clear;
            });

            Make("card_back", 90, 126, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 45f, 63f, 43f, 61f, 9f);
                if (d > 0f) return Color.clear;
                if (d > -1.5f) return new Color(.25f, .25f, .25f, 1f);
                if (d > -6f) return Color.white;
                float lx = x + y, ly = x - y;
                bool line = Mathf.Abs(((lx % 14f) + 14f) % 14f) < 1.4f || Mathf.Abs(((ly % 14f) + 14f) % 14f) < 1.4f;
                return line ? new Color(.62f, .62f, .62f, 1f) : new Color(.92f, .92f, .92f, 1f);
            });

            Make("glow_floor", 200, 50, (x, y) =>
            {
                float e = Mathf.Sqrt(Mathf.Pow((x - 100f) / 100f, 2) + Mathf.Pow((y - 25f) / 25f, 2));
                return new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - e), 1.4f));
            });

            // ---- gift icons (128 x 128)
            Make("gift_watch", 128, 128, (x, y) =>
            {
                Color col = Color.clear;
                if (Mathf.Abs(Circ(x, y, 64f, 18f) - 11f) < 3.5f) col = C(0xd2ad60);
                if (x > 57f && x < 71f && y > 26f && y < 38f) col = C(0xb99048);
                float r = Circ(x, y, 64f, 80f);
                if (r < 44f) col = Color.Lerp(C(0xfff0b8), C(0x7a5a22), Mathf.Clamp01((y - 36f) / 88f));
                if (r < 36f) col = C(0xefe6cf);
                if (r < 36f)
                {
                    foreach (var tk in new[] { new Vector2(64, 50), new Vector2(64, 110), new Vector2(34, 80), new Vector2(94, 80) })
                        if (Circ(x, y, tk.x, tk.y) < 2.8f) col = C(0x2a2217);
                    if (Seg(x, y, new Vector2(64, 80), new Vector2(64, 56)) < 2.2f) col = C(0x2a2217);
                    if (Seg(x, y, new Vector2(64, 80), new Vector2(82, 90)) < 2.8f) col = C(0x2a2217);
                    if (Circ(x, y, 64f, 80f) < 4f) col = C(0x8a1c24);
                }
                if (r < 44f && r > 36f && x < 50f && y < 70f) col = Over(C(0xffffff, .35f), col);
                return col;
            });

            Make("gift_key", 128, 128, (x, y) =>
            {
                Color col = Color.clear;
                Color Steel(float xx) => Color.Lerp(C(0xf4f6f7), C(0x6a6d70), Mathf.Clamp01(Mathf.Abs(xx - 40f) / 90f));
                float r = Circ(x, y, 38f, 64f);
                if (r < 28f && r > 13f) col = Steel(x);
                foreach (var d in new[] { new Vector2(38, 30), new Vector2(10, 64), new Vector2(66, 64) }) if (Circ(x, y, d.x, d.y) < 7f) col = Steel(x);
                if (x > 62f && x < 118f && y > 58f && y < 70f) col = Steel(x);
                if (x > 96f && x < 105f && y >= 70f && y < 90f) col = Steel(x);
                if (x > 108f && x < 115f && y >= 70f && y < 82f) col = Steel(x);
                return col;
            });

            Make("gift_rose", 128, 128, (x, y) =>
            {
                Color col = Color.clear;
                if (Seg(x, y, new Vector2(64, 88), new Vector2(64, 126)) < 3f) col = C(0x2a6a3a);
                if (Mathf.Pow((x - 82f) / 16f, 2) + Mathf.Pow((y - 108f) / 8f, 2) < 1f) col = C(0x2f7a42);
                if (Mathf.Pow((x - 46f) / 16f, 2) + Mathf.Pow((y - 100f) / 8f, 2) < 1f) col = C(0x2f7a42);
                float r = Circ(x, y, 64f, 50f);
                if (r < 42f)
                {
                    col = Color.Lerp(C(0xe85a64), C(0x4a0c12), r / 42f);
                    float ang = Mathf.Atan2(y - 50f, x - 64f);
                    if (Mathf.Abs(Mathf.Sin(r * .26f + ang * 1.6f)) < .12f) col = Over(C(0x2a0508, .65f), col);
                    if (r > 39f) col = Over(C(0x000000, .5f), col);
                }
                return col;
            });

            var crown = new[] { new Vector2(22, 92), new Vector2(14, 40), new Vector2(42, 64), new Vector2(64, 26), new Vector2(86, 64), new Vector2(114, 40), new Vector2(106, 92) };
            Make("gift_crown", 128, 128, (x, y) =>
            {
                Color col = Color.clear;
                Color Gold(float yy) => Color.Lerp(C(0xfff6d0), C(0x8a5a1c), Mathf.Clamp01((yy - 20f) / 90f));
                if (InPoly(x, y, crown)) col = Gold(y);
                if (x > 20f && x < 108f && y >= 92f && y < 110f) col = Gold(y + 10f);
                foreach (var o in new[] { new Vector2(14, 36), new Vector2(64, 22), new Vector2(114, 36) }) if (Circ(x, y, o.x, o.y) < 7f) col = Gold(y);
                if (Circ(x, y, 40f, 101f) < 5f) col = C(0xc9424c);
                if (Circ(x, y, 64f, 101f) < 6f) col = C(0x2a8a6a);
                if (Circ(x, y, 88f, 101f) < 5f) col = C(0xc9424c);
                return col;
            });

            AssetDatabase.Refresh();
        }
    }
}
