'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createApp } = require('../src/app');
const { createChat } = require('../src/chat');
const { createFriends } = require('../src/friends');
const { createLeaderboard } = require('../src/leaderboard');
const { createStore } = require('../src/store');
const { createAdminStore } = require('../src/adminstore');
const { createAdmin, hashPassword } = require('../src/admin');
const { createRecovery } = require('../src/recovery');

const PASSWORD = 'correct horse battery';
const T0 = Date.UTC(2026, 10, 2, 12);

async function boot({ failLogin = false } = {}) {
  const cfg = { currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: { publicCooldownMs: 0, cooldownMs: 0 } };
  const calls = [];
  const playfab = {
    authenticate: async t => t, getDisplayName: async pf => 'اسم ' + pf, getProgress: async () => ({ rankPoints: 100, level: 3 }),
    async linkServerCustomId(pf, id) { calls.push(['link', pf, id]); },
    async unlinkServerCustomId(pf, id) { calls.push(['unlink', pf, id]); },
    async loginServerCustomId(id) { calls.push(['login', id]); if (failLogin) throw new Error('down'); return { SessionTicket: 'ticket-of-' + id, PlayFabId: id.slice(4) }; },
  };
  let clock = T0; const now = () => clock;
  const adminStore = createAdminStore({ file: null, now });
  const chat = createChat({ cfg, playfab, now });
  const friends = createFriends({ chat, cfg, now });
  const leaderboard = createLeaderboard({ chat, cfg, now });
  const recovery = createRecovery({ store: adminStore, playfab, now, log: { error() {} } });
  const admin = createAdmin({ cfg, store: adminStore, chat, friends, leaderboard, playfab, recovery, now, adminCfg: { passwordHash: hashPassword(PASSWORD), path: '/office' } });
  const { server } = createApp({ cfg, playfab, store: createStore(null), chat, friends, leaderboard, recovery, admin, now });
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
  const rec = async (username, password) => {                       // no session ticket: the player has no account here yet
    const res = await fetch(url + '/v1/recover/login', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ username, password }) });
    return { status: res.status, json: await res.json() };
  };
  await adm('/api/login', { password: PASSWORD });
  return { adm, rec, calls, adminStore, tick: ms => { clock += ms; }, close: () => server.close() };
}

test('the admin makes a recovery account; the password is shown once and only its hash is kept', async () => {
  const b = await boot();
  try {
    const r = await b.adm('/api/players/recovery', { pf: 'PLAYER0001', username: 'Abu.Fares' });
    assert.equal(r.status, 200); assert.equal(r.json.username, 'abu.fares'); assert.match(r.json.password, /^[A-Za-z0-9]{4}-[A-Za-z0-9]{4}-[A-Za-z0-9]{4}$/);
    assert.deepEqual(b.calls, [['link', 'PLAYER0001', 'rec-PLAYER0001']]);
    assert.ok(!JSON.stringify(b.adminStore.recovery).includes(r.json.password));
    assert.match(b.adminStore.recovery.PLAYER0001.hash, /^scrypt:/);
    const card = (await b.adm('/api/players?q=PLAYER0001')).json.players[0];
    assert.equal(card.recovery.username, 'abu.fares'); assert.equal(card.recovery.count, 0); assert.ok(!('hash' in card.recovery));
    assert.deepEqual(card.log.map(l => l.action), ['recovery_set']);
  } finally { b.close(); }
});

test('bad user names and passwords are refused, and a name belongs to one player', async () => {
  const b = await boot();
  try {
    assert.equal((await b.adm('/api/players/recovery', { pf: 'PLAYER0001', username: 'ab' })).json.error, 'bad_username');
    assert.equal((await b.adm('/api/players/recovery', { pf: 'PLAYER0001', username: 'أبو فارس' })).json.error, 'bad_username');
    assert.equal((await b.adm('/api/players/recovery', { pf: 'PLAYER0001', username: 'abu.fares', password: 'short' })).json.error, 'bad_password');
    assert.equal((await b.adm('/api/players/recovery', { pf: 'x', username: 'abu.fares' })).json.error, 'bad_player');
    assert.equal((await b.adm('/api/players/recovery', { pf: 'PLAYER0001', username: 'abu.fares' })).status, 200);
    assert.equal((await b.adm('/api/players/recovery', { pf: 'PLAYER0002', username: 'ABU.fares' })).json.error, 'username_taken');
    assert.equal((await b.adm('/api/players/recovery', { pf: 'PLAYER0001', username: 'abu.fares' })).status, 200);   // same player may renew his own
  } finally { b.close(); }
});

