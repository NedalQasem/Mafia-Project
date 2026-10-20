// Adds Arabic Presentation Forms-B (U+FE70-FEFF) to a TrueType font by shaping each letter with the font's own GSUB/GPOS data.
// Unity's text can't shape Arabic, so ArabicShaper (Scripts/UI/Kit/ArabicShaper.cs) writes presentation-form code points and the font must have them in its cmap.
// Fonts that draw a letter as a body glyph plus separate dot glyphs (Aref Ruqaa) get one composite glyph per joined form (outlines unchanged, dots placed from GPOS).
// Usage: node Tools/ui-gen/fontfix.js <in.ttf> <out.ttf>
const fs = require("fs"), path = require("path");
const [, , inPath, outPath] = process.argv;
const buf = fs.readFileSync(inPath);
const u16 = o => buf.readUInt16BE(o), i16 = o => buf.readInt16BE(o), u32 = o => buf.readUInt32BE(o);
const tag = o => buf.toString("latin1", o, o + 4);

// ---- sfnt directory
const numTables = u16(4);
const tables = {};
for (let i = 0; i < numTables; i++) { const o = 12 + i * 16; tables[tag(o)] = { off: u32(o + 8), len: u32(o + 12) }; }

// ---- cmap -> Map(unicode -> gid)
function readCmap() {
  const base = tables.cmap.off, n = u16(base + 2); let pick = null, best = -1;
  for (let i = 0; i < n; i++) {
    const p = u16(base + 4 + i * 8), e = u16(base + 6 + i * 8), off = base + u32(base + 8 + i * 8), f = u16(off);
    const score = (p === 3 && e === 10 ? 4 : p === 3 && e === 1 ? 3 : p === 0 ? 2 : 0) + (f === 12 ? 0.5 : 0);
    if ((f === 4 || f === 12) && score > best) { best = score; pick = { off, f }; }
  }
  const m = new Map();
  if (pick.f === 4) {
    const o = pick.off, segX2 = u16(o + 6), segs = segX2 / 2;
    const endO = o + 14, startO = endO + segX2 + 2, deltaO = startO + segX2, rangeO = deltaO + segX2;
    for (let s = 0; s < segs; s++) {
      const end = u16(endO + s * 2), start = u16(startO + s * 2), delta = i16(deltaO + s * 2), ro = u16(rangeO + s * 2);
      for (let c = start; c <= end && c !== 0xFFFF; c++) {
        let g;
        if (ro === 0) g = (c + delta) & 0xFFFF;
        else { const gi = rangeO + s * 2 + ro + (c - start) * 2; g = u16(gi); if (g !== 0) g = (g + delta) & 0xFFFF; }
        if (g) m.set(c, g);
      }
    }
  } else {
    const o = pick.off, groups = u32(o + 12);
    for (let g = 0; g < groups; g++) { const s = u32(o + 16 + g * 12), e = u32(o + 20 + g * 12), gid = u32(o + 24 + g * 12); for (let c = s; c <= e; c++) m.set(c, gid + c - s); }
  }
  return m;
}
const cmap = readCmap();

// ---- GSUB lookups
const G = tables.GSUB.off;
const featureList = G + u16(G + 6), lookupList = G + u16(G + 8);
function lookupSubtables(li) {
  const lo = lookupList + u16(lookupList + 2 + li * 2);
  const type = u16(lo), n = u16(lo + 4), subs = [];
  for (let i = 0; i < n; i++) {
    let so = lo + u16(lo + 6 + i * 2), t = type;
    if (type === 7) { t = u16(so + 2); so = so + u32(so + 4); }
    subs.push({ t, so });
  }
  return subs;
}
const featLookups = {};   // every feature with that tag, not only the ones the default language system picks
for (let i = 0, fcount = u16(featureList); i < fcount; i++) {
  const t = tag(featureList + 2 + i * 6), fo = featureList + u16(featureList + 6 + i * 6), ln = u16(fo + 2);
  (featLookups[t] = featLookups[t] || new Set());
  for (let k = 0; k < ln; k++) featLookups[t].add(u16(fo + 4 + k * 2));
}

