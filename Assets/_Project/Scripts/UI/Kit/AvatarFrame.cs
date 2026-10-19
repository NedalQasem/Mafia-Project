using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>Avatar disc with a rank-coloured ring and an optional online dot.</summary>
    public class AvatarFrame : MonoBehaviour
    {
        [SerializeField] private Image ring;
        [SerializeField] private Image avatar;
        [SerializeField] private GameObject onlineDot;
        [SerializeField, Range(0, 7)] private int rank;

        public void Set(int rankIndex, Sprite avatarSprite = null, bool online = false)
        {
            rank = Mathf.Clamp(rankIndex, 0, 7);
            if (avatarSprite != null && avatar != null) avatar.sprite = avatarSprite;
            if (onlineDot != null) onlineDot.SetActive(online);
            Apply();
        }

        /// <summary>
        /// Wears a store frame: it replaces the rank bezel (the ring) and becomes the outer ring of the avatar.
        /// The picture is sized so its ring is about as wide as this avatar's box (the ring is ~0.79 of the picture width).
        /// Null takes the frame off and brings the rank bezel back.
        /// </summary>
        public void SetFrame(Sprite frame)
        {
            if (avatar == null) return;
            var root = (RectTransform)transform;
            var existing = root.Find("StoreFrame");
            if (ring != null) ring.enabled = frame == null;
            var ar = avatar.rectTransform;
            if (!insetSaved) { insetMin = ar.offsetMin; insetMax = ar.offsetMax; insetSaved = true; }
            if (frame == null)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                ar.offsetMin = insetMin; ar.offsetMax = insetMax;   // back to the picture size the rank bezel expects
                return;
            }
            Image img;
            if (existing == null)
            {
                var go = new GameObject("StoreFrame", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root, false);
                img = go.GetComponent<Image>(); img.raycastTarget = false; img.preserveAspect = true;
                existing = go.transform;
            }
            else img = existing.GetComponent<Image>();
            existing.gameObject.SetActive(true);
            img.sprite = frame;
            var rt = (RectTransform)existing;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            float side = Mathf.Min(root.rect.width, root.rect.height) * 0.85f / 0.676f;   // 0.85 x box / ring width in the picture; measured ring ends up ~box wide
            rt.sizeDelta = new Vector2(side, side);
            rt.SetAsLastSibling();
            // the picture grows to fill the frame (no gap), tucked just under the ring
            float diameter = side * 0.69f;   // measured: the ring is ~0.79 of the picture, its inner edge ~0.69
            float insetX = (root.rect.width - diameter) * 0.5f, insetY = (root.rect.height - diameter) * 0.5f;
            ar.offsetMin = new Vector2(insetX, insetY); ar.offsetMax = new Vector2(-insetX, -insetY);
        }

        private bool insetSaved;
        private Vector2 insetMin, insetMax;

        private void OnEnable() { Apply(); }
#if UNITY_EDITOR
        private void OnValidate() { if (isActiveAndEnabled) Apply(); }
#endif

        private void Apply()
        {
            var th = KitTheme.Current;
            if (th == null || ring == null) return;
            if (rank < th.ringByRank.Length) ring.sprite = th.ringByRank[rank];
        }
    }
}
