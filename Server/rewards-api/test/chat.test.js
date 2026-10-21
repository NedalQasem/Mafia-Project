'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const { createChat, normalize, rankIndex, dmChannel } = require('../src/chat');
const { createApp } = require('../src/app');
const { createStore } = require('../src/store');

const A = 'AAAA0001', B = 'BBBB0002', C = 'CCCC0003', D = 'DDDD0004';

function fakePlayFab(progressByPf = {}) {
  return {
    names: { [A]: 'أبو سالم', [B]: 'أم سعيد', [C]: 'لينا', [D]: 'مازن' },
    async authenticate(t) { if (!/^t-/.test(t)) { const e = new Error('bad'); e.status = 400; e.code = 'InvalidSessionTicket'; throw e; } return t.slice(2); },
    async getDisplayName(pf) { if (!this.names[pf]) throw new Error('unknown'); return this.names[pf]; },
    async getProgress(pf) { return progressByPf[pf] || null; },
  };
}

function setup(over = {}, progress = {}) {
  let t = Date.UTC(2026, 9, 20, 12, 0, 0);
  const clock = { now: () => t, advance: ms => { t += ms; } };
  const playfab = fakePlayFab(progress);
  const chat = createChat({ cfg: { chat: over }, playfab, now: clock.now, log: { error() {} } });
  return { chat, clock, playfab };
}

test('a public message reaches everybody and carries name and rank', async () => {
  const { chat } = setup({}, { [A]: { rankPoints: 2500 } });
  const r = await chat.send(A, { channel: 'public', text: '  مساء الخير  ' });
  assert.equal(r.ok, true);
  const got = await chat.poll(B, 0, 0, false);
  assert.equal(got.messages.length, 1);
  assert.deepEqual([got.messages[0].name, got.messages[0].rank, got.messages[0].text], ['أبو سالم', 2, 'مساء الخير']);
});

test('public chat has a 5 second slow mode, other chats 1 second', async () => {
  const { chat, clock } = setup();
  await chat.send(A, { channel: 'public', text: 'one' });
  await assert.rejects(() => chat.send(A, { channel: 'public', text: 'two' }), e => e.code === 'slow' && e.extra.retryMs > 4000);
  clock.advance(5001);
  await chat.send(A, { channel: 'public', text: 'two' });
  await chat.send(A, { channel: 'dm:' + B, text: 'hi' });
  await assert.rejects(() => chat.send(A, { channel: 'dm:' + B, text: 'again' }), e => e.code === 'slow');
  clock.advance(1001);
  await chat.send(A, { channel: 'dm:' + B, text: 'again' });
});

test('empty, too long and control-character messages are handled', async () => {
  const { chat } = setup({ maxLen: 10 });
  await assert.rejects(() => chat.send(A, { channel: 'public', text: '   ' }), e => e.code === 'empty');
  await assert.rejects(() => chat.send(A, { channel: 'public', text: null }), e => e.code === 'empty');
  const r = await chat.send(A, { channel: 'public', text: 'abcdefghijklmnop\u0000x' });
  assert.equal(r.text.length, 10);
});

test('a direct message is seen only by its two players, and the inbox lists it with unread counts', async () => {
  const { chat } = setup();
  await chat.send(A, { channel: 'dm:' + B, text: 'سر' });
  assert.equal((await chat.poll(C, 0, 0, false)).messages.length, 0, 'a third player sees nothing');
  const b = await chat.poll(B, 0, 0, false);
  assert.equal(b.messages.length, 1);
  assert.equal(b.messages[0].ch, dmChannel(A, B));
  const inboxB = await chat.inbox(B);
  assert.equal(inboxB.conversations.length, 1);
  assert.deepEqual([inboxB.conversations[0].kind, inboxB.conversations[0].other, inboxB.conversations[0].name, inboxB.conversations[0].unread], ['dm', A, 'أبو سالم', 1]);
  chat.markRead(B, dmChannel(A, B));
  assert.equal((await chat.inbox(B)).conversations[0].unread, 0);
  assert.equal((await chat.inbox(A)).conversations[0].unread, 0, 'his own message is never unread for the sender');
});

