using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// The pinned card of an official room in the rooms list (Docs/mockups/office-room.html, section 1): dark green felt in a brass frame, the red "official" seal,
    /// the "pinned" tag, the room's name and line, the number inside, a fill bar, who from the team is there and the way in. Built in code at the width it is given
    /// (the mockup's CSS pixels times <c>K</c>); its height follows the hint's real text, so a short or a very long line keeps the same shape.
    /// </summary>
    public sealed class KitOfficialCard : MonoBehaviour
    {
        private static readonly Color Gold = new Color32(0xF5, 0xE2, 0xA8, 255), Brass = new Color32(0xB9, 0x90, 0x48, 255), Dark = new Color32(0x2A, 0x1A, 0x08, 255);

        private Image glow;
        private float phase;
        private bool pulse;

        public RoomInfo Info { get; private set; }

        /// <summary>Makes the card inside <paramref name="parent"/> (a list that places its children by their own size) and returns it.</summary>
        public static KitOfficialCard Create(RectTransform parent, RoomInfo info, float width, Action<RoomInfo> onJoin)
        {
            var go = new GameObject("OfficialCard", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<KitOfficialCard>();
            c.Build(info, width, onJoin);
            return c;
        }

        private void Update()
        {
            if (!pulse || glow == null) return;
            float k = 0.5f + 0.5f * Mathf.Cos((Time.unscaledTime + phase) / 1.8f * Mathf.PI * 2f);
            var col = glow.color; col.a = k; glow.color = col;
        }

        private void Build(RoomInfo info, float width, Action<RoomInfo> onJoin)
        {
            Info = info;
            var th = KitTheme.Current;
            float k = width / 362f;                       // the mockup's card is 362 CSS pixels wide
            bool closed = info.closed;
            bool full = closed || info.count >= info.capacity;
            bool hasAdmin = info.adminsOnline > 0;
            var root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f); root.pivot = new Vector2(0.5f, 1f);

            float padT = 20 * k, padS = 16 * k, padB = 14 * k, gap = 11 * k;
            float avatar = 46 * k, rowH = 46 * k, barH = 8 * k, btnH = 40 * k, chipH = 21 * k;
            float inner = width - 2 * padS;

            // ---- the hint: its height is its text's
            string lead = closed || !hasAdmin ? "" : "أول مرة؟";
            string body = closed ? (info.closedByAdmin ? "الغرفة مسكّرة مؤقتاً من الإدارة، وبتفتح قريباً." : "الغرفة بتفتح بعد لحظات، جرّب كمان شوي.") : hasAdmin ? "ادخل واضغط «ارفع إيدك» ليعطيك الأدمن المايك." : "الأدمن مش موجود هلأ. تقدر تدخل وتسمع، والأدمن بيرجع بوقت الدوام.";
            var hintBox = Rect(root, "Hint", 0, 0);
            float hintPadX = 10 * k, hintPadY = 6 * k, leadW = 0f;
            var leadLabel = lead.Length > 0 ? Label(hintBox, "Lead", lead, th.fontBody, 11.5f * k, Gold, TextAlignmentOptions.TopRight, true) : null;
            if (leadLabel != null) { leadLabel.GetComponent<TMP_Text>().ForceMeshUpdate(); leadW = leadLabel.GetComponent<TMP_Text>().preferredWidth + 6 * k; }
            var bodyLabel = Label(hintBox, "Body", body, th.fontBody, 11.5f * k, new Color32(0xC6, 0xD6, 0xCC, 255), TextAlignmentOptions.TopRight, false);
            bodyLabel.Wrap = true;
            var bodyRt = (RectTransform)bodyLabel.transform;
            float bodyW = inner - 2 * hintPadX - leadW;
            bodyRt.anchorMin = bodyRt.anchorMax = new Vector2(1f, 1f); bodyRt.pivot = new Vector2(1f, 1f); bodyRt.sizeDelta = new Vector2(bodyW, 10);
            bodyLabel.Text = ""; bodyLabel.Text = body;
            var bodyTmp = bodyLabel.GetComponent<TMP_Text>();
            bodyTmp.ForceMeshUpdate();
            float bodyH = Mathf.Max(bodyTmp.preferredHeight, 11.5f * k * 1.6f);
            bodyRt.sizeDelta = new Vector2(bodyW, bodyH);
            bodyRt.anchoredPosition = new Vector2(-hintPadX - leadW, -hintPadY);
            if (leadLabel != null)
            {
                var lr = (RectTransform)leadLabel.transform; lr.anchorMin = lr.anchorMax = new Vector2(1f, 1f); lr.pivot = new Vector2(1f, 1f);
                lr.sizeDelta = new Vector2(leadW, bodyH); lr.anchoredPosition = new Vector2(-hintPadX, -hintPadY);
            }
            float hintH = bodyH + 2 * hintPadY;

            float height = padT + rowH + gap + barH + gap + btnH + gap + hintH + padB;
            root.sizeDelta = new Vector2(width, height);

            // ---- the felt, the frame and its shadow
            float margin = 64f;
            var bgSpec = new KitProc.Spec
            {
                W = width, H = height, Radius = 12 * k, Margin = margin,
                Top = new Color32(0x25, 0x5D, 0x4B, 255), Mid = new Color32(0x1A, 0x3F, 0x33, 255), Bottom = new Color32(0x0F, 0x26, 0x1F, 255), MidAt = 0.55f,
                Glow = new Color32(0x3D, 0x7D, 0x6A, 0x70), GlowAt = new Vector2(0.7f, 0f), GlowReach = 0.6f,
                TopLight = new Color(1, 1, 1, 0.14f), TopLightPx = 1f * k,
                Dash = new Color32(0xE6, 0xC9, 0xA0, 0x59), DashInset = 6 * k, DashPx = 1.5f * k, DashLen = 5 * k, DashGap = 4 * k, DashRadius = 8 * k,
                Shadow = new Color32(0, 0, 0, 0xCC), ShadowDy = 14 * k, ShadowBlur = 26 * k,
            };
            bgSpec.Rings.Add(new System.Collections.Generic.KeyValuePair<Color, float>(Color.black, 2 * k));
            bgSpec.Rings.Add(new System.Collections.Generic.KeyValuePair<Color, float>(Brass, 2 * k));
            bgSpec.Rings.Add(new System.Collections.Generic.KeyValuePair<Color, float>(new Color32(0x3A, 0x2A, 0x0C, 255), 1 * k));
            var bg = Img(root, "Felt", KitProc.Paint("off-bg" + Mathf.RoundToInt(width) + "x" + Mathf.RoundToInt(height), bgSpec), Color.white);
            Stretch(bg.rectTransform, -margin);
            if (full) bg.color = new Color(0.62f, 0.66f, 0.64f, 1f);

            if (info.pulse && !full)
            {
                var gs = new KitProc.Spec { W = width, H = height, Radius = 12 * k, Margin = margin };
                gs.Rings.Add(new System.Collections.Generic.KeyValuePair<Color, float>(Color.clear, 5 * k));
                gs.Rings.Add(new System.Collections.Generic.KeyValuePair<Color, float>(new Color32(0xB9, 0x90, 0x48, 0x40), 4 * k));
                glow = Img(root, "Glow", KitProc.Paint("off-glow" + Mathf.RoundToInt(width) + "x" + Mathf.RoundToInt(height), gs), Color.white);
                Stretch(glow.rectTransform, -margin);
                glow.transform.SetAsFirstSibling();
                pulse = true; phase = UnityEngine.Random.value * 2f;
            }

            // ---- the row: avatar (right), name and line, the number (left)
            float y = -padT;
            var av = Disc(root, "Avatar", avatar, KitProc.Paint("off-av" + Mathf.RoundToInt(avatar), AvatarSpec(avatar, k)), -padS - avatar * 0.5f, y - rowH * 0.5f, true);
            var sil = Img(av, "Fed", th.fedSil, new Color32(0xD9, 0xC4, 0x8A, 255)); Center(sil.rectTransform, 33 * k, 33 * k);

            float cntW = 46 * k, cntH = 44 * k;
            var cnt = Img(root, "Count", KitProc.Paint("off-cnt" + Mathf.RoundToInt(cntW) + "x" + Mathf.RoundToInt(cntH), PlateSpec(cntW, cntH, 6 * k, 0.53f, 0.09f)), Color.white);
            PlaceLeftTop(cnt.rectTransform, padS, y - (rowH - cntH) * 0.5f, cntW, cntH);
            var num = Label(cnt.transform, "Num", CoinPill.Format(info.count), th.fontLalezar, 20 * k, th.ivory, TextAlignmentOptions.Midline, false);
            Fill(num, 0, 0.32f, 1, 1);
            var of = Label(cnt.transform, "Of", "من " + CoinPill.Format(info.capacity), th.fontBody, 9.3f * k, th.ivoryDim, TextAlignmentOptions.Midline, false);
            Fill(of, 0, 0f, 1, 0.38f);

            float nameRight = padS + avatar + gap, nameLeft = padS + cntW + gap;
            var nameBox = Rect(root, "Name", 0, 0);
            nameBox.anchorMin = nameBox.anchorMax = new Vector2(0f, 1f); nameBox.pivot = new Vector2(0f, 1f);
            nameBox.sizeDelta = new Vector2(width - nameRight - nameLeft, rowH); nameBox.anchoredPosition = new Vector2(nameLeft, y);
            var nm = Label(nameBox, "Title", "", th.fontCalligraphy, 21 * k, Gold, TextAlignmentOptions.BottomRight, false);
            Fill(nm, 0, 0.42f, 1, 1);
            KitText.Fit(nm, info.name, 28, 21 * k * 0.55f);
            var nt = nm.GetComponent<TMP_Text>(); nt.enableVertexGradient = true; nt.colorGradient = new VertexGradient(Gold, Gold, Brass, Brass);
            string subText = (string.IsNullOrEmpty(info.sub) ? "" : info.sub);
            var sub = Label(nameBox, "Sub", "", th.fontBody, 11.8f * k, new Color32(0xB6, 0xC9, 0xBD, 255), TextAlignmentOptions.TopRight, false);
            Fill(sub, 0, 0, 1, 0.42f);
            KitText.Fit(sub, subText, 60, 11.8f * k * 0.6f);
            y -= rowH + gap;

            // ---- the fill bar
            var track = Img(root, "Bar", KitProc.Paint("off-bar" + Mathf.RoundToInt(inner), BarSpec(inner, barH, k)), Color.white);
            PlaceLeftTop(track.rectTransform, padS, y, inner, barH);
            float frac = info.capacity > 0 ? Mathf.Clamp01((float)info.count / info.capacity) : 0f;
            if (frac > 0f)
            {
                float fw = Mathf.Max(barH, inner * frac);
                var fill = Img(track.transform, "Fill", KitProc.Paint("off-fill" + Mathf.RoundToInt(fw), FillSpec(fw, barH)), Color.white);
                PlaceLeftTop(fill.rectTransform, 0, 0, fw, barH);
            }
            y -= barH + gap;

            // ---- chips (right) and the way in (left)
            float cx = -padS;
            string adm = closed ? "مغلقة مؤقتاً" : !hasAdmin ? "ما في أدمن هلأ" : info.adminsOnline == 1 ? "أدمن واحد حاضر" : CoinPill.Format(info.adminsOnline) + " أدمن حاضرين";
            cx = Chip(root, th, adm, y - (btnH - chipH) * 0.5f, cx, chipH, k, true, hasAdmin ? Gold : new Color32(0xE6, 0xB3, 0xB6, 255), hasAdmin ? Brass : new Color32(0x8A, 0x1C, 0x24, 255), hasAdmin ? new Color32(0x4F, 0xD4, 0x9B, 255) : new Color32(0xC0, 0x32, 0x3A, 255)) - 8 * k;
            Chip(root, th, "صوت فقط", y - (btnH - chipH) * 0.5f, cx, chipH, k, false, new Color32(0xCF, 0xC6, 0xB2, 255), new Color(1, 1, 1, 0.11f), Color.clear);

            float goW = 100 * k;
            var go = Img(root, "Go", KitProc.Paint("off-go" + Mathf.RoundToInt(100 * k) + (full ? "f" : "o"), GoSpec(100 * k, btnH, k, full)), Color.white);
            PlaceLeftTop(go.rectTransform, padS, y, goW, btnH);
            var gl = Label(go.transform, "Label", closed ? "قريباً" : full ? "ممتلئة" : "ادخل", th.fontBody, 15.2f * k, full ? new Color32(0x7D, 0x74, 0x6A, 255) : th.brassText, TextAlignmentOptions.Midline, true);
            Fill(gl, 0, 0, 1, 1);
            y -= btnH + gap;

            // ---- the hint
            var hintBg = Img(hintBox, "Bg", KitProc.Paint("off-hint" + Mathf.RoundToInt(inner) + "x" + Mathf.RoundToInt(hintH), PlateSpec(inner, hintH, 6 * k, 0.4f, 0f)), Color.white);
            hintBg.transform.SetAsFirstSibling();
            hintBg.rectTransform.anchorMin = hintBg.rectTransform.anchorMax = new Vector2(0f, 1f); hintBg.rectTransform.pivot = new Vector2(0f, 1f);
            hintBg.rectTransform.sizeDelta = new Vector2(inner, hintH); hintBg.rectTransform.anchoredPosition = Vector2.zero;
            hintBox.anchorMin = hintBox.anchorMax = new Vector2(0f, 1f); hintBox.pivot = new Vector2(0f, 1f);
            hintBox.sizeDelta = new Vector2(inner, hintH); hintBox.anchoredPosition = new Vector2(padS, y);
            hintBox.SetAsLastSibling();       // it was made first (its text sets the card height): it has to draw over the felt

            // ---- the seal and the pin tag sit over the frame
            var seal = Disc(root, "Seal", 46 * k, KitProc.Paint("off-seal" + Mathf.RoundToInt(46 * k), SealSpec(46 * k, k)), padS + 23 * k, 16 * k - 23 * k, false);
            seal.anchorMin = seal.anchorMax = new Vector2(0f, 1f); seal.localRotation = Quaternion.Euler(0, 0, 10f);
            var sl = Label(seal, "T", "رسمية", th.fontLalezar, 13.6f * k, new Color32(0xF3, 0xC9, 0xCC, 255), TextAlignmentOptions.Midline, false);
            Fill(sl, 0, 0, 1, 1);

            string pinText = "مثبتة";
            var pinLabelTmp = Label(root, "PinText", pinText, th.fontLalezar, 12.8f * k, Dark, TextAlignmentOptions.Midline, false);
            pinLabelTmp.GetComponent<TMP_Text>().ForceMeshUpdate();
            float pinW = pinLabelTmp.GetComponent<TMP_Text>().preferredWidth + 32 * k, pinH = 24 * k;
            var pinBg = Img(root, "Pin", KitProc.Paint("off-pin" + Mathf.RoundToInt(pinW) + "x" + Mathf.RoundToInt(pinH), PinPlateSpec(pinW, pinH, k)), Color.white);
            pinBg.rectTransform.anchorMin = pinBg.rectTransform.anchorMax = new Vector2(1f, 1f); pinBg.rectTransform.pivot = new Vector2(1f, 0.5f);
            pinBg.rectTransform.sizeDelta = new Vector2(pinW, pinH); pinBg.rectTransform.anchoredPosition = new Vector2(-18 * k, 13 * k - pinH * 0.5f);
            pinLabelTmp.transform.SetParent(pinBg.transform, false);
            var plr = (RectTransform)pinLabelTmp.transform; plr.anchorMin = Vector2.zero; plr.anchorMax = Vector2.one; plr.offsetMin = new Vector2(10 * k, 0); plr.offsetMax = new Vector2(-(10 * k + 14 * k), 0);
            var pi = Img(pinBg.transform, "Icon", KitProc.Pin(Dark), Color.white);
            pi.rectTransform.anchorMin = pi.rectTransform.anchorMax = new Vector2(1f, 0.5f); pi.rectTransform.pivot = new Vector2(1f, 0.5f);
            pi.rectTransform.sizeDelta = new Vector2(12 * k, 12 * k); pi.rectTransform.anchoredPosition = new Vector2(-10 * k, 0);

            // ---- a full room: the red stamp over it
            if (full && !closed)
            {
                var st = Label(root, "Stamp", "ممتلئة", th.fontLalezar, 25.6f * k, new Color32(0xE0, 0x52, 0x5A, 255), TextAlignmentOptions.Midline, false);
                st.GetComponent<TMP_Text>().ForceMeshUpdate();
                float sw = st.GetComponent<TMP_Text>().preferredWidth + 32 * k, sh = 38 * k;
                var sbg = Img(root, "StampFrame", KitProc.Paint("off-stamp" + Mathf.RoundToInt(sw) + "x" + Mathf.RoundToInt(sh), StampSpec(sw, sh, k)), Color.white);
                sbg.rectTransform.anchorMin = sbg.rectTransform.anchorMax = new Vector2(0.5f, 0.52f); sbg.rectTransform.sizeDelta = new Vector2(sw, sh); sbg.rectTransform.anchoredPosition = Vector2.zero;
                sbg.rectTransform.localRotation = Quaternion.Euler(0, 0, 8f);
                st.transform.SetParent(sbg.transform, false); Fill(st, 0, 0, 1, 1);
            }

            // tapping anywhere on the card goes in (unless it is full)
            if (!full)
            {
                var hit = Img(root, "Hit", null, new Color(0, 0, 0, 0));
                Stretch(hit.rectTransform, 0);
                hit.transform.SetAsLastSibling();
                KitTap.On(hit.transform, () => onJoin?.Invoke(info));
            }
        }

        // ------------------------------------------------------------------ pieces

        private static float Chip(RectTransform parent, KitTheme th, string text, float yTop, float xRight, float h, float k, bool dot, Color textColor, Color edge, Color dotColor)
        {
            var label = Label(parent, "ChipText", text, th.fontBody, 11.5f * k, textColor, TextAlignmentOptions.Midline, true);
            var tmp = label.GetComponent<TMP_Text>(); tmp.ForceMeshUpdate();
            float dotW = dot ? 12 * k : 0f;
            float w = tmp.preferredWidth + 18 * k + dotW;
            var spec = PlateSpec(w, h, 3 * k, 0.6f, 0f);
            spec.Edge = edge; spec.EdgePx = 1f * k;
            var chip = Img(parent, "Chip", KitProc.Paint("off-chip" + Mathf.RoundToInt(w) + "x" + Mathf.RoundToInt(h) + ColorUtility.ToHtmlStringRGBA(edge), spec), Color.white);
            var r = chip.rectTransform; r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f); r.sizeDelta = new Vector2(w, h); r.anchoredPosition = new Vector2(xRight, yTop);
            label.transform.SetParent(chip.transform, false);
            var lr = (RectTransform)label.transform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(9 * k, 0); lr.offsetMax = new Vector2(-(9 * k + dotW), 0);
            if (dot)
            {
                var d = Img(chip.transform, "Dot", KitTheme.Current.dotWhite, dotColor);
                d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(1f, 0.5f); d.rectTransform.pivot = new Vector2(1f, 0.5f);
                d.rectTransform.sizeDelta = new Vector2(7 * k, 7 * k); d.rectTransform.anchoredPosition = new Vector2(-9 * k, 0);
            }
            return xRight - w;
        }

        private static KitProc.Spec GoSpec(float w, float h, float k, bool full)
        {
            var s = new KitProc.Spec { W = w, H = h, Radius = 6 * k, Margin = 4 * k, Shadow = full ? Color.black : new Color32(0x2A, 0x1C, 0x08, 255), ShadowDy = 3 * k, ShadowBlur = 1.2f };
            if (full) { s.Top = s.Bottom = new Color32(0x2A, 0x25, 0x23, 255); return s; }
            s.Top = new Color32(0xE8, 0xCC, 0x8C, 255); s.Mid = new Color32(0xB9, 0x90, 0x48, 255); s.Bottom = new Color32(0x6E, 0x52, 0x24, 255); s.MidAt = 0.55f;
            s.TopLight = new Color(1, 1, 1, 0.6f); s.TopLightPx = 1f * k;
            return s;
        }

        private static KitProc.Spec PlateSpec(float w, float h, float radius, float blackAlpha, float edgeAlpha)
        {
            var s = new KitProc.Spec { W = w, H = h, Radius = radius, Top = new Color(0, 0, 0, KitProc.LinA(blackAlpha)), Bottom = new Color(0, 0, 0, KitProc.LinA(blackAlpha)) };
            if (edgeAlpha > 0f) { s.Edge = new Color(1, 1, 1, edgeAlpha); s.EdgePx = 1.2f; }
            return s;
        }

        private static KitProc.Spec BarSpec(float w, float h, float k)
        {
            var s = PlateSpec(w, h, 4 * k, 0.6f, 0.1f);
            return s;
        }

        private static KitProc.Spec FillSpec(float w, float h)
        {
            return new KitProc.Spec { W = w, H = h, Radius = h * 0.5f, Horizontal = true, Top = new Color32(0x2C, 0x8D, 0x73, 255), Bottom = new Color32(0x6F, 0xD4, 0xB3, 255) };
        }

        private static KitProc.Spec AvatarSpec(float size, float k)
        {
            var s = new KitProc.Spec { W = size, H = size, Radius = size * 0.5f, Margin = 4 * k, Radial = true, RadialAt = new Vector2(0.5f, 0.3f), RA = new Color32(0x2A, 0x26, 0x23, 255), RB = new Color32(0x0C, 0x0B, 0x0A, 255), RC = new Color32(0x0C, 0x0B, 0x0A, 255), RBAt = 1f, RadialReach = 0.7f };
            s.Rings.Add(new System.Collections.Generic.KeyValuePair<Color, float>(Color.black, 2 * k));
            s.Rings.Add(new System.Collections.Generic.KeyValuePair<Color, float>(Brass, 1 * k));
            return s;
        }

        private static KitProc.Spec SealSpec(float size, float k)
        {
            var s = new KitProc.Spec
            {
                W = size, H = size, Radius = size * 0.5f, Margin = 6 * k, Radial = true, RadialAt = new Vector2(0.35f, 0.3f),
                RA = new Color32(0xD8, 0x43, 0x4B, 255), RB = new Color32(0x8A, 0x1C, 0x24, 255), RC = new Color32(0x4A, 0x0B, 0x10, 255), RBAt = 0.6f, RadialReach = 0.85f,
                Dash = new Color32(0xF3, 0xC9, 0xCC, 0x66), DashInset = 4 * k, DashPx = 1.5f * k, DashLen = 4 * k, DashGap = 3.5f * k, DashRadius = size * 0.5f - 4 * k,
                Shadow = new Color32(0, 0, 0, 0xAA), ShadowDy = 3 * k, ShadowBlur = 6 * k,
            };
            s.Rings.Add(new System.Collections.Generic.KeyValuePair<Color, float>(Color.black, 2 * k));
            return s;
        }

        private static KitProc.Spec PinPlateSpec(float w, float h, float k)
        {
            var s = new KitProc.Spec { W = w, H = h, Radius = 5 * k, Margin = 6 * k, Top = new Color32(0xE8, 0xCC, 0x8C, 255), Bottom = new Color32(0x9A, 0x76, 0x32, 255), Shadow = new Color32(0, 0, 0, 0xAA), ShadowDy = 2 * k, ShadowBlur = 3 * k };
            return s;
        }

        private static KitProc.Spec StampSpec(float w, float h, float k)
        {
            var s = new KitProc.Spec { W = w, H = h, Radius = 4 * k, Margin = 2 * k, Top = new Color(0, 0, 0, KitProc.LinA(0.4f)), Bottom = new Color(0, 0, 0, KitProc.LinA(0.4f)), Edge = new Color32(0xE0, 0x52, 0x5A, 255), EdgePx = 3 * k };
            return s;
        }

        // ------------------------------------------------------------------ small helpers

        private static RectTransform Rect(Transform parent, string name, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform; r.SetParent(parent, false); r.sizeDelta = new Vector2(w, h);
            return r;
        }

        private static Image Img(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = sprite; i.color = color; i.raycastTarget = false;
            return i;
        }

        private static ArabicText Label(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false; t.overflowMode = TextOverflowModes.Overflow;
            if (bold) t.fontStyle = FontStyles.Bold;
            var a = go.AddComponent<ArabicText>(); a.ArabicDigits = true; a.Text = text;
            return a;
        }

        private static void Stretch(RectTransform r, float outset) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(outset, outset); r.offsetMax = new Vector2(-outset, -outset); }
        private static void Fill(ArabicText a, float x0, float y0, float x1, float y1) { var r = (RectTransform)a.transform; r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero; }
        private static void Center(RectTransform r, float w, float h) { r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(w, h); }
        private static void PlaceLeftTop(RectTransform r, float x, float y, float w, float h) { r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); }

        /// <summary>A round piece (avatar, seal) centred at (x from the card's right edge when <paramref name="fromRight"/>, y from the top).</summary>
        private static RectTransform Disc(RectTransform parent, string name, float size, Sprite sprite, float x, float y, bool fromRight)
        {
            var i = Img(parent, name, sprite, Color.white);
            var r = i.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(fromRight ? 1f : 0f, 1f); r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(size, size); r.anchoredPosition = new Vector2(x, y);
            return r;
        }
    }
}
