using UnityEngine;
using static MafiaNight.UI.Kit.Editor.KitProceduralTextures;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>Top bar medal: dark brass-rimmed disc and the thin ring that fills with XP.</summary>
    public static class KitProceduralTextures6
    {
        public static void Generate()
        {
            Make("xp_ring", 300, 300, (x, y) =>
            {
                float r = Mathf.Sqrt((x - 150f) * (x - 150f) + (y - 150f) * (y - 150f));
                return (r >= 127f && r <= 148f) ? Color.white : Color.clear;
            });

            Make("medal_disc", 300, 300, (x, y) =>
            {
                float dx = x - 150f, dy = y - 150f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 150f) return Color.clear;
                if (r > 128f)   // brass rim
                {
                    float t = Mathf.Clamp01((y - 20f) / 260f);
                    return Color.Lerp(C(0x9c7a3a), C(0x2c200e), t);
                }
                if (r > 124f) return C(0x050404);
                float k = Mathf.Clamp01(r / 124f);
                Color c = Color.Lerp(C(0x2e2a26), C(0x0d0b0a), k * k);
                float hl = Mathf.Clamp01(1f - Mathf.Sqrt((x - 105f) * (x - 105f) + (y - 90f) * (y - 90f)) / 90f);
                return Over(C(0xffffff, hl * 0.07f), c);
            });
            UnityEditor.AssetDatabase.Refresh();
        }
    }
}
