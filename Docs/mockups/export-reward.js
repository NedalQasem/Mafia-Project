/* Exports the visual parts of reward.html (the loot briefcase) as exact PNGs (transparent background) into Assets/_Project/Art/UI/Reward.
   Run in reward.html (served by Tools/ui-gen/serve.js):  load this script, then  await exportAll()   (see export-match.js for the method).
   Text is left out of every sprite: Unity draws it live (Lalezar, Cairo, Amiri), because web fonts do not load inside the SVG image. */
(function () {
  const css = () => [...document.querySelectorAll('style')].map(s => s.textContent).join('\n').replace(/body\{[^}]*\}/, '');

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
  const save = (name, data) => fetch('/save?dir=reward&name=' + encodeURIComponent(name), { method: 'POST', body: data }).then(r => r.text());

  const list = [];
  const add = (name, html, w, h, pad, scale, slice, extraCss) => list.push({ name, html, w, h, pad, scale, slice: slice || 0, extraCss });
  const at = 'position:absolute;inset:0;translate:none;margin:0;';

  // stage
  add('rw_dim', `<div class="dimbg" style="${at}opacity:1"></div>`, 390, 790, 0, 1);
  add('rw_spot', `<div class="spot" style="${at}opacity:1;transition:none"></div>`, 390, 790, 0, 1);
  add('rw_rays', `<div class="rays" style="${at}width:100%;height:100%;opacity:1;animation:none;transition:none"></div>`, 600, 600, 0, 1);
  add('rw_rule', `<div class="rule" style="${at}width:100%;height:100%;margin:0"></div>`, 180, 8, 0, 3);
  // the briefcase
  add('rw_case_body', `<div class="body" style="${at}"><i class="corner a"></i><i class="corner b"></i></div>`, 210, 104, 14, 3);
  add('rw_case_plate', `<div class="plate" style="${at}padding:0"></div>`, 96, 20, 3, 3);
  add('rw_case_shadow', `<div class="shadow" style="${at}"></div>`, 178, 22, 6, 2);
  add('rw_case_lid', `<div class="lid" style="position:absolute;left:0;right:0;top:24px;height:50px;transition:none"><div class="handle"></div></div>`, 210, 74, 6, 3);
  add('rw_latch', `<div class="latch" style="${at}transition:none"></div>`, 26, 30, 6, 3);
  add('rw_inside', `<div class="inside" style="${at}opacity:1;transition:none"></div>`, 194, 22, 40, 2);
  // the loot cards (face with ribbon, no text) and the sheen of the legendary one
  for (const r of ['common', 'rare', 'legend'])
    add('rw_card_' + r, `<div class="card ${r} out" style="${at}width:100%;height:100%;transform:none;opacity:1;transition:none"><div class="face" style="--x:0"><div class="rib"></div></div></div>`, 104, 144, 30, 3, 0, '.card.legend .face::after{display:none}');
  add('rw_sheen', `<div style="${at}background:linear-gradient(90deg,transparent,#fff6,transparent)"></div>`, 34, 200, 0, 2);
  for (const k of ['coins', 'gems', 'frame', 'bubble'])
    add('rw_art_' + k, `<div class="card"><div class="art" style="${at}width:100%;height:100%">${ART[k].replace('<svg ', '<svg xmlns="http://www.w3.org/2000/svg" ')}</div></div>`, 70, 70, 8, 5, 0, '.card{position:absolute;inset:0;opacity:1;transform:none;margin:0;width:100%;height:100%}');
  // flying tokens, the wallet pills and the summary chips
  add('rw_coin', `<i class="coin" style="display:block;width:100%;height:100%"></i>`, 20, 20, 3, 4);
  add('rw_gem', `<i class="gem" style="display:block;width:100%;height:100%"></i>`, 16, 16, 5, 4);
  add('rw_pill', `<div class="pill" style="width:100%;height:100%;padding:0"></div>`, 90, 30, 4, 3, 14);
  add('rw_chip', `<div class="sum" style="margin:0;display:block;width:100%;height:100%"><span style="display:block;width:100%;height:100%;padding:0"></span></div>`, 80, 26, 3, 3, 12);

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
