/* settings.html -> Unity: sprites (transparent, alpha remapped for the Linear colour space) into Assets/_Project/Art/UI/Settings,
   and the measured layout of the parts (settings-layout.json there; CSS px on a 390px phone).
   Load in settings.html (served by Tools/ui-gen/serve.js), then  await exportSettings()  and  await measureSettings()  */
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
  const pageCss = () => [...document.querySelectorAll('style')].map(s => s.textContent).join('\n').replace(/body\{[^}]*\}/, '').replace(/@property[^}]*\}/g, '');   // "<percentage>" in an @property would break the XML of the svg
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
  const save = (name, body) => fetch('/save?dir=settings&name=' + encodeURIComponent(name), { method: 'POST', body }).then(r => r.text());
  const box = (cls, w, h, pad, style) => `<div class="${cls}" style="position:absolute;left:${pad}px;top:${pad}px;right:auto;bottom:auto;width:${w}px;height:${h}px;margin:0;${style || ''}"></div>`;   // right/bottom auto: in RTL a set right would win over left

  window.exportSettings = async function () {
    const out = [];
    const put = async (name, html, W, H, scale, extra, remap) => { await save(name + '.png', await raster(html, W, H, scale, extra, remap)); out.push(name); };
    // the page behind everything: the warm glow at the top and the dark leather fall-off
    await put('set_bg', '<div style="position:absolute;inset:0;background:radial-gradient(ellipse 90% 30% at 50% 0,#e8c27a18,transparent 70%),linear-gradient(180deg,#15100d,#090605)"></div>', 390, 790, 2, '', false);
    // the ID card: paper, ribbon (right 16..38px), inner shadow; sliced 40
    await put('set_me', box('me', 140, 76, 16, 'rotate:none;display:block;padding:0'), 172, 112, 4);
    // the ID number pill
    await put('set_idc', '<i style="position:absolute;left:2px;top:2px;width:70px;height:26px;border-radius:13px;background:#1c170f18;box-shadow:inset 0 0 0 1px #1c170f55"></i>', 74, 30, 6);
    // a drawer (closed or open: the whole leather block), sliced 14
    await put('set_sec', box('sec', 120, 90, 14, 'display:block;margin:0'), 148, 118, 4);
    // the gold line under an open drawer's header
    await put('set_secline', '<i style="position:absolute;left:0;top:0;width:100px;height:1px;background:linear-gradient(90deg,transparent,#b9904888,transparent)"></i>', 100, 2, 6);
    // the radio panel with its two screws, sliced 16
    await put('set_radio', box('radio', 140, 100, 14, 'display:block;padding:0'), 168, 128, 4);
    // a dial: ticks and the knurled brass knob (the pointer is separate)
    await put('set_dial', '<div class="dial" style="position:absolute;left:12px;top:10px"><div class="ticks"></div><div class="face"></div></div>', 128, 128, 5);
    await put('set_ptr', '<div style="position:absolute;left:3px;top:3px;width:4px;height:20px;border-radius:2px;background:#1a0f08;box-shadow:0 0 0 1px #fff3"></div>', 10, 26, 12);
    // the value box under a dial, sliced 6
    await put('set_val', '<div style="position:absolute;left:4px;top:4px;width:60px;height:26px;border-radius:5px;background:#000b;box-shadow:inset 0 0 0 1px #3a2a1c"></div>', 68, 34, 6);
    // the VU meter bar, sliced 8
    await put('set_vu', '<div class="vu" style="position:absolute;left:4px;top:4px;margin:0;width:140px;height:42px"></div>', 148, 50, 5, '.vu i{display:none}');
    // the brass mic-test button, sliced 14
    await put('set_mic', '<button class="mic" style="position:absolute;left:6px;top:6px;width:120px;height:44px;padding:0"></button>', 132, 62, 5);
    return out.join(' ');
  };

  window.measureSettings = async function () {
    const force = document.createElement('style'); force.textContent = '.phone{width:390px!important}'; document.head.appendChild(force);
    document.querySelectorAll('.sec').forEach(s => s.classList.add('open'));
    await new Promise(r => setTimeout(r, 400));
    const ph = document.querySelector('.phone');
    const R = (el, rel) => { if (!el) return null; const a = el.getBoundingClientRect(), b = rel.getBoundingClientRect(); return [+(a.left - b.left).toFixed(2), +(a.top - b.top).toFixed(2), +a.width.toFixed(2), +a.height.toFixed(2)]; };
    const q = s => document.querySelector(s);
    const me = q('.me'), sec = q('.sec'), sh = q('.sh'), radio = q('.radio'), dials = [...document.querySelectorAll('.dial')], kb = q('.kb');
    const res = {
      top: R(q('.top'), ph), ic: R(q('.ic'), ph), ttl: R(q('.ttl'), ph), scrollPad: [84, 12, 40],
      me: R(me, ph), meIn: { av: R(q('.me .av'), me), name: R(q('.me b'), me), sub: R(q('.me small'), me), idc: R(q('.me .idc'), me), idcBtn: R(q('.me .idc button'), me), idcLbl: R(q('.me .idc'), me) },
      sec: R(sec, ph), sh: R(sh, sec), shIcon: R(q('.sh svg.k'), sh), shTitle: R(q('.sh b'), sh), shChev: R(q('.sh .cv'), sh), gapSec: 12,
      radio: R(radio, ph), dials: dials.map(d => R(d, radio)), vals: [...document.querySelectorAll('.kb .val')].map(v => R(v, radio)), nms: [...document.querySelectorAll('.kb .nm')].map(v => R(v, radio)),
      vu: R(q('.vu'), radio), mic: R(q('.mic'), radio), seg: R(q('.seg'), ph), lab: R(q('.lab'), ph), row: R(q('.r'), ph), tg: R(q('.tg'), q('.r')),
    };
    force.remove();
    await fetch('/save?dir=settings&name=settings-layout.json', { method: 'POST', body: JSON.stringify(res) });
    return res;
  };
})();
