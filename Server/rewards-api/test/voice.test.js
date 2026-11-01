'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { createApp } = require('../src/app');
const { createChat } = require('../src/chat');
const { createStore } = require('../src/store');
const { createOfficialRooms } = require('../src/officialrooms');
const { createVoice } = require('../src/voice');

const SECRET = 'a-livekit-secret-that-is-long-enough-0123456789';
const playfab = { authenticate: async t => String(t).split('-')[0], getDisplayName: async pf => 'x' + pf, getProgress: async () => ({}), addCurrency: async () => {} };

function decode(jwt, secret = SECRET) {
  const [h, b, s] = jwt.split('.');
  const want = crypto.createHmac('sha256', secret).update(h + '.' + b).digest('base64url');
  return { valid: want === s, head: JSON.parse(Buffer.from(h, 'base64url')), body: JSON.parse(Buffer.from(b, 'base64url')) };
}

async function boot(opts = {}) {
  let t = 1_700_000_000_000;
  const clock = { now: () => t, tick: ms => { t += ms; } };
  const cfg = { currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: {}, events: [], offers: [] };
  const chat = createChat({ cfg, playfab, now: clock.now });
  const rooms = createOfficialRooms({ chat, now: clock.now });
  const voice = createVoice({ apiKey: opts.off ? '' : 'APIkey123', apiSecret: SECRET, url: 'wss://api.example.com/lk', officialRooms: rooms, now: clock.now });
  const { server } = createApp({ cfg, playfab, store: createStore(null), chat, officialRooms: rooms, voice, now: clock.now });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const url = 'http://127.0.0.1:' + server.address().port;
  const ask = async (pf, room) => {
    const res = await fetch(url + '/v1/voice/token', { method: 'POST', headers: { 'x-authorization': pf + '-ticket-0123456789', 'content-type': 'application/json' }, body: JSON.stringify({ room }) });
    return { status: res.status, json: await res.json().catch(() => null) };
  };
  return { ask, rooms, clock, voice, close: () => server.close() };
}

const beat = (b, members, stage) => b.rooms.beat({ rooms: [{ id: '1001', lobbyId: 'L', members, stage }] });

test('a token is a signed LiveKit JWT for the room, for the player', async () => {
  const b = await boot();
  try {
    b.rooms.create({ name: 'مكتب' });
    const r = await b.ask('PLAYER0001', 'official:1001');
    assert.equal(r.status, 200);
    assert.equal(r.json.url, 'wss://api.example.com/lk');
    const d = decode(r.json.token);
    assert.ok(d.valid); assert.equal(d.head.alg, 'HS256');
    assert.equal(d.body.iss, 'APIkey123'); assert.equal(d.body.sub, 'PLAYER0001'); assert.equal(d.body.video.room, 'official:1001');
    assert.equal(d.body.video.roomJoin, true); assert.equal(d.body.video.canSubscribe, true);
    assert.ok(d.body.exp - d.body.nbf > 3600);
    assert.ok(!decode(r.json.token, 'another secret').valid);
  } finally { b.close(); }
});

test('in an official room only the admins and the people on a mic seat may speak', async () => {
  const b = await boot();
  try {
    b.rooms.create({ name: 'مكتب' });
    b.rooms.setAdmin('1001', 'ADMIN00001', true);
    const members = [{ puid: 'u1', pf: 'ADMIN00001', name: 'ريما' }, { puid: 'u2', pf: 'PLAYER0001', name: 'نور' }, { puid: 'u3', pf: 'PLAYER0002', name: 'سامر' }];
    beat(b, members, { head: 'u1', seats: [], queue: ['u2'] });
    const can = async pf => decode((await b.ask(pf, 'official:1001')).json.token).body.video.canPublish;
    assert.equal(await can('ADMIN00001'), true);
    assert.equal(await can('PLAYER0001'), false);                 // hand up, not on a seat yet
    beat(b, members, { head: 'u1', seats: ['u2'], queue: [] });
    assert.equal(await can('PLAYER0001'), true);                  // brought onto a seat: his next token lets him speak
    assert.equal(await can('PLAYER0002'), false);
    b.clock.tick(31_000);                                         // the bot went silent: nobody but the admin speaks
    assert.equal(await can('PLAYER0001'), false);
    assert.equal(await can('ADMIN00001'), true);
  } finally { b.close(); }
});

test('rooms the players opened are open to speak, a hidden official room is not offered', async () => {
  const b = await boot();
  try {
    const r = await b.ask('PLAYER0001', 'lobby:ABCDEF123456');
    assert.equal(r.status, 200); assert.equal(r.json.canPublish, true);
    b.rooms.create({ name: 'قاعة', hidden: true });
    assert.equal((await b.ask('PLAYER0001', 'official:1001')).status, 404);
    assert.equal((await b.ask('PLAYER0001', 'official:999')).status, 404);
  } finally { b.close(); }
});

test('bad room names and a missing key are refused', async () => {
  const b = await boot();
  try {
    assert.equal((await b.ask('PLAYER0001', 'nonsense')).status, 400);
    assert.equal((await b.ask('PLAYER0001', 'lobby:../x')).status, 400);
    assert.equal((await b.ask('PLAYER0001', '')).status, 400);
  } finally { b.close(); }
  const off = await boot({ off: true });
  try { assert.equal((await off.ask('PLAYER0001', 'lobby:ABCDEF123456')).status, 404); } finally { off.close(); }          // no LiveKit configured: the route does not exist
});
