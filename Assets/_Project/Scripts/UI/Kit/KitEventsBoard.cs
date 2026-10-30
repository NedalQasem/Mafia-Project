using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The events board that opens from the quick menu (Docs/mockups/events.html): a leather folder with a newspaper masthead, three index tabs
    /// (running, soon, finished) and the events as paper clippings. Create it from the prefab, then <see cref="Show"/>. The app answers the
    /// button of a clipping (<see cref="OnAction"/>) and calls <see cref="ClaimDone"/> with the result.
    /// </summary>
    public sealed class KitEventsBoard : MonoBehaviour
    {
        /// <summary>One event as the board shows it (the app maps its data onto this).</summary>
        public sealed class Card
        {
            public string id, kicker, title, body;
            public int kind;                              // 0 announcement, 1 coins bonus, 2 challenge
            public int state;                             // 0 running, 1 soon, 2 finished
            public int multiplier, goal, progress, reward;
            public bool countsGames, claimed, claimable, secret;
            public int seconds;                           // left while running, until it starts while soon
        }

        [SerializeField] private RectTransform board, content;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private CanvasGroup scrimGroup, boardGroup, toastGroup;
        [SerializeField] private Button closeButton, scrimButton;
        [SerializeField] private Button[] tabButtons = new Button[3];
        [SerializeField] private Image[] tabImages = new Image[3];
        [SerializeField] private ArabicText[] tabLabels = new ArabicText[3];
        [SerializeField] private GameObject liveBadge, emptyNote;
        [SerializeField] private ArabicText liveBadgeText, countText, toastText, emptyText;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Sprite paperTab, leatherTab;

        /// <summary>The button of a clipping was pressed: a bonus asks to play, a challenge asks to claim.</summary>
        public event Action<Card> OnAction;
        /// <summary>The board is gone.</summary>
        public event Action OnClosed;

        private readonly List<Card>[] lists = { new List<Card>(), new List<Card>(), new List<Card>() };
        private readonly List<KitEventCard> cards = new List<KitEventCard>();
        private int tab = -1;
        private float toastLeft;
        private bool closing;

        private void Awake()
        {
            if (closeButton == null) return;     // the prefab builder adds this component before its fields are set
            closeButton.onClick.AddListener(Close);
            scrimButton.onClick.AddListener(Close);
            for (int i = 0; i < 3; i++) { int k = i; tabButtons[i].onClick.AddListener(() => Select(k)); }
            toastGroup.alpha = 0f; scrimGroup.alpha = 0f; boardGroup.alpha = 0f;
        }

        public void Show(IList<Card> live, IList<Card> soon, IList<Card> done)
        {
            Fill(live, soon, done);
            Select(lists[0].Count > 0 || lists[1].Count == 0 ? 0 : 1);
            StartCoroutine(Enter());
        }

        /// <summary>Replaces what the board shows (the app fetched again) and keeps the tab the player is on.</summary>
        public void Refresh(IList<Card> live, IList<Card> soon, IList<Card> done)
        {
            Fill(live, soon, done);
            int t = tab; tab = -1; Select(Mathf.Max(0, t));
        }

        private void Fill(IList<Card> live, IList<Card> soon, IList<Card> done)
        {
            lists[0].Clear(); lists[1].Clear(); lists[2].Clear();
            if (live != null) lists[0].AddRange(live);
            if (soon != null) lists[1].AddRange(soon);
            if (done != null) lists[2].AddRange(done);
            bool any = lists[0].Count > 0;
            liveBadge.SetActive(any); liveBadgeText.Text = CoinPill.Format(lists[0].Count);
            countText.Text = any ? CoinPill.Format(lists[0].Count) + " شغّالة" : "";
        }

        private void Select(int index)
        {
            if (index == tab) return;
            tab = index;
            for (int i = 0; i < 3; i++)
            {
                bool on = i == index;
                tabImages[i].sprite = on ? paperTab : leatherTab;
                tabImages[i].color = Color.white;
                tabLabels[i].GetComponent<TMPro.TMP_Text>().color = on ? new Color32(0x1C, 0x17, 0x0F, 255) : new Color32(0xB9, 0xA9, 0x8A, 255);
                var r = (RectTransform)tabButtons[i].transform;
                r.anchoredPosition = new Vector2(r.anchoredPosition.x, on ? 0f : -8f);
                if (on) r.SetAsLastSibling();
            }
            foreach (var c in cards) if (c != null) Destroy(c.gameObject);
            cards.Clear();
            var list = lists[index];
            for (int i = 0; i < list.Count; i++)
            {
                var go = Instantiate(cardPrefab, content, false);
                var card = go.GetComponent<KitEventCard>();
                card.Bind(list[i], i);
                card.OnAction += c => OnAction?.Invoke(c);
                cards.Add(card);
            }
            emptyNote.SetActive(list.Count == 0);
            emptyText.Text = index == 0 ? "ما في أحداث شغّالة هلأ. مرّ علينا بعدين." : index == 1 ? "ما في أحداث قادمة معلنة." : "ما في أحداث منتهية بعد.";
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>The server answered a claim: coins &gt; 0 means paid, 0 means it failed.</summary>
        public void ClaimDone(string id, int coins)
        {
            foreach (var l in lists) foreach (var c in l) if (c.id == id && coins > 0) { c.claimed = true; c.claimable = false; }
            foreach (var kc in cards) if (kc != null && kc.Card != null && kc.Card.id == id) kc.Bind(kc.Card, cards.IndexOf(kc));
            Toast(coins > 0 ? "+" + CoinPill.Format(coins) + " ذهب" : "ما زبطت، جرّب بعد شوي");
        }

        public void Toast(string text) { toastText.Text = text; toastLeft = 2.4f; }

        private void Update()
        {
            if (toastLeft > 0f) { toastLeft -= Time.unscaledDeltaTime; toastGroup.alpha = Mathf.Clamp01(toastLeft / 0.3f) * Mathf.Clamp01((2.4f - toastLeft) / 0.15f + 0.2f); }
            else toastGroup.alpha = 0f;
        }

        private IEnumerator Enter()
        {
            var rest = board.anchoredPosition;
            for (float t = 0f; t < 0.32f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.32f), 3f);
                scrimGroup.alpha = Mathf.Clamp01(t / 0.25f); boardGroup.alpha = Mathf.Clamp01(t / 0.25f);
                board.anchoredPosition = rest + new Vector2(100f * (1f - k), 0f);
                board.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, k);
                yield return null;
            }
            scrimGroup.alpha = 1f; boardGroup.alpha = 1f; board.anchoredPosition = rest; board.localScale = Vector3.one;
        }

        public void Close()
        {
            if (closing) return;
            closing = true;
            StartCoroutine(Leave());
        }

        private IEnumerator Leave()
        {
            var rest = board.anchoredPosition;
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.2f;
                scrimGroup.alpha = 1f - k; boardGroup.alpha = 1f - k;
                board.anchoredPosition = rest + new Vector2(60f * k, 0f);
                board.localScale = Vector3.one * Mathf.Lerp(1f, 0.96f, k);
                yield return null;
            }
            OnClosed?.Invoke();
            Destroy(gameObject);
        }
    }
}
