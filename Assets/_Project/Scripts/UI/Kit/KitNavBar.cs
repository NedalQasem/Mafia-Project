using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Bottom navigation (matches idea B "the case file" of Docs/mockups/navbar.html). Five tabs, right to left: market, family, rooms,
    /// ranks, settings. The chosen tab rises as an ivory file tab with its name in Aref Ruqaa. Market, family and ranks have two places
    /// behind them, so a tap opens a small menu above the tab (store / bag, chat / family, leaderboards / my rank) over a dimmed page;
    /// rooms and settings open straight away.
    /// </summary>
    public class KitNavBar : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            public Button button;
            public RectTransform content;  // the icon and its badge: rises when the tab is chosen
            public CanvasGroup iconOn;     // the drawing in its colours
            public CanvasGroup iconOff;    // the same drawing dimmed
            public TMP_Text label;         // the name when not chosen (Cairo)
            public CanvasGroup labelOn;    // the name when chosen (Aref Ruqaa image)
            public CanvasGroup paper;      // the ivory file tab behind the chosen tab
            public RectTransform tag;      // only rooms: the paper tag with the players online, rides on the tab's top edge
        }

        [Serializable]
        public class Place
        {
            public Button button;
            public GameObject current;     // the crimson highlight of the place the player is in
            public RectTransform text;     // title and line under it; moves aside when a badge shows
            public float textLeft, textLeftWithBadge;
            public GameObject badge;       // a red count ...
            public TMP_Text badgeNumber;
            public GameObject dot;         // ... or a plain red dot
            public bool soon;              // not built yet: the place says so and the tap only closes the menu
        }

        [Serializable]
        public class Menu
        {
            public int tab;
            public RectTransform root;
            public CanvasGroup group;
            public Place[] places;
        }

        [SerializeField] private Tab[] tabs = new Tab[5];
        [SerializeField] private Menu[] menus = new Menu[0];
        [SerializeField] private CanvasGroup scrim;
        [SerializeField] private int selected = 2;
        [SerializeField] private float rise = 20f;        // how far the icon climbs on the chosen tab (canvas units)
        [SerializeField] private float tagRise = 33f;     // how far the rooms tag climbs (the tab's top edge moves more than its icon)
        [SerializeField, HideInInspector] private float[] restY;
        [SerializeField, HideInInspector] private float tagRestY;

        /// <summary>A tab with nothing behind it was tapped (rooms, settings).</summary>
        public event Action<int> OnTabSelected;
        /// <summary>A place in a tab's menu was chosen: (tab, place), place 0 is the upper row.</summary>
        public event Action<int, int> OnPlaceSelected;

        private float[] t;
        private float menuT;           // 0 closed .. 1 open (eases the menu in and out)
        private int open = -1;         // the tab whose menu is open
        private int shown = -1;        // the menu still drawn while it closes

        public int Selected => selected;
        public bool MenuOpen => open >= 0;

        private void EnsureInit()
        {
            if (t != null && t.Length == tabs.Length) return;
            t = new float[tabs.Length];
            if (restY == null || restY.Length != tabs.Length) CaptureRest();
            for (int i = 0; i < tabs.Length; i++) t[i] = i == selected ? 1f : 0f;
        }

        /// <summary>Remembers where the icons sit when not chosen (the builder calls it once).</summary>
        public void CaptureRest()
        {
            restY = new float[tabs.Length];
            for (int i = 0; i < tabs.Length; i++) restY[i] = tabs[i]?.content != null ? tabs[i].content.anchoredPosition.y : 0f;
            var tg = tabs.Length > 2 ? tabs[2]?.tag : null;
            tagRestY = tg != null ? tg.anchoredPosition.y : 0f;
        }

        private void Awake()
        {
            EnsureInit();
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                if (tabs[i]?.button != null) tabs[i].button.onClick.AddListener(() => Tap(idx));
            }
            foreach (var m in menus)
            {
                if (m?.places == null) continue;
                for (int p = 0; p < m.places.Length; p++)
                {
                    int tab = m.tab, place = p;
                    if (m.places[p]?.button != null) m.places[p].button.onClick.AddListener(() => Choose(tab, place));
                }
                if (m.root != null) m.root.gameObject.SetActive(false);
            }
            if (scrim != null)
            {
                scrim.alpha = 0f; scrim.blocksRaycasts = false; scrim.gameObject.SetActive(false);
                var b = scrim.GetComponent<Button>(); if (b != null) b.onClick.AddListener(CloseMenu);
            }
        }

        private Menu MenuOf(int tab)
        {
            foreach (var m in menus) if (m != null && m.tab == tab && m.root != null) return m;
            return null;
        }

        private void Tap(int index)
        {
            if (MenuOf(index) != null) { if (open == index) CloseMenu(); else OpenMenu(index); return; }
            CloseMenu();
            Select(index);
        }

        private void OpenMenu(int tab)
        {
            if (shown >= 0 && shown != tab) { var old = MenuOf(shown); if (old != null) old.root.gameObject.SetActive(false); menuT = 0f; }
            open = shown = tab;
            var m = MenuOf(tab);
            m.root.gameObject.SetActive(true);
            m.root.SetAsLastSibling();
            if (scrim != null) { scrim.gameObject.SetActive(true); scrim.blocksRaycasts = true; }
        }

        /// <summary>Closes the open menu, if any (a tap outside it, or the same tab again).</summary>
        public void CloseMenu()
        {
            open = -1;
            if (scrim != null) scrim.blocksRaycasts = false;
        }

        private void Choose(int tab, int place)
        {
            var m = MenuOf(tab);
            bool soon = m != null && place < m.places.Length && m.places[place] != null && m.places[place].soon;
            CloseMenu();
            if (!soon) OnPlaceSelected?.Invoke(tab, place);
        }

        /// <summary>Jump to the chosen look without animation (used by the builder and on first show).</summary>
        public void SnapToSelected()
        {
            t = null; EnsureInit();
            Apply();
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
            int lit = open >= 0 ? open : selected;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < tabs.Length; i++) t[i] = Mathf.MoveTowards(t[i], i == lit ? 1f : 0f, dt * 5f);
            Apply();

            menuT = Mathf.MoveTowards(menuT, open >= 0 ? 1f : 0f, dt / (open >= 0 ? 0.28f : 0.16f));
            var sm = shown >= 0 ? MenuOf(shown) : null;
            if (sm != null)
            {
                float e = open >= 0 ? BackOut(menuT) : menuT;
                if (sm.group != null) sm.group.alpha = Mathf.Clamp01(menuT * 1.6f);
                sm.root.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, e);
                sm.root.anchoredPosition = new Vector2(sm.root.anchoredPosition.x, MenuRestY(sm) - (1f - e) * 44f);
                if (menuT <= 0f && open < 0) { sm.root.gameObject.SetActive(false); shown = -1; }
            }
            if (scrim != null)
            {
                scrim.alpha = menuT;
                if (menuT <= 0f && open < 0 && scrim.gameObject.activeSelf) scrim.gameObject.SetActive(false);
            }
        }

        private static float BackOut(float x) { const float c = 1.3f; x -= 1f; return 1f + (c + 1f) * x * x * x + c * x * x; }

        private float[] menuRest;
        private float MenuRestY(Menu m)
        {
            if (menuRest == null) { menuRest = new float[menus.Length]; for (int i = 0; i < menus.Length; i++) menuRest[i] = menus[i]?.root != null ? menus[i].root.anchoredPosition.y : 0f; }
            int k = Array.IndexOf(menus, m);
            return k >= 0 ? menuRest[k] : m.root.anchoredPosition.y;
        }

        private void Apply()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                if (tab == null) continue;
                float e = 1f - Mathf.Pow(1f - t[i], 3f);
                if (tab.content != null) tab.content.anchoredPosition = new Vector2(tab.content.anchoredPosition.x, restY[i] + rise * e);
                if (tab.iconOn != null) tab.iconOn.alpha = e;
                if (tab.iconOff != null) tab.iconOff.alpha = 1f - e;
                if (tab.label != null) tab.label.alpha = 1f - e;
                if (tab.labelOn != null) tab.labelOn.alpha = e;
                if (tab.paper != null)
                {
                    tab.paper.alpha = e;
                    var pr = (RectTransform)tab.paper.transform;
                    pr.localScale = new Vector3(1f, Mathf.Lerp(0.86f, 1f, e), 1f);   // grows from the bar like the mockup's tab (66 -> 78 high)
                }
                if (tab.tag != null) tab.tag.anchoredPosition = new Vector2(tab.tag.anchoredPosition.x, tagRestY + tagRise * e);
            }
        }

        // ---------------------------------------------------------------- what the app fills in

        private Transform TabChild(int tab, string name)
        {
            if (tab < 0 || tab >= tabs.Length || tabs[tab]?.content == null) return null;
            return tabs[tab].content.Find(name);
        }

        /// <summary>How many things wait behind a tab (0 hides it). Family has a numbered badge, ranks a plain dot.</summary>
        public void SetBadge(int tab, int count)
        {
            var b = TabChild(tab, "Badge") ?? TabChild(tab, "Dot");
            if (b == null) return;
            b.gameObject.SetActive(count > 0);
            var n = b.Find("N");
            if (n != null && count > 0) SetCount(n.GetComponent<ArabicText>(), (RectTransform)b, count);
        }

        /// <summary>How many things wait in one place of a tab's menu (0 hides it); the place's text makes room for the badge.</summary>
        public void SetPlaceBadge(int tab, int place, int count)
        {
            var m = MenuOf(tab);
            if (m == null || place < 0 || place >= m.places.Length || m.places[place] == null) return;
            var p = m.places[place];
            bool on = count > 0 && !p.soon;
            if (p.badge != null) { p.badge.SetActive(on); if (on && p.badgeNumber != null) SetCount(p.badgeNumber.GetComponent<ArabicText>(), (RectTransform)p.badge.transform, count); }
            if (p.dot != null) p.dot.SetActive(on);
            if (p.text != null) p.text.offsetMin = new Vector2(on || p.soon ? p.textLeftWithBadge : p.textLeft, p.text.offsetMin.y);
        }

        /// <summary>Marks the place the player is in (the crimson row in the menu); -1 for none.</summary>
        public void SetCurrentPlace(int tab, int place)
        {
            var m = MenuOf(tab);
            if (m == null) return;
            for (int i = 0; i < m.places.Length; i++) if (m.places[i]?.current != null) m.places[i].current.SetActive(i == place);
        }

        private static void SetCount(ArabicText a, RectTransform badge, int count)
        {
            if (a == null) return;
            string s = count > 99 ? "٩٩+" : CoinPill.Format(count);
            a.Text = s;
            var sz = badge.sizeDelta; sz.x = sz.y * (s.Length <= 1 ? 1f : s.Length == 2 ? 1.3f : 1.65f); badge.sizeDelta = sz;   // one digit is a circle, more widen it
        }

        /// <summary>The number on the paper tag over the rooms tab (players online). A negative number hides the tag: nothing is known yet.</summary>
        public void SetOnline(int count)
        {
            var tg = tabs.Length > 2 ? tabs[2]?.tag : null;
            if (tg == null) return;
            tg.gameObject.SetActive(count >= 0);
            var c = tg.Find("Count");
            if (c != null && count >= 0) { var a = c.GetComponent<ArabicText>(); if (a != null) a.Text = CoinPill.Format(count) + " متصل"; }
        }
    }
}
