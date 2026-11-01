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

const PASSWORD = 'correct horse battery';
const KEY = 'bot-key-for-the-tests-0123456789';
const playfab = { authenticate: async t => String(t).split('-')[0], getDisplayName: async pf => 'اسم ' + pf, getProgress: async () => ({ rankPoints: 0, level: 1 }), addCurrency: async () => {} };
const ADM = 'ADMIN00001', P1 = 'PLAYER0001', P2 = 'PLAYER0002', P3 = 'PLAYER0003';

async function boot(opts = {}) {
  let t = 1_700_000_000_000;
  const clock = { now: () => t, tick: ms => { t += ms; } };
  const cfg = { currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: { publicCooldownMs: 0, cooldownMs: 0 }, events: [], offers: [] };
  const chat = createChat({ cfg, playfab, now: clock.now });
  const friends = createFriends({ chat, cfg });
  const rooms = createOfficialRooms({ chat, now: clock.now, isNew: pf => pf === P3 });
  chat.setRooms(rooms);
  const adminStore = createAdminStore({ file: null });
  const admin = createAdmin({ cfg, store: adminStore, chat, friends, officialRooms: rooms, playfab: null, now: clock.now, adminCfg: { passwordHash: hashPassword(PASSWORD), path: '/office' } });
  const { server } = createApp({ cfg, playfab, store: createStore(null), chat, friends, admin, officialRooms: rooms, botKey: opts.noBot ? '' : KEY, now: clock.now });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const url = 'http://127.0.0.1:' + server.address().port;
  const player = async (pf, path, body, method) => {
    const res = await fetch(url + path, { method: method || (body !== undefined ? 'POST' : 'GET'), headers: { 'x-authorization': pf + '-ticket-0123456789', 'content-type': 'application/json' }, body: body !== undefined ? JSON.stringify(body) : undefined });
    return { status: res.status, json: await res.json().catch(() => null) };
  };
  const bot = async (path, body, key = KEY) => {
    const res = await fetch(url + path, { method: 'POST', headers: { 'x-bot-key': key, 'content-type': 'application/json' }, body: JSON.stringify(body || {}) });
    return { status: res.status, json: await res.json().catch(() => null) };
  };
  let cookie = '';
  const board = async (path, body) => {
    const h = { 'x-admin': '1' }; if (cookie) h.cookie = cookie; if (body !== undefined) h['content-type'] = 'application/json';
    const res = await fetch(url + '/office' + path, { method: body !== undefined ? 'POST' : 'GET', headers: h, body: body !== undefined ? JSON.stringify(body) : undefined });
    const sc = res.headers.get('set-cookie'); if (sc) cookie = sc.split(';')[0];
    return { status: res.status, json: await res.json().catch(() => null) };
  };
  await board('/api/login', { password: PASSWORD });
  return { player, bot, board, rooms, chat, friends, clock, close: () => server.close() };
}

const boot2 = boot;

test('the board creates a room, the bot reports it, players see it pinned and open', async () => {
  const b = await boot2();
  try {
    const made = await b.board('/api/office/save', { name: 'غرفة مكتب المافيا', nameEn: 'Mafia Office Room', sub: 'للاعبين الجدد', capacity: 64, micSeats: 8, micMode: 'request' });
    assert.equal(made.status, 200);
    const id = made.json.room.id;
    assert.equal(id, '1001');
    await b.board('/api/office/save', { name: 'قاعة الدعم', capacity: 16, pinned: false, hidden: true });

    let list = await b.player(P1, '/v1/rooms/official', {});
    assert.equal(list.status, 200);
    assert.deepEqual(list.json.rooms.map(r => r.id), ['1001']);       // the hidden one is for its admins only
    assert.equal(list.json.rooms[0].lobbyId, '');                      // the bot has not reported: not open
    assert.equal(list.json.rooms[0].capacity, 64);

    const beat = await b.bot('/v1/bot/beat', { rooms: [{ id, lobbyId: 'LOBBY1', members: [{ puid: 'bot', pf: '', name: 'bot' }], stage: { head: '', seats: [], queue: [] } }] });
    assert.equal(beat.status, 200);
    list = await b.player(P1, '/v1/rooms/official', {});
    assert.equal(list.json.rooms[0].lobbyId, 'LOBBY1');
    assert.equal(list.json.rooms[0].online, 1);
  } finally { b.close(); }
});

