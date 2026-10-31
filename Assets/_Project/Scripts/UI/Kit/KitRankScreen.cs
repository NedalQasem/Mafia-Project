using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MafiaNight.Data.Player;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// My rank (matches Docs/mockups/myrank.html): the membership card (medal, rank, the three bullets of the tier, the bar and how many wins
    /// are left), how points come, and the family ladder from the Godfather down to the errand boy. A tap on a rung opens that rank's sheet with
    /// its five exclusive items. The app gives it the player's points (RankFlow); sizes are the mockup's CSS px times K.
    /// </summary>
    public class KitRankScreen : MonoBehaviour
    {
        [Serializable]
        public class Rung
        {
            public Button button;
            public Image bg, medal, lockIcon;
            public TMP_Text title, desc, endTop, endBottom;
            public GameObject pin;
        }

        public const float K = 1080f / 390f;
        public const int Win = 50, Lose = 20;                  // points of a win / a loss: the server's config.json (rankWin / rankLose)
        private const int PerTier = RankLadder.PointsPerTier, PerRank = RankLadder.PointsPerTier * RankLadder.TiersPerRank, Max = RankLadder.MaxPoints;

        /// <summary>One line about each rank, in the ladder.</summary>
        public static readonly string[] Descriptions =
        {
            "بيوصّل الرسائل وبيفتح الأبواب. كل كبير بلّش من هون.", "معه مفتاح واحد، وبيعرف مين داخل ومين طالع.", "بينفّذ وما بيسأل. اسمه صار ينسمع بالحارة.", "بيقود جماعته بقبعته وكلمته.",
            "الكل بيرجعله قبل أي قرار.", "أقرب واحد للزعيم، وكلمته من كلمته.", "الحارة كلها تحت إيده.", "فوق الكل. كلمته ما بتنرد.",
        };
        private static readonly string[] Tiers = { "الأولى", "الثانية", "الثالثة" };

        [Header("Card")]
        [SerializeField] private TMP_Text cardNumber, playerName, tierText, progLeft, progRight, stampText;
        [SerializeField] private Image medal, nameImg;
        [SerializeField] private Image[] bullets = new Image[3];
        [SerializeField] private RectTransform fill;
        [SerializeField] private RectTransform stamp;
        [SerializeField] private Sprite[] medals = new Sprite[8], medalsLocked = new Sprite[8], names = new Sprite[8];
        [SerializeField] private Sprite bulletOn, bulletOff, rungNormal, rungLock, rungNow, tileOpen, tileLocked;
        [Header("Ladder")]
        [SerializeField] private Rung[] rungs = new Rung[8];     // index 0 = the Godfather (top of the ladder)
        [Header("Rank sheet")]
        [SerializeField] private GameObject sheetLayer;
        [SerializeField] private Button dim, closeButton, storeButton;
        [SerializeField] private Image sheetMedal;
        [SerializeField] private TMP_Text sheetName, sheetDesc, sheetRange, sheetNeed, closeText, storeText;
        [SerializeField] private RectTransform[] tiles = new RectTransform[5];      // each: Bg, Thumb, Name, Type

        public event Action OnStore;

        private int points;
        private int openRank = -1;

        private void Awake()
        {
            if (dim != null) dim.onClick.AddListener(CloseSheet);
            if (closeButton != null) closeButton.onClick.AddListener(CloseSheet);
            if (storeButton != null) storeButton.onClick.AddListener(() => OnStore?.Invoke());
            for (int i = 0; i < rungs.Length; i++)
            {
                int r = 7 - i;     // the ladder runs from rank 8 down to rank 1
                if (rungs[i]?.button != null) rungs[i].button.onClick.AddListener(() => OpenSheet(r));
            }
            if (sheetLayer != null) sheetLayer.SetActive(false);
        }

        private ArabicText nameEn;

        // the rank's name on the membership card is a picture of Arabic calligraphy: in English a live text takes its place
        private void ShowEnglishRankName(int rank)
        {
            bool en = MafiaNight.Core.Loc.English;
            nameImg.enabled = !en;
            if (!en) { if (nameEn != null) nameEn.gameObject.SetActive(false); return; }
            if (nameEn == null)
            {
                var go = new GameObject("RankNameEn", typeof(RectTransform));
                var r = (RectTransform)go.transform; r.SetParent(nameImg.transform.parent, false);
                var src = nameImg.rectTransform;
                r.anchorMin = src.anchorMin; r.anchorMax = src.anchorMax; r.pivot = src.pivot; r.anchoredPosition = src.anchoredPosition; r.sizeDelta = new Vector2(300f * K, 40f * K);
                var tm = go.AddComponent<TMPro.TextMeshProUGUI>(); tm.font = playerName.font; tm.fontStyle = TMPro.FontStyles.Bold; tm.fontSize = 22f * K; tm.color = nameImg.color; tm.raycastTarget = false;
                tm.alignment = TMPro.TextAlignmentOptions.MidlineRight;       // as the Arabic one sits: the mirror turns it to the left
                nameEn = go.AddComponent<ArabicText>();
            }
            nameEn.gameObject.SetActive(true);
            nameEn.Text = KitTheme.RankNames[rank];
        }

        /// <summary>Draws everything for a player with this many rank points. number = his account number ("" hides it).</summary>
        public void Show(string player, string number, int rankPoints)
        {
            points = Mathf.Max(0, rankPoints);
            var st = RankLadder.FromPoints(points);
            bool top = points >= Max;
            int rank = st.RankIndex;

            medal.sprite = medals[rank];
            nameImg.sprite = names[rank]; nameImg.SetNativeSize();
            ShowEnglishRankName(rank);
            KitBagScreen.SetText(cardNumber, string.IsNullOrEmpty(number) ? "" : "رقم " + number, 0f);
            KitBagScreen.SetText(playerName, player ?? "", 230f);
            for (int i = 0; i < 3; i++) { bullets[i].sprite = bulletOn; bullets[i].color = i <= st.Tier - 1 ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.3f); }    // the empty ones are the same bullet, dim
            KitBagScreen.SetText(tierText, top ? "كل الدرجات" : "الدرجة " + Tiers[st.Tier - 1] + " من ٣", 189f);

            float pct = top ? 1f : st.Progress01;
            fill.gameObject.SetActive(pct > 0f);
            float w = Mathf.Max(10f * K, 334f * K * pct);                      // the bar is 334 wide; never narrower than its two round ends
            fill.sizeDelta = new Vector2(w + 4f * K, fill.sizeDelta.y);

            string next = top ? "" : st.Tier < 3 ? "للدرجة " + Tiers[st.Tier] : "للترقية لـ" + KitTheme.RankNames[rank + 1];
            string left = top ? "باقيلك" : "باقيلك " + Wins(Mathf.CeilToInt(st.PointsToNext / (float)Win));
            KitBagScreen.SetText(progRight, top ? "العرّاب" : left, 330f * 0.5f);
            float rw = KitBagScreen.Width(progRight) / K;
            KitBagScreen.SetText(progLeft, top ? Num(Max) + " نقطة" : Num(st.PointsIntoTier) + " من " + Num(PerTier) + " " + next, 334f - rw - 10f);
            stampText.GetComponent<ArabicText>().Text = top ? "العرّاب" : "عضو";
            float sw = Mathf.Max(43.59f, KitBagScreen.Width(stampText) / K + 18f);
            stamp.sizeDelta = new Vector2(sw * K, stamp.sizeDelta.y);
            stamp.anchoredPosition = new Vector2((252.41f + 43.59f / 2f) * K, stamp.anchoredPosition.y);

            for (int i = 0; i < 8; i++)
            {
                int r = 7 - i; var v = rungs[i];
                bool done = r < rank, now = r == rank, locked = r > rank;
                v.bg.sprite = now ? rungNow : locked ? rungLock : rungNormal;
                v.medal.sprite = locked ? medalsLocked[r] : medals[r];
                v.title.GetComponent<ArabicText>().Text = KitTheme.RankNames[r];
                v.title.color = locked ? new Color32(0x7D, 0x73, 0x65, 255) : new Color32(0xD9, 0xD0, 0xBD, 255);
                KitBagScreen.SetText(v.desc, Descriptions[r], 236f);
                v.desc.color = now ? new Color32(0xD7, 0xC3, 0xA0, 255) : locked ? new Color32(0x5F, 0x57, 0x4C, 255) : new Color32(0xA7, 0x9D, 0x88, 255);
                v.pin.SetActive(now); v.lockIcon.gameObject.SetActive(locked);
                int from = r * PerRank;
                var topRt = (RectTransform)v.endTop.transform; var botRt = (RectTransform)v.endBottom.transform;
                if (now)
                {
                    SetEnd(v.endTop, Num(points), new Color32(0xA9, 0x8F, 0x62, 255), true); SetEnd(v.endBottom, "نقطة", new Color32(0x8F, 0x87, 0x77, 255), false);
                    topRt.anchoredPosition = new Vector2(topRt.anchoredPosition.x, -(10f + 10.5f) * K); botRt.anchoredPosition = new Vector2(botRt.anchoredPosition.x, -(34f + 9f) * K);
                }
                else if (done)
                {
                    SetEnd(v.endTop, "وصلتها", new Color32(0x8F, 0xD4, 0xB4, 255), false); SetEnd(v.endBottom, Num(from) + "+", new Color32(0x8F, 0x87, 0x77, 255), false);
                    topRt.anchoredPosition = new Vector2(topRt.anchoredPosition.x, -(11.5f + 9f) * K); botRt.anchoredPosition = new Vector2(botRt.anchoredPosition.x, -(32.5f + 9f) * K);
                }
                else
                {
                    SetEnd(v.endTop, "", Color.white, false); SetEnd(v.endBottom, "من " + Num(from), new Color32(0x8F, 0x87, 0x77, 255), false);
                    botRt.anchoredPosition = new Vector2(botRt.anchoredPosition.x, -(30.5f + 9f) * K);
                }
            }
            if (openRank >= 0) FillSheet(openRank);
        }

        private static void SetEnd(TMP_Text t, string s, Color c, bool lalezar)
        {
            t.GetComponent<ArabicText>().Text = s; t.color = c;
        }

        // ---------------------------------------------------------------- the rank sheet

        private void OpenSheet(int rank) { openRank = rank; sheetLayer.SetActive(true); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)sheetName.transform.parent); FillSheet(rank); }

        public void CloseSheet() { openRank = -1; if (sheetLayer != null) sheetLayer.SetActive(false); }

        private void FillSheet(int rank)
        {
            int myRank = RankLadder.FromPoints(points).RankIndex;
            bool locked = rank > myRank;
            int from = rank * PerRank, need = from - points;
            sheetMedal.sprite = locked ? medalsLocked[rank] : medals[rank];
            KitBagScreen.SetText(sheetName, KitTheme.RankNames[rank], 354f);
            sheetDesc.GetComponent<ArabicText>().Text = Descriptions[rank];
            KitBagScreen.SetText(sheetRange, "الرتبة " + KitBagScreen.Ar(rank + 1) + " من ٨ · من " + Num(from) + " لـ " + Num(from + PerRank) + " نقطة", 354f);
            KitBagScreen.SetText(sheetNeed, locked ? "باقيلك " + Num(need) + " نقطة ≈ " + Wins(Mathf.CeilToInt(need / (float)Win)) : rank == myRank ? "هاي رتبتك هلّق، أغراضها مفتوحة إلك" : "وصلتها، أغراضها مفتوحة إلك", 354f);
            sheetNeed.color = locked ? new Color32(0xE0, 0x64, 0x6A, 255) : new Color32(0x8F, 0xD4, 0xB4, 255);
            storeButton.gameObject.SetActive(!locked); closeButton.gameObject.SetActive(locked);
            var set = RankSets.All[Mathf.Clamp(rank, 0, RankSets.All.Length - 1)];
            for (int k = 0; k < tiles.Length; k++)
            {
                var t = tiles[k];
                t.Find("Bg").GetComponent<Image>().sprite = locked ? tileLocked : tileOpen;
                DrawThumb((RectTransform)t.Find("Thumb"), set, k, locked);
                string item = set.items != null && k < set.items.Length ? set.items[k] : "";
                KitBagScreen.SetText(t.Find("Name").GetComponent<TMP_Text>(), item, 61f);
                t.Find("Type").GetComponent<ArabicText>().Text = RankSets.TypeNames[k];
            }
        }

        // the thumbnails RankArt draws at their own sizes, fitted in a square box
        private static readonly Vector2[] ThumbSize = { new Vector2(127, 127), new Vector2(155, 122), new Vector2(130, 77), new Vector2(166, 61), new Vector2(84, 50) };

        private static void DrawThumb(RectTransform box, RankSetInfo set, int type, bool locked)
        {
            for (int i = box.childCount - 1; i >= 0; i--) Destroy(box.GetChild(i).gameObject);
            var h = new GameObject("Holder", typeof(RectTransform)).GetComponent<RectTransform>();
            h.SetParent(box, false); h.anchorMin = h.anchorMax = h.pivot = new Vector2(0.5f, 0.5f); h.anchoredPosition = Vector2.zero;
            var nat = ThumbSize[Mathf.Clamp(type, 0, 4)]; var r = box.rect.size;
            float s = Mathf.Min(r.x / nat.x, r.y / nat.y);
            h.localScale = new Vector3(s, s, 1f); h.sizeDelta = r / s;
            RankArt.Build(h, set, type, locked, 1f);
        }

        // ---------------------------------------------------------------- helpers

        private static string Num(int n) => KitBagScreen.Ar(n);

        /// <summary>Arabic counts the noun by the number: فوز واحد، فوزين، ٣-١٠ انتصارات، ١١+ انتصار.</summary>
        public static string Wins(int n) => n <= 1 ? "فوز واحد" : n == 2 ? "فوزين" : Num(n) + (n <= 10 ? " انتصارات" : " انتصار");
    }
}
