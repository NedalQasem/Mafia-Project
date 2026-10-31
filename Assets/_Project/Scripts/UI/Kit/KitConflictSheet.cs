using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// "This Google already has an account" (Docs/mockups/login.html, scene 4): asks the player whether to open the old account instead of the one he is in.
    /// After he agrees and the old account is tied to this device, <see cref="ShowDone"/> tells him to close the game and open it again.
    /// </summary>
    public sealed class KitConflictSheet : MonoBehaviour
    {
        [SerializeField] private RectTransform sheet;
        [SerializeField] private CanvasGroup dim;
        [SerializeField] private ArabicText titleText, nameText, warnText, messageText, openLabel;
        [SerializeField] private Button keepButton, openButton, dimButton;

        /// <summary>"No, stay with my account".</summary>
        public event Action OnKeep;
        /// <summary>"Open my old account".</summary>
        public event Action OnOpenOld;
        /// <summary>After <see cref="ShowDone"/>: the button closes the game.</summary>
        public event Action OnQuit;

        private bool done, busy;

        private void Awake()
        {
            if (keepButton == null) return;      // the prefab builder adds this component before its fields are set
            keepButton.onClick.AddListener(() => { if (!busy && !done) Close(); });
            dimButton.onClick.AddListener(() => { if (!busy && !done) Close(); });
            openButton.onClick.AddListener(() => { if (busy) return; if (done) OnQuit?.Invoke(); else OnOpenOld?.Invoke(); });
            dim.alpha = 0f;
        }

        public void Show(string accountName, string providerName)
        {
            titleText.Text = "هالـ" + providerName + " إله حساب قديم";
            nameText.Text = string.IsNullOrEmpty(accountName) ? "حساب لاعب" : accountName;
            messageText.Text = "";
            StartCoroutine(Fade(0f, 1f, .25f, false));
        }

        public void SetBusy(bool value) { busy = value; openButton.interactable = !value; keepButton.interactable = !value; if (value) messageText.Text = "لحظة…"; }

        public void ShowMessage(string text) { SetBusy(false); messageText.Text = text; }

        public void ShowDone(string text)
        {
            done = true; SetBusy(false);
            messageText.Text = text; warnText.gameObject.SetActive(false);
            openLabel.Text = "سكّر اللعبة"; keepButton.gameObject.SetActive(false);
        }

        public void Close() { StartCoroutine(Fade(1f, 0f, .2f, true)); }

        private IEnumerator Fade(float from, float to, float seconds, bool destroy)
        {
            var home = sheet.anchoredPosition;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Lerp(from, to, t / seconds);
                dim.alpha = k; sheet.anchoredPosition = home + new Vector2(0f, (1f - k) * -220f);
                yield return null;
            }
            dim.alpha = to; sheet.anchoredPosition = home;
            if (destroy) { OnKeep?.Invoke(); Destroy(gameObject); }
        }
    }
}