// ---- a small GSUB shaping engine (lookup types 1, 2, 4, 5/3, 6) to find the glyph each letter gets in each joining position.
// Fonts like Aref Ruqaa choose joined shapes with contextual rules, so reading init/medi/fina tables is not enough.
function covIdx(o, gid) {
  const f = u16(o);
  if (f === 1) { const n = u16(o + 2); let lo = 0, hi = n - 1; while (lo <= hi) { const mid = (lo + hi) >> 1, v = u16(o + 4 + mid * 2); if (v === gid) return mid; if (v < gid) lo = mid + 1; else hi = mid - 1; } return -1; }
  const n = u16(o + 2);
  for (let i = 0; i < n; i++) { const s = u16(o + 4 + i * 6), e = u16(o + 6 + i * 6), sc = u16(o + 8 + i * 6); if (gid >= s && gid <= e) return sc + gid - s; }
  return -1;
}
function classOf(o, gid) {
  if (!o) return 0; const f = u16(o);
  if (f === 1) { const sg = u16(o + 2), n = u16(o + 4); return gid >= sg && gid < sg + n ? u16(o + 6 + (gid - sg) * 2) : 0; }
  const n = u16(o + 2);
  for (let i = 0; i < n; i++) { const s = u16(o + 4 + i * 6), e = u16(o + 6 + i * 6); if (gid >= s && gid <= e) return u16(o + 8 + i * 6); }
  return 0;
}
function contextual(g, mask, t, so, i, depth) {
  const f = u16(so);
  const run = (recsOff, n, inputLen) => {
    for (let k = 0; k < n; k++) applyOne(g, mask, u16(recsOff + k * 4 + 2), i + u16(recsOff + k * 4), depth + 1);
    return Math.max(1, inputLen);
  };
  const readArr = (o, n) => { const a = []; for (let k = 0; k < n; k++) a.push(u16(o + k * 2)); return a; };
  if (t === 5 && f === 3) {
    const gc = u16(so + 2), sc = u16(so + 4), cov = readArr(so + 6, gc);
    if (i + gc > g.length) return 0;
    for (let k = 0; k < gc; k++) if (covIdx(so + cov[k], g[i + k]) < 0) return 0;
    return run(so + 6 + gc * 2, sc, gc);
  }
  if (t !== 6) return 0;
  if (f === 3) {
    let o = so + 2; const bc = u16(o); const back = readArr(o + 2, bc); o += 2 + bc * 2;
    const ic = u16(o); const inp = readArr(o + 2, ic); o += 2 + ic * 2;
    const lc = u16(o); const look = readArr(o + 2, lc); o += 2 + lc * 2;
    const sc = u16(o);
    if (i + ic > g.length) return 0;
    for (let k = 0; k < ic; k++) if (covIdx(so + inp[k], g[i + k]) < 0) return 0;
    for (let k = 0; k < bc; k++) { const p = i - 1 - k; if (p < 0 || covIdx(so + back[k], g[p]) < 0) return 0; }
    for (let k = 0; k < lc; k++) { const p = i + ic + k; if (p >= g.length || covIdx(so + look[k], g[p]) < 0) return 0; }
    return run(o + 2, sc, ic);
  }
  const rules = (setOff, test) => {   // chain rules of one rule set; test(back[], input[], look[]) decides a match
    const rn = u16(setOff);
    for (let r = 0; r < rn; r++) {
      let o = setOff + u16(setOff + 2 + r * 2);
      const bc = u16(o); const back = readArr(o + 2, bc); o += 2 + bc * 2;
      const ic = u16(o); const inp = readArr(o + 2, ic - 1); o += 2 + (ic - 1) * 2;
      const lc = u16(o); const look = readArr(o + 2, lc); o += 2 + lc * 2;
      const sc = u16(o);
      if (i + ic > g.length) continue;
      if (test(back, inp, look)) return run(o + 2, sc, ic);
    }
    return 0;
  };
  if (f === 1) {
    const idx = covIdx(so + u16(so + 2), g[i]); if (idx < 0 || idx >= u16(so + 4)) return 0;
    const so2 = u16(so + 6 + idx * 2); if (!so2) return 0;
    return rules(so + so2, (back, inp, look) => {
      for (let k = 0; k < back.length; k++) { const p = i - 1 - k; if (p < 0 || g[p] !== back[k]) return false; }
      for (let k = 0; k < inp.length; k++) if (g[i + 1 + k] !== inp[k]) return false;
      for (let k = 0; k < look.length; k++) { const p = i + inp.length + 1 + k; if (p >= g.length || g[p] !== look[k]) return false; }
      return true;
    });
  }
  if (f === 2) {
    if (covIdx(so + u16(so + 2), g[i]) < 0) return 0;
    const bcd = u16(so + 4) ? so + u16(so + 4) : 0, icd = u16(so + 6) ? so + u16(so + 6) : 0, lcd = u16(so + 8) ? so + u16(so + 8) : 0;
    const cls = classOf(icd, g[i]);
    if (cls >= u16(so + 10)) return 0;
    const so2 = u16(so + 12 + cls * 2); if (!so2) return 0;
    return rules(so + so2, (back, inp, look) => {
      for (let k = 0; k < back.length; k++) { const p = i - 1 - k; if (p < 0 || classOf(bcd, g[p]) !== back[k]) return false; }
      for (let k = 0; k < inp.length; k++) if (classOf(icd, g[i + 1 + k]) !== inp[k]) return false;
      for (let k = 0; k < look.length; k++) { const p = i + inp.length + 1 + k; if (p >= g.length || classOf(lcd, g[p]) !== look[k]) return false; }
      return true;
    });
  }
  return 0;
}
function applyOne(g, mask, li, i, depth) {
  if (depth > 8 || i < 0 || i >= g.length) return 0;
  for (const { t, so } of lookupSubtables(li)) {
    if (t === 1) {
      const idx = covIdx(so + u16(so + 2), g[i]); if (idx < 0) continue;
      g[i] = u16(so) === 1 ? (g[i] + i16(so + 4)) & 0xFFFF : u16(so + 6 + idx * 2); return 1;
    }
    if (t === 2) {
      const idx = covIdx(so + u16(so + 2), g[i]); if (idx < 0) continue;
      const sq = so + u16(so + 6 + idx * 2), n = u16(sq), seq = [];
      for (let k = 0; k < n; k++) seq.push(u16(sq + 2 + k * 2));
      const m = mask[i]; g.splice(i, 1, ...seq); mask.splice(i, 1, ...seq.map(() => m)); return Math.max(1, n);
    }
    if (t === 4) {
      const idx = covIdx(so + u16(so + 2), g[i]); if (idx < 0) continue;
      const set = so + u16(so + 6 + idx * 2), ln = u16(set);
      for (let l = 0; l < ln; l++) {
        const lg = set + u16(set + 2 + l * 2), by = u16(lg), cn = u16(lg + 2);
        let ok = i + cn <= g.length; for (let c = 0; ok && c < cn - 1; c++) if (g[i + 1 + c] !== u16(lg + 4 + c * 2)) ok = false;
        if (ok) { const m = mask[i]; g.splice(i, cn, by); mask.splice(i, cn, m); return 1; }
      }
      continue;
    }
    if (t === 5 || t === 6) { const r = contextual(g, mask, t, so, i, depth); if (r) return r; }
  }
  return 0;
}
const lookupTags = {};
for (const t in featLookups) for (const li of featLookups[t]) (lookupTags[li] = lookupTags[li] || new Set()).add(t);
const GLOBAL_FEATURES = new Set(["ccmp", "locl", "rlig", "calt", "liga", "rclt"]);
function shape(glyphs, masks) {
  const g = glyphs.slice(), mask = masks.slice();
  for (const li of Object.keys(lookupTags).map(Number).sort((a, b) => a - b)) {
    const tags = lookupTags[li];
    for (let i = 0; i < g.length;) {
      const on = [...tags].some(t => GLOBAL_FEATURES.has(t) || t === mask[i]);
      if (!on) { i++; continue; }
      const adv = applyOne(g, mask, li, i, 0); i += adv > 0 ? adv : 1;
    }
  }
  return g;
}

