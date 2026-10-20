using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The voice room: card table with seats around it, who is talking, the mic button and the ready button.
    /// Seats are laid out on an ellipse (index 0 at the top, clockwise).
    /// </summary>
    public class KitVoiceRoom : MonoBehaviour
    {
        /// <summary>Reports press and release of the mic button (for push-to-talk).</summary>
        private sealed class MicHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public event Action<bool> Held;
            private bool down;
            public void OnPointerDown(PointerEventData e) { down = true; Held?.Invoke(true); }
            public void OnPointerUp(PointerEventData e) { if (down) { down = false; Held?.Invoke(false); } }
            public void OnPointerExit(PointerEventData e) { if (down) { down = false; Held?.Invoke(false); } }
        }

        [SerializeField] private RectTransform seatsRoot;
        [SerializeField] private GameObject seatPrefab;
        [SerializeField] private ArabicText roomName, roomSub, speakerLabel;
        [SerializeField] private Button backButton, inviteButton, micButton, readyButton;
        [SerializeField] private KitHostDrawer hostDrawer;
        [SerializeField] private Image micIcon;
        [SerializeField] private Image micSeal;
        [SerializeField] private Vector2 radius = new Vector2(430f, 610f);
        [SerializeField] private int seatCount = 10;

        public event Action OnBack;
        public event Action OnInvite;
        public event Action<bool> OnMicChanged;
        public event Action<int> OnSeatClicked;
        /// <summary>The big bottom button was pressed (the host starting the match).</summary>
        public event Action OnStartClicked;
        /// <summary>Off with real rooms: the "who is talking" animation is only for the prototype.</summary>
        public bool SimulateSpeaking = true;
        public bool MicOn { get; private set; } = true;
        /// <summary>Push-to-talk: the mic is open only while the mic button is held (set by the app layer from the settings).</summary>
        public bool PushToTalk { get; set; }

        /// <summary>The host's control panel (lock, chat, kick).</summary>
        public KitHostDrawer HostDrawer => hostDrawer;

        private readonly List<SeatView> seats = new List<SeatView>();
        private IList<SeatInfo> infos;
        private float nextSpeakAt;
        private int speaker = -1;

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(() => OnBack?.Invoke());
            if (micButton != null)
            {
                micButton.onClick.AddListener(() => { if (!PushToTalk) ToggleMic(); });
                var relay = micButton.gameObject.AddComponent<MicHold>();
                relay.Held += down => { if (PushToTalk && MicOn != down) { MicOn = down; ApplyMic(); OnMicChanged?.Invoke(MicOn); } };
            }
            if (inviteButton != null) inviteButton.onClick.AddListener(() => OnInvite?.Invoke());
            if (readyButton != null) readyButton.onClick.AddListener(() => OnStartClicked?.Invoke());
        }

        /// <summary>Label and state of the big bottom button.</summary>
        public void SetStartButton(string label, bool enabled)
        {
            if (readyButton == null) return;
            var t = readyButton.GetComponentInChildren<ArabicText>(true);
            if (t != null) t.Text = label;
            readyButton.interactable = enabled;
            var cg = readyButton.GetComponent<CanvasGroup>();
            if (cg == null) cg = readyButton.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = enabled ? 1f : 0.55f;
        }

        public void ToggleMic()
        {
            MicOn = !MicOn;
            ApplyMic();
            OnMicChanged?.Invoke(MicOn);
        }

        /// <summary>Shows the mic state without raising the event (the app layer owns the real state).</summary>
        public void SetMicState(bool on) { MicOn = on; ApplyMic(); }

        /// <summary>The seat of a talking player glows (real rooms; the prototype animation is off).</summary>
        public void SetTalking(int index, bool on, string name)
        {
            if (index < 0 || index >= seats.Count) return;
            seats[index].SetTalking(on);
            if (speakerLabel != null && on) speakerLabel.Text = "يتكلّم الآن: " + name;
            else if (speakerLabel != null && !on && speakerLabel.Text.EndsWith(name)) speakerLabel.Text = "";
        }

        private void ApplyMic()
        {
            var th = KitTheme.Current;
            if (micIcon != null && th != null) micIcon.sprite = MicOn ? th.iconMic : th.iconMicOff;
            if (micSeal != null) micSeal.color = MicOn ? Color.white : new Color(0.6f, 0.55f, 0.5f, 1f);
        }

        public void Open(string name, string sub, IList<SeatInfo> seatInfos)
        {
            roomName.Text = name; roomSub.Text = sub;
            infos = seatInfos;
            BuildSeats(false);
            ApplyMic();
            gameObject.SetActive(true);
        }

        /// <summary>Opens (or refreshes) a real room: one seat per capacity place.</summary>
        public void Show(string name, string sub, IList<SeatInfo> seatInfos, int capacity)
        {
            seatCount = Mathf.Clamp(capacity, 2, 16);
            SimulateSpeaking = false;
            bool wasOpen = gameObject.activeSelf;
            Open(name, sub, seatInfos);
            if (speakerLabel != null) speakerLabel.Text = "";
            if (!wasOpen) ApplyMic();
        }

        /// <summary>Edit-mode build used by the scene builder.</summary>
        public void BuildSeatsImmediate(IList<SeatInfo> seatInfos)
        {
            infos = seatInfos;
            BuildSeats(true);
            ApplyMic();
        }

        private void BuildSeats(bool immediate)
        {
            for (int i = seatsRoot.childCount - 1; i >= 0; i--)
                if (immediate) DestroyImmediate(seatsRoot.GetChild(i).gameObject); else Destroy(seatsRoot.GetChild(i).gameObject);
            seats.Clear();
            for (int i = 0; i < seatCount; i++)
            {
                float a = (-90f + i * 360f / seatCount) * Mathf.Deg2Rad;
                GameObject go;
#if UNITY_EDITOR
                go = immediate ? (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(seatPrefab, seatsRoot) : Instantiate(seatPrefab, seatsRoot);
#else
                go = Instantiate(seatPrefab, seatsRoot);
#endif
                var rt = (RectTransform)go.transform;
                rt.anchoredPosition = new Vector2(Mathf.Cos(a) * radius.x, -Mathf.Sin(a) * radius.y);
                var v = go.GetComponent<SeatView>();
                // chair faces the table centre: angle of the vector pointing to the centre, relative to "down"
                float toCenter = Mathf.Atan2(Mathf.Sin(a) * radius.y, -Mathf.Cos(a) * radius.x) * Mathf.Rad2Deg;
                v.SetChairAngle(toCenter - 90f + 180f);
                v.Index = i;
                v.Clicked += idx => OnSeatClicked?.Invoke(idx);
                v.Bind(i < infos.Count ? infos[i] : null);
                seats.Add(v);
            }
        }

        private void Update()
        {
            if (!SimulateSpeaking || !Application.isPlaying || infos == null || seats.Count == 0) return;
            if (Time.unscaledTime < nextSpeakAt) return;
            nextSpeakAt = Time.unscaledTime + 2.6f;
            if (speaker >= 0 && speaker < seats.Count) seats[speaker].SetTalking(false);
            var candidates = new List<int>();
            for (int i = 0; i < infos.Count && i < seats.Count; i++) if (!infos[i].Empty && !infos[i].muted) candidates.Add(i);
            if (candidates.Count == 0) { speaker = -1; return; }
            speaker = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            seats[speaker].SetTalking(true);
            if (speakerLabel != null) speakerLabel.Text = "يتكلّم الآن: " + infos[speaker].name;
        }
    }
}
