/* bag.html -> Unity: sprites (transparent, alpha remapped for the Linear colour space) into Assets/_Project/Art/UI/Bag,
   and the measured layout of every part (bag-layout.json there; CSS px, origin at the top-left of the part's parent, phone 390 wide).
   Load in bag.html (served by Tools/ui-gen/serve.js), then  await exportBag()  and  await measureBag()  */
(function () {
  let fontCss = null;
  async function fonts() {
    if (fontCss) return fontCss;
    const css = await (await fetch(document.querySelector('link[rel=stylesheet]').href)).text(); let out = '';
    for (const b of css.split('@font-face').slice(1)) {
      const m = b.match(/url\(([^)]+)\)/); if (!m) continue;
      const bytes = new Uint8Array(await (await fetch(m[1])).arrayBuffer()); let bin = '';
      for (let i = 0; i < bytes.length; i += 0x8000) bin += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
      out += '@font-face' + b.replace(m[0], 'url(data:font/woff2;base64,' + btoa(bin) + ')');
    }
    return (fontCss = out);
  }
  const pageCss = () => [...document.querySelectorAll('style')].map(s => s.textContent).join('\n').replace(/body\{[^}]*\}/, '');
  async function raster(html, W, H, scale, extra, remap = true) {
    const defs = new XMLSerializer().serializeToString(document.querySelector('body > svg'));
    const doc = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}"><foreignObject width="100%" height="100%"><div xmlns="http://www.w3.org/1999/xhtml" dir="rtl" style="position:relative;width:${W}px;height:${H}px;font-family:Cairo,Tahoma,sans-serif;direction:rtl"><style>${await fonts()}${pageCss()}*,*::before,*::after{animation:none!important}${extra || ''}</style>${defs}${html}</div></foreignObject></svg>`;
    const img = new Image(); img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(doc); await img.decode();
    const c = document.createElement('canvas'); c.width = Math.round(W * scale); c.height = Math.round(H * scale);
    const g = c.getContext('2d'); g.scale(scale, scale); g.drawImage(img, 0, 0);
    if (remap) {
      const id = g.getImageData(0, 0, c.width, c.height), d = id.data;
      for (let i = 0; i < d.length; i += 4) {
        const a = d[i + 3]; if (a === 0 || a === 255) continue;
        const al = a / 255, lum = (0.2126 * d[i] + 0.7152 * d[i + 1] + 0.0722 * d[i + 2]) / 255;
        d[i + 3] = Math.round(((1 - lum) * (1 - Math.pow(1 - al, 2.2)) + lum * Math.pow(al, 2.2)) * 255);
      }
      g.putImageData(id, 0, 0);
    }
    return c.toDataURL('image/png');
  }
  const save = (name, body) => fetch('/save?dir=bag&name=' + encodeURIComponent(name), { method: 'POST', body }).then(r => r.text());
  const box = (cls, w, h, pad, style) => `<div class="${cls}" style="position:absolute;left:${pad}px;top:${pad}px;right:auto;bottom:auto;width:${w}px;height:${h}px;margin:0;${style || ''}"></div>`;   // right/bottom auto: in RTL a set right would win over left

  window.exportBag = async function () {
    const out = [];
    // name, html, W, H, scale; every sliced sprite is drawn at a size where only its middle stretches
    const put = async (name, html, W, H, scale, extra) => { await save(name + '.png', await raster(html, W, H, scale, extra)); out.push(name); };
    await put('bag_bg', '<div class="phone" style="position:absolute;inset:0;border-radius:0;box-shadow:none"></div>', 390, 790, 2, '', false);
    await put('bag_lid', '<div class="lid" style="height:100px"></div>', 390, 130, 3);                       // 30px under it for the shadow
    await put('bag_clasp', '<span class="clasp" style="position:absolute;left:6px;top:6px;bottom:auto"></span>', 42, 30, 6);
    await put('bag_ic', box('ic', 38, 38, 8), 54, 54, 5);
    await put('bag_back', '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" style="position:absolute;left:2px;top:2px;width:18px;height:18px;stroke:#cfc6b2;fill:none;stroke-width:2.2;stroke-linecap:round;stroke-linejoin:round"><use href="#back"/></svg>', 22, 22, 8);
    await put('bag_count', box('count', 60, 22, 8, 'rotate:none;padding:0'), 76, 40, 5);                       // paper tag, sliced 12
    await put('bag_look', box('look', 120, 90, 8, 'display:block;padding:0'), 136, 106, 4);                    // sliced 22
    await put('bag_bit', box('bit', 60, 18, 2, 'display:block;padding:0'), 64, 22, 6);                         // sliced 10
    await put('bag_chip', box('chip', 80, 30, 2, 'display:block;padding:0'), 84, 34, 6);                       // sliced 16
    await put('bag_chip_on', box('chip on', 80, 30, 2, 'display:block;padding:0'), 84, 36, 6);
    await put('bag_chip_n', '<i style="position:absolute;left:2px;top:2px;width:18px;height:18px;border-radius:9px;background:#000a"></i>', 22, 22, 6);
    await put('bag_cell', box('cell', 114.67, 104, 6, 'display:block;padding:0'), 126.67, 116, 4);
    await put('bag_cell_on', box('cell on', 114.67, 104, 10, 'display:block;padding:0'), 134.67, 124, 4);
    await put('bag_dot', '<i style="position:absolute;left:3px;top:3px;width:7px;height:7px;border-radius:50%;background:#fff"></i>', 13, 13, 8, '', );
    await put('bag_tag', box('tagw', 44, 16, 8, 'rotate:none;padding:0'), 64, 34, 6);                          // the "worn" tag with its string hole, sliced 10
    await put('bag_empty', box('empty', 120, 120, 4, 'display:block;padding:0;margin:0'), 128, 128, 4);       // sliced 18
    await put('bag_case', '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 96" style="position:absolute;left:0;top:0;width:96px;height:96px"><use href="#caseopen"/></svg>', 96, 96, 4);
    await put('bag_btn', box('btn', 120, 42, 6, 'display:block;padding:0'), 132, 56, 4);                         // sliced 14
    await put('bag_btn_dark', box('btn dark', 120, 42, 6, 'display:block;padding:0'), 132, 56, 4);
    await put('bag_sheet', box('sheet', 200, 120, 30, 'position:absolute;display:block;padding:0'), 260, 150, 3);     // sliced 40 (top corners + the brass edge)
    await put('bag_stage', box('stage', 120, 120, 2, 'position:absolute;display:block'), 124, 124, 4, '.stage{border-radius:12px;background:radial-gradient(ellipse 60% 70% at 50% 45%,#4a1016,#1e0508);box-shadow:inset 0 6px 14px #000d}');
    await put('bag_rr', '<span style="position:absolute;left:2px;top:2px;width:40px;height:18px;border-radius:9px;background:#8a1c24"></span>', 44, 22, 6);
    return out.join(' ');
  };

  window.measureBag = async function () {
    const force = document.createElement('style'); force.textContent = '.col{width:390px!important}.phone{width:390px!important}'; document.head.appendChild(force);
    await new Promise(r => setTimeout(r, 120));
    const R = (el, rel) => { if (!el) return null; const a = el.getBoundingClientRect(), b = rel.getBoundingClientRect(); return [+(a.left - b.left).toFixed(2), +(a.top - b.top).toFixed(2), +a.width.toFixed(2), +a.height.toFixed(2)]; };
    const T = (el, rel) => { if (!el) return null; const g = document.createRange(); g.selectNodeContents(el); const a = g.getBoundingClientRect(), b = rel.getBoundingClientRect(); return [+(a.left - b.left).toFixed(2), +(a.top - b.top).toFixed(2), +a.width.toFixed(2), +a.height.toFixed(2)]; };
    const p1 = document.getElementById('p1'), p2 = document.getElementById('p2'), p3 = document.getElementById('p3');
    const q = (p, s) => p.querySelector(s);
    const look = q(p1, '.look'), chip = q(p1, '.chip.on'), chipOff = q(p1, '.chip:not(.on)'), sec = q(p1, '.sec'), cellOn = q(p1, '.cell.on'), cell = q(p1, '.cell:not(.on)'), grid = q(p1, '.grid');
    const sheet = q(p2, '.sheet'), empty = q(p3, '.empty');
    const res = {
      lid: R(q(p1, '.lid'), p1), clasps: [...p1.querySelectorAll('.clasp')].map(c => R(c, p1)), ic: R(q(p1, '.ic'), p1), title: T(q(p1, '.ttl').firstChild, p1), sub: R(q(p1, '.ttl small'), p1), count: R(q(p1, '.count'), p1),
      scroll: R(q(p1, '.scroll'), p1), look: R(look, p1),
      lookIn: { fr: R(q(look, '.fr'), look), lbl: R(q(look, '.lbl'), look), name: R(q(look, 'b'), look), bits: R(q(look, '.bits'), look), bit: R(q(look, '.bit'), look) },
      chips: R(q(p1, '.chips'), p1), chip: R(chip, chip.parentElement), chipIn: { text: T(chip.firstChild, chip), n: R(q(chip, 'i'), chip) }, chipOff: R(chipOff, chip.parentElement),
      sec: R(sec, p1), secIn: { title: T(sec.firstChild, sec), small: R(q(sec, 'small'), sec) },
      grid: R(grid, p1), cell: R(cell, grid), cellOn: R(cellOn, grid),
      cellIn: { art: R(q(cell, '.art'), cell), nm: R(q(cell, '.nm'), cell), rar: R(q(cell, '.rar'), cell), tag: R(q(cellOn, '.tagw'), cellOn) },
      gaps: { scroll: 12, grid: 9, chips: 6 },
      sheet: R(sheet, p2), sheetIn: { hd: R(q(sheet, '.hd'), sheet), stage: R(q(sheet, '.stage'), sheet), name: R(q(sheet, 'b'), sheet), meta: R(q(sheet, '.meta'), sheet), rr: R(q(sheet, '.rr'), sheet), desc: R(q(sheet, 'p'), sheet), acts: R(q(sheet, '.acts'), sheet), btn: R(q(sheet, '.btn'), sheet), price: R(sheet.querySelector(':scope > .meta:last-child'), sheet) },
      empty: R(empty, p3), emptyIn: { svg: R(q(empty, 'svg'), empty), b: R(q(empty, 'b'), empty), p: R(q(empty, 'p'), empty), btn: R(q(empty, '.btn'), empty), small: R(q(empty, '.small'), empty) },
    };
    force.remove();
    await save('bag-layout.json', JSON.stringify(res));
    return res;
  };
})();