// ---- glyph helpers (some fonts, like Aref Ruqaa, draw a letter as a body glyph plus separate zero-width dot glyphs placed by GPOS)
const locaFmt = i16(tables.head.off + 50), numGlyphs = u16(tables.maxp.off + 4), numHM = u16(tables.hhea.off + 34);
const locaOf = g => (locaFmt ? u32(tables.loca.off + g * 4) : u16(tables.loca.off + g * 2) * 2);
const advOf = g => u16(tables.hmtx.off + Math.min(g, numHM - 1) * 4);
const lsbOf = g => (g < numHM ? i16(tables.hmtx.off + g * 4 + 2) : i16(tables.hmtx.off + numHM * 4 + (g - numHM) * 2));
const glyphBox = g => { if (locaOf(g + 1) === locaOf(g)) return null; const o = tables.glyf.off + locaOf(g); return [i16(o + 2), i16(o + 4), i16(o + 6), i16(o + 8)]; };
const markBase = [];   // GPOS mark-to-base subtables
if (tables.GPOS) {
  const P = tables.GPOS.off, ll = P + u16(P + 8), ln = u16(ll);
  for (let li = 0; li < ln; li++) {
    const lo = ll + u16(ll + 2 + li * 2), type = u16(lo), n = u16(lo + 4);
    for (let i = 0; i < n; i++) { let so = lo + u16(lo + 6 + i * 2), t = type; if (type === 9) { t = u16(so + 2); so = so + u32(so + 4); } if (t === 4) markBase.push(so); }
  }
}
function attach(base, mark) {   // where the mark glyph goes relative to the base glyph origin
  for (const so of markBase) {
    const mi = covIdx(so + u16(so + 2), mark), bi = covIdx(so + u16(so + 4), base);
    if (mi < 0 || bi < 0) continue;
    const cc = u16(so + 6), ma = so + u16(so + 8), ba = so + u16(so + 10);
    const cls = u16(ma + 2 + mi * 4), mAnchor = ma + u16(ma + 2 + mi * 4 + 2), bOff = u16(ba + 2 + bi * cc * 2 + cls * 2);
    if (!bOff) continue;
    const bAnchor = ba + bOff;
    return [i16(bAnchor + 2) - i16(mAnchor + 2), i16(bAnchor + 4) - i16(mAnchor + 4)];
  }
  return [0, 0];
}
const newGlyphs = [], seqCache = new Map();
const isMark = g => advOf(g) === 0;
const groups = seq => { const o = []; for (const g of seq) { if (!o.length || !isMark(g)) o.push([g]); else o[o.length - 1].push(g); } return o; };
function glyphForSeq(seq) {
  if (seq.length === 1) return seq[0];
  const key = seq.join(","); if (seqCache.has(key)) return seqCache.get(key);
  const parts = [{ gid: seq[0], dx: 0, dy: 0 }];
  for (const m of seq.slice(1)) { const [dx, dy] = attach(seq[0], m); parts.push({ gid: m, dx, dy }); }
  const gid = numGlyphs + newGlyphs.length; newGlyphs.push({ parts, adv: advOf(seq[0]) }); seqCache.set(key, gid); return gid;
}