test('only the admins of a hidden room see it', async () => {
  const b = await boot2();
  try {
    await b.board('/api/office/save', { name: 'قاعة الدعم', hidden: true });
    await b.board('/api/office/admin', { room: '1001', player: ADM });
    assert.equal((await b.player(P1, '/v1/rooms/official', {})).json.rooms.length, 0);
    const mine = await b.player(ADM, '/v1/rooms/official', {});
    assert.equal(mine.json.rooms.length, 1);
    assert.equal(mine.json.rooms[0].staff, true);
    assert.deepEqual(mine.json.rooms[0].admins, [ADM]);
    assert.equal((await b.player(P1, '/v1/rooms/official/enter', { id: '1001' })).status, 404);
  } finally { b.close(); }
});

test('the bot needs its key', async () => {
  const b = await boot2();
  try {
    assert.equal((await b.bot('/v1/bot/beat', {}, 'wrong')).status, 401);
    assert.equal((await b.bot('/v1/bot/beat', {}, '')).status, 401);
    assert.equal((await b.bot('/v1/bot/beat', {})).status, 200);
  } finally { b.close(); }
  const off = await boot2({ noBot: true });
  try { assert.equal((await off.bot('/v1/bot/beat', {})).status, 404); } finally { off.close(); }   // no key configured: the bot routes do not exist
});

test('a room is open only while the bot keeps reporting', async () => {
  const b = await boot2();
  try {
    await b.board('/api/office/save', { name: 'مكتب' });
    await b.bot('/v1/bot/beat', { rooms: [{ id: '1001', lobbyId: 'L', members: [] }] });
    assert.equal((await b.player(P1, '/v1/rooms/official', {})).json.rooms[0].lobbyId, 'L');
    b.clock.tick(31_000);
    assert.equal((await b.player(P1, '/v1/rooms/official', {})).json.rooms[0].lobbyId, '');
    assert.equal((await b.board('/api/office')).json.botAlive, false);
  } finally { b.close(); }
});

test('joins, leaves, hands and mic seats become the room log', async () => {
  const b = await boot2();
  try {
    await b.board('/api/office/save', { name: 'مكتب' });
    await b.board('/api/office/admin', { room: '1001', player: ADM });
    const m = (puid, pf, name) => ({ puid, pf, name });
    await b.bot('/v1/bot/beat', { rooms: [{ id: '1001', lobbyId: 'L', members: [m('u0', '', 'bot')], stage: { head: '', seats: [], queue: [] } }] });
    b.clock.tick(5000);
    await b.bot('/v1/bot/beat', { rooms: [{ id: '1001', lobbyId: 'L', members: [m('u0', '', 'bot'), m('u1', ADM, 'ريما'), m('u3', P3, 'نور')], stage: { head: 'u1', seats: [], queue: ['u3'] } }] });
    b.clock.tick(5000);
    await b.bot('/v1/bot/beat', { rooms: [{ id: '1001', lobbyId: 'L', members: [m('u0', '', 'bot'), m('u1', ADM, 'ريما'), m('u3', P3, 'نور')], stage: { head: 'u1', seats: ['u3'], queue: [] } }] });
    b.clock.tick(5000);
    await b.bot('/v1/bot/beat', { rooms: [{ id: '1001', lobbyId: 'L', members: [m('u0', '', 'bot'), m('u1', ADM, 'ريما')], stage: { head: 'u1', seats: [], queue: [] } }] });
    const log = (await b.board('/api/office/log?room=1001')).json;
    const kinds = log.log.map(l => l.kind + ':' + l.name);
    assert.ok(kinds.includes('join:ريما') && kinds.includes('join:نور') && kinds.includes('hand:نور') && kinds.includes('leave:نور'));
    assert.equal(log.log.filter(l => l.kind === 'mic').length, 2);                    // went on the mic, came off it
    assert.ok(log.log.find(l => l.name === 'نور' && l.kind === 'join').text.includes('لاعب جديد'));
    assert.ok(log.log.find(l => l.name === 'ريما' && l.kind === 'join').text.includes('أدمن'));
    assert.equal(log.stats.joinsToday, 2);
    const view = (await b.board('/api/office')).json.rooms[0];
    assert.equal(view.online, 2);
    assert.equal(view.adminsOnline, 1);
  } finally { b.close(); }
});

