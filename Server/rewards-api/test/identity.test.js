'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createApp } = require('../src/app');
const { createChat } = require('../src/chat');
const { createStore } = require('../src/store');
const { createAccounts } = require('../src/accounts');
const { createGoogle, createFacebook, createIdentity } = require('../src/identity');

const WEB = '1007236526273-web.apps.googleusercontent.com';
const T0 = Date.UTC(2026, 10, 2, 12);
const JWT = sub => 'aaaaaaaaaaaa.' + sub + 'bbbbbbbbbbbbbbbb.cccccccccccc';       // looks like a token; the fake Google answers by the middle part

function setup({ aud = WEB, exp = (T0 + 3600000) / 1000, iss = 'https://accounts.google.com', status = 200 } = {}) {
  let clock = T0; const now = () => clock;
  const playfabLog = []; const customIds = {};                      // customId -> pf
  let nextPf = 1;
  const playfab = {
    authenticate: async t => t.replace('-ticket-0123456789', ''), getDisplayName: async pf => 'اسم ' + pf,
    async loginServerCustomId(id, create) {
      playfabLog.push(['login', id, !!create]);
      if (!customIds[id]) { if (!create) throw new Error('NotFound'); customIds[id] = 'NEWPF' + String(nextPf++).padStart(4, '0'); }
      return { SessionTicket: 'sess-' + customIds[id], PlayFabId: customIds[id] };
    },
    async linkServerCustomId(pf, id, force) { playfabLog.push(['link', pf, id, force]); if (customIds[id] && customIds[id] !== pf && !force) throw new Error('LinkedAccountAlreadyClaimed'); customIds[id] = pf; },
  };
  const fetchFn = async url => {
    const tok = new URL(url).searchParams.get('id_token'); const sub = tok.split('.')[1].replace(/b+$/, '');
    return { ok: status === 200, status, json: async () => ({ sub, aud, iss, exp: String(exp), email: sub + '@example.com', name: 'Name ' + sub }) };
  };
  const accounts = createAccounts({ cfg: {}, playfab, now, log: { error() {} } });
  const chat = createChat({ cfg: { chat: { publicCooldownMs: 0, cooldownMs: 0 } }, playfab, now });
  const identity = createIdentity({ accounts, playfab, chat, providers: { google: createGoogle({ clientIds: [WEB], fetchFn, now }) }, log: { error() {} } });
  return { accounts, identity, playfab, playfabLog, customIds, chat, now, tick: ms => { clock += ms; } };
}

test('a new Google identity gets an account, and the same identity opens it again', async () => {
  const s = setup();
  const a = await s.identity.login('google', JWT('sub1'));
  assert.equal(a.ok, true); assert.equal(a.created, true); assert.match(a.sessionTicket, /^sess-NEWPF/);
  assert.deepEqual(s.playfabLog[0], ['login', 'g-sub1', true]);
  assert.equal(s.accounts.owner('google', 'sub1'), a.playFabId); assert.equal(s.accounts.status(a.playFabId).guest, false);
  const b = await s.identity.login('google', JWT('sub1'));
  assert.equal(b.created, false); assert.equal(b.playFabId, a.playFabId); assert.deepEqual(s.playfabLog[1], ['login', 'g-sub1', false]);
});

test('a guest links Google; linking again is harmless; the account stops being a guest', async () => {
  const s = setup(); s.accounts.touch('GUEST001');
  assert.equal(s.accounts.status('GUEST001').guest, true);
  assert.deepEqual(await s.identity.link('GUEST001', 'google', JWT('sub2')), { ok: true, already: false });
  assert.deepEqual(s.playfabLog.at(-1), ['link', 'GUEST001', 'g-sub2', false]);          // never forced: nobody's identity is taken away
  assert.equal(s.accounts.status('GUEST001').guest, false); assert.deepEqual(s.accounts.status('GUEST001').methods, ['google']);
  assert.deepEqual(await s.identity.link('GUEST001', 'google', JWT('sub2')), { ok: true, already: true });
});

test('linking an identity that belongs to another account answers account_exists with that account name', async () => {
  const s = setup();
  s.accounts.touch('OLD00001'); s.accounts.link('OLD00001', 'google', 'sub3');
  await assert.rejects(s.identity.link('GUEST002', 'google', JWT('sub3')), e => e.code === 'account_exists' && e.status === 409 && e.extra.name === 'اسم OLD00001');
  assert.equal(s.accounts.status('GUEST002').guest, true);                                // nothing changed
});

test('bad tokens are refused: wrong app, wrong issuer, expired, malformed, Google says no', async () => {
  for (const [opts, tok] of [[{ aud: 'someone-else' }, JWT('x')], [{ iss: 'https://evil.example' }, JWT('x')], [{ exp: (T0 - 1000) / 1000 }, JWT('x')], [{ status: 400 }, JWT('x')], [{}, 'not-a-token'], [{}, '']]) {
    const s = setup(opts);
    await assert.rejects(s.identity.login('google', tok), e => e.code === 'bad_token' && e.status === 401);
  }
});

test('without a configured Google client the sign-in is off', async () => {
  const s = setup();
  const off = createIdentity({ accounts: s.accounts, playfab: s.playfab, providers: { google: createGoogle({ clientIds: [] }) } });
  await assert.rejects(off.login('google', JWT('x')), e => e.code === 'not_configured');
});

