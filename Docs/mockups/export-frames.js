/* Renders every match.html frame to a PNG with the real web fonts (so it can be compared with the Unity capture).
   Fonts are fetched once and embedded as data URLs, because an SVG image cannot load web fonts by itself.
   Load this script in match.html, then  await exportFrames()  (writes Assets/Screenshots/h_<frame>.png). */
(function () {
  let fontCss = null;
  async function loadFonts() {
    if (fontCss) return fontCss;
    const url = 'https://fonts.googleapis.com/css2?family=Lalezar&family=Cairo:wght@600;800&family=Aref+Ruqaa:wght@700&display=swap';
    const css = await (await fetch(url)).text();
    const blocks = css.split('@font-face').slice(1);
    let out = '';
    for (const b of blocks) {
      const m = b.match(/url\(([^)]+)\)/);
      if (!m) continue;
      const buf = await (await fetch(m[1])).arrayBuffer();
      let bin = ''; const bytes = new Uint8Array(buf);
      for (let i = 0; i < bytes.length; i += 0x8000) bin += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
      out += '@font-face' + b.replace(m[0], 'url(data:font/woff2;base64,' + btoa(bin) + ')');
    }
    fontCss = out;
    return fontCss;
  }
  async function frameToPng(phone, scale) {
    const fonts = await loadFonts();
    const css = [...document.querySelectorAll('style')].map(s => s.textContent).join('\n').replace(/body\{[^}]*\}/, '');
    const defs = new XMLSerializer().serializeToString(document.querySelector('body > svg'));
    const cl = phone.cloneNode(true);
    cl.removeAttribute('id');
    cl.setAttribute('style', 'position:absolute;left:0;top:0;width:390px;height:790px;border-radius:0;box-shadow:none;');
    const html = new XMLSerializer().serializeToString(cl);
    const doc = `<svg xmlns="http://www.w3.org/2000/svg" width="390" height="790"><foreignObject width="100%" height="100%"><div xmlns="http://www.w3.org/1999/xhtml" dir="rtl" lang="ar" style="position:relative;width:390px;height:790px;overflow:hidden;direction:rtl;color:#d9d0bd;font-family:Cairo,Tahoma,sans-serif"><style>${fonts}${css}</style>${defs}${html}</div></foreignObject></svg>`;
    const img = new Image(); img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(doc);
    await img.decode();
    await document.fonts.ready;
    const c = document.createElement('canvas'); c.width = 390 * scale; c.height = 790 * scale;
    const g = c.getContext('2d'); g.scale(scale, scale); g.drawImage(img, 0, 0);
    return c.toDataURL('image/png');
  }
  // white text on transparent, 12px padding around the line box, 4 texels per CSS px
  window.exportTextSprites = async function () {
    const fonts = await loadFonts(); const pad = 12, scale = 4; const done = [];
    const css = [...document.querySelectorAll('style')].map(s => s.textContent).join('\n').replace(/body\{[^}]*\}/, '');
    for (const [name, t] of Object.entries(window.__textimgs || {})) {
      const W = Math.ceil(t.w) + 2 * pad, H = Math.ceil(t.h) + 2 * pad, w = Math.ceil(t.w);
      const al = t.align === 'center' ? 'center' : (t.align === 'left' || t.align === 'end') ? 'left' : 'right';
      const doc = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}"><foreignObject width="100%" height="100%"><div xmlns="http://www.w3.org/1999/xhtml" dir="rtl" lang="ar" style="position:relative;width:${W}px;height:${H}px;direction:rtl"><style>${fonts}</style><div style="position:absolute;left:${pad}px;top:${pad}px;width:${w + 2}px;height:${Math.ceil(t.h)}px;line-height:${Math.ceil(t.h)}px;white-space:nowrap;font-family:'Aref Ruqaa';font-weight:${t.fw};font-size:${t.fs}px;color:#fff;text-align:${al}">${t.text}</div></div></foreignObject></svg>`;
      const img = new Image(); img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(doc); await img.decode();
      const c = document.createElement('canvas'); c.width = W * scale; c.height = H * scale;
      const g = c.getContext('2d'); g.scale(scale, scale); g.drawImage(img, 0, 0);
    { const id = g.getImageData(0, 0, c.width, c.height), d = id.data;
      for (let i = 0; i < d.length; i += 4) {
        const a = d[i + 3]; if (a === 0 || a === 255) continue;
        const al = a / 255, lum = (0.2126 * d[i] + 0.7152 * d[i + 1] + 0.0722 * d[i + 2]) / 255;
        d[i + 3] = Math.round(((1 - lum) * (1 - Math.pow(1 - al, 2.2)) + lum * Math.pow(al, 2.2)) * 255);
      }
      g.putImageData(id, 0, 0); }
      await fetch('/save?' + (window.__saveDir ? 'dir=' + window.__saveDir + '&' : '') + 'name=' + encodeURIComponent(name + '.png'), { method: 'POST', body: c.toDataURL('image/png') });
      done.push(name);
    }
    await fetch('/save?' + (window.__saveDir ? 'dir=' + window.__saveDir + '&' : '') + 'name=textimgs.json', { method: 'POST', body: JSON.stringify({ pad, scale, names: done }) });
    return done.length + ' text sprites';
  };
  window.__frameToPng = frameToPng;
  /* any page: renders each given phone element to Assets/Screenshots/<prefix><id>.png */
  window.exportPhones = async function (ids, prefix) {
    const out = [];
    for (const id of ids) { const data = await frameToPng(document.getElementById(id), 2); await fetch("/save?dir=shots&name=" + encodeURIComponent((prefix || "s_") + id + ".png"), { method: "POST", body: data }); out.push(id); }
    return out.join(" ");
  };
  window.exportFrames = async function (only) {
    const variants = [
      ['p1', '#t1', ['mafia', 'doc', 'sher', 'civ']], ['p2', '#t2', ['mafia', 'doc', 'sher', 'civ']], ['p3', '#t3', ['kill', 'save']],
      ['p4'], ['p5'], ['p6', '#t6', ['civ', 'mafia']], ['p7'], ['p8', '#t8', ['civ', 'mafia']]];
    const done = [];
    for (const [id, tab, keys] of variants) {
      for (const key of (keys || [''])) {
        const name = id + (key ? '_' + key : '');
        if (only && !only.includes(name)) continue;
        if (tab) { document.querySelector(tab + ' button[data-k=' + key + ']').click(); await new Promise(r => setTimeout(r, 30)); }
        const data = await frameToPng(document.getElementById(id), 2);
        await fetch('/save?dir=shots&name=' + encodeURIComponent('h_' + name + '.png'), { method: 'POST', body: data });
        done.push(name);
      }
    }
    return done.join(' ');
  };
})();
