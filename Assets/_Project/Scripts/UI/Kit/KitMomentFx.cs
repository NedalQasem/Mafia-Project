using System.Collections;
using MafiaNight.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The table's moments (Docs/mockups/match-moments.html): the camera comes close to a player's seat, the bullet hits (white flash, a little shake, the mark of the shot), the doctor's shield opens and
    /// throws the bullet back, and a red "out" stamp lands on the one the table voted out. Everything is drawn from code over the match frames and only aims at a seat by the player's id; a device that
    /// should save its motion (low tier, battery saver, reduced motion) keeps the close-up and the mark but skips the flash and the shake. Nothing here takes a tap.
    /// </summary>
    public sealed class KitMomentFx : MonoBehaviour
    {
        private KitMatchView view;
        private RectTransform layer;
        private Image flash;

        private RectTransform zoomed;
        private Vector2 baseSize, basePos, basePivot, shake;
        private Coroutine zoomRun;

        public static KitMomentFx Create(KitMatchView view, RectTransform parent)
        {
            var go = new GameObject("MomentFx", typeof(RectTransform));
            var r = (RectTransform)go.transform; r.SetParent(parent, false);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            var fx = go.AddComponent<KitMomentFx>();
            fx.view = view; fx.layer = r;
            var f = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            f.transform.SetParent(r, false);
            var fr = (RectTransform)f.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            fx.flash = f.GetComponent<Image>(); fx.flash.color = new Color(1, 1, 1, 0); fx.flash.raycastTarget = false;
            return fx;
        }

        private static bool Motion => DevicePerf.DecorativeMotion;

        /// <summary>Seconds per step; the editor preview sets it to step the animations by hand (edit mode has no frames).</summary>
        public static float StepOverride;
        private static float Dt => StepOverride > 0f ? StepOverride : Time.unscaledDeltaTime;

        private void Run(IEnumerator r) { if (Application.isPlaying) StartCoroutine(r); }

        /// <summary>Plays the named animation of a cue (zoom_seat, shot, shield, stamp_out) at a seat. <paramref name="hold"/> is how long the close-up stays (what is left of the cue).</summary>
        public void Play(string anim, string seatId, float hold)
        {
            if (anim == "zoom_seat" && zoomRun != null) { StopCoroutine(zoomRun); zoomRun = null; }
            var r = Routine(anim, seatId, hold);
            if (r == null) return;
            if (anim == "zoom_seat") zoomRun = StartCoroutine(r); else StartCoroutine(r);
        }

        /// <summary>The animation as a routine to run (or to step by hand in the editor preview); null when the name is unknown or its seat is not on the table.</summary>
        public IEnumerator Routine(string anim, string seatId, float hold)
        {
            switch (anim)
            {
                case "zoom_seat":
                    var seat = view.SeatOf(seatId);
                    var frame = view.Current != null ? view.Frame(view.Current) : null;
                    if (seat == null || frame == null) return null;
                    Restore();
                    return Zoom(frame, seat, Mathf.Max(1f, hold));
                case "shot": return Shot(seatId);
                case "shield": return Shield(seatId);
                case "stamp_out": return Stamp(seatId, hold);
            }
            return null;
        }

        /// <summary>Puts everything back (the match ended, or the scene is leaving).</summary>
        public void Clear()
        {
            StopAllCoroutines(); zoomRun = null;
            Restore();
            for (int i = layer.childCount - 1; i >= 0; i--) { var c = layer.GetChild(i); if (c != flash.transform) Destroy(c.gameObject); }
            flash.color = new Color(1, 1, 1, 0);
        }

        // ------------------------------------------------------------------ the close-up

        private IEnumerator Zoom(RectTransform frame, RectTransform seat, float hold)
        {
            zoomed = frame; baseSize = frame.rect.size; basePos = frame.anchoredPosition; basePivot = frame.pivot;
            // the seat becomes the pivot of the frame (the frame does not move), so it can grow around him
            Vector2 local = frame.InverseTransformPoint(seat.position);
            Vector2 newPivot = basePivot + new Vector2(local.x / baseSize.x, local.y / baseSize.y);
            frame.pivot = newPivot;
            frame.anchoredPosition = basePos + Vector2.Scale(newPivot - basePivot, baseSize) * frame.localScale.x;
            Vector2 start = frame.anchoredPosition;
            // and he slides towards the middle of the screen while it grows
            var screen = (RectTransform)layer;
            Vector3 toMiddle = frame.parent.InverseTransformVector(screen.TransformPoint(screen.rect.center + new Vector2(0, 40f)) - seat.position);
            Vector2 end = start + (Vector2)toMiddle;
            const float k = 1.9f, grow = 0.5f;
            for (float t = 0; t < grow; t += Dt) { float e = Ease(t / grow); frame.localScale = Vector3.one * Mathf.Lerp(1f, k, e); frame.anchoredPosition = Vector2.Lerp(start, end, e) + shake; yield return null; }
            frame.localScale = Vector3.one * k;
            for (float t = 0; t < hold - grow - 0.4f; t += Dt) { frame.anchoredPosition = end + shake; yield return null; }
            for (float t = 0; t < 0.4f; t += Dt) { float e = Ease(t / 0.4f); frame.localScale = Vector3.one * Mathf.Lerp(k, 1f, e); frame.anchoredPosition = Vector2.Lerp(end, start, e) + shake; yield return null; }
            Restore();
        }

        private void Restore()
        {
            if (zoomed != null)
            {
                zoomed.localScale = Vector3.one; zoomed.pivot = basePivot; zoomed.anchoredPosition = basePos;
                zoomed = null;
            }
            shake = Vector2.zero;
        }

        private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private void OnDisable() { Restore(); }

        // ------------------------------------------------------------------ small drawing helpers

        private RectTransform Piece(string name, Vector2 at, Vector2 size, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(layer, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f); r.sizeDelta = size; r.anchoredPosition = at;
            var im = go.GetComponent<Image>(); im.sprite = sprite; im.color = color; im.raycastTarget = false;
            return r;
        }

        private static Sprite Disc(float d, Color fill, Color ring, float ringPx)
        {
            return KitProc.Paint("fxdisc" + Mathf.RoundToInt(d) + "_" + ColorUtility.ToHtmlStringRGBA(fill) + ColorUtility.ToHtmlStringRGBA(ring) + Mathf.RoundToInt(ringPx), new KitProc.Spec { W = d, H = d, Radius = d / 2f, Top = fill, Bottom = fill, Edge = ring, EdgePx = ringPx });
        }

        /// <summary>Where a seat's avatar is, in this layer's own coordinates (the layer does not zoom; the frame under it does).</summary>
        private Vector2 SeatAt(string seatId, Vector2 fallback)
        {
            var seat = view.SeatOf(seatId);
            if (seat == null) return fallback;
            var av = seat.Find("f_av") as RectTransform;
            Vector3 world = av != null ? av.position : seat.position;
            Vector2 local = layer.InverseTransformPoint(world);
            return local;       // the layer is centred, anchors at 0.5: its local origin is its centre
        }

        private float ScaleOfSeat(string seatId)
        {
            var seat = view.SeatOf(seatId);
            var av = seat != null ? seat.Find("f_av") as RectTransform : null;
            if (av == null) return 1f;
            return Mathf.Abs(layer.InverseTransformVector(av.TransformVector(Vector3.right)).x);       // how many layer units one avatar unit takes now (the close-up grows it)
        }

        private IEnumerator Burst()
        {
            if (!Motion) yield break;
            flash.color = new Color(1, 1, 1, 0.95f);
            Vector2 first = shake;
            for (float t = 0; t < 0.5f; t += Dt)
            {
                flash.color = new Color(1, 1, 1, 0.95f * (1f - t / 0.5f));
                float k = 1f - t / 0.5f;
                shake = new Vector2(Mathf.Sin(t * 70f), Mathf.Cos(t * 83f)) * 14f * k;
                yield return null;
            }
            flash.color = new Color(1, 1, 1, 0); shake = first;
        }

        // ------------------------------------------------------------------ the shot

        private IEnumerator Shot(string seatId)
        {
            Run(Burst());
            Vector2 at = SeatAt(seatId, Vector2.zero);
            float s = ScaleOfSeat(seatId);
            float size = 125f * s * 1.25f;
            var root = Piece("Impact", at, new Vector2(size, size), null, Color.clear);
            root.GetComponent<Image>().enabled = false;
            var crim = new Color32(0xE0, 0x58, 0x5F, 255);
            var ring = Piece("Ring", Vector2.zero, new Vector2(size * 0.5f, size * 0.5f), Disc(size * 0.5f, new Color32(0x1A, 0x06, 0x06, 255), crim, 4f), Color.white); ring.SetParent(root, false);
            ring.anchoredPosition = Vector2.zero;
            var halo = Piece("Halo", Vector2.zero, new Vector2(size * 0.9f, size * 0.9f), Disc(size * 0.9f, Color.clear, new Color(crim.r / 255f, crim.g / 255f, crim.b / 255f, 0.7f), 3f), Color.white); halo.SetParent(root, false);
            float[] angles = { 90, 30, -30, -80, -150, 150 };
            foreach (var a in angles)
            {
                var line = Piece("Crack", Vector2.zero, new Vector2(size * 0.5f, 6f), null, Color.white); line.SetParent(root, false);
                line.pivot = new Vector2(0f, 0.5f); line.localRotation = Quaternion.Euler(0, 0, a); line.anchoredPosition = Vector2.zero;
            }
            for (float t = 0; t < 1.8f; t += Dt)
            {
                float sc = t < 0.15f ? Mathf.Lerp(0.2f, 1.15f, t / 0.15f) : t < 0.3f ? Mathf.Lerp(1.15f, 1f, (t - 0.15f) / 0.15f) : 1f;
                root.localScale = Vector3.one * sc;
                var live = view.SeatOf(seatId); if (live != null) root.anchoredPosition = SeatAt(seatId, root.anchoredPosition);
                float fade = t > 1.4f ? 1f - (t - 1.4f) / 0.4f : 1f;
                foreach (var im in root.GetComponentsInChildren<Image>()) { var c = im.color; c.a = Mathf.Clamp01(fade); im.color = c; }
                yield return null;
            }
            Destroy(root.gameObject);
        }

        // ------------------------------------------------------------------ the doctor's shield

        private IEnumerator Shield(string seatId)
        {
            Vector2 at = SeatAt(seatId, Vector2.zero);
            float s = ScaleOfSeat(seatId);
            float size = 125f * s * 1.25f;
            var teal = new Color32(0x7F, 0xE0, 0xC0, 255);

            // the bullet flies in from the left...
            var bullet = Piece("Bullet", at + new Vector2(-size * 1.6f, size * 0.45f), new Vector2(size * 0.26f, size * 0.08f), null, new Color32(0xE6, 0xBF, 0x55, 255));
            bullet.localRotation = Quaternion.Euler(0, 0, 14);
            Vector2 from = bullet.anchoredPosition, hit = at + new Vector2(-size * 0.08f, size * 0.02f);
            for (float t = 0; t < 0.32f; t += Dt) { bullet.anchoredPosition = Vector2.Lerp(from, hit, t / 0.32f); yield return null; }

            // ...and meets the shield: flash, shake, a ring that spreads, sparks, the shield itself
            Run(Burst());
            var ring = Piece("Ripple", at, new Vector2(size * 0.5f, size * 0.5f), Disc(size * 0.5f, Color.clear, teal, 5f), Color.white);
            var shield = Piece("Shield", at, new Vector2(size * 0.95f, size * 1.05f), KitSvg.Icon("M12 2.5l8 3v6c0 4.8-3.2 8.2-8 10-4.8-1.8-8-5.2-8-10v-6z|M12 8.5v7M8.5 12h7", 1.8f, 160), new Color32(0xC9, 0xF7, 0xE6, 255));
            var sparks = new RectTransform[7]; var dirs = new[] { new Vector2(-40, -50), new Vector2(40, -48), new Vector2(-52, 10), new Vector2(50, 14), new Vector2(0, -62), new Vector2(-24, 52), new Vector2(26, 50) };
            for (int i = 0; i < sparks.Length; i++) sparks[i] = Piece("Spark", at, new Vector2(14f, 14f), Disc(14f, new Color32(0xFF, 0xF3, 0xB0, 255), Color.clear, 0f), Color.white);
            Vector2 bFrom = bullet.anchoredPosition;
            for (float t = 0; t < 2.2f; t += Dt)
            {
                if (view.SeatOf(seatId) != null) at = SeatAt(seatId, at);
                float pop = t < 0.3f ? Mathf.Lerp(0.3f, 1.18f, t / 0.3f) : t < 0.57f ? Mathf.Lerp(1.18f, 1f, (t - 0.3f) / 0.27f) : t > 1.7f ? Mathf.Lerp(1f, 1.25f, (t - 1.7f) / 0.5f) : 1f;
                float alpha = t < 0.3f ? t / 0.3f : t > 1.7f ? 1f - (t - 1.7f) / 0.5f : 1f;
                shield.anchoredPosition = at; shield.localScale = Vector3.one * pop; shield.GetComponent<Image>().color = new Color(0.79f, 0.97f, 0.9f, Mathf.Clamp01(alpha));
                float rp = Mathf.Clamp01(t / 1f); ring.anchoredPosition = at; ring.localScale = Vector3.one * Mathf.Lerp(0.4f, 3.2f, rp); ring.GetComponent<Image>().color = new Color(1, 1, 1, 0.9f * (1f - rp));
                float sp = Mathf.Clamp01(t / 0.6f);
                for (int i = 0; i < sparks.Length; i++) { sparks[i].anchoredPosition = at + dirs[i] * (s * 1.5f) * sp; sparks[i].GetComponent<Image>().color = new Color(1, 1, 1, 1f - sp); }
                // the bullet bounces back and away
                float bp = Mathf.Clamp01(t / 0.9f);
                bullet.anchoredPosition = Vector2.Lerp(hit, hit + new Vector2(-size * 0.6f, -size * 0.45f), bp) + new Vector2(0, Mathf.Sin(bp * Mathf.PI) * size * 0.35f);
                bullet.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(14f, -420f, bp));
                bullet.GetComponent<Image>().color = new Color(0.9f, 0.75f, 0.33f, 1f - bp);
                yield return null;
            }
            Destroy(bullet.gameObject); Destroy(ring.gameObject); Destroy(shield.gameObject);
            foreach (var sp in sparks) if (sp != null) Destroy(sp.gameObject);
        }

        // ------------------------------------------------------------------ the stamp

        private IEnumerator Stamp(string seatId, float hold)
        {
            var th = KitTheme.Current;
            Vector2 at = SeatAt(seatId, Vector2.zero);
            float s = ScaleOfSeat(seatId);
            var crim = new Color32(0xE0, 0x58, 0x5F, 255);
            var plate = Piece("Stamp", at + new Vector2(0, 125f * s * 1.4f), new Vector2(420f, 190f), KitProc.Paint("fxstamp", new KitProc.Spec { Radius = 14, Top = new Color32(0, 0, 0, 170), Bottom = new Color32(0, 0, 0, 170), Edge = crim, EdgePx = 8, Sliced = true }), Color.white);
            plate.GetComponent<Image>().type = Image.Type.Sliced;
            var tgo = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            tgo.transform.SetParent(plate, false);
            var tr = (RectTransform)tgo.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            var tm = tgo.GetComponent<TMPro.TextMeshProUGUI>();
            if (th != null && th.fontLalezar != null) tm.font = th.fontLalezar;
            tm.fontSize = 120; tm.color = crim; tm.alignment = TMPro.TextAlignmentOptions.Midline; tm.raycastTarget = false;
            tgo.AddComponent<ArabicText>().Text = "طُرد";
            var cg = plate.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0f;
            plate.localRotation = Quaternion.Euler(0, 0, 12f);
            for (float t = 0; t < 0.35f; t += Dt)
            {
                float k = t / 0.35f; plate.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, 1f - (1f - k) * (1f - k) * (1f - k)); cg.alpha = Mathf.Clamp01(t / 0.25f);
                if (view.SeatOf(seatId) != null) plate.anchoredPosition = SeatAt(seatId, at) + new Vector2(0, 125f * ScaleOfSeat(seatId) * 1.4f);
                yield return null;
            }
            plate.localScale = Vector3.one; cg.alpha = 1f;
            yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, hold - 1.2f));
            for (float t = 0; t < 0.4f; t += Dt) { cg.alpha = 1f - t / 0.4f; yield return null; }
            Destroy(plate.gameObject);
        }
    }
}
