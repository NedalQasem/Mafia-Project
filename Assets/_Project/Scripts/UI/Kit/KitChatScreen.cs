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
    /// </summary>
    public class KitChatScreen : MonoBehaviour
    {
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

        private ChatConversation pub; private List<ChatConversation> dms, groups;
        private ChatConversation current;
        private int tab;
        private int cooldown;
        private Coroutine coolRoutine;

        private void Start() { if (Application.isPlaying) Init(); }

        public void Init()
        {
            pub = ChatData.Public(); dms = ChatData.Direct(); groups = ChatData.Groups();
            for (int i = 0; i < tabs.Length; i++) { int idx = i; tabs[i].onClick.AddListener(() => SelectTab(idx)); }
            backButton.onClick.AddListener(() => { current = null; Refresh(); });
            sendButton.onClick.AddListener(Send);
            input.onSubmit.AddListener(_ => Send());
            SelectTab(0);
        }

        public void SelectTab(int i) { tab = i; current = null; Refresh(); }

        private void Refresh()
        {
            var th = KitTheme.Current;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool on = i == tab;
                tabs[i].GetComponent<Image>().sprite = on ? th.brassPlate : th.leatherDark;
                tabs[i].GetComponentInChildren<TMP_Text>().color = on ? th.brassText : th.ivoryDim;
                int u = i == 1 ? Sum(dms) : i == 2 ? Sum(groups) : 0;
                tabBadges[i].SetActive(u > 0); if (u > 0) tabBadgeLabels[i].Text = CoinPill.Format(u);
            }
            if (tab == 0) { OpenConversation(pub); return; }
            // list view
            current = null;
            listScroll.SetActive(true); msgScroll.SetActive(false); composer.SetActive(false);
            tabsRow.SetActive(true); convHeader.SetActive(false); publicNotice.SetActive(false);
            Clear(listContent);
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
            current = c; c.unread = 0;
            bool isPublic = c.kind == ChatKind.Public;
            listScroll.SetActive(false); msgScroll.SetActive(true); composer.SetActive(true);
            tabsRow.SetActive(isPublic); convHeader.SetActive(!isPublic); publicNotice.SetActive(isPublic);
            if (!isPublic)
            {
                convTitle.Text = c.name;
                convSub.Text = c.kind == ChatKind.Group ? CoinPill.Format(c.members.Length + 1) + " أعضاء" : (c.online ? "متصل الآن" : "غير متصل");
            }
            Clear(msgContent);
            foreach (var m in c.messages) AddView(m, c);
            ScrollDown();
        }

        private void AddView(ChatMessage m, ChatConversation c)
        {
            GameObject prefab;
            if (m.kind == MsgKind.Day) prefab = msgDayPrefab;
            else if (m.kind != MsgKind.Normal) prefab = msgSysPrefab;
            else prefab = m.Mine ? msgMinePrefab : msgOtherPrefab;
            var go = Instantiate(prefab, msgContent);
            bool showSender = m.kind == MsgKind.Normal && !m.Mine && c.kind != ChatKind.Direct;
            go.GetComponent<ChatMessageView>().Bind(m, showSender);
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
            var m = new ChatMessage { from = "me", text = t, time = System.DateTime.Now.ToString("h:mm") };
            current.messages.Add(m); AddView(m, current); input.text = ""; ScrollDown();
            if (current.kind == ChatKind.Public) { if (coolRoutine != null) StopCoroutine(coolRoutine); coolRoutine = StartCoroutine(Cool(5)); }
            StartCoroutine(AutoReply(current));
            input.ActivateInputField();
        }

        private IEnumerator Cool(int seconds)
        {
            cooldown = seconds;
            sendButton.interactable = false; sendIcon.SetActive(false);
            while (cooldown > 0) { cooldownLabel.Text = CoinPill.Format(cooldown); yield return new WaitForSecondsRealtime(1f); cooldown--; }
            cooldownLabel.Text = ""; sendButton.interactable = true; sendIcon.SetActive(true);
        }

        private IEnumerator AutoReply(ChatConversation c)
        {
            string who = c.kind == ChatKind.Direct ? c.name : c.kind == ChatKind.Group ? c.members[0] : "أم سعيد";
            yield return new WaitForSecondsRealtime(0.6f);
            if (c.kind == ChatKind.Direct && current == c) convSub.Text = "يكتب الآن...";
            yield return new WaitForSecondsRealtime(1.6f);
            string[] lines = { "تمام", "أكيد", "كلامك صح", "جاي حالاً", "ممتاز، شكراً" };
            var r = new ChatMessage { from = who, text = lines[Random.Range(0, lines.Length)], time = System.DateTime.Now.ToString("h:mm"), rank = 3 };
            c.messages.Add(r);
            if (current == c) { AddView(r, c); ScrollDown(); if (c.kind == ChatKind.Direct) convSub.Text = c.online ? "متصل الآن" : "غير متصل"; }
        }
    }
}