test('you cannot message yourself or a bad id, and cannot post into a channel you are not in', async () => {
  const { chat } = setup();
  await assert.rejects(() => chat.send(A, { channel: 'dm:' + A, text: 'x' }), e => e.code === 'bad_channel');
  await assert.rejects(() => chat.send(A, { channel: 'dm:../..', text: 'x' }), e => e.code === 'bad_channel');
  await assert.rejects(() => chat.send(A, { channel: 'grp:nope', text: 'x' }), e => e.code === 'not_member');
  await assert.rejects(() => chat.send(A, { channel: 'weird', text: 'x' }), e => e.code === 'bad_channel');
  await assert.rejects(() => chat.send(A, {}), e => e.code === 'bad_channel');
});

test('groups: created with members, visible to members only, left and removed when empty', async () => {
  const { chat } = setup();
  const g = await chat.createGroup(A, { name: 'شلّة الحارة', members: [B, C] });
  assert.equal(g.ok, true);
  await chat.send(B, { channel: g.ch, text: 'أهلين' });
  for (const pf of [A, B, C]) assert.ok((await chat.poll(pf, 0, 0, false)).messages.some(m => m.text === 'أهلين'), pf);
  assert.equal((await chat.poll(D, 0, 0, false)).messages.length, 0);
  await assert.rejects(() => chat.send(D, { channel: g.ch, text: 'x' }), e => e.code === 'not_member');
  const inbox = await chat.inbox(C);
  assert.deepEqual([inbox.conversations[0].kind, inbox.conversations[0].name, inbox.conversations[0].members.length], ['group', 'شلّة الحارة', 3]);
  await chat.leaveGroup(C, g.id);
  await assert.rejects(() => chat.send(C, { channel: g.ch, text: 'x' }), e => e.code === 'not_member');
  await chat.leaveGroup(B, g.id); await chat.leaveGroup(A, g.id);
  assert.equal(chat._state().groups[g.id], undefined);
});

test('group limits: name, members, unknown players, how many groups one player may have', async () => {
  const { chat } = setup({ maxGroups: 2, maxGroupMembers: 3 });
  await assert.rejects(() => chat.createGroup(A, { name: 'x', members: [B] }), e => e.code === 'bad_name');
  await assert.rejects(() => chat.createGroup(A, { name: 'اسم', members: [] }), e => e.code === 'bad_members');
  await assert.rejects(() => chat.createGroup(A, { name: 'اسم', members: [B, C, D] }), e => e.code === 'bad_members');
  await assert.rejects(() => chat.createGroup(A, { name: 'اسم', members: ['NOBODY999'] }), e => e.code === 'unknown_player');
  await chat.createGroup(A, { name: 'واحد', members: [B] });
  await chat.createGroup(A, { name: 'اثنين', members: [C] });
  await assert.rejects(() => chat.createGroup(A, { name: 'ثلاثة', members: [D] }), e => e.code === 'too_many_groups');
});

test('blocking: a blocked player is hidden from the public chat and his direct messages never arrive', async () => {
  const { chat, clock } = setup();
  chat.block(B, A, true);
  await chat.send(A, { channel: 'public', text: 'for everybody' });
  assert.equal((await chat.poll(B, 0, 0, false)).messages.length, 0, 'B does not see A in public');
  assert.equal((await chat.poll(C, 0, 0, false)).messages.length, 1);
  clock.advance(6000);
  const r = await chat.send(A, { channel: 'dm:' + B, text: 'hello?' });
  assert.equal(r.ok, true, 'A is not told he is blocked');
  assert.equal((await chat.poll(B, 0, 0, false)).messages.length, 0);
  await assert.rejects(() => chat.send(B, { channel: 'dm:' + A, text: 'x' }), e => e.code === 'you_blocked');
  chat.block(B, A, false);
  clock.advance(6000);
  await chat.send(A, { channel: 'public', text: 'again' });
  assert.ok((await chat.poll(B, 0, 0, false)).messages.some(m => m.text === 'again'), 'unblocked: new messages show again');
  assert.deepEqual(chat.blockedList(B).blocked, []);
});

