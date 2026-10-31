/* Exports the visual parts of family.html as exact PNGs (transparent background) into Assets/_Project/Art/UI/Family.
   Run in family.html (served by Tools/ui-gen/serve.js):  load this script, then  await exportAll()   (see export-match.js for the method). */
(function () {
  const css = () => [...document.querySelectorAll('style')].map(s => s.textContent).join('\n').replace(/body\{[^}]*\}/, '');
  const sym = id => { const n = document.getElementById(id); return n ? n.innerHTML : ''; };
  const svg = (id, style, cls) => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="24" height="24" ${cls ? 'class="' + cls + '"' : ''} style="${style || ''}">${sym(id)}</svg>`;

  async function raster(html, w, h, pad, scale, extraCss) {
    const W = w + 2 * pad, H = h + 2 * pad;
    const doc = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}"><foreignObject width="100%" height="100%"><div xmlns="http://www.w3.org/1999/xhtml" dir="rtl" style="position:relative;width:${W}px;height:${H}px;font-family:Cairo,Tahoma,sans-serif;direction:rtl"><style>${css()}${extraCss || ''}</style><div style="position:absolute;left:${pad}px;top:${pad}px;width:${w}px;height:${h}px">${html}</div></div></foreignObject></svg>`;
    const img = new Image();
    img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(doc);
    await img.decode();
    const c = document.createElement('canvas'); c.width = Math.round(W * scale); c.height = Math.round(H * scale);
    const g = c.getContext('2d'); g.scale(scale, scale); g.drawImage(img, 0, 0);
    { const id = g.getImageData(0, 0, c.width, c.height), d = id.data;   // Linear color space: remap alpha (see export-match.js)
      for (let i = 0; i < d.length; i += 4) {
        const a = d[i + 3]; if (a === 0 || a === 255) continue;
        const al = a / 255, lum = (0.2126 * d[i] + 0.7152 * d[i + 1] + 0.0722 * d[i + 2]) / 255;
        d[i + 3] = Math.round(((1 - lum) * (1 - Math.pow(1 - al, 2.2)) + lum * Math.pow(al, 2.2)) * 255);
      }
      g.putImageData(id, 0, 0); }
    return c.toDataURL('image/png');
  }
  const save = (name, data) => fetch('/save?dir=family&name=' + encodeURIComponent(name), { method: 'POST', body: data }).then(r => r.text());

  const list = [];
  const add = (name, html, w, h, pad, scale, slice, extraCss) => list.push({ name, html, w, h, pad, scale, slice: slice || 0, extraCss });
  const fill = 'width:100%;height:100%;padding:0;';
  const none = 'background:none;box-shadow:none;';

  add('bg', '<div style="position:absolute;inset:0;background:radial-gradient(ellipse 90% 30% at 50% 0,#e8c27a18,transparent 70%),linear-gradient(180deg,#15100d,#090605)"></div>', 390, 790, 0, 3);
  add('top_fade', '<div style="position:absolute;inset:0;background:linear-gradient(180deg,#0a0705f2 70%,transparent)"></div>', 390, 80, 0, 2);
  add('ic_btn', `<button class="ic" style="${fill}"></button>`, 38, 38, 8, 4);
  add('badge_dot', `<div class="ic" style="${none}display:block;width:100%;height:100%"><span class="dot" style="position:static;${fill}"></span></div>`, 16, 16, 4, 4);
  add('seg_bg', `<div class="seg" style="position:static;${fill}"></div>`, 100, 38, 6, 4, 10);
  add('seg_on', `<div class="seg" style="position:static;${none}padding:0;display:block;width:100%;height:100%"><button class="on" style="display:block;${fill}"></button></div>`, 100, 31, 6, 4, 7);
  add('seg_n', `<div class="seg" style="position:static;${none}padding:0;display:block;width:100%;height:100%"><i class="n" style="position:static;${fill}display:grid"></i></div>`, 17, 17, 4, 4);
  add('row_bg', `<div class="row" style="${fill}"></div>`, 200, 66, 18, 3, 12);
  add('av', `<div class="av" style="width:100%;height:100%">${svg('fed', '', 'h')}</div>`, 46, 46, 8, 4);
  for (const k of ['on', 'room', 'off']) add('st_' + k, `<div class="av" style="width:13px;height:13px;${none}"><i class="st ${k}" style="left:0;bottom:0;width:13px;height:13px"></i></div>`, 13, 13, 8, 4);
  add('lv_pill', `<span class="lv" style="display:block;${fill}"></span>`, 28, 16, 3, 4, 8);
  add('go_red', `<button class="go" style="display:block;${fill}"></button>`, 80, 34, 10, 4, 10);
  add('go_gold', `<button class="go gold" style="display:block;${fill}"></button>`, 80, 34, 10, 4, 10);
  add('go_teal', `<button class="go teal" style="display:block;${fill}"></button>`, 80, 34, 10, 4, 10);
  add('ghost', `<button class="ghost" style="display:block;${fill}"></button>`, 80, 32, 6, 4, 10);
  add('more_btn', `<button class="more" style="${fill}"></button>`, 32, 32, 4, 4);
  add('idc_bg', `<div class="idc" style="${fill}rotate:none;display:block"></div>`, 300, 74, 24, 3, 30);
  add('cp_btn', `<div class="idc" style="${none}padding:0;rotate:none;display:block;width:100%;height:100%"><button class="cp" style="margin:0;display:block;${fill}"></button></div>`, 54, 30, 4, 4, 7);
  add('field_bg', `<div class="field" style="${fill}"></div>`, 362, 50, 10, 3, 8);
  add('hint_bg', `<div class="hint" style="${fill}min-height:0;display:block"></div>`, 362, 50, 3, 3, 9);
  add('sheet_bg', `<div class="sheet" style="position:static;${fill}display:block"></div>`, 390, 300, 40, 2, 24);
  add('grab', `<div class="sheet" style="position:static;${none}padding:0;display:block;width:100%;height:100%"><div class="grab" style="width:100%;height:100%"></div></div>`, 44, 5, 2, 6);
  add('act', `<button class="act" style="display:block;${fill}"></button>`, 362, 56, 18, 3, 10);
  add('act_bad', `<button class="act bad" style="display:block;${fill}"></button>`, 362, 56, 18, 3, 10);
  add('act_hot', `<button class="act hot" style="display:block;${fill}"></button>`, 362, 56, 18, 3, 10);
  add('opt_bg', `<div class="opt" style="display:block;${fill}"></div>`, 362, 42, 4, 3, 10);
  add('opt_on', `<div class="opt on" style="display:block;${fill}"></div>`, 362, 42, 4, 3, 10);
  add('radio', `<div class="opt" style="${none}padding:0;display:block;width:100%;height:100%"><i style="display:block;width:100%;height:100%"></i></div>`, 20, 20, 6, 4);
  add('radio_on', `<div class="opt on" style="${none}padding:0;display:block;width:100%;height:100%"><i style="display:block;width:100%;height:100%"></i></div>`, 20, 20, 6, 4);
  add('note_bg', `<div class="note" style="${fill}display:block"></div>`, 362, 70, 6, 3, 8);
  add('tg_off', '<div class="tg"><i></i></div>', 52, 28, 12, 4);
  add('tg_on', '<div class="tg on"><i></i></div>', 52, 28, 12, 4);
  add('banner_bg', `<div class="banner" style="position:static;${fill}display:block"></div>`, 370, 64, 24, 3, 18);
  add('bar_fill', `<div class="banner" style="position:static;${none}padding:0;display:block;width:100%;height:100%"><div class="bar" style="position:static;${none}width:100%;height:100%"><i style="width:100%"></i></div></div>`, 100, 3, 1, 4);
  add('stamp', `<span class="stamp" style="display:block;${fill}rotate:none;opacity:1;color:#fff"></span>`, 100, 56, 4, 4, 14);
  // chat-first home (c1 c2 c3)
  add('ic_fr', `<button class="ic fr" style="${fill}"></button>`, 44, 44, 8, 4);
  add('ctab_off', `<button class="ctab" style="position:static;display:block;${fill}"></button>`, 110, 36, 6, 4, 10);
  add('ctab_on', `<button class="ctab on" style="position:static;display:block;${fill}"></button>`, 110, 36, 14, 4, 10);
  add('cbd', `<i class="cbd" style="display:block;${fill}"></i>`, 17, 17, 4, 4);
  add('av_grp', `<div class="av grp" style="width:100%;height:100%">${svg('grp', 'width:24px;height:24px')}</div>`, 46, 46, 8, 4);
  add('av_add', `<div class="pp add" style="width:100%;height:100%;display:block"><div class="av" style="width:100%;height:100%">${svg('plus', 'width:22px;height:22px')}</div></div>`, 50, 50, 8, 4);
  add('letter_bg', `<div class="letter" style="margin:0;rotate:none;display:block;${fill}"></div>`, 362, 140, 30, 3, 24);
  add('big_red', `<button class="big red" style="display:block;${fill}"></button>`, 362, 48, 10, 3, 12);
  add('big_dim', `<button class="big dim2" style="display:block;${fill}"></button>`, 362, 48, 6, 3, 12);
  add('newg_bg', `<button class="newg" style="display:block;${fill}"></button>`, 362, 52, 4, 3, 14);
  add('coach_bg', `<div class="coach" style="position:static;rotate:none;display:block;max-width:none;${fill}"></div>`, 210, 60, 16, 3, 10);
  for (const id of ['back', 'plus', 'dots', 'door', 'mail', 'chat', 'user-x', 'flag', 'block', 'copy', 'info', 'search', 'check', 'friends', 'useradd', 'square', 'grp'])
    add('icon_' + id, svg(id, 'stroke:#fff;fill:none;stroke-width:2;stroke-linecap:round;stroke-linejoin:round'), 24, 24, 2, 6);

  window.exportAll = async function () {
    const manifest = {};
    for (const it of list) {
      await save(it.name + '.png', await raster(it.html, it.w, it.h, it.pad, it.scale, it.extraCss));
      manifest[it.name] = { w: it.w, h: it.h, pad: it.pad, scale: it.scale, slice: it.slice };
    }
    await save('manifest.json', JSON.stringify(manifest, null, 1));
    return Object.keys(manifest).length + ' sprites';
  };
})();
