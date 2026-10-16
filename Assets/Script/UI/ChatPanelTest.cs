using UnityEngine;

namespace MafiaNight.UI
{
    /// <summary>
    /// Simple test script to open chat panel on start
    /// Attach to any GameObject in the scene
    /// </summary>
    public class ChatPanelTest : MonoBehaviour
    {
        private void Start()
        {
            // Wait a frame to ensure ChatPanel is initialized
            Invoke(nameof(TestChat), 0.1f);
        }

        private void TestChat()
        {
            // Find ChatPanel
            ChatPanel chatPanel = FindObjectOfType<ChatPanel>();
            
            if (chatPanel != null)
            {
                Debug.Log("[ChatPanelTest] ChatPanel found!");
                
                // Open chat automatically
                chatPanel.OpenChat();
                
                Debug.Log("[ChatPanelTest] Chat opened!");
                
                // Add test messages
                chatPanel.AddMessage("System", "Welcome to the chat!", ChatChannel.Room);
                chatPanel.AddMessage("Friend1", "Hey there!", ChatChannel.Friends);
                chatPanel.AddMessage("Leader", "Rally up!", ChatChannel.Alliance);
            }
            else
            {
                Debug.LogError("[ChatPanelTest] ChatPanel not found!");
            }
        }
    }
}
