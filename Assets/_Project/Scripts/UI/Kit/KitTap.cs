using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>A transparent tap area over any part of a frame (the frames are pictures, so buttons and seats get their click from this).</summary>
    public sealed class KitTap : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public static KitTap On(Transform t, Action clicked)
        {
            var tap = t.GetComponent<KitTap>();
            if (tap == null)
            {
                var hit = t.GetComponent<Image>();
                if (hit == null) { hit = t.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); }
                hit.raycastTarget = true;
                tap = t.gameObject.AddComponent<KitTap>();
            }
            tap.Clicked = clicked;
            return tap;
        }

        public void OnPointerClick(PointerEventData e)
        {
            MafiaNight.Core.GameJournal.Tap(NameOf(transform));
            Clicked?.Invoke();
        }

        /// <summary>Where the tap was, for the test journal: the object and the two above it (e.g. "Mic < Row < Bar").</summary>
        private static string NameOf(Transform t)
        {
            string s = t.name;
            var p = t.parent;
            for (int i = 0; i < 2 && p != null; i++, p = p.parent) s += " < " + p.name;
            return s;
        }
    }
}
