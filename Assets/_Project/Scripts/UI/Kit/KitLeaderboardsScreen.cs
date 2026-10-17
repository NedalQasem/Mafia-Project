using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>Leaderboards room: three doors (today = chalkboard, week = newspaper, hall of fame) and eight categories.</summary>
    public class KitLeaderboardsScreen : MonoBehaviour
    {
        [SerializeField] private Button[] doors = new Button[3];
        [SerializeField] private Button backButton;
        public event System.Action OnBack;
        [SerializeField] private GameObject boardView, wallView, chipsRow;
        [SerializeField] private Image boardBg;
        [SerializeField] private ArabicText boardTitle, boardUnit, rewardLabel, resetLabel;
        [SerializeField] private LeaderPodiumSlot[] podium = new LeaderPodiumSlot[3];    // [0]=1st, [1]=2nd, [2]=3rd
        [SerializeField] private RectTransform rowsRoot, chipsRoot;
        [SerializeField] private GameObject rowPrefab, chipPrefab;
        [SerializeField] private ArabicText mineRank, mineNeed, mineValue;
        [SerializeField] private RectTransform mineFill;
        [SerializeField] private GameObject mineBar;
        [SerializeField] private ArabicText mineWall;

        private int period = 0, cat = 0;           // period: 0 day, 1 week, 2 wall
        private readonly List<Button> chips = new List<Button>();

        private void Start() { if (Application.isPlaying) Build(); }

        public void Build()
        {
            for (int i = chipsRoot.childCount - 1; i >= 0; i--) Del(chipsRoot.GetChild(i).gameObject);
            chips.Clear();
            for (int i = 0; i < LeaderboardData.Categories.Length; i++)
            {
                int idx = i;
                var go = Instantiate(chipPrefab, chipsRoot);
                go.GetComponentInChildren<ArabicText>().Text = LeaderboardData.Categories[i];
                var b = go.GetComponent<Button>(); b.onClick.AddListener(() => SetCategory(idx));
                float w = Mathf.Max(170f, LeaderboardData.Categories[i].Length * 27f + 90f);
                go.GetComponent<LayoutElement>().preferredWidth = w; ((RectTransform)go.transform).sizeDelta = new Vector2(w, 76f);
                chips.Add(b);
            }
            for (int i = 0; i < doors.Length; i++) { int idx = i; doors[i].onClick.AddListener(() => SetPeriod(idx)); }
            if (backButton != null) backButton.onClick.AddListener(() => OnBack?.Invoke());
            Apply();
        }

        private static void Del(GameObject g) { if (Application.isPlaying) Destroy(g); else DestroyImmediate(g); }

        public void SetPeriod(int p) { period = p; Apply(); }
        public void SetCategory(int c) { cat = c; Apply(); }

        private void Apply()
        {
            var th = KitTheme.Current;
            bool wall = period == 2, paper = period == 1;
            boardView.SetActive(!wall); wallView.SetActive(wall); chipsRow.SetActive(!wall);
            mineBar.SetActive(true);
            for (int i = 0; i < doors.Length; i++)
            {
                bool on = i == period;
                StyleDoor(doors[i], i, on);
            }
            for (int i = 0; i < chips.Count; i++)
            {
                bool on = i == cat;
                { var ci = chips[i].GetComponent<Image>(); ci.sprite = on ? th.chipOn : th.chipOff; ci.color = on ? Color.white : new Color(1f, 1f, 1f, 0.03f); }
                chips[i].GetComponentInChildren<TMPro.TMP_Text>().color = on ? th.brassText : th.ivoryDim;
            }
            if (wall)
            {
                mineWall.Text = "تدخل القاعة بالوصول إلى المركز الأول بموسم أو بتحطيم رقم قياسي";
                mineRank.Text = "—"; mineNeed.Text = ""; mineValue.Text = "";
                SetFill(0.12f);
                resetLabel.Text = ""; SetResetVisible(false); return;
            }
            mineWall.Text = ""; SetResetVisible(true);
            resetLabel.Text = period == 0 ? "يتجدد خلال ٠٣:٤٢:١٠" : "يتجدد خلال ٢ يوم ٠٥:١٨";
            boardBg.sprite = paper ? th.paper : th.boardDay;
            var list = LeaderboardData.Make(period, cat);
            boardTitle.Text = (paper ? "جريدة الأسبوع · " : "سبورة الليلة · ") + LeaderboardData.Categories[cat];
            boardUnit.Text = LeaderboardData.Units[cat];
            boardTitle.GetComponent<TMPro.TMP_Text>().color = paper ? th.paperInk : new Color32(0xE9, 0xE6, 0xDC, 255);
            boardUnit.GetComponent<TMPro.TMP_Text>().color = paper ? th.crimson : new Color32(0xFF, 0xD9, 0x8A, 255);
            for (int i = 0; i < 3; i++) podium[i].Bind(list[i], i + 1, paper);
            for (int i = rowsRoot.childCount - 1; i >= 0; i--) Del(rowsRoot.GetChild(i).gameObject);
            for (int i = 3; i < list.Count; i++)
            {
                var go = Instantiate(rowPrefab, rowsRoot);
                go.GetComponent<LeaderRowView>().Bind(list[i], i + 1, paper);
            }
            var me = LeaderboardData.Me(period, cat);
            var meGo = Instantiate(rowPrefab, rowsRoot);
            meGo.GetComponent<LeaderRowView>().Bind(me, 42, paper);
            rewardLabel.Text = period == 0 ? "جائزة الليلة: ٣٠٠ كوينز وشارة \"سيّد الليلة\" للأول" : "جوائز الأسبوع: ١: إطار \"ملك الأسبوع\" + ٥٬٠٠٠  ·  ٢: ٢٬٥٠٠  ·  ٣: ١٬٠٠٠";
            mineRank.Text = "#" + CoinPill.Format(42);
            mineNeed.Text = "تحتاج " + CoinPill.Format(period == 0 ? 320 : 1850) + " " + LeaderboardData.Units[cat] + " لتدخل أول ١٠";
            mineValue.Text = CoinPill.Format(me.value);
            SetFill(period == 0 ? 0.64f : 0.42f);
        }

        // HTML .door: the picked door rises 5px and gets a cream outline; the others keep a dimmed, greyed icon and label
        private static readonly Color32[] DoorOn = { new Color32(0xF2, 0xEF, 0xE4, 255), new Color32(0x1C, 0x17, 0x0F, 255), new Color32(0xF5, 0xE2, 0xA8, 255) };
        private static readonly Color32[] DoorOff = { new Color32(0x8F, 0x87, 0x77, 255), new Color32(0x4A, 0x40, 0x30, 255), new Color32(0xC9, 0xA8, 0x6A, 255) };

        private static void StyleDoor(Button door, int i, bool on)
        {
            var rt = (RectTransform)door.transform;
            rt.anchoredPosition = new Vector2(0f, on ? 14f : 0f);
            var ring = rt.Find("Ring"); if (ring != null) ring.gameObject.SetActive(on);
            var label = rt.Find("Label"); if (label != null) label.GetComponent<TMPro.TMP_Text>().color = on ? DoorOn[i] : DoorOff[i];
            var icon = rt.Find("Icon"); if (icon != null) icon.GetComponent<Image>().color = on ? Color.white : new Color(0.62f, 0.6f, 0.58f, 1f);
        }

        private void SetResetVisible(bool on) { if (resetLabel != null && resetLabel.transform.parent != null && resetLabel.transform.parent.name == "ResetPill") resetLabel.transform.parent.gameObject.SetActive(on); }

        private void SetFill(float v)
        {
            if (mineFill == null) return;
            mineFill.anchorMin = new Vector2(1f - v, 0); mineFill.anchorMax = new Vector2(1, 1);
            mineFill.offsetMin = mineFill.offsetMax = Vector2.zero;
        }
    }
}
