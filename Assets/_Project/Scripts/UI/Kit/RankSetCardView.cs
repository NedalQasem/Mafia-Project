using System;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>One rank card in the store's rank-exclusive drawer (store.html .rkc): medal, name, status chip and five exclusive items.</summary>
    public sealed class RankSetCardView : MonoBehaviour
    {
        [SerializeField] private RectTransform medalHolder;
        [SerializeField] private ArabicText numberLabel, nameLabel, subLabel, statusLabel;
        [SerializeField] private Image statusChip, currentOutline;
        [SerializeField] private RectTransform[] slots = new RectTransform[5];

        public event Action<int, int> OnItem;   // rank index (0-based), item type (0-4)

        public void Bind(int rankIndex, int playerRank)
        {
            var r = RankSets.All[rankIndex];
            int n = rankIndex + 1;
            bool locked = n > playerRank, current = n == playerRank;
            var th = KitTheme.Current;

            for (int i = medalHolder.childCount - 1; i >= 0; i--)
            {
                var c = medalHolder.GetChild(i);
                if (c.name == "Medal") DestroyImmediate(c.gameObject);
            }
            RankArt.Medal(medalHolder, r, locked, Vector2.zero, 122f);
            numberLabel.Text = CoinPill.Format(n);
            numberLabel.GetComponent<TMPro.TMP_Text>().color = locked ? new Color(.3f, .3f, .3f) : r.e;
            nameLabel.Text = r.name;
            nameLabel.GetComponent<TMPro.TMP_Text>().color = locked ? new Color32(0x8F, 0x87, 0x77, 255) : (Color)new Color32(0xF0, 0xE6, 0xCC, 255);
            subLabel.Text = "الرتبة " + CoinPill.Format(n) + " · خمسة عناصر حصرية";

            bool ok = !locked;
            statusLabel.Text = current ? "رتبتك" : locked ? "مقفلة" : "مفتوحة";
            statusLabel.GetComponent<TMPro.TMP_Text>().color = ok ? new Color32(0x8F, 0xE0, 0xC6, 255) : (Color)new Color32(0xA7, 0x9D, 0x88, 255);
            statusChip.color = ok ? new Color32(0x2D, 0x7A, 0x60, 255) : new Color(1f, 1f, 1f, 0.13f);

            if (currentOutline != null) { currentOutline.gameObject.SetActive(current); currentOutline.color = r.m1; }

            for (int t = 0; t < slots.Length; t++)
            {
                int type = t;
                var slot = slots[t];
                RankArt.Build((RectTransform)slot.Find("Thumb"), r, type, locked, 1f);
                var label = slot.Find("Label").GetComponent<ArabicText>();
                label.Text = r.items[t];
                label.GetComponent<TMPro.TMP_Text>().color = locked ? new Color32(0x6F, 0x68, 0x5C, 255) : (Color)new Color32(0xA7, 0x9D, 0x88, 255);
                var b = slot.GetComponent<Button>();
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => OnItem?.Invoke(rankIndex, type));
            }
        }
    }
}
