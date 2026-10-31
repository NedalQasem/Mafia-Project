using System;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// One event of the events board: a paper clipping with a stamp (Docs/mockups/events.html). It is built by <c>KitBuilder</c> ("EventCard" prefab)
    /// and filled by <see cref="KitEventsBoard"/> through <see cref="Bind"/>. The parts are found by name once, so the prefab can be rebuilt freely.
    /// </summary>
    public sealed class KitEventCard : MonoBehaviour
    {
        private static readonly Color PaperSoon = new Color32(0xCF, 0xC5, 0xA8, 255), PaperDone = new Color32(0xB9, 0xB2, 0xA0, 255);
        private static readonly Color Crimson = new Color32(0xC0, 0x32, 0x3A, 255), Green = new Color32(0x1D, 0x4A, 0x3C, 255), Brown = new Color32(0x6E, 0x4B, 0x1C, 255);

        [SerializeField] private Sprite redFace, greenFace;

        /// <summary>The action button was pressed: the event's id.</summary>
        public event Action<KitEventsBoard.Card> OnAction;
        /// <summary>The "ranking" button of an event that has one.</summary>
        public event Action<KitEventsBoard.Card> OnRank;

        private KitEventsBoard.Card card;
        private Image paper, stampFrame, actionFace;
        private ArabicText kicker, number, title, body, stampText, countLabel, countValue, rewardValue, rewardLabel, tallyCount, actionLabel;
        private GameObject stamp, bigStamp, tape, clip, ring, burn, typedRow, redactRow, tallyRow, countRow, rewardRow, coin;
        private Image[] slots = new Image[10];
        private RectTransform[] strikes = new RectTransform[2];
        private GameObject rankRow;
        private ArabicText rankNumber, rankLabel;
        private Button action, rankButton;
        private CanvasGroup actionGroup;
        private float left;
        private RectTransform pic;               // the picture of the event (from the admin board), edge to edge at the top of the clipping
        private int baseTop = -1;
        private const float PicHeight = 410f;      // events.html: 150px of the 328px card, in canvas units
        private int shown = -1;
        private bool found;

        private Transform T(string path) => transform.Find(path);
        private ArabicText A(string path) => T(path).GetComponent<ArabicText>();

        private void Find()
        {
            if (found) return;
            found = true;
            paper = GetComponent<Image>();
            kicker = A("Kick/Kicker"); number = A("Kick/Number"); title = A("TitleRow/Title"); body = A("Body");
            stamp = T("Deco/Stamp").gameObject; stampFrame = stamp.GetComponent<Image>(); stampText = A("Deco/Stamp/Text");
            bigStamp = T("Deco/BigStamp").gameObject; tape = T("Deco/Tape").gameObject; clip = T("Deco/Clip").gameObject; ring = T("Deco/Ring").gameObject; burn = T("Deco/Burn").gameObject;
            typedRow = T("TypedRow").gameObject; redactRow = T("Redact").gameObject; tallyRow = T("Tally").gameObject; countRow = T("Count").gameObject; rewardRow = T("Reward").gameObject;
            countLabel = A("Count/Label"); countValue = A("Count/Box/Value");
            tallyCount = A("Tally/Count");
            for (int i = 0; i < 10; i++) slots[i] = T("Tally/Marks/M" + i).GetComponent<Image>();
            for (int g = 0; g < 2; g++) strikes[g] = (RectTransform)T("Tally/Marks/Strike" + g);
            rewardValue = A("Reward/Value"); rewardLabel = A("Reward/Label"); coin = T("Reward/Coin").gameObject;
            action = T("Reward/Action").GetComponent<Button>(); actionFace = T("Reward/Action/Face").GetComponent<Image>(); actionLabel = A("Reward/Action/Face/Label");
            actionGroup = action.GetComponent<CanvasGroup>();
            rankRow = T("Rank").gameObject; rankNumber = A("Rank/Number"); rankLabel = A("Rank/Label"); rankButton = T("Rank/Go").GetComponent<Button>();
            rankButton.onClick.AddListener(() => { if (card != null) OnRank?.Invoke(card); });
            action.onClick.AddListener(() => { if (card != null) OnAction?.Invoke(card); });
        }

        public KitEventsBoard.Card Card => card;

        public void Bind(KitEventsBoard.Card c, int index)
        {
            Find();
            card = c;
            ShowPicture(c);
            bool live = c.state == 0, soon = c.state == 1, done = c.state == 2;
            bool challenge = c.kind == 2, bonus = c.kind == 1;
            transform.localRotation = Quaternion.Euler(0f, 0f, index % 2 == 0 ? 0.7f : -0.6f);
            paper.color = soon ? PaperSoon : done ? PaperDone : Color.white;

            kicker.Text = KitText.Clip(c.kicker, 30); number.Text = "الحدث " + CoinPill.Format(index + 1);
            title.Text = KitText.Clip(c.title, 30);
            // one line whatever the length: the title shrinks to fit, keeping clear of the stamp; a long kicker shrinks too
            { var tt = title.GetComponent<TMPro.TMP_Text>(); tt.enableAutoSizing = true; tt.fontSizeMin = 38f; tt.fontSizeMax = 58f; tt.margin = new Vector4(c.state == 2 ? 0f : 250f, 0, 0, 0); }
            { var kt = kicker.GetComponent<TMPro.TMP_Text>(); kt.enableAutoSizing = true; kt.fontSizeMin = 20f; kt.fontSizeMax = 26f; kt.margin = new Vector4(0, 0, 0, 0); }
            body.Text = c.body ?? "";
            int lines = Mathf.Max(1, Mathf.CeilToInt((c.body ?? "").Length / 42f));
            body.GetComponent<LayoutElement>().preferredHeight = 14f + lines * 46f;

            // the stamp: red while it runs, green once collected, brown while it is only announced; a finished one gets the big green stamp
            stamp.SetActive(!done);
            if (!done)
            {
                bool took = challenge && c.claimed;
                var col = took ? Green : soon ? Brown : Crimson;
                stampText.Text = took ? "تمّ" : soon ? "قريباً" : "شغّال";
                stampFrame.color = col; stampText.GetComponent<TMPro.TMP_Text>().color = col;
            }
            bigStamp.SetActive(done);
            tape.SetActive(!challenge); clip.SetActive(challenge); ring.SetActive(bonus && live); burn.SetActive(done);

            rankRow.SetActive(c.hasRank && !c.secret);
            if (c.hasRank)
            {
                rankNumber.Text = c.rankMine > 0 ? CoinPill.Format(c.rankMine) : "—";
                rankLabel.Text = c.rankMine > 0 ? "مركزك\n" + CoinPill.Format(c.rankValue) + " " + KitEventsBoard.MetricName(c.rankMetric) : "لسا ما دخلت\nالتصنيف";
            }
            typedRow.SetActive(c.secret); redactRow.SetActive(c.secret);
            tallyRow.SetActive(challenge && !c.secret && !done && c.goal > 0);
            if (tallyRow.activeSelf) Tally(c);

            left = c.seconds; shown = -1;
            countRow.SetActive(!done && c.seconds > 0);
            countLabel.Text = soon ? "يبدأ بعد" : "ينتهي خلال";
            UpdateClock(true);

            // reward line and the button
            bool reward = !done && !c.secret && (bonus || (challenge && c.reward > 0));
            rewardRow.SetActive(reward);
            if (reward)
            {
                coin.SetActive(true);
                if (bonus) { rewardValue.Text = "×" + CoinPill.Format(c.multiplier); rewardLabel.Text = "على كل ربح"; }
                else { rewardValue.Text = CoinPill.Format(c.reward); rewardLabel.Text = "مكافأة"; }
                bool showButton = live && (bonus || challenge);
                action.gameObject.SetActive(showButton);
                if (showButton)
                {
                    if (bonus) { actionFace.sprite = redFace; actionLabel.Text = "العب الآن"; SetEnabled(true); }
                    else { actionFace.sprite = greenFace; actionLabel.Text = c.claimed ? "تم الاستلام" : "استلم"; SetEnabled(c.claimable && !c.claimed); }
                }
            }
        }

        private void ShowPicture(KitEventsBoard.Card c)
        {
            var v = GetComponent<VerticalLayoutGroup>();
            if (baseTop < 0) baseTop = v.padding.top;
            bool has = !string.IsNullOrEmpty(c.image);
            if (pic == null)
            {
                var g = new GameObject("Pic", typeof(RectTransform), typeof(LayoutElement)); pic = (RectTransform)g.transform; pic.SetParent(transform, false);
                g.GetComponent<LayoutElement>().ignoreLayout = true;
                pic.anchorMin = new Vector2(0f, 1f); pic.anchorMax = new Vector2(1f, 1f); pic.pivot = new Vector2(0.5f, 1f);
                pic.offsetMin = new Vector2(14f, -PicHeight); pic.offsetMax = new Vector2(-14f, -14f);
                pic.SetAsFirstSibling();                       // under the stamp, tape and clip
            }
            pic.gameObject.SetActive(has);
            v.padding.top = baseTop + (has ? (int)PicHeight - 14 : 0);
            if (!has) { var old = pic.GetComponentInChildren<KitPoster>(true); if (old != null) Destroy(old.gameObject); return; }
            KitPoster.Show(this, pic, c.image, ok =>
            {   // the picture could not be had: the clipping goes back to how it was
                if (!ok && card == c && pic != null) { pic.gameObject.SetActive(false); v.padding.top = baseTop; }
            });
        }

        // a disabled button turns grey like the mockup's, instead of fading into the paper
        public void SetEnabled(bool on)
        {
            actionFace.color = on ? Color.white : new Color(0.38f, 0.38f, 0.38f, 1f);
            actionLabel.GetComponent<TMPro.TMP_Text>().color = on ? new Color32(0xF0, 0xDC, 0xD8, 255) : new Color(0.72f, 0.72f, 0.72f, 1f);
            actionGroup.alpha = 1f; action.interactable = on;
        }

        private void Tally(KitEventsBoard.Card c)
        {
            int n = Mathf.Clamp(c.progress, 0, 10);
            for (int i = 0; i < 10; i++)
            {
                var img = slots[i];
                bool f = i < n;
                img.color = f ? new Color32(0x24, 0x1C, 0x10, 255) : new Color(0.11f, 0.09f, 0.06f, 0.26f);
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, f ? ((i * 37 % 5) - 2) * 0.8f : 0f);
            }
            for (int g = 0; g < 2; g++) strikes[g].gameObject.SetActive(n >= (g + 1) * 5);
            tallyCount.Text = CoinPill.Format(c.progress) + " من " + CoinPill.Format(c.goal) + (c.countsGames ? " مباراة" : "");
        }

        private void Update() { Tick(Time.unscaledDeltaTime); }

        private void Tick(float dt)
        {
            if (card == null || left <= 0f) return;
            left = Mathf.Max(0f, left - dt);
            UpdateClock(false);
        }

        private static string Clock(int s) => CoinPill.Format(s / 3600).PadLeft(2, '٠') + ":" + CoinPill.Format(s % 3600 / 60).PadLeft(2, '٠') + ":" + CoinPill.Format(s % 60).PadLeft(2, '٠');

        private void UpdateClock(bool force)
        {
            if (card == null) return;
            int s = Mathf.CeilToInt(left);
            if (!force && s == shown) return;
            shown = s;
            countValue.Text = s >= 2 * 86400 ? CoinPill.Format(s / 86400) + " أيام" : Clock(s);
            if (s <= 0 && countRow.activeSelf) countRow.SetActive(false);
        }
    }
}
