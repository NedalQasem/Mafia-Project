using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Animations
{
    /// <summary>
    /// Animates UI elements with various effects
    /// Supports: Fade, Scale, Slide, Bounce, Sequential animations
    /// </summary>
    public class UIElementAnimator : MonoBehaviour
    {
        #region Fade Animations
        
        /// <summary>
        /// Fade in a CanvasGroup
        /// </summary>
        public static async Task FadeIn(CanvasGroup canvasGroup, float duration, AnimationCurve curve = null)
        {
            if (canvasGroup == null) return;
            
            if (curve == null)
                curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            
            float elapsed = 0;
            float startAlpha = canvasGroup.alpha;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);
                await Task.Yield();
            }
            
            canvasGroup.alpha = 1f;
        }
        
        /// <summary>
        /// Fade out a CanvasGroup
        /// </summary>
        public static async Task FadeOut(CanvasGroup canvasGroup, float duration, AnimationCurve curve = null)
        {
            if (canvasGroup == null) return;
            
            if (curve == null)
                curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            
            float elapsed = 0;
            float startAlpha = canvasGroup.alpha;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                await Task.Yield();
            }
            
            canvasGroup.alpha = 0f;
        }
        
        #endregion
        
        #region Scale Animations
        
        /// <summary>
        /// Scale from zero to full size (pop in effect)
        /// </summary>
        public static async Task ScaleIn(RectTransform rectTransform, float duration, AnimationCurve curve = null)
        {
            if (rectTransform == null) return;
            
            if (curve == null)
                curve = AnimationPresets.BounceInCurve;
            
            Vector3 targetScale = rectTransform.localScale;
            rectTransform.localScale = Vector3.zero;
            
            float elapsed = 0;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
                rectTransform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);
                await Task.Yield();
            }
            
            rectTransform.localScale = targetScale;
        }
        
        /// <summary>
        /// Scale from full size to zero (pop out effect)
        /// </summary>
        public static async Task ScaleOut(RectTransform rectTransform, float duration, AnimationCurve curve = null)
        {
            if (rectTransform == null) return;
            
            if (curve == null)
                curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            
            Vector3 startScale = rectTransform.localScale;
            
            float elapsed = 0;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
                rectTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                await Task.Yield();
            }
            
            rectTransform.localScale = Vector3.zero;
        }
        
        #endregion
        
        #region Slide Animations
        
        public enum SlideDirection
        {
            Up,
            Down,
            Left,
            Right
        }
        
        /// <summary>
        /// Slide UI element from off-screen
        /// </summary>
        public static async Task SlideIn(RectTransform rectTransform, SlideDirection direction, float duration, float distance = 1000f)
        {
            if (rectTransform == null) return;
            
            Vector2 startPos = rectTransform.anchoredPosition;
            Vector2 offsetPos = startPos;
            
            switch (direction)
            {
                case SlideDirection.Up:
                    offsetPos.y -= distance;
                    break;
                case SlideDirection.Down:
                    offsetPos.y += distance;
                    break;
                case SlideDirection.Left:
                    offsetPos.x += distance;
                    break;
                case SlideDirection.Right:
                    offsetPos.x -= distance;
                    break;
            }
            
            rectTransform.anchoredPosition = offsetPos;
            
            float elapsed = 0;
            AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
                rectTransform.anchoredPosition = Vector2.Lerp(offsetPos, startPos, t);
                await Task.Yield();
            }
            
            rectTransform.anchoredPosition = startPos;
        }
        
        #endregion
        
        #region Combo Animations
        
        /// <summary>
        /// Scale and fade in simultaneously
        /// </summary>
        public static async Task ScaleAndFadeIn(GameObject target, float duration)
        {
            if (target == null) return;
            
            RectTransform rectTransform = target.GetComponent<RectTransform>();
            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            
            if (canvasGroup == null)
                canvasGroup = target.AddComponent<CanvasGroup>();
            
            // Start both animations concurrently
            Task scaleTask = rectTransform != null ? ScaleIn(rectTransform, duration) : Task.CompletedTask;
            Task fadeTask = FadeIn(canvasGroup, duration);
            
            await Task.WhenAll(scaleTask, fadeTask);
        }
        
        #endregion
        
        #region Sequential Animations
        
        /// <summary>
        /// Animate multiple elements sequentially with delay
        /// </summary>
        public static async Task AnimateSequential(GameObject[] elements, float delayBetween, float animationDuration)
        {
            foreach (GameObject element in elements)
            {
                if (element == null) continue;
                
                // Animate this element
                await ScaleAndFadeIn(element, animationDuration);
                
                // Wait before next element
                await Task.Delay((int)(delayBetween * 1000));
            }
        }
        
        /// <summary>
        /// Animate multiple elements sequentially with custom animation
        /// </summary>
        public static async Task AnimateSequentialCustom(
            GameObject[] elements, 
            float delayBetween,
            System.Func<GameObject, Task> customAnimation)
        {
            foreach (GameObject element in elements)
            {
                if (element == null) continue;
                
                // Custom animation
                await customAnimation(element);
                
                // Wait before next element
                await Task.Delay((int)(delayBetween * 1000));
            }
        }
        
        #endregion
        
        #region Utility
        
        /// <summary>
        /// Hide element instantly
        /// </summary>
        public static void HideInstant(GameObject target)
        {
            if (target == null) return;
            
            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = target.AddComponent<CanvasGroup>();
            
            canvasGroup.alpha = 0;
            
            RectTransform rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform != null)
                rectTransform.localScale = Vector3.zero;
        }
        
        /// <summary>
        /// Show element instantly
        /// </summary>
        public static void ShowInstant(GameObject target)
        {
            if (target == null) return;
            
            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = target.AddComponent<CanvasGroup>();
            
            canvasGroup.alpha = 1;
            
            RectTransform rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform != null)
                rectTransform.localScale = Vector3.one;
        }
        
        #endregion
    }
}
