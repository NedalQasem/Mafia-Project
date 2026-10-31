using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Text chat: public square, direct messages, groups. Tabs on top, a conversation view with leather bubbles,
    /// a composer with a send button (public chat has a 5 second slow mode).
    /// The screen only shows what it is given: the app layer (ChatFlow) fills it from the chat service and listens to its events.
    /// Without live services it shows sample conversations (the prototype scene).
    /// </summary>
    public class KitChatScreen : MonoBehaviour
    {
        public const string NewGroupId = "__new_group";

        [Header("Tabs / header")]
        [SerializeField] private Button[] tabs = new Button[3];
        [SerializeField] private GameObject[] tabBadges = new GameObject[3];
        [SerializeField] private ArabicText[] tabBadgeLabels = new ArabicText[3];
        [SerializeField] private GameObject tabsRow, convHeader, publicNotice;
        [SerializeField] private Button backButton;
        [SerializeField] private ArabicText convTitle, convSub;
        [Header("Lists / messages")]
        [SerializeField] private GameObject listScroll, msgScroll;
        [SerializeField] private RectTransform listContent, msgContent;
        [SerializeField] private ScrollRect msgScrollRect;
        [SerializeField] private GameObject listItemPrefab, msgOtherPrefab, msgMinePrefab, msgSysPrefab, msgDayPrefab;
        [Header("Composer")]
        [SerializeField] private GameObject composer;
        [SerializeField] private TMP_InputField input;
        [SerializeField] private Button sendButton;
        [SerializeField] private ArabicText cooldownLabel;
        [SerializeField] private GameObject sendIcon;

        private ChatConversation pub; private List<ChatConversation> dms = new List<ChatConversation>(), groups = new List<ChatConversation>();
        private ChatConversation current;
        private int tab;
        private int cooldown;
        private Coroutine coolRoutine;
        private bool inited;

        /// <summary>The player pressed send in a conversation.</summary>
        public event Action<ChatConversation, string> OnSend;
        /// <summary>A conversation was opened (its messages count as read).</summary>
        public event Action<ChatConversation> OnOpened;
        /// <summary>The player left the open conversation (back to the list) .</summary>
        public event Action OnClosed;
        /// <summary>The player tapped somebody else's message (to message him, report or block).</summary>
        public event Action<ChatConversation, ChatMessage> OnMessageTapped;
        /// <summary>The player tapped the title of the open direct chat or group.</summary>
        public event Action<ChatConversation> OnHeaderTapped;
        /// <summary>The player pressed "new group".</summary>
        public event Action OnNewGroup;

        public ChatConversation Current => current;
        public bool IsOpen => current != null;

        private void Start() { if (Application.isPlaying) Init(); }

        public void Init()
        {
            if (inited) return;
            inited = true;
            if (pub == null && !KitLiveMode.Active) { pub = ChatData.Public(); dms = ChatData.Direct(); groups = ChatData.Groups(); sample = true; }
            for (int i = 0; i < tabs.Length; i++) { int idx = i; tabs[i].onClick.AddListener(() => SelectTab(idx)); }
            backButton.onClick.AddListener(() => { current = null; OnClosed?.Invoke(); Refresh(); });
            if (!sample && convHeader != null) KitTap.On(convHeader.transform, () => { if (current != null) OnHeaderTapped?.Invoke(current); });
            sendButton.onClick.AddListener(Send);
            input.onSubmit.AddListener(_ => Send());
            SelectTab(0);
        }

        private bool sample;

        /// <summary>Gives the screen its conversations (the same objects are reused between calls, so the open one stays open).</summary>
        public void SetData(ChatConversation publicChat, List<ChatConversation> direct, List<ChatConversation> group)
        {
            sample = false;
            pub = publicChat; dms = direct ?? new List<ChatConversation>(); groups = group ?? new List<ChatConversation>();
            if (!inited) return;
            if (current != null && current != pub && !dms.Contains(current) && !groups.Contains(current)) current = null;
            Refresh(true);
        }

        /// <summary>A message was added to a conversation: show it if that conversation is open.</summary>
        public void Push(ChatConversation c, ChatMessage m)
        {
            if (!inited) return;
            if (current == c) { AddView(m, c); ScrollDown(); }
        }

        /// <summary>Opens a conversation (switching to its tab).</summary>
        public void Show(ChatConversation c)
        {
            if (!inited || c == null) return;
            tab = c.kind == ChatKind.Public ? 0 : c.kind == ChatKind.Direct ? 1 : 2;
            OpenConversation(c);
        }

        public void SelectTab(int i) { tab = i; current = null; Refresh(); }

        public void StartCooldown(int seconds)
        {
            if (!isActiveAndEnabled) return;
            if (coolRoutine != null) StopCoroutine(coolRoutine);
            coolRoutine = StartCoroutine(Cool(seconds));
        }

        private void Refresh(bool keepOpen = false)
        {
            var th = KitTheme.Current;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool on = i == tab;
                tabs[i].GetComponent<Image>().sprite = on ? th.brassPlate : th.leatherDark;
                tabs[i].GetComponentInChildren<TMP_Text>().color = on ? th.brassText : th.ivoryDim;
                int u = i == 1 ? Sum(dms) : i == 2 ? Sum(groups) : 0;
                tabBadges[i].SetActive(u > 0); if (u > 0) tabBadgeLabels[i].Text = KitText.Badge(u);
            }
            if (keepOpen && current != null) { RenderConversation(current, false); return; }
            if (tab == 0) { OpenConversation(pub); return; }
            // list view
            current = null;
            listScroll.SetActive(true); msgScroll.SetActive(false); composer.SetActive(false);
            tabsRow.SetActive(true); convHeader.SetActive(false); publicNotice.SetActive(false);
            Clear(listContent);
            if (tab == 2)
            {   // the first row of the groups list starts a new group
                var go = Instantiate(listItemPrefab, listContent);
                var v = go.GetComponent<ChatListItemView>();
                v.Bind(new ChatConversation { id = NewGroupId, kind = ChatKind.Group, name = "+ مجموعة جديدة", last = "اختر أصدقاءك من المحادثات الخاصة", members = new string[0] });
                v.OnClick += _ => OnNewGroup?.Invoke();
            }
            foreach (var c in tab == 1 ? dms : groups)
            {
                var go = Instantiate(listItemPrefab, listContent);
                var v = go.GetComponent<ChatListItemView>(); v.Bind(c);
                v.OnClick += cc => { OpenConversation(cc); };
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
        }

        private static int Sum(List<ChatConversation> l) { int s = 0; foreach (var c in l) s += c.unread; return s; }
        private static void Clear(RectTransform r) { for (int i = r.childCount - 1; i >= 0; i--) { if (Application.isPlaying) Destroy(r.GetChild(i).gameObject); else DestroyImmediate(r.GetChild(i).gameObject); } }

        private void OpenConversation(ChatConversation c)
        {
            if (c == null) return;
            current = c; c.unread = 0;
            RenderConversation(c, true);
            OnOpened?.Invoke(c);
        }

        private void RenderConversation(ChatConversation c, bool refreshTabs)
        {
            bool isPublic = c.kind == ChatKind.Public;
            listScroll.SetActive(false); msgScroll.SetActive(true); composer.SetActive(true);
            tabsRow.SetActive(isPublic); convHeader.SetActive(!isPublic); publicNotice.SetActive(isPublic);
            if (!isPublic)
            {
                convTitle.Text = c.name;
                convSub.Text = c.kind == ChatKind.Group ? CoinPill.Format((c.members?.Length ?? 0)) + " أعضاء" : (c.online ? "متصل الآن" : "غير متصل");
            }
            Clear(msgContent);
            foreach (var m in c.messages) AddView(m, c);
            ScrollDown();
            if (refreshTabs)
            {
                var th = KitTheme.Current;
                for (int i = 0; i < tabs.Length; i++)
                {
                    int u = i == 1 ? Sum(dms) : i == 2 ? Sum(groups) : 0;
                    tabBadges[i].SetActive(u > 0); if (u > 0) tabBadgeLabels[i].Text = KitText.Badge(u);
                }
            }
        }

        private void AddView(ChatMessage m, ChatConversation c)
        {
            GameObject prefab;
            if (m.kind == MsgKind.Day) prefab = msgDayPrefab;
            else if (m.kind != MsgKind.Normal) prefab = msgSysPrefab;
            else prefab = m.Mine ? msgMinePrefab : msgOtherPrefab;
            var go = Instantiate(prefab, msgContent);
            bool showSender = m.kind == MsgKind.Normal && !m.Mine && c.kind != ChatKind.Direct;
            var view = go.GetComponent<ChatMessageView>();
            view.Bind(m, showSender);
            if (m.kind == MsgKind.Normal && !m.Mine && !sample) view.SetTap(() => OnMessageTapped?.Invoke(c, m));
        }

        private void ScrollDown()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(msgContent);
            Canvas.ForceUpdateCanvases();
            if (msgScrollRect != null) msgScrollRect.verticalNormalizedPosition = 0f;
        }

        private void Send()
        {
            if (current == null || cooldown > 0) return;
            string t = input.text.Trim();
            if (string.IsNullOrEmpty(t)) return;
            if (sample)
            {   // prototype scene: the message stays on this device
                var m = new ChatMessage { from = "me", text = t, time = DateTime.Now.ToString("h:mm") };
                current.messages.Add(m); AddView(m, current); ScrollDown();
            }
            else OnSend?.Invoke(current, t);
            input.text = "";
            if (current.kind == ChatKind.Public) StartCooldown(5);
            input.ActivateInputField();
        }

        private IEnumerator Cool(int seconds)
        {
            cooldown = seconds;
            sendButton.interactable = false; sendIcon.SetActive(false);
            while (cooldown > 0) { cooldownLabel.Text = CoinPill.Format(cooldown); yield return new WaitForSecondsRealtime(1f); cooldown--; }
            cooldownLabel.Text = ""; sendButton.interactable = true; sendIcon.SetActive(true);
        }
    }
}
