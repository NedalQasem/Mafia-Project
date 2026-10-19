using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    [System.Serializable]
    public class SeatInfo
    {
        public string name;          // null/empty = empty seat
        public int rank;
        public bool host, muted, ready;
        public bool Empty => string.IsNullOrEmpty(name);
    }

    /// <summary>A seat around the table: chair (turned toward the table), avatar, name plate and small status marks.</summary>
    public class SeatView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private RectTransform chair;       // rotated toward the table centre
        [SerializeField] private AvatarFrame avatar;
        [SerializeField] private Image dashed;               // shown for an empty seat
        [SerializeField] private ArabicText nameLabel;
        [SerializeField] private GameObject namePlate, hostBadge, mutedDot, readyDot;
        [SerializeField] private Image talkGlow;

        public int Index { get; set; }
        public event Action<int> Clicked;

        private bool talking;
        private float pulse;

        private void Awake()
        {
            // an invisible plate over the seat so a tap lands on it
            if (GetComponent<Graphic>() == null) { var hit = gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true; }
        }

        public void OnPointerClick(PointerEventData e) { Clicked?.Invoke(Index); }

        public void SetChairAngle(float degrees) { if (chair != null) chair.localRotation = Quaternion.Euler(0, 0, degrees); }

        public void Bind(SeatInfo s)
        {
            bool empty = s == null || s.Empty;
            avatar.gameObject.SetActive(!empty);
            dashed.gameObject.SetActive(empty);
            namePlate.SetActive(!empty);
            hostBadge.SetActive(!empty && s.host);
            mutedDot.SetActive(!empty && s.muted);
            readyDot.SetActive(!empty && s.ready && !s.muted);
            if (!empty) { nameLabel.Text = s.name; avatar.Set(s.rank, null, false); }
            SetTalking(false);
        }

        public void SetTalking(bool value)
        {
            talking = value;
            if (talkGlow != null) { var c = talkGlow.color; c.a = value ? 1f : 0f; talkGlow.color = c; }
        }

        private void Update()
        {
            if (!talking || talkGlow == null) return;
            pulse += Time.unscaledDeltaTime * 4f;
            var c = talkGlow.color; c.a = 0.7f + 0.3f * Mathf.Sin(pulse); talkGlow.color = c;
        }
    }
}
