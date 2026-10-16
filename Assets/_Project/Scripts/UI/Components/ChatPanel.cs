using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace MafiaNight.UI.Components
{
    /// <summary>
    /// Chat channel types
    /// </summary>
    public enum ChatChannel
    {
        Friends,    // أصدقاء
        Room,       // الغرفة الحالية
        Alliance    // التحالف (مستقبلاً)
    }

    /// <summary>
    /// Slide-in chat panel from the right side with multi-tab support
    /// Shows only arrow button when closed, full chat when open
    /// </summary>
    public class ChatPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform chatPanelRect;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Image toggleButtonImage;
        [SerializeField] private TMP_InputField messageInputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private ScrollRect scrollRect;
        
        [Header("Tab System")]
        [SerializeField] private Button friendsTabButton;
        [SerializeField] private Button roomTabButton;
        [SerializeField] private Button allianceTabButton;
        [SerializeField] private Color activeTabColor = new Color(0.2f, 0.6f, 1f); // Blue
        [SerializeField] private Color inactiveTabColor = new Color(0.3f, 0.3f, 0.3f); // Gray
        
        [Header("Message Containers")]
        [SerializeField] private Transform friendsMessageContainer;
        [SerializeField] private Transform roomMessageContainer;
        [SerializeField] private Transform allianceMessageContainer;
        [SerializeField] private GameObject messageTextPrefab;
        
        [Header("Animation Settings")]
        [SerializeField] private float animationDuration = 0.3f;
        [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Arrow Sprites")]
        [SerializeField] private Sprite arrowOpenSprite; // ← Left arrow (to open)
        [SerializeField] private Sprite arrowCloseSprite; // → Right arrow (to close)
        
        private bool isOpen = false;
        private float panelWidth;
        private Vector2 closedPosition;
        private Vector2 openPosition;
        private Coroutine slideCoroutine;
        
        // Current active channel
        private ChatChannel currentChannel = ChatChannel.Room;
        
        // Message containers dictionary
        private Dictionary<ChatChannel, Transform> messageContainers;

        private void Awake()
        {
            // Initialize message containers dictionary FIRST
            messageContainers = new Dictionary<ChatChannel, Transform>
            {
                { ChatChannel.Friends, friendsMessageContainer },
                { ChatChannel.Room, roomMessageContainer },
                { ChatChannel.Alliance, allianceMessageContainer }
            };
            
            Debug.Log("[ChatPanel] Awake - Dictionary initialized");
        }

        private void Start()
        {
            // Get panel width
            panelWidth = chatPanelRect.rect.width;
            
            // Calculate positions
            // Closed: panel is off-screen (only button visible)
            closedPosition = new Vector2(panelWidth, 0);
            
            // Open: panel is on-screen
            openPosition = Vector2.zero;
            
            // Start closed
            chatPanelRect.anchoredPosition = closedPosition;
            isOpen = false;
            UpdateArrowSprite();
            
            // Setup toggle button
            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(ToggleChat);
            }
            
            // Setup send button
            if (sendButton != null)
            {
                sendButton.onClick.AddListener(() => OnSendMessage(messageInputField.text));
            }
            
            // Setup input field
            if (messageInputField != null)
            {
                messageInputField.onSubmit.AddListener(OnSendMessage);
            }
            
            // Setup tab buttons
            if (friendsTabButton != null)
            {
                friendsTabButton.onClick.AddListener(() => SwitchChannel(ChatChannel.Friends));
            }
            
            if (roomTabButton != null)
            {
                roomTabButton.onClick.AddListener(() => SwitchChannel(ChatChannel.Room));
            }
            
            if (allianceTabButton != null)
            {
                allianceTabButton.onClick.AddListener(() => SwitchChannel(ChatChannel.Alliance));
            }
            
            // Set default channel (Room)
            SwitchChannel(ChatChannel.Room);
            
            Debug.Log("[ChatPanel] Initialized with multi-tab support");
        }

        private void OnDestroy()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.RemoveListener(ToggleChat);
            }
            
            if (sendButton != null)
            {
                sendButton.onClick.RemoveAllListeners();
            }
            
            if (messageInputField != null)
            {
                messageInputField.onSubmit.RemoveListener(OnSendMessage);
            }
            
            if (friendsTabButton != null)
            {
                friendsTabButton.onClick.RemoveAllListeners();
            }
            
            if (roomTabButton != null)
            {
                roomTabButton.onClick.RemoveAllListeners();
            }
            
            if (allianceTabButton != null)
            {
                allianceTabButton.onClick.RemoveAllListeners();
            }
        }

        #region Tab System

        /// <summary>
        /// Switch to a different chat channel
        /// </summary>
        public void SwitchChannel(ChatChannel channel)
        {
            currentChannel = channel;
            
            // Hide all message containers
            foreach (var container in messageContainers.Values)
            {
                if (container != null)
                {
                    container.gameObject.SetActive(false);
                }
            }
            
            // Show current channel's container
            if (messageContainers.ContainsKey(channel) && messageContainers[channel] != null)
            {
                messageContainers[channel].gameObject.SetActive(true);
            }
            
            // Update tab button colors
            UpdateTabColors();
            
            Debug.Log($"[ChatPanel] Switched to {channel} channel");
        }
        
        /// <summary>
        /// Update tab button colors based on active channel
        /// </summary>
        private void UpdateTabColors()
        {
            // Friends tab
            if (friendsTabButton != null)
            {
                var colors = friendsTabButton.colors;
                colors.normalColor = (currentChannel == ChatChannel.Friends) ? activeTabColor : inactiveTabColor;
                friendsTabButton.colors = colors;
            }
            
            // Room tab
            if (roomTabButton != null)
            {
                var colors = roomTabButton.colors;
                colors.normalColor = (currentChannel == ChatChannel.Room) ? activeTabColor : inactiveTabColor;
                roomTabButton.colors = colors;
            }
            
            // Alliance tab
            if (allianceTabButton != null)
            {
                var colors = allianceTabButton.colors;
                colors.normalColor = (currentChannel == ChatChannel.Alliance) ? activeTabColor : inactiveTabColor;
                allianceTabButton.colors = colors;
            }
        }

        #endregion

        #region Toggle

        public void ToggleChat()
        {
            if (isOpen)
            {
                CloseChat();
            }
            else
            {
                OpenChat();
            }
        }

        public void OpenChat()
        {
            if (isOpen) return;
            
            isOpen = true;
            UpdateArrowSprite();
            
            if (slideCoroutine != null)
            {
                StopCoroutine(slideCoroutine);
            }
            
            slideCoroutine = StartCoroutine(SlideTo(openPosition));
        }

        public void CloseChat()
        {
            if (!isOpen) return;
            
            isOpen = false;
            UpdateArrowSprite();
            
            if (slideCoroutine != null)
            {
                StopCoroutine(slideCoroutine);
            }
            
            slideCoroutine = StartCoroutine(SlideTo(closedPosition));
        }

        #endregion

        #region Animation

        private IEnumerator SlideTo(Vector2 targetPosition)
        {
            Vector2 startPosition = chatPanelRect.anchoredPosition;
            float elapsed = 0f;
            
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = slideCurve.Evaluate(elapsed / animationDuration);
                chatPanelRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
                yield return null;
            }
            
            chatPanelRect.anchoredPosition = targetPosition;
        }

        private void UpdateArrowSprite()
        {
            if (toggleButtonImage != null && arrowOpenSprite != null && arrowCloseSprite != null)
            {
                toggleButtonImage.sprite = isOpen ? arrowCloseSprite : arrowOpenSprite;
            }
            // If using text instead of sprite, you can update text here
            // Example: toggleButtonText.text = isOpen ? "►" : "◄";
        }

        #endregion

        #region Chat Messages

        public void OnSendMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }
            
            // Add message to current channel
            AddMessage("You", message, currentChannel);
            
            // Clear input
            messageInputField.text = "";
            messageInputField.ActivateInputField();
            
            // TODO: Send message to server/network based on channel
            Debug.Log($"[ChatPanel] Sent message to {currentChannel}: {message}");
        }

        /// <summary>
        /// Add a message to a specific channel
        /// </summary>
        public void AddMessage(string senderName, string message, ChatChannel channel)
        {
            Debug.Log($"[ChatPanel] AddMessage called: {senderName} -> {message} (Channel: {channel})");
            
            if (!messageContainers.ContainsKey(channel))
            {
                Debug.LogWarning($"[ChatPanel] Channel {channel} not found in dictionary!");
                return;
            }
            
            Transform container = messageContainers[channel];
            if (container == null)
            {
                Debug.LogError($"[ChatPanel] Container for {channel} is NULL!");
                return;
            }
            
            if (messageTextPrefab == null)
            {
                Debug.LogError("[ChatPanel] Message Text Prefab is NULL! Please assign it in Inspector.");
                return;
            }
            
            Debug.Log($"[ChatPanel] Creating message in container: {container.name}");
            
            // Create message text
            GameObject messageObj = Instantiate(messageTextPrefab, container);
            messageObj.name = $"Message_{senderName}_{System.DateTime.Now.Ticks}";
            
            TextMeshProUGUI messageText = messageObj.GetComponent<TextMeshProUGUI>();
            
            if (messageText != null)
            {
                // Color code by channel
                string channelTag = GetChannelTag(channel);
                messageText.text = $"{channelTag}<b>{senderName}:</b> {message}";
                Debug.Log($"[ChatPanel] Message created successfully: {messageText.text}");
            }
            else
            {
                Debug.LogError("[ChatPanel] MessageText prefab doesn't have TextMeshProUGUI component!");
            }
            
            // Scroll to bottom if on active channel
            if (channel == currentChannel)
            {
                StartCoroutine(ScrollToBottom());
            }
            
            // Debug visibility
            DebugMessageVisibility(container);
        }
        
        /// <summary>
        /// Debug helper to check message visibility
        /// </summary>
        private void DebugMessageVisibility(Transform container)
        {
            Debug.Log($"[ChatPanel] === Container Debug: {container.name} ===");
            Debug.Log($"Active: {container.gameObject.activeSelf}");
            Debug.Log($"Children Count: {container.childCount}");
            
            RectTransform rt = container.GetComponent<RectTransform>();
            if (rt != null)
            {
                Debug.Log($"Size: {rt.rect.width} x {rt.rect.height}");
                Debug.Log($"Position: {rt.anchoredPosition}");
            }
            
            VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                Debug.Log($"Layout: Enabled={layout.enabled}, Padding={layout.padding.top}");
            }
            else
            {
                Debug.LogWarning("NO VERTICAL LAYOUT GROUP!");
            }
            
            if (container.childCount > 0)
            {
                Transform lastChild = container.GetChild(container.childCount - 1);
                Debug.Log($"Last Message: {lastChild.name}");
                
                TextMeshProUGUI text = lastChild.GetComponent<TextMeshProUGUI>();
                if (text != null)
                {
                    Debug.Log($"  Text: '{text.text}'");
                    Debug.Log($"  Color: {text.color}, Alpha: {text.alpha}");
                    Debug.Log($"  Font Size: {text.fontSize}");
                    Debug.Log($"  Enabled: {text.enabled}");
                }
            }
        }
        
        /// <summary>
        /// Add message to current active channel
        /// </summary>
        public void AddMessage(string senderName, string message)
        {
            AddMessage(senderName, message, currentChannel);
        }
        
        /// <summary>
        /// Get color tag for channel
        /// </summary>
        private string GetChannelTag(ChatChannel channel)
        {
            switch (channel)
            {
                case ChatChannel.Friends:
                    return "<color=#FFD700>"; // Gold for friends
                case ChatChannel.Room:
                    return "<color=#00BFFF>"; // Blue for room
                case ChatChannel.Alliance:
                    return "<color=#32CD32>"; // Green for alliance
                default:
                    return "";
            }
        }

        private IEnumerator ScrollToBottom()
        {
            yield return null; // Wait one frame for layout
            
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        #endregion
    }
}
