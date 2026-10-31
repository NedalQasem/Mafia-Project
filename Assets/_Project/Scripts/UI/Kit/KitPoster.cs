using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// A picture the admin board uploaded, drawn into a box (Docs/mockups/launch-banner.html ".poster", events.html ".pic"): it covers the box
    /// (cropped from the middle, never stretched), a grey-beige shimmer shows while it loads, and when it cannot be had the box is left empty so
    /// the caller shows what it showed before. The pictures come through <see cref="KitImages.Loader"/>.
    /// </summary>
    public sealed class KitPoster : MonoBehaviour
    {
        private RawImage image, shimmer;
        private Texture2D shown;
        private float t;

        /// <summary>Puts a poster in the box (an earlier one in it is replaced). done(true) when the picture arrived, done(false) when it did not.</summary>
        public static KitPoster Show(MonoBehaviour owner, RectTransform box, string imageId, Action<bool> done = null)
        {
            var old = box.GetComponentInChildren<KitPoster>(true);
            if (old != null) Destroy(old.gameObject);
            if (string.IsNullOrEmpty(imageId) || KitImages.Loader == null) { done?.Invoke(false); return null; }
            var go = new GameObject("Poster", typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(box, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<RectMask2D>();
            var p = go.AddComponent<KitPoster>();
            p.shimmer = MakeLayer(go.transform, "Shimmer"); p.shimmer.color = new Color(0.79f, 0.75f, 0.64f, 1f);
            p.image = MakeLayer(go.transform, "Picture"); p.image.color = new Color(1, 1, 1, 0);
            p.StartCoroutine(p.Run(imageId, done));
            return p;
        }

        private static RawImage MakeLayer(Transform parent, string name)
        {
            var g = new GameObject(name, typeof(RectTransform)); g.transform.SetParent(parent, false);
            var r = (RectTransform)g.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            var ri = g.AddComponent<RawImage>(); ri.raycastTarget = false;
            return ri;
        }

        private IEnumerator Run(string id, Action<bool> done)
        {
            Texture2D tex = null;
            yield return KitImages.Loader(id, x => tex = x);
            if (tex == null) { Destroy(gameObject); done?.Invoke(false); yield break; }
            shown = tex; image.texture = tex; Cover();
            for (float k = 0f; k < 0.25f; k += Time.unscaledDeltaTime) { image.color = new Color(1, 1, 1, k / 0.25f); yield return null; }
            image.color = Color.white; shimmer.gameObject.SetActive(false);
            done?.Invoke(true);
        }

        private void Update()
        {
            if (shown != null || shimmer == null) return;
            t += Time.unscaledDeltaTime;
            float a = 0.88f + 0.12f * Mathf.Sin(t * 4.5f);          // a slow breathing instead of the page's moving gleam
            shimmer.color = new Color(0.79f * a, 0.75f * a, 0.64f * a, 1f);
        }

        private void OnRectTransformDimensionsChange() { if (shown != null) Cover(); }

        /// <summary>Crops the picture from its middle to the box's shape.</summary>
        private void Cover()
        {
            var r = ((RectTransform)transform).rect;
            if (shown == null || r.width < 1f || r.height < 1f) return;
            float ta = (float)shown.width / shown.height, fa = r.width / r.height;
            image.uvRect = ta > fa ? new Rect((1f - fa / ta) / 2f, 0f, fa / ta, 1f) : new Rect(0f, (1f - ta / fa) / 2f, 1f, ta / fa);
        }
    }
}
