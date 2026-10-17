using System.IO;
using UnityEditor;
using UnityEngine;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// Textures that are plain gradients/circles are drawn directly here (3x3 supersampled) so they do not depend on a browser.
    /// The look follows Docs/mockups/footer.html (leather bar with stitching, brass seal, spotlight, badges).
    /// </summary>
    public static class KitProceduralTextures
    {
        private const string Dir = "Assets/_Project/Art/UI/Kit/";
        internal delegate Color Px(float x, float y);   // x, y in texture pixels, y from the top

        internal static Color C(int hex, float a = 1f) =>
            new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, a);

        internal static Color Over(Color top, Color bottom)
        {
            float a = top.a + bottom.a * (1f - top.a);
            if (a <= 0f) return Color.clear;
            return new Color((top.r * top.a + bottom.r * bottom.a * (1f - top.a)) / a,
                             (top.g * top.a + bottom.g * bottom.a * (1f - top.a)) / a,
                             (top.b * top.a + bottom.b * bottom.a * (1f - top.a)) / a, a);
        }

        internal static float Hash(int x, int y)
        {
            float v = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        internal static void Make(string name, int w, int h, Px f)
        {
            const int S = 3;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int j = 0; j < S; j++)
                        for (int i = 0; i < S; i++)
                        {
                            var c = f(x + (i + .5f) / S, y + (j + .5f) / S);
                            r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                        }
                    float n = S * S;
                    tex.SetPixel(x, h - 1 - y, a <= 0 ? Color.clear : new Color(r / a, g / a, b / a, a / n));
                }
            tex.Apply();
            File.WriteAllBytes(Dir + name + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }


        internal static bool InPoly(float x, float y, Vector2[] p)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if (((p[i].y > y) != (p[j].y > y)) && (x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)) inside = !inside;
            return inside;
        }

        internal static float RoundRectSdf(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x - cx) - (hx - r), qy = Mathf.Abs(y - cy) - (hy - r);
            return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        public static void Generate()
        {
            Directory.CreateDirectory(Dir);

            // leather bar tile (80 x 210, repeats horizontally; dash period 16 divides 80)
            Make("navbar_tile", 80, 210, (x, y) =>
            {
                float t = y / 210f;
                Color c = t < .55f ? Color.Lerp(C(0x2c1f17), C(0x1a110d), t / .55f) : Color.Lerp(C(0x1a110d), C(0x0e0806), (t - .55f) / .45f);
                float n = (Hash((int)x, (int)y) - .5f) * .08f;
                c = new Color(c.r + n, c.g + n * .8f, c.b + n * .6f, 1f);
                if (y < 3f) c = Over(C(0xffffff, .16f), c);
                else if (y < 6f) c = Color.black;
                if (y >= 20f && y < 24f && (x % 16f) < 10f) c = Over(C(0xa98f62, .42f), c);
                return c;
            });

            Make("nav_shadow", 8, 90, (x, y) => new Color(0, 0, 0, Mathf.Pow(y / 90f, 1.5f) * .92f));

            // brass seal for the centre tab
            Make("nav_seal", 192, 192, (x, y) =>
            {
                float dx = x - 96f, dy = y - 96f, r = Mathf.Sqrt(dx * dx + dy * dy);
                float sx = x - 96f, sy = y - 108f, rs = Mathf.Sqrt(sx * sx + sy * sy);
                Color col = new Color(0, 0, 0, .55f * Mathf.Clamp01(1f - (rs - 76f) / 18f));
                if (r <= 88f) col = C(0x6e5224);
                if (r <= 85f) col = Color.black;
                if (r <= 80f)
                {
                    float ang = (Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg + 360f) % 360f;
                    float u = ((ang - 210f) + 360f) % 360f / 360f;
                    Color[] st = { C(0xf5e2a8), C(0x8a6a30), C(0xd2ad60), C(0x7a5a22), C(0xf5e2a8) };
                    float p = u * 4f; int k = Mathf.Min(3, (int)p);
                    col = Color.Lerp(st[k], st[k + 1], p - k);
                    if (r > 77f) col = Over(C(0xffffff, .25f), col);
                }
                if (r <= 64f)
                {
                    float d = Mathf.Sqrt(dx * dx + (dy + 19f) * (dy + 19f)) / 96f;
                    col = Color.Lerp(C(0x3a2a20), C(0x0c0807), Mathf.Clamp01(d));
                    if (r > 61f) col = C(0xb99048);
                }
                return col;
            });

            Make("nav_seal_glow", 360, 360, (x, y) =>
            {
                float r = Mathf.Sqrt((x - 180f) * (x - 180f) + (y - 180f) * (y - 180f)) / 180f;
                float a = r < .38f ? Mathf.Lerp(.8f, .33f, r / .38f) : r < .68f ? Mathf.Lerp(.33f, 0f, (r - .38f) / .30f) : 0f;
                return C(0xf5e2a8, a);
            });

            Make("nav_spot", 260, 200, (x, y) =>
            {
                float dxn = (x / 260f - .5f) / .5f, dyn = (y / 200f - 1f) / .8f;
                float d = Mathf.Sqrt(dxn * dxn + dyn * dyn);
                return C(0xf5e2a8, .35f * Mathf.Clamp01(1f - d / .7f));
            });

            Make("live_pill", 176, 56, (x, y) =>
            {
                Color col = Color.clear;
                float Sdf(float yy) { float qx = Mathf.Abs(x - 88f) - (83f - 23f), qy = Mathf.Abs(yy - 28f) - (23f - 23f); return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - 23f; }
                float ds = Sdf(y - 3f);
                if (ds <= 0f) col = new Color(0, 0, 0, .6f);
                float d = Sdf(y);
                if (d <= 0f) col = d > -3f ? C(0x8a1c24) : C(0x1a0e0e);
                return col;
            });

            Make("badge_dot", 64, 64, (x, y) =>
            {
                float r = Mathf.Sqrt((x - 32f) * (x - 32f) + (y - 32f) * (y - 32f));
                Color col = C(0xe0343f, .45f * Mathf.Clamp01(1f - (r - 23f) / 9f) * (r > 23f ? 1f : 0f));
                if (r <= 23f) col = C(0x1a110d);
                if (r <= 18f)
                {
                    float d = Mathf.Sqrt((x - 26f) * (x - 26f) + (y - 25f) * (y - 25f)) / 26f;
                    col = Color.Lerp(C(0xff6a72), C(0xc0222c), Mathf.Clamp01(d));
                }
                return col;
            });


            // ---- parts for the top bar + hanging profile card (Docs/mockups/profilecard.html) ----
            // default avatar: dark disc with a fedora silhouette
            Make("avatar_default", 128, 128, (x, y) =>
            {
                float dx = x - 64f, dy = y - 64f, r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 63f) return Color.clear;
                float bgT = Mathf.Clamp01(Mathf.Sqrt(dx * dx + (y - 38f) * (y - 38f)) / 90f);
                Color col = Color.Lerp(C(0x2a2623), C(0x0c0b0a), bgT);
                bool In(float cx, float cy, float rx, float ry) { float a = (x - cx) / rx, b = (y - cy) / ry; return a * a + b * b <= 1f; }
                if (In(64f, 132f, 52f, 42f)) col = C(0x6f6759);
                if (In(64f, 74f, 17f, 19f)) col = C(0x7b7262);
                if (In(64f, 58f, 37f, 8f)) col = C(0xa39880);
                if (y <= 58f && In(64f, 47f, 20f, 17f)) col = C(0xa39880);
                if (y > 49f && y < 55f && Mathf.Abs(dx) < 20f) col = C(0x3a0c10);
                return col;
            });

            Make("dots_tile", 12, 6, (x, y) => (x % 12f) < 5f && y > 1f && y < 5f ? C(0x1c170f, .55f) : Color.clear);

            var ribbon = new[] { new Vector2(0, 0), new Vector2(56, 0), new Vector2(56, 44), new Vector2(28, 31), new Vector2(0, 44) };
            Make("ribbon_red", 56, 44, (x, y) => InPoly(x, y, ribbon) ? Color.Lerp(C(0x9a2230), C(0x5c1018), y / 44f) : Color.clear);

            var strap = new[] { new Vector2(0, 0), new Vector2(130, 0), new Vector2(78, 170), new Vector2(52, 170) };
            Make("strap_red", 130, 170, (x, y) =>
            {
                if (!InPoly(x, y, strap)) return Color.clear;
                float shade = 1f - Mathf.Abs(x - 65f) / 130f * .5f;
                return Color.Lerp(C(0x5c1018), C(0x9a2230), shade) * new Color(1, 1, 1, 1);
            });

            Make("clip_metal", 104, 48, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 52f, 24f, 50f, 22f, 8f);
                if (d > 0f) return Color.clear;
                if (d > -2.5f) return Color.black;
                float t = Mathf.Clamp01((y - 2f) / 44f);
                Color c = Color.Lerp(C(0xe3e5e6), C(0x6a6d6f), t);
                if (y > 8f && y < 11f) c = Over(C(0xffffff, .5f), c);
                return c;
            });

            Make("level_seal", 64, 64, (x, y) =>
            {
                float r = Mathf.Sqrt((x - 32f) * (x - 32f) + (y - 32f) * (y - 32f));
                if (r > 31f) return Color.clear;
                if (r > 28f) return Color.black;
                float t = Mathf.Clamp01(((x - 32f) + (y - 32f)) / 56f + .5f);
                Color c = Color.Lerp(C(0xf0d890), C(0x8a6a2e), t);
                if (r > 25f) c = Over(C(0xffffff, .22f), c);
                return c;
            });

            Make("barcode_tile", 26, 44, (x, y) =>
            {
                float m = x % 26f;
                bool bar = m < 4f || (m >= 8f && m < 10f) || (m >= 16f && m < 22f);
                return bar ? C(0x1c170f, .82f) : Color.clear;
            });

            Make("stamp_frame", 64, 64, (x, y) =>
            {
                float d = RoundRectSdf(x, y, 32f, 32f, 31f, 31f, 14f);
                return (d <= 0f && d > -5f) ? Color.white : Color.clear;
            });

            AssetDatabase.Refresh();
        }

        /// <summary>Sprite import settings for everything in the kit folder (9-slice borders for panels).</summary>
        public static void ConfigureSprites()
        {
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir.TrimEnd('/') }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null) continue;
                var name = Path.GetFileNameWithoutExtension(p);
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.filterMode = FilterMode.Bilinear;
                imp.spritePixelsPerUnit = 100;
                imp.maxTextureSize = 1024;
                imp.wrapMode = (name == "skyline_far" || name == "skyline_near" || name == "road_dash" || name == "dash_h" || name == "wall_lines" || name == "curb_tile") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.Compressed;
                var s = new TextureImporterSettings();
                imp.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect;
                if (name.StartsWith("leather_") || name == "brass_plate" || name == "paper" || name == "slot_dark")
                    s.spriteBorder = new Vector4(52, 64, 52, 52);
                else if (name == "paper_ticket")
                    s.spriteBorder = new Vector4(40, 64, 40, 40);
                else if (name == "steel_plate" || name == "vip_frame")
                    s.spriteBorder = new Vector4(52, 64, 52, 52);
                else if (name == "chalkboard")
                    s.spriteBorder = new Vector4(60, 60, 60, 60);
                else if (name == "gold_frame")
                    s.spriteBorder = new Vector4(40, 40, 40, 40);
                else if (name == "plank")
                    s.spriteBorder = new Vector4(30, 20, 30, 28);
                else if (name == "price_tag")
                    s.spriteBorder = new Vector4(30, 24, 56, 24);
                else if (name == "stamp_frame")
                    s.spriteBorder = new Vector4(24, 24, 24, 24);
                else if (name == "reset_pill")
                    s.spriteBorder = new Vector4(30, 30, 30, 30);
                else if (name == "wall_vignette")
                    s.spriteBorder = new Vector4(125, 125, 125, 125);
                else if (name == "plaque_gold")
                    s.spriteBorder = new Vector4(36, 28, 36, 28);
                else if (name == "rec_card")
                    s.spriteBorder = new Vector4(48, 48, 48, 48);
                else if (name == "mine_bar")
                    s.spriteBorder = new Vector4(48, 48, 48, 48);
                else if (name.StartsWith("door_"))
                    s.spriteBorder = new Vector4(34, 34, 34, 34);
                else if (name == "board_day")
                    s.spriteBorder = new Vector4(70, 70, 70, 70);
                else if (name == "chip_pill" || name == "chip_pill_on")
                    s.spriteBorder = new Vector4(30, 30, 30, 30);
                else if (name == "row_bg")
                    s.spriteBorder = new Vector4(22, 22, 22, 22);
                else if (name == "live_pill")
                    s.spriteBorder = new Vector4(30, 30, 30, 30);
                else
                    s.spriteBorder = Vector4.zero;
                imp.SetTextureSettings(s);
                imp.SaveAndReimport();
            }
        }
    }
}
