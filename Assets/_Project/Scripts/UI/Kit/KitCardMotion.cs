using MafiaNight.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The "motion" layer of a card (shine, embers, smoke): a few images moved by transform only, clipped to the card. It is the first thing the device gives up:
    /// <see cref="DevicePerf.DecorativeMotion"/> off (a weak phone, battery saver, reduced motion) and nothing is built; the number of moving pieces is also capped by
    /// <see cref="DevicePerf.MaxAnimatedFrames"/>. Cards of other players that are small (the seats) never get one.
    /// </summary>
    public sealed class KitCardMotion : MonoBehaviour
    {
        private struct Piece { public RectTransform Rt; public float Phase, Speed, X, Drift; }

        private RectTransform card;
        private string kind = "none";
        private float K = 1f;
        private Piece[] pieces = new Piece[0];
        private RectTransform band;

        public void Setup(RectTransform cardRoot, string motion, float k)
        {
            card = cardRoot; kind = motion ?? "none"; K = k;
            if (!MafiaNight.Data.Cards.CardStyles.IsMotion(kind) || !DevicePerf.DecorativeMotion || cardRoot.rect.width < 120f) return;

            // everything moves inside a clip that follows the card's plate
            var clip = new GameObject("Motion", typeof(RectTransform), typeof(RectMask2D));
            var cr = (RectTransform)clip.transform; cr.SetParent(card, false);
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = new Vector2(8 * K, 8 * K); cr.offsetMax = new Vector2(-8 * K, -8 * K);

            if (kind == "shine")
            {
                var spec = new KitProc.Spec { W = 90 * K, H = 340 * K, Radius = 1f, Horizontal = true, Top = new Color(1, 1, 1, 0), Mid = new Color(1, 1, 1, KitProc.LinA(0.28f)), MidAt = 0.5f, Bottom = new Color(1, 1, 1, 0) };
                var im = NewImage(cr, "Band", KitProc.Paint("rcshine" + Mathf.RoundToInt(K * 100), spec), Color.white);
                band = im.rectTransform; band.sizeDelta = new Vector2(90 * K, 340 * K); band.localRotation = Quaternion.Euler(0, 0, -16f);
                band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f);
                return;
            }

            int n = Mathf.Min(kind == "embers" ? 12 : 4, Mathf.Max(0, DevicePerf.MaxAnimatedFrames));
            pieces = new Piece[n];
            for (int i = 0; i < n; i++)
            {
                bool ember = kind == "embers";
                float size = ember ? Random.Range(3f, 5.5f) * K : Random.Range(70f, 110f) * K;
                Sprite sp = ember
                    ? KitProc.Paint("rcember" + Mathf.RoundToInt(size), new KitProc.Spec { W = size, H = size, Radius = size * 0.5f, Margin = 3 * K, Radial = true, RA = new Color32(0xFF, 0xF3, 0xB0, 255), RB = new Color32(0xFF, 0x9A, 0x3A, 255), RC = new Color32(0xFF, 0x9A, 0x3A, 0), RBAt = 0.5f, RadialAt = new Vector2(0.5f, 0.5f), RadialReach = 0.9f })
                    : KitProc.Paint("rcsmoke" + Mathf.RoundToInt(size), new KitProc.Spec { W = size, H = size, Radius = size * 0.5f, Radial = true, RA = new Color(1, 1, 1, KitProc.LinA(0.16f)), RB = new Color(1, 1, 1, KitProc.LinA(0.08f)), RC = new Color(1, 1, 1, 0), RBAt = 0.5f, RadialAt = new Vector2(0.5f, 0.5f), RadialReach = 0.75f });
                var im = NewImage(cr, ember ? "Ember" : "Smoke", sp, Color.white);
                var r = im.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0f, 0f); r.sizeDelta = new Vector2(size + 6 * K, size + 6 * K);
                pieces[i] = new Piece { Rt = r, Phase = Random.value, Speed = ember ? Random.Range(0.18f, 0.32f) : Random.Range(0.08f, 0.14f), X = Random.Range(0.05f, 0.95f), Drift = Random.Range(-24f, 24f) * K };
            }
        }

        private static Image NewImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = sprite; i.color = color; i.raycastTarget = false;
            return i;
        }

        private void Update()
        {
            if (card == null) return;
            if (!DevicePerf.DecorativeMotion) { enabled = false; return; }
            float t = Time.unscaledTime;
            if (band != null)
            {
                float w = card.rect.width, k = Mathf.Repeat(t / 3.4f, 1f);
                float x = Mathf.Lerp(-w * 0.9f, w * 0.9f, k < 0.6f ? k / 0.6f : 1f);        // sweeps across, then rests for the rest of the cycle
                band.anchoredPosition = new Vector2(x, 0f);
                band.gameObject.SetActive(k < 0.6f);
                return;
            }
            var clip = card.Find("Motion") as RectTransform;
            if (clip == null) return;
            float cw = clip.rect.width, ch = clip.rect.height;
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                float u = Mathf.Repeat(p.Phase + t * p.Speed, 1f);
                float fade = Mathf.Sin(u * Mathf.PI);
                p.Rt.anchoredPosition = new Vector2(p.X * cw + p.Drift * u, -20f * K + u * (ch + 40f * K));
                var im = p.Rt.GetComponent<Image>(); var c = im.color; c.a = fade; im.color = c;
            }
        }
    }
}
