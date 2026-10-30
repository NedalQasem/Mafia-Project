'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { createApp } = require('../src/app');
const { createChat } = require('../src/chat');
const { createFriends } = require('../src/friends');
const { createLeaderboard } = require('../src/leaderboard');
const { createStore } = require('../src/store');
const { createMail } = require('../src/mail');
const { createAdminStore } = require('../src/adminstore');
const { createAdmin, hashPassword } = require('../src/admin');

const PASSWORD = 'correct horse battery';
const T0 = Date.UTC(2026, 10, 2, 12);

function fakePlayfab() {
  const log = []; const bal = {};
  return {
    log, bal, failNext: null,
    authenticate: async t => t, getDisplayName: async pf => 'اسم ' + pf, getProgress: async () => ({ rankPoints: 100, level: 3 }),
    async addCurrency(pf, code, n) { if (this.failNext === 'coins' && code === 'CO') { this.failNext = null; throw new Error('down'); } log.push(['add', pf, code, n]); bal[pf + code] = (bal[pf + code] || 0) + n; },
    async subtractCurrency(pf, code, n) { if ((bal[pf + code] || 0) < n) throw new Error('insufficient'); log.push(['sub', pf, code, n]); bal[pf + code] -= n; },
    async grantItems(pf, ids) { log.push(['items', pf, ids]); },
    async getBalances(pf) { return { CO: bal[pf + 'CO'] || 0, GM: bal[pf + 'GM'] || 0 }; },
  };
}

async function boot() {
  const cfg = { currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: { publicCooldownMs: 0, cooldownMs: 0 } };
  const playfab = fakePlayfab();
  let clock = T0; const now = () => clock;
  const adminStore = createAdminStore({ file: null, now });
  const chat = createChat({ cfg, playfab, now });
  const friends = createFriends({ chat, cfg, now });
  const leaderboard = createLeaderboard({ chat, cfg, now });
  const mail = createMail({ cfg, playfab, now });
  const admin = createAdmin({ cfg, store: adminStore, chat, friends, leaderboard, mail, playfab, now, adminCfg: { passwordHash: hashPassword(PASSWORD), path: '/office' } });
  const { server } = createApp({ cfg, playfab, store: createStore(null), chat, friends, leaderboard, mail, admin, now });
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
  playfab.authenticate = async t => t.replace('-ticket-0123456789', '');
  await adm('/api/login', { password: PASSWORD });
  return { adm, player, playfab, mail, adminStore, friends, leaderboard, tick: ms => { clock += ms; }, close: () => server.close() };
}

test('a telegram to one player: listed, collected once, paid by the server, then stamped', async () => {
  const b = await boot();
  try {
    const num = (await b.friends.me('PLAYER0001')).num;
    const s = await b.adm('/api/mail/send', { audience: 'one', targets: [String(num)], title: { ar: 'هدية', en: 'Gift' }, body: { ar: 'نص', en: 'text' }, pack: { coins: 500, gems: 5, items: ['frame_ember'] }, expiresInDays: 7 });
    assert.equal(s.status, 200); assert.equal(s.json.count, 1);
    let l = (await b.player('PLAYER0001', '/v1/mail')).json;
    assert.equal(l.unread, 1); assert.equal(l.items[0].title.en, 'Gift'); assert.deepEqual(l.items[0].pack, { coins: 500, gems: 5, items: ['frame_ember'] });
    assert.equal((await b.player('PLAYER0002', '/v1/mail')).json.unread, 0);                      // nobody else sees it
    const c = await b.player('PLAYER0001', '/v1/mail/claim', { id: l.items[0].id });
    assert.deepEqual([c.json.coins, c.json.gems, c.json.items], [500, 5, ['frame_ember']]);
    assert.deepEqual(b.playfab.log, [['add', 'PLAYER0001', 'CO', 500], ['add', 'PLAYER0001', 'GM', 5], ['items', 'PLAYER0001', ['frame_ember']]]);
    assert.equal((await b.player('PLAYER0001', '/v1/mail/claim', { id: l.items[0].id })).status, 409);     // never twice
    l = (await b.player('PLAYER0001', '/v1/mail')).json; assert.equal(l.unread, 0); assert.ok(l.items[0].claimed > 0);
    assert.equal(b.playfab.log.length, 3);
  } finally { b.close(); }
});

