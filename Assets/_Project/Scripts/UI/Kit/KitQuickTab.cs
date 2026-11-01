using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MafiaNight.UI.Kit
{
    /// <summary>Hands the drag of the tab to <see cref="KitQuickMenu"/> (the tab is also its button: a plain press opens the menu).</summary>
    public sealed class KitQuickTab : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [NonSerialized] public KitQuickMenu owner;
        public void OnBeginDrag(PointerEventData e) { if (owner != null) owner.BeginDrag(e); }
        public void OnDrag(PointerEventData e) { if (owner != null) owner.Drag(e); }
        public void OnEndDrag(PointerEventData e) { if (owner != null) owner.EndDrag(e); }
    }
}
