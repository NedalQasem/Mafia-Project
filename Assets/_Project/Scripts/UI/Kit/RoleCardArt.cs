using System;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>The fixed portrait of each role's card (Art/Cards/Portraits/portrait_&lt;id&gt;): one asset, Resources/Kit/RoleCardArt, so the portraits are loaded only when a card is shown.</summary>
    [CreateAssetMenu(fileName = "RoleCardArt", menuName = "Mafia Night/Role Card Art")]
    public class RoleCardArt : ScriptableObject
    {
        [Serializable] public struct Entry { public string id; public Sprite portrait; }

        public Entry[] portraits = new Entry[0];

        private static RoleCardArt cached;
        public static RoleCardArt Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<RoleCardArt>("Kit/RoleCardArt");
                return cached;
            }
        }

        public Sprite Portrait(string id)
        {
            foreach (var e in portraits) if (e.id == id) return e.portrait;
            return null;
        }
    }
}