// ---- positional forms for the Presentation Forms-B layout: groups of [isol, fina, init, medi] share one NFKC base
const added = new Map();
const baseOf = cp => String.fromCodePoint(cp).normalize("NFKC");
const beh = cmap.get(0x628);
function letterForms(baseCp) {
  const g0 = cmap.get(baseCp); if (!g0) return null;
  const f = { isol: g0, fina: g0, init: g0, medi: g0 };
  let r = groups(shape([g0], ["isol"])); if (r.length === 1) f.isol = glyphForSeq(r[0]);
  r = groups(shape([g0, beh], ["init", "fina"])); if (r.length === 2) f.init = glyphForSeq(r[0]);
  r = groups(shape([beh, g0, beh], ["init", "medi", "fina"])); if (r.length === 3) f.medi = glyphForSeq(r[1]);
  r = groups(shape([beh, g0], ["init", "fina"])); if (r.length === 2) f.fina = glyphForSeq(r[1]);
  return f;
}
let cp = 0xFE80;
const report = [];
while (cp <= 0xFEF4) {
  const b = baseOf(cp);
  let size = 1; while (cp + size <= 0xFEF4 && baseOf(cp + size) === b) size++;
  const forms = b.length === 1 ? letterForms(b.codePointAt(0)) : null;
  if (forms) {
    const order = ["isol", "fina", "init", "medi"];
    for (let k = 0; k < size && k < 4; k++) if (!cmap.has(cp + k)) added.set(cp + k, forms[order[k]]);
  } else report.push("no base glyph for U+" + cp.toString(16));
  cp += size;
}
// lam-alef (FEF5..FEFC): isolated = lam.init + alef.fina, final = lam.medi + alef.fina; only when the font really makes one ligature glyph
const lam = cmap.get(0x644);
let ligFound = 0;
if (lam) for (const [isolCp, alefCp] of [[0xFEF5, 0x622], [0xFEF7, 0x623], [0xFEF9, 0x625], [0xFEFB, 0x627]]) {
  const a = cmap.get(alefCp); if (!a) continue;
  let r = groups(shape([lam, a], ["init", "fina"]));
  if (r.length === 1 && !cmap.has(isolCp)) { added.set(isolCp, glyphForSeq(r[0])); ligFound++; }
  r = groups(shape([beh, lam, a], ["init", "medi", "fina"]));
  if (r.length === 2 && !cmap.has(isolCp + 1)) { added.set(isolCp + 1, glyphForSeq(r[1])); ligFound++; }
}
report.push("forms added: " + added.size + ", lam-alef ligatures: " + ligFound + ", composite glyphs: " + newGlyphs.length);

