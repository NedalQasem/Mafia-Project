using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Settings → الحساب (Docs/mockups/login.html, scene 3): what kind of account this is (a guest with the days left before it is deleted, or a linked one)
    /// and a row for each way to come back: Google, Facebook, Apple and the office account. Create it from the prefab, then <see cref="Show"/>.
    /// </summary>
    public sealed class KitAccountSheet : MonoBehaviour
    {
        [Serializable]
        public sealed class Row
        {
            public string key;                 // google, facebook, apple, office
            public Button action;
            public GameObject linkedMark, soonMark;
            public ArabicText sub;
        }

        public sealed class Data
        {
            public bool guest, purge;
            public int daysLeft;
            public string[] methods = new string[0];
            /// <summary>Methods the app can link now (the rest show "soon").</summary>
            public string[] ready = new string[0];
            /// <summary>The name of the account a method is linked to, for the sub line (empty = none).</summary>
            public string accountName;
        }

        [SerializeField] private RectTransform board;
        [SerializeField] private CanvasGroup scrimGroup, boardGroup;
        [SerializeField] private Button closeButton, scrimButton;
        [SerializeField] private ArabicText statusText, warnText;
        [SerializeField] private GameObject warnObject;
        [SerializeField] private Row[] rows;

        /// <summary>The player pressed a row's button; the argument is its key.</summary>
        public event Action<string> OnRow;
        public event Action OnClosed;

        private bool closing;

        private void Awake()
        {
            if (closeButton == null) return;      // the prefab builder adds this component before its fields are set
            closeButton.onClick.AddListener(Close); scrimButton.onClick.AddListener(Close);
            foreach (var r in rows) { var key = r.key; r.action.onClick.AddListener(() => OnRow?.Invoke(key)); }
            scrimGroup.alpha = 0f; boardGroup.alpha = 0f;
        }

        public void Show(Data d)
        {
            statusText.Text = d.guest ? "حسابك الحالي: ضيف" : "حسابك محفوظ";
            bool warn = d.guest && d.purge && d.daysLeft > 0;
            warnObject.SetActive(d.guest);
            warnText.Text = d.guest ? (warn ? "لو ما ربطته، بينحذف بعد " + CoinPill.Format(d.daysLeft) + " يوم من آخر دخول وبتخسر تقدمك." : "اربطه عشان ما تخسر تقدمك لو بدّلت جهازك.") : "";
            foreach (var r in rows)
            {
                bool linked = Array.IndexOf(d.methods, r.key) >= 0;
                bool ready = r.key == "office" || Array.IndexOf(d.ready, r.key) >= 0;
                r.linkedMark.SetActive(linked);
                r.soonMark.SetActive(!linked && !ready);
                r.action.gameObject.SetActive(!linked && ready);
            }
            StartCoroutine(Fade(0f, 1f, .3f, false));
        }

        public void Close() { if (closing) return; closing = true; StartCoroutine(Fade(1f, 0f, .2f, true)); }

        private IEnumerator Fade(float from, float to, float seconds, bool destroy)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Lerp(from, to, t / seconds);
                scrimGroup.alpha = k; boardGroup.alpha = k;
                board.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, k);
                yield return null;
            }
            scrimGroup.alpha = to; boardGroup.alpha = to; board.localScale = Vector3.one;
            if (destroy) { OnClosed?.Invoke(); Destroy(gameObject); }
        }
    }
}
