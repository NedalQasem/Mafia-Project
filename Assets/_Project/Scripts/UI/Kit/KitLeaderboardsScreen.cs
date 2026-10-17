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
        [SerializeField] private Image headerLine;
        public event System.Action OnBack;
        [SerializeField] private GameObject boardView, wallView, chipsRow;
        [SerializeField] private Image boardBg;
        [SerializeField] private ArabicText boardTitle, boardUnit, rewardLabel, resetLabel, resetCaption;
        [SerializeField] private LeaderPodiumSlot[] podium = new LeaderPodiumSlot[3];    // [0]=1st, [1]=2nd, [2]=3rd
        [SerializeField] private RectTransform rowsRoot, chipsRoot;
        [SerializeField] private GameObject rowPrefab, chipPrefab;
        [SerializeField] private ArabicText mineRank, mineNeed, mineValue;
        [SerializeField] private RectTransform mineFill;
        [SerializeField] private GameObject mineBar;
        [SerializeField] private ArabicText mineWall;
        [SerializeField] private ArabicText mineCaption;
        [SerializeField] private RectTransform mineBarTrack;

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
                var lbl = go.GetComponentInChildren<TMPro.TMP_Text>(); lbl.ForceMeshUpdate();
                float w = Mathf.Max(110f, lbl.preferredWidth + 78f);
                go.GetComponent<LayoutElement>().preferredWidth = w; ((RectTransform)go.transform).sizeDelta = new Vector2(w, 76f);
                chips.Add(b);
            }
            for (int i = 0; i < doors.Length; i++) { int idx = i; doors[i].onClick.AddListener(() => SetPeriod(idx)); }
            if (backButton != null) backButton.onClick.AddListener(() => OnBack?.Invoke());
            Apply();
        }

        private static void Del(GameObject g) { if (Application.isPlaying) Destroy(g); else DestroyImmediate(g); }

        public void SetPeriod(int p)
        {
            if (p == period) { Apply(); return; }
            bool toHigher = p > period; period = p; Apply();
            // HTML .view: the new board fades in while sliding ~30px from the side of the door that was pressed
            KitEnterAnim.Play(period == 2 ? wallView : boardView, 0f, toHigher ? -83f : 83f, 0.45f, 0.35f);
        }
        public void SetCategory(int c) { cat = c; Apply(); }

        private void Apply()
        {
            var th = KitTheme.Current;
            bool wall = period == 2, paper = period == 1;
            boardView.SetActive(!wall); wallView.SetActive(wall); chipsRow.SetActive(!wall);
            mineBar.SetActive(true);
            FitMineBar(wall);
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
                mineRank.Text = "—"; mineNeed.Text = ""; mineValue.Text = ""; if (mineCaption != null) mineCaption.Text = "غير مصنف";
                SetFill(0.12f);
                resetLabel.Text = ""; SetResetVisible(false); return;
            }
            mineWall.Text = ""; SetResetVisible(true); if (mineCaption != null) mineCaption.Text = "ترتيبك";
            SetReset(period == 0 ? "٠٣:٤٢:١٠" : "٢ يوم ٠٥:١٨");
            boardBg.sprite = paper ? th.paper : th.boardDay;
            // newspaper = bold display type and a solid ink rule; chalkboard = calligraphy and a dashed chalk line
            var tt = boardTitle.GetComponent<TMPro.TMP_Text>();
            tt.font = paper ? th.fontHeading : th.fontCalligraphy; tt.fontStyle = paper ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal; tt.fontSize = paper ? 48f : 56f;
            var ut = boardUnit.GetComponent<TMPro.TMP_Text>(); ut.fontStyle = TMPro.FontStyles.Bold; ut.fontSize = paper ? 30f : 34f;
            if (headerLine != null)
            {
                headerLine.sprite = paper ? null : th.dashH; headerLine.type = paper ? Image.Type.Simple : Image.Type.Tiled;
                headerLine.color = paper ? new Color(0.11f, 0.09f, 0.06f, 0.9f) : new Color(0.91f, 0.9f, 0.86f, 0.3f);
            }
            for (int s = 0; s < 3; s++)
            {
                var st = boardView.transform.Find("Stand" + (s + 1)); if (st == null) continue;
                st.GetComponent<Image>().color = paper ? new Color(0.11f, 0.09f, 0.06f, 0.07f) : new Color(1f, 1f, 1f, 0.01f);
                var hl = st.Find("Hl"); if (hl != null) hl.GetComponent<Image>().color = paper ? new Color(0.11f, 0.09f, 0.06f, 0.04f) : new Color(1f, 1f, 1f, 0.025f);
            }
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
                KitEnterAnim.Play(go, (i - 3) * 0.04f, 38f, 0.4f, 0.4f);
            }
            var me = LeaderboardData.Me(period, cat);
            var meGo = Instantiate(rowPrefab, rowsRoot);
            meGo.GetComponent<LeaderRowView>().Bind(me, 42, paper);
            KitEnterAnim.Play(meGo, (list.Count - 3) * 0.04f, 38f, 0.4f, 0.4f);
            boardView.transform.localRotation = paper ? Quaternion.Euler(0f, 0f, 0.6f) : Quaternion.identity;   // the newspaper lies slightly crooked
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
            if (!Application.isPlaying) rt.anchoredPosition = new Vector2(0f, on ? 14f : 0f);   // in play mode Update eases it
            var ring = rt.Find("Ring"); if (ring != null) ring.gameObject.SetActive(on);
            var label = rt.Find("Label"); if (label != null) label.GetComponent<TMPro.TMP_Text>().color = on ? DoorOn[i] : DoorOff[i];
            var icon = rt.Find("Icon"); if (icon != null) icon.GetComponent<Image>().color = on ? Color.white : new Color(0.62f, 0.6f, 0.58f, 1f);
        }

        private float resetSeconds = 3 * 3600 + 42 * 60 + 10, resetTick;

        private static string Ar2(int v) => v < 10 ? "٠" + CoinPill.Format(v) : CoinPill.Format(v);

        private void Update()
        {
            if (!Application.isPlaying) return;
            for (int i = 0; i < doors.Length; i++)
            {
                var rt = (RectTransform)doors[i].transform;
                float target = i == period ? 14f : 0f;
                float y = Mathf.Lerp(rt.anchoredPosition.y, target, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
                rt.anchoredPosition = new Vector2(0f, Mathf.Abs(y - target) < 0.05f ? target : y);
            }
            if (period == 0)
            {
                resetTick += Time.unscaledDeltaTime;
                if (resetTick >= 1f)
                {
                    resetTick -= 1f; resetSeconds = Mathf.Max(0f, resetSeconds - 1f);
                    int s = (int)resetSeconds;
                    SetReset(Ar2(s / 3600) + ":" + Ar2(s % 3600 / 60) + ":" + Ar2(s % 60));
                }
            }
        }

        // caption on the right, the amber countdown to its left; the pill is as wide as both
        private void SetReset(string value)
        {
            resetLabel.Text = value;
            var cap = resetCaption.GetComponent<TMPro.TMP_Text>(); var val = resetLabel.GetComponent<TMPro.TMP_Text>();
            cap.ForceMeshUpdate(); val.ForceMeshUpdate();
            float cw = cap.preferredWidth, vw = val.preferredWidth;
            ((RectTransform)resetCaption.transform).sizeDelta = new Vector2(cw + 4f, 50f);
            var vr = (RectTransform)resetLabel.transform; vr.sizeDelta = new Vector2(vw + 4f, 56f); vr.anchoredPosition = new Vector2(-(24f + cw + 14f), 0f);
            ((RectTransform)resetLabel.transform.parent).sizeDelta = new Vector2(24f + cw + 14f + vw + 24f, 64f);
        }

        private void SetResetVisible(bool on) { if (resetLabel != null && resetLabel.transform.parent != null && resetLabel.transform.parent.name == "ResetPill") resetLabel.transform.parent.gameObject.SetActive(on); }

        // the pinned bar grows upward when it has to hold a two-line note; its content keeps its distance from the bottom edge
        private void FitMineBar(bool tall)
        {
            var r = (RectTransform)mineBar.transform;
            float extra = tall ? 44f : 0f;
            r.offsetMax = new Vector2(r.offsetMax.x, 168f + extra);
            if (mineWall != null) ((RectTransform)mineWall.transform).anchoredPosition = new Vector2(-10f, 24f + extra * 0.1f);
            if (mineBarTrack != null) mineBarTrack.anchoredPosition = new Vector2(-10f, -44f - extra * 0.5f);
            if (mineCaption != null) ((RectTransform)mineCaption.transform).anchoredPosition = new Vector2(-170f, -44f - extra * 0.5f);
            if (mineRank != null) ((RectTransform)mineRank.transform).anchoredPosition = new Vector2(-170f, 20f - extra * 0.5f + extra * 0.5f);
        }

        private void SetFill(float v)
        {
            if (mineFill == null) return;
            mineFill.anchorMin = new Vector2(1f - v, 0); mineFill.anchorMax = new Vector2(1, 1);
            mineFill.offsetMin = mineFill.offsetMax = Vector2.zero;
        }
    }
}
