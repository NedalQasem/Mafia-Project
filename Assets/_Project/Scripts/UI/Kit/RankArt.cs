using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>One rank's exclusive set in the store: name, three colours (HTML --m1 --m2 --e) and the five item names.</summary>
    public sealed class RankSetInfo
    {
        public string name;
        public Color m1, m2, e;
        public string[] items;
    }

    public static class RankSets
    {
        public static readonly string[] TypeNames = { "إطار", "روم", "دردشة", "دخول", "لقب" };

        private static Color H(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 1f);

        public static readonly RankSetInfo[] All =
        {
            new RankSetInfo { name = "الصبي", m1 = H(0x8a6a4a), m2 = H(0x5a4330), e = H(0xe0c2a0), items = new[] { "حبل الصبي", "زقاق الحارة", "ورقة جريدة", "خطوات على الرصيف", "ابن الحارة" } },
            new RankSetInfo { name = "العميل", m1 = H(0x9aa0a4), m2 = H(0x4d5254), e = H(0xe6ebee), items = new[] { "حلقة المفتاح", "مكتب صغير", "بطاقة عمل", "طرقة على الباب", "المطّلع" } },
            new RankSetInfo { name = "الجندي", m1 = H(0xc08a5a), m2 = H(0x6a4020), e = H(0xffd8b0), items = new[] { "إطار الرصاصة", "ساحة التدريب", "فقاعة الخرطوش", "دخول الجندي", "الجندي الأمين" } },
            new RankSetInfo { name = "الريّس", m1 = H(0xd2ad60), m2 = H(0x7a5a22), e = H(0xfff0c0), items = new[] { "إطار الفيدورا", "المقهى الخلفي", "فقاعة الريّس", "وصول السيارة", "الريّس" } },
            new RankSetInfo { name = "المستشار", m1 = H(0xe8cc8c), m2 = H(0x8a6a30), e = H(0xfff6d8), items = new[] { "إطار الريشة", "المكتبة", "فقاعة الحكمة", "ظل المستشار", "صاحب الرأي" } },
            new RankSetInfo { name = "اليد اليمنى", m1 = H(0xc9424c), m2 = H(0x5c0f16), e = H(0xffd9db), items = new[] { "إطار الخاتم", "الغرفة المغلقة", "فقاعة الختم", "دخول بلا ضجيج", "الذراع القوية" } },
            new RankSetInfo { name = "الزعيم", m1 = H(0xf0d68a), m2 = H(0x6a1218), e = H(0xfff2c4), items = new[] { "إطار التاج", "قاعة العرش", "فقاعة الزعيم", "السجادة الذهبية", "الزعيم" } },
            new RankSetInfo { name = "العرّاب", m1 = H(0xf5e2a8), m2 = H(0x2a2018), e = H(0xffe9a0), items = new[] { "إطار الوردة", "الحديقة السوداء", "فقاعة العرّاب", "دخول العرّاب", "العرّاب" } },
        };
    }

    /// <summary>Draws the small thumbnails of a rank set (store.html .tf .tr .tbb .te .tt) from simple parts, tinted by the rank's colours.</summary>
    public static class RankArt
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        private static Color Lock(Color c, bool locked)
        {
            if (!locked) return c;
            float l = (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) * 0.5f;   // HTML: grayscale(1) brightness(.5)
            return new Color(l, l, l, c.a);
        }

        /// <summary>Conic gradient disc or ring, like CSS conic-gradient. stops are evenly or explicitly placed (pos 0..1).</summary>
        private static Sprite Conic(string key, int size, float fromDeg, float[] pos, Color[] col, float innerRatio)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float c = (size - 1) * 0.5f, ro = size * 0.5f - 1f, ri = ro * innerRatio;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(ro - r + 0.5f);
                    if (innerRatio > 0f) a *= Mathf.Clamp01(r - ri + 0.5f);
                    if (a <= 0f) { tex.SetPixel(x, y, Color.clear); continue; }
                    float ang = Mathf.Repeat((Mathf.Atan2(dx, dy) * Mathf.Rad2Deg - fromDeg) / 360f, 1f);
                    Color k = col[col.Length - 1];
                    for (int i = 0; i < pos.Length - 1; i++)
                        if (ang <= pos[i + 1]) { k = Color.Lerp(col[i], col[i + 1], (ang - pos[i]) / Mathf.Max(1e-4f, pos[i + 1] - pos[i])); break; }
                    k.a = a; tex.SetPixel(x, y, k);
                }
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            cache[key] = s; return s;
        }

        private static Image Add(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, Color color, bool sliced = false, float ppu = 1.6f)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.sprite = sprite; img.color = color; img.raycastTarget = false;
            if (sliced) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = ppu; }
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
            return img;
        }

        /// <summary>Round medal with the rank number (HTML .rkm), 122 px. Number text is added by the caller.</summary>
        public static Image Medal(Transform parent, RankSetInfo r, bool locked, Vector2 pos, float size)
        {
            var th = KitTheme.Current;
            var cols = new[] { Lock(r.m1, locked), Lock(r.m2, locked), Lock(r.m1, locked), Lock(r.m2, locked), Lock(r.m1, locked) };
            var outer = Add(parent, "Medal", Conic("medal" + r.name + locked, 128, 210f, new[] { 0f, .3f, .55f, .8f, 1f }, cols, 0f), pos, new Vector2(size + 12f, size + 12f), Color.white);
            outer.transform.SetAsFirstSibling();
            var black = Add(outer.transform, "Rim", th.dotWhite, Vector2.zero, new Vector2(size + 12f, size + 12f), Color.black); black.transform.SetAsFirstSibling();
            var face = Add(outer.transform, "Face", th.dotWhite, Vector2.zero, new Vector2(size * 0.7f, size * 0.7f), new Color32(0x0F, 0x0B, 0x09, 255));
            return outer;
        }

        /// <summary>Builds one thumbnail (type 0 frame, 1 room, 2 chat bubbles, 3 entry strip, 4 title) centred in parent. scale 1 = the small size used in the rank cards.</summary>
        public static void Build(RectTransform parent, RankSetInfo r, int type, bool locked, float scale)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
            var th = KitTheme.Current;
            Color m1 = Lock(r.m1, locked), m2 = Lock(r.m2, locked), e = Lock(r.e, locked);
            float k = scale;
            switch (type)
            {
                case 0:
                {
                    var cols = new[] { m1, m2, e, m2, m1 };
                    Add(parent, "Ring", Conic("ring" + r.name + locked, 128, 0f, new[] { 0f, .25f, .5f, .75f, 1f }, cols, 0.826f), Vector2.zero, new Vector2(127, 127) * k, Color.white);
                    Add(parent, "Av", th.avatarDefault, Vector2.zero, new Vector2(105, 105) * k, locked ? new Color(.5f, .5f, .5f, 1f) : Color.white);
                    break;
                }
                case 1:
                {
                    Add(parent, "Border", th.rowBg, Vector2.zero, new Vector2(155, 122) * k, m1, true);
                    Add(parent, "Bg", th.rowBg, Vector2.zero, new Vector2(149, 116) * k, new Color32(0x0B, 0x08, 0x06, 255), true);
                    Add(parent, "Glow", th.dotWhite, new Vector2(0, 38) * k, new Vector2(120, 70) * k, new Color(m1.r, m1.g, m1.b, 0.28f));
                    Add(parent, "TableRim", th.dotWhite, new Vector2(0, -6) * k, new Vector2(91, 52) * k, m2);
                    Add(parent, "Table", th.dotWhite, new Vector2(0, -6) * k, new Vector2(83, 44) * k, locked ? new Color(.1f, .1f, .1f) : new Color32(0x0F, 0x3A, 0x26, 255));
                    break;
                }
                case 2:
                {
                    Add(parent, "B1", th.rowBg, new Vector2(25, 22) * k, new Vector2(105, 33) * k, m1, true);
                    var b2 = Add(parent, "B2", th.rowBg, new Vector2(-36, -22) * k, new Vector2(83, 33) * k, new Color(m1.r, m1.g, m1.b, 0.8f), true);
                    break;
                }
                case 3:
                {
                    Add(parent, "Border", th.rowBg, Vector2.zero, new Vector2(166, 61) * k, m1, true);
                    Add(parent, "Bg", th.rowBg, Vector2.zero, new Vector2(160, 55) * k, Color.Lerp(new Color32(0x12, 0x0D, 0x0A, 255), m2, 0.55f), true);
                    Add(parent, "Dot", th.dotWhite, new Vector2(56, 0) * k, new Vector2(33, 33) * k, e);
                    Add(parent, "Bar", th.rowBg, new Vector2(-22, 0) * k, new Vector2(100, 11) * k, m1, true);
                    break;
                }
                default:
                {
                    var bg = Add(parent, "Plate", th.rowBg, Vector2.zero, new Vector2(84, 50) * k, Color.Lerp(m1, e, 0.18f), true);
                    var go = new GameObject("T", typeof(RectTransform)); go.transform.SetParent(bg.transform, false);
                    var t = go.AddComponent<TMPro.TextMeshProUGUI>(); t.font = th.fontCalligraphy; t.fontSize = 34f * k; t.color = new Color32(0x1A, 0x10, 0x06, 255); t.alignment = TMPro.TextAlignmentOptions.Center; t.raycastTarget = false;
                    var a = go.AddComponent<ArabicText>(); a.Text = "لقب";
                    var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(0, 4); rt.offsetMax = Vector2.zero;
                    break;
                }
            }
        }
    }
}