test('the word filter hides banned words whatever the spelling tricks', async () => {
  const { chat, clock } = setup({ bannedWords: ['كلمةسيئة', 'badword'] });
  const r1 = await chat.send(A, { channel: 'public', text: 'هذه كلمةسيئة هنا' });
  assert.equal(r1.text, 'هذه ******** هنا');
  clock.advance(6000);
  const r2 = await chat.send(A, { channel: 'public', text: 'a BadWord b' });
  assert.equal(r2.text, 'a ******* b');
  assert.equal(normalize('إِسْلَام'), normalize('اسلام'));
  assert.equal(normalize('مدرسة'), normalize('مدرسه'));
});

test('reports are stored, duplicates ignored, and three different reporters mute the player for a while', async () => {
  const { chat, clock } = setup({ autoMuteReports: 3, autoMuteMinutes: 60 });
  const m1 = await chat.send(D, { channel: 'public', text: 'rude one' });
  clock.advance(6000);
  const m2 = await chat.send(D, { channel: 'public', text: 'rude two' });
  assert.deepEqual(chat.report(A, { channel: 'public', messageId: m1.id, reason: 'spam' }), { ok: true, muted: false });
  assert.equal(chat.report(A, { channel: 'public', messageId: m1.id, reason: 'spam' }).duplicate, true);
  assert.equal(chat.report(B, { channel: 'public', messageId: m1.id }).muted, false);
  assert.equal(chat.report(C, { channel: 'public', messageId: m2.id }).muted, true, 'the third reporter of the same person triggers the mute');
  await assert.rejects(() => chat.send(D, { channel: 'public', text: 'again' }), e => e.code === 'muted' && e.extra.retryMs > 3000000);
  clock.advance(61 * 60 * 1000);
  await chat.send(D, { channel: 'public', text: 'back' });
});

test('you cannot report your own message, a system line, or a chat you are not in', async () => {
  const { chat } = setup();
  const g = await chat.createGroup(A, { name: 'سرّي', members: [B] });
  const m = await chat.send(A, { channel: 'public', text: 'mine' });
  assert.throws(() => chat.report(A, { channel: 'public', messageId: m.id }), e => e.code === 'own_message');
  assert.throws(() => chat.report(C, { channel: g.ch, messageId: 1 }), e => e.code === 'not_member');
  const sys = chat._state().messages[g.ch][0];
  assert.throws(() => chat.report(B, { channel: g.ch, messageId: sys.id }), e => e.code === 'no_message');
});

test('long poll: an empty poll waits and wakes up when a message arrives, and times out otherwise', async () => {
  const { chat } = setup({ pollMaxMs: 80 });
  const waiting = chat.poll(B, 0, 10000, false);
  await new Promise(r => setTimeout(r, 10));
  await chat.send(A, { channel: 'public', text: 'wake up' });
  const got = await waiting;
  assert.equal(got.messages.length, 1);
  assert.equal(got.messages[0].text, 'wake up');
  const t0 = Date.now();
  const empty = await chat.poll(B, got.cursor, 10000, false);
  assert.equal(empty.messages.length, 0);
  assert.ok(Date.now() - t0 < 1000, 'the wait is capped');
  assert.equal(empty.cursor, got.cursor);
});

test('a direct message wakes only the receiver; a second poll from the same player replaces the first', async () => {
  const { chat } = setup({ pollMaxMs: 300 });
  let cWoke = false;
  const forC = chat.poll(C, 0, 5000, false).then(r => { cWoke = r.messages.length > 0; return r; });
  const forB1 = chat.poll(B, 0, 5000, false);
  const forB2 = chat.poll(B, 0, 5000, false);
  assert.deepEqual((await forB1).messages, []);
  await chat.send(A, { channel: 'dm:' + B, text: 'for B only' });
  assert.equal((await forB2).messages.length, 1);
  await forC;
  assert.equal(cWoke, false);
});

