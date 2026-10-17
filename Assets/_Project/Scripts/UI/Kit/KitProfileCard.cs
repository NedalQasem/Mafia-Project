using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The player's ID card, hanging from a red strap. Drops in from the top with a bounce, then sways gently.
    /// Close by tapping the dark area or the close button.
    /// </summary>
    public class KitProfileCard : MonoBehaviour
    {
        [SerializeField] private RectTransform hang;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Button dimButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private AvatarFrame avatar;
        [SerializeField] private ArabicText nameLabel, titleLabel, levelLabel, xpText, memberLabel, cardNumber;
        [SerializeField] private RectTransform xpFill;
        [SerializeField] private ArabicText[] statNumbers = new ArabicText[3];
        [SerializeField] private GameObject front, back;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Button flipButton;
        [SerializeField] private ArabicText[] roleValues = new ArabicText[4];   // citizen, mafia, detective, doctor

        public bool IsOpen { get; private set; }
        private Coroutine anim;
        private float hiddenY;

        private void Awake()
        {
            if (dimButton != null) dimButton.onClick.AddListener(Hide);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (flipButton != null) flipButton.onClick.AddListener(Flip);
            hiddenY = hang.rect.height + 260f;
            if (!IsOpen) gameObject.SetActive(false);
        }

        public void SetPlayer(string playerName, string title, int level, int rankIndex, float xp01, int xp, int xpMax, int games, int wins)
        {
            if (nameLabel != null) nameLabel.Text = playerName;
            if (titleLabel != null) titleLabel.Text = title;
            if (levelLabel != null) levelLabel.Text = "م" + CoinPill.Format(level);
            if (avatar != null) avatar.Set(rankIndex);
            if (xpText != null) xpText.Text = CoinPill.Format(xp) + "/" + CoinPill.Format(xpMax);
            if (xpFill != null)
            {
                xp01 = Mathf.Clamp01(xp01);
                xpFill.anchorMin = new Vector2(1f - xp01, 0f); xpFill.anchorMax = new Vector2(1f, 1f);
                xpFill.offsetMin = xpFill.offsetMax = Vector2.zero;
            }
            if (statNumbers.Length >= 3)
            {
                if (statNumbers[0] != null) statNumbers[0].Text = CoinPill.Format(games);
                if (statNumbers[1] != null) statNumbers[1].Text = CoinPill.Format(wins);
                if (statNumbers[2] != null) statNumbers[2].Text = CoinPill.Format(games > 0 ? Mathf.RoundToInt(100f * wins / games) : 0) + "٪";
            }
        }

        public void SetRoles(int citizen, int mafia, int detective, int doctor)
        {
            int[] v = { citizen, mafia, detective, doctor };
            for (int i = 0; i < roleValues.Length && i < 4; i++) if (roleValues[i] != null) roleValues[i].Text = CoinPill.Format(v[i]);
        }

        private bool showingFront = true;
        private Coroutine flipAnim;

        public void Flip()
        {
            if (flipAnim != null || cardRect == null) return;
            flipAnim = StartCoroutine(FlipRoutine());
        }

        private IEnumerator FlipRoutine()
        {
            const float half = 0.16f;
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            { cardRect.localScale = new Vector3(1f - t / half, 1f, 1f); yield return null; }
            showingFront = !showingFront;
            front.SetActive(showingFront); back.SetActive(!showingFront);
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            { cardRect.localScale = new Vector3(t / half, 1f, 1f); yield return null; }
            cardRect.localScale = Vector3.one;
            flipAnim = null;
        }

        public void Show()
        {
            if (IsOpen) return;
            if (front != null) { showingFront = true; front.SetActive(true); back.SetActive(false); cardRect.localScale = Vector3.one; flipAnim = null; }
            IsOpen = true;
            gameObject.SetActive(true);
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Drop());
        }

        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Lift());
        }

        private IEnumerator Drop()
        {
            group.blocksRaycasts = true;
            const float dur = 1.1f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float p = 1f - Mathf.Exp(-5.5f * t) * Mathf.Cos(9f * t);   // overshoot then settle
                hang.anchoredPosition = new Vector2(0, Mathf.LerpUnclamped(hiddenY, 0f, p));
                hang.localRotation = Quaternion.Euler(0, 0, 5f * Mathf.Exp(-3.5f * t) * Mathf.Sin(13f * t));
                group.alpha = Mathf.Clamp01(t / 0.25f);
                yield return null;
            }
            hang.anchoredPosition = Vector2.zero; group.alpha = 1f;
            anim = null;
        }

        private IEnumerator Lift()
        {
            const float dur = 0.32f;
            Vector2 from = hang.anchoredPosition;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur; k *= k;
                hang.anchoredPosition = Vector2.Lerp(from, new Vector2(0, hiddenY), k);
                group.alpha = 1f - k;
                yield return null;
            }
            group.alpha = 0f; group.blocksRaycasts = false;
            gameObject.SetActive(false);
            anim = null;
        }

        private void Update()
        {
            if (IsOpen && anim == null)
                hang.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 0.9f) * 1.1f);   // idle sway
        }
    }
}
