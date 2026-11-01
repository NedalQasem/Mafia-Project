/* Exports the visual parts of quick-notify.html (the telegram that slides out of the quick menu tab) as exact PNGs (transparent background) into Assets/_Project/Art/UI/QuickNotice.
   Run in quick-notify.html (served by Tools/ui-gen/serve.js):  load this script, then  await exportAll()   (see export-match.js for the method).
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
  const save = (name, data) => fetch('/save?dir=notice&name=' + encodeURIComponent(name), { method: 'POST', body: data }).then(r => r.text());

  const list = [];
  const add = (name, html, w, h, pad, scale, slice, extraCss) => list.push({ name, html, w, h, pad, scale, slice: slice || 0, extraCss });
  const at = 'position:absolute;inset:0;translate:none;margin:0;';
  const bare = '.slip::before,.slip::after{display:none}';
  const loose = 'translate:none;rotate:none;transition:none;';

  // the telegram (paper, grain, dashed ink border, shadow); Unity tilts it 1 degree towards the tab's side
  add('qn_slip', `<div class="slip" style="${at}${loose}width:100%;height:100%;padding:0"></div>`, 256, 58, 14, 3);
  // one wax seal per kind, with its icon
  for (const k of Object.keys(ICON))
    add('qn_seal_' + k, `<div class="slip k-${k}" style="${at}${loose}width:100%;height:100%;display:block;padding:0;background:none;box-shadow:none"><div class="seal" style="width:100%;height:100%;animation:none">${ICON[k].replace('<svg ', '<svg xmlns="http://www.w3.org/2000/svg" ')}</div></div>`, 38, 38, 6, 4, 0, bare);
  // the "+2" ink stamp frame (sliced) and the dotted time line
  add('qn_stamp', `<div style="${at}border:1.5px solid #8a1c24;border-radius:3px;opacity:.85"></div>`, 30, 18, 2, 4, 6);
  add('qn_time', `<div style="position:absolute;left:0;right:0;top:0;border-top:1.5px dotted #1c170f88"></div>`, 180, 2, 1, 3);

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
