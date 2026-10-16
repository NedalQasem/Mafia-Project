using System.Collections;
using MafiaNight.Core;
using MafiaNight.UI.Animations;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MafiaNight.Services.Scenes
{
    /// <summary>
    /// Loads scenes asynchronously with a fade transition. Persists across scenes.
    /// Uses <see cref="SceneTransitionAnimator"/> when available, otherwise its own fade image.
    /// </summary>
    public class SceneTransitionManager : PersistentService<ISceneLoader>, ISceneLoader
    {
        private const string Tag = "SceneTransition";

        [Header("Transition Settings")]
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private bool showLoadingScreen = true;

        [Header("Loading UI (Optional)")]
        [SerializeField] private GameObject loadingCanvas;
        [SerializeField] private Image fadeImage;
        [SerializeField] private TextMeshProUGUI loadingText;
        [SerializeField] private Slider progressBar;

        private bool isTransitioning = false;

        public bool IsTransitioning => isTransitioning;

        protected override void OnServiceAwake()
        {
            if (loadingCanvas != null)
                loadingCanvas.SetActive(false);
        }

        public void LoadScene(string sceneName)
        {
            if (isTransitioning)
            {
                Log.Warning($"Already transitioning - ignoring request for '{sceneName}'", Tag);
                return;
            }

            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            isTransitioning = true;
            ServiceLocator.TryGet<SceneTransitionAnimator>(out var animator);

            // Fade out
            if (animator != null)
            {
                var fadeOutTask = animator.FadeOut(fadeDuration);
                yield return new WaitUntil(() => fadeOutTask.IsCompleted);
            }
            else if (fadeImage != null)
            {
                yield return Fade(0f, 1f);
            }

            if (showLoadingScreen && loadingCanvas != null)
            {
                loadingCanvas.SetActive(true);
                SetLoadingText($"Loading {sceneName}...");
            }

            // Load
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.allowSceneActivation = false;

            while (!asyncLoad.isDone)
            {
                if (progressBar != null)
                    progressBar.value = Mathf.Clamp01(asyncLoad.progress / 0.9f);

                if (asyncLoad.progress >= 0.9f && !asyncLoad.allowSceneActivation)
                {
                    SetLoadingText("Ready!");
                    yield return new WaitForSeconds(0.3f);
                    asyncLoad.allowSceneActivation = true;
                }

                yield return null;
            }

            Log.Info($"✅ Loaded scene: {sceneName}", Tag);

            // Fade in
            if (animator != null)
            {
                var fadeInTask = animator.FadeIn(fadeDuration);
                yield return new WaitUntil(() => fadeInTask.IsCompleted);
            }
            else if (fadeImage != null)
            {
                yield return Fade(1f, 0f);
            }

            if (loadingCanvas != null)
                loadingCanvas.SetActive(false);

            isTransitioning = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            float elapsed = 0f;
            Color color = fadeImage.color;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                color.a = Mathf.Lerp(from, to, elapsed / fadeDuration);
                fadeImage.color = color;
                yield return null;
            }

            color.a = to;
            fadeImage.color = color;
        }

        private void SetLoadingText(string text)
        {
            if (loadingText != null)
                loadingText.text = text;
        }
    }
}