// ---- contextual forms (optional 3rd argument: output text file).
// Calligraphic fonts such as Aref Ruqaa pick a different glyph for a letter depending on its neighbours, so one glyph per joining form is not enough.
// For every letter and every connecting neighbour pair we shape [prev, letter, next] with the font's own rules, give each distinct result a
// Private Use code point (U+E000..) and write a lookup table that Scripts/UI/Kit/ArabicShaper.cs reads at run time.
const ctxOut = process.argv[4];
if (ctxOut) {
  const DUAL = "ئبتثجحخسشصضطظعغفقكلمنهي", RIGHT = "آأؤإادذرزةوى", ALL = DUAL + RIGHT + "ء";
  const connectsNext = ch => DUAL.includes(ch), acceptsPrev = ch => DUAL.includes(ch) || RIGHT.includes(ch);
  const puaOf = new Map(); let puaNext = 0xE000;
  const pua = gid => { if (!puaOf.has(gid)) { const c = puaNext++; puaOf.set(gid, c); added.set(c, gid); } return puaOf.get(gid); };
  const maskName = ["isol", "fina", "init", "medi"];
  const formOf = (X, prevJoin, nextJoin) => DUAL.includes(X) ? (prevJoin && nextJoin ? 3 : prevJoin ? 1 : nextJoin ? 2 : 0) : (prevJoin ? 1 : 0);
  function shapeLetter(P, X, N) {
    const prevJoin = !!P && connectsNext(P) && acceptsPrev(X), nextJoin = !!N && connectsNext(X) && acceptsPrev(N);
    const gl = [], masks = []; let idx = 0;
    if (P) { gl.push(cmap.get(P.codePointAt(0))); masks.push("init"); idx = 1; }
    gl.push(cmap.get(X.codePointAt(0))); masks.push(maskName[formOf(X, prevJoin, nextJoin)]);
    if (N) { gl.push(cmap.get(N.codePointAt(0))); masks.push("fina"); }
    if (gl.some(g => !g)) return null;
    const gr = groups(shape(gl, masks));
    if (gr.length !== gl.length) return null;   // letters merged into a ligature: leave to the default
    return pua(glyphForSeq(gr[idx]));
  }
  const lines = []; const def = {};
  for (const X of ALL) {
    if (!cmap.get(X.codePointAt(0))) continue;
    for (let f = 0; f < 4; f++) {
      if (!DUAL.includes(X) && f > 1) continue;
      const needPrev = f === 1 || f === 3, needNext = f === 2 || f === 3;
      const v = shapeLetter(needPrev ? "ب" : "", X, needNext ? "ب" : "");
      if (v) { def[X + f] = v; lines.push(["D", X, f, v.toString(16)].join("\t")); }
    }
  }
  let exc = 0;
  for (const X of ALL) {
    if (!cmap.get(X.codePointAt(0))) continue;
    const prevs = acceptsPrev(X) ? [""].concat([...DUAL]) : [""];
    const nexts = connectsNext(X) ? [""].concat([...ALL].filter(acceptsPrev)) : [""];
    for (const P of prevs) for (const N of nexts) {
      const f = formOf(X, !!P, !!N);
      const v = shapeLetter(P, X, N);
      if (v && v !== def[X + f]) { lines.push(["E", P || "-", X, N || "-", v.toString(16)].join("\t")); exc++; }
    }
  }
  fs.mkdirSync(path.dirname(ctxOut), { recursive: true });
  fs.writeFileSync(ctxOut, lines.join("\n"), "utf8");
  report.push("contextual: " + Object.keys(def).length + " defaults, " + exc + " exceptions, " + puaOf.size + " private-use glyphs");
}

