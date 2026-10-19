using System;
using MafiaNight.Data.Rooms;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>"Open your table": the form to create a room, and the ticket with the code once it exists (Docs/mockups/create-room.html).</summary>
    public class KitCreateRoomScreen : MonoBehaviour
    {
        [Header("Form")]
        [SerializeField] private GameObject formRoot;
        [SerializeField] private Button backButton, createButton;
        [SerializeField] private KitArabicInput nameInput;
        [SerializeField] private ArabicText nameCounter;
        [SerializeField] private Button[] suggestionButtons;
        [SerializeField] private Button[] typeButtons;          // public, private, vip
        [SerializeField] private ArabicText[] typeSubLabels;
        [SerializeField] private ArabicText typeHint;
        [SerializeField] private KitSegmented modeSegment;
        [SerializeField] private Button seatsMinus, seatsPlus;
        [SerializeField] private ArabicText seatsLabel;
        [SerializeField] private Image[] seatDots;
        [SerializeField] private KitToggle chatToggle, micToggle;
        [SerializeField] private ArabicText createSub, errorLabel;

        [Header("Ticket (after creation)")]
        [SerializeField] private GameObject successRoot;
        [SerializeField] private ArabicText ticketKind, ticketName, ticketCode, ticketNote;
        [SerializeField] private Button whatsappButton, copyInviteButton, copyCodeButton, enterButton, successBack;

        public event Action<RoomSettings> OnSubmit;
        public event Action OnBack;
        public event Action<RoomListing> OnEnter;

        private int level = 1;
        private bool vip;
        private RoomKind kind = RoomKind.Private;
        private int seats = 12;
        private bool busy;
        private RoomListing created;

        private static readonly string[] TypeHints =
        {
            "عامة: بتظهر بالقائمة للجميع. بتفتح عند المستوى {0}.",
            "خاصة: ما بتظهر بالقائمة. بيدخل عليها اللي معه الكود أو الدعوة.",
            "VIP: اسم ملوّن وخلفية مميّزة، وبتظهر أول القائمة. بتحتاج عضوية VIP.",
        };

        private void Awake()
        {
            backButton.onClick.AddListener(() => OnBack?.Invoke());
            successBack.onClick.AddListener(() => OnBack?.Invoke());
            createButton.onClick.AddListener(Submit);
            for (int i = 0; i < typeButtons.Length; i++) { int k = i; typeButtons[i].onClick.AddListener(() => SetKind((RoomKind)k)); }
            for (int i = 0; i < suggestionButtons.Length; i++)
            {
                var b = suggestionButtons[i];
                b.onClick.AddListener(() => { nameInput.Text = b.GetComponentInChildren<ArabicText>().Text; UpdateCounter(); });
            }
            seatsMinus.onClick.AddListener(() => SetSeats(seats - 1));
            seatsPlus.onClick.AddListener(() => SetSeats(seats + 1));
            nameInput.OnChanged += _ => { UpdateCounter(); errorLabel.Text = ""; };
            whatsappButton.onClick.AddListener(() => Application.OpenURL("https://wa.me/?text=" + Uri.EscapeDataString(InviteText())));
            copyInviteButton.onClick.AddListener(() => { GUIUtility.systemCopyBuffer = InviteText(); copyInviteButton.GetComponentInChildren<ArabicText>().Text = "اننسخت"; });
            copyCodeButton.onClick.AddListener(() => { GUIUtility.systemCopyBuffer = created != null ? created.Code : ""; copyCodeButton.GetComponentInChildren<ArabicText>().Text = "اننسخ"; });
            enterButton.onClick.AddListener(() => { if (created != null) OnEnter?.Invoke(created); });
        }

        /// <summary>Shows an empty form. <paramref name="suggestedName"/> goes in the name field.</summary>
        public void Open(int playerLevel, bool isVip, string suggestedName)
        {
            level = playerLevel; vip = isVip; busy = false; created = null;
            formRoot.SetActive(true); successRoot.SetActive(false);
            gameObject.SetActive(true); transform.SetAsLastSibling();
            nameInput.Text = suggestedName ?? "";
            errorLabel.Text = "";
            modeSegment.Set(0);
            SetSeats(12);
            chatToggle.IsOn = true; micToggle.IsOn = false;
            SetKind(RoomKind.Private);
            UpdateCounter();
        }

        public void Close() { gameObject.SetActive(false); }

        public void SetBusy(bool value) { busy = value; createButton.interactable = !value; if (value) errorLabel.Text = ""; }

        public void ShowError(string text) { busy = false; createButton.interactable = true; errorLabel.Text = text; }

        public void ShowTicket(RoomListing room)
        {
            created = room; busy = false; createButton.interactable = true;
            formRoot.SetActive(false); successRoot.SetActive(true);
            bool priv = room.Kind == RoomKind.Private;
            ticketKind.Text = priv ? "غرفة خاصة" : room.Kind == RoomKind.Vip ? "غرفة VIP" : "غرفة عامة";
            ticketName.Text = room.Name;
            ticketCode.Text = priv ? room.Code : "";
            ticketCode.gameObject.SetActive(priv);
            ticketNote.Text = priv ? "الكود بيدخّل أي لاعب، حتى لو ما كانت الغرفة بالقائمة." : "غرفتك ظاهرة بالقائمة لكل اللاعبين.";
            whatsappButton.gameObject.SetActive(priv); copyInviteButton.gameObject.SetActive(priv); copyCodeButton.gameObject.SetActive(priv);
            copyInviteButton.GetComponentInChildren<ArabicText>().Text = "نسخ الدعوة";
            copyCodeButton.GetComponentInChildren<ArabicText>().Text = "نسخ الكود";
        }

        private string InviteText() => created == null ? "" : "تعال نسهر بلعبة المافيا! ادخل بالكود " + created.Code;

        private void SetKind(RoomKind k)
        {
            kind = k;
            var th = KitTheme.Current;
            bool[] locked = { level < RoomRules.PublicMinLevel, false, !vip };
            for (int i = 0; i < typeButtons.Length; i++)
            {
                bool on = i == (int)k;
                var img = typeButtons[i].GetComponent<Image>();
                if (th != null) { img.sprite = on ? th.brassPlate : th.slotDark; }
                img.color = locked[i] && !on ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
                var texts = typeButtons[i].GetComponentsInChildren<TMPro.TMP_Text>();
                foreach (var t in texts) if (th != null) t.color = on ? th.brassText : th.ivoryDim;
                if (typeSubLabels != null && i < typeSubLabels.Length)
                    typeSubLabels[i].Text = i == 0 ? (locked[0] ? "المستوى " + CoinPill.Format(RoomRules.PublicMinLevel) : "تظهر للجميع") : i == 1 ? "بكود أو دعوة" : (locked[2] ? "بعضوية VIP" : "شكل مميّز");
            }
            string hint = string.Format(TypeHints[(int)k], CoinPill.Format(RoomRules.PublicMinLevel));
            if (locked[(int)k]) hint += k == RoomKind.Public ? " مستواك الآن " + CoinPill.Format(level) + "." : "";
            typeHint.Text = hint;
            UpdateSub();
        }

        private void SetSeats(int n)
        {
            seats = Mathf.Clamp(n, RoomRules.MinSeats, RoomRules.MaxSeats);
            seatsLabel.Text = CoinPill.Format(seats);
            var th = KitTheme.Current;
            for (int i = 0; i < seatDots.Length; i++)
                seatDots[i].color = i < seats ? (th != null ? th.ivory : Color.white) : new Color(0.08f, 0.07f, 0.06f, 1f);
            seatsMinus.interactable = seats > RoomRules.MinSeats; seatsPlus.interactable = seats < RoomRules.MaxSeats;
            UpdateSub();
        }

        private void UpdateSub()
        {
            if (createSub == null) return;
            createSub.Text = (kind == RoomKind.Public ? "غرفة عامة" : kind == RoomKind.Vip ? "غرفة VIP" : "غرفة خاصة") + " · " + CoinPill.Format(seats) + " كرسي";
        }

        private void UpdateCounter() { nameCounter.Text = CoinPill.Format(nameInput.Text.Length) + " / " + CoinPill.Format(RoomRules.MaxNameLength); }

        private void Submit()
        {
            if (busy) return;
            string name = nameInput.Text.Trim();
            if (name.Length == 0) { errorLabel.Text = "اكتب اسماً للغرفة."; return; }
            if (kind == RoomKind.Public && level < RoomRules.PublicMinLevel) { errorLabel.Text = "الغرف العامة تفتح عند المستوى " + CoinPill.Format(RoomRules.PublicMinLevel) + ". افتح غرفة خاصة بالكود."; return; }
            if (kind == RoomKind.Vip && !vip) { errorLabel.Text = "غرف VIP تحتاج عضوية VIP."; return; }
            SetBusy(true);
            OnSubmit?.Invoke(new RoomSettings
            {
                Name = name, Kind = kind, Mode = modeSegment.Selected == 1 ? RoomMode.Mafia : RoomMode.Hangout,
                Capacity = seats, ChatEnabled = chatToggle.IsOn, MicMutedOnJoin = micToggle.IsOn,
            });
        }
    }
}
