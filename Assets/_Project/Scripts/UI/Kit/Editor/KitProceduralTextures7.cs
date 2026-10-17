using UnityEngine;
using static MafiaNight.UI.Kit.Editor.KitProceduralTextures;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Leaderboards look from Docs/mockups/leaderboards.html: flat chalkboard, pill chips, row plates and the three door icons.</summary>
    public static class KitProceduralTextures7
    {
        private static bool Box(float x, float y, float x0, float y0, float x1, float y1) => x >= x0 && x <= x1 && y >= y0 && y <= y1;
        private static bool RR(float x, float y, float x0, float y0, float x1, float y1, float r) =>
            RoundRectSdf(x, y, (x0 + x1) * .5f, (y0 + y1) * .5f, (x1 - x0) * .5f, (y1 - y0) * .5f, r) <= 0f;
        private static float SegD(float x, float y, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(x, y); var a = new Vector2(ax, ay); var ab = new Vector2(bx, by) - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        private static void Door(string name, Color top, Color bottom, Color border, float borderPx, bool grain)
        {
            Make(name, 256, 128, (x, y) =>
            {
                float r = y < 64f ? 25f : 8f;
                float d = RoundRectSdf(x, y, 128f, 64f, 128f, 64f, r);
                if (d > 0f) return Color.clear;
                if (d > -2f) return C(0x000000);
                Color c = Color.Lerp(top, bottom, Mathf.Clamp01(y / 128f));
                if (grain) { float g = (Hash((int)x, (int)y) - .5f) * .05f; c = new Color(c.r + g, c.g + g, c.b + g, 1f); }
                if (d > -2f - borderPx) return Over(border, c);
                return c;
            });
        }

        public static void Generate()
        {
            // dark chalkboard with a thin wooden frame (the HTML ".board.day" and ".door.d1")
            Make("board_day", 256, 256, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 128f, 128f, 128f, 128f, 22f);
                if (d > 0f) return Color.clear;
                if (d > -2f) return C(0x000000);
                if (d > -9f) return Color.Lerp(C(0x5a4230), C(0x3a281a), Mathf.Clamp01(y / 256f));
                if (d > -11f) return C(0x000000);
                float g = (Hash((int)x, (int)y) - .5f) * .035f;
                Color c = Color.Lerp(C(0x1c2622), C(0x101714), Mathf.Clamp01(y / 256f));
                return new Color(c.r + g, c.g + g, c.b + g, 1f);
            });

            // the three doors (HTML .door.d1/.d2/.d3): top radius 9px, bottom 3px, inset border; and the cream selection ring
            Door("door_d1", C(0x26302b), C(0x121815), C(0x4a3a2a), 6f, false);
            Door("door_d2", C(0xbdb08e), C(0x8a7e60), new Color(0f, 0f, 0f, 0.27f), 2.5f, false);
            Door("door_d3", C(0x3a1218), C(0x1a0609), C(0x6e5224), 4.5f, true);
            Make("door_ring", 256, 128, (x, y) =>
            {
                float dOut = RoundRectSdf(x, y, 128f, 64f, 128f, 64f, y < 64f ? 31f : 13f);
                if (dOut > 0f) return Color.clear;
                float dIn = RoundRectSdf(x, y, 128f, 64f, 123f, 59f, y < 64f ? 26f : 8f);
                return dIn > 0f ? Color.white : Color.clear;
            });

            // pill chips: translucent outline / gold
            Make("chip_pill", 128, 64, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 64f, 32f, 64f, 32f, 32f);
                if (d > 0f) return Color.clear;
                if (d > -2.2f) return C(0xffffff, 1f);
                return C(0xffffff, .25f);
            });
            Make("chip_pill_on", 128, 64, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 64f, 30f, 64f, 30f, 30f);
                float ds = RoundRectSdf(x, y, 64f, 33f, 64f, 30f, 30f);   // hard shadow under the pill
                if (d > 0f) return ds <= 0f ? C(0x000000) : Color.clear;
                Color c = Color.Lerp(C(0xf5e2a8), C(0xc9a050), Mathf.Clamp01(y / 60f));
                if (d > -2f && y < 30f) c = Color.Lerp(c, Color.white, .45f);
                return c;
            });

            // row plate (tinted in the scene)
            Make("row_bg", 64, 64, (x, y) => RoundRectSdf(x, y, 32f, 32f, 32f, 32f, 14f) <= 0f ? Color.white : Color.clear);

            // door icons (96 x 96)
            Make("icon_day", 96, 96, (x, y) =>
            {
                if (RR(x, y, 28f, 80f, 68f, 87f, 2f)) return C(0xe9e6dc);                 // chalk tray
                if (RR(x, y, 10f, 17f, 86f, 74f, 5f))
                {
                    if (!RR(x, y, 16f, 23f, 80f, 68f, 3f)) return C(0x8a6040);            // frame
                    if (SegD(x, y, 22f, 37f, 58f, 37f) < 2.2f || SegD(x, y, 22f, 49f, 70f, 49f) < 2.2f || SegD(x, y, 22f, 61f, 46f, 61f) < 2.2f) return C(0xe9e6dc);
                    return C(0x1b2420);
                }
                return Color.clear;
            });
            Make("icon_week", 96, 96, (x, y) =>
            {
                if (Box(x, y, 74f, 28f, 86f, 78f)) return Box(x, y, 74.5f, 28.5f, 85.5f, 77.5f) ? C(0xbfb393) : C(0x1c170f);   // folded side
                if (Box(x, y, 14f, 16f, 76f, 82f))
                {
                    if (!Box(x, y, 15.5f, 17.5f, 74.5f, 80.5f)) return C(0x1c170f);
                    if (Box(x, y, 52f, 38f, 66f, 62f)) return C(0x8a1c24);                  // photo block
                    if (SegD(x, y, 22f, 26f, 66f, 26f) < 2f || SegD(x, y, 22f, 38f, 46f, 38f) < 2f || SegD(x, y, 22f, 50f, 46f, 50f) < 2f || SegD(x, y, 22f, 62f, 46f, 62f) < 2f) return C(0x1c170f);
                    return C(0xd9cfb4);
                }
                return Color.clear;
            });
            var star = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = -Mathf.PI / 2f + i * Mathf.PI / 5f, rad = i % 2 == 0 ? 17f : 7f;
                star[i] = new Vector2(48f + Mathf.Cos(a) * rad, 50f + Mathf.Sin(a) * rad);
            }
            Make("icon_wall", 96, 96, (x, y) =>
            {
                if (RR(x, y, 16f, 8f, 80f, 88f, 6f))
                {
                    if (!RR(x, y, 20f, 12f, 76f, 84f, 4f)) return Color.Lerp(C(0xfff4cc), C(0x6a4c1c), Mathf.Clamp01((y - 8f) / 80f));   // gold frame
                    if (InPoly(x, y, star)) return Color.Lerp(C(0xfff4cc), C(0xb48a3c), Mathf.Clamp01((y - 33f) / 34f));
                    return C(0x1a0609);
                }
                return Color.clear;
            });

            UnityEditor.AssetDatabase.Refresh();
        }
    }
}
