/* Extracts the layout of every family.html frame (what the browser computed) into Art/UI/Family/frames.json for the Unity builder.
   Same method as extract-match.js. Run in family.html with export-frames.js loaded:  await extractAll()  then  await exportTextSprites()  */
(function () {
  const rgba = s => {
    const m = (s || '').match(/rgba?\(([^)]+)\)/);
    if (!m) return null;
    const p = m[1].split(/[ ,\/]+/).filter(Boolean).map(Number);
    return [p[0], p[1], p[2], p.length > 3 ? p[3] : 1];
  };
  const hex = c => '#' + c.slice(0, 3).map(v => Math.round(Math.max(0, Math.min(255, v))).toString(16).padStart(2, '0')).join('') + Math.round((c[3] === undefined ? 1 : c[3]) * 255).toString(16).padStart(2, '0');
  const mulC = (c, b) => [c[0] * b, c[1] * b, c[2] * b, c[3]];
  const fromHex = h => { const m = /^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})?$/i.exec(h || ''); return m ? [parseInt(m[1], 16), parseInt(m[2], 16), parseInt(m[3], 16), m[4] ? parseInt(m[4], 16) / 255 : 1] : null; };
  const bright = cs => { const m = (cs.filter || '').match(/brightness\(([\d.]+)\)/); return m ? parseFloat(m[1]) : 1; };

  function spriteFor(el, cs) {
    const c = el.classList, has = k => c.contains(k), par = el.parentElement;
    if (has('phone')) return { s: 'bg', stretch: true };
    if (has('dock')) return { kind: 'nav' };
    if (has('top')) return { s: 'top_fade' };
    if (has('ic') && has('fr')) return { s: 'ic_fr' };
    if (has('ic')) return { s: 'ic_btn' };
    if (has('ctab')) return { s: has('on') ? 'ctab_on' : 'ctab_off' };
    if (has('cbd')) return { s: 'cbd' };
    if (has('av') && has('grp')) return { s: 'av_grp' };
    if (has('av') && el.closest('.add')) return { s: 'av_add' };
    if (has('letter')) return { s: 'letter_bg' };
    if (has('big')) return { s: has('red') ? 'big_red' : 'big_dim' };
    if (has('newg')) return { s: 'newg_bg' };
    if (has('coach')) return { s: 'coach_bg' };
    if (has('dot')) return { s: 'badge_dot' };
    if (has('seg')) return { s: 'seg_bg' };
    if (el.tagName === 'BUTTON' && par && par.classList.contains('seg') && has('on')) return { s: 'seg_on' };
    if (el.tagName === 'I' && has('n') && el.closest('.seg')) return { s: 'seg_n' };
    if (has('row')) return cs.backgroundImage === 'none' ? null : { s: 'row_bg' };
    if (has('av')) return { s: 'av' };
    if (has('st')) return { s: 'st_' + (has('on') ? 'on' : has('room') ? 'room' : 'off') };
    if (has('lv')) return { s: 'lv_pill' };
    if (has('go')) return { s: has('gold') ? 'go_gold' : has('teal') ? 'go_teal' : 'go_red' };
    if (has('ghost')) return { s: 'ghost' };
    if (has('more')) return { s: 'more_btn' };
    if (has('idc')) return { s: 'idc_bg' };
    if (has('cp')) return { s: 'cp_btn' };
    if (has('field')) return { s: 'field_bg' };
    if (has('hint')) return { s: 'hint_bg' };
    if (has('sheet')) return { s: 'sheet_bg' };
    if (has('grab')) return { s: 'grab' };
    if (has('dim')) return { s: '', t: '#000000bb', stretch: true, flat: true };
    if (has('act')) return { s: has('bad') ? 'act_bad' : has('hot') ? 'act_hot' : 'act' };
    if (has('opt')) return { s: has('on') ? 'opt_on' : 'opt_bg' };
    if (el.tagName === 'I' && par && par.classList.contains('opt')) return { s: par.classList.contains('on') ? 'radio_on' : 'radio' };
    if (has('note')) return { s: 'note_bg' };
    if (has('tg')) return { s: has('on') ? 'tg_on' : 'tg_off', skipKids: true };
    if (has('banner')) return { s: 'banner_bg' };
    if (has('bar')) return { s: '', t: '#000000aa', flat: true };
    if (el.tagName === 'I' && par && par.classList.contains('bar')) return { s: 'bar_fill' };
    if (has('stamp')) return { s: 'stamp', t: hex(rgba(cs.color)) };
    return null;
  }

  const SKIP = new Set(['STYLE', 'SCRIPT', 'defs', 'symbol']);

  function extract(phone) {
    const nodes = [];
    const pr = phone.getBoundingClientRect();
    const add = n => { n.i = nodes.length; nodes.push(n); return n.i; };
    const textNode = (idx, over) => add(Object.assign({ p: idx, name: 'text', kind: 'text', rot: 0, op: 1, z: 0, wrap: false }, over));

    function textStyle(cs, b) {
      const ff = cs.fontFamily; const col = mulC(rgba(cs.color) || [255, 255, 255, 1], b);
      const ls = parseFloat(cs.letterSpacing); const fs = parseFloat(cs.fontSize);
      return { fs, fw: parseInt(cs.fontWeight) || 400, color: hex(col), align: cs.textAlign, dir: cs.direction, ls: isNaN(ls) ? 0 : ls / fs,
        font: ff.includes('Lalezar') ? 'head' : ff.includes('Aref') ? 'calli' : 'body' };
    }

    function walk(el, parentRect, pi, ctx) {
      const cs = getComputedStyle(el);
      if (cs.display === 'none') return;
      const r = el.getBoundingClientRect();
      const b = ctx.b * bright(cs);
      const n = { p: pi, name: el.tagName.toLowerCase() + (el.className && el.className.baseVal === undefined ? '.' + String(el.className).split(' ').join('.') : ''), kind: 'box',
        x: r.left - parentRect.left, y: r.top - parentRect.top, w: r.width, h: r.height,
        rot: parseFloat(el.dataset.rot || '0') || 0, op: Math.min(1, parseFloat(cs.opacity)), z: cs.position !== 'static' ? (parseInt(cs.zIndex) || 0) : 0 };

      if (el.tagName.toLowerCase() === 'svg') {
        if (el.closest('.av')) return;
        const use = el.querySelector('use'); const id = use ? (use.getAttribute('href') || '').replace('#', '') : '';
        n.kind = 'icon'; n.sprite = 'icon_' + id; n.tint = hex(mulC(rgba(cs.stroke) || [255, 255, 255, 1], b));
        add(n); return;
      }

      const sp = spriteFor(el, cs);
      if (sp && sp.kind === 'nav') { n.kind = 'nav'; n.bottom = true; add(n); return; }
      if (sp) {
        n.sprite = sp.s; n.stretch = !!sp.stretch; n.flat = !!sp.flat; n.bottom = !!sp.bottom;
        n.tint = sp.t ? hex(mulC(fromHex(sp.t) || [255, 255, 255, 1], sp.flat ? 1 : b)) : (b < 0.999 ? hex([255 * b, 255 * b, 255 * b, 1]) : '');
      }
      const idx = add(n);
      const me = { b };

      // the line after a section title (::after) and CSS borders
      if (el.classList.contains('sec')) {
        let left = r.right; el.childNodes.forEach(ch => { const rr = ch.nodeType === 3 ? (() => { const g = document.createRange(); g.selectNodeContents(ch); return g.getBoundingClientRect(); })() : ch.getBoundingClientRect(); if (rr.width) left = Math.min(left, rr.left); });
        add({ p: idx, name: 'secline', kind: 'box', x: 0, y: r.height / 2, w: Math.max(0, left - r.left - 8), h: 1, rot: 0, op: 1, z: 0, sprite: '', tint: hex([255, 255, 255, .0706]), flat: true });
      }
      for (const side of (sp ? [] : ['Top', 'Bottom'])) {
        const w = parseFloat(cs['border' + side + 'Width']);
        if (w > 0 && cs['border' + side + 'Style'] !== 'none') add({ p: idx, name: 'line', kind: 'line', x: 0, y: side === 'Top' ? 0 : r.height - w, w: r.width, h: w, rot: 0, op: 1, z: 0, tint: hex(rgba(cs['border' + side + 'Color']) || [255, 255, 255, 1]), dashed: cs['border' + side + 'Style'] === 'dashed' });
      }
      // text inside form controls
      if (el.tagName === 'INPUT' && el.value) {
        const ts = textStyle(cs, b); const pl = parseFloat(cs.paddingLeft) || 0, pr2 = parseFloat(cs.paddingRight) || 0;
        textNode(idx, Object.assign(ts, { x: pl, y: 0, w: r.width - pl - pr2, h: r.height, text: el.value, align: 'right', lh: r.height }));
      }
      if (el.tagName === 'TEXTAREA' && !el.value && el.placeholder) {
        const ts = textStyle(cs, b); const pc = rgba(getComputedStyle(el, '::placeholder').color) || rgba(cs.color);
        textNode(idx, Object.assign(ts, { x: 12, y: 8, w: r.width - 24, h: 20, text: el.placeholder, color: hex(mulC(pc, b)), align: 'right', lh: 20 }));
      }
      if (sp && sp.skipKids) return;

      const kids = []; el.childNodes.forEach(ch => kids.push(ch));
      const ordered = kids.map((ch, k) => {
        let z = 0;
        if (ch.nodeType === 1) { const c2 = getComputedStyle(ch); z = c2.position !== 'static' ? (parseInt(c2.zIndex) || 0) : 0; }
        return { ch, k, z };
      }).sort((a, c) => a.z - c.z || a.k - c.k);
      for (const { ch } of ordered) {
        if (ch.nodeType === 1) { if (!SKIP.has(ch.tagName)) walk(ch, r, idx, me); }
        else if (ch.nodeType === 3 && ch.textContent.trim()) {
          const range = document.createRange(); range.selectNodeContents(ch);
          const rr = range.getBoundingClientRect(); const lines = new Set([...range.getClientRects()].map(q => Math.round(q.top / 4))).size;
          const ts = textStyle(cs, b); const txt = ch.textContent.replace(/\s+/g, ' ').trim();
          if (ts.font === 'calli') {
            let hsh = 5381; const keyStr = txt + '|' + ts.fs + '|' + ts.fw;
            for (let q = 0; q < keyStr.length; q++) hsh = ((hsh * 33) ^ keyStr.charCodeAt(q)) >>> 0;
            const sprite = 't_' + hsh.toString(16);
            (window.__textimgs = window.__textimgs || {})[sprite] = { text: txt, fs: ts.fs, fw: ts.fw, w: rr.width, h: rr.height, align: cs.textAlign };
            add({ p: idx, name: 'textimg', kind: 'textimg', x: rr.left - r.left, y: rr.top - r.top, w: rr.width, h: rr.height, rot: 0, op: 1, z: 0, sprite, tint: ts.color });
            continue;
          }
          textNode(idx, Object.assign(ts, { x: rr.left - r.left, y: rr.top - r.top, w: rr.width, h: rr.height, text: txt, wrap: lines > 1, lh: rr.height / Math.max(1, lines) }));
        }
      }
    }

    nodes.length = 0;
    add({ p: -1, name: 'phone', kind: 'box', x: 0, y: 0, w: pr.width, h: pr.height, rot: 0, op: 1, z: 0, sprite: 'bg', tint: '', stretch: true });
    const kids = [...phone.children].map((ch, k) => { const c2 = getComputedStyle(ch); return { ch, k, z: c2.position !== 'static' ? (parseInt(c2.zIndex) || 0) : 0 }; }).sort((a, c) => a.z - c.z || a.k - c.k);
    for (const { ch } of kids) walk(ch, pr, 0, { b: 1 });
    return nodes;
  }

  window.extractAll = async function () {
    const style = document.createElement('style');
    const frames = [];
    const run = async (id, name) => {
      const phone = document.getElementById(id);
      const col = phone.closest('.col'); col.style.width = '390px'; phone.style.width = '390px'; phone.style.height = '790px';   // the page is laid out for a phone even when the browser pane is narrow
      phone.querySelectorAll('*').forEach(e => { e.dataset.rot = getComputedStyle(e).rotate === 'none' ? '0' : getComputedStyle(e).rotate; });
      style.textContent = '#' + id + ' *{rotate:none!important}'; document.head.appendChild(style);
      await new Promise(r => setTimeout(r, 30));
      frames.push({ name, w: 390, h: 790, nodes: extract(phone) });
      style.remove();
    };
    for (const id of ['f1', 'f2', 'f3', 'f4', 'f5', 'f6', 'c1', 'c2', 'c3']) await run(id, id);
    document.getElementById('rb2').click(); await new Promise(r => setTimeout(r, 30));
    await run('f6', 'f6_after');
    document.getElementById('rb1').click(); await new Promise(r => setTimeout(r, 30));
    await fetch('/save?dir=family&name=frames.json', { method: 'POST', body: JSON.stringify({ frames }) });
    return frames.map(f => f.name + ':' + f.nodes.length).join(' ');
  };
})();
