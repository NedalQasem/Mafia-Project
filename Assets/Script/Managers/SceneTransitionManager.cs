using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
using UnityEngine.UI;

namespace MafiaNight.Core
{
    /// <summary>
    /// Scene Transition Manager - إدارة الانتقال بين Scenes
    /// Singleton - يبقى بين الـ Scenes
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("Transition Settings")]
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private bool showLoadingScreen = true;

        [Header("Loading UI (Optional)")]
        [SerializeField] private GameObject loadingCanvas;
        [SerializeField] private Image fadeImage;
        [SerializeField] private TextMeshProUGUI loadingText;
        [SerializeField] private Slider progressBar;

        private bool isTransitioning = false;

        #region Unity Lifecycle

        private void Awake()
        {
            // Singleton Pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // إخفاء Loading UI في البداية
            if (loadingCanvas != null)
                loadingCanvas.SetActive(false);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// تحميل Scene جديدة مع Transition
        /// </summary>
        public void LoadScene(string sceneName)
        {
            if (isTransitioning)
            {
                Debug.LogWarning("[SceneTransition] Already transitioning to another scene!");
                return;
            }

            StartCoroutine(LoadSceneAsync(sceneName));
        }

        /// <summary>
        /// تحميل Scene بـ build index
        /// </summary>
        public void LoadScene(int sceneIndex)
        {
            if (isTransitioning)
            {
                Debug.LogWarning("[SceneTransition] Already transitioning!");
                return;
            }

            StartCoroutine(LoadSceneAsync(sceneIndex));
        }

        /// <summary>
        /// Reload Scene الحالية
        /// </summary>
        public void ReloadCurrentScene()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            LoadScene(currentScene.name);
        }

        #endregion

        #region Scene Loading

        /// <summary>
        /// تحميل Scene مع Async + Loading Screen
        /// </summary>
        private IEnumerator LoadSceneAsync(string sceneName)
        {
            isTransitioning = true;

            // ================== Fade Out ==================
            if (fadeImage != null)
            {
                yield return StartCoroutine(FadeOut());
            }

            // ================== عرض Loading Screen ==================
            if (showLoadingScreen && loadingCanvas != null)
            {
                loadingCanvas.SetActive(true);
                UpdateLoadingText($"Loading {sceneName}...");
            }

            // ================== بدء Async Loading ==================
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.allowSceneActivation = false; // ما نفعل Scene إلا لما نخلص

            // ================== انتظار التحميل ==================
            while (!asyncLoad.isDone)
            {
                // Progress من 0 إلى 0.9 (Unity limitation)
                float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                
                if (progressBar != null)
                    progressBar.value = progress;

                // لما يوصل 90%، Scene جاهز
                if (asyncLoad.progress >= 0.9f)
                {
                    UpdateLoadingText("Press any key to continue...");
                    
                    // انتظار لحظة أو Input (اختياري)
                    yield return new WaitForSeconds(0.5f);
                    
                    // تفعيل Scene
                    asyncLoad.allowSceneActivation = true;
                }

                yield return null;
            }

            Debug.Log($"[SceneTransition] ✅ Loaded scene: {sceneName}");

            // ================== Fade In ==================
            if (fadeImage != null)
            {
                yield return StartCoroutine(FadeIn());
            }

            // ================== إخفاء Loading Screen ==================
            if (loadingCanvas != null)
            {
                loadingCanvas.SetActive(false);
            }

            isTransitioning = false;
        }

        /// <summary>
        /// تحميل Scene بـ index
        /// </summary>
        private IEnumerator LoadSceneAsync(int sceneIndex)
        {
            string sceneName = SceneManager.GetSceneByBuildIndex(sceneIndex).name;
            yield return StartCoroutine(LoadSceneAsync(sceneName));
        }

        #endregion

        #region Fade Effects

        /// <summary>
        /// Fade to black
        /// </summary>
        private IEnumerator FadeOut()
        {
            if (fadeImage == null) yield break;

            float elapsed = 0f;
            Color color = fadeImage.color;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                color.a = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
                fadeImage.color = color;
                yield return null;
            }

            color.a = 1f;
            fadeImage.color = color;
        }

        /// <summary>
        /// Fade from black
        /// </summary>
        private IEnumerator FadeIn()
        {
            if (fadeImage == null) yield break;

            float elapsed = 0f;
            Color color = fadeImage.color;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                color.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                fadeImage.color = color;
                yield return null;
            }

            color.a = 0f;
            fadeImage.color = color;
        }

        #endregion

        #region UI Helpers

        private void UpdateLoadingText(string text)
        {
            if (loadingText != null)
                loadingText.text = text;
        }

        #endregion
    }
}
