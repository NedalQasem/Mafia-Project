using UnityEditor;
using UnityEngine;
using static MafiaNight.UI.Kit.Editor.KitProceduralTextures;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Leaderboards / chat / settings textures.</summary>
    public static class KitProceduralTextures4
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
            // chalkboard in a wooden frame (9-slice 384x160)
            Make("chalkboard", 384, 160, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 192f, 74f, 184f, 68f, 26f);
                Color col = RoundRectSdf(x, y - 8f, 192f, 74f, 184f, 68f, 26f) <= 0f ? new Color(0, 0, 0, .6f) : Color.clear;
                if (d > 0f) return col;
                float inner = RoundRectSdf(x, y, 192f, 74f, 168f, 52f, 10f);
                if (inner > 0f)
                {
                    float grain = Mathf.Sin(x * .05f + Mathf.Sin(y * .22f) * 1.3f) * .5f + .5f;
                    Color wood = Color.Lerp(C(0x4a2c18), C(0x7a4c2a), grain * .5f);
                    wood = Color.Lerp(wood, C(0x1c0f08), Mathf.Clamp01((y - 6f) / 136f) * .45f);
                    if (d > -2f) wood = C(0x000000);
                    if (inner < 3f) wood = Over(C(0x000000, .5f), wood);
                    return wood;
                }
                float n = (Hash((int)x * 2, (int)y * 2) - .5f) * .05f;
                Color c = Color.Lerp(C(0x1f2a25), C(0x0f1613), Mathf.Clamp01((y - 20f) / 110f));
                float dust = Hash((int)(x * .37f), (int)(y * .41f)) > .985f ? .08f : 0f;
                return new Color(c.r + n + dust, c.g + n + dust, c.b + n + dust, 1f);
            });

            // red wallpaper tile (96 x 96, repeats both ways)
            Make("wall_tile", 96, 96, (x, y) =>
            {
                float stripe = (x % 24f) < 2f ? .35f : 0f;
                Color c = Color.Lerp(C(0x4a1a22), C(0x2a0c14), (y % 96f) / 96f * .4f);
                float diamond = Mathf.Abs((x % 48f) - 24f) + Mathf.Abs((y % 48f) - 24f);
                if (diamond < 5f) c = Color.Lerp(c, C(0x6a2a30), .5f);
                return new Color(c.r * (1f - stripe), c.g * (1f - stripe), c.b * (1f - stripe), 1f);
            });

            // ornate gold picture frame (9-slice 160 x 200, border 34)
            Make("gold_frame", 160, 200, (x, y) =>
            {
                float shadow = RoundRectSdf(x, y - 8f, 80f, 96f, 76f, 92f, 10f);
                Color col = shadow <= 0f ? new Color(0, 0, 0, .6f) : Color.clear;
                float d = RoundRectSdf(x, y, 80f, 96f, 76f, 92f, 10f);
                if (d > 0f) return col;
                float u = Mathf.Clamp01((x + y) / 360f);
                Color gold = Color.Lerp(Color.Lerp(C(0xfff0b8), C(0x8a6a30), u), C(0xd2ad60), Mathf.Abs(Mathf.Sin(u * 9f)) * .35f);
                if (d > -28f) { if (d > -2f) gold = C(0x2a1c08); return gold; }
                if (d > -31f) return C(0x000000);
                float t = Mathf.Clamp01((y - 30f) / 140f);
                Color pic = Color.Lerp(C(0x4a3a2c), C(0x14100c), t);
                float vig = Mathf.Clamp01(Circ(x, y, 80f, 90f) / 90f);
                pic = Color.Lerp(pic, Color.black, vig * vig * .55f);
                return pic;
            });

            Make("icon_gift", 128, 128, (x, y) =>
            {
                Color col = Color.clear;
                float box = RoundRectSdf(x, y, 64f, 78f, 40f, 30f, 4f);
                float lid = RoundRectSdf(x, y, 64f, 50f, 46f, 12f, 4f);
                if (box <= 0f) col = Color.Lerp(C(0xe0535c), C(0x5c0f16), Mathf.Clamp01((y - 48f) / 60f));
                if (lid <= 0f) col = Color.Lerp(C(0xf0646c), C(0x7a1620), Mathf.Clamp01((y - 38f) / 24f));
                if (Mathf.Abs(x - 64f) < 7f && y > 38f && y < 108f) col = Color.Lerp(C(0xfff0b8), C(0xb88a3a), Mathf.Clamp01((y - 38f) / 70f));
                float b1 = Circ(x, y, 46f, 30f), b2 = Circ(x, y, 82f, 30f);
                if (Mathf.Abs(b1 - 14f) < 4f || Mathf.Abs(b2 - 14f) < 4f) col = C(0xe8c878);
                return col;
            });

            Make("icon_send", 128, 128, (x, y) =>
            {
                var plane = new[] { new Vector2(112, 64), new Vector2(18, 20), new Vector2(40, 64), new Vector2(18, 108) };
                if (!InPoly(x, y, plane)) return Color.clear;
                float t = Mathf.Clamp01(x / 128f);
                return Color.Lerp(C(0x3a2410), C(0x1a1006), t);
            });

            Make("tri_up", 48, 48, (x, y) =>
            {
                var tri = new[] { new Vector2(24, 8), new Vector2(42, 38), new Vector2(6, 38) };
                return InPoly(x, y, tri) ? Color.white : Color.clear;
            });

            // chat input pill (paper) and round buttons are reused from existing sprites

            Make("knob_face", 208, 208, (x, y) =>
            {
                float dx = x - 104f, dy = y - 104f, r = Mathf.Sqrt(dx * dx + dy * dy);
                Color col = Circ(x, y, 104f, 112f) <= 82f ? new Color(0, 0, 0, .55f) : Color.clear;
                if (r <= 80f) col = Color.black;
                if (r <= 78f)
                {
                    float ang = Mathf.Atan2(dy, dx) + Mathf.PI;
                    bool tooth = ((int)(ang / (Mathf.PI * 2f / 56f))) % 2 == 0;
                    col = tooth ? C(0x3e3e3e) : C(0x161616);
                    col = Over(new Color(1, 1, 1, Mathf.Clamp01(-dy / 80f) * .08f), col);
                }
                if (r <= 58f)
                {
                    float d = Circ(x, y, 90f, 86f) / 70f;
                    col = Color.Lerp(C(0xfff0b8), C(0x6e4b1c), Mathf.Clamp01(d));
                    if (r > 54f) col = Over(new Color(0, 0, 0, .45f), col);
                }
                return col;
            });

            Make("dial_ticks", 208, 208, (x, y) =>
            {
                for (int i = 0; i < 11; i++)
                {
                    float a = (-135f + i * 27f) * Mathf.Deg2Rad;
                    var p0 = new Vector2(104f + Mathf.Sin(a) * 88f, 104f - Mathf.Cos(a) * 88f);
                    var p1 = new Vector2(104f + Mathf.Sin(a) * 100f, 104f - Mathf.Cos(a) * 100f);
                    if (Seg(x, y, p0, p1) < 1.8f) return C(0xd9d0bd, .75f);
                }
                return Color.clear;
            });

            AssetDatabase.Refresh();
        }
    }
}
