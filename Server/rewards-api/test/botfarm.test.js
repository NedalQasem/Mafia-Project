'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createApp } = require('../src/app');
const { createChat } = require('../src/chat');
const { createFriends } = require('../src/friends');
const { createStore } = require('../src/store');
const { createAdminStore } = require('../src/adminstore');
const { createAdmin, hashPassword } = require('../src/admin');
const { createOfficialRooms } = require('../src/officialrooms');
const { createBotFarm } = require('../src/botfarm');

const PASSWORD = 'correct horse battery';
const KEY = 'bot-key-for-the-tests-0123456789';
const playfab = { authenticate: async t => String(t).split('-')[0], getDisplayName: async pf => 'x' + pf, getProgress: async () => ({}), addCurrency: async () => {} };

async function boot() {
  let t = 1_700_000_000_000;
  const clock = { now: () => t, tick: ms => { t += ms; } };
  const cfg = { currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: {}, events: [], offers: [] };
  const chat = createChat({ cfg, playfab, now: clock.now });
  const friends = createFriends({ chat, cfg });
  const rooms = createOfficialRooms({ chat, now: clock.now });
  const farm = createBotFarm({ now: clock.now });
  const admin = createAdmin({ cfg, store: createAdminStore({ file: null }), chat, friends, officialRooms: rooms, botFarm: farm, now: clock.now, adminCfg: { passwordHash: hashPassword(PASSWORD), path: '/office' } });
  const { server } = createApp({ cfg, playfab, store: createStore(null), chat, friends, admin, officialRooms: rooms, botFarm: farm, botKey: KEY, now: clock.now });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const url = 'http://127.0.0.1:' + server.address().port;
  let cookie = '';
  const board = async (path, body) => {
    const h = { 'x-admin': '1' }; if (cookie) h.cookie = cookie; if (body !== undefined) h['content-type'] = 'application/json';
    const res = await fetch(url + '/office' + path, { method: body !== undefined ? 'POST' : 'GET', headers: h, body: body !== undefined ? JSON.stringify(body) : undefined });
    const sc = res.headers.get('set-cookie'); if (sc) cookie = sc.split(';')[0];
    return { status: res.status, json: await res.json().catch(() => null) };
  };
  const beat = async body => { const res = await fetch(url + '/v1/bot/beat', { method: 'POST', headers: { 'x-bot-key': KEY, 'content-type': 'application/json' }, body: JSON.stringify(body || {}) }); return { status: res.status, json: await res.json() }; };
  await board('/api/login', { password: PASSWORD });
  return { board, beat, clock, farm, close: () => server.close() };
}

test('the board makes bots with Arabic names in the level range, and never twice the same name', async () => {
  const b = await boot();
  try {
    const first = await b.board('/api/bots/generate', { count: 12, levelFrom: 3, levelTo: 9 });
    assert.equal(first.status, 200);
    assert.equal(first.json.made.length, 12);
    for (const p of first.json.made) { assert.match(p.name, /[؀-ۿ]/); assert.ok(p.level >= 3 && p.level <= 9); assert.ok(['quiet', 'normal', 'chatty'].includes(p.style)); assert.equal(p.lines.length, 8); }
    await b.board('/api/bots/generate', { count: 12 });
    const names = (await b.board('/api/bots')).json.profiles.map(p => p.name);
    assert.equal(new Set(names).size, names.length);
  } finally { b.close(); }
});

test('a profile is edited by hand and removed', async () => {
  const b = await boot();
  try {
    const p = (await b.board('/api/bots/profile', { name: 'مازن', level: 7, style: 'chatty', lines: ['أهلين', 'شو الأخبار'] })).json.profile;
    assert.equal(p.rank, 1); assert.deepEqual(p.lines, ['أهلين', 'شو الأخبار']);
    assert.equal((await b.board('/api/bots/profile', { name: 'مازن' })).status, 400);                    // the same name again
    assert.equal((await b.board('/api/bots/profile', { id: p.id, name: 'مازن', level: 30 })).json.profile.rank, 7);
    assert.equal((await b.board('/api/bots/profile', { name: '' })).status, 400);
    assert.equal((await b.board('/api/bots/profile/delete', { id: p.id })).status, 200);
    assert.equal((await b.board('/api/bots/profile/delete', { id: p.id })).status, 404);
  } finally { b.close(); }
});

test('orders put bots into an official room or into a room of the players', async () => {
  const b = await boot();
  try {
    const o = (await b.board('/api/bots/order', { room: 'official:1001', count: 3, sit: true, chat: true, play: false })).json.order;
    assert.equal(o.count, 3); assert.equal(o.play, false);
    assert.equal((await b.board('/api/bots/order', { room: 'lobby:ABCD1234' })).status, 200);
    assert.equal((await b.board('/api/bots/order', { room: 'somewhere' })).status, 400);
    assert.equal((await b.board('/api/bots/order', { room: 'lobby:ab' })).status, 400);
    assert.equal((await b.board('/api/bots/order/delete', { id: o.id })).status, 200);
    assert.equal((await b.board('/api/bots')).json.orders.length, 1);
  } finally { b.close(); }
});

test('the launch mode is saved within its limits', async () => {
  const b = await boot();
  try {
    const l = (await b.board('/api/bots/launch', { enabled: true, minPerRoom: 99, fromHour: 16, toHour: 2, maxBots: 30, leaveWhenReal: true })).json.launch;
    assert.equal(l.enabled, true); assert.equal(l.minPerRoom, 16); assert.equal(l.fromHour, 16);
    assert.equal((await b.board('/api/bots/launch', { enabled: false })).json.launch.enabled, false);
  } finally { b.close(); }
});

test('the bot program learns what is wanted and reports what it runs', async () => {
  const b = await boot();
  try {
    await b.board('/api/bots/generate', { count: 5 });
    await b.board('/api/bots/order', { room: 'official:1001', count: 2 });
    assert.equal((await b.board('/api/bots')).json.alive, false);
    const answer = await b.beat({ lobbies: [{ id: 'LOBBY0001', name: 'سهرة', count: 3, capacity: 12, real: 1 }], bots: [{ id: 'b1', room: 'official:1001', status: 'seat' }, { id: 'b2', room: 'lobby:LOBBY0001', status: 'match', role: 'doctor' }], real: 4 });
    assert.equal(answer.status, 200);
    assert.equal(answer.json.farm.profiles.length, 5);
    assert.equal(answer.json.farm.orders.length, 1);
    const v = (await b.board('/api/bots')).json;
    assert.equal(v.alive, true); assert.equal(v.real, 4); assert.equal(v.lobbies[0].name, 'سهرة');
    assert.deepEqual(v.stats, { total: 5, running: 2, rooms: 2, matches: 1 });
    b.clock.tick(31_000);
    const gone = (await b.board('/api/bots')).json;
    assert.equal(gone.alive, false); assert.equal(gone.running.length, 0);
  } finally { b.close(); }
});
