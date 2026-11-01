using System;
using System.Collections;
using System.Collections.Generic;
using MafiaNight.UI.Animations;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The small tabs inside a screen (bag kinds, store categories, family chats): the shown page burns away to the next one, quicker and lighter than
    /// the screen change (0.4 s, fewer sparks, no smoke). Follows the player's choice in settings (burn, simple fade, or nothing).
    /// </summary>
    public static class KitTabBurn
    {
        private static readonly HashSet<RectTransform> busy = new HashSet<RectTransform>();

        /// <summary>Runs <paramref name="swap"/> (which draws the new page) and burns the picture of the old page of <paramref name="area"/> away over it. With <paramref name="below"/> only the part under that element burns (the tabs stay).</summary>
        public static void Swap(MonoBehaviour host, RectTransform area, Action swap, BurnCorner corner = BurnCorner.BottomRight, RectTransform below = null)
        {
            if (host == null || area == null || !host.isActiveAndEnabled || BurnSettings.Mode == BurnSettings.Off || busy.Contains(area)) { swap(); return; }
            host.StartCoroutine(Run(area, swap, corner, below));
        }

        private static IEnumerator Run(RectTransform area, Action swap, BurnCorner corner, RectTransform below)
        {
            busy.Add(area);
            try
            {
                Texture2D snap = null;
                yield return BurnTransition.Capture(area, t => snap = t, below);
                swap();
                if (snap == null) yield break;
                if (area == null) { UnityEngine.Object.Destroy(snap); yield break; }
                yield return BurnTransition.Attach(area, snap, below).Play(corner, 0.4f, true);
            }
            finally { busy.Remove(area); }
        }
    }
}
