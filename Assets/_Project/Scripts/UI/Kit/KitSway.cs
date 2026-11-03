using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>Slow pendulum sway of a hanging frame (HTML "swayf": rotate -0.7deg..0.7deg over 7s). Pivot is the top edge.</summary>
    public sealed class KitSway : MonoBehaviour
    {
        [SerializeField] private float degrees = 0.7f;
        [SerializeField] private float period = 7f;
        [SerializeField] private float phase;

        private void LateUpdate()
        {
            if (!Application.isPlaying || !MafiaNight.Core.DevicePerf.DecorativeMotion) return;
            float a = Mathf.Sin((Time.unscaledTime + phase) / period * Mathf.PI * 2f) * degrees;
            transform.localRotation = Quaternion.Euler(0f, 0f, a);
        }
    }
}