// ---- write the new cmap (format 4, BMP) and rebuild the font
const all = new Map(cmap); for (const [c, g] of added) all.set(c, g);
const codes = [...all.keys()].filter(c => c <= 0xFFFE).sort((a, b) => a - b);
const segs = [];
for (const c of codes) {
  const g = all.get(c), last = segs[segs.length - 1];
  if (last && c === last.end + 1 && g === last.gEnd + 1) { last.end = c; last.gEnd = g; } else segs.push({ start: c, end: c, g0: g, gEnd: g });
}
segs.push({ start: 0xFFFF, end: 0xFFFF, g0: 1, gEnd: 1, last: true });
const segCount = segs.length;
const sub4 = Buffer.alloc(16 + segCount * 8);
sub4.writeUInt16BE(4, 0); sub4.writeUInt16BE(sub4.length, 2); sub4.writeUInt16BE(0, 4);
sub4.writeUInt16BE(segCount * 2, 6);
let sr = 1, es = 0; while (sr * 2 <= segCount) { sr *= 2; es++; }
sub4.writeUInt16BE(sr * 2, 8); sub4.writeUInt16BE(es, 10); sub4.writeUInt16BE(segCount * 2 - sr * 2, 12);
segs.forEach((s, i) => {
  sub4.writeUInt16BE(s.end, 14 + i * 2);
  sub4.writeUInt16BE(s.start, 16 + segCount * 2 + i * 2);
  sub4.writeInt16BE(s.last ? 1 : ((s.g0 - s.start) << 16) >> 16, 16 + segCount * 4 + i * 2);
  sub4.writeUInt16BE(0, 16 + segCount * 6 + i * 2);
});
const newCmap = Buffer.alloc(4 + 16 + sub4.length);
newCmap.writeUInt16BE(0, 0); newCmap.writeUInt16BE(2, 2);
newCmap.writeUInt16BE(3, 4); newCmap.writeUInt16BE(1, 6); newCmap.writeUInt32BE(20, 8);
newCmap.writeUInt16BE(0, 12); newCmap.writeUInt16BE(3, 14); newCmap.writeUInt32BE(20, 16);
sub4.copy(newCmap, 20);

