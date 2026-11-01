/* Idea B of navbar.html (the case file, the owner's choice): sprites for Unity and the measured layout.
   Load in navbar.html (served by Tools/ui-gen/serve.js), then
     await exportNav()   -> PNGs into Assets/_Project/Art/UI/Nav (transparent, alpha remapped for the Linear colour space)
     await measureNav()  -> layout.json there: every part's rect in CSS px, origin at the phone's bottom-left, phone 390 wide */
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
  const save = (name, body) => fetch('/save?dir=nav&name=' + encodeURIComponent(name), { method: 'POST', body }).then(r => r.text());
  const ON = 'filter:drop-shadow(0 2px 2px #000)', OFF = 'filter:grayscale(.45) brightness(.8)';
  const icon = (sym, size, pad, f) => `<svg xmlns="http://www.w3.org/2000/svg" class="ico ill" style="position:absolute;left:${pad}px;top:${pad}px;width:${size}px;height:${size}px;${f}"><use href="#${sym}"/></svg>`;
  const LABELS = { market: 'السوق', family: 'العائلة', rooms: 'الغرف', ranks: 'الرتب', settings: 'الإعدادات' };

  window.exportNav = async function () {
    const out = [];
    const put = async (name, html, W, H, scale, extra) => { await save(name + '.png', await raster(html, W, H, scale, extra)); out.push(name); };
    // the leather strip with its stitch line; 32px above it for the shadow it throws on the page
    await put('nb_leather', '<div class="bar B" style="position:absolute;left:0;bottom:0;width:390px;height:94px;padding:0"></div>', 390, 126, 3);
    // the ivory file tab behind the chosen tab (slot 73.2 x 78, reaching 16px into the safe area)
    await put('nb_paper', '<div class="pp"></div><i class="pl"></i>', 73.2, 94, 4,
      '.pp{position:absolute;inset:0;clip-path:polygon(10% 0,90% 0,100% 18px,100% 100%,0 100%,0 18px);background:var(--grain),linear-gradient(180deg,#efe4c8,#d9c9a2);background-blend-mode:multiply,normal}.pl{position:absolute;left:14%;right:14%;top:7px;height:2px;background:#8a1c24;opacity:.7}');
    // tab icons (34px, 4px room for the shadow), lit and dimmed
    for (const k of ['market', 'family', 'rooms', 'ranks', 'safe']) {
      await put('ni_' + k, icon('m-' + k, 34, 4, ON), 42, 42, 6);
      await put('ni_' + k + '_off', icon('m-' + k, 34, 4, OFF), 42, 42, 6);
    }
    // menu icons (38px)
    for (const k of ['market', 'case', 'phone', 'family', 'trophy', 'medal']) await put('nm_' + k, icon('m-' + k, 38, 4, ON), 46, 46, 6);
    // the chosen tab's name in Aref Ruqaa (white, tinted in Unity), drawn as images because the font is cursive
    const sizes = {};
    for (const [k, t] of Object.entries(LABELS)) {
      const probe = document.createElement('span');
      probe.style.cssText = "position:absolute;visibility:hidden;white-space:nowrap;font-family:'Aref Ruqaa';font-weight:700;font-size:12.8px;line-height:1.2";
      probe.textContent = t; document.body.appendChild(probe);
      const r = probe.getBoundingClientRect(), w = Math.ceil(r.width) + 2, h = Math.ceil(r.height); probe.remove();
      await put('nl_' + k, `<span style="position:absolute;left:4px;top:4px;width:${w}px;height:${h}px;text-align:center;white-space:nowrap;font-family:'Aref Ruqaa';font-weight:700;font-size:12.8px;line-height:1.2;color:#fff">${t}</span>`, w + 8, h + 8, 8);
      sizes[k] = [w, h];
    }
    // the paper tag over the rooms tab (sliced in Unity: 6px ends), without its text
    await put('nb_tag', '<span style="position:absolute;left:6px;top:6px;width:40px;height:16px;border-radius:3px;background:#d9c9a2;box-shadow:0 0 0 1px #8a1c24,0 3px 5px #000a"></span>', 52, 30, 6);
    // the menu: the panel with its stitch between the two places (250 x 124.72 as measured: two 53.86 rows, 40px around for the brass rings and the shadow), the arrow, the current place's highlight
    await put('nm_panel', '<div class="menu" style="left:40px;top:40px;height:124.72px;bottom:auto;animation:none"><i style="position:absolute;left:17px;right:17px;top:61.86px;border-top:1px dashed #a98f6244"></i></div>', 330, 204.72, 3, '.menu::after{display:none}');
    await put('nm_arrow', '<i style="position:absolute;left:8.5px;top:8.5px;width:13px;height:13px;transform:rotate(45deg);background:#160e0a;box-shadow:2.5px 2.5px 0 0 #8a6a30,3.5px 3.5px 0 0 #000"></i>', 30, 30, 6);
    await put('nm_cur', '<div style="position:absolute;left:2px;top:2px;width:236px;height:53.86px;border-radius:8px;background:linear-gradient(180deg,#5a141a,#360a0e);box-shadow:inset 0 0 0 1px #c9a050"></div>', 240, 57.86, 4);
    // the red count (17px, 2px dark ring; sliced in Unity so two digits widen it) and the plain dot (9px)
    await put('nb_cnt', '<i style="position:absolute;left:4px;top:4px;width:17px;height:17px;border-radius:50%;background:#d6313b;box-shadow:0 0 0 2px #170f0b"></i>', 25, 25, 6);
    await put('nb_dot', '<i style="position:absolute;left:3px;top:3px;width:9px;height:9px;border-radius:50%;background:#d6313b;box-shadow:0 0 0 2px #170f0b"></i>', 15, 15, 8);
    await save('labels.json', JSON.stringify(sizes));
    return out.join(' ');
  };

  window.measureNav = async function () {
    const force = document.createElement('style'); force.textContent = '.col{width:390px!important}.menu{animation:none!important}'; document.head.appendChild(force);   // draw() rebuilds the columns, so the width goes in a rule
    const wait = () => new Promise(r => setTimeout(r, 450));   // after the menu's rise animation
    const ph = () => document.querySelector('.bar.B').closest('.phone');
    const rect = el => { if (!el) return null; const P = ph().getBoundingClientRect(), r = el.getBoundingClientRect(); return { x: +(r.left - P.left).toFixed(2), y: +(P.bottom - r.bottom).toFixed(2), w: +r.width.toFixed(2), h: +r.height.toFixed(2) }; };
    const click = sel => document.querySelector(sel).click();
    const res = { phone: { w: ph().getBoundingClientRect().width, h: ph().getBoundingClientRect().height }, tabs: {}, menus: {} };
    const tabOf = k => ph().querySelector(`.bar.B .s[data-k=${k}]`);
    const grab = k => { const s = tabOf(k); return { tab: rect(s), icon: rect(s.querySelector('.ico')), label: rect(s.querySelector('.lb')), cnt: rect(s.querySelector('.cnt')), dot: rect(s.querySelector('.nd')), live: rect(s.querySelector('.live')) }; };
    // every tab chosen and not chosen: settings, then rooms are chosen in turn (they open no menu); the others are measured with their menu open
    click('.bar.B .s[data-k=settings]'); await wait();
    for (const k of ['market', 'family', 'rooms', 'ranks']) res.tabs[k] = { off: grab(k) };
    res.tabs.settings = { on: grab('settings') };
    click('.bar.B .s[data-k=rooms]'); await wait();
    res.tabs.rooms.on = grab('rooms'); res.tabs.settings.off = grab('settings');
    for (const k of ['market', 'family', 'ranks']) {
      click(`.bar.B .s[data-k=${k}]`); await wait(); res.tabs[k].on = grab(k);
      const m = ph().querySelector('.menu');
      res.menus[k] = { box: rect(m), ax: parseFloat(m.style.getPropertyValue('--ax')), items: [...m.querySelectorAll('.mi')].map(mi => ({ item: rect(mi), icon: rect(mi.querySelector('.ico')), title: rect(mi.querySelector('b')), sub: rect(mi.querySelector('small')), cnt: rect(mi.querySelector('.cnt')), dot: rect(mi.querySelector('.nd')) })) };
      click(`.bar.B .s[data-k=${k}]`); await wait();
    }
    res.bar = rect(ph().querySelector('.bar.B'));
    force.remove();
    await save('layout.json', JSON.stringify(res, null, 1));
    return res;
  };
})();
