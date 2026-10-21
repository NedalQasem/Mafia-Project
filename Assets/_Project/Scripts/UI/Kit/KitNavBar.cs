using System;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Bottom navigation (matches Docs/mockups/footer.html). Five tabs, right to left: shop, family, rooms (brass seal),
    /// ranks, settings. The selected plain tab rises and glows; the seal tab only lights up; a spotlight glides under it.
    /// </summary>
    public class KitNavBar : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            public Button button;
            public RectTransform icon;     // moves/scales for normal tabs; for the seal it is the seal root
            public Image iconImage;
            public ArabicText label;
            public bool isSeal;
            public Image glow;             // optional, only the seal uses it
        }

        [SerializeField] private Tab[] tabs = new Tab[5];
        [SerializeField] private RectTransform spotlight;
        [SerializeField] private float rise = 19f;
        [SerializeField] private float selectedScale = 1.14f;
        [SerializeField] private int selected = 2;
        [SerializeField, HideInInspector] private float[] restY;

        public event Action<int> OnTabSelected;
        private float[] t;

        public int Selected => selected;

        public void CaptureRest()
        {
            restY = new float[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
                restY[i] = tabs[i] != null && tabs[i].icon != null ? tabs[i].icon.anchoredPosition.y : 0f;
        }

        private void EnsureInit()
        {
            if (t != null && t.Length == tabs.Length) return;
            t = new float[tabs.Length];
            if (restY == null || restY.Length != tabs.Length) CaptureRest();
            for (int i = 0; i < tabs.Length; i++) t[i] = i == selected ? 1f : 0f;
        }

        private void Awake()
        {
            EnsureInit();
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                if (tabs[i]?.button != null) tabs[i].button.onClick.AddListener(() => Select(idx));
            }
        }

        /// <summary>Jump to the selected look without animation (used by the builder and on first show).</summary>
        public void SnapToSelected()
        {
            t = null; EnsureInit();
            Apply();
            SnapSpotlight();
        }

        public void Select(int index)
        {
            if (index == selected || index < 0 || index >= tabs.Length) return;
            selected = index;
            OnTabSelected?.Invoke(index);
        }

        private void Update()
        {
            EnsureInit();
            for (int i = 0; i < tabs.Length; i++)
                t[i] = Mathf.MoveTowards(t[i], i == selected ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            Apply();
            if (spotlight != null && tabs[selected]?.button != null)
            {
                var p = spotlight.anchoredPosition;
                p.x = Mathf.Lerp(p.x, TargetSpotX(), 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                spotlight.anchoredPosition = p;
            }
        }

        private float TargetSpotX()
        {
            var rt = (RectTransform)tabs[selected].button.transform;
            return spotlight.parent.InverseTransformPoint(rt.position).x;
        }

        private void SnapSpotlight()
        {
            if (spotlight == null || tabs[selected]?.button == null) return;
            var p = spotlight.anchoredPosition; p.x = TargetSpotX(); spotlight.anchoredPosition = p;
        }

        // ---------------------------------------------------------------- badges (the builder draws them; the app fills them)

        private Transform TabChild(int tab, string name)
        {
            if (tab < 0 || tab >= tabs.Length || tabs[tab]?.button == null) return null;
            return tabs[tab].button.transform.Find(name);
        }

        /// <summary>Shows how many things wait on a tab (0 hides the badge). The family tab has a numbered badge, the ranks tab a plain dot.</summary>
        public void SetBadge(int tab, int count)
        {
            var b = TabChild(tab, "Badge") ?? TabChild(tab, "Dot");
            if (b == null) return;
            b.gameObject.SetActive(count > 0);
            var n = b.Find("N");
            if (n != null && count > 0) { var a = n.GetComponent<ArabicText>(); if (a != null) a.Text = count > 99 ? "٩٩+" : CoinPill.Format(count); }
        }

        /// <summary>The number on the pill above the rooms seal (players online). A negative number hides the pill: nothing is known yet.</summary>
        public void SetOnline(int count)
        {
            var pill = TabChild(2, "LivePill");
            if (pill == null) return;
            pill.gameObject.SetActive(count >= 0);
            var c = pill.Find("Count");
            if (c != null && count >= 0) { var a = c.GetComponent<ArabicText>(); if (a != null) a.Text = CoinPill.Format(count); }
        }

        private void Apply()
        {
            var theme = KitTheme.Current;
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                if (tab == null || tab.icon == null) continue;
                float e = 1f - Mathf.Pow(1f - t[i], 3f);
                Color tint = Color.Lerp(new Color(0.6f, 0.55f, 0.5f, 1f), Color.white, e);

                if (tab.isSeal)
                {
                    foreach (var img in tab.icon.GetComponentsInChildren<Image>(true)) img.color = tint;
                    if (tab.glow != null) { var c = tab.glow.color; c.a = e; tab.glow.color = c; }
                }
                else
                {
                    tab.icon.anchoredPosition = new Vector2(tab.icon.anchoredPosition.x, restY[i] + rise * e);
                    float s = Mathf.Lerp(1f, selectedScale, e);
                    tab.icon.localScale = new Vector3(s, s, 1f);
                    if (tab.iconImage != null) tab.iconImage.color = tint;
                }

                if (tab.label != null && theme != null)
                {
                    var tmp = tab.label.GetComponent<TMPro.TMP_Text>();
                    if (tmp != null) tmp.color = Color.Lerp(theme.ivoryDim, theme.brassLight, e);
                }
            }
        }
    }
}