test('login with the right pair gives a session of the old account; every failure looks the same', async () => {
  const b = await boot();
  try {
    const { json: made } = await b.adm('/api/players/recovery', { pf: 'PLAYER0001', username: 'abu.fares', password: 'myOwnPassword1' });
    assert.equal(made.password, 'myOwnPassword1');
    const ok = await b.rec('Abu.Fares', 'myOwnPassword1');
    assert.equal(ok.status, 200); assert.deepEqual(ok.json, { ok: true, sessionTicket: 'ticket-of-rec-PLAYER0001', playFabId: 'PLAYER0001' });
    const wrongPw = await b.rec('abu.fares', 'nope'), wrongName = await b.rec('nobody', 'myOwnPassword1'), bad = await b.rec('', '');
    for (const x of [wrongPw, wrongName, bad]) { assert.equal(x.status, 401); assert.equal(x.json.error, 'bad_login'); }
    const card = (await b.adm('/api/players?q=PLAYER0001')).json.players[0];
    assert.equal(card.recovery.count, 1); assert.equal(card.recovery.last, T0);
  } finally { b.close(); }
});

test('five wrong passwords lock the user name for 15 minutes, even with the right one', async () => {
  const b = await boot();
  try {
    await b.adm('/api/players/recovery', { pf: 'PLAYER0001', username: 'abu.fares', password: 'myOwnPassword1' });
    for (let i = 0; i < 5; i++) assert.equal((await b.rec('abu.fares', 'wrong' + i)).status, 401);
    const locked = await b.rec('abu.fares', 'myOwnPassword1');
    assert.equal(locked.status, 429); assert.equal(locked.json.error, 'locked');
    b.tick(16 * 60 * 1000);
    assert.equal((await b.rec('abu.fares', 'myOwnPassword1')).status, 200);
  } finally { b.close(); }
});

test('a new password replaces the old one; removing the account stops the login and unlinks PlayFab', async () => {
  const b = await boot();
  try {
    await b.adm('/api/players/recovery', { pf: 'PLAYER0001', username: 'abu.fares', password: 'firstPassword1' });
    await b.adm('/api/players/recovery', { pf: 'PLAYER0001', username: 'abu.fares', password: 'secondPassword2' });
    assert.equal((await b.rec('abu.fares', 'firstPassword1')).status, 401);
    assert.equal((await b.rec('abu.fares', 'secondPassword2')).status, 200);
    assert.equal(b.calls.filter(c => c[0] === 'link').length, 1);                      // linked once, at the first time
    assert.equal((await b.adm('/api/players/recovery/remove', { pf: 'PLAYER0001' })).status, 200);
    assert.equal((await b.rec('abu.fares', 'secondPassword2')).status, 401);
    assert.deepEqual(b.calls.at(-1), ['unlink', 'PLAYER0001', 'rec-PLAYER0001']);
    assert.equal((await b.adm('/api/players/recovery/remove', { pf: 'PLAYER0001' })).status, 404);
    assert.deepEqual((await b.adm('/api/players?q=PLAYER0001')).json.players[0].log.map(l => l.action), ['recovery_remove', 'recovery_set', 'recovery_set']);
  } finally { b.close(); }
});

test('when PlayFab is down the login answers try_again and the failure is not counted as a use', async () => {
  const b = await boot({ failLogin: true });
  try {
    await b.adm('/api/players/recovery', { pf: 'PLAYER0001', username: 'abu.fares', password: 'myOwnPassword1' });
    const r = await b.rec('abu.fares', 'myOwnPassword1');
    assert.equal(r.status, 502); assert.equal(r.json.error, 'try_again');
    assert.equal(b.adminStore.recovery.PLAYER0001.count, 0);
  } finally { b.close(); }
});
