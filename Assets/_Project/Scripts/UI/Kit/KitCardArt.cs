using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>The small drawings of a role card (Docs/mockups/role-cards.html): the corner ornament, the emblem of each role and the hexagon gem (the card backs are painted by KitEmblem).</summary>
    public static class KitCardArt
    {
        /// <summary>The gold filigree of a corner, in the 24-unit space of <see cref="KitSvg"/> (the mockup draws it in 36 units).</summary>
        public const string Ornament = "M2 2c0 8 2.7 13.3 9.3 14.7M2 2c8 0 13.3 2.7 14.7 9.3M5.3 5.3c0 4.7 2 7.3 6 8M5.3 5.3c4.7 0 7.3 2 8 6M11.3 16.7c2.7.7 4.7-.7 4.7-2.7M16.7 11.3c.7 2.7-.7 4.7-2.7 4.7|O2.3 2.3 1.2|O12.7 12.7 1.1";

        public static string RoleEmblem(string roleId)
        {
            switch (roleId)
            {
                case "mafia": return "M12 3 4 7v5c0 4.5 3.5 7.5 8 9 4.5-1.5 8-4.5 8-9V7zM9 11.5h6";
                case "doctor": return "M12 5v14M5 12h14";
                case "sheikh": return "M12 3l2.6 5.6 6 .7-4.5 4 1.3 6-5.4-3.2-5.4 3.2 1.3-6-4.5-4 6-.7z";
                default: return "O12 8 4|M4 20c0-4 3.6-6.5 8-6.5s8 2.5 8 6.5";
            }
        }

        /// <summary>The hexagon of the corner index: a light rim, then a gradient from <paramref name="top"/> to <paramref name="bottom"/>, black outline. 52x64 pixels, drawn once per colour pair.</summary>
        public static Sprite Gem(Color top, Color bottom)
        {
            string key = "gem" + ColorUtility.ToHtmlStringRGB(top) + ColorUtility.ToHtmlStringRGB(bottom);
            if (cache.TryGetValue(key, out var hit) && hit != null) return hit;
            const int w = 52, h = 64;
            var verts = new[] { new Vector2(w * 0.5f, h - 2f), new Vector2(w - 2f, h * 0.76f), new Vector2(w - 2f, h * 0.28f), new Vector2(w * 0.5f, 2f), new Vector2(2f, h * 0.28f), new Vector2(2f, h * 0.76f) };
            var centre = new Vector2(w * 0.5f, h * 0.5f);
            var normals = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                var e = verts[(i + 1) % 6] - verts[i];
                var n = new Vector2(e.y, -e.x).normalized;
                if (Vector2.Dot(n, verts[i] - centre) < 0f) n = -n;
                normals[i] = n;
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float sd = float.MinValue;
                    for (int i = 0; i < 6; i++) sd = Mathf.Max(sd, Vector2.Dot(p - verts[i], normals[i]));
                    float cover = Mathf.Clamp01(0.5f - sd);
                    if (cover <= 0f) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    float t = 1f - (float)y / (h - 1);                 // 0 at the top, 1 at the bottom
                    Color body = Color.Lerp(top, bottom, t);
                    Color c;
                    if (sd > -1.6f) c = new Color(0f, 0f, 0f, 1f);                                         // outline
                    else if (sd > -3.6f) c = Color.Lerp(Color.white, top, 0.55f);                           // light rim
                    else c = Color.Lerp(body, Color.black, 0.25f * t);
                    c.a = cover;
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            cache[key] = sp;
            return sp;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> cache = new System.Collections.Generic.Dictionary<string, Sprite>();
    }
}
