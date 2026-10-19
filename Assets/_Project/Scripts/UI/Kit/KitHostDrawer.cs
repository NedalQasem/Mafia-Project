using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>A player as the host drawer shows them.</summary>
    public sealed class DrawerMember
    {
        public string puid, name;
        public bool isHost, isMe;
    }

    /// <summary>
    /// The host's control panel inside a room. A small tab sits half hidden at the left screen edge; pulling it to the right
    /// slides the panel out (lock the room, switch chat, kick players). Only the host sees it.
    /// </summary>
    public class KitHostDrawer : MonoBehaviour
    {
        /// <summary>Forwards taps and drags of the tab (and of the dim area behind the panel) to the drawer.</summary>
        private sealed class Pull : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
        {
            public KitHostDrawer owner;
            public bool closeOnly;
            public void OnBeginDrag(PointerEventData e) { owner.dragging = true; }
            public void OnDrag(PointerEventData e) { owner.DragBy(e.delta.x); }
            public void OnEndDrag(PointerEventData e) { owner.EndDrag(); }
            public void OnPointerClick(PointerEventData e)
            {
                if (e.dragging) return;
                if (closeOnly) owner.Close(); else owner.Toggle();
            }
        }

        [SerializeField] private RectTransform panel;
        [SerializeField] private Image scrim;
        [SerializeField] private GameObject handle;
        [SerializeField] private KitToggle lockToggle, chatToggle;
        [SerializeField] private RectTransform membersRoot;
        [SerializeField] private KitDrawerMemberRow memberTemplate;
        [SerializeField] private ArabicText membersTitle;
        [SerializeField] private float speed = 6f;

        /// <summary>The host asked for a different state; the argument is the state requested.</summary>
        public event Action<bool> OnLockChanged, OnChatChanged;
        public event Action<string> OnKick;

        public bool IsOpen => target > 0.5f;

        private readonly List<KitDrawerMemberRow> rows = new List<KitDrawerMemberRow>();
        private float progress, target;
        private bool dragging;
        private Canvas canvas;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            if (handle != null) { var p = handle.AddComponent<Pull>(); p.owner = this; }
            if (scrim != null) { var p = scrim.gameObject.AddComponent<Pull>(); p.owner = this; p.closeOnly = true; }
            if (lockToggle != null) lockToggle.OnChanged += on => OnLockChanged?.Invoke(on);
            if (chatToggle != null) chatToggle.OnChanged += on => OnChatChanged?.Invoke(on);
            if (memberTemplate != null) memberTemplate.gameObject.SetActive(false);
            Apply();
        }

        /// <summary>Shows the drawer to the host only; for everybody else it is gone.</summary>
        public void Refresh(bool isHost, bool locked, bool chatOn, IList<DrawerMember> members)
        {
            if (!isHost) { Close(true); gameObject.SetActive(false); return; }
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (lockToggle != null) lockToggle.IsOn = locked;
            if (chatToggle != null) chatToggle.IsOn = chatOn;
            Fill(members);
        }

        private void Fill(IList<DrawerMember> members)
        {
            foreach (var r in rows) if (r != null) Destroy(r.gameObject);
            rows.Clear();
            if (memberTemplate == null || membersRoot == null) return;
            int n = members?.Count ?? 0;
            if (membersTitle != null) membersTitle.Text = "اللاعبون · " + CoinPill.Format(n);
            for (int i = 0; i < n; i++)
            {
                var row = Instantiate(memberTemplate, membersRoot);
                row.gameObject.SetActive(true);
                row.Bind(members[i]);
                row.Kick += puid => OnKick?.Invoke(puid);
                rows.Add(row);
            }
        }

        public void Open() { target = 1f; }

        public void Close(bool instant = false)
        {
            target = 0f;
            if (instant) { progress = 0f; Apply(); }
        }

        public void Toggle() { target = target > 0.5f ? 0f : 1f; }

        private float PanelWidth => panel != null ? panel.rect.width : 800f;

        private void DragBy(float screenDx)
        {
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            progress = Mathf.Clamp01(progress + screenDx / scale / PanelWidth);
            target = progress;
            Apply();
        }

        private void EndDrag()
        {
            dragging = false;
            target = progress > 0.35f ? 1f : 0f;
        }

        private void Update()
        {
            if (dragging || Mathf.Approximately(progress, target)) return;
            progress = Mathf.MoveTowards(progress, target, Time.unscaledDeltaTime * speed);
            Apply();
        }

        private void Apply()
        {
            float e = progress * progress * (3f - 2f * progress);
            if (panel != null) panel.anchoredPosition = new Vector2(Mathf.Lerp(-PanelWidth, 0f, e), 0f);
            if (scrim != null)
            {
                scrim.color = new Color(0f, 0f, 0f, 0.6f * e);
                scrim.raycastTarget = progress > 0.02f;   // the dim area only catches taps while the panel is out
            }
        }

        private void OnDisable() { dragging = false; }
    }
}
