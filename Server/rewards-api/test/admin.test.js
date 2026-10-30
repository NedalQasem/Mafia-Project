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
const { createEvents } = require('../src/events');
const { createOffers } = require('../src/offers');
const { createAdminStore } = require('../src/adminstore');
const { createAdmin, hashPassword, verifyPassword } = require('../src/admin');

const PASSWORD = 'correct horse battery';
const playfab = { authenticate: async t => t, getDisplayName: async pf => 'اسم ' + pf, getProgress: async () => ({ rankPoints: 1200, level: 7 }), addCurrency: async () => {} };

async function boot(opts = {}) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'admin-'));
  const cfg = Object.assign({ currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: { publicCooldownMs: 0, cooldownMs: 0 }, events: [], offers: [] }, opts.cfg);
  const adminStore = createAdminStore({ file: null });
  const reportsFile = path.join(dir, 'reports.jsonl');
  const chat = createChat({ cfg, playfab, extraWords: () => adminStore.words, reportsFile });
  const friends = createFriends({ chat, cfg, reportsFile });
  const leaderboard = createLeaderboard({ chat, cfg });
  const events = createEvents({ cfg, playfab, source: () => (adminStore.events !== null ? adminStore.events : cfg.events) });
  const offers = createOffers({ cfg, source: () => (adminStore.offers !== null ? adminStore.offers : cfg.offers) });
  const admin = createAdmin({ cfg, store: adminStore, chat, friends, leaderboard, reportsFile, adminCfg: opts.noPassword ? {} : { passwordHash: hashPassword(PASSWORD), path: '/office' } });
  const { server } = createApp({ cfg, playfab, store: createStore(null), chat, friends, leaderboard, offers, events, admin });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const url = 'http://127.0.0.1:' + server.address().port;
  let cookie = '';
  const call = async (p, body, headers = {}) => {
    const h = Object.assign({}, headers);
    if (cookie) h.cookie = cookie;
    if (body !== undefined) { h['content-type'] = 'application/json'; if (!('x-admin' in h)) h['x-admin'] = '1'; }
    const res = await fetch(url + '/office' + p, { method: body !== undefined ? 'POST' : 'GET', headers: h, body: body !== undefined ? JSON.stringify(body) : undefined });
    const sc = res.headers.get('set-cookie');
    if (sc) cookie = sc.split(';')[0];
    const text = await res.text();
    let json = null; try { json = JSON.parse(text); } catch { /* html */ }
    return { status: res.status, json, text, headers: res.headers };
  };
  const login = () => call('/api/login', { password: PASSWORD });
  return { call, login, adminStore, chat, friends, leaderboard, events, offers, admin, reportsFile, cfg, close: () => server.close(), url };
}

test('password hashes verify, and wrong or broken ones do not', () => {
  const h = hashPassword(PASSWORD);
  assert.match(h, /^scrypt:[0-9a-f]{32}:[0-9a-f]{64}$/);
  assert.ok(verifyPassword(PASSWORD, h)); assert.ok(!verifyPassword('nope', h)); assert.ok(!verifyPassword(PASSWORD, '')); assert.ok(!verifyPassword(PASSWORD, 'scrypt:zz:zz'));
});

test('without a password hash the board does not exist', async () => {
  const b = await boot({ noPassword: true });
  try { assert.equal((await b.call('/')).status, 404); assert.equal((await b.call('/api/overview')).status, 404); } finally { b.close(); }
});

test('the page is served with safe headers, the api needs the login and the X-Admin header', async () => {
  const b = await boot();
  try {
    const page = await b.call('/');
    assert.equal(page.status, 200); assert.match(page.text, /MAFIA ADMIN BOARD/); assert.match(page.text, /const BASE = '\/office'/);
    assert.equal(page.headers.get('x-frame-options'), 'DENY'); assert.match(page.headers.get('x-robots-tag'), /noindex/);
    assert.equal((await b.call('/api/overview')).status, 401);
    assert.equal((await b.call('/api/events/save', { kind: 'info', title: 'x' })).status, 401);
    assert.equal((await b.login()).status, 200);
    assert.equal((await b.call('/api/overview')).status, 200);
    assert.equal((await b.call('/api/events/save', { kind: 'info', title: 'x' }, { 'x-admin': '0' })).status, 403);
  } finally { b.close(); }
});

test('five wrong passwords lock the address, even for the right password', async () => {
  const b = await boot();
  try {
    for (let i = 0; i < 4; i++) { const r = await b.call('/api/login', { password: 'bad' + i }); assert.equal(r.status, 401); assert.equal(r.json.left, 4 - i); }
    assert.equal((await b.call('/api/login', { password: 'bad5' })).status, 401);
    const locked = await b.call('/api/login', { password: PASSWORD });
    assert.equal(locked.status, 429); assert.equal(locked.json.error, 'locked');
  } finally { b.close(); }
});

