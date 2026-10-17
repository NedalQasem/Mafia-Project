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
