using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// A private message from the office, as it lands over the screen (Docs/mockups/direct-message.html): a paper letter with a wax seal, a title,
    /// a text and maybe a photo taped to it (tap it to see it big). A note has an "OK" button; a warning has hazard tape, a red "تحذير" stamp and
    /// a button that stays dead for three seconds when the warning must be read. Create it from the prefab, then <see cref="Show"/>.
    /// </summary>
    public sealed class KitDirectLetter : MonoBehaviour
    {
        public sealed class Data
        {
            /// <summary>1 = a note, 2 = a warning.</summary>
            public int type = 1;
            public string title, body, when, imageId;
            public bool must;
        }

        private const float MustSeconds = 3f;

        [SerializeField] private RectTransform letter;
        [SerializeField] private CanvasGroup scrimGroup, letterGroup;
        [SerializeField] private Image paper, okFace;
        [SerializeField] private GameObject tapeTop, tapeBottom, warnStamp, mustNote, photo, viewer, keepNote;
        [SerializeField] private ArabicText fromText, whenText, titleText, bodyText, okLabel, sealLetter;
        [SerializeField] private Button okButton, photoButton, viewerButton;
        [SerializeField] private RawImage photoPic, viewerPic;
        [SerializeField] private Image sealImage;
        [SerializeField] private HorizontalLayoutGroup titleRow;
        [SerializeField] private Sprite greenFace, redFace;

        /// <summary>The player pressed the button: the letter counts as read.</summary>
        public event Action OnDismissed;

        private Data data;
        private Texture2D loaded;
        private bool leaving, ready;
        private Coroutine countdown;

        private void Awake()
        {
            if (okButton == null) return;      // the prefab builder adds this component before its fields are set
            okButton.onClick.AddListener(Dismiss);
            photoButton.onClick.AddListener(() => { if (loaded != null) OpenViewer(); });
            viewerButton.onClick.AddListener(() => viewer.SetActive(false));
            viewer.SetActive(false);
            scrimGroup.alpha = 0f; letterGroup.alpha = 0f;
        }

        public void Show(Data d)
        {
            data = d;
            bool warn = d.type == 2;
            titleText.Text = d.title ?? ""; bodyText.Text = d.body ?? "";
            fromText.Text = warn ? "تحذير رسمي · من مكتب الريّس" : "رسالة خاصة · من مكتب الريّس";
            whenText.Text = string.IsNullOrEmpty(d.when) ? "هلأ" : d.when;
            fromText.GetComponent<TMPro.TMP_Text>().color = warn ? new Color32(0x8A, 0x1C, 0x24, 255) : new Color32(0xC0, 0x32, 0x3A, 255);
            titleText.GetComponent<TMPro.TMP_Text>().color = warn ? new Color32(0x8A, 0x1C, 0x24, 255) : new Color32(0x1C, 0x17, 0x0F, 255);
            titleRow.padding = new RectOffset(warn ? 330 : 0, 0, 0, 0);       // keeps the title clear of the stamp
            paper.color = warn ? new Color32(0xFC, 0xF7, 0xF2, 255) : Color.white;
            tapeTop.SetActive(warn); tapeBottom.SetActive(warn); warnStamp.SetActive(warn);
            sealLetter.Text = warn ? "!" : "م";
            titleRow.GetComponent<LayoutElement>().preferredHeight = 24f + Mathf.Max(1, Mathf.CeilToInt((d.title ?? "").Length / (warn ? 11f : 20f))) * 84f;
            bodyText.GetComponent<LayoutElement>().preferredHeight = 20f + Mathf.Max(1, Mathf.CeilToInt((d.body ?? "").Length / 34f)) * 54f;
            mustNote.SetActive(warn && d.must);
            keepNote.SetActive(!warn);
            okFace.sprite = warn && redFace != null ? redFace : greenFace;
            okLabel.Text = warn ? "فهمت" : "حسناً";
            bool hasPic = !string.IsNullOrEmpty(d.imageId);
            photo.SetActive(hasPic);
            if (hasPic)
            {
                photoPic.color = new Color(1f, 1f, 1f, 0f);
                if (KitImages.Loader != null) StartCoroutine(KitImages.Loader(d.imageId, tex => { if (this == null || tex == null) return; loaded = tex; photoPic.texture = tex; photoPic.color = Color.white; Cover(photoPic, tex); }));
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(letter);
            StartCoroutine(Enter(warn));
            if (warn && d.must) countdown = StartCoroutine(Countdown());
            else { ready = true; okButton.interactable = true; }
        }

        // the picture fills its frame like a photo print: the middle is kept and the edges are cut
        private static void Cover(RawImage img, Texture tex)
        {
            var size = img.rectTransform.rect.size;
            if (size.x <= 1f || size.y <= 1f || tex.width == 0) { img.uvRect = new Rect(0, 0, 1, 1); return; }
            float frame = size.x / size.y, pic = (float)tex.width / tex.height;
            img.uvRect = pic > frame ? new Rect((1f - frame / pic) / 2f, 0f, frame / pic, 1f) : new Rect(0f, (1f - pic / frame) / 2f, 1f, pic / frame);
        }

        private void OpenViewer()
        {
            viewerPic.texture = loaded;
            var fit = viewerPic.GetComponent<AspectRatioFitter>();
            if (fit != null) fit.aspectRatio = (float)loaded.width / loaded.height;
            viewer.SetActive(true);
            viewer.transform.SetAsLastSibling();
        }

        private IEnumerator Countdown()
        {
            ready = false; okButton.interactable = false;
            for (float left = MustSeconds; left > 0f; left -= Time.unscaledDeltaTime)
            {
                okLabel.Text = "فهمت (" + CoinPill.Format(Mathf.CeilToInt(left)) + ")";
                yield return null;
            }
            okLabel.Text = "فهمت"; okButton.interactable = true; ready = true;
        }

        private void Dismiss()
        {
            if (leaving || !ready) return;
            leaving = true;
            StartCoroutine(Leave());
        }

        private IEnumerator Enter(bool warn)
        {
            var rest = letter.anchoredPosition;
            for (float t = 0f; t < 0.55f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.55f);
                float e = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);       // a short bounce at the end
                scrimGroup.alpha = Mathf.Clamp01(t / 0.3f); letterGroup.alpha = Mathf.Clamp01(t / 0.2f);
                letter.anchoredPosition = rest + new Vector2(0f, 1500f * (1f - e));
                letter.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(6f, -1f, e));
                yield return null;
            }
            scrimGroup.alpha = 1f; letterGroup.alpha = 1f; letter.anchoredPosition = rest; letter.localRotation = Quaternion.Euler(0f, 0f, -1f);
            if (warn)
                for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
                {
                    letter.anchoredPosition = rest + new Vector2(Mathf.Sin(t * 60f) * 14f * (1f - t / 0.5f), 0f);
                    yield return null;
                }
            letter.anchoredPosition = rest;
        }

        private IEnumerator Leave()
        {
            var rest = letter.anchoredPosition;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.25f;
                scrimGroup.alpha = 1f - k; letterGroup.alpha = 1f - k;
                letter.anchoredPosition = rest + new Vector2(0f, 120f * k);
                yield return null;
            }
            OnDismissed?.Invoke();
            Destroy(gameObject);
        }
    }
}
