using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>
    /// Draws the mockups' stroke icons (the SVG path strings of Docs/mockups, 24x24, round caps and joins) into white sprites the UI tints,
    /// so an icon in Unity is the same drawing as in the HTML. Supports M L H V C S Q A Z (absolute and relative), circles and rounded rectangles.
    /// </summary>
    public static class KitSvg
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static readonly string Arrow = "M9 5l7 7-7 7";
        public static readonly string Mic = "R9 3 6 12 3|M5 11a7 7 0 0014 0M12 18v3";
        public static readonly string MicOff = "R9 3 6 12 3|M5 11a7 7 0 0014 0M12 18v3M4 4l16 16";
        public static readonly string Eject = "M15 4h4v16h-4M10 8l-4 4 4 4M6 12h10";
        public static readonly string Users = "O9 8 3.2|M3 20c0-3.5 2.7-5.5 6-5.5s6 2 6 5.5M16 5.2a3 3 0 010 5.6M18 14.8c1.8.7 3 2.3 3 5.2";
        public static readonly string Plus = "M12 5v14M5 12h14";
        public static readonly string Lock = "R6 11 12 9 2|M8.5 11V8a3.5 3.5 0 017 0v3";
        public static readonly string Volume = "M4 9v6h4l5 4V5L8 9zM16.5 8.5a5 5 0 010 7M19 6a8.5 8.5 0 010 12";
        public static readonly string VolumeOff = "M4 9v6h4l5 4V5L8 9zM17 9l5 6M22 9l-5 6";
        public static readonly string Grid = "R4 4 6.5 6.5 1.5|R13.5 4 6.5 6.5 1.5|R4 13.5 6.5 6.5 1.5|R13.5 13.5 6.5 6.5 1.5";
        public static readonly string Smile = "O12 12 9|M8 14.5a5 5 0 008 0M9 9.5h.01M15 9.5h.01";
        public static readonly string Hand = "M18 11V6a2 2 0 00-4 0v1M14 10V4a2 2 0 00-4 0v2M10 10.5V5a2 2 0 00-4 0v9M18 8a2 2 0 114 0v6a8 8 0 01-8 8h-2c-2.8 0-4.5-.86-5.99-2.34l-3.6-3.6a2 2 0 012.83-2.82L7 15";
        public static readonly string Send = "M4 12l16-8-6 16-3-7z";

        /// <summary>A white sprite of the icon: <paramref name="px"/> square, stroke width in 24-unit space.</summary>
        public static Sprite Icon(string spec, float stroke = 2f, int px = 96)
        {
            string key = spec + "|" + stroke + "|" + px;
            if (cache.TryGetValue(key, out var hit) && hit != null) return hit;
            var lines = new List<List<Vector2>>();
            foreach (var part in spec.Split('|'))
            {
                if (part.Length > 0 && part[0] == 'R') { var n = Nums(part.Substring(1)); lines.Add(RoundRect(n[0], n[1], n[2], n[3], n[4])); }
                else if (part.Length > 0 && part[0] == 'O') { var n = Nums(part.Substring(1)); lines.Add(Circle(n[0], n[1], n[2])); }
                else lines.AddRange(Path(part));
            }
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pix = new Color32[px * px];
            float scale = px / 24f, half = stroke * 0.5f;
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                {
                    var p = new Vector2((x + 0.5f) / scale, (px - y - 0.5f) / scale);
                    float d = float.MaxValue;
                    foreach (var l in lines)
                        for (int i = 0; i + 1 < l.Count; i++)
                        {
                            float dd = SegDist(p, l[i], l[i + 1]);
                            if (dd < d) d = dd;
                        }
                    float a = Mathf.Clamp01(0.5f - (d - half) * scale);
                    pix[y * px + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(pix); tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px / 24f);
            cache[key] = sp;
            return sp;
        }

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float len2 = ab.sqrMagnitude;
            float t = len2 <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return (p - (a + ab * t)).magnitude;
        }

        private static float[] Nums(string s)
        {
            var parts = s.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var r = new float[Mathf.Max(5, parts.Length)];
            for (int i = 0; i < parts.Length; i++) float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out r[i]);
            return r;
        }

        private static List<Vector2> Circle(float cx, float cy, float r)
        {
            var l = new List<Vector2>();
            for (int i = 0; i <= 48; i++) { float a = i / 48f * Mathf.PI * 2f; l.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r)); }
            return l;
        }

        private static List<Vector2> RoundRect(float x, float y, float w, float h, float r)
        {
            var l = new List<Vector2>();
            void Arc(float cx, float cy, float a0) { for (int i = 0; i <= 8; i++) { float a = (a0 + i * 90f / 8f) * Mathf.Deg2Rad; l.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r)); } }
            Arc(x + w - r, y + r, -90f); Arc(x + w - r, y + h - r, 0f); Arc(x + r, y + h - r, 90f); Arc(x + r, y + r, 180f);
            l.Add(l[0]);
            return l;
        }

        // ------------------------------------------------------------------ path data

        private sealed class Scanner
        {
            private readonly string s; public int I;
            public Scanner(string s) { this.s = s; }
            public bool More { get { SkipSep(); return I < s.Length; } }
            public char Peek => s[I];
            private void SkipSep() { while (I < s.Length && (s[I] == ' ' || s[I] == ',')) I++; }
            public bool AtCommand { get { SkipSep(); return I < s.Length && char.IsLetter(s[I]); } }
            public char Cmd() { SkipSep(); return s[I++]; }
            public float Num()
            {
                SkipSep();
                int st = I;
                if (I < s.Length && (s[I] == '-' || s[I] == '+')) I++;
                bool dot = false;
                while (I < s.Length && (char.IsDigit(s[I]) || (s[I] == '.' && !dot))) { if (s[I] == '.') dot = true; I++; }
                return float.Parse(s.Substring(st, I - st), CultureInfo.InvariantCulture);
            }
            public bool Flag() { SkipSep(); return s[I++] == '1'; }
        }

        private static List<List<Vector2>> Path(string d)
        {
            var result = new List<List<Vector2>>();
            var sc = new Scanner(d);
            List<Vector2> cur = null;
            Vector2 pos = Vector2.zero, start = Vector2.zero, lastCtl = Vector2.zero;
            char cmd = ' ', prev = ' ';
            while (sc.More)
            {
                if (sc.AtCommand) cmd = sc.Cmd();
                bool rel = char.IsLower(cmd);
                char c = char.ToUpperInvariant(cmd);
                Vector2 Pt(float x, float y) => rel ? pos + new Vector2(x, y) : new Vector2(x, y);
                switch (c)
                {
                    case 'M': { var p = Pt(sc.Num(), sc.Num()); cur = new List<Vector2> { p }; result.Add(cur); pos = start = p; cmd = rel ? 'l' : 'L'; break; }
                    case 'L': { var p = Pt(sc.Num(), sc.Num()); cur.Add(p); pos = p; break; }
                    case 'H': { float x = sc.Num(); var p = new Vector2(rel ? pos.x + x : x, pos.y); cur.Add(p); pos = p; break; }
                    case 'V': { float y = sc.Num(); var p = new Vector2(pos.x, rel ? pos.y + y : y); cur.Add(p); pos = p; break; }
                    case 'C':
                    case 'S':
                        {
                            Vector2 c1;
                            if (c == 'C') c1 = Pt(sc.Num(), sc.Num());
                            else c1 = (prev == 'C' || prev == 'S') ? pos * 2f - lastCtl : pos;
                            var c2 = Pt(sc.Num(), sc.Num()); var p = Pt(sc.Num(), sc.Num());
                            for (int i = 1; i <= 16; i++) { float t = i / 16f, u = 1 - t; cur.Add(u * u * u * pos + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * p); }
                            lastCtl = c2; pos = p; break;
                        }
                    case 'Q':
                        {
                            var c1 = Pt(sc.Num(), sc.Num()); var p = Pt(sc.Num(), sc.Num());
                            for (int i = 1; i <= 12; i++) { float t = i / 12f, u = 1 - t; cur.Add(u * u * pos + 2 * u * t * c1 + t * t * p); }
                            lastCtl = c1; pos = p; break;
                        }
                    case 'A':
                        {
                            float rx = sc.Num(), ry = sc.Num(), rot = sc.Num(); bool large = sc.Flag(), sweep = sc.Flag();
                            var p = Pt(sc.Num(), sc.Num());
                            Arc(cur, pos, rx, ry, rot, large, sweep, p); pos = p; break;
                        }
                    case 'Z': cur.Add(start); pos = start; break;
                    default: return result;
                }
                prev = c;
            }
            return result;
        }

        // SVG implementation notes F.6.5: endpoint to centre parameterisation
        private static void Arc(List<Vector2> pts, Vector2 p0, float rx, float ry, float rotDeg, bool large, bool sweep, Vector2 p1)
        {
            if (rx == 0 || ry == 0 || p0 == p1) { pts.Add(p1); return; }
            float phi = rotDeg * Mathf.Deg2Rad, cp = Mathf.Cos(phi), sp = Mathf.Sin(phi);
            float dx = (p0.x - p1.x) / 2f, dy = (p0.y - p1.y) / 2f;
            float x1 = cp * dx + sp * dy, y1 = -sp * dx + cp * dy;
            rx = Mathf.Abs(rx); ry = Mathf.Abs(ry);
            float lam = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lam > 1f) { float s = Mathf.Sqrt(lam); rx *= s; ry *= s; }
            float num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            float den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            float co = Mathf.Sqrt(Mathf.Max(0f, num / den)) * (large == sweep ? -1f : 1f);
            float cxp = co * rx * y1 / ry, cyp = -co * ry * x1 / rx;
            float cx = cp * cxp - sp * cyp + (p0.x + p1.x) / 2f, cy = sp * cxp + cp * cyp + (p0.y + p1.y) / 2f;
            float th1 = Mathf.Atan2((y1 - cyp) / ry, (x1 - cxp) / rx);
            float th2 = Mathf.Atan2((-y1 - cyp) / ry, (-x1 - cxp) / rx);
            float dth = th2 - th1;
            if (sweep && dth < 0) dth += Mathf.PI * 2f; else if (!sweep && dth > 0) dth -= Mathf.PI * 2f;
            int n = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(dth) / (Mathf.PI / 18f)));
            for (int i = 1; i <= n; i++)
            {
                float t = th1 + dth * i / n;
                float ex = rx * Mathf.Cos(t), ey = ry * Mathf.Sin(t);
                pts.Add(new Vector2(cp * ex - sp * ey + cx, sp * ex + cp * ey + cy));
            }
        }
    }
}
