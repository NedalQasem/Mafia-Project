using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.Animations
{
    /// <summary>
    /// Handles smooth scene transition animations
    /// Supports: Fade, Slide, Scale transitions
    /// </summary>
    public class SceneTransitionAnimator : MonoBehaviour
    {
        public static SceneTransitionAnimator Instance { get; private set; }
        
        [Header("Transition Settings")]
        [SerializeField] private CanvasGroup transitionCanvasGroup;
        [SerializeField] private Image transitionImage;
        [SerializeField] private float defaultFadeDuration = 0.5f;
        
        [Header("Transition Colors")]
        [SerializeField] private Color fadeColor = Color.black;
        
        private void Awake()
        {
            // Singleton pattern
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            // Initialize
            if (transitionCanvasGroup != null)
            {
                transitionCanvasGroup.alpha = 0;
                transitionCanvasGroup.blocksRaycasts = false;
            }
            
            if (transitionImage != null)
            {
                transitionImage.color = fadeColor;
            }
        }
        
        #region Fade Transitions
        
        /// <summary>
        /// Fade out to black (or specified color)
        /// </summary>
        public async Task FadeOut(float duration = -1)
        {
            if (duration < 0) duration = defaultFadeDuration;
            
            if (transitionCanvasGroup == null)
            {
                Debug.LogWarning("[SceneTransitionAnimator] TransitionCanvasGroup is null!");
                return;
            }
            
            transitionCanvasGroup.blocksRaycasts = true;
            
            float elapsed = 0;
            float startAlpha = transitionCanvasGroup.alpha;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transitionCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);
                await Task.Yield();
            }
            
            transitionCanvasGroup.alpha = 1f;
        }
        
        /// <summary>
        /// Fade in from black (or specified color)
        /// </summary>
        public async Task FadeIn(float duration = -1)
        {
            if (duration < 0) duration = defaultFadeDuration;
            
            if (transitionCanvasGroup == null)
            {
                Debug.LogWarning("[SceneTransitionAnimator] TransitionCanvasGroup is null!");
                return;
            }
            
            float elapsed = 0;
            float startAlpha = transitionCanvasGroup.alpha;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transitionCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                await Task.Yield();
            }
            
            transitionCanvasGroup.alpha = 0f;
            transitionCanvasGroup.blocksRaycasts = false;
        }
        
        /// <summary>
        /// Fade out then fade in (complete transition)
        /// </summary>
        public async Task FadeTransition(float fadeOutDuration = -1, float fadeInDuration = -1)
        {
            await FadeOut(fadeOutDuration);
            await FadeIn(fadeInDuration);
        }
        
        #endregion
        
        #region Utility
        
        /// <summary>
        /// Set transition color
        /// </summary>
        public void SetTransitionColor(Color color)
        {
            fadeColor = color;
            if (transitionImage != null)
            {
                transitionImage.color = color;
            }
        }
        
        /// <summary>
        /// Instantly hide transition overlay
        /// </summary>
        public void HideInstant()
        {
            if (transitionCanvasGroup != null)
            {
                transitionCanvasGroup.alpha = 0;
                transitionCanvasGroup.blocksRaycasts = false;
            }
        }
        
        /// <summary>
        /// Instantly show transition overlay
        /// </summary>
        public void ShowInstant()
        {
            if (transitionCanvasGroup != null)
            {
                transitionCanvasGroup.alpha = 1;
                transitionCanvasGroup.blocksRaycasts = true;
            }
        }
        
        #endregion
    }
}
