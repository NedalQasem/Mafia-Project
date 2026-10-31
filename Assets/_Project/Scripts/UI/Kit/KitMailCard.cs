using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// One telegram of the mail (Docs/mockups/mailbox.html): a sheet with a dashed edge, who sent it and when, the title and text, the chips of the
    /// package, the expiry and the "collect" button. A private message from the office (Docs/mockups/direct-message.html) uses the same sheet: a
    /// note gets a small photo, a warning gets the hazard tape, a red edge and a "تحذير" stamp, and its button opens the letter instead of collecting.
    /// Built by <c>KitBuilder</c> ("MailCard" prefab), filled by <see cref="KitMailBoard"/>.
    /// </summary>
    public sealed class KitMailCard : MonoBehaviour
    {
        [SerializeField] private Sprite greenFace, redFace;

        public event Action<KitMailBoard.Card> OnClaim;

        private KitMailBoard.Card card;
        private ArabicText from, when, title, body, expiry, actionLabel, coinText, gemText, stampText;
        private GameObject dot, stamp, chips, coinChip, gemChip, itemChipTemplate, actionRow, expiryGo, tape, band, thumb;
        private Image paper, actionFace, stampImage;
        private RawImage thumbPic;
        private Button action;
        private readonly List<GameObject> itemChips = new List<GameObject>();
        private bool found;

        private static readonly Color Ink = new Color32(0x1C, 0x17, 0x0F, 255), Crim = new Color32(0x8A, 0x1C, 0x24, 255), Green = new Color32(0x1D, 0x4A, 0x3C, 255), StampRed = new Color32(0xC0, 0x32, 0x3A, 255);

        private Transform T(string path) => transform.Find(path);
        private ArabicText A(string path) => T(path).GetComponent<ArabicText>();

        private void Find()
        {
            if (found) return;
            found = true;
            paper = GetComponent<Image>();
            from = A("From/Who"); when = A("From/When"); title = A("Title"); body = A("Body");
            dot = T("Deco/Dot").gameObject; stamp = T("Deco/Stamp").gameObject; stampText = A("Deco/Stamp/Text"); stampImage = stamp.GetComponent<Image>();
            tape = T("Deco/Tape").gameObject; band = T("Deco/Band").gameObject; thumb = T("Deco/Thumb").gameObject; thumbPic = T("Deco/Thumb/Pic").GetComponent<RawImage>();
            stamp.transform.SetAsLastSibling();       // the stamp lies over the photo
            chips = T("Chips").gameObject; coinChip = T("Chips/Coin").gameObject; gemChip = T("Chips/Gem").gameObject; itemChipTemplate = T("Chips/Item").gameObject;
            coinText = A("Chips/Coin/Text"); gemText = A("Chips/Gem/Text");
            actionRow = T("Row").gameObject; expiry = A("Row/Expiry"); expiryGo = T("Row/Expiry").gameObject;
            action = T("Row/Action").GetComponent<Button>(); actionFace = T("Row/Action/Face").GetComponent<Image>(); actionLabel = A("Row/Action/Face/Label");
            action.onClick.AddListener(() => { if (card != null && (card.type != 0 || !card.claimed)) OnClaim?.Invoke(card); });
            itemChipTemplate.SetActive(false);
        }

        public KitMailBoard.Card Card => card;

        public void Bind(KitMailBoard.Card c, int index)
        {
            Find();
            card = c;
            bool msg = c.type != 0, warn = c.type == 2;
            transform.localRotation = Quaternion.Euler(0f, 0f, index % 2 == 0 ? -0.6f : 0.5f);
            Color paperColor = warn ? new Color32(0xFC, 0xF7, 0xF2, 255) : Color.white;
            paper.color = c.claimed ? Color.Lerp(paperColor, new Color32(0xB9, 0xB2, 0xA0, 255), .75f) : paperColor;

            from.Text = (msg ? (warn ? "تحذير · من: " : "رسالة خاصة · من: ") : "من: ") + (c.from ?? "مكتب الريّس"); when.Text = c.when ?? "";
            from.GetComponent<TMPro.TMP_Text>().color = warn ? Crim : StampRed;
            title.Text = KitText.Clip(c.title, 64);
            { var tt = title.GetComponent<TMPro.TMP_Text>(); tt.enableAutoSizing = true; tt.fontSizeMin = 34f; tt.fontSizeMax = 54f; }       // a long title gets smaller instead of running off the card
            title.GetComponent<TMPro.TMP_Text>().color = warn ? Crim : Ink;

            // the text: a message shows two lines, next to its photo when it has one
            bool pic = msg && !string.IsNullOrEmpty(c.imageId);
            var bodyText = body.GetComponent<TMPro.TMP_Text>();
            // a warning's stamp lands on its photo like a rubber stamp, or beside the text when there is no photo; the text keeps a fixed column either way
            bodyText.margin = new Vector4(warn && !pic ? (c.claimed ? 330f : 260f) : 0f, 0, pic ? 250f : 0f, 0);
            { var sr = (RectTransform)stamp.transform; sr.anchoredPosition = !warn ? new Vector2(200f, -150f) : pic ? new Vector2(752f, -286f) : new Vector2(200f, -300f); sr.localScale = Vector3.one * (warn && pic ? 0.72f : 1f); }
            // a message shows a short preview (the letter has the rest), so every card of a kind has the same shape whatever the text length
            string shown = c.body ?? "";
            if (msg) { int cut = pic ? 64 : 110; if (shown.Length > cut) shown = shown.Substring(0, cut).TrimEnd() + "…"; }
            body.Text = shown;
            body.gameObject.SetActive(!string.IsNullOrEmpty(shown));
            if (!string.IsNullOrEmpty(shown))
            {
                int chars = pic ? 22 : (warn ? 28 : 42), lines = Mathf.Max(1, Mathf.CeilToInt(shown.Length / (float)chars));
                body.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(pic ? 170f : 0f, 14f + lines * 46f);
            }
            thumb.SetActive(pic);
            if (pic) { thumbPic.texture = null; thumbPic.color = new Color(1f, 1f, 1f, 0f); if (KitImages.Loader != null) StartCoroutine(KitImages.Loader(c.imageId, tex => { if (this == null || tex == null) return; thumbPic.texture = tex; thumbPic.color = Color.white; })); }
            tape.SetActive(warn); band.SetActive(warn);

            bool pack = !msg && (c.coins > 0 || c.gems > 0 || (c.items != null && c.items.Length > 0));
            chips.SetActive(pack);
            coinChip.SetActive(c.coins > 0); if (c.coins > 0) coinText.Text = CoinPill.Format(c.coins);
            gemChip.SetActive(c.gems > 0); if (c.gems > 0) gemText.Text = CoinPill.Format(c.gems);
            foreach (var g in itemChips) Destroy(g);
            itemChips.Clear();
            if (c.items != null && !msg)
                foreach (var name in c.items)
                {
                    var g = Instantiate(itemChipTemplate, itemChipTemplate.transform.parent, false);
                    g.SetActive(true); g.GetComponentInChildren<ArabicText>().Text = name;
                    itemChips.Add(g);
                }

            // the stamp: "collected" for a package, "reviewed" for a read warning, a red "warning" for a new one
            bool showStamp = msg ? warn : c.claimed;
            stamp.SetActive(showStamp);
            if (showStamp)
            {
                var col = msg && !c.claimed ? StampRed : Green;
                stampText.Text = msg ? (c.claimed ? "تمّ الاطلاع" : "تحذير") : "تمّ الاستلام";
                stampText.GetComponent<TMPro.TMP_Text>().color = col; stampImage.color = col;
            }
            dot.SetActive(!c.claimed);

            // the button: collect for a package; open for a note; read for a warning that is still new (a read message can be opened again)
            action.gameObject.SetActive(msg || !c.claimed);
            actionFace.sprite = warn && !c.claimed && redFace != null ? redFace : greenFace;
            actionLabel.Text = msg ? (warn && !c.claimed ? "اقرأ" : "افتح") : (pack ? "استلم" : "تمام");
            expiryGo.SetActive(!string.IsNullOrEmpty(c.expiry) || c.claimed || msg);
            expiry.Text = msg ? (c.claimed ? "انقرأت" : (warn && c.must ? "لازم تقراها" : "اضغط لتقرأ")) : (c.claimed ? "انستلمت" : c.expiry ?? "");
            expiry.GetComponent<TMPro.TMP_Text>().color = msg && !c.claimed && warn ? Crim : new Color32(0x4A, 0x3C, 0x24, 255);
            actionRow.SetActive(msg || !c.claimed || pack);
        }

        public void SetEnabled(bool on) { action.interactable = on; }
    }
}
