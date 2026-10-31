using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Keeps this rect at the middle of another rect's slot with a fixed shape (width : height), as large as fits inside it with a margin.
    /// The offer picture of the launch banner uses it: the page gives its art slot whatever height the screen leaves, but the picture is 13 : 8,
    /// so it is shown whole (never cropped) in the middle of the slot. The target's own scale is ignored (the banner scales its art 1.5 times).
    /// </summary>
    public sealed class KitFollowRect : MonoBehaviour
    {
        public RectTransform target;
        public float aspect = 13f / 8f;
        public float margin;

        private void LateUpdate()
        {
            if (target == null) return;
            var me = (RectTransform)transform;
            var size = target.rect.size;
            float w = Mathf.Max(0f, size.x - 2f * margin), h = Mathf.Max(0f, size.y);
            if (w / aspect > h) w = h * aspect; else h = w / aspect;
            me.anchorMin = target.anchorMin; me.anchorMax = target.anchorMax; me.pivot = target.pivot;      // the page's layout moves the slot's anchors after this was made
            me.sizeDelta = new Vector2(w, h);
            me.anchoredPosition = target.anchoredPosition;
        }
    }
}
