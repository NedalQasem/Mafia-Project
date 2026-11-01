using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>The store's chat bubbles (StoreCatalog "bubbles", by variant) as colors: 0 plain leather (no change), 1 note paper, 2 torn ticket, 3 typewriter, 4 brass plate, 5 crimson velvet.</summary>
    public static class KitBubbleLooks
    {
        public struct Look { public Color Fill, Edge, Text, Name; public Look(Color f, Color e, Color t, Color n) { Fill = f; Edge = e; Text = t; Name = n; } }

        private static readonly Look[] Looks =
        {
            default,
            new Look(new Color32(0xD9, 0xCF, 0xB4, 255), new Color32(0x1C, 0x17, 0x0F, 0x55), new Color32(0x1C, 0x17, 0x0F, 255), new Color32(0x4A, 0x3C, 0x24, 255)),
            new Look(new Color32(0xE9, 0xDB, 0xB4, 255), new Color32(0x8A, 0x1C, 0x24, 0x88), new Color32(0x2A, 0x1E, 0x10, 255), new Color32(0x8A, 0x1C, 0x24, 255)),
            new Look(new Color32(0xC9, 0xC5, 0xB8, 255), new Color32(0x2A, 0x26, 0x20, 0x88), new Color32(0x1E, 0x1B, 0x16, 255), new Color32(0x4A, 0x45, 0x3A, 255)),
            new Look(new Color32(0xB9, 0x90, 0x48, 255), new Color32(0x6E, 0x52, 0x24, 255), new Color32(0x2A, 0x1A, 0x08, 255), new Color32(0x3A, 0x28, 0x0C, 255)),
            new Look(new Color32(0x6A, 0x14, 0x20, 255), new Color32(0xC0, 0x32, 0x3A, 0xAA), new Color32(0xF3, 0xDC, 0xD8, 255), new Color32(0xF5, 0xE2, 0xA8, 255)),
        };

        public static bool Has(int variant) => variant > 0 && variant < Looks.Length;
        public static Look Of(int variant) => Looks[Mathf.Clamp(variant, 0, Looks.Length - 1)];
    }
}
