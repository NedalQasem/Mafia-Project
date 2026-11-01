using UnityEditor;
using UnityEngine;
using static MafiaNight.UI.Kit.Editor.KitProceduralTextures;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Textures for the rooms list and the voice-room table (steel/VIP cards, table, chairs, props, icons).</summary>
    public static class KitProceduralTextures2
    {
        private static float Seg(float x, float y, Vector2 a, Vector2 b)
        {
            var p = new Vector2(x, y); var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        private static float Circ(float x, float y, float cx, float cy) =>
            Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        // Shared rounded-card geometry (same footprint as leather_dark): 368 x 136 at (8, 6), radius 30.
        private static float CardSdf(float x, float y, float dy = 0f) => RoundRectSdf(x, y - dy, 192f, 74f, 184f, 68f, 30f);

        private static Color CardShadow(float x, float y)
        {
            float d = CardSdf(x, y, 8f);
            if (d <= 0f) return new Color(0, 0, 0, .65f);
            if (d < 14f) return new Color(0, 0, 0, .35f * (1f - d / 14f));
            return Color.clear;
        }

        public static void Generate()
        {
            // ---------------- steel plate (private rooms)
            Make("steel_plate", 384, 160, (x, y) =>
            {
                Color col = CardShadow(x, y);
                float d = CardSdf(x, y);
                if (d <= 0f)
                {
                    float t = Mathf.Clamp01((y - 6f) / 136f);
                    Color c = t < .5f ? Color.Lerp(C(0x4b5052), C(0x2c3032), t / .5f) : Color.Lerp(C(0x2c3032), C(0x1b1e1f), (t - .5f) / .5f);
                    float n = (Hash((int)x, (int)y) - .5f) * .07f; c = new Color(c.r + n, c.g + n, c.b + n, 1f);
                    if (d > -2.5f && y < 74f) c = Over(C(0xffffff, .28f), c);
                    if (d > -1.5f) c = C(0x000000);
                    foreach (var rv in new[] { new Vector2(30, 34), new Vector2(354, 34), new Vector2(30, 114), new Vector2(354, 114) })
                    {
                        float r = Circ(x, y, rv.x, rv.y);
                        if (r < 8f) c = r > 6.5f ? C(0x0a0a0a) : Color.Lerp(C(0xcfcfcf), C(0x2a2a2a), Mathf.Clamp01(Circ(x, y, rv.x - 2f, rv.y - 2f) / 7f));
                    }
                    col = c;
                }
                return col;
            });

            // ---------------- VIP frame: crimson velvet, brass border, corner brackets
            Make("vip_frame", 384, 160, (x, y) =>
            {
                Color col = CardShadow(x, y);
                float d = CardSdf(x, y);
                if (d <= 0f)
                {
                    float t = Mathf.Clamp01((y - 6f) / 136f);
                    Color c = Color.Lerp(C(0x5a1119), C(0x25060a), t);
                    float n = (Hash((int)x, (int)y) - .5f) * .06f; c = new Color(c.r + n, c.g, c.b, 1f);
                    if (d > -4f) c = Color.Lerp(C(0xf5e2a8), C(0x8a6a30), Mathf.Clamp01((y - 6f) / 136f));   // brass border
                    else if (d > -6f) c = C(0x000000);
                    else if (d > -7.5f && y < 74f) c = Over(C(0xffffff, .12f), c);
                    var corners = new[] { new Vector2(24, 30), new Vector2(360, 30), new Vector2(24, 118), new Vector2(360, 118) };
                    for (int i = 0; i < 4; i++)
                    {
                        float sx = i % 2 == 0 ? 1f : -1f, sy = i < 2 ? 1f : -1f; var p = corners[i];
                        bool onH = Mathf.Abs(y - p.y) < 1.6f && (x - p.x) * sx > -1.6f && (x - p.x) * sx < 34f;
                        bool onV = Mathf.Abs(x - p.x) < 1.6f && (y - p.y) * sy > -1.6f && (y - p.y) * sy < 34f;
                        if (onH || onV) c = C(0xc9a050);
                    }
                    col = c;
                }
                return col;
            });

            // ---------------- the poker table seen from above
            Make("table_top", 760, 1100, (x, y) =>
            {
                float ex = (x - 380f) / 350f, ey = (y - 560f) / 500f, e = Mathf.Sqrt(ex * ex + ey * ey);
                if (e > 1.14f) return Color.clear;
                if (e > 1f) return new Color(0, 0, 0, .55f * (1f - (e - 1f) / .14f));
                if (e > .88f)
                {
                    float grain = Mathf.Sin(y * .09f + Mathf.Sin(x * .02f) * 3f) * .5f + .5f;
                    Color wood = Color.Lerp(C(0x4a2a16), C(0x7a4a28), grain * .55f);
                    float shade = Mathf.Clamp01((ex + ey) * .5f + .5f);
                    wood = Color.Lerp(wood, C(0x1c0f08), shade * .55f);
                    if (e > .985f) wood = Over(C(0x000000, .5f), wood);
                    if (ex + ey < -.4f && e > .9f && e < .96f) wood = Over(C(0xffffff, .12f), wood);
                    return wood;
                }
                if (e > .80f)
                {
                    Color rail = Color.Lerp(C(0x3a2216), C(0x180e08), Mathf.Clamp01((ex + ey) * .5f + .5f));
                    float n = (Hash((int)x, (int)y) - .5f) * .05f;
                    rail = new Color(rail.r + n, rail.g + n, rail.b + n, 1f);
                    if (e > .865f) rail = Over(C(0x000000, .5f), rail);
                    return rail;
                }
                if (e > .79f) return C(0x6e5224);
                float f = Mathf.Sqrt((ex + .15f) * (ex + .15f) + (ey + .2f) * (ey + .2f)) / .95f;
                Color felt = Color.Lerp(C(0x2f5e47), C(0x0f3a26), Mathf.Clamp01(f));
                float fn = (Hash((int)x * 3, (int)y * 3) - .5f) * .06f;
                if (e > .74f) felt = Color.Lerp(felt, Color.black, (e - .74f) / .05f * .35f);
                return new Color(felt.r + fn * .5f, felt.g + fn, felt.b + fn * .6f, 1f);
            });

            // ---------------- leather chair (top view, backrest at the top)
            Make("chair", 200, 150, (x, y) =>
            {
                float shadow = RoundRectSdf(x, y - 8f, 100f, 90f, 66f, 42f, 20f);
                Color col = shadow <= 0f ? new Color(0, 0, 0, .5f) : Color.clear;
                float seat = RoundRectSdf(x, y, 100f, 92f, 62f, 38f, 18f);
                if (seat <= 0f)
                {
                    Color c = Color.Lerp(C(0x4a3226), C(0x1c110c), Mathf.Clamp01((y - 54f) / 76f));
                    float n = (Hash((int)x, (int)y) - .5f) * .06f; c = new Color(c.r + n, c.g + n, c.b + n, 1f);
                    if (seat > -2f) c = Over(C(0x000000, .6f), c);
                    col = c;
                }
                float back = RoundRectSdf(x, y, 100f, 40f, 74f, 24f, 20f);
                if (back <= 0f)
                {
                    Color c = Color.Lerp(C(0x2e1d14), C(0x120a07), Mathf.Clamp01((y - 16f) / 48f));
                    if (back > -2f) c = Over(C(0x000000, .7f), c);
                    if (y < 30f && back < -2f) c = Over(C(0xffffff, .10f), c);
                    col = c;
                }
                return col;
            });

            // ---------------- table props
            Make("prop_cup", 72, 72, (x, y) =>
            {
                Color col = Circ(x, y - 4f, 36f, 36f) < 29f ? new Color(0, 0, 0, .5f) : Color.clear;
                float r = Circ(x, y, 36f, 34f);
                if (r < 28f) col = Color.Lerp(C(0xf0e8d4), C(0xb5a98a), r / 28f);
                float hd = Circ(x, y, 62f, 34f); if (hd < 7f && r > 18f) col = hd > 4.5f ? C(0xb5a98a) : Color.clear;
                if (r < 19f) col = C(0xe9e1cc);
                if (r < 14f) col = Color.Lerp(C(0x3a2010), C(0x120804), Circ(x, y, 32f, 30f) / 14f);
                return col;
            });

            Make("prop_ashtray", 104, 104, (x, y) =>
            {
                Color col = Circ(x, y - 5f, 52f, 52f) < 42f ? new Color(0, 0, 0, .55f) : Color.clear;
                float r = Circ(x, y, 52f, 50f);
                if (r < 40f) col = Color.Lerp(C(0x6a6a68), C(0x222222), r / 40f);
                if (r < 31f) col = Color.Lerp(C(0x101010), C(0x2a2a2a), r / 31f);
                float s = Seg(x, y, new Vector2(30, 68), new Vector2(76, 42));
                if (s < 3.6f) col = Color.Lerp(C(0xf2ead8), C(0xb9ae94), s / 3.6f);
                float tip = Seg(x, y, new Vector2(70, 45), new Vector2(76, 42));
                if (tip < 3.6f) col = C(0xff7a2a);
                if (Circ(x, y, 78f, 41f) < 6f) col = Over(C(0xffb04a, .55f), col);
                return col;
            });

            Make("prop_deck", 112, 148, (x, y) =>
            {
                Color col = Color.clear;
                for (int i = 0; i < 4; i++)
                {
                    float ox = 4f + i * -1.2f, oy = 6f + i * -3.2f;
                    float d = RoundRectSdf(x, y, 50f + ox, 72f + oy - 4f, 42f, 62f, 9f);
                    if (i == 0 && d < 7f && d > 0f) col = Over(new Color(0, 0, 0, .35f * (1f - d / 7f)), col);
                    if (d <= 0f)
                    {
                        Color c = C(0x8a1c24);
                        float lx = x - (50f + ox), ly = y - (68f + oy);
                        if (Mathf.Abs(((lx + ly) % 16f + 16f) % 16f) < 1.3f || Mathf.Abs(((lx - ly) % 16f + 16f) % 16f) < 1.3f) c = C(0x5a1018);
                        if (d > -4.5f) c = C(0xd9d0bd);
                        if (d > -1f) c = C(0x4a4438);
                        col = c;
                    }
                }
                return col;
            });

            Make("prop_dallah", 150, 210, (x, y) =>
            {
                Color col = Color.clear;
                Color Metal(float xx, float yy)
                {
                    float u = Mathf.Clamp01((xx - 28f) / 100f);
                    Color[] st = { C(0x5a4018), C(0xfff0b8), C(0xc9a050), C(0x6a4c1c), C(0x3a2a0c) };
                    float p = u * 4f; int k = Mathf.Min(3, (int)p);
                    Color c = Color.Lerp(st[k], st[k + 1], p - k);
                    return Color.Lerp(c, C(0x000000), Mathf.Clamp01((yy - 120f) / 160f) * .25f);
                }
                // shadow
                if (Mathf.Pow((x - 75f) / 52f, 2) + Mathf.Pow((y - 196f) / 9f, 2) < 1f) col = new Color(0, 0, 0, .5f);
                // body profile: radius by y
                float[] ys = { 52, 62, 78, 100, 130, 160, 184, 194 };
                float[] rs = { 12, 17, 28, 40, 49, 52, 48, 40 };
                if (y >= 52f && y <= 194f)
                {
                    float rad = rs[rs.Length - 1];
                    for (int i = 0; i < ys.Length - 1; i++) if (y <= ys[i + 1]) { rad = Mathf.Lerp(rs[i], rs[i + 1], (y - ys[i]) / (ys[i + 1] - ys[i])); break; }
                    if (Mathf.Abs(x - 75f) <= rad) col = Metal(x, y);
                }
                // neck + lid
                if (y >= 26f && y < 52f && Mathf.Abs(x - 75f) <= Mathf.Lerp(9f, 12f, (y - 26f) / 26f)) col = Metal(x, y);
                if (Mathf.Pow((x - 75f) / 17f, 2) + Mathf.Pow((y - 28f) / 12f, 2) <= 1f) col = Metal(x, y);
                if (Circ(x, y, 75f, 11f) < 7f) col = Color.Lerp(C(0xfff0b8), C(0x6a4c1c), Circ(x, y, 72f, 8f) / 8f);
                // spout
                var spout = new[] { new Vector2(108, 118), new Vector2(116, 100), new Vector2(136, 56), new Vector2(143, 60), new Vector2(124, 108), new Vector2(118, 132) };
                if (InPoly(x, y, spout)) col = C(0xb98a3a);
                // handle (left)
                float hr = Circ(x, y, 30f, 112f);
                if (hr > 22f && hr < 29f && x < 40f) col = C(0x8a4a22);
                // engraved band
                if (y > 118f && y < 124f && Mathf.Abs(x - 75f) < 48f) col = Over(C(0x000000, .22f), col);
                return col;
            });

            Make("icon_mic", 128, 128, (x, y) => MicIcon(x, y, false));
            Make("icon_mic_off", 128, 128, (x, y) => MicIcon(x, y, true));

            Make("host_star", 64, 64, (x, y) =>
            {
                var pts = new Vector2[10];
                for (int i = 0; i < 10; i++) { float a = -Mathf.PI / 2f + i * Mathf.PI / 5f, r = i % 2 == 0 ? 29f : 12f; pts[i] = new Vector2(32f + Mathf.Cos(a) * r, 33f + Mathf.Sin(a) * r); }
                if (!InPoly(x, y, pts)) return Color.clear;
                return Color.Lerp(C(0xfff0b8), C(0x8a6a30), Mathf.Clamp01((y - 8f) / 48f));
            });

            Make("seat_dashed", 128, 128, (x, y) =>
            {
                float dx = x - 64f, dy = y - 64f, r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r < 54f || r > 60f) return Color.clear;
                float ang = Mathf.Atan2(dy, dx) + Mathf.PI;
                return ((int)(ang / (Mathf.PI * 2f / 28f)) % 2 == 0) ? C(0xa98f62, .85f) : Color.clear;
            });

            Make("dot_white", 64, 64, (x, y) => Circ(x, y, 32f, 32f) <= 30f ? Color.white : Color.clear);
            Make("talk_glow", 256, 256, (x, y) =>
            {
                float r = Circ(x, y, 128f, 128f) / 128f;
                float a = r < .5f ? 0f : r < .62f ? (r - .5f) / .12f * .9f : r < 1f ? .9f * (1f - (r - .62f) / .38f) : 0f;
                return C(0x7fe0c4, a * .8f);
            });

            AssetDatabase.Refresh();
        }

        private static Color MicIcon(float x, float y, bool off)
        {
            Color ivory = C(0xd9d0bd);
            Color col = Color.clear;
            float cap = RoundRectSdf(x, y, 64f, 46f, 19f, 36f, 19f);
            if (cap <= 0f) col = Color.Lerp(C(0xf4eedd), C(0xa49a82), Mathf.Clamp01((x - 45f) / 38f));
            if (cap <= 0f && (Mathf.Abs(y - 34f) < 1.4f || Mathf.Abs(y - 46f) < 1.4f || Mathf.Abs(y - 58f) < 1.4f) && Mathf.Abs(x - 64f) < 12f) col = C(0x6a604c);
            float arc = Circ(x, y, 64f, 56f);
            if (arc > 31f && arc < 37f && y > 56f) col = ivory;
            if (Seg(x, y, new Vector2(64, 92), new Vector2(64, 110)) < 3.6f) col = ivory;
            if (Seg(x, y, new Vector2(44, 112), new Vector2(84, 112)) < 3.8f) col = ivory;
            if (off)
            {
                float s = Seg(x, y, new Vector2(22, 20), new Vector2(106, 108));
                if (s < 10f) col = C(0x000000);
                if (s < 6.5f) col = C(0xd6313b);
            }
            return col;
        }
    }
}