test('history on the first poll is capped per channel and the cursor moves past what was delivered', async () => {
  const { chat, clock } = setup({ historyPerChannel: 3 });
  for (let i = 0; i < 6; i++) { await chat.send(A, { channel: 'public', text: 'm' + i }); clock.advance(6000); }
  const first = await chat.poll(B, 0, 0, false);
  assert.deepEqual(first.messages.map(m => m.text), ['m3', 'm4', 'm5']);
  const next = await chat.poll(B, first.cursor, 0, false);
  assert.equal(next.messages.length, 0);
});

test('online status comes from recent polling', async () => {
  const { chat, clock } = setup();
  await chat.send(B, { channel: 'dm:' + A, text: 'hi' });
  await chat.poll(B, 0, 0, false);
  assert.equal((await chat.inbox(A)).conversations[0].online, true);
  clock.advance(60000);
  assert.equal((await chat.inbox(A)).conversations[0].online, false);
});

test('rank ladder matches the game', () => {
  assert.deepEqual([0, 999, 1000, 2999, 3000, 6000, 99999].map(rankIndex), [0, 0, 1, 2, 3, 6, 6]);
});

// ------------------------------------------------------------------ the http side

test('http: chat routes need a ticket, work with json and query strings, and long polling returns', async () => {
  const playfab = fakePlayFab();
  const chat = createChat({ cfg: { chat: { pollMaxMs: 300 } }, playfab, log: { error() {} } });
  const store = createStore(null);
  const app = createApp({ cfg: { titleId: 'T', currencyCode: 'CO', requestsPerMinutePerIp: 1000, minPlayers: 4, maxPlayers: 16, minRounds: 1, minDurationSec: 60, quorumRatio: 0.5, reportWindowSeconds: 900, dailyMatchCap: 3, trustProxy: false, rewards: { baseXp: 1, winXp: 1, survivedXp: 1, baseCoins: 1, winCoins: 1, rankWin: 1, rankLose: -1 } }, playfab, store, chat, log: { error() {} } });
  await new Promise(r => app.server.listen(0, '127.0.0.1', r));
  const port = app.server.address().port;
  const call = (method, p, headers, body) => new Promise((resolve, reject) => {
    const req = http.request({ host: '127.0.0.1', port, method, path: p, headers }, res => { let d = ''; res.on('data', x => d += x); res.on('end', () => resolve({ status: res.statusCode, json: d ? JSON.parse(d) : null })); });
    req.on('error', reject); if (body) req.write(body); req.end();
  });
  const auth = pf => ({ 'X-Authorization': 't-' + pf, 'Content-Type': 'application/json' });
  try {
    assert.equal((await call('GET', '/v1/chat/inbox', {})).status, 401);
    const sent = await call('POST', '/v1/chat/send', auth(A), JSON.stringify({ channel: 'public', text: 'عبر http' }));
    assert.equal(sent.status, 200); assert.equal(sent.json.ok, true);
    const slow = await call('POST', '/v1/chat/send', auth(A), JSON.stringify({ channel: 'public', text: 'again' }));
    assert.equal(slow.status, 429); assert.equal(slow.json.error, 'slow'); assert.ok(slow.json.retryMs > 0);
    const poll = await call('GET', '/v1/chat/poll?since=0', auth(B));
    assert.equal(poll.json.messages.length, 1); assert.equal(poll.json.messages[0].text, 'عبر http');
    const t0 = Date.now();
    const quiet = await call('GET', '/v1/chat/poll?since=' + poll.json.cursor + '&wait=20', auth(B));
    assert.equal(quiet.json.messages.length, 0); assert.ok(Date.now() - t0 < 2000);
    const bad = await call('POST', '/v1/chat/send', auth(A), '{oops');
    assert.equal(bad.status, 400);
    const inbox = await call('GET', '/v1/chat/inbox', auth(B));
    assert.equal(inbox.status, 200); assert.deepEqual(inbox.json.conversations, []);
    const blk = await call('POST', '/v1/chat/block', auth(B), JSON.stringify({ player: A, on: true }));
    assert.equal(blk.json.ok, true);
    assert.deepEqual((await call('GET', '/v1/chat/blocked', auth(B))).json.blocked, [A]);
  } finally { await new Promise(r => app.server.close(r)); }
});
