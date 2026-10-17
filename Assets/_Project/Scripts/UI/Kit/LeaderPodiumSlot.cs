using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>A podium position (1st, 2nd or 3rd): avatar, name, value, rank and place medal.</summary>
    public class LeaderPodiumSlot : MonoBehaviour
    {
        [SerializeField] private AvatarFrame avatar;
        [SerializeField] private ArabicText nameLabel, valueLabel, rankLabel, placeLabel;
        [SerializeField] private GameObject crown;

        public void Bind(LeaderEntry e, int place, bool paper)
        {
            var th = KitTheme.Current;
            avatar.Set(e.rank);
            nameLabel.Text = e.name; valueLabel.Text = CoinPill.Format(e.value); rankLabel.Text = KitTheme.RankNames[Mathf.Clamp(e.rank, 0, 7)];
            placeLabel.Text = CoinPill.Format(place);
            if (crown != null) crown.SetActive(place == 1);
            nameLabel.GetComponent<TMPro.TMP_Text>().color = paper ? th.paperInk : th.leatherText;
            rankLabel.GetComponent<TMPro.TMP_Text>().color = paper ? new Color32(0x6A, 0x5A, 0x3A, 255) : th.ivoryDim;
            valueLabel.GetComponent<TMPro.TMP_Text>().color = paper ? th.crimson : new Color32(0xFF, 0xD9, 0x8A, 255);
        }
    }
}
