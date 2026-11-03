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
            long before = KitProc.CachedBytes;
            KitProc.Trim(0);
            Resources.UnloadUnusedAssets();
            GameJournal.Event("Memory", "the phone is low on memory: dropped " + (before >> 20) + " MB of painted sprites and unused assets");
        }
    }
}
