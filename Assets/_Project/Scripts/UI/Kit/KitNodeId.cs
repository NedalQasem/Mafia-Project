using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>Marks a part of a frame built from a mockup with its number in frames.json, so code can find "text 58 of frame p2_mafia" without searching by name.</summary>
    public sealed class KitNodeId : MonoBehaviour
    {
        public int Index;
    }
}
