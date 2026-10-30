using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The mail ("البريد") that opens from the quick menu (Docs/mockups/mailbox.html): the same leather folder as the events board, with the
    /// telegrams the admin sent as paper sheets, "collect all" on top and a strip for promo codes at the bottom. Create it from the prefab,
    /// then <see cref="Show"/>; the app answers <see cref="OnClaim"/> / <see cref="OnClaimAll"/> and reports back with <see cref="ClaimDone"/>.
    /// </summary>
    public sealed class KitMailBoard : MonoBehaviour
    {
        /// <summary>One telegram as the board shows it (the app maps its data onto this).</summary>
        public sealed class Card
        {
            public string id, from, when, title, body, expiry;
            public int coins, gems;
            public string[] items;
            public bool claimed;
            /// <summary>0 = a telegram with a package, 1 = a private note, 2 = a warning. For a message, claimed means read.</summary>
            public int type;
            public string imageId;
            public bool must;
        }

        [SerializeField] private RectTransform board, content;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private CanvasGroup scrimGroup, boardGroup, toastGroup;
        [SerializeField] private Button closeButton, scrimButton, claimAllButton;
        [SerializeField] private GameObject emptyNote, codeStrip;
        [SerializeField] private ArabicText countText, newText, toastText, codeMessage;
        [SerializeField] private TMPro.TMP_InputField codeInput;
        [SerializeField] private Button redeemButton;
        [SerializeField] private GameObject cardPrefab;

        /// <summary>The "collect" button of a telegram.</summary>
        public event Action<Card> OnClaim;
        /// <summary>The player typed a promo code and pressed "activate".</summary>
        public event Action<string> OnRedeem;
        /// <summary>"Collect all".</summary>
        public event Action OnClaimAll;
        /// <summary>The board is gone.</summary>
        public event Action OnClosed;

        private readonly List<Card> items = new List<Card>();
        private readonly List<KitMailCard> cards = new List<KitMailCard>();
        private float toastLeft;
        private bool closing;

        private void Awake()
        {
            if (closeButton == null) return;     // the prefab builder adds this component before its fields are set
            closeButton.onClick.AddListener(Close);
            scrimButton.onClick.AddListener(Close);
            claimAllButton.onClick.AddListener(() => OnClaimAll?.Invoke());
            redeemButton.onClick.AddListener(() => { var c = (codeInput.text ?? "").Trim(); if (c.Length == 0) { CodeResult(false, "اكتب الكود أول"); return; } codeMessage.Text = ""; OnRedeem?.Invoke(c); });
            toastGroup.alpha = 0f; scrimGroup.alpha = 0f; boardGroup.alpha = 0f;
        }

        /// <summary>Shows the promo code strip (the app turns it on once codes exist).</summary>
        public void SetCodeStrip(bool on) { if (codeStrip != null) codeStrip.SetActive(on); }

        public void Show(IList<Card> list)
        {
            Fill(list);
            StartCoroutine(Enter());
        }

        /// <summary>Replaces what the board shows (the app fetched again).</summary>
        public void Refresh(IList<Card> list) => Fill(list);

        private void Fill(IList<Card> list)
        {
            var copy = list != null ? new List<Card>(list) : new List<Card>();      // the list may be our own
            items.Clear(); items.AddRange(copy);
            foreach (var c in cards) if (c != null) Destroy(c.gameObject);
            cards.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                var go = Instantiate(cardPrefab, content, false);
                var card = go.GetComponent<KitMailCard>();
                card.Bind(items[i], i);
                card.OnClaim += c => OnClaim?.Invoke(c);
                cards.Add(card);
            }
            int fresh = 0, packs = 0; foreach (var c in items) if (!c.claimed) { fresh++; if (c.type == 0) packs++; }
            countText.Text = CoinPill.Format(items.Count) + " برقيات";
            newText.Text = fresh > 0 ? CoinPill.Format(fresh) + " جديدة" : "";
            claimAllButton.gameObject.SetActive(packs > 1);       // messages are read one by one, "collect all" is for packages
            emptyNote.SetActive(items.Count == 0);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        /// <summary>The server answered a claim of one telegram.</summary>
        public void ClaimDone(string id, bool ok, string message)
        {
            if (ok) foreach (var c in items) if (c.id == id) c.claimed = true;
            Refresh(items);
            Toast(message);
        }

        /// <summary>The server answered a promo code: a green line when it worked (the field is cleared), a red one when not.</summary>
        public void CodeResult(bool ok, string message)
        {
            codeMessage.Text = message;
            codeMessage.GetComponent<TMPro.TMP_Text>().color = ok ? new Color32(0x8F, 0xE0, 0xBD, 255) : new Color32(0xFF, 0x8B, 0x8B, 255);
            if (ok) codeInput.text = "";
        }

        public void Toast(string text) { toastText.Text = text; toastLeft = 2.6f; }

        private void Update()
        {
            if (toastLeft > 0f) { toastLeft -= Time.unscaledDeltaTime; toastGroup.alpha = Mathf.Clamp01(toastLeft / 0.3f) * Mathf.Clamp01((2.6f - toastLeft) / 0.15f + 0.2f); }
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
