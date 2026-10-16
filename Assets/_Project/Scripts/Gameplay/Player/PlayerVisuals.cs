using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MafiaNight.Data.Customization;
using MafiaNight.Data.Player;

namespace MafiaNight.Gameplay.Player
{
    /// <summary>
    /// Handles visual representation of the player (Avatar, Frame, Nameplate)
    /// Used by PlayerController to update visuals based on profile data
    /// </summary>
    public class PlayerVisuals : MonoBehaviour
    {
        [Header("UI Elements (World Space or Screen Space)")]
        [SerializeField] private SpriteRenderer avatarRenderer; // For 2D world
        [SerializeField] private SpriteRenderer frameRenderer;  // For 2D world
        [SerializeField] private TextMeshPro nameText;          // World space text
        
        [Header("Canvas Elements (Optional - if using Canvas based player)")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private TextMeshProUGUI uiNameText;

        [Header("Settings")]
        [SerializeField] private Color vipColor = new Color(1f, 0.84f, 0f);
        [SerializeField] private Color normalColor = Color.white;

        // Reference to database to load assets
        private CustomizationDatabase customizationDB;

        private void Awake()
        {
            // Find database if not assigned (or Inject it)
            // customizationDB = Resources.Load<CustomizationDatabase>("CustomizationDatabase");
        }

        public void SetVisuals(PlayerPublicData data)
        {
            if (data == null) return;

            UpdateName(data.DisplayName, data.IsVIP);
            UpdateAvatar(data.ProfilePictureID);
            UpdateFrame(data.ProfileFrameID);
        }

        private void Update()
        {
            // BILLBOARD EFFECT: Simple Parallel Mode
            // This forces the text to align perfectly with the camera's plane.
            if (Camera.main != null)
            {
                if (nameText != null)
                {
                    nameText.transform.rotation = Camera.main.transform.rotation;
                }
                
                if (uiNameText != null && uiNameText.transform.parent.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace)
                {
                    uiNameText.transform.rotation = Camera.main.transform.rotation;
                }
            }
        }

        private void UpdateName(string name, bool isVip)
        {
            Color color = isVip ? vipColor : normalColor;

            Debug.Log($"[PlayerVisuals] Updating Name to: '{name}' on {gameObject.name}");

            if (nameText != null)
            {
                // Clean assignment only. We rely on Inspector for Size/Width.
                nameText.text = name;
                nameText.color = color; 
            }

            if (uiNameText != null)
            {
                uiNameText.text = name;
                uiNameText.color = color;
            }
            else
            {
                // Only warn if nameText is ALSO missing, otherwise we might just be using one mode
                if(nameText == null) Debug.LogWarning($"[PlayerVisuals] No Name Text (World or UI) assigned on {gameObject.name}!");
            }
        }

        private void UpdateAvatar(string avatarId)
        {
            // TODO: Load real avatar from ID
            // Sprite avatarSprite = customizationDB.GetAvatar(avatarId);
            
            // if (avatarRenderer != null) avatarRenderer.sprite = avatarSprite;
            // if (avatarImage != null) avatarImage.sprite = avatarSprite;
        }

        private void UpdateFrame(string frameId)
        {
            // TODO: Load real frame from ID
            // Sprite frameSprite = customizationDB.GetFrame(frameId).FrameSprite;

            // if (frameRenderer != null) frameRenderer.sprite = frameSprite;
            // if (frameImage != null) frameImage.sprite = frameSprite;
        }

        public void SetTransparency(float alpha)
        {
            // Helper to fade player (e.g. ghost mode)
            if (avatarRenderer != null) 
            {
                Color c = avatarRenderer.color;
                avatarRenderer.color = new Color(c.r, c.g, c.b, alpha);
            }
        }
    }
}
