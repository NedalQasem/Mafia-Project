using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>Back-and-forth swing between two angles (HTML "swing": the hanging price tag sways around its top corner).</summary>
    public sealed class KitSwing : MonoBehaviour
    {
        [SerializeField] private float angleA = -5f;
        [SerializeField] private float angleB = 2f;
        [SerializeField] private float period = 4f;
        [SerializeField] private float phase;

        private void LateUpdate()
        {
            if (!Application.isPlaying || !MafiaNight.Core.DevicePerf.DecorativeMotion) return;
            float k = 0.5f - 0.5f * Mathf.Cos((Time.unscaledTime + phase) / period * Mathf.PI * 2f);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(angleA, angleB, k));
        }

        public void Randomize() { phase = Random.value * period; }
    }
}