test('events: the first save copies config.json, edits apply live to the game, toggle and delete work', async () => {
  const b = await boot({ cfg: { events: [{ id: 'from-config', kind: 'info', title: 'Config event' }] } });
  try {
    await b.login();
    let r = await b.call('/api/events'); assert.equal(r.json.source, 'config'); assert.equal(r.json.events.length, 1);
    const save = await b.call('/api/events/save', { kind: 'bonus', title: 'Double', kicker: 'k', body: 'b', coinsMultiplier: 2, from: '2020-01-01T00:00:00Z', until: '2999-01-01T00:00:00Z' });
    assert.equal(save.status, 200); const id = save.json.id;
    r = await b.call('/api/events'); assert.equal(r.json.source, 'admin'); assert.deepEqual(r.json.events.map(e => e.id).sort(), ['from-config', id].sort());
    assert.equal(b.events.coinsMultiplier(), 2);                                        // the game's server logic sees it at once
    assert.equal(b.events.board('p1').live.some(e => e.id === id), true);
    await b.call('/api/events/toggle', { id, enabled: false }); assert.equal(b.events.coinsMultiplier(), 1);
    await b.call('/api/events/toggle', { id, enabled: true }); assert.equal(b.events.coinsMultiplier(), 2);
    await b.call('/api/events/save', { id, kind: 'bonus', title: 'Triple', coinsMultiplier: 3, from: '2020-01-01T00:00:00Z', until: '2999-01-01T00:00:00Z' }); assert.equal(b.events.coinsMultiplier(), 3);
    assert.equal((await b.call('/api/events/delete', { id })).status, 200); assert.equal(b.events.coinsMultiplier(), 1);
    assert.equal((await b.call('/api/events/delete', { id })).status, 404);
  } finally { b.close(); }
});

test('events: bad input is refused with a reason', async () => {
  const b = await boot();
  try {
    await b.login();
    const bad = async body => (await b.call('/api/events/save', body)).json.error;
    assert.equal(await bad({ kind: 'nonsense', title: 'x' }), 'bad_kind');
    assert.equal(await bad({ kind: 'info', title: ' ' }), 'title_required');
    assert.equal(await bad({ kind: 'bonus', title: 'x', coinsMultiplier: 9 }), 'bad_multiplier');
    assert.equal(await bad({ kind: 'challenge', title: 'x', goal: 0, rewardCoins: 5 }), 'bad_goal');
    assert.equal(await bad({ kind: 'challenge', title: 'x', goal: 5, rewardCoins: 99999 }), 'bad_reward');
    assert.equal(await bad({ kind: 'info', title: 'x', from: '2026-02-01T00:00:00Z', until: '2026-01-01T00:00:00Z' }), 'until_before_from');
    assert.equal(await bad({ kind: 'info', title: 'x', from: 'not a date' }), 'bad_from');
    assert.equal(await bad({ id: 'bad id!', kind: 'info', title: 'x' }), 'bad_id');
  } finally { b.close(); }
});

test('offers: saved offers are what the game gets, and one must point at an ad or an item', async () => {
  const b = await boot({ cfg: { offers: [{ adId: 'ad_old' }] } });
  try {
    await b.login();
    assert.equal((await b.call('/api/offers/save', { headline: 'x' })).json.error, 'ad_or_item_required');
    assert.equal((await b.call('/api/offers/save', { adId: 'bad id' })).json.error, 'bad_adId');
    const r = await b.call('/api/offers/save', { adId: 'ad_ember', stamp: '−٣٠٪', until: '2999-01-01T00:00:00Z' });
    assert.equal(r.status, 200);
    assert.deepEqual(b.offers.active().map(o => o.adId), ['ad_old', 'ad_ember']);       // the config offer was copied first
    await b.call('/api/offers/toggle', { id: r.json.id, enabled: false }); assert.deepEqual(b.offers.active().map(o => o.adId), ['ad_old']);
    await b.call('/api/offers/delete', { id: 'cfg-0' }); assert.deepEqual(b.offers.active().map(o => o.adId), []);
  } finally { b.close(); }
});

test('banned words added in the board hide in chat at once, both kinds', async () => {
  const b = await boot();
  try {
    await b.login();
    const say = async t => (await b.chat.send('p1', { channel: 'public', text: t })).text;
    assert.equal(await say('hello zzzword there'), 'hello zzzword there');
    await b.call('/api/words/add', { list: 'contains', word: 'zzz' });
    await b.call('/api/words/add', { list: 'exact', word: 'cat' });
    assert.equal(await say('hello zzzword there'), 'hello ******* there');
    assert.equal(await say('a cat and a catalog'), 'a *** and a catalog');                // whole word only
    assert.equal((await b.call('/api/words/add', { list: 'exact', word: 'two words' })).json.error, 'bad_word');
    assert.equal((await b.call('/api/words/add', { list: 'nope', word: 'x' })).json.error, 'bad_list');
    assert.deepEqual((await b.call('/api/words')).json.contains, ['zzz']);
    await b.call('/api/words/remove', { list: 'contains', word: 'zzz' });
    assert.equal(await say('hello zzzword there'), 'hello zzzword there');
    assert.equal((await b.call('/api/words/remove', { list: 'contains', word: 'zzz' })).status, 404);
  } finally { b.close(); }
});