test('the admin orders reach the bot once', async () => {
  const b = await boot2();
  try {
    await b.board('/api/office/save', { name: 'مكتب' });
    assert.equal((await b.board('/api/office/command', { room: '1001', kind: 'kick', puid: 'u9', pf: P2 })).status, 200);
    assert.equal((await b.board('/api/office/command', { room: '1001', kind: 'dance', puid: 'u9' })).status, 400);
    assert.equal((await b.board('/api/office/command', { room: '999', kind: 'kick', puid: 'u9' })).status, 404);
    const first = await b.bot('/v1/bot/beat', {});
    assert.deepEqual(first.json.commands.map(c => [c.kind, c.puid, c.room]), [['kick', 'u9', '1001']]);
    assert.equal((await b.bot('/v1/bot/beat', {})).json.commands.length, 0);
    assert.ok(first.json.rooms.find(r => r.id === '1001'));                         // the bot also learns the rooms to keep
  } finally { b.close(); }
});

test('the chat of a room: only for players who are inside, admins skip the slow mode, history for latecomers', async () => {
  const b = await boot2();
  try {
    await b.board('/api/office/save', { name: 'مكتب', slowSeconds: 3, welcome: 'أهلاً' });
    await b.board('/api/office/admin', { room: '1001', player: ADM });
    // not inside yet
    assert.equal((await b.player(P1, '/v1/chat/send', { channel: 'room:1001', text: 'مرحبا' })).status, 403);
    const e1 = await b.player(P1, '/v1/rooms/official/enter', { id: '1001' });
    assert.equal(e1.status, 200); assert.equal(e1.json.welcome, 'أهلاً'); assert.deepEqual(e1.json.history, []);
    await b.player(ADM, '/v1/rooms/official/enter', { id: '1001' });
    const s1 = await b.player(P1, '/v1/chat/send', { channel: 'room:1001', text: 'كيف بلعب؟' });
    assert.equal(s1.status, 200);
    assert.equal((await b.player(P1, '/v1/chat/send', { channel: 'room:1001', text: 'ثاني' })).status, 429);       // slow mode
    assert.equal((await b.player(ADM, '/v1/chat/send', { channel: 'room:1001', text: 'أول' })).status, 200);
    assert.equal((await b.player(ADM, '/v1/chat/send', { channel: 'room:1001', text: 'ثاني' })).status, 200);      // no wait for admins
    // the admin hears the line through the ordinary poll; a player who is not inside does not
    const poll = await b.player(ADM, '/v1/chat/poll?since=0', undefined);
    assert.ok(poll.json.messages.some(m => m.ch === 'room:1001' && m.text === 'كيف بلعب؟'));
    const outsider = await b.player(P2, '/v1/chat/poll?since=0', undefined);
    assert.ok(!outsider.json.messages.some(m => m.ch === 'room:1001'));
    // a latecomer gets the last lines when he enters
    const late = await b.player(P2, '/v1/rooms/official/enter', { id: '1001' });
    assert.equal(late.json.history.length, 3);
    // leaving closes the chat for him
    await b.player(P2, '/v1/rooms/official/leave', {});
    assert.equal((await b.player(P2, '/v1/chat/send', { channel: 'room:1001', text: 'x' })).status, 403);
  } finally { b.close(); }
});

test('presence ends when a player stops pinging, and the room chat can be closed', async () => {
  const b = await boot2();
  try {
    await b.board('/api/office/save', { name: 'مكتب', slowSeconds: 0 });
    await b.player(P1, '/v1/rooms/official/enter', { id: '1001' });
    assert.equal((await b.player(P1, '/v1/chat/send', { channel: 'room:1001', text: 'a' })).status, 200);
    b.clock.tick(95_000);
    assert.equal((await b.player(P1, '/v1/chat/send', { channel: 'room:1001', text: 'b' })).status, 403);   // no ping for 95 s
    await b.player(P1, '/v1/rooms/official/enter', { id: '1001' });
    await b.board('/api/office/save', { id: '1001', name: 'مكتب', slowSeconds: 0, chatEnabled: false });
    assert.equal((await b.player(P1, '/v1/chat/send', { channel: 'room:1001', text: 'c' })).status, 403);   // the chat is closed
  } finally { b.close(); }
});

