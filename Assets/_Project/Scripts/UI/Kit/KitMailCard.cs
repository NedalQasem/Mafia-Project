using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// One telegram of the mail (Docs/mockups/mailbox.html): a sheet with a dashed edge, who sent it and when, the title and text, the chips of the
    /// package, the expiry and the "collect" button. Built by <c>KitBuilder</c> ("MailCard" prefab), filled by <see cref="KitMailBoard"/>.
    /// </summary>
    public sealed class KitMailCard : MonoBehaviour
    {
        [SerializeField] private Sprite greenFace;

        public event Action<KitMailBoard.Card> OnClaim;

        private KitMailBoard.Card card;
        private ArabicText from, when, title, body, expiry, actionLabel, coinText, gemText;
        private GameObject dot, stamp, chips, coinChip, gemChip, itemChipTemplate, actionRow, expiryGo;
        private Image paper, actionFace;
        private Button action;
        private readonly List<GameObject> itemChips = new List<GameObject>();
        private bool found;

        private Transform T(string path) => transform.Find(path);
        private ArabicText A(string path) => T(path).GetComponent<ArabicText>();

        private void Find()
        {
            if (found) return;
            found = true;
            paper = GetComponent<Image>();
            from = A("From/Who"); when = A("From/When"); title = A("Title"); body = A("Body");
            dot = T("Deco/Dot").gameObject; stamp = T("Deco/Stamp").gameObject;
            chips = T("Chips").gameObject; coinChip = T("Chips/Coin").gameObject; gemChip = T("Chips/Gem").gameObject; itemChipTemplate = T("Chips/Item").gameObject;
            coinText = A("Chips/Coin/Text"); gemText = A("Chips/Gem/Text");
            actionRow = T("Row").gameObject; expiry = A("Row/Expiry"); expiryGo = T("Row/Expiry").gameObject;
            action = T("Row/Action").GetComponent<Button>(); actionFace = T("Row/Action/Face").GetComponent<Image>(); actionLabel = A("Row/Action/Face/Label");
            action.onClick.AddListener(() => { if (card != null && !card.claimed) OnClaim?.Invoke(card); });
            itemChipTemplate.SetActive(false);
        }

        public KitMailBoard.Card Card => card;

        public void Bind(KitMailBoard.Card c, int index)
        {
            Find();
            card = c;
            transform.localRotation = Quaternion.Euler(0f, 0f, index % 2 == 0 ? -0.6f : 0.5f);
            paper.color = c.claimed ? new Color32(0xB9, 0xB2, 0xA0, 255) : Color.white;
            from.Text = "من: " + (c.from ?? "مكتب الريّس"); when.Text = c.when ?? "";
            title.Text = c.title ?? ""; body.Text = c.body ?? "";
            body.gameObject.SetActive(!string.IsNullOrEmpty(c.body));
            if (!string.IsNullOrEmpty(c.body)) body.GetComponent<LayoutElement>().preferredHeight = 14f + Mathf.Max(1, Mathf.CeilToInt(c.body.Length / 42f)) * 46f;

            bool pack = c.coins > 0 || c.gems > 0 || (c.items != null && c.items.Length > 0);
            chips.SetActive(pack);
            coinChip.SetActive(c.coins > 0); if (c.coins > 0) coinText.Text = CoinPill.Format(c.coins);
            gemChip.SetActive(c.gems > 0); if (c.gems > 0) gemText.Text = CoinPill.Format(c.gems);
            foreach (var g in itemChips) Destroy(g);
            itemChips.Clear();
            if (c.items != null)
                foreach (var name in c.items)
                {
                    var g = Instantiate(itemChipTemplate, itemChipTemplate.transform.parent, false);
                    g.SetActive(true); g.GetComponentInChildren<ArabicText>().Text = name;
                    itemChips.Add(g);
                }

            dot.SetActive(!c.claimed); stamp.SetActive(c.claimed);
            action.gameObject.SetActive(!c.claimed);
            actionFace.sprite = greenFace; actionLabel.Text = pack ? "استلم" : "تمام";
            expiryGo.SetActive(!string.IsNullOrEmpty(c.expiry));
            expiry.Text = c.claimed ? "انستلمت" : c.expiry ?? "";
            if (c.claimed) { expiryGo.SetActive(true); }
            actionRow.SetActive(!c.claimed || pack);
        }

        public void SetEnabled(bool on) { action.interactable = on; }
    }
}