test('a failed payment can be collected again without paying the first steps twice', async () => {
  const b = await boot();
  try {
    await b.adm('/api/mail/send', { audience: 'one', targets: ['PLAYER0003'], title: { ar: 'x' }, pack: { coins: 100, gems: 2 }, expiresInDays: 7 });
    const id = (await b.player('PLAYER0003', '/v1/mail')).json.items[0].id;
    b.playfab.failNext = 'coins';
    assert.equal((await b.player('PLAYER0003', '/v1/mail/claim', { id })).status, 502);
    assert.equal((await b.player('PLAYER0003', '/v1/mail')).json.unread, 1);                      // still waiting
    assert.equal((await b.player('PLAYER0003', '/v1/mail/claim', { id })).status, 200);
    assert.deepEqual(b.playfab.log, [['add', 'PLAYER0003', 'CO', 100], ['add', 'PLAYER0003', 'GM', 2]]);
  } finally { b.close(); }
});

test('a broadcast reaches everybody, each collects his own, and the admin sees how many did', async () => {
  const b = await boot();
  try {
    assert.equal((await b.adm('/api/mail/send', { audience: 'all', title: { ar: 'للكل' }, pack: { coins: 1000 } })).json.error, 'confirm_required');
    await b.adm('/api/mail/send', { audience: 'all', confirm: true, title: { ar: 'للكل', en: 'All' }, pack: { coins: 1000 }, expiresInDays: 3 });
    for (const pf of ['PLAYER0001', 'PLAYER0002']) assert.equal((await b.player(pf, '/v1/mail')).json.unread, 1);
    const id = (await b.player('PLAYER0001', '/v1/mail')).json.items[0].id;
    await b.player('PLAYER0001', '/v1/mail/claim', { id });
    assert.equal((await b.player('PLAYER0001', '/v1/mail')).json.unread, 0); assert.equal((await b.player('PLAYER0002', '/v1/mail')).json.unread, 1);
    assert.equal((await b.adm('/api/mail/sent')).json.sent[0].claimed, 1);
    b.tick(4 * 86400000);                                                                          // after the expiry only the ones not collected vanish
    assert.equal((await b.player('PLAYER0002', '/v1/mail')).json.unread, 0);
    assert.equal((await b.player('PLAYER0002', '/v1/mail/claim', { id })).status, 410);
  } finally { b.close(); }
});

test('claim all collects every waiting telegram in one go', async () => {
  const b = await boot();
  try {
    for (const c of [100, 200, 300]) await b.adm('/api/mail/send', { audience: 'one', targets: ['PLAYER0004'], title: { ar: 'م' + c }, pack: { coins: c }, expiresInDays: 7 });
    const r = (await b.player('PLAYER0004', '/v1/mail/claim-all')).json;
    assert.equal(r.claimed, 3); assert.equal(r.coins, 600); assert.equal(b.playfab.bal.PLAYER0004CO, 600);
    assert.equal((await b.player('PLAYER0004', '/v1/mail/claim-all')).json.claimed, 0);
  } finally { b.close(); }
});

