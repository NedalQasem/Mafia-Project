using MafiaNight.Core;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// When the phone says it is running low on memory (Android's onTrimMemory reaches Unity as Application.lowMemory), let go of everything that can be made again:
    /// the painted sprites and whatever no screen uses. Better a screen that paints itself again than the system closing the game.
    /// </summary>
    internal static class MemoryGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() { Application.lowMemory += OnLowMemory; }

        private static void OnLowMemory()
        {
            // Android also reports memory trims when the player pulls down the notifications or leaves for the home screen: that must not stall the game in the middle of the gesture.
            // So the cheap part (half of the painted sprites) goes at once, and the heavy part (scanning every asset) only when the game is no longer in front.
            long before = KitProc.CachedBytes;
            KitProc.Trim(before / 2);
            bool heavy = !Application.isFocused;
            if (heavy) Resources.UnloadUnusedAssets();
            GameJournal.Event("Memory", "the phone reports low memory: dropped " + ((before - KitProc.CachedBytes) >> 20) + " MB of painted sprites" + (heavy ? " and unused assets" : ""));
        }
    }
}
