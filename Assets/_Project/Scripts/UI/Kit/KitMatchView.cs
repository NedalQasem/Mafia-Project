using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    public enum SeatLook { Normal, Pick, PickDoc, PickSher, Talk, Dead, Sleep }
    public enum SeatTag { None, Mafia, Count, Check }

    /// <summary>One player at the table, as the table screens draw him.</summary>
    public sealed class SeatSpec
    {
        public string Id, Name;
        public bool Me;
        public SeatLook Look;
        public SeatTag Tag;
        public string TagText;      // for Count / Check
        public bool Dim;            // nametag faded (asleep)
    }

    /// <summary>
    /// Drives the match screens that the editor builder made from Docs/mockups/match.html. The frames are exact copies of the mockup, so
    /// this only swaps what changes: it shows one frame, changes the text of numbered parts, puts a tap area on buttons and lays the
    /// seats out on the table for the real number of players. The editor builder fills the frames, names and sprite bank.
    /// </summary>
    public sealed class KitMatchView : MonoBehaviour
    {
        // the table ellipse of the mockup (canvas units, relative to the stage)
        private const float CenterX = 540f, CenterY = -609.23f, RadiusX = 464.4f, RadiusY = 548.2f;
        private const float CssToCanvas = 1080f / 390f;

        [SerializeField] private GameObject[] frames;
        [SerializeField] private string[] names;
        [SerializeField] private Sprite[] bank;
        [SerializeField] private TMP_FontAsset headFont;

        private readonly Dictionary<string, int> byName = new Dictionary<string, int>();
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, Transform[]> nodes = new Dictionary<string, Transform[]>();
        private GameObject seatProto;
        private readonly Dictionary<SeatTag, GameObject> tagProtos = new Dictionary<SeatTag, GameObject>();
        private bool ready;
        private KitFitGroup fit;

        /// <summary>Keeps the bottom of the screen free for the chat dock (0 gives the whole screen back to the frames).</summary>
        public void ReserveBottom(float units) { Init(); if (fit != null) fit.SetReserve(units); }

        public string Current { get; private set; }
        public IReadOnlyList<string> FrameNames => names;

        private void Awake() { Init(); }

        private void Init()
        {
            if (ready) return;
            ready = true;
            var prev = GetComponent<KitMatchPreview>();
            if (prev != null) prev.enabled = false;
            for (int i = 0; i < names.Length; i++) byName[names[i]] = i;
            if (bank != null) foreach (var s in bank) if (s != null) sprites[s.name] = s;
            FindProtos();
            // the frames are drawn for the tall phone: on a shorter screen they shrink together instead of the table running under the bottom panel
            if (frames != null && frames.Length > 0 && frames[0] != null && frames[0].transform.parent is RectTransform frameParent)
            {
                var rts = new List<RectTransform>();
                foreach (var f in frames) if (f != null) rts.Add((RectTransform)f.transform);
                fit = KitFitGroup.Wrap(frameParent, 2188f, rts.ToArray());
            }
        }

        public bool Has(string frame) { Init(); return byName.ContainsKey(frame); }
        public Sprite Sprite(string name) { Init(); return name != null && sprites.TryGetValue(name, out var s) ? s : null; }
        public RectTransform Frame(string frame) { Init(); return byName.TryGetValue(frame, out var i) ? (RectTransform)frames[i].transform : null; }

        public void Show(string frame)
        {
            Init();
            if (!byName.TryGetValue(frame, out var idx)) { Debug.LogWarning("[Match] no frame " + frame); return; }
            Current = frame;
            for (int k = 0; k < frames.Length; k++) if (frames[k] != null) frames[k].SetActive(k == idx);
        }

        // ---------------------------------------------------------------- parts by number

        public Transform Node(string frame, int index)
        {
            Init();
            if (!byName.TryGetValue(frame, out var idx)) return null;
            if (!nodes.TryGetValue(frame, out var list))
            {
                var ids = frames[idx].GetComponentsInChildren<KitNodeId>(true);
                int max = 0; foreach (var id in ids) if (id.Index > max) max = id.Index;
                list = new Transform[max + 1];
                foreach (var id in ids) list[id.Index] = id.transform;
                nodes[frame] = list;
            }
            return index >= 0 && index < list.Length ? list[index] : null;
        }

        public void SetText(string frame, int index, string text)
        {
            var t = Node(frame, index);
            var a = t != null ? t.GetComponent<ArabicText>() : null;
            if (a != null) a.Text = text ?? "";
        }

        public string GetText(string frame, int index)
        {
            var t = Node(frame, index);
            var a = t != null ? t.GetComponent<ArabicText>() : null;
            return a != null ? a.Text : null;
        }

        public void SetActive(string frame, int index, bool on)
        {
            var t = Node(frame, index);
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        public void SetTap(string frame, int index, Action clicked)
        {
            var t = Node(frame, index);
            if (t != null) KitTap.On(t, clicked);
        }

        /// <summary>Changes the picture of a box (a button face, a chip...) to another sprite of the bank, keeping its size.</summary>
        public void SetSprite(string frame, int index, string spriteName)
        {
            var t = Node(frame, index);
            var v = t != null ? t.Find("V") : null;
            var im = v != null ? v.GetComponent<Image>() : (t != null ? t.GetComponent<Image>() : null);   // icons carry their picture on the part itself
            var s = Sprite(spriteName);
            if (im != null && s != null) im.sprite = s;
        }

        public void SetColor(string frame, int index, Color c)
        {
            var t = Node(frame, index);
            if (t == null) return;
            var tmp = t.GetComponent<TMP_Text>();
            if (tmp != null) { tmp.color = c; return; }
            var v = t.Find("V");
            var im = v != null ? v.GetComponent<Image>() : null;
            if (im != null) im.color = c;
        }

        /// <summary>
        /// The mockup prints some lines as pictures (calligraphy). When the words change at run time (a player's name), the picture is
        /// hidden and the same place gets live text in the heading font.
        /// </summary>
        public void ReplaceTextImage(string frame, int index, string text, float cssFontSize)
        {
            var img = Node(frame, index);
            if (img == null) return;
            var holder = img.parent;
            var dyn = holder.Find("Dyn");
            if (dyn == null)
            {
                var go = new GameObject("Dyn", typeof(RectTransform));
                dyn = go.transform; dyn.SetParent(holder, false);
                var rt = (RectTransform)dyn; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                var tmp = go.AddComponent<TextMeshProUGUI>();
                if (headFont != null) tmp.font = headFont;
                tmp.fontSize = cssFontSize * CssToCanvas;
                tmp.alignment = TextAlignmentOptions.Midline;
                tmp.raycastTarget = false;
                tmp.overflowMode = TextOverflowModes.Overflow;
                var v = img.Find("V");
                var vi = v != null ? v.GetComponent<Image>() : null;
                tmp.color = vi != null ? vi.color : Color.white;
                var ar = go.AddComponent<ArabicText>(); ar.ArabicDigits = false;
            }
            img.gameObject.SetActive(false);
            dyn.GetComponent<ArabicText>().Text = text ?? "";
        }

        /// <summary>
        /// Resizes little text plates (vote counts) to their words and lines them up from the right edge of their row, as the mockup's flex row does.
        /// <paramref name="textNodes"/> are the text parts inside the plates, in the order they should appear from the right.
        /// </summary>
        public void FitChips(string frame, int[] textNodes, float rightEdgeCss, float padCss = 9f, float gapCss = 6f)
        {
            float right = rightEdgeCss * CssToCanvas, pad = padCss * CssToCanvas, gap = gapCss * CssToCanvas;
            foreach (int i in textNodes)
            {
                var label = Node(frame, i);
                if (label == null || label.parent == null || !label.parent.gameObject.activeSelf) continue;
                var tmp = label.GetComponent<TMP_Text>(); if (tmp == null) continue;
                tmp.ForceMeshUpdate();
                float w = tmp.preferredWidth + pad * 2f;
                var chip = (RectTransform)label.parent; var lrt = (RectTransform)label;
                chip.sizeDelta = new Vector2(w, chip.sizeDelta.y);
                chip.anchoredPosition = new Vector2(right - w / 2f, chip.anchoredPosition.y);
                lrt.sizeDelta = new Vector2(w, lrt.sizeDelta.y);
                lrt.anchoredPosition = new Vector2(w / 2f, lrt.anchoredPosition.y);
                right -= w + gap;
            }
        }

        private readonly HashSet<string> scrollable = new HashSet<string>();

        /// <summary>
        /// Lets a frame whose page grew taller than the screen be dragged up and down: <paramref name="content"/> (the page part) gets the height
        /// (mockup pixels) with its top fixed, and the frame itself scrolls it. Calling it again keeps the scroll position.
        /// </summary>
        public void MakeScrollable(string frame, RectTransform content, float heightCss)
        {
            Init();
            if (content == null || !byName.TryGetValue(frame, out var idx)) return;
            float h = heightCss * CssToCanvas;
            float oldH = content.sizeDelta.y;
            float offset = content.anchoredPosition.y + oldH / 2f;               // how far it was dragged up
            content.sizeDelta = new Vector2(content.sizeDelta.x, h);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, -h / 2f + Mathf.Clamp(offset, 0f, Mathf.Max(0f, h - oldH)));
            if (!scrollable.Add(frame)) return;
            var root = (RectTransform)frames[idx].transform;
            var hit = root.GetComponent<Image>();
            if (hit == null) { hit = root.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); }
            hit.raycastTarget = true;
            var sr = root.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.viewport = root; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.inertia = true; sr.scrollSensitivity = 40f;
        }

        // ---------------------------------------------------------------- the table

        private void FindProtos()
        {
            if (frames == null) return;
            foreach (var f in frames)
            {
                if (f == null) continue;
                foreach (var tr in f.GetComponentsInChildren<Transform>(true))
                {
                    if (seatProto == null && tr.name == "f_seat" && tr.parent != null && tr.parent.name == "f_stage") seatProto = tr.gameObject;
                    if (tr.name == "f_tag.mafia" && !tagProtos.ContainsKey(SeatTag.Mafia)) tagProtos[SeatTag.Mafia] = tr.gameObject;
                    else if (tr.name == "f_tag." && !tagProtos.ContainsKey(SeatTag.Count)) tagProtos[SeatTag.Count] = tr.gameObject;
                    else if (tr.name == "f_tag.gold" && !tagProtos.ContainsKey(SeatTag.Check)) tagProtos[SeatTag.Check] = tr.gameObject;
                }
            }
            if (seatProto != null)
            {   // keep untouched copies out of the frames, so replacing seats never loses the originals
                seatProto = Instantiate(seatProto, transform);
                seatProto.name = "SeatProto"; seatProto.SetActive(false);
                foreach (var tag in seatProto.GetComponentsInChildren<Transform>(true))
                    if (tag.name.StartsWith("f_tag")) Destroy(tag.gameObject);
            }
            var keys = new List<SeatTag>(tagProtos.Keys);
            foreach (var k in keys)
            {
                var c = Instantiate(tagProtos[k], transform);
                c.name = "TagProto_" + k; c.SetActive(false); tagProtos[k] = c;
            }
        }

        private static string AvatarSprite(SeatLook look, bool me)
        {
            switch (look)
            {
                case SeatLook.Pick: return "av_pick";
                case SeatLook.PickDoc: return "av_pick_doc";
                case SeatLook.PickSher: return "av_pick_sher";
                case SeatLook.Talk: return "av_talk";
                case SeatLook.Dead: return me ? "av_me_dead" : "av_dead";
                case SeatLook.Sleep: return me ? "av_me_sleep" : "av_sleep";
                default: return me ? "av_me" : "av";
            }
        }

        /// <summary>
        /// Replaces the seats of a frame with one seat per player on the table ellipse. The first spec marked Me sits at the bottom.
        /// <paramref name="tapped"/> gets the player id when a seat is pressed.
        /// </summary>
        public void SetSeats(string frame, IList<SeatSpec> specs, Action<string> tapped)
        {
            Init();
            if (!byName.TryGetValue(frame, out var idx) || seatProto == null) return;
            var stage = frames[idx].transform.Find("f_stage");
            if (stage == null) return;
            for (int i = stage.childCount - 1; i >= 0; i--)
            {
                var c = stage.GetChild(i);
                if (c.name.StartsWith("f_seat") || c.name == "Seat") Destroy(c.gameObject);
            }
            int n = specs.Count; if (n == 0) return;
            int meAt = 0; for (int i = 0; i < n; i++) if (specs[i].Me) { meAt = i; break; }
            for (int k = 0; k < n; k++)
            {
                var spec = specs[(meAt + k) % n];
                var seat = Instantiate(seatProto, stage);
                seat.name = "Seat"; seat.SetActive(true);
                var rt = (RectTransform)seat.transform;
                float a = k * Mathf.PI * 2f / n;
                rt.anchoredPosition = new Vector2(CenterX - RadiusX * Mathf.Sin(a), CenterY - RadiusY * Mathf.Cos(a));
                Fill(seat.transform, spec);
                string id = spec.Id;
                if (tapped != null) KitTap.On(seat.transform, () => tapped(id)); else KitTap.On(seat.transform, null);
            }
            // the table's own picture stays behind the seats
            var felt = stage.Find("f_felt"); if (felt != null) felt.SetAsFirstSibling();
        }

        private void Fill(Transform seat, SeatSpec spec)
        {
            var av = seat.Find("f_av");
            var v = av != null ? av.Find("V") : null;
            var im = v != null ? v.GetComponent<Image>() : null;
            var sp = Sprite(AvatarSprite(spec.Look, spec.Me));
            if (im != null && sp != null) im.sprite = sp;

            if (spec.Tag != SeatTag.None && tagProtos.TryGetValue(spec.Tag, out var proto) && av != null)
            {
                var tag = Instantiate(proto, av);
                tag.name = "Tag"; tag.SetActive(true);
                var t = tag.GetComponentInChildren<ArabicText>(true);
                if (t != null && spec.Tag != SeatTag.Mafia) t.Text = spec.TagText ?? "";
            }

            var nm = seat.Find("f_nm");
            if (nm == null) return;
            var label = nm.Find("f_text");
            var at = label != null ? label.GetComponent<ArabicText>() : null;
            var tmp = label != null ? label.GetComponent<TMP_Text>() : null;
            if (at == null || tmp == null) return;
            string shown = spec.Me ? "أنا (" + spec.Name + ")" : spec.Name;
            at.Text = shown;
            tmp.ForceMeshUpdate();
            float w = Mathf.Clamp(tmp.preferredWidth + 8f * CssToCanvas, 24f * CssToCanvas, 78f * CssToCanvas);
            var nrt = (RectTransform)nm; var lrt = (RectTransform)label;
            nrt.sizeDelta = new Vector2(w, nrt.sizeDelta.y);
            lrt.sizeDelta = new Vector2(w + 4f * CssToCanvas, lrt.sizeDelta.y);
            lrt.anchoredPosition = new Vector2(w / 2f, lrt.anchoredPosition.y);
            var cg = nm.GetComponent<CanvasGroup>();
            if (spec.Dim) { if (cg == null) cg = nm.gameObject.AddComponent<CanvasGroup>(); cg.alpha = Mathf.Pow(0.6f, 2.2f); }
            else if (cg != null) cg.alpha = 1f;
        }
    }
}
