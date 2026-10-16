using UnityEngine;

namespace MafiaNight.Animations
{
    /// <summary>
    /// Predefined animation curves and presets
    /// </summary>
    public static class AnimationPresets
    {
        #region Animation Curves
        
        /// <summary>
        /// Bounce effect with overshoot (elastic feel)
        /// </summary>
        public static AnimationCurve BounceInCurve
        {
            get
            {
                AnimationCurve curve = new AnimationCurve();
                curve.AddKey(new Keyframe(0f, 0f, 0f, 0f));
                curve.AddKey(new Keyframe(0.5f, 1.1f)); // Overshoot
                curve.AddKey(new Keyframe(0.75f, 0.95f)); // Bounce back
                curve.AddKey(new Keyframe(1f, 1f, 0f, 0f));
                return curve;
            }
        }
        
        /// <summary>
        /// Smooth exponential ease in/out
        /// </summary>
        public static AnimationCurve SmoothCurve
        {
            get
            {
                return AnimationCurve.EaseInOut(0, 0, 1, 1);
            }
        }
        
        /// <summary>
        /// Quick snap animation
        /// </summary>
        public static AnimationCurve QuickCurve
        {
            get
            {
                AnimationCurve curve = new AnimationCurve();
                curve.AddKey(new Keyframe(0f, 0f));
                curve.AddKey(new Keyframe(1f, 1f));
                return curve;
            }
        }
        
        /// <summary>
        /// Elastic overshoot (for dramatic effects)
        /// </summary>
        public static AnimationCurve ElasticCurve
        {
            get
            {
                AnimationCurve curve = new AnimationCurve();
                curve.AddKey(new Keyframe(0f, 0f));
                curve.AddKey(new Keyframe(0.6f, 1.2f)); // Big overshoot
                curve.AddKey(new Keyframe(0.8f, 0.9f));
                curve.AddKey(new Keyframe(1f, 1f));
                return curve;
            }
        }
        
        #endregion
        
        #region Duration Presets
        
        public const float QuickDuration = 0.2f;
        public const float NormalDuration = 0.3f;
        public const float SlowDuration = 0.5f;
        public const float VerySlowDuration = 1.0f;
        
        #endregion
        
        #region Delay Presets
        
        public const float TinyDelay = 0.05f;
        public const float ShortDelay = 0.1f;
        public const float MediumDelay = 0.2f;
        public const float LongDelay = 0.3f;
        
        #endregion
    }
}
