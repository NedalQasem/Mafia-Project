using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>One row of a leaderboard: position, avatar, name + rank, value and movement arrow.</summary>
    public class LeaderRowView : MonoBehaviour
    {
        [SerializeField] private Image bg;
        [SerializeField] private ArabicText posLabel, nameLabel, rankLabel, valueLabel, deltaLabel;
        [SerializeField] private AvatarFrame avatar;
        [SerializeField] private Image arrow;

        public void Bind(LeaderEntry e, int position, bool paper)
        {
            var th = KitTheme.Current;
            Color text = paper ? th.paperInk : th.leatherText;
            posLabel.Text = CoinPill.Format(position);
            nameLabel.Text = e.name; rankLabel.Text = KitTheme.RankNames[Mathf.Clamp(e.rank, 0, 7)];
            valueLabel.Text = CoinPill.Format(e.value);
            avatar.Set(e.rank);
            nameLabel.GetComponent<TMPro.TMP_Text>().color = text;
            rankLabel.GetComponent<TMPro.TMP_Text>().color = paper ? new Color32(0x6A, 0x5A, 0x3A, 255) : th.ivoryDim;
            posLabel.GetComponent<TMPro.TMP_Text>().color = text;
            valueLabel.GetComponent<TMPro.TMP_Text>().color = paper ? th.crimson : new Color32(0xFF, 0xD9, 0x8A, 255);
            bool up = e.delta > 0, down = e.delta < 0;
            arrow.gameObject.SetActive(up || down);
            arrow.transform.localRotation = Quaternion.Euler(0, 0, down ? 180f : 0f);
            Color c = up ? (paper ? new Color32(0x1D, 0x6A, 0x4C, 255) : new Color32(0x7F, 0xE0, 0xB8, 255)) : (paper ? new Color32(0xA0, 0x26, 0x2E, 255) : new Color32(0xE5, 0x7A, 0x82, 255));
            arrow.color = c;
            deltaLabel.Text = up || down ? CoinPill.Format(Mathf.Abs(e.delta)) : "";
            deltaLabel.GetComponent<TMPro.TMP_Text>().color = c;
            bg.color = e.me ? new Color(0.54f, 0.11f, 0.14f, 0.45f) : (paper ? new Color(0.11f, 0.09f, 0.06f, 0.07f) : new Color(1, 1, 1, 0.012f));
        }
    }
}
