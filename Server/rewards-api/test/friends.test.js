'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const { createChat } = require('../src/chat');
const { createFriends } = require('../src/friends');
const { createApp } = require('../src/app');
const { createStore } = require('../src/store');

const A = 'AAAA0001', B = 'BBBB0002', C = 'CCCC0003', D = 'DDDD0004';

function setup(over = {}) {
  let t = Date.UTC(2026, 9, 20, 12, 0, 0);
  const clock = { now: () => t, advance: ms => { t += ms; } };
  const names = { [A]: 'أبو سالم', [B]: 'أم سعيد', [C]: 'لينا', [D]: 'مازن' };
  const playfab = {
    async authenticate(tk) { return tk.slice(2); },
    async getDisplayName(pf) { if (!names[pf]) throw new Error('unknown'); return names[pf]; },
    async getProgress(pf) { return { rankPoints: 2100, level: pf === A ? 26 : 7 }; },
  };
  const chat = createChat({ cfg: {}, playfab, now: clock.now, log: { error() {} } });
  const friends = createFriends({ chat, cfg: { friends: over }, now: clock.now, log: { error() {} } });
  return { chat, friends, clock, playfab };
}

async function becomeFriends(friends, a, b) {
  await friends.me(a); const mb = await friends.me(b);
  await friends.request(a, { num: mb.num });
  await friends.respond(b, { player: a, accept: true });
}

test('everybody gets a stable six digit number and nobody shares one', async () => {
  const { friends } = setup();
  const a1 = (await friends.me(A)).num, a2 = (await friends.me(A)).num;
  assert.match(a1, /^[0-9]{6}$/); assert.equal(a1, a2);
  const nums = new Set([a1, (await friends.me(B)).num, (await friends.me(C)).num, (await friends.me(D)).num]);
  assert.equal(nums.size, 4);
});

test('lookup finds a player by his number, and tells you who is who', async () => {
  const { friends } = setup();
  const mb = await friends.me(B);
  const r = await friends.lookup(A, { num: mb.num });
  assert.deepEqual([r.found, r.pf, r.name, r.level, r.self, r.friend], [true, B, 'أم سعيد', 7, false, false]);
  await assert.rejects(() => friends.lookup(A, { num: '123456' }), e => e.code === 'unknown_number');
  await assert.rejects(() => friends.lookup(A, { num: 'abc' }), e => e.code === 'bad_number');
  assert.equal((await friends.lookup(B, { num: mb.num })).self, true);
});

test('a request waits for an answer, the other player is told, and accepting makes both friends', async () => {
  const { friends, chat } = setup();
  await friends.me(A); const mb = await friends.me(B);
  const r = await friends.request(A, { num: mb.num });
  assert.equal(r.pending, true);
  const news = await chat.poll(B, 0, 0, false);
  assert.deepEqual(news.events.map(e => [e.type, e.pf, e.name]), [['friend_request', A, 'أبو سالم']]);
  const lb = await friends.list(B);
  assert.deepEqual(lb.incoming.map(x => x.pf), [A]);
  assert.deepEqual((await friends.list(A)).outgoing.map(x => x.pf), [B]);
  await friends.respond(B, { player: A, accept: true });
  assert.deepEqual((await friends.list(A)).friends.map(x => x.pf), [B]);
  assert.deepEqual((await friends.list(B)).friends.map(x => x.pf), [A]);
  assert.equal((await friends.list(B)).incoming.length, 0);
  const back = await chat.poll(A, 0, 0, false);
  assert.deepEqual(back.events.map(e => e.type), ['friend_accepted']);
});

test('declining or cancelling removes the request without making friends', async () => {
  const { friends } = setup();
  await friends.me(A); const mb = await friends.me(B); const mc = await friends.me(C);
  await friends.request(A, { num: mb.num });
  await friends.respond(B, { player: A, accept: false });
  assert.equal((await friends.list(A)).outgoing.length, 0);
  await friends.request(A, { num: mc.num });
  friends.cancel(A, { player: C });
  assert.equal((await friends.list(C)).incoming.length, 0);
  assert.equal((await friends.list(A)).friends.length, 0);
  await assert.rejects(() => friends.respond(B, { player: A, accept: true }), e => e.code === 'no_request');
});

test('when two players asked each other it is a yes', async () => {
  const { friends } = setup();
  const ma = await friends.me(A); const mb = await friends.me(B);
  await friends.request(A, { num: mb.num });
  const r = await friends.request(B, { num: ma.num });
  assert.equal(r.accepted, true);
  assert.deepEqual((await friends.list(A)).friends.map(x => x.pf), [B]);
});

