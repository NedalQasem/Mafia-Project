using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Tiny UI particles for the reward popup: dust drifting up in the spotlight, and bursts of sparks that fall with gravity.
    /// Every particle is a pooled Image (the dot sprite), moved in unscaled time; local units of this RectTransform.
    /// </summary>
    public sealed class KitSparks : MonoBehaviour
    {
        [SerializeField] private Sprite dot;
        [Tooltip("Canvas units per mockup pixel: speeds and sizes are written in mockup pixels.")]
        [SerializeField] private float unit = 2.77f;

        private sealed class P { public Image img; public Vector2 pos, vel; public float size, alpha, life, gravity; public bool dust; }
        private readonly List<P> live = new List<P>();
        private readonly Stack<Image> pool = new Stack<Image>();
        private RectTransform area;

        private void Awake() => area = (RectTransform)transform;

        /// <summary>Slow dust in the spotlight cone (it wraps from top to bottom until <see cref="Clear"/>).</summary>
        public void Dust(int count)
        {
            var r = area.rect;
            for (int i = 0; i < count; i++)
                Add(new Vector2(Random.Range(-75f, 75f) * unit, Random.Range(r.yMin, r.yMax)), new Vector2(Random.Range(-.075f, .075f), Random.Range(.15f, .4f)) * unit * 60f,
                    Random.Range(.6f, 2f), Random.Range(.15f, .5f), float.MaxValue, 0f, new Color(.96f, .89f, .66f), true);
        }

        /// <summary>A burst of sparks at a local point (mockup speeds: 1.5 to 7 px per frame, gravity .12).</summary>
        public void Burst(Vector2 at, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f, s = Random.Range(1.5f, 7f);
                var v = new Vector2(Mathf.Cos(a) * s, -Mathf.Sin(a) * s + 3f) * unit * 60f;
                Add(at, v, Random.Range(1f, 3.4f), 1f, Random.Range(.85f, 1.5f), .12f * unit * 3600f, color, false);
            }
        }

        public void Clear()
        {
            foreach (var p in live) Release(p.img);
            live.Clear();
        }

        private void Add(Vector2 pos, Vector2 vel, float size, float alpha, float life, float gravity, Color c, bool dust)
        {
            var img = pool.Count > 0 ? pool.Pop() : NewImage();
            img.gameObject.SetActive(true);
            c.a = alpha; img.color = c;
            float d = size * 2f * unit; img.rectTransform.sizeDelta = new Vector2(d, d);
            img.rectTransform.anchoredPosition = pos;
            live.Add(new P { img = img, pos = pos, vel = vel, size = size, alpha = alpha, life = life, gravity = gravity, dust = dust });
        }

        private Image NewImage()
        {
            var g = new GameObject("p", typeof(RectTransform));
            g.transform.SetParent(transform, false);
            var img = g.AddComponent<Image>(); img.sprite = dot; img.raycastTarget = false;
            return img;
        }

        private void Release(Image img) { img.gameObject.SetActive(false); pool.Push(img); }

        private void Update()
        {
            if (live.Count == 0) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f), top = area.rect.yMax, bottom = area.rect.yMin;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.pos += p.vel * dt;
                if (p.dust) { if (p.pos.y > top + 10f) p.pos.y = bottom - 10f; }
                else
                {
                    p.vel.y -= p.gravity * dt; p.vel.x *= Mathf.Pow(.985f, dt * 60f);
                    p.life -= dt;
                    if (p.life < .66f) { var c = p.img.color; c.a = Mathf.Max(0f, p.life / .66f); p.img.color = c; }
                    if (p.life <= 0f) { Release(p.img); live.RemoveAt(i); continue; }
                }
                p.img.rectTransform.anchoredPosition = p.pos;
            }
        }
    }
}
