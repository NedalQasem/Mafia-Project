using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MafiaNight.UI.Components
{
    /// <summary>
    /// Horizontal swipe carousel: one big card in the center, neighbours smaller on the sides.
    /// Cards are the active children of <see cref="content"/>. Supports swipe, arrow buttons and page dots.
    /// Clicks on the cards themselves are handled by their own Buttons (see MainMenuUI).
    /// </summary>
    public class RoomCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Cards")]
        [SerializeField] private RectTransform content;
        [Tooltip("Horizontal distance between card centers (canvas units).")]
        [SerializeField] private float cardSpacing = 620f;
        [SerializeField] private float sideCardScale = 0.75f;
        [SerializeField, Range(0f, 1f)] private float sideCardAlpha = 0.6f;
        [SerializeField] private float snapSpeed = 12f;

        [Header("Swipe")]
        [Tooltip("Fraction of a card spacing the finger must travel to change card.")]
        [SerializeField, Range(0.05f, 0.5f)] private float swipeThreshold = 0.15f;

        [Header("Navigation (optional)")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private List<Image> pageDots = new List<Image>();
        [SerializeField] private Color activeDotColor = new Color(1f, 0.82f, 0.35f);
        [SerializeField] private Color inactiveDotColor = new Color(1f, 1f, 1f, 0.35f);

        public event Action<int> OnIndexChanged;

        public int CurrentIndex { get; private set; }
        public int Count => _cards.Count;

        private readonly List<RectTransform> _cards = new List<RectTransform>();
        private readonly List<CanvasGroup> _cardGroups = new List<CanvasGroup>();
        private float _visualPosition;   // current (animated) position in "card units"
        private float _dragOffset;       // live drag in card units
        private bool _isDragging;
        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            CollectCards();

            if (previousButton != null) previousButton.onClick.AddListener(Previous);
            if (nextButton != null) nextButton.onClick.AddListener(Next);

            _visualPosition = CurrentIndex;
            ApplyLayout();
            RefreshNavigation();
        }

        private void OnDestroy()
        {
            if (previousButton != null) previousButton.onClick.RemoveListener(Previous);
            if (nextButton != null) nextButton.onClick.RemoveListener(Next);
        }

        private void CollectCards()
        {
            _cards.Clear();
            _cardGroups.Clear();
            if (content == null) return;

            foreach (Transform child in content)
            {
                if (!child.gameObject.activeSelf) continue;
                var card = (RectTransform)child;
                card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
                card.pivot = new Vector2(0.5f, 0.5f);

                var group = card.GetComponent<CanvasGroup>();
                if (group == null) group = card.gameObject.AddComponent<CanvasGroup>();

                _cards.Add(card);
                _cardGroups.Add(group);
            }
        }

        #region Public API

        public void GoTo(int index)
        {
            if (_cards.Count == 0) return;
            int clamped = Mathf.Clamp(index, 0, _cards.Count - 1);
            if (clamped == CurrentIndex) return;

            CurrentIndex = clamped;
            RefreshNavigation();
            OnIndexChanged?.Invoke(CurrentIndex);
        }

        public void Next() => GoTo(CurrentIndex + 1);
        public void Previous() => GoTo(CurrentIndex - 1);

        #endregion

        #region Swipe

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _dragOffset = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Screen pixels -> content units (canvas scale x content scale)
            float scale = (_canvas != null ? _canvas.scaleFactor : 1f) * (content != null ? content.localScale.x : 1f);
            // Finger moving left = go to the next card (positive position)
            float delta = -eventData.delta.x / scale / cardSpacing;

            // Rubber-band past the first/last card
            float position = _visualPosition + _dragOffset;
            if (position < 0f || position > _cards.Count - 1) delta *= 0.35f;

            _dragOffset += delta;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
            _visualPosition += _dragOffset;
            _dragOffset = 0f;

            float moved = _visualPosition - CurrentIndex;
            if (moved > swipeThreshold) Next();
            else if (moved < -swipeThreshold) Previous();
        }

        #endregion

        private void Update()
        {
            if (_cards.Count == 0) return;

            if (!_isDragging)
            {
                float t = 1f - Mathf.Exp(-snapSpeed * Time.unscaledDeltaTime);
                _visualPosition = Mathf.Lerp(_visualPosition, CurrentIndex, t);
            }

            ApplyLayout();
        }

        private void ApplyLayout()
        {
            float position = _visualPosition + (_isDragging ? _dragOffset : 0f);

            for (int i = 0; i < _cards.Count; i++)
            {
                float distance = i - position;
                float abs = Mathf.Min(Mathf.Abs(distance), 1f);

                _cards[i].anchoredPosition = new Vector2(distance * cardSpacing, 0f);
                _cards[i].localScale = Vector3.one * Mathf.Lerp(1f, sideCardScale, abs);
                _cardGroups[i].alpha = Mathf.Lerp(1f, sideCardAlpha, abs);
            }

            // Draw the focused card on top of its neighbours
            int nearest = Mathf.Clamp(Mathf.RoundToInt(position), 0, _cards.Count - 1);
            if (_cards[nearest].GetSiblingIndex() != content.childCount - 1) _cards[nearest].SetAsLastSibling();
        }

        private void RefreshNavigation()
        {
            if (previousButton != null) previousButton.interactable = CurrentIndex > 0;
            if (nextButton != null) nextButton.interactable = CurrentIndex < _cards.Count - 1;

            for (int i = 0; i < pageDots.Count; i++)
            {
                if (pageDots[i] != null) pageDots[i].color = i == CurrentIndex ? activeDotColor : inactiveDotColor;
            }
        }
    }
}