test('requests are refused for yourself, a friend, a bad number, and when the family is full', async () => {
  const { friends } = setup({ maxFriends: 1 });
  const ma = await friends.me(A); const mb = await friends.me(B); const mc = await friends.me(C);
  await assert.rejects(() => friends.request(A, { num: ma.num }), e => e.code === 'self');
  await assert.rejects(() => friends.request(A, { num: '000000' }), e => e.code === 'unknown_number');
  await becomeFriends(friends, A, B);
  await assert.rejects(() => friends.request(A, { num: mb.num }), e => e.code === 'already_friends');
  await assert.rejects(() => friends.request(A, { num: mc.num }), e => e.code === 'you_full');
});

test('a full family on the other side stops the acceptance and clears the request', async () => {
  const { friends } = setup({ maxFriends: 1 });
  await friends.me(A); const mc = await friends.me(C); const md = await friends.me(D);
  await friends.request(A, { num: mc.num });
  await becomeFriends(friends, C, D);          // C fills up with D while A waits
  await assert.rejects(() => friends.respond(C, { player: A, accept: true }), e => e.code === 'you_full');
  assert.deepEqual((await friends.list(C)).friends.map(x => x.pf), [D]);
  assert.ok(md);
});

test('only so many requests a day', async () => {
  const { friends, clock } = setup({ requestsPerDay: 2 });
  await friends.me(A);
  const nums = [(await friends.me(B)).num, (await friends.me(C)).num, (await friends.me(D)).num];
  await friends.request(A, { num: nums[0] }); await friends.request(A, { num: nums[1] });
  await assert.rejects(() => friends.request(A, { num: nums[2] }), e => e.code === 'too_many_requests');
  clock.advance(25 * 3600 * 1000);
  await friends.request(A, { num: nums[2] });
});

test('a blocked requester is not told: the request just sits there unseen', async () => {
  const { friends, chat } = setup();
  await friends.me(A); const mb = await friends.me(B);
  chat.block(B, A, true);
  const r = await friends.request(A, { num: mb.num });
  assert.equal(r.ok, true);
  assert.equal((await chat.poll(B, 0, 0, false)).events.length, 0, 'no news for the blocker');
});

test('the list puts friends in a room first, then online ones, then the rest by last seen', async () => {
  const { friends, chat, clock } = setup();
  await becomeFriends(friends, A, B); await becomeFriends(friends, A, C); await becomeFriends(friends, A, D);
  // D was seen a while ago, C is online, B is online in a room
  chat.touch(D); clock.advance(3 * 3600 * 1000);
  chat.touch(C); chat.touch(B);
  friends.setPresence(B, { room: { id: 'LOBBY1', name: 'سهرة الشباب', count: 9, cap: 16, kind: 'public' } });
  const l = await friends.list(A);
  assert.deepEqual(l.friends.map(x => x.pf), [B, C, D]);
  assert.deepEqual([l.friends[0].online, l.friends[0].room.name, l.friends[0].room.count], [true, 'سهرة الشباب', 9]);
  assert.deepEqual([l.friends[1].online, l.friends[1].room], [true, null]);
  assert.equal(l.friends[2].online, false);
  assert.ok(l.friends[2].seen > 0 && l.friends[2].seen < l.friends[1].seen);
});

test('presence goes stale: a friend who stopped reporting is no longer in his room', async () => {
  const { friends, chat, clock } = setup();
  await becomeFriends(friends, A, B);
  friends.setPresence(B, { room: { id: 'L', name: 'x', count: 1, cap: 4, kind: 'private', code: 'ABCD' } });
  assert.equal((await friends.list(A)).friends[0].room.id, 'L');
  clock.advance(100 * 1000);
  chat.touch(B);                                 // still online (polling), but no recent room report
  assert.equal((await friends.list(A)).friends[0].room, null);
  friends.setPresence(B, { room: null });
  assert.equal((await friends.list(A)).friends[0].room, null);
});

test('invitations only go to friends, carry the room, and cannot be spammed', async () => {
  const { friends, chat, clock } = setup();
  await becomeFriends(friends, A, B); await friends.me(C);
  const room = { id: 'LOBBY9', name: 'سهرة', count: 5, cap: 12, kind: 'private', code: 'K7QP' };
  await assert.rejects(() => friends.invite(A, { friend: C, room }), e => e.code === 'not_friends');
  await friends.invite(A, { friend: B, room });
  const news = await chat.poll(B, 0, 0, false);
  assert.deepEqual(news.events.filter(e => e.type === 'invite').map(e => [e.type, e.pf, e.room.id, e.room.code]), [['invite', A, 'LOBBY9', 'K7QP']]);
  await assert.rejects(() => friends.invite(A, { friend: B, room }), e => e.code === 'slow');
  clock.advance(21000);
  await friends.invite(A, { friend: B, room });
  await assert.rejects(() => friends.invite(A, { friend: B, room: { name: 'no id' } }), e => e.code === 'slow' || e.code === 'bad_room');
});

