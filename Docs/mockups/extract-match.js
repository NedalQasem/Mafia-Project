/* Extracts the real layout of every match.html frame (what the browser computed) into frames.json for the Unity builder.
   Run in the mockup page: load this script, then  await extractAll()  (saves Assets/_Project/Art/UI/Match/frames.json via the local server).
   Each node: i (index), p (parent index), x y w h (CSS px, relative to the parent's box), rot (deg), op (opacity), z, kind: box | text | icon,
   sprite + tint for visuals, and text fields. Sprites come from export-match.js. */
(function () {
  const rgba = s => {
    const m = (s || '').match(/rgba?\(([^)]+)\)/);
    if (!m) return null;
    const p = m[1].split(/[ ,\/]+/).filter(Boolean).map(Number);
    return [p[0], p[1], p[2], p.length > 3 ? p[3] : 1];
  };
  const hex = c => '#' + c.slice(0, 3).map(v => Math.round(v).toString(16).padStart(2, '0')).join('') + Math.round((c[3] === undefined ? 1 : c[3]) * 255).toString(16).padStart(2, '0');
  const darken = c => { const g = (c[0] + c[1] + c[2]) / 3; const m = .55; return [g * m, g * m, g * m, c[3]]; };

  function spriteFor(el, cs, phoneCls, dark) {
    const c = el.classList, has = k => c.contains(k);
    const seat = el.closest('.seat');
    if (has('phone')) return { s: phoneCls.includes('night') ? 'bg_night' : phoneCls.includes('day') ? 'bg_day' : 'bg_default', stretch: true };
    if (has('felt')) return { s: 'felt', t: el.closest('.sleepy') ? '#666666ff' : '' };
    if (has('ring')) return { s: 'ring_bg' };
    if (has('pill')) return { s: has('mafia') ? 'pill_mafia' : has('warn') ? 'pill_warn' : 'pill' };
    if (has('av')) {
      if (el.closest('.role-row')) return { s: 'av_small' };
      if (el.closest('.mate')) return { s: 'av_mate' };
      if (seat) {
        const sc = seat.classList, me = sc.contains('me');
        if (sc.contains('dead')) return { s: me ? 'av_me_dead' : 'av_dead' };
        if (sc.contains('sleep')) return { s: me ? 'av_me_sleep' : 'av_sleep' };
        if (sc.contains('pick')) return { s: sc.contains('doc') ? 'av_pick_doc' : sc.contains('sher') ? 'av_pick_sher' : 'av_pick' };
        if (sc.contains('talk')) return { s: 'av_talk' };
        return { s: me ? 'av_me' : 'av' };
      }
    }
    if (has('tag')) return { s: has('mafia') ? 'tag_mafia' : has('gold') ? 'tag_gold' : 'tag_red' };
    if (has('nm')) return { s: 'nametag', t: dark ? '#8c8c8cff' : '' };
    if (has('bub')) return { s: 'bubble' };
    if (has('card')) return { s: 'card' };
    if (has('go')) return { s: has('gold') ? 'go_gold' : has('teal') ? 'go_teal' : 'go_red' };
    if (has('ghost')) return { s: 'ghost' };
    if (has('mic')) return { s: has('off') ? 'mic_off' : 'mic' };
    if (has('paper')) return { s: 'paper' };
    if (has('stamp')) return { s: 'stamp', t: hex(rgba(cs.color)) };
    if (has('banner')) return { s: has('mafia') ? 'banner_mafia' : 'banner_civ' };
    if (has('role-row')) return { s: 'role_row' };
    if (has('chip')) return { s: 'chip', t: hex(rgba(cs.color)) };
    if (el.parentElement && el.parentElement.classList.contains('rew') && el.tagName === 'DIV') return { s: 'rew_box' };
    if (el.tagName === 'SPAN' && el.parentElement && el.parentElement.classList.contains('tally')) return { s: (el.getAttribute('style') || '').includes('#0001') ? 'tally_light' : 'tally' };
    if (has('dim-layer')) {
      const bg = el.getAttribute('style') || '';
      if (bg.includes('#000b')) return { s: '', t: '#000000bb', stretch: true };
      if (bg.includes('40%')) return { s: 'dim_radial_civ', stretch: true };
      return { s: 'dim_radial', stretch: true };
    }
    if (has('bot')) return { s: phoneCls.includes('night') ? 'fade_night' : 'fade_day', bottom: true };
    return null;
  }

  const SKIP = new Set(['STYLE', 'SCRIPT', 'defs', 'symbol']);

  function extract(phone) {
    const phoneCls = phone.className;
    const nodes = [];
    const pr = phone.getBoundingClientRect();

    function add(n) { n.i = nodes.length; nodes.push(n); return n.i; }

    function walk(el, parentRect, pi, ctx) {
      const cs = getComputedStyle(el);
      if (cs.display === 'none') return;
      const r = el.getBoundingClientRect();
      const dark = ctx.dark || (cs.filter || '').includes('grayscale');
      const n = { p: pi, name: el.tagName.toLowerCase() + (el.className && el.className.baseVal === undefined ? '.' + String(el.className).split(' ').join('.') : ''), kind: 'box',
        x: r.left - parentRect.left, y: r.top - parentRect.top, w: r.width, h: r.height,
        rot: parseFloat(el.dataset.rot || '0') || 0, op: Math.min(1, parseFloat(cs.opacity)), z: cs.position !== 'static' ? (parseInt(cs.zIndex) || 0) : 0 };

      if (el.tagName.toLowerCase() === 'svg') {
        if (el.closest('.av')) return;   // baked into the avatar sprites
        const use = el.querySelector('use'); const id = use ? (use.getAttribute('href') || '').replace('#', '') : '';
        n.kind = 'icon';
        const col = rgba(cs.stroke) || rgba(cs.fill) || [255, 255, 255, 1];
        n.sprite = el.classList.contains('moon') ? 'moon' : 'icon_' + id;
        n.tint = el.classList.contains('moon') ? '' : hex(col);
        add(n); return;
      }

      const sp = spriteFor(el, cs, phoneCls, dark);
      if (sp) { n.sprite = sp.s; n.tint = sp.t || ''; n.stretch = !!sp.stretch; n.bottom = !!sp.bottom; }
      if (el.classList.contains('ring')) {
        const p = parseFloat(el.style.getPropertyValue('--p')) || 0;
        n.arc = { p: p / 100, c: (el.style.getPropertyValue('--c') || '#ffb35a').trim() };
      }
      const idx = add(n);
      const me = { dark };

      // dashed / solid borders drawn by CSS (paper dividers, newspaper header)
      for (const side of (sp ? [] : ['Top', 'Bottom'])) {
        const w = parseFloat(cs['border' + side + 'Width']);
        if (w > 0 && cs['border' + side + 'Style'] !== 'none') {
          add({ p: idx, name: 'line', kind: 'line', x: 0, y: side === 'Top' ? 0 : r.height - w, w: r.width, h: w, rot: 0, op: 1, z: 0,
            tint: hex(rgba(cs['border' + side + 'Color']) || [255, 255, 255, 1]), dashed: cs['border' + side + 'Style'] === 'dashed' });
        }
      }
      // the "z" of a sleeping seat
      if (el.classList.contains('av') && el.closest('.seat.sleep')) {
        add({ p: idx, name: 'sleepz', kind: 'text', x: -4, y: -6, w: 14, h: 18, rot: 0, op: 1, z: 0, text: 'z', fs: 16, fw: 400, color: '#9bb0ffff', align: 'center', font: 'head', wrap: false, lh: 18 });
      }

      // children in DOM order (z-index sorted later), including text nodes
      const kids = [];
      el.childNodes.forEach(ch => kids.push(ch));
      const ordered = kids.map((ch, k) => {
        let z = 0;
        if (ch.nodeType === 1) { const c2 = getComputedStyle(ch); z = c2.position !== 'static' ? (parseInt(c2.zIndex) || 0) : 0; }
        return { ch, k, z };
      }).sort((a, b) => a.z - b.z || a.k - b.k);
      for (const { ch } of ordered) {
        if (ch.nodeType === 1) { if (!SKIP.has(ch.tagName)) walk(ch, r, idx, me); }
        else if (ch.nodeType === 3 && ch.textContent.trim()) {
          const range = document.createRange(); range.selectNodeContents(ch);
          const rr = range.getBoundingClientRect(); const lines = new Set([...range.getClientRects()].map(q => Math.round(q.top / 4))).size;   // distinct rows (bidi text gives several rects per row)
          let color = rgba(cs.color) || [255, 255, 255, 1];
          if (el.closest('.sleepy') && el.closest('.felt')) { const g = (color[0] + color[1] + color[2]) / 3; color = [(color[0] * .7 + g * .3) * .4, (color[1] * .7 + g * .3) * .4, (color[2] * .7 + g * .3) * .4, color[3]]; }
          const ff = cs.fontFamily;
          const fontKey = ff.includes('Lalezar') ? 'head' : ff.includes('Aref') ? 'calli' : 'body';
          if (fontKey === 'calli') {   // drawn from the web font into a PNG (export-frames.js: exportTextSprites)
            const txt = ch.textContent.replace(/\s+/g, ' ').trim(); let hsh = 5381; const keyStr = txt + '|' + cs.fontSize + '|' + cs.fontWeight;
            for (let q = 0; q < keyStr.length; q++) hsh = ((hsh * 33) ^ keyStr.charCodeAt(q)) >>> 0;
            const sprite = 't_' + hsh.toString(16);
            (window.__textimgs = window.__textimgs || {})[sprite] = { text: txt, fs: parseFloat(cs.fontSize), fw: parseInt(cs.fontWeight) || 400, w: rr.width, h: rr.height, align: cs.textAlign };
            add({ p: idx, name: 'textimg', kind: 'textimg', x: rr.left - r.left, y: rr.top - r.top, w: rr.width, h: rr.height, rot: 0, op: 1, z: 0, sprite, tint: hex(dark ? darken(color) : color) });
            continue;
          }
          add({ p: idx, name: 'text', kind: 'text', x: rr.left - r.left, y: rr.top - r.top, w: rr.width, h: rr.height, rot: 0, op: 1, z: 0,
            text: ch.textContent.replace(/\s+/g, ' ').trim(), fs: parseFloat(cs.fontSize), fw: parseInt(cs.fontWeight) || 400,
            color: hex(dark ? darken(color) : color), align: cs.textAlign, dir: cs.direction,
            font: ff.includes('Lalezar') ? 'head' : ff.includes('Aref') ? 'calli' : 'body', wrap: lines > 1, lh: rr.height / Math.max(1, lines) });
        }
      }
    }

    // root
    nodes.length = 0;
    const root = { p: -1, name: 'phone', kind: 'box', x: 0, y: 0, w: pr.width, h: pr.height, rot: 0, op: 1, z: 0, sprite: phoneCls.includes('night') ? 'bg_night' : phoneCls.includes('day') ? 'bg_day' : 'bg_default', tint: '', stretch: true };
    add(root);
    const kids = [...phone.children].map((ch, k) => { const c2 = getComputedStyle(ch); return { ch, k, z: c2.position !== 'static' ? (parseInt(c2.zIndex) || 0) : 0 }; }).sort((a, b) => a.z - b.z || a.k - b.k);
    for (const { ch } of kids) walk(ch, pr, 0, { dark: false });
    return nodes;
  }

  window.extractAll = async function () {
    const style = document.createElement('style'); style.id = '__norot';
    const variants = [
      ['p1', '#t1', ['mafia', 'doc', 'sher', 'civ']], ['p2', '#t2', ['mafia', 'doc', 'sher', 'civ']], ['p3', '#t3', ['kill', 'save']],
      ['p4'], ['p5'], ['p6', '#t6', ['civ', 'mafia']], ['p7'], ['p8', '#t8', ['civ', 'mafia']]];
    const frames = [];
    for (const [id, tab, keys] of variants) {
      for (const key of (keys || [''])) {
        if (tab) { document.querySelector(tab + ' button[data-k=' + key + ']').click(); await new Promise(r => setTimeout(r, 30)); }
        const phone = document.getElementById(id);
        phone.querySelectorAll('*').forEach(e => { e.dataset.rot = getComputedStyle(e).rotate === 'none' ? '0' : getComputedStyle(e).rotate; });
        style.textContent = '#' + id + ' *{rotate:none!important}';
        document.head.appendChild(style);
        await new Promise(r => setTimeout(r, 30));
        frames.push({ name: id + (key ? '_' + key : ''), w: 390, h: 790, nodes: extract(phone) });
        style.remove();
      }
    }
    await fetch('/save?name=frames.json', { method: 'POST', body: JSON.stringify({ frames }) });
    return frames.map(f => f.name + ':' + f.nodes.length).join(' ');
  };
})();
