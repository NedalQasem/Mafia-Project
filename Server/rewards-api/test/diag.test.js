'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { createApp } = require('../src/app');
const { createChat } = require('../src/chat');
const { createStore } = require('../src/store');
const { createDiag } = require('../src/diag');

const playfab = { authenticate: async t => String(t).split('-')[0], getDisplayName: async pf => 'name' + pf, getProgress: async () => ({}), addCurrency: async () => {} };

async function boot() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'diag-'));
  let t = Date.UTC(2026, 10, 3, 12, 0, 0);
  const now = () => t;
  const cfg = { currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: {}, events: [], offers: [] };
  const chat = createChat({ cfg, playfab, now });
  const diag = createDiag({ dir, nameOf: async pf => (await chat.who(pf)).name, now });
  const { server } = createApp({ cfg, playfab, store: createStore(null), chat, diag, now });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const url = 'http://127.0.0.1:' + server.address().port;
  const send = (pf, body) => fetch(url + '/v1/diag/upload', { method: 'POST', headers: { 'content-type': 'application/json', 'x-authorization': pf + '-ticket-long-enough' }, body: JSON.stringify(body) }).then(async r => ({ status: r.status, body: await r.json() }));
  return { diag, send, url, dir, close: () => server.close(), tick: ms => { t += ms; } };
}

test('a batch is kept per player and per day, with the name and the phone', async () => {
  const h = await boot();
  const r = await h.send('AAA111', { sid: 'run1abcdef', build: '1.4.3', device: 'SM-S928B', lines: ['12:00:01.000 +1.0 EV Room | joined', '12:00:02.000 +2.0 AN Health | no voice'] });
  assert.equal(r.body.ok, true); assert.equal(r.body.saved, 2);
  const players = h.diag.players();
  assert.equal(players.length, 1);
  assert.equal(players[0].pf, 'AAA111'); assert.equal(players[0].name, 'nameAAA111'); assert.equal(players[0].device, 'SM-S928B'); assert.deepEqual(players[0].days, ['2026-11-03']);
  const text = h.diag.log('AAA111', '2026-11-03');
  assert.match(text, /run1 12:00:01.000 \+1.0 EV Room \| joined/);
  assert.match(text, /AN Health \| no voice/);
  h.close();
});

test('two players on one timeline, sorted by the time of each line', async () => {
  const h = await boot();
  await h.send('AAA111', { sid: 'aaaa', lines: ['12:00:05.000 +5 EV Voice | connected', '12:00:09.000 +9 EV Room | left'] });
  await h.send('BBB222', { sid: 'bbbb', lines: ['12:00:07.000 +7 EV Voice | connected'] });
  const lines = h.diag.merged(['AAA111', 'BBB222'], '2026-11-03').trim().split('\n');
  assert.equal(lines.length, 3);
  assert.match(lines[0], /nameAAA111.*connected/); assert.match(lines[1], /nameBBB222.*connected/); assert.match(lines[2], /left/);
  h.close();
});

test('bad input is refused or trimmed, and a path cannot be escaped', async () => {
  const h = await boot();
  assert.equal(h.diag.log('../etc', '2026-11-03'), null);
  assert.equal(h.diag.log('AAA111', '../../x'), null);
  const r = await h.send('CCC333', { sid: 'x', lines: ['a'.repeat(5000), 'line\r\nbreak'] });
  assert.equal(r.body.saved, 2);
  const text = h.diag.log('CCC333', '2026-11-03');
  assert.ok(text.split('\n').every(l => l.length < 760));
  assert.equal(text.trim().split('\n').length, 2);       // the line break inside a line did not make a third line
  const none = await h.send('CCC333', { sid: 'x', lines: 'not a list' });
  assert.equal(none.body.saved, 0);
  h.close();
});

test('without a ticket nothing is accepted', async () => {
  const h = await boot();
  const r = await fetch(h.url + '/v1/diag/upload', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ lines: ['x'] }) });
  assert.equal(r.status, 401);
  assert.deepEqual(h.diag.players(), []);
  h.close();
});

test('a full batch of long lines (300 lines of 450 characters) is accepted, and the next batch of the same run follows it', async () => {
  const h = await boot();
  const big = Array.from({ length: 300 }, (_, i) => '12:00:' + String(i % 60).padStart(2, '0') + '.000 +' + i + ' W Unity | ' + 'The character with Unicode value \uDE04 was not found "x" '.repeat(8));
  const a = await h.send('AAA111', { sid: 'run1', lines: big });
  assert.equal(a.status, 200); assert.equal(a.body.saved, 300);
  const b = await h.send('AAA111', { sid: 'run1', lines: ['12:01:00.000 +300 EV Room | next batch'] });
  assert.equal(b.status, 200); assert.equal(b.body.saved, 1);
  assert.match(h.diag.log('AAA111', '2026-11-03'), /next batch/);
  h.close();
});
