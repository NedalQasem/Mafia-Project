using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The "how do you want to come in" screen of Docs/mockups/login.html: Google, Facebook, Apple, "enter as a guest" and the link to the office account.
    /// A method that is not ready yet shows its "soon" tag and cannot be pressed. Create it from the prefab; the app answers the events.
    /// </summary>
    public sealed class KitLoginChoice : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Button googleButton, facebookButton, appleButton, guestButton, officeButton;
        [SerializeField] private GameObject googleSoon, facebookSoon, appleSoon;
        [SerializeField] private ArabicText messageText;
        [SerializeField] private KitSegmented language;      // the two small optional pills: العربية / English
        [SerializeField] private RectTransform languageBox;

        public event Action OnGoogle, OnFacebook, OnGuest, OnOffice;
        /// <summary>The player picked a language with the little pills: "ar" or "en". The pref is already saved by the caller.</summary>
        public event Action<string> OnLanguage;

        private bool closing;

        private void Awake()
        {
            if (googleButton == null) return;      // the prefab builder adds this component before its fields are set
            KitFitGroup.Wrap((RectTransform)transform, 2400f, content);        // the parts are placed for the tall phone; a shorter screen shrinks them together
            googleButton.onClick.AddListener(() => OnGoogle?.Invoke());
            facebookButton.onClick.AddListener(() => OnFacebook?.Invoke());
            guestButton.onClick.AddListener(() => OnGuest?.Invoke());
            officeButton.onClick.AddListener(() => OnOffice?.Invoke());
            appleButton.interactable = false; appleSoon.SetActive(true);
            if (language != null)
            {
                language.Set(MafiaNight.Core.Loc.English ? 1 : 0);
                language.OnChanged += i => OnLanguage?.Invoke(i == 1 ? "en" : "ar");
                // the pills stay at the top right in both languages: in English the screen is drawn in a mirror, so they are built on the left
                if (MafiaNight.Core.Loc.English && languageBox != null)
                {
                    languageBox.anchorMin = languageBox.anchorMax = new Vector2(0f, 1f); languageBox.pivot = new Vector2(0f, 1f);
                    languageBox.anchoredPosition = new Vector2(-languageBox.anchoredPosition.x, languageBox.anchoredPosition.y);
                }
            }
            group.alpha = 0f;
        }

        private void OnEnable() { if (group != null) StartCoroutine(Fade(0f, 1f, .45f, false)); }

        /// <summary>A line under the buttons (an error from a sign-in); empty hides it.</summary>
        public void SetMessage(string text) { if (messageText != null) messageText.Text = text ?? ""; }

        /// <summary>Which of Google and Facebook can be used now (the others show "soon").</summary>
        public void SetAvailable(bool google, bool facebook)
        {
            googleButton.interactable = google; googleSoon.SetActive(!google);
            facebookButton.interactable = facebook; facebookSoon.SetActive(!facebook);
        }

        public void Close() { if (closing) return; closing = true; StartCoroutine(Fade(1f, 0f, .3f, true)); }

        private IEnumerator Fade(float from, float to, float seconds, bool destroy)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) { group.alpha = Mathf.Lerp(from, to, t / seconds); yield return null; }
            group.alpha = to;
            if (destroy) Destroy(gameObject);
        }
    }
}