test('the admin deletes a message and speaks as the office', async () => {
  const b = await boot2();
  try {
    await b.board('/api/office/save', { name: 'مكتب', slowSeconds: 0 });
    await b.player(P1, '/v1/rooms/official/enter', { id: '1001' });
    const sent = await b.player(P1, '/v1/chat/send', { channel: 'room:1001', text: 'رسالة سيئة' });
    assert.equal((await b.board('/api/office/chat?room=1001')).json.messages.length, 1);
    await b.board('/api/office/chat/say', { room: '1001', text: 'مرحبا من الإدارة' });
    assert.equal((await b.board('/api/office/chat?room=1001')).json.messages.length, 2);
    assert.equal((await b.board('/api/office/chat/delete', { id: sent.json.id })).json.removed, true);
    const left = (await b.board('/api/office/chat?room=1001')).json.messages;
    assert.deepEqual(left.map(m => m.text), ['مرحبا من الإدارة']);
  } finally { b.close(); }
});

test('admins are added by player number or id, validated, and removed again', async () => {
  const b = await boot2();
  try {
    await b.board('/api/office/save', { name: 'مكتب' });
    assert.equal((await b.board('/api/office/admin', { room: '1001', player: '!!' })).status, 404);
    assert.equal((await b.board('/api/office/admin', { room: '999', player: ADM })).status, 404);
    assert.deepEqual((await b.board('/api/office/admin', { room: '1001', player: ADM })).json.admins, [ADM]);
    assert.deepEqual((await b.board('/api/office/admin', { room: '1001', player: ADM })).json.admins, [ADM]);      // twice: still one
    const view = (await b.board('/api/office')).json.rooms[0];
    assert.equal(view.adminList[0].pf, ADM);
    assert.deepEqual((await b.board('/api/office/admin', { room: '1001', player: ADM, on: false })).json.admins, []);
  } finally { b.close(); }
});

test('a room needs a name, sizes are kept in range, and a deleted room is gone', async () => {
  const b = await boot2();
  try {
    assert.equal((await b.board('/api/office/save', { name: '' })).status, 400);
    const r = (await b.board('/api/office/save', { name: 'x', capacity: 500, micSeats: 1 })).json.room;
    assert.equal(r.capacity, 64); assert.equal(r.micSeats, 2);
    assert.equal((await b.board('/api/office/delete', { id: r.id })).status, 200);
    assert.equal((await b.board('/api/office/delete', { id: r.id })).status, 404);
    assert.equal((await b.player(P1, '/v1/rooms/official', {})).json.rooms.length, 0);
  } finally { b.close(); }
});

test('a bot says a line in the room chat; closed chat and unknown rooms refuse it', async () => {
  const b = await boot2();
  try {
    const id = (await b.board('/api/office/save', { name: 'غرفة', capacity: 16 })).json.room.id;
    await b.player(P1, '/v1/rooms/official/enter', { id });
    const said = await b.bot('/v1/bot/say', { room: id, bot: { id: 'b3', name: 'سامر', rank: 2 }, text: 'مساء الخير' });
    assert.equal(said.json.ok, true);
    const hist = await b.player(P1, '/v1/rooms/official/enter', { id });
    const line = hist.json.history.find(l => l.text === 'مساء الخير');
    assert.ok(line);
    assert.equal(line.from, 'bot:b3');
    assert.equal(line.name, 'سامر');
    assert.equal((await b.bot('/v1/bot/say', { room: '9999', bot: { id: 'b3', name: 'x' }, text: 'hi' })).json.ok, false);
    assert.equal((await b.bot('/v1/bot/say', { room: id, bot: { id: '', name: 'x' }, text: 'hi' })).json.ok, false);
    assert.equal((await b.bot('/v1/bot/say', { room: id, bot: { id: 'b3', name: 'x' }, text: 'hi' }, 'wrong')).status, 401);
    await b.board('/api/office/save', { id, name: 'غرفة', capacity: 16, chatEnabled: false });
    assert.equal((await b.bot('/v1/bot/say', { room: id, bot: { id: 'b3', name: 'x' }, text: 'hi' })).json.ok, false);
  } finally { b.close(); }
});
