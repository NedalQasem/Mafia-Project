using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The small telegram that slides out from behind the quick menu tab when something new happens (Docs/mockups/quick-notify.html):
    /// a new telegram, a prize to collect, a new event, an event about to end, a new offer. One at a time, the others wait in turn
    /// ("+2" ink stamp). It stays 4.5 s (a dotted ink line runs out), a tap opens what it is about, a swipe to the edge sends it away.
    /// The tab knocks and its ring pulses in the colour of the wax. It waits while the menu is busy (open, carried, in a room) or the app says so.
    /// Positions are the mockup's pixels times K, in the tab's root (pivot top-left).
    /// </summary>
    public sealed class KitQuickNotice : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public enum Kind { Mail, Reward, Event, Ending, Offer }

        [SerializeField] private float k = 1080f / 390f;
        [SerializeField] private RectTransform slip, seal, texts, time, stamp;
        [SerializeField] private Image sealImage, timeImage;
        [SerializeField] private ArabicText title, detail;
        [SerializeField] private TextMeshProUGUI stampText;
        [SerializeField] private Button tapButton;
        [SerializeField] private CanvasGroup slipGroup;
        [SerializeField] private Sprite sealMail, sealReward, sealEvent, sealEnding, sealOffer;

        /// <summary>The app can hold the notices back (another sheet or popup is open).</summary>
        public Func<bool> CanShow;
        /// <summary>The app's record of news already seen: a queued notice whose keys are all seen meanwhile (its board was opened) is dropped.</summary>
        public Func<string, bool> IsSeen;
        /// <summary>Called with each key of a notice when it really comes out (only then does it count as seen).</summary>
        public Action<string> Shown;

        private sealed class Item { public Kind kind; public string title, detail; public Action tap; public string[] keys; }
        private readonly Queue<Item> queue = new Queue<Item>();
        private KitQuickMenu menu;
        private Item showing;
        private float shownFor, dragX;
        private bool sideRight = true, leaving, dragged;
        private Coroutine anim;

        private const float SlipW = 256f, SlipH = 58f, Stay = 4.5f;
        private static readonly Color[] Ring = { new Color32(0xD8, 0x40, 0x4A, 255), new Color32(0xE8, 0xB0, 0x4A, 255), new Color32(0x3F, 0xAE, 0x8C, 255), new Color32(0xE0, 0x7A, 0x3A, 255), new Color32(0xB9, 0x90, 0x48, 255) };

        /// <summary>Puts the notice behind the tab of this menu (it follows the tab and its side).</summary>
        public void Attach(KitQuickMenu quickMenu)
        {
            menu = quickMenu;
            transform.SetParent(menu.Anchor, false);
            transform.SetAsFirstSibling();                  // behind the tab: the telegram comes out from under it
            slipGroup.alpha = 0f; slipGroup.blocksRaycasts = false;
            tapButton.onClick.AddListener(Tap);
            Layout(menu.IsRight);
        }

        /// <summary>Adds news to the queue (shown when its turn comes and the menu is free).</summary>
        public void Enqueue(Kind kind, string titleText, string detailText, Action onTap, params string[] keys)
        {
            queue.Enqueue(new Item { kind = kind, title = titleText, detail = detailText, tap = onTap, keys = keys ?? new string[0] });
            UpdateMore();
        }

        public int Waiting => queue.Count + (showing != null ? 1 : 0);

        // ---------------------------------------------------------------- layout for a side (the right one is the mockup; the left one is its mirror)
        private void Layout(bool right)
        {
            sideRight = right;
            float s = right ? 1f : -1f;
            var root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f); root.pivot = new Vector2(right ? 1f : 0f, .5f);
            // the slot ends 20 px inside the tab's outer edge; it is 6 px above the tab and 64 px high
            root.anchoredPosition = new Vector2(right ? 83f - 20f * k : 20f * k, -(-6f + 32f) * k);
            root.sizeDelta = new Vector2(266f * k, 64f * k);
            slip.anchorMin = slip.anchorMax = new Vector2(right ? 1f : 0f, .5f); slip.pivot = new Vector2(right ? 1f : 0f, .5f);
            slip.localRotation = Quaternion.Euler(0, 0, s * 1f);          // the CSS rotate(-1deg) of the right side
            // inside the slip: seal next to the tab, texts and the time line away from it, the "+2" stamp at the far corner
            // (the children are anchored at the slip's middle: 256 x 58 px, padding 18 on the tab's side and 12 on the other)
            seal.anchoredPosition = new Vector2(s * (SlipW / 2f - 18f - 19f) * k, 0f);
            texts.anchoredPosition = new Vector2(s * -26.5f * k, 0f);
            time.anchoredPosition = new Vector2(s * -25f * k, -21f * k);
            timeImage.fillOrigin = right ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            stamp.anchoredPosition = new Vector2(s * -102f * k, 15f * k);
            stamp.localRotation = Quaternion.Euler(0, 0, s * 8f);
            // the texts hug the seal: right-aligned on the right side, left-aligned on the mirrored one (the "+2" stamp keeps the far corner)
            var align = right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            title.GetComponent<TMP_Text>().alignment = align; detail.GetComponent<TMP_Text>().alignment = align;
            slip.anchoredPosition = Hidden();
        }

        private Vector2 Hidden() => new Vector2((sideRight ? 1f : -1f) * 1.05f * SlipW * k, 0f);

        // ---------------------------------------------------------------- the turn
        private void Update()
        {
            if (menu == null) return;
            // nobody is looking (another app, or another window over the editor): the news waits and the time stands still
            if (!Application.isFocused) return;
            if (menu.IsRight != sideRight && showing == null) Layout(menu.IsRight);
            bool free = !menu.Busy && (CanShow == null || CanShow());
            if (showing == null)
            {
                while (queue.Count > 0 && Stale(queue.Peek())) queue.Dequeue();
                if (queue.Count > 0 && free && anim == null) Show(queue.Dequeue());
                return;
            }
            if (!free && !leaving) { Leave(); return; }                         // the menu got busy: away now, it will not come back
            if (leaving || dragged) return;
            shownFor += Time.unscaledDeltaTime;
            timeImage.fillAmount = Mathf.Clamp01(1f - shownFor / Stay);
            if (shownFor >= Stay) Leave();
        }

        private bool Stale(Item it)
        {
            if (IsSeen == null || it.keys.Length == 0) return false;
            foreach (var key in it.keys) if (!IsSeen(key)) return false;
            return true;
        }

        private void Show(Item it)
        {
            if (Shown != null) foreach (var key in it.keys) Shown(key);
            showing = it; shownFor = 0f; leaving = false; dragged = false;
            KitText.Fit(title, it.title, 26, 22f); KitText.Fit(detail, it.detail, 44, 18f);       // server text of any length stays on its line
            sealImage.sprite = it.kind == Kind.Mail ? sealMail : it.kind == Kind.Reward ? sealReward : it.kind == Kind.Event ? sealEvent : it.kind == Kind.Ending ? sealEnding : sealOffer;
            timeImage.fillAmount = 1f;
            UpdateMore();
            menu.Knock(Ring[(int)it.kind]);
            slipGroup.alpha = 1f; slipGroup.blocksRaycasts = true;
            anim = StartCoroutine(SlideIn());
        }

        private IEnumerator SlideIn()
        {
            yield return new WaitForSecondsRealtime(.12f);
            Vector2 a = Hidden(), b = Vector2.zero;
            seal.localScale = Vector3.zero;
            for (float t = 0; t < .75f; t += Time.unscaledDeltaTime)
            {
                // the slip: 0.45 s with a little overshoot (cubic-bezier .25,1.35,.45,1); the seal is stamped after 0.25 s
                float u = Mathf.Clamp01(t / .45f);
                slip.anchoredPosition = Vector2.LerpUnclamped(a, b, BackOut(u, 1.3f));
                float v = Mathf.Clamp01((t - .25f) / .5f), e = BackOut(v, 2.2f);
                seal.localScale = Vector3.one * Mathf.LerpUnclamped(1.7f, 1f, e) * (v > 0f ? 1f : 0f);
                seal.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(25f, 0f, e) * (sideRight ? 1f : -1f));
                var c = sealImage.color; c.a = Mathf.Clamp01(v * 2f); sealImage.color = c;
                yield return null;
            }
            slip.anchoredPosition = b; seal.localScale = Vector3.one; seal.localRotation = Quaternion.identity;
            anim = null;
        }

        private void Leave()
        {
            if (showing == null || leaving) return;
            leaving = true;
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(SlideOut());
        }

        private IEnumerator SlideOut()
        {
            Vector2 a = slip.anchoredPosition, b = Hidden();
            for (float t = 0; t < .3f; t += Time.unscaledDeltaTime)
            {
                float u = t / .3f;
                slip.anchoredPosition = Vector2.Lerp(a, b, u * u); slipGroup.alpha = 1f - u;
                yield return null;
            }
            slip.anchoredPosition = b; slipGroup.alpha = 0f; slipGroup.blocksRaycasts = false;
            showing = null; leaving = false;
            yield return new WaitForSecondsRealtime(.25f);
            anim = null;
        }

        private void Tap()
        {
            if (showing == null || leaving || dragged) return;
            var act = showing.tap;
            Leave();
            act?.Invoke();
        }

        private void UpdateMore()
        {
            int n = queue.Count;
            stamp.gameObject.SetActive(showing != null && n > 0);
            stampText.text = "+" + CoinPill.Format(n);
        }

        // ---------------------------------------------------------------- swipe it back to the edge
        public void OnBeginDrag(PointerEventData e) { if (showing == null || leaving) return; dragged = true; dragX = 0f; }

        public void OnDrag(PointerEventData e)
        {
            if (!dragged || showing == null || leaving) return;
            dragX += e.delta.x / (slip.lossyScale.x > 0f ? slip.lossyScale.x : 1f);
            float d = sideRight ? Mathf.Max(0f, dragX) : Mathf.Min(0f, dragX);
            slip.anchoredPosition = new Vector2(d, 0f);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!dragged) return;
            if (Mathf.Abs(dragX) > 60f * k) { Leave(); StartCoroutine(Undrag()); return; }
            slip.anchoredPosition = Vector2.zero;
            StartCoroutine(Undrag());
        }

        private IEnumerator Undrag() { yield return null; dragged = false; }

        private static float BackOut(float u, float s) { u -= 1f; return u * u * ((s + 1f) * u + s) + 1f; }
    }
}