test('mail sending is checked: title, recipients, pack and unknown players', async () => {
  const b = await boot();
  try {
    const err = async body => (await b.adm('/api/mail/send', body)).json.error;
    assert.equal(await err({ audience: 'one', targets: ['PLAYER0001'] }), 'title_required');
    assert.equal(await err({ audience: 'nobody', title: { ar: 'x' } }), 'bad_audience');
    assert.equal(await err({ audience: 'one', targets: ['999999'], title: { ar: 'x' } }), 'unknown_player');
    assert.equal(await err({ audience: 'one', targets: ['PLAYER0001'], title: { ar: 'x' }, packId: 'nope' }), 'pack_not_found');
    assert.equal(await err({ audience: 'one', targets: ['PLAYER0001'], title: { ar: 'x' }, pack: { coins: 999999999 } }), 'bad_coins');
    assert.equal(await err({ audience: 'one', targets: [], title: { ar: 'x' } }), 'bad_targets');
    assert.equal(await err({ audience: 'active7', title: { ar: 'x' } }), 'confirm_required');
  } finally { b.close(); }
});

test('active7 reaches the players who finished a match in the last week', async () => {
  const b = await boot();
  try {
    b.leaderboard.record('PLAYER0001', { won: true, role: 'Mafia', survived: true, durationSec: 60, rankPointsGained: 10, rankPointsNow: 10 });
    const r = await b.adm('/api/mail/send', { audience: 'active7', confirm: true, title: { ar: 'نشطين' }, pack: { gems: 1 } });
    assert.equal(r.json.count, 1);
    assert.equal((await b.player('PLAYER0001', '/v1/mail')).json.unread, 1); assert.equal((await b.player('PLAYER0002', '/v1/mail')).json.unread, 0);
  } finally { b.close(); }
});

test('packages: saved, used by id when sending, and checked', async () => {
  const b = await boot();
  try {
    assert.equal((await b.adm('/api/packs/save', { name: { ar: '' }, coins: 5 })).json.error, 'name_required');
    assert.equal((await b.adm('/api/packs/save', { name: { ar: 'فاضي' } })).json.error, 'empty_pack');
    assert.equal((await b.adm('/api/packs/save', { name: { ar: 'س' }, coins: 5, items: ['bad id'] })).json.error, 'bad_items');
    const id = (await b.adm('/api/packs/save', { name: { ar: 'هدية العيد', en: 'Eid gift' }, coins: 1000, gems: 5 })).json.id;
    assert.equal((await b.adm('/api/packs')).json.packs.length, 1);
    await b.adm('/api/mail/send', { audience: 'one', targets: ['PLAYER0005'], title: { ar: 'عيد' }, packId: id });
    assert.deepEqual((await b.player('PLAYER0005', '/v1/mail')).json.items[0].pack, { coins: 1000, gems: 5, items: [] });
    assert.equal((await b.adm('/api/packs/delete', { id })).status, 200);
    assert.equal((await b.adm('/api/packs/delete', { id })).status, 404);
  } finally { b.close(); }
});

test('balance: add and take coins or gems with a reason, never below zero, and it shows on the player card', async () => {
  const b = await boot();
  try {
    const adj = body => b.adm('/api/players/balance', Object.assign({ pf: 'PLAYER0006', currency: 'coins', amount: 300, reason: 'تعويض' }, body));
    assert.equal((await adj({ op: 'add' })).status, 200);
    assert.equal((await adj({ op: 'add', reason: 'x' })).json.error, 'reason_required');
    assert.equal((await adj({ op: 'add', amount: 0 })).json.error, 'bad_amount');
    assert.equal((await adj({ op: 'add', currency: 'gold' })).json.error, 'bad_currency');
    assert.equal((await adj({ op: 'steal' })).json.error, 'bad_op');
    assert.equal((await adj({ op: 'remove', amount: 1000 })).json.error, 'insufficient_or_failed');
    assert.equal((await adj({ op: 'remove', amount: 100 })).status, 200);
    await adj({ op: 'add', currency: 'gems', amount: 4 });
    const card = (await b.adm('/api/players?q=PLAYER0006')).json.players[0];
    assert.deepEqual(card.balances, { coins: 200, gems: 4 });
    const log = (await b.adm('/api/overview')).json.activity.map(a => a.action);
    assert.ok(log.includes('balance_add') && log.includes('balance_remove'));
  } finally { b.close(); }
});
