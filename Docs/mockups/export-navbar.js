/* Renders the navbar.html ideas to PNGs with the real web fonts (the browser pane's own screenshots are low-res).
   Load this script in navbar.html, then  await shotRow('nav_ar')  -> Assets/Screenshots/nav_ar.png  (the whole row of phones),
   or  await shotBars('nav_bars')  -> only the bottom 150px of every phone, side by side, at 3x. */
(function () {
  let fontCss = null;
  async function loadFonts() {
    if (fontCss) return fontCss;
    const css = await (await fetch(document.querySelector('link[rel=stylesheet]').href)).text();
    let out = '';
    for (const b of css.split('@font-face').slice(1)) {
      const m = b.match(/url\(([^)]+)\)/); if (!m) continue;
      const bytes = new Uint8Array(await (await fetch(m[1])).arrayBuffer()); let bin = '';
      for (let i = 0; i < bytes.length; i += 0x8000) bin += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
      out += '@font-face' + b.replace(m[0], 'url(data:font/woff2;base64,' + btoa(bin) + ')');
    }
    return (fontCss = out);
  }
  async function render(el, scale, crop) {
    const fonts = await loadFonts();
    const css = [...document.querySelectorAll('style')].map(s => s.textContent).join('\n');
    const defs = new XMLSerializer().serializeToString(document.querySelector('body > svg'));
    const r = el.getBoundingClientRect(), W = Math.ceil(r.width), H = Math.ceil(r.height);
    const cl = el.cloneNode(true); cl.style.width = W + 'px'; cl.style.height = H + 'px'; cl.style.margin = '0';   // body.* size rules don't reach the copy
    const html = new XMLSerializer().serializeToString(cl);
    const doc = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}"><foreignObject width="100%" height="100%"><div xmlns="http://www.w3.org/1999/xhtml" class="${document.body.className}" dir="${document.documentElement.dir}" style="position:relative;width:${W}px;height:${H}px;background:#050404;color:#d9d0bd;font-family:Cairo,Tahoma,sans-serif;padding:0"><style>${fonts}${css.replace(/body\{[^}]*\}/, '')}*,*::before,*::after{animation:none!important}</style>${defs}${html}</div></foreignObject></svg>`;
    const img = new Image(); img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(doc); await img.decode();
    const [cx, cy, cw, ch] = crop || [0, 0, W, H];
    const c = document.createElement('canvas'); c.width = cw * scale; c.height = ch * scale;
    const g = c.getContext('2d'); g.scale(scale, scale); g.drawImage(img, -cx, -cy);
    return c;
  }
  const save = (name, c) => fetch('/save?dir=shots&name=' + encodeURIComponent(name + '.png'), { method: 'POST', body: c.toDataURL('image/png') });
  window.shotRow = async name => { const c = await render(document.getElementById('row'), 2); await save(name, c); return c.width + 'x' + c.height; };
  window.shotBars = async (name, only) => {   // only = bar letters, e.g. 'DE'
    const phones = [...document.querySelectorAll('.phone')].filter(p => !only || only.includes(p.querySelector('.bar').classList[1])), scale = 3, h = 150, gap = 16;
    const parts = [];
    for (const p of phones) { const r = p.getBoundingClientRect(); parts.push(await render(p, scale, [0, r.height - h, r.width, h])); }
    const out = document.createElement('canvas'); out.width = parts.reduce((s, c) => s + c.width, 0) + gap * scale * (parts.length - 1); out.height = h * scale;
    const g = out.getContext('2d'); g.fillStyle = '#050404'; g.fillRect(0, 0, out.width, out.height);
    let x = out.width; for (const c of parts) { x -= c.width; g.drawImage(c, x, 0); x -= gap * scale; }   // right to left, like the page
    await save(name, out); return out.width + 'x' + out.height;
  };
})();