test('the routes: login needs no ticket, link needs one, and the conflict reaches the game', async () => {
  const s = setup();
  const cfg = { currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: { publicCooldownMs: 0, cooldownMs: 0 } };
  const { server } = createApp({ cfg, playfab: s.playfab, store: createStore(null), chat: s.chat, accounts: s.accounts, identity: s.identity, now: s.now });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const base = 'http://127.0.0.1:' + server.address().port;
  const post = async (path, body, ticket) => { const h = { 'content-type': 'application/json' }; if (ticket) h['x-authorization'] = ticket; const res = await fetch(base + path, { method: 'POST', headers: h, body: JSON.stringify(body) }); return { status: res.status, json: await res.json() }; };
  try {
    const login = await post('/v1/auth/google/login', { idToken: JWT('sub9') });
    assert.equal(login.status, 200); assert.equal(login.json.created, true); assert.ok(login.json.sessionTicket);
    assert.equal((await post('/v1/auth/google/login', { idToken: 'nope' })).status, 401);
    assert.equal((await post('/v1/auth/google/link', { idToken: JWT('sub10') })).status, 401);                       // no ticket
    const ok = await post('/v1/auth/google/link', { idToken: JWT('sub10') }, 'GUEST003-ticket-0123456789');
    assert.equal(ok.status, 200); assert.equal(ok.json.ok, true);
    const clash = await post('/v1/auth/google/link', { idToken: JWT('sub9') }, 'GUEST004-ticket-0123456789');
    assert.equal(clash.status, 409); assert.equal(clash.json.error, 'account_exists'); assert.ok('name' in clash.json);
  } finally { server.close(); }
});

// ---------------------------------------------------------------- Facebook
const APP = '2057586301904403';
function fbSetup({ appId = APP, valid = true, exp = 0, status = 200 } = {}) {
  const s = setup();
  const asked = [];
  const fetchFn = async url => {
    const u = new URL(url); asked.push(u);
    const tok = u.searchParams.get('input_token');
    return { ok: status === 200, status, json: async () => ({ data: { app_id: appId, is_valid: valid, user_id: tok.split('-').pop(), expires_at: exp } }) };
  };
  s.identity = createIdentity({ accounts: s.accounts, playfab: s.playfab, chat: s.chat, providers: { facebook: createFacebook({ appId: APP, appSecret: 'secret-value', fetchFn, now: s.now }) }, log: { error() {} } });
  s.asked = asked;
  return s;
}

test('Facebook: a valid token of our app opens (or makes) the account; the app token is id|secret', async () => {
  const s = fbSetup();
  const a = await s.identity.login('facebook', 'fbtoken-padding-1001001001');
  assert.equal(a.created, true); assert.deepEqual(s.playfabLog[0], ['login', 'f-1001001001', true]);
  assert.equal(s.asked[0].hostname, 'graph.facebook.com'); assert.equal(s.asked[0].searchParams.get('access_token'), APP + '|secret-value');
  assert.equal((await s.identity.login('facebook', 'fbtoken-padding-1001001001')).playFabId, a.playFabId);
  s.accounts.touch('GUEST010');
  assert.deepEqual(await s.identity.link('GUEST010', 'facebook', 'fbtoken-padding-2002002002'), { ok: true, already: false });
  assert.deepEqual(s.accounts.status('GUEST010').methods, ['facebook']);
  await assert.rejects(s.identity.link('GUEST011', 'facebook', 'fbtoken-padding-1001001001'), e => e.code === 'account_exists');
});

test('Facebook: tokens of another app, invalid, expired, or refused are bad_token; without the secret it is off', async () => {
  for (const opts of [{ appId: '999' }, { valid: false }, { exp: (T0 - 1000) / 1000 }, { status: 400, valid: false }]) {
    const s = fbSetup(opts);
    await assert.rejects(s.identity.login('facebook', 'fbtoken-padding-3003003003'), e => e.code === 'bad_token', JSON.stringify(opts));
  }
  const s = fbSetup();
  await assert.rejects(s.identity.login('facebook', 'short'), e => e.code === 'bad_token');
  const off = createIdentity({ accounts: s.accounts, playfab: s.playfab, providers: { facebook: createFacebook({ appId: APP, appSecret: '' }) } });
  await assert.rejects(off.login('facebook', 'fbtoken-padding-3003003003'), e => e.code === 'not_configured');
});

test('Facebook routes, and the public privacy and data deletion pages', async () => {
  const s = fbSetup();
  const cfg = { currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: { publicCooldownMs: 0, cooldownMs: 0 } };
  const { server } = createApp({ cfg, playfab: s.playfab, store: createStore(null), chat: s.chat, accounts: s.accounts, identity: s.identity, now: s.now });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const base = 'http://127.0.0.1:' + server.address().port;
  const post = async (path, body, ticket) => { const h = { 'content-type': 'application/json' }; if (ticket) h['x-authorization'] = ticket; const res = await fetch(base + path, { method: 'POST', headers: h, body: JSON.stringify(body) }); return { status: res.status, json: await res.json() }; };
  try {
    const login = await post('/v1/auth/facebook/login', { accessToken: 'fbtoken-padding-4004004004' });
    assert.equal(login.status, 200); assert.equal(login.json.created, true);
    assert.equal((await post('/v1/auth/facebook/link', { accessToken: 'fbtoken-padding-5005005005' })).status, 401);                // no ticket
    assert.equal((await post('/v1/auth/facebook/link', { accessToken: 'fbtoken-padding-5005005005' }, 'GUEST012-ticket-0123456789')).status, 200);
    for (const [p, words] of [['/privacy', ['سياسة الخصوصية', 'Privacy Policy', 'supportmafia@mena-gaming.com']], ['/data-deletion', ['حذف بياناتك', 'Delete your data', 'supportmafia@mena-gaming.com']]]) {
      const res = await fetch(base + p); const html = await res.text();
      assert.equal(res.status, 200); assert.match(res.headers.get('content-type'), /text\/html/);
      for (const w of words) assert.ok(html.includes(w), p + ' has ' + w);
      assert.ok(!html.includes('__BODY__') && !html.includes('__BASE__'));
    }
  } finally { server.close(); }
});
