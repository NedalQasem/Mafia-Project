using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>Rank medal (8 ranks) + three bullet pips showing the tier inside the rank.</summary>
    public class RankMedalView : MonoBehaviour
    {
        [SerializeField] private Image medal;
        [SerializeField] private Image[] pips = new Image[3];
        [SerializeField, Range(0, 7)] private int rank;
        [SerializeField, Range(0, 3)] private int filledPips = 1;

        public void Set(int rankIndex, int tiersFilled)
        {
            rank = Mathf.Clamp(rankIndex, 0, 7);
            filledPips = Mathf.Clamp(tiersFilled, 0, 3);
            Apply();
        }

        private void OnEnable() { Apply(); }
#if UNITY_EDITOR
        private void OnValidate() { if (isActiveAndEnabled) Apply(); }
#endif

        private void Apply()
        {
            var th = KitTheme.Current;
            if (th == null) return;
            if (medal != null && rank < th.medalByRank.Length) medal.sprite = th.medalByRank[rank];
            for (int i = 0; i < pips.Length; i++)
                if (pips[i] != null) pips[i].sprite = i < filledPips ? th.bulletOn : th.bulletOff;
        }
    }
}
