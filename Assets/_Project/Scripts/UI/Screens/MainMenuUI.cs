using System.Threading.Tasks;
using MafiaNight.Core;
using MafiaNight.Data;
using MafiaNight.Data.Player;
using MafiaNight.Services.Economy;
using MafiaNight.Services.Profile;
using MafiaNight.Services.Scenes;
using MafiaNight.Services.Session;
using MafiaNight.UI.Animations;
using MafiaNight.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Screens
{
    /// <summary>
    /// Main menu UI controller - displays player profile and handles room selection
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        private const string Tag = "MainMenuUI";

        [Header("Profile Display")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private TextMeshProUGUI gemsText;
        [SerializeField] private Button settingsButton;

        [Header("Player Profile Display")]
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private Image rankBadgeImage;
        [SerializeField] private Scrollbar xpProgressBar;
        [SerializeField] private TextMeshProUGUI xpText;

        [Header("Room Cards (children of the carousel)")]
        [SerializeField] private Button starterRoomButton;
        [SerializeField] private Button proRoomButton;
        [SerializeField] private Button eliteRoomButton;

        [Header("Room Carousel")]
        [SerializeField] private RoomCarousel roomCarousel;
        [SerializeField] private Button playButton;
        [SerializeField] private TextMeshProUGUI selectedRoomText;

        [Header("Room Tier Data")]
        [SerializeField] private RoomTierData starterRoomTier;
        [SerializeField] private RoomTierData proRoomTier;
        [SerializeField] private RoomTierData eliteRoomTier;

        [Header("Layout Elements")]
        [SerializeField] private GameObject headerPanel;
        [SerializeField] private GameObject downBar;

        private ICurrencyService _currency;
        private IPlayerProfileService _profile;
        private RoomTierData[] _roomTiers = new RoomTierData[3];
        private bool _isJoiningRoom;

        private async void Start()
        {
            ServiceLocator.TryGet(out _currency);
            ServiceLocator.TryGet(out _profile);

            if (_currency != null)
            {
                _currency.OnCoinsChanged += UpdateCoinsDisplay;
                _currency.OnGemsChanged += UpdateGemsDisplay;
                UpdateCoinsDisplay(_currency.Coins);
                UpdateGemsDisplay(_currency.Gems);
            }

            if (_profile != null)
            {
                _profile.OnProfileLoaded += DisplayProfile;
                _profile.OnProfileUpdated += DisplayProfile;
                if (_profile.HasProfile) DisplayProfile(_profile.LocalProfile);
            }

            if (_profile == null || !_profile.HasProfile)
            {
                ShowPlaceholderName();
            }

            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);

            _roomTiers = new[] { starterRoomTier, proRoomTier, eliteRoomTier };
            SetupRoomButton(starterRoomButton, 0);
            SetupRoomButton(proRoomButton, 1);
            SetupRoomButton(eliteRoomButton, 2);

            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (roomCarousel != null)
            {
                roomCarousel.OnIndexChanged += UpdateSelectedRoomText;
                UpdateSelectedRoomText(roomCarousel.CurrentIndex);
            }

            Log.Info("Initialized", Tag);

            await AnimateUIElements();
        }

        private void OnDestroy()
        {
            if (_currency != null)
            {
                _currency.OnCoinsChanged -= UpdateCoinsDisplay;
                _currency.OnGemsChanged -= UpdateGemsDisplay;
            }

            if (_profile != null)
            {
                _profile.OnProfileLoaded -= DisplayProfile;
                _profile.OnProfileUpdated -= DisplayProfile;
            }

            if (roomCarousel != null) roomCarousel.OnIndexChanged -= UpdateSelectedRoomText;
            if (playButton != null) playButton.onClick.RemoveListener(OnPlayClicked);
            if (settingsButton != null) settingsButton.onClick.RemoveAllListeners();
            if (starterRoomButton != null) starterRoomButton.onClick.RemoveAllListeners();
            if (proRoomButton != null) proRoomButton.onClick.RemoveAllListeners();
            if (eliteRoomButton != null) eliteRoomButton.onClick.RemoveAllListeners();
        }

        private void SetupRoomButton(Button button, int index)
        {
            if (button == null || _roomTiers[index] == null) return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnRoomCardClicked(index));
        }

        /// <summary>Side card → bring it to the center. Center card → join.</summary>
        private void OnRoomCardClicked(int index)
        {
            if (roomCarousel != null && roomCarousel.CurrentIndex != index)
            {
                roomCarousel.GoTo(index);
                return;
            }
            OnRoomButtonClicked(_roomTiers[index]);
        }

        private void OnPlayClicked()
        {
            int index = roomCarousel != null ? roomCarousel.CurrentIndex : 0;
            if (_roomTiers[index] != null) OnRoomButtonClicked(_roomTiers[index]);
        }

        private void UpdateSelectedRoomText(int index)
        {
            if (selectedRoomText == null || index < 0 || index >= _roomTiers.Length || _roomTiers[index] == null) return;
            var tier = _roomTiers[index];
            selectedRoomText.text = $"{tier.TierName} • {tier.MaxPlayers} Players";
        }

        #region Display Updates

        private void UpdateCoinsDisplay(int amount)
        {
            if (coinsText != null) coinsText.text = amount.ToString();
        }

        private void UpdateGemsDisplay(int amount)
        {
            if (gemsText != null) gemsText.text = amount.ToString();
        }

        private void ShowPlaceholderName()
        {
            if (playerNameText != null) playerNameText.SetText("Guest");
        }

        private void DisplayProfile(PlayerProfile profile)
        {
            if (profile == null) return;

            if (playerNameText != null && !string.IsNullOrEmpty(profile.DisplayName))
                playerNameText.text = profile.DisplayName;

            if (levelText != null)
                levelText.text = $"Level {profile.Level}";

            if (rankText != null)
                rankText.text = profile.Rank.ToString();

            if (rankBadgeImage != null)
                rankBadgeImage.color = RankRules.GetRankColor(profile.Rank);

            if (xpProgressBar != null)
                xpProgressBar.value = profile.GetLevelProgress();

            if (xpText != null)
                xpText.text = profile.GetLevelProgressText();
        }

        #endregion

        #region Button Handlers

        private void OnRoomButtonClicked(RoomTierData roomTier)
        {
            if (_isJoiningRoom) return;

            if (_currency == null || !_currency.HasEnoughCoins(roomTier.EntryFee))
            {
                Log.Warning($"Not enough coins! Need {roomTier.EntryFee}, have {_currency?.Coins}", Tag);
                // TODO: Show "Not enough coins" popup
                return;
            }

            Log.Info($"{roomTier.TierName} room - Fee: {roomTier.EntryFee}, Players: {roomTier.MaxPlayers}", Tag);
            _isJoiningRoom = true;

            _currency.DeductCoins(roomTier.EntryFee,
                onSuccess: () =>
                {
                    if (ServiceLocator.TryGet<RoomSession>(out var session))
                        session.Join(roomTier, roomTier.EntryFee);

                    ServiceLocator.Get<ISceneLoader>().LoadScene(GameConstants.SCENE_LOBBY);
                },
                onError: error =>
                {
                    _isJoiningRoom = false;
                    Log.Error($"Failed to deduct coins: {error}", Tag);
                });
        }

        private void OnSettingsClicked()
        {
            // TODO: Open settings panel (audio, language, account, logout)
            Log.Info("Settings button clicked", Tag);
        }

        #endregion

        #region UI Animations

        /// <summary>Sequential reveal of the main menu elements.</summary>
        private async Task AnimateUIElements()
        {
            try
            {
                if (headerPanel != null)
                {
                    await UIElementAnimator.ScaleAndFadeIn(headerPanel, AnimationPresets.NormalDuration);
                }

                await Task.Delay(100);

                // The carousel scales its own cards, so animate the carousel as a whole
                if (roomCarousel != null)
                {
                    await UIElementAnimator.ScaleAndFadeIn(roomCarousel.gameObject, AnimationPresets.NormalDuration);
                }

                if (downBar != null && downBar.TryGetComponent<RectTransform>(out var rectTransform))
                {
                    await UIElementAnimator.SlideIn(rectTransform, UIElementAnimator.SlideDirection.Up,
                        AnimationPresets.NormalDuration, distance: 200f);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"Animation error (non-critical): {ex.Message}", Tag);
            }
        }

        #endregion
    }
}
