using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Bottom sheet of the settings screen where a player on a new device types the user name and password the support gave him, to get his old
    /// account back. After a success the only way on is to close the game (the account is loaded again from the start).
    /// </summary>
    public class KitRecoverySheet : MonoBehaviour
    {
        [SerializeField] private CanvasGroup dim;
        [SerializeField] private RectTransform sheet;
        [SerializeField] private TMP_InputField usernameInput, passwordInput;
        [SerializeField] private ArabicText message, submitLabel;
        [SerializeField] private Button submitButton, closeButton;
        [Tooltip("The settings row that opens this sheet.")]
        [SerializeField] private Button opener;

        private static readonly Color ErrorColor = new Color32(0xE6, 0x7A, 0x80, 255), DoneColor = new Color32(0x9F, 0xD0, 0x8C, 255);

        /// <summary>(user name, password) when the player presses the button.</summary>
        public event Action<string, string> OnSubmit;
        /// <summary>The account came back and the player pressed "close the game".</summary>
        public event Action OnQuit;

        public Button Opener => opener;

        private Coroutine anim;
        private bool busy, done, wired;

        private void Awake() => Wire();

        private void Wire()
        {
            if (wired) return;
            wired = true;
            submitButton.onClick.AddListener(Submit);
            closeButton.onClick.AddListener(Close);
            var d = dim != null ? dim.GetComponent<Button>() : null; if (d != null) d.onClick.AddListener(Close);
            foreach (var f in new[] { usernameInput, passwordInput })
            {
                f.selectionColor = new Color(0.72f, 0.56f, 0.28f, 0.35f);
                f.onValueChanged.AddListener(_ => { if (!done) message.Text = ""; });
            }
            usernameInput.onSubmit.AddListener(_ => passwordInput.ActivateInputField());
            passwordInput.onSubmit.AddListener(_ => Submit());
            dim.alpha = 0f; dim.blocksRaycasts = false;
            sheet.anchoredPosition = new Vector2(0f, -(sheet.rect.height + 80f));
        }

        public void Open()
        {
            gameObject.SetActive(true);
            Wire();
            transform.SetAsLastSibling();
            if (!done)
            {
                usernameInput.SetTextWithoutNotify(""); passwordInput.SetTextWithoutNotify("");
                message.Text = ""; SetBusy(false);
            }
            Play(true);
            if (!done) usernameInput.ActivateInputField();
        }

        public void Close()
        {
            if (busy) return;
            usernameInput.DeactivateInputField(); passwordInput.DeactivateInputField();
            Play(false);
        }

        public void ShowMessage(string text) { message.GetComponent<TMP_Text>().color = ErrorColor; message.Text = text; SetBusy(false); }

        /// <summary>The account is back: the fields lock and the button closes the game.</summary>
        public void ShowDone(string text)
        {
            done = true; SetBusy(false);
            usernameInput.interactable = false; passwordInput.interactable = false;
            message.GetComponent<TMP_Text>().color = DoneColor; message.Text = text;
            submitLabel.Text = "سكّر اللعبة";
        }

        public void SetBusy(bool value)
        {
            busy = value; submitButton.interactable = !value;
            if (!done) submitLabel.Text = value ? "لحظة..." : "استرجع";
        }

        private void Submit()
        {
            if (busy) return;
            if (done) { OnQuit?.Invoke(); return; }
            string user = usernameInput.text.Trim(), pass = passwordInput.text;
            if (user.Length < 4) { ShowMessage("اكتب اسم المستخدم."); return; }
            if (pass.Length < 8) { ShowMessage("كلمة السر ٨ خانات أو أكثر."); return; }
            OnSubmit?.Invoke(user, pass);
        }

        private void Play(bool show) { if (anim != null) StopCoroutine(anim); anim = StartCoroutine(Slide(show)); }

        private IEnumerator Slide(bool show)
        {
            dim.blocksRaycasts = show;
            float from = sheet.anchoredPosition.y, to = show ? 0f : -(sheet.rect.height + 80f);
            float a0 = dim.alpha, a1 = show ? 1f : 0f;
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.4f), 3f);
                sheet.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, to, k));
                dim.alpha = Mathf.Lerp(a0, a1, k);
                yield return null;
            }
            sheet.anchoredPosition = new Vector2(0f, to); dim.alpha = a1;
            if (!show) gameObject.SetActive(false);
        }
    }
}
