'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createApp } = require('../src/app');
const { createChat } = require('../src/chat');
const { createFriends } = require('../src/friends');
const { createLeaderboard } = require('../src/leaderboard');
const { createStore } = require('../src/store');
const { createMail } = require('../src/mail');
const { createImages } = require('../src/images');
const { createAdminStore } = require('../src/adminstore');
const { createAdmin, hashPassword } = require('../src/admin');

const PASSWORD = 'correct horse battery';
const JPEG = Buffer.concat([Buffer.from([0xff, 0xd8, 0xff, 0xe0]), Buffer.alloc(200, 7)]);
const PNG = Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), Buffer.alloc(100, 1)]);

async function boot() {
  const cfg = { currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: { publicCooldownMs: 0, cooldownMs: 0 } };
  const playfab = { authenticate: async t => t.replace('-ticket-0123456789', ''), getDisplayName: async pf => 'اسم ' + pf, getProgress: async () => ({ rankPoints: 1, level: 1 }), addCurrency: async () => {}, getBalances: async () => ({ CO: 0, GM: 0 }) };
  const adminStore = createAdminStore({ file: null });
  const chat = createChat({ cfg, playfab });
  const friends = createFriends({ chat, cfg });
  const leaderboard = createLeaderboard({ chat, cfg });
  const notified = [];
  const mail = createMail({ cfg, playfab, notify: p => notified.push(p) });
  const images = createImages();
  const admin = createAdmin({ cfg, store: adminStore, chat, friends, leaderboard, mail, playfab, images, adminCfg: { passwordHash: hashPassword(PASSWORD), path: '/office' } });
  const { server } = createApp({ cfg, playfab, store: createStore(null), chat, friends, leaderboard, mail, images, admin });
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
  const player = async (pf, p, body = {}) => {
    const res = await fetch(url + p, { method: 'POST', headers: { 'content-type': 'application/json', 'x-authorization': pf + '-ticket-0123456789' }, body: JSON.stringify(body) });
    return { status: res.status, json: await res.json() };
  };
  await adm('/api/login', { password: PASSWORD });
  return { adm, player, mail, images, notified, url, close: () => server.close() };
}

const msg = (o = {}) => Object.assign({ pf: 'PLAYER0001', kind: 'note', title: { ar: 'ملاحظة', en: 'A note' }, body: { ar: 'نص الرسالة', en: 'The text' } }, o);

test('a note reaches the player with a picture, unread, and the game is told at once', async () => {
  const b = await boot();
  try {
    const up = await b.adm('/api/upload-image', { data: JPEG.toString('base64') });
    assert.equal(up.status, 200); assert.match(up.json.id, /^[a-f0-9]{24}$/);
    const r = await b.adm('/api/players/message', msg({ image: up.json.id }));
    assert.equal(r.status, 200);
    assert.deepEqual(b.notified.at(-1), ['PLAYER0001']);
    const l = (await b.player('PLAYER0001', '/v1/mail')).json;
    assert.equal(l.unread, 1);
    const m = l.items[0];
    assert.equal(m.kind, 'note'); assert.equal(m.image, up.json.id); assert.equal(m.must, false); assert.equal(m.claimed, 0); assert.equal(m.exp, 0);
    assert.equal((await b.player('PLAYER0002', '/v1/mail')).json.unread, 0);                  // nobody else gets it
    const img = await fetch(b.url + '/v1/img/' + up.json.id);                                // the picture needs no ticket
    assert.equal(img.status, 200); assert.equal(img.headers.get('content-type'), 'image/jpeg'); assert.equal(Buffer.from(await img.arrayBuffer()).length, JPEG.length);
  } finally { b.close(); }
});