// tables: new composite glyphs need glyf, loca (long), hmtx, hhea, maxp; post goes to the name-less format 3; DSIG is dropped (it no longer matches)
const outTables = [];
const repl = { cmap: newCmap };
if (newGlyphs.length) {
  const total = numGlyphs + newGlyphs.length;
  const oldEnd = locaOf(numGlyphs);
  const pad4 = n => Math.ceil(n / 4) * 4;
  const chunks = [Buffer.concat([Buffer.from(buf.subarray(tables.glyf.off, tables.glyf.off + oldEnd)), Buffer.alloc(pad4(oldEnd) - oldEnd)])];
  let cursor = pad4(oldEnd);
  const starts = []; for (let g = 0; g < numGlyphs; g++) starts.push(locaOf(g));
  const boxes = [];
  for (const ng of newGlyphs) {
    starts.push(cursor);
    let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
    for (const p of ng.parts) { const bx = glyphBox(p.gid); if (!bx) continue; x0 = Math.min(x0, bx[0] + p.dx); y0 = Math.min(y0, bx[1] + p.dy); x1 = Math.max(x1, bx[2] + p.dx); y1 = Math.max(y1, bx[3] + p.dy); }
    if (x0 > x1) { x0 = y0 = x1 = y1 = 0; }
    boxes.push([x0, y0, x1, y1]);
    const rec = Buffer.alloc(pad4(10 + ng.parts.length * 8));
    rec.writeInt16BE(-1, 0); rec.writeInt16BE(x0, 2); rec.writeInt16BE(y0, 4); rec.writeInt16BE(x1, 6); rec.writeInt16BE(y1, 8);
    ng.parts.forEach((p, i) => { const o = 10 + i * 8; rec.writeUInt16BE(0x0003 | (i < ng.parts.length - 1 ? 0x0020 : 0), o); rec.writeUInt16BE(p.gid, o + 2); rec.writeInt16BE(p.dx, o + 4); rec.writeInt16BE(p.dy, o + 6); });
    chunks.push(rec); cursor += rec.length;
  }
  starts.push(cursor);
  repl.glyf = Buffer.concat(chunks);
  const loca = Buffer.alloc(starts.length * 4); starts.forEach((v, i) => loca.writeUInt32BE(v, i * 4)); repl.loca = loca;
  const head = Buffer.from(buf.subarray(tables.head.off, tables.head.off + tables.head.len)); head.writeInt16BE(1, 50); repl.head = head;
  const hmtx = Buffer.alloc(total * 4);
  for (let g = 0; g < numGlyphs; g++) { hmtx.writeUInt16BE(advOf(g), g * 4); hmtx.writeInt16BE(lsbOf(g), g * 4 + 2); }
  newGlyphs.forEach((ng, i) => { hmtx.writeUInt16BE(ng.adv, (numGlyphs + i) * 4); hmtx.writeInt16BE(boxes[i][0], (numGlyphs + i) * 4 + 2); });
  repl.hmtx = hmtx;
  const hhea = Buffer.from(buf.subarray(tables.hhea.off, tables.hhea.off + tables.hhea.len)); hhea.writeUInt16BE(total, 34); repl.hhea = hhea;
  const maxp = Buffer.from(buf.subarray(tables.maxp.off, tables.maxp.off + tables.maxp.len)); maxp.writeUInt16BE(total, 4);
  if (maxp.length >= 32) { maxp.writeUInt16BE(Math.max(maxp.readUInt16BE(28), 4), 28); maxp.writeUInt16BE(Math.max(maxp.readUInt16BE(30), 2), 30); }
  repl.maxp = maxp;
  if (tables.post) { const post = Buffer.from(buf.subarray(tables.post.off, tables.post.off + 32)); post.writeUInt32BE(0x00030000, 0); repl.post = post; }
}
for (const t of Object.keys(tables)) { if (t === "DSIG" && newGlyphs.length) continue; outTables.push({ t, data: repl[t] || Buffer.from(buf.subarray(tables[t].off, tables[t].off + tables[t].len)) }); }
outTables.sort((a, b) => (a.t < b.t ? -1 : 1));
const dirLen = 12 + outTables.length * 16; let off = dirLen;
const sum = b => { let s = 0; const p = Buffer.concat([b, Buffer.alloc((4 - (b.length % 4)) % 4)]); for (let i = 0; i < p.length; i += 4) s = (s + p.readUInt32BE(i)) >>> 0; return s; };
outTables.forEach(e => { if (e.t === "head") e.data.writeUInt32BE(0, 8); e.off = off; off += Math.ceil(e.data.length / 4) * 4; });
const file = Buffer.alloc(off);
buf.copy(file, 0, 0, 4); file.writeUInt16BE(outTables.length, 4);
let rs = 1, rsl = 0; while (rs * 2 <= outTables.length) { rs *= 2; rsl++; }
file.writeUInt16BE(rs * 16, 6); file.writeUInt16BE(rsl, 8); file.writeUInt16BE(outTables.length * 16 - rs * 16, 10);
outTables.forEach((e, i) => { const o = 12 + i * 16; file.write(e.t, o, "latin1"); file.writeUInt32BE(sum(e.data), o + 4); file.writeUInt32BE(e.off, o + 8); file.writeUInt32BE(e.data.length, o + 12); e.data.copy(file, e.off); });
const headT = outTables.find(e => e.t === "head");
file.writeUInt32BE((0xB1B0AFBA - sum(file)) >>> 0, headT.off + 8);
fs.writeFileSync(outPath, file);
console.log(outPath, file.length, "bytes;", report.join("; "), "; composite glyphs now:", newGlyphs.length);
