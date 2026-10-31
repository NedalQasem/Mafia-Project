'use strict';
// Pictures of offers and events: the board uploads one (images.js), saves its id on the offer / event, and the game gets that id with the
// list it already asks for, then loads /v1/img/<id>. An id the board never uploaded is refused, and an offer without a picture stays as before.
const test = require('node:test');
const assert = require('node:assert/strict');
const { createApp } = require('../src/app');
const { createChat } = require('../src/chat');
const { createStore } = require('../src/store');
const { createImages } = require('../src/images');
const { createOffers } = require('../src/offers');
const { createEvents } = require('../src/events');
const { createAdminStore } = require('../src/adminstore');
const { createAdmin, hashPassword } = require('../src/admin');

const PASSWORD = 'correct horse battery';
const JPEG = Buffer.concat([Buffer.from([0xff, 0xd8, 0xff, 0xe0]), Buffer.alloc(300, 7)]);

async function boot() {
  const cfg = { currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: { publicCooldownMs: 0, cooldownMs: 0 } };
  const playfab = { authenticate: async t => t.replace('-ticket-0123456789', ''), getDisplayName: async pf => 'اسم ' + pf, getProgress: async () => ({ rankPoints: 1, level: 1 }), addCurrency: async () => ({}) };
  const adminStore = createAdminStore({ file: null });
  const chat = createChat({ cfg, playfab });
  const images = createImages();
  const offers = createOffers({ cfg, source: () => (adminStore.offers !== null ? adminStore.offers : cfg.offers) });
  const events = createEvents({ cfg, playfab, source: () => (adminStore.events !== null ? adminStore.events : cfg.events) });
  const admin = createAdmin({ cfg, store: adminStore, chat, images, events, adminCfg: { passwordHash: hashPassword(PASSWORD), path: '/office' } });
  const { server } = createApp({ cfg, playfab, store: createStore(null), chat, images, offers, events, admin });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const url = 'http://127.0.0.1:' + server.address().port;
  let cookie = '';
  const adm = async (p, body) => {
    const h = {}; if (cookie) h.cookie = cookie;
    if (body !== undefined) { h['content-type'] = 'application/json'; h['x-admin'] = '1'; }
    const res = await fetch(url + '/office' + p, { method: body !== undefined ? 'POST' : 'GET', headers: h, body: body !== undefined ? JSON.stringify(body) : undefined });
    const sc = res.headers.get('set-cookie'); if (sc) cookie = sc.split(';')[0];
    return { status: res.status, json: await res.json() };
  };
  const player = async (p, body = {}) => {
    const res = await fetch(url + p, { method: 'POST', headers: { 'content-type': 'application/json', 'x-authorization': 'PLAYER0001-ticket-0123456789' }, body: JSON.stringify(body) });
    return { status: res.status, json: await res.json() };
  };
  await adm('/api/login', { password: PASSWORD });
  const upload = async () => (await adm('/api/upload-image', { data: JPEG.toString('base64') })).json.id;
  return { adm, player, upload, url, close: () => server.close() };
}

test('an offer with a picture: the game gets its id and can load the picture', async () => {
  const b = await boot();
  try {
    const id = await b.upload();
    const r = await b.adm('/api/offers/save', { adId: 'ad_ember', headline: 'x', image: id });
    assert.equal(r.status, 200);
    const list = (await b.player('/v1/offers')).json.offers;
    assert.equal(list.length, 1); assert.equal(list[0].image, id);
    const img = await fetch(b.url + '/v1/img/' + id);
    assert.equal(img.status, 200); assert.equal(img.headers.get('content-type'), 'image/jpeg');
    assert.equal((await b.adm('/api/offers')).json.offers[0].image, id);          // the board shows it again when editing
  } finally { b.close(); }
});

test('an offer without a picture has an empty image, and a picture can be taken away', async () => {
  const b = await boot();
  try {
    const id = await b.upload();
    const saved = await b.adm('/api/offers/save', { adId: 'ad_ember', image: id });
    assert.equal((await b.player('/v1/offers')).json.offers[0].image, id);
    await b.adm('/api/offers/save', { id: saved.json.id, adId: 'ad_ember', image: '' });
    assert.equal((await b.player('/v1/offers')).json.offers[0].image, '');
    await b.adm('/api/offers/save', { adId: 'ad_plain' });
    assert.equal((await b.player('/v1/offers')).json.offers.find(o => o.adId === 'ad_plain').image, '');
  } finally { b.close(); }
});

test('a picture id the board never uploaded, or a made-up one, is refused', async () => {
  const b = await boot();
  try {
    assert.equal((await b.adm('/api/offers/save', { adId: 'ad_ember', image: 'a'.repeat(24) })).json.error, 'image_not_found');
    assert.equal((await b.adm('/api/offers/save', { adId: 'ad_ember', image: '../../etc/passwd' })).json.error, 'bad_image');
    assert.equal((await b.adm('/api/events/save', { kind: 'info', title: 'x', image: 'zz' })).json.error, 'bad_image');
    assert.equal((await b.player('/v1/offers')).json.offers.length, 0);
  } finally { b.close(); }
});

test('an event with a picture: the board of the game carries the id', async () => {
  const b = await boot();
  try {
    const id = await b.upload();
    const r = await b.adm('/api/events/save', { kind: 'info', title: 'ليلة كبرى', body: 'b', from: '2020-01-01T00:00:00Z', until: '2999-01-01T00:00:00Z', image: id });
    assert.equal(r.status, 200);
    const live = (await b.player('/v1/events')).json.live;
    assert.equal(live.length, 1); assert.equal(live[0].image, id);
    await b.adm('/api/events/save', { id: r.json.id, kind: 'info', title: 'ليلة كبرى', from: '2020-01-01T00:00:00Z', until: '2999-01-01T00:00:00Z' });
    assert.equal((await b.player('/v1/events')).json.live[0].image, '');
  } finally { b.close(); }
});