test('removing a friend works for both sides', async () => {
  const { friends } = setup();
  await becomeFriends(friends, A, B);
  friends.remove(A, { player: B });
  assert.equal((await friends.list(A)).friends.length, 0);
  assert.equal((await friends.list(B)).friends.length, 0);
});

test('a waiting poll wakes up when news arrives', async () => {
  const { friends, chat } = setup();
  await friends.me(A); const mb = await friends.me(B);
  const waiting = chat.poll(B, 0, 5000, false);
  await new Promise(r => setTimeout(r, 10));
  await friends.request(A, { num: mb.num });
  const got = await waiting;
  assert.deepEqual(got.events.map(e => e.type), ['friend_request']);
  assert.equal(got.messages.length, 0);
});

test('reports about a player need a real player and a known reason', async () => {
  const { friends } = setup();
  assert.deepEqual(friends.reportPlayer(A, { player: B, reason: 'abuse' }), { ok: true });
  assert.throws(() => friends.reportPlayer(A, { player: A, reason: 'abuse' }), e => e.code === 'bad_player');
  assert.throws(() => friends.reportPlayer(A, { player: B, reason: 'because' }), e => e.code === 'bad_reason');
  assert.throws(() => friends.reportPlayer(A, { player: '??', reason: 'spam' }), e => e.code === 'bad_player');
});

test('http: the family routes need a ticket and answer json', async () => {
  const { chat, friends, playfab } = setup();
  const app = createApp({ cfg: { titleId: 'T', currencyCode: 'CO', requestsPerMinutePerIp: 1000, minPlayers: 4, maxPlayers: 16, minRounds: 1, minDurationSec: 60, quorumRatio: 0.5, reportWindowSeconds: 900, dailyMatchCap: 3, trustProxy: false, rewards: { baseXp: 1, winXp: 1, survivedXp: 1, baseCoins: 1, winCoins: 1, rankWin: 1, rankLose: -1 } }, playfab, store: createStore(null), chat, friends, log: { error() {} } });
  await new Promise(r => app.server.listen(0, '127.0.0.1', r));
  const port = app.server.address().port;
  const call = (method, p, headers, body) => new Promise((resolve, reject) => {
    const req = http.request({ host: '127.0.0.1', port, method, path: p, headers }, res => { let d = ''; res.on('data', x => d += x); res.on('end', () => resolve({ status: res.statusCode, json: d ? JSON.parse(d) : null })); });
    req.on('error', reject); if (body) req.write(body); req.end();
  });
  const auth = pf => ({ 'X-Authorization': 't-' + pf, 'Content-Type': 'application/json' });
  try {
    assert.equal((await call('GET', '/v1/friends', {})).status, 401);
    const me = await call('GET', '/v1/me', auth(A));
    assert.equal(me.status, 200); assert.match(me.json.num, /^[0-9]{6}$/);
    const mb = (await call('GET', '/v1/me', auth(B))).json;
    const req = await call('POST', '/v1/friends/request', auth(A), JSON.stringify({ num: mb.num }));
    assert.equal(req.status, 200); assert.equal(req.json.pending, true);
    const bad = await call('POST', '/v1/friends/request', auth(A), JSON.stringify({ num: '999999' }));
    assert.equal(bad.status, 404); assert.equal(bad.json.error, 'unknown_number');
    const list = await call('GET', '/v1/friends', auth(B));
    assert.equal(list.json.incoming.length, 1);
    const ok = await call('POST', '/v1/friends/respond', auth(B), JSON.stringify({ player: A, accept: true }));
    assert.equal(ok.json.ok, true);
    const pres = await call('POST', '/v1/presence', auth(B), JSON.stringify({ room: { id: 'R1', name: 'غرفة', count: 2, cap: 8, kind: 'public' } }));
    assert.equal(pres.json.ok, true);
    const la = (await call('GET', '/v1/friends', auth(A))).json;
    assert.equal(la.friends[0].room.name, 'غرفة');
  } finally { await new Promise(r => app.server.close(r)); }
});

test('presence answers with how many players are online right now', async () => {
  const { friends, clock } = setup();
  assert.equal((await friends.setPresence(A, { room: null })).online, 1);
  assert.equal((await friends.setPresence(B, { room: null })).online, 2);
  clock.advance(5 * 60 * 1000);                                      // nobody was heard for a while
  assert.equal((await friends.setPresence(C, { room: null })).online, 1);
});
