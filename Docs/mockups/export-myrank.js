/* myrank.html -> Unity: sprites (transparent, alpha remapped for the Linear colour space) into Assets/_Project/Art/UI/Rank, with
   manifest.json (name -> {w,h,pad,scale}: size in CSS px without the shadow room, the room around it, texels per CSS px),
   and rank-layout.json: the measured place of every part (CSS px; origin = the parent's top-left; unrotated; phone 390 wide).
   Load in myrank.html (served by Tools/ui-gen/serve.js), then  await exportRank()  and  await measureRank()  */
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
  async function raster(html, W, H, scale, extra) {
    const defs = new XMLSerializer().serializeToString(document.querySelector('body > svg'));
    const doc = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}"><foreignObject width="100%" height="100%"><div xmlns="http://www.w3.org/1999/xhtml" dir="rtl" style="position:relative;width:${W}px;height:${H}px;font-family:Cairo,Tahoma,sans-serif;direction:rtl"><style>${await fonts()}${pageCss()}*,*::before,*::after{animation:none!important}${extra || ''}</style>${defs}${html}</div></foreignObject></svg>`;
    const img = new Image(); img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(doc); await img.decode();
    const c = document.createElement('canvas'); c.width = Math.round(W * scale); c.height = Math.round(H * scale);
    const g = c.getContext('2d'); g.scale(scale, scale); g.drawImage(img, 0, 0);
    const id = g.getImageData(0, 0, c.width, c.height), d = id.data;
    for (let i = 0; i < d.length; i += 4) {        // Linear colour space: UI alpha blends in linear light (see export-match.js)
      const a = d[i + 3]; if (a === 0 || a === 255) continue;
      const al = a / 255, lum = (0.2126 * d[i] + 0.7152 * d[i + 1] + 0.0722 * d[i + 2]) / 255;
      d[i + 3] = Math.round(((1 - lum) * (1 - Math.pow(1 - al, 2.2)) + lum * Math.pow(al, 2.2)) * 255);
    }
    g.putImageData(id, 0, 0);
    return c.toDataURL('image/png');
  }
  const save = (name, body) => fetch('/save?dir=rank&name=' + encodeURIComponent(name), { method: 'POST', body }).then(r => r.text());
  const sz = el => { const cs = getComputedStyle(el); return [parseFloat(cs.width), parseFloat(cs.height)]; };
  const nolayout = '.top,.scroll{position:static}';

  window.exportRank = async function () {
    const force = document.createElement('style'); force.textContent = '.col{width:390px!important}.phone{width:390px!important}.card,.stamp,.pin{rotate:none!important}'; document.head.appendChild(force);
    await new Promise(r => setTimeout(r, 150));
    const p1 = document.getElementById('r1'), p2 = document.getElementById('r2');
    const manifest = {}, out = [];
    // a plain box of a class: only its look (background, border, shadow), at its real size, `pad` room around it for the shadow
    const box = async (name, cls, el, pad, scale, style) => {
      const [w, h] = sz(el);
      const html = `<div class="${cls}" style="position:absolute;left:${pad}px;top:${pad}px;right:auto;bottom:auto;width:${w}px;height:${h}px;margin:0;rotate:none;display:block;padding:0;${style || ''}"></div>`;
      await save(name + '.png', await raster(html, w + 2 * pad, h + 2 * pad, scale, '.card .hd,.card .body{display:none}'));
      manifest[name] = { w, h, pad, scale }; out.push(name);
    };
    await box('rk_card', 'card', p1.querySelector('.card'), 36, 3);
    await box('rk_rule', 'rule', p1.querySelector('.rule'), 6, 5);
    await box('rk_rung', 'rung', p1.querySelector('.rung.lock'), 14, 4);
    await box('rk_rung_done', 'rung', p1.querySelector('.rung.lock'), 14, 4);
    await box('rk_rung_lock', 'rung lock', p1.querySelector('.rung.lock'), 14, 4);
    await box('rk_rung_now', 'rung now', p1.querySelector('.rung.now'), 22, 4);
    await box('rk_it', 'it', p2.querySelector('.it'), 4, 6, '');
    await box('rk_it_lk', 'it locked', p2.querySelector('.it'), 4, 6, '');
    await box('rk_pin', 'pin', p1.querySelector('.pin'), 8, 6);
    await box('rk_stamp', 'stamp', p1.querySelector('.stamp'), 4, 6, 'color:transparent'); manifest.rk_stamp.slice = 6;
    // the bar: track and fill (a rounded pill, sliced in Unity)
    await box('rk_track', 'bar', p1.querySelector('.bar'), 4, 6, 'margin:0');
    await save('rk_fill.png', await raster('<i style="position:absolute;left:2px;top:2px;width:30px;height:10px;border-radius:5px;background:linear-gradient(90deg,#8a1c24,#c9424c)"></i>', 34, 14, 6)); manifest.rk_fill = { w: 30, h: 10, pad: 2, scale: 6, slice: 5 }; out.push('rk_fill');
    // the brass rail through the ladder: 4px wide, vertical gradient
    await save('rk_rail.png', await raster('<i style="position:absolute;left:2px;top:2px;width:4px;height:96px;border-radius:2px;background:linear-gradient(180deg,#6a4c1c,#d2ad60 50%,#6a4c1c);box-shadow:0 0 0 1px #000"></i>', 8, 100, 8)); manifest.rk_rail = { w: 4, h: 96, pad: 2, scale: 8 }; out.push('rk_rail');
    // the screen's background and the fade under the title
    await save('rk_bg.png', await raster('<div style="position:absolute;inset:0;background:radial-gradient(ellipse 90% 30% at 50% 0,#e8c27a18,transparent 70%),linear-gradient(180deg,#15100d,#090605)"></div>', 390, 790, 2)); manifest.rk_bg = { w: 390, h: 790, pad: 0, scale: 2 }; out.push('rk_bg');
    const topH = sz(p1.querySelector('.top'))[1];
    await save('rk_top.png', await raster('<div style="position:absolute;inset:0;background:linear-gradient(180deg,#0a0705f2 75%,transparent)"></div>', 390, topH, 3)); manifest.rk_top = { w: 390, h: topH, pad: 0, scale: 3 }; out.push('rk_top');
    await save('rk_ic.png', await raster('<button class="ic" style="position:absolute;left:8px;top:8px"></button>', 54, 54, 5)); manifest.rk_ic = { w: 38, h: 38, pad: 8, scale: 5 }; out.push('rk_ic');
    await save('rk_back.png', await raster('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" style="position:absolute;left:2px;top:2px;width:18px;height:18px;stroke:#cfc6b2;fill:none;stroke-width:2.2;stroke-linecap:round;stroke-linejoin:round"><use href="#back"/></svg>', 22, 22, 8)); manifest.rk_back = { w: 18, h: 18, pad: 2, scale: 8 }; out.push('rk_back');
    // the lock beside a locked rung's points
    await save('rk_lock.png', await raster('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" style="position:absolute;left:2px;top:2px;width:14px;height:14px;stroke:#8f8777;fill:none;stroke-width:2.2;stroke-linecap:round"><use href="#lock"/></svg>', 18, 18, 8)); manifest.rk_lock = { w: 14, h: 14, pad: 2, scale: 8 }; out.push('rk_lock');
    // the medals: coloured and locked (grey, dark), 96px with 10px room for the shadow
    for (let i = 1; i <= 8; i++) {
      for (const lock of [false, true]) {
        const f = lock ? 'grayscale(1) brightness(.45)' : 'none';
        const html = `<img src="${await dataUrl('img/medal_r' + i + '.png')}" style="position:absolute;left:10px;top:10px;width:96px;height:96px;filter:${f} drop-shadow(0 6px 8px #0008)"/>`;
        const name = 'rk_m' + i + (lock ? '_lock' : '');
        await save(name + '.png', await raster(html, 116, 116, 4)); manifest[name] = { w: 96, h: 96, pad: 10, scale: 4 }; out.push(name);
      }
    }
    // the chosen rank's name in Aref Ruqaa (cursive, so drawn as an image; white, tinted in Unity): one per rank
    const names = ['الصبي', 'العميل', 'الجندي', 'الريّس', 'المستشار', 'اليد اليمنى', 'الزعيم', 'العرّاب'];
    const labels = {};
    for (let i = 0; i < 8; i++) {
      const probe = document.createElement('span');
      probe.style.cssText = "position:absolute;visibility:hidden;white-space:nowrap;font-family:'Aref Ruqaa';font-weight:700;font-size:30.4px;line-height:1.15";
      probe.textContent = names[i]; document.body.appendChild(probe);
      const r = probe.getBoundingClientRect(), w = Math.ceil(r.width) + 2, h = Math.ceil(r.height); probe.remove();
      await save('rk_n' + (i + 1) + '.png', await raster(`<span style="position:absolute;left:4px;top:4px;width:${w}px;height:${h}px;white-space:nowrap;text-align:center;font-family:'Aref Ruqaa';font-weight:700;font-size:30.4px;line-height:1.15;color:#fff">${names[i]}</span>`, w + 8, h + 8, 6));
      manifest['rk_n' + (i + 1)] = { w, h, pad: 4, scale: 6 }; out.push('rk_n' + (i + 1));
    }
    force.remove();
    await save('manifest.json', JSON.stringify(manifest));
    return out.length + ' sprites';
  };

  async function dataUrl(src) {
    const b = await (await fetch(src)).blob();
    return await new Promise(r => { const f = new FileReader(); f.onload = () => r(f.result); f.readAsDataURL(b); });
  }

  window.measureRank = async function () {
    const force = document.createElement('style'); force.textContent = '.col{width:390px!important}.phone{width:390px!important}.card,.stamp,.pin{rotate:none!important}'; document.head.appendChild(force);
    await new Promise(r => setTimeout(r, 150));
    const R = (el, rel) => { if (!el) return null; const a = el.getBoundingClientRect(), b = rel.getBoundingClientRect(); return [+(a.left - b.left).toFixed(2), +(a.top - b.top).toFixed(2), +a.width.toFixed(2), +a.height.toFixed(2)]; };
    const T = (el, rel) => { if (!el) return null; const g = document.createRange(); g.selectNodeContents(el); const a = g.getBoundingClientRect(), b = rel.getBoundingClientRect(); return [+(a.left - b.left).toFixed(2), +(a.top - b.top).toFixed(2), +a.width.toFixed(2), +a.height.toFixed(2)]; };
    const q = (p, s) => p.querySelector(s);
    const p1 = document.getElementById('r1'), p2 = document.getElementById('r2'), p4 = document.getElementById('r4');
    const card = q(p1, '.card'), rules = q(p1, '.rules'), rule = q(p1, '.rule'), sec = q(p1, '.sec'), ladder = q(p1, '.ladder');
    const rungs = [...p1.querySelectorAll('.rung')], now = q(p1, '.rung.now'), lock = q(p1, '.rung.lock'), done = q(p4, '.rung.done');
    const sheet = q(p2, '.sheet'), it = q(p2, '.it');
    const res = {
      phone: [390, 790], top: R(q(p1, '.top'), p1), ic: R(q(p1, '.ic'), p1), title: T(q(p1, '.ttl').firstChild, p1), sub: R(q(p1, '.ttl small'), p1), scroll: R(q(p1, '.scroll'), p1),
      card: R(card, p1), hd: R(q(card, '.hd'), card), hdB: R(q(card, '.hd b'), card), hdS: R(q(card, '.hd span'), card), body: R(q(card, '.body'), card), medal: R(q(card, '.medal'), card), mid: R(q(card, '.mid'), card),
      nm: R(q(card, '.nm'), card), rk: R(q(card, '.rk'), card), tier: R(q(card, '.tier'), card), bul: R(q(card, '.bul'), card), bul1: R(q(card, '.bul img'), card), tierTxt: T([...q(card, '.tier').childNodes].find(n => n.nodeType === 3), card),
      bar: R(q(card, '.bar'), card), prog: R(q(card, '.prog'), card), progS: R(q(card, '.prog span'), card), progB: R(q(card, '.prog b'), card), stamp: R(q(card, '.stamp'), card),
      rules: R(rules, p1), rule: R(rule, rules), rule2: R([...p1.querySelectorAll('.rule')][1], rules), rule3: R([...p1.querySelectorAll('.rule')][2], rules), ruleB: R(q(rule, 'b'), rule), ruleS: R(q(rule, 'span'), rule),
      sec: R(sec, p1), secTitle: T(sec.firstChild, sec), secSmall: R(q(sec, 'small'), sec),
      ladder: R(ladder, p1), rung0: R(rungs[0], ladder), rungN: R(now, ladder), rungs: rungs.map(r => R(r, ladder)),
      rungIn: { m: R(q(lock, 'img.m'), lock), tx: R(q(lock, '.tx'), lock), b: R(q(lock, '.tx b'), lock), p: R(q(lock, '.tx p'), lock), end: R(q(lock, '.end'), lock), lk: R(q(lock, '.lk'), lock), endSpan: R(q(lock, '.end span'), lock) },
      nowIn: { m: R(q(now, 'img.m'), now), end: R(q(now, '.end'), now), pts: R(q(now, '.end .pts'), now), sp: R(q(now, '.end span:last-child'), now), pin: R(q(now, '.pin'), now) },
      doneIn: { end: R(q(done, '.end'), done), ok: R(q(done, '.end .ok'), done), sp: R(q(done, '.end span:last-child'), done) },
      sheet: R(sheet, p2), sheetIn: { hd: R(q(sheet, '.hd'), sheet), m: R(q(sheet, 'img.m'), sheet), nm: R(q(sheet, 'b.nm'), sheet), p: R(q(sheet, 'p'), sheet), range: R(q(sheet, '.range'), sheet), set: R(q(sheet, '.set'), sheet), need: R(q(sheet, '.need'), sheet), btn: R(q(sheet, '.btn'), sheet) },
      it: R(it, q(sheet, '.set')), it2: R([...p2.querySelectorAll('.it')][1], q(sheet, '.set')), itIn: { i: R(q(it, 'i'), it), span: R(q(it, 'span'), it), small: R(q(it, 'small'), it) },
    };
    force.remove();
    await save('rank-layout.json', JSON.stringify(res));
    return res;
  };
})();