test('a warning is "must read" by default; reading it is remembered, repeatable, and "claim all" never reads it for the player', async () => {
  const b = await boot();
  try {
    assert.equal((await b.adm('/api/players/message', msg({ kind: 'warn' }))).status, 200);
    let m = (await b.player('PLAYER0001', '/v1/mail')).json.items[0];
    assert.equal(m.kind, 'warn'); assert.equal(m.must, true);
    const all = (await b.player('PLAYER0001', '/v1/mail/claim-all')).json;
    assert.equal(all.claimed, 0);
    assert.equal((await b.player('PLAYER0001', '/v1/mail')).json.unread, 1);               // still unread
    const rd = await b.player('PLAYER0001', '/v1/mail/read', { id: m.id });
    assert.equal(rd.status, 200); assert.ok(rd.json.read > 0);
    assert.equal((await b.player('PLAYER0001', '/v1/mail/read', { id: m.id })).status, 200);
    const l = (await b.player('PLAYER0001', '/v1/mail')).json;
    assert.equal(l.unread, 0); assert.ok(l.items[0].claimed > 0);
    assert.equal((await b.player('PLAYER0001', '/v1/mail/read', { id: 'nope' })).status, 404);
    assert.equal((await b.adm('/api/players/message', msg({ kind: 'warn', must: false }))).status, 200);
    assert.equal((await b.player('PLAYER0001', '/v1/mail')).json.items[0].must, false);       // the admin can make it a soft warning
  } finally { b.close(); }
});

test('a telegram with a package cannot be "read" away, only collected', async () => {
  const b = await boot();
  try {
    b.mail.send({ audience: 'players', recipients: ['PLAYER0001'], title: { ar: 'هدية' }, body: { ar: 'x' }, pack: { coins: 10 } });
    const m = (await b.player('PLAYER0001', '/v1/mail')).json.items[0];
    const r = await b.player('PLAYER0001', '/v1/mail/read', { id: m.id });
    assert.equal(r.status, 409);
    assert.equal((await b.player('PLAYER0001', '/v1/mail/claim', { id: m.id })).status, 200);
  } finally { b.close(); }
});

test('bad kinds, empty texts, bad players and missing pictures are refused', async () => {
  const b = await boot();
  try {
    assert.equal((await b.adm('/api/players/message', msg({ kind: 'spam' }))).json.error, 'bad_kind');
    assert.equal((await b.adm('/api/players/message', msg({ title: {} }))).json.error, 'title_required');
    assert.equal((await b.adm('/api/players/message', msg({ body: { ar: ' ' } }))).json.error, 'body_required');
    assert.equal((await b.adm('/api/players/message', msg({ pf: 'x' }))).json.error, 'bad_player');
    assert.equal((await b.adm('/api/players/message', msg({ image: 'a'.repeat(24) }))).json.error, 'image_not_found');
    assert.equal((await b.player('PLAYER0001', '/v1/mail')).json.items.length, 0);
  } finally { b.close(); }
});

test('only real pictures are stored: wrong bytes and big files are refused, PNG works, the image route is strict', async () => {
  const b = await boot();
  try {
    assert.equal((await b.adm('/api/upload-image', { data: Buffer.from('not a picture at all').toString('base64') })).json.error, 'bad_image');
    assert.equal((await b.adm('/api/upload-image', { data: '' })).json.error, 'bad_image');
    const big = Buffer.concat([JPEG, Buffer.alloc(1100 * 1024)]);
    assert.equal((await b.adm('/api/upload-image', { data: big.toString('base64') })).json.error, 'image_too_big');
    const png = await b.adm('/api/upload-image', { data: PNG.toString('base64') });
    assert.equal(png.status, 200);
    assert.equal((await fetch(b.url + '/v1/img/' + png.json.id)).headers.get('content-type'), 'image/png');
    assert.equal((await fetch(b.url + '/v1/img/' + 'f'.repeat(24))).status, 404);
    assert.equal((await fetch(b.url + '/v1/img/..%2F..%2Fetc%2Fpasswd')).status, 404);
  } finally { b.close(); }
});

test('the player card lists his messages with their read state, and the log has them', async () => {
  const b = await boot();
  try {
    await b.adm('/api/players/message', msg({ kind: 'warn' }));
    await b.adm('/api/players/message', msg({ title: { ar: 'عادية' } }));
    const first = (await b.player('PLAYER0001', '/v1/mail')).json.items.find(m => m.kind === 'warn');
    await b.player('PLAYER0001', '/v1/mail/read', { id: first.id });
    const card = (await b.adm('/api/players?q=PLAYER0001')).json.players[0];
    assert.deepEqual(card.messages.map(m => [m.kind, m.read > 0]), [['note', false], ['warn', true]]);
    assert.deepEqual(card.log.map(l => l.action), ['message_send', 'message_send']);
  } finally { b.close(); }
});
