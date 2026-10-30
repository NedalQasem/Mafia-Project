using System;
using System.Collections;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Where the kit gets the pictures of private messages: the app sets <see cref="Loader"/> (it downloads the picture by its id and caches it),
    /// because the kit itself knows nothing about the server.
    /// </summary>
    public static class KitImages
    {
        /// <summary>Loads the picture with this id; the callback gets null when it fails.</summary>
        public static Func<string, Action<Texture2D>, IEnumerator> Loader;
    }
}
