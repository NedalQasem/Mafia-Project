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
  const log = []; const bal = {}; const names = {};
  return {
    log, bal, names, failNext: null,
    authenticate: async t => t, getDisplayName: async pf => names[pf] || 'اسم ' + pf,
    async setDisplayName(pf, n) { if (n === 'TAKEN') throw Object.assign(new Error('NameNotAvailable'), { code: 'NameNotAvailable' }); names[pf] = n; },
    async banUser(pf, reason, hours) { log.push(['ban', pf, reason, hours]); },
    async unbanUser(pf) { log.push(['unban', pf]); }, getProgress: async () => ({ rankPoints: 100, level: 3 }),
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

test('balance: an added amount arrives as a gift in the mail, taking is at once, never below zero, and it shows on the player card', async () => {
  const b = await boot();
  try {
    const adj = body => b.adm('/api/players/balance', Object.assign({ pf: 'PLAYER0006', currency: 'coins', amount: 300, reason: 'تعويض' }, body));
    const collectAll = async () => { for (const m of (await b.player('PLAYER0006', '/v1/mail')).json.items) if (!m.claimed) await b.player('PLAYER0006', '/v1/mail/claim', { id: m.id }); };
    assert.equal((await adj({ op: 'add' })).json.viaMail, true);
    assert.deepEqual((await b.adm('/api/players?q=PLAYER0006')).json.players[0].balances, { coins: 0, gems: 0 });   // nothing until he collects
    const gift = (await b.player('PLAYER0006', '/v1/mail')).json.items[0];
    assert.equal(gift.title.ar, 'هدية من إدارة الحارة'); assert.equal(gift.pack.coins, 300); assert.ok(!JSON.stringify(gift).includes('تعويض'));   // the reason stays in the office
    await collectAll();
    assert.equal((await adj({ op: 'add', reason: 'x' })).json.error, 'reason_required');
    assert.equal((await adj({ op: 'add', amount: 0 })).json.error, 'bad_amount');
    assert.equal((await adj({ op: 'add', currency: 'gold' })).json.error, 'bad_currency');
    assert.equal((await adj({ op: 'steal' })).json.error, 'bad_op');
    assert.equal((await adj({ op: 'remove', amount: 1000 })).json.error, 'insufficient_or_failed');
    assert.equal((await adj({ op: 'remove', amount: 100 })).status, 200);
    await adj({ op: 'add', currency: 'gems', amount: 4 }); await collectAll();
    const card = (await b.adm('/api/players?q=PLAYER0006')).json.players[0];
    assert.deepEqual(card.balances, { coins: 200, gems: 4 });
    const log = (await b.adm('/api/overview')).json.activity.map(a => a.action);
    assert.ok(log.includes('balance_add') && log.includes('balance_remove'));
  } finally { b.close(); }
});

test('admin: an event with a ranking is saved only with valid prizes, shows its ranking, and gives the prizes once', async () => {
  const { createEvents } = require('../src/events');
  const cfg = { currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: { publicCooldownMs: 0, cooldownMs: 0 } };
  const playfab = fakePlayfab();
  const adminStore = createAdminStore({ file: null });
  const chat = createChat({ cfg, playfab });
  const friends = createFriends({ chat, cfg });
  const leaderboard = createLeaderboard({ chat, cfg });
  const mail = createMail({ cfg, playfab });
  const events = createEvents({ cfg, playfab, mail, packs: () => adminStore.packs, nameOf: async pf => (await chat.who(pf)).name, source: () => adminStore.events });
  const admin = createAdmin({ cfg, store: adminStore, chat, friends, leaderboard, mail, playfab, events, adminCfg: { passwordHash: hashPassword(PASSWORD), path: '/office' } });
  const { server } = createApp({ cfg, playfab, store: createStore(null), chat, friends, leaderboard, mail, events, admin });
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
  try {
    await adm('/api/login', { password: PASSWORD });
    const pack = (await adm('/api/packs/save', { name: { ar: 'ذهبية' }, coins: 5000 })).json.id;
    const base = { kind: 'info', title: 'سباق', from: '2020-01-01T00:00:00Z', until: '2999-01-01T00:00:00Z' };
    const bad = async ranking => (await adm('/api/events/save', Object.assign({}, base, { ranking }))).json.error;
    assert.equal(await bad({ enabled: true, metric: 'luck', winners: 3, prizes: [] }), 'bad_metric');
    assert.equal(await bad({ enabled: true, metric: 'wins', winners: 99, prizes: [] }), 'bad_winners');
    assert.equal(await bad({ enabled: true, metric: 'wins', winners: 3, prizes: [{ from: 2, to: 5, packId: pack }] }), 'bad_prize_ranks');
    assert.equal(await bad({ enabled: true, metric: 'wins', winners: 3, prizes: [{ from: 1, to: 1, packId: 'nope' }] }), 'pack_not_found');
    const id = (await adm('/api/events/save', Object.assign({}, base, { ranking: { enabled: true, metric: 'wins', winners: 3, distribution: 'manual', prizes: [{ from: 1, to: 1, packId: pack }] } }))).json.id;
    events.record('PLAYER0001', { won: true, points: 50, secs: 100 }); events.record('PLAYER0001', { won: true, points: 50, secs: 100 }); events.record('PLAYER0002', { won: true, points: 50, secs: 100 });
    const r = (await adm('/api/events/ranking?id=' + id)).json;
    assert.deepEqual(r.rows.map(x => [x.rank, x.pf, x.value]), [[1, 'PLAYER0001', 2], [2, 'PLAYER0002', 1]]); assert.equal(r.rows[0].prize.coins, 5000); assert.equal(r.rows[1].prize, null); assert.equal(r.distributed, null);
    assert.equal((await adm('/api/events/distribute', { id })).json.count, 1);
    assert.equal(mail.list('PLAYER0001').items[0].pack.coins, 5000);
    assert.equal((await adm('/api/events/distribute', { id })).json.error, 'already_distributed');
    assert.equal((await adm('/api/events/ranking?id=' + id)).json.distributed.by, 'admin');
    assert.equal((await adm('/api/events/ranking?id=nope')).status, 404);
  } finally { server.close(); }
});

test('codes through the admin and the player routes: create, redeem once, wrong codes, switch off, delete', async () => {
  const { createCodes } = require('../src/codes');
  const cfg = { currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: { publicCooldownMs: 0, cooldownMs: 0 } };
  const playfab = fakePlayfab();
  const adminStore = createAdminStore({ file: null });
  const chat = createChat({ cfg, playfab });
  const friends = createFriends({ chat, cfg });
  const leaderboard = createLeaderboard({ chat, cfg });
  const mail = createMail({ cfg, playfab });
  const codes = createCodes({ store: adminStore, mail, packs: () => adminStore.packs });
  const admin = createAdmin({ cfg, store: adminStore, chat, friends, leaderboard, mail, playfab, codes, adminCfg: { passwordHash: hashPassword(PASSWORD), path: '/office' } });
  const { server } = createApp({ cfg, playfab, store: createStore(null), chat, friends, leaderboard, mail, codes, admin });
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
  const player = async (pf, code) => {
    const res = await fetch(url + '/v1/codes/redeem', { method: 'POST', headers: { 'content-type': 'application/json', 'x-authorization': pf + '-ticket-0123456789' }, body: JSON.stringify({ code }) });
    return { status: res.status, json: await res.json() };
  };
  playfab.authenticate = async t => t.replace('-ticket-0123456789', '');
  try {
    await adm('/api/login', { password: PASSWORD });
    const pack = (await adm('/api/packs/save', { name: { ar: 'عيد' }, coins: 1000, gems: 5 })).json.id;
    assert.equal((await adm('/api/codes/save', { code: 'x', packId: pack })).json.error, 'bad_code_format');
    assert.equal((await adm('/api/codes/save', { code: 'MAFIA2026', packId: 'nope' })).json.error, 'pack_not_found');
    assert.equal((await adm('/api/codes/save', { code: 'MAFIA2026', packId: pack, maxUses: -4 })).json.error, 'bad_max_uses');
    assert.equal((await adm('/api/codes/save', { code: 'mafia2026', packId: pack, maxUses: 2 })).json.code, 'MAFIA2026');
    const random = (await adm('/api/codes/save', { packId: pack })).json.code; assert.match(random, /^[A-Z2-9]{8}$/);
    assert.equal((await adm('/api/codes')).json.codes.length, 2);
    assert.equal((await player('PLAYER0001', 'mafia2026')).status, 200);
    assert.equal(mail.list('PLAYER0001').items[0].pack.coins, 1000);
    assert.equal((await player('PLAYER0001', 'MAFIA2026')).json.error, 'already_used');
    assert.equal((await player('PLAYER0002', 'WRONGCODE')).json.error, 'bad_code');
    assert.equal((await adm('/api/codes')).json.codes.find(c => c.code === 'MAFIA2026').uses, 1);
    await adm('/api/codes/toggle', { code: 'MAFIA2026', enabled: false });
    assert.equal((await player('PLAYER0003', 'MAFIA2026')).json.error, 'bad_code');
    await adm('/api/codes/toggle', { code: 'MAFIA2026', enabled: true });
    assert.equal((await player('PLAYER0003', 'MAFIA2026')).status, 200);
    assert.equal((await player('PLAYER0004', 'MAFIA2026')).json.error, 'bad_code');                 // two uses were the limit
    assert.equal((await adm('/api/codes/delete', { code: 'MAFIA2026' })).status, 200);
    assert.equal((await adm('/api/codes/delete', { code: 'MAFIA2026' })).status, 404);
  } finally { server.close(); }
});

test('events and offers keep the English texts next to the Arabic ones, and the players get both', async () => {
  const { createEvents } = require('../src/events');
  const { createOffers } = require('../src/offers');
  const b = await boot();
  try {
    const ev = (await b.adm('/api/events/save', { kind: 'info', title: 'ليلة', titleEn: 'Night', kicker: 'بطولة', kickerEn: 'Tournament', bodyEn: 'A big night.' })).json;
    assert.equal(ev.ok, true);
    const e = (await b.adm('/api/events')).json.events[0];
    assert.deepEqual([e.title, e.titleEn, e.kickerEn, e.bodyEn], ['ليلة', 'Night', 'Tournament', 'A big night.']);
    const onlyEn = (await b.adm('/api/events/save', { kind: 'info', titleEn: 'Only English' })).json;
    assert.equal((await b.adm('/api/events')).json.events.find(x => x.id === onlyEn.id).title, 'Only English');         // the Arabic title is never empty
    assert.equal((await b.adm('/api/events/save', { kind: 'info' })).json.error, 'title_required');
    const board = createEvents({ cfg: {}, source: () => (b.adminStore.events), now: () => Date.now() }).board('p').live.find(x => x.id === ev.id);
    assert.deepEqual([board.title, board.titleEn, board.kickerEn, board.bodyEn], ['ليلة', 'Night', 'Tournament', 'A big night.']);
    await b.adm('/api/offers/save', { adId: 'ad_ember', headline: 'إطار الجمر', headlineEn: 'Ember frame', stamp: 'جديد', stampEn: 'NEW', mastEn: 'Tonight only', bodyEn: 'A glowing ring.' });
    const o = createOffers({ cfg: {}, source: () => b.adminStore.offers, now: () => Date.now() }).active()[0];
    assert.deepEqual([o.headline, o.headlineEn, o.stampEn, o.mastEn, o.bodyEn], ['إطار الجمر', 'Ember frame', 'NEW', 'Tonight only', 'A glowing ring.']);
  } finally { b.close(); }
});

test('support: rename (3 to 25 characters, taken names refused), ban and unban, all in the player log', async () => {
  const b = await boot();
  try {
    const pf = 'PLAYER0007';
    assert.equal((await b.adm('/api/players/rename', { pf, name: 'ab' })).json.error, 'bad_name');
    assert.equal((await b.adm('/api/players/rename', { pf, name: 'TAKEN' })).json.error, 'name_taken');
    assert.equal((await b.adm('/api/players/rename', { pf, name: 'أبو فارس الجديد' })).status, 200);
    assert.equal(b.playfab.names[pf], 'أبو فارس الجديد');
    assert.equal((await b.adm('/api/players?q=' + pf)).json.players[0].name, 'أبو فارس الجديد');             // the cached old name is gone

    assert.equal((await b.adm('/api/players/ban', { pf, reason: 'x', hours: 0 })).json.error, 'reason_required');
    assert.equal((await b.adm('/api/players/ban', { pf, reason: 'غش بالمباريات', hours: 99999 })).json.error, 'bad_hours');
    assert.equal((await b.adm('/api/players/ban', { pf, reason: 'غش بالمباريات', hours: 48 })).status, 200);
    let card = (await b.adm('/api/players?q=' + pf)).json.players[0];
    assert.equal(card.ban.reason, 'غش بالمباريات'); assert.ok(card.ban.until > Date.now());
    assert.deepEqual(b.playfab.log.find(l => l[0] === 'ban'), ['ban', pf, 'غش بالمباريات', 48]);
    assert.equal((await b.adm('/api/players/unban', { pf })).status, 200);
    card = (await b.adm('/api/players?q=' + pf)).json.players[0];
    assert.equal(card.ban, null);
    assert.deepEqual(card.log.map(l => l.action), ['unban', 'ban', 'rename']);                                 // newest first, only this player
    assert.equal((await b.adm('/api/players/rename', { pf: 'x', name: 'valid name' })).json.error, 'bad_player');
  } finally { b.close(); }
});

test('new mail is announced at once on the chat poll: to its recipients, and a broadcast to everybody online', async () => {
  const chat = createChat({ cfg: { chat: { pollMaxMs: 2000 } }, playfab: { getDisplayName: async () => null, getProgress: async () => null }, log: { error() {} } });
  const mail = createMail({ cfg: { currencyCode: 'CO' }, playfab: null, notify: pfs => { for (const pf of (pfs || chat.onlinePlayers())) chat.pushEvent(pf, { type: 'mail' }); } });
  await chat.poll('PLAYER0001', 0, 0); await chat.poll('PLAYER0002', 0, 0);           // both games are polling
  const waiting = chat.poll('PLAYER0001', 0, 2000);                                    // a long poll that waits for news
  const t0 = Date.now();
  mail.send({ audience: 'players', recipients: ['PLAYER0001'], title: { ar: 'هدية' }, pack: { coins: 100 } });
  const r = await waiting;
  assert.ok(Date.now() - t0 < 500, 'woken at once');
  assert.deepEqual(r.events.map(e => e.type), ['mail']);
  assert.deepEqual((await chat.poll('PLAYER0002', 0, 0)).events, []);                  // not his mail
  mail.send({ audience: 'all', title: { ar: 'للكل' } });
  assert.deepEqual((await chat.poll('PLAYER0002', 0, 0)).events.map(e => e.type), ['mail']);
});
