/* Exports the visual parts of match.html as exact PNGs (transparent background) for the Unity UI kit.
   Run in the mockup page (served by Tools/ui-gen/serve.js):  const s=document.createElement('script');s.src='/export-match.js';document.body.appendChild(s);  then  await exportAll()
   Every sprite is rendered by the browser from the real CSS, so gradients, grain, inset shadows and dashed borders match the HTML. */
(function () {
  const css = () => [...document.querySelectorAll('style')].map(s => s.textContent).join('\n').replace(/body\{[^}]*\}/, '');
  const sym = id => { const n = document.getElementById(id); return n ? n.innerHTML : ''; };
  const svg = (id, style) => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="24" height="24" style="${style || ''}">${sym(id)}</svg>`;
  const NOZ = '.noz .av::after{content:none!important}';

  async function raster(html, w, h, pad, scale, extraCss) {
    const W = w + 2 * pad, H = h + 2 * pad;
    const doc = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}"><foreignObject width="100%" height="100%"><div xmlns="http://www.w3.org/1999/xhtml" style="position:relative;width:${W}px;height:${H}px;font-family:Cairo,Tahoma,sans-serif"><style>${css()}${extraCss || ''}</style><div style="position:absolute;left:${pad}px;top:${pad}px;width:${w}px;height:${h}px">${html}</div></div></foreignObject></svg>`;
    const img = new Image();
    img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(doc);
    await img.decode();
    const c = document.createElement('canvas'); c.width = Math.round(W * scale); c.height = Math.round(H * scale);
    const g = c.getContext('2d'); g.scale(scale, scale); g.drawImage(img, 0, 0);
    { const id = g.getImageData(0, 0, c.width, c.height), d = id.data;
      for (let i = 0; i < d.length; i += 4) {
        const a = d[i + 3]; if (a === 0 || a === 255) continue;
        const al = a / 255, lum = (0.2126 * d[i] + 0.7152 * d[i + 1] + 0.0722 * d[i + 2]) / 255;
        d[i + 3] = Math.round(((1 - lum) * (1 - Math.pow(1 - al, 2.2)) + lum * Math.pow(al, 2.2)) * 255);
      }
      g.putImageData(id, 0, 0); }
    return c.toDataURL('image/png');
  }
  const save = (name, data) => fetch('/save?name=' + encodeURIComponent(name), { method: 'POST', body: data }).then(r => r.text());

  // seat wrapper: .seat rules (filter, ring colours) apply to the .av inside
  const seatAv = (cls, size) => `<div class="seat ${cls}" style="position:static;transform:none;width:${size}px;height:${size}px;display:block"><div class="av" style="width:${size}px;height:${size}px">${svg('fed')}</div></div>`;

  const list = [];
  const add = (name, html, w, h, pad, scale, slice, extraCss) => list.push({ name, html, w, h, pad, scale, slice: slice || 0, extraCss });

  // ---- backgrounds (full phone, 390x790)
  const full = bg => `<div style="position:absolute;inset:0;background:${bg}"></div>`;
  add('bg_default', full('radial-gradient(ellipse 90% 30% at 50% 0,#e8c27a18,transparent 70%),linear-gradient(180deg,#15100d,#090605)'), 390, 790, 0, 3);
  add('bg_day', full('radial-gradient(ellipse 90% 34% at 50% 0,#f0c06a30,transparent 70%),linear-gradient(180deg,#1d150e,#0b0705)'), 390, 790, 0, 3);
  add('bg_night', full('radial-gradient(ellipse 80% 40% at 50% 8%,#2a3a7a44,transparent 70%),linear-gradient(180deg,#0b0e1c,#05060b)'), 390, 790, 0, 3);
  add('dim_radial', full('radial-gradient(ellipse at 50% 38%,#0000,#000d 80%)'), 390, 790, 0, 2);
  add('dim_radial_civ', full('radial-gradient(ellipse at 50% 40%,#0000,#000c 75%)'), 390, 790, 0, 2);
  add('fade_day', full('linear-gradient(0deg,#090605 66%,transparent)'), 390, 240, 0, 2);
  add('fade_night', full('linear-gradient(0deg,#05060b 66%,transparent)'), 390, 240, 0, 2);

  // ---- table
  add('felt', '<div class="felt" style="left:0;right:0;top:0;bottom:0"></div>', 304, 361, 36, 3);

  // ---- seats (44px avatar, glow needs padding)
  const avs = { av: '', av_me: 'me', av_talk: 'talk', av_pick: 'pick', av_pick_doc: 'pick doc', av_pick_sher: 'pick sher', av_dead: 'dead', av_me_dead: 'me dead' };
  for (const k in avs) add(k, seatAv(avs[k], 44), 44, 44, 40, 4);
  add('av_sleep', seatAv('sleep noz', 44), 44, 44, 40, 4, 0, NOZ);
  add('av_me_sleep', seatAv('me sleep noz', 44), 44, 44, 40, 4, 0, NOZ);
  add('av_small', `<div class="role-row" style="padding:0;gap:0;background:none;box-shadow:none;display:block"><div class="av">${svg('fed')}</div></div>`, 30, 30, 8, 4);
  add('av_mate', `<div class="mates" style="display:block"><div class="mate" style="display:block"><div class="av">${svg('fed')}</div></div></div>`, 40, 40, 10, 4);
  add('tag_red', '<div class="seat" style="position:static;transform:none;width:100%;height:100%;display:block"><span class="tag" style="position:static;display:grid;width:100%;height:100%;padding:0"></span></div>', 19, 19, 6, 4);
  add('tag_mafia', '<div class="seat" style="position:static;transform:none;width:100%;height:100%;display:block"><span class="tag mafia" style="position:static;display:grid;width:100%;height:100%;padding:0"></span></div>', 19, 19, 6, 4);
  add('tag_gold', '<div class="seat" style="position:static;transform:none;width:100%;height:100%;display:block"><span class="tag gold" style="position:static;display:grid;width:100%;height:100%;padding:0"></span></div>', 19, 19, 6, 4);
  add('nametag', '<div class="seat" style="position:static;transform:none;width:100%;height:100%;display:block"><span class="nm" style="display:block;width:100%;height:100%;padding:0;max-width:none"></span></div>', 40, 13, 3, 4, 6, '.seat{position:static!important;transform:none!important;width:100%!important;display:block!important}');
  add('bubble', '<div class="seat" style="position:static;transform:none;width:100%;height:100%;display:block"><span class="bub" style="position:static;display:block;width:100%;height:100%;margin:0;padding:0"></span></div>', 80, 18, 10, 4, 9);

  // ---- top bar
  add('ring_bg', '<div class="ring" style="--p:0%;width:46px;height:46px"></div>', 46, 46, 8, 4, 0, '.ring::before{display:none}');
  add('ring_arc', '<div class="ring" style="--p:100%;--c:#fff;width:46px;height:46px;background:none;box-shadow:none"></div>', 46, 46, 8, 4);
  add('pill', '<div class="pill" style="width:100%;height:100%;padding:0"></div>', 60, 26, 4, 4, 13);
  add('pill_mafia', '<div class="pill mafia" style="width:100%;height:100%;padding:0"></div>', 60, 26, 4, 4, 13);
  add('pill_warn', '<div class="pill warn" style="width:100%;height:100%;padding:0"></div>', 60, 26, 4, 4, 13);

  // ---- panels + buttons
  add('card', '<div class="card" style="width:100%;height:100%;padding:0"></div>', 300, 100, 22, 3, 12);
  add('go_red', '<button class="go" style="display:block;width:100%;height:100%;padding:0"></button>', 200, 56, 36, 3, 14);
  add('go_gold', '<button class="go gold" style="display:block;width:100%;height:100%;padding:0"></button>', 200, 56, 36, 3, 14);
  add('go_teal', '<button class="go teal" style="display:block;width:100%;height:100%;padding:0"></button>', 200, 56, 36, 3, 14);
  add('ghost', '<button class="ghost" style="display:block;width:100%;height:100%;padding:0"></button>', 70, 40, 6, 4, 14);
  add('mic', '<button class="mic" style="padding:0"></button>', 60, 60, 14, 4);
  add('mic_off', '<button class="mic off" style="padding:0"></button>', 60, 60, 14, 4);
  add('paper', '<div class="paper" style="width:100%;height:100%;padding:0"></div>', 300, 200, 26, 3, 10);
  add('stamp', '<span class="stamp" style="display:block;width:100%;height:100%;padding:0;rotate:none;opacity:1;color:#fff"></span>', 140, 56, 4, 4, 14);
  add('banner_civ', '<div class="banner civ" style="width:100%;height:100%;padding:0"></div>', 360, 110, 22, 3, 14);
  add('banner_mafia', '<div class="banner mafia" style="width:100%;height:100%;padding:0"></div>', 360, 110, 22, 3, 14);
  add('role_row', '<div class="role-row" style="width:100%;height:100%;padding:0;gap:0"></div>', 360, 44, 4, 4, 10);
  add('chip', '<span class="chip" style="display:block;width:100%;height:100%;padding:0;color:#fff"></span>', 60, 20, 3, 4, 10);
  add('rew_box', '<div class="rew" style="display:block"><div style="width:100%;height:58px;padding:0;font-size:0"></div></div>', 110, 58, 3, 4, 12);
  add('tally', '<div class="tally" style="display:block"><span style="display:block;width:100%;height:22px;padding:0"></span></div>', 60, 22, 3, 4, 11);

  add('tally_light', '<div class="tally" style="display:block"><span style="display:block;width:100%;height:22px;padding:0;background:#0001;color:#4a3820"></span></div>', 60, 22, 3, 4, 11);

  // ---- art
  add('moon', `<div class="moon" style="width:58px;height:58px;filter:drop-shadow(0 0 18px #7d92ff30)"><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="58" height="58" fill="#e8e2c4">${sym('moon')}</svg></div>`, 58, 58, 56, 4);
  const lineIcons = ['mic', 'micoff', 'eye', 'hand', 'vote', 'ghost', 'cross', 'badge', 'skull', 'clock', 'sun', 'moon'];
  for (const id of lineIcons) add('icon_' + id, svg(id, 'stroke:#fff;fill:none;stroke-width:2;stroke-linecap:round;stroke-linejoin:round'), 24, 24, 2, 6);
  add('icon_fed', svg('fed', 'fill:#fff;stroke:none'), 24, 24, 2, 6);

  window.exportAll = async function () {
    const manifest = {};
    for (const it of list) {
      const data = await raster(it.html, it.w, it.h, it.pad, it.scale, it.extraCss);
      await save(it.name + '.png', data);
      manifest[it.name] = { w: it.w, h: it.h, pad: it.pad, scale: it.scale, slice: it.slice };
    }
    await save('manifest.json', JSON.stringify(manifest, null, 1));
    return Object.keys(manifest).length + ' sprites';
  };
  window.exportOne = async function (name) {
    const it = list.find(x => x.name === name);
    const data = await raster(it.html, it.w, it.h, it.pad, it.scale, it.extraCss);
    await save('_' + name + '.png', data);
    return name;
  };
})();