test('reports: listed with names, can be muted or ignored once, and the mute is real', async () => {
  const b = await boot();
  try {
    await b.login();
    fs.writeFileSync(b.reportsFile, JSON.stringify({ ts: '2026-10-30T10:00:00Z', reporter: 'REPORTER1', target: 'TARGET0001', channel: 'public', messageId: 4, text: 'bad words', reason: 'abuse' }) + '\n' +
      JSON.stringify({ ts: '2026-10-30T11:00:00Z', type: 'player', reporter: 'REPORTER2', target: 'TARGET0002', reason: 'cheat', note: '' }) + '\n' + 'not json\n');
    let r = (await b.call('/api/reports')).json.reports;
    assert.equal(r.length, 2); assert.equal(r[0].targetName, 'اسم TARGET0002'); assert.equal(r[1].type, 'message'); assert.ok(r.every(x => x.status === 'new'));
    assert.equal((await b.call('/api/overview')).json.openReports, 2);
    assert.equal((await b.call('/api/reports/handle', { id: r[1].id, action: 'mute', minutes: 30 })).status, 200);
    assert.ok(b.chat.muteUntil('TARGET0001') > Date.now());
    await assert.rejects(b.chat.send('TARGET0001', { channel: 'public', text: 'hi' }), { code: 'muted' });
    await b.call('/api/reports/handle', { id: r[0].id, action: 'ignore' });
    r = (await b.call('/api/reports')).json.reports;
    assert.deepEqual(r.map(x => x.status), ['handled', 'handled']); assert.equal(r[1].muted, true);
    assert.equal((await b.call('/api/overview')).json.openReports, 0);
    assert.equal((await b.call('/api/reports/handle', { id: 'nope', action: 'ignore' })).status, 404);
    assert.equal((await b.call('/api/reports/handle', { id: r[0].id, action: 'delete everything' })).json.error, 'bad_action');
  } finally { b.close(); }
});

test('players: found by id, by number or by name, with stats and reports, and muted or freed', async () => {
  const b = await boot();
  try {
    await b.login();
    const num = (await b.friends.me('PLAYER0001')).num;
    b.leaderboard.record('PLAYER0001', { won: true, role: 'Mafia', survived: true, durationSec: 600, rankPointsGained: 50, rankPointsNow: 1250 });
    fs.writeFileSync(b.reportsFile, JSON.stringify({ ts: '2026-10-30T10:00:00Z', type: 'player', reporter: 'OTHER00001', target: 'PLAYER0001', reason: 'abuse', note: '' }) + '\n');
    for (const q of ['PLAYER0001', String(num), 'اسم PLAYER']) {
      const r = (await b.call('/api/players?q=' + encodeURIComponent(q))).json.players;
      assert.equal(r.length, 1, q); assert.equal(r[0].pf, 'PLAYER0001'); assert.equal(r[0].num, num); assert.equal(r[0].stats.games, 1); assert.equal(r[0].stats.wins, 1); assert.equal(r[0].reportsAgainst, 1); assert.equal(r[0].level, 7);
    }
    assert.deepEqual((await b.call('/api/players?q=')).json.players, []);
    const m = await b.call('/api/players/mute', { pf: 'PLAYER0001', minutes: 60 });
    assert.ok(m.json.mutedUntil > Date.now()); assert.ok((await b.call('/api/players?q=PLAYER0001')).json.players[0].mutedUntil > Date.now());
    assert.equal((await b.call('/api/players/mute', { pf: 'PLAYER0001', minutes: 0 })).json.mutedUntil, 0);
    assert.equal((await b.call('/api/players/mute', { pf: 'x', minutes: 5 })).json.error, 'bad_player');
  } finally { b.close(); }
});

test('overview counts every paid match once and keeps a log of what the admin did', async () => {
  const b = await boot();
  try {
    await b.login();
    b.admin.noteMatch('m1'); b.admin.noteMatch('m1'); b.admin.noteMatch('m2');
    b.leaderboard.record('P1AAAAAA', { won: true, role: 'Mafia', survived: true, durationSec: 60, rankPointsGained: 50, rankPointsNow: 50 });
    await b.call('/api/words/add', { list: 'exact', word: 'abc' });
    const o = (await b.call('/api/overview')).json;
    assert.equal(o.matchesToday, 2); assert.equal(o.days.length, 7); assert.equal(o.days[6].matches, 2); assert.equal(o.players, 1);
    assert.deepEqual(o.activity.map(a => a.action).slice(0, 2), ['word_add', 'login']);
    assert.equal((await b.call('/api/nothing')).status, 404);
  } finally { b.close(); }
});
