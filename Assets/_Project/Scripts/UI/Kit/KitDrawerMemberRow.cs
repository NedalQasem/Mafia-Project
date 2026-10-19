using System;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>One player in the host drawer: name, host star, and a kick button that needs a second tap to confirm.</summary>
    public class KitDrawerMemberRow : MonoBehaviour
    {
        private const float ConfirmWindow = 3f;

        [SerializeField] private ArabicText nameLabel, kickLabel;
        [SerializeField] private Button kickButton;
        [SerializeField] private GameObject star;

        public event Action<string> Kick;
        private string puid;
        private float confirmUntil;

        private void Awake()
        {
            if (kickButton != null) kickButton.onClick.AddListener(OnKickTapped);
        }

        public void Bind(DrawerMember m)
        {
            puid = m.puid;
            if (nameLabel != null) nameLabel.Text = m.name + (m.isMe ? " (أنت)" : "");
            if (star != null) star.SetActive(m.isHost);
            // the host can't kick themselves
            if (kickButton != null) kickButton.gameObject.SetActive(!m.isMe);
            Reset();
        }

        private void OnKickTapped()
        {
            if (Time.unscaledTime < confirmUntil) { Reset(); Kick?.Invoke(puid); return; }
            confirmUntil = Time.unscaledTime + ConfirmWindow;
            if (kickLabel != null) kickLabel.Text = "تأكيد؟";
        }

        private void Update()
        {
            if (confirmUntil > 0f && Time.unscaledTime >= confirmUntil) Reset();
        }

        private void Reset()
        {
            confirmUntil = 0f;
            if (kickLabel != null) kickLabel.Text = "طرد";
        }
    }
}
