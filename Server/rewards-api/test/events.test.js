'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createEvents } = require('../src/events');

const T0 = Date.UTC(2026, 10, 2, 12, 0, 0);   // 2 Nov 2026, noon
let clock = T0;
const now = () => clock;
const bank = [];
const playfab = { addCurrency: async (pf, code, n) => { bank.push([pf, code, n]); } };
const cfg = { currencyCode: 'CO', events: [
  { id: 'gold', kind: 'bonus', title: 'x2', coinsMultiplier: 2, from: '2026-11-01T00:00:00Z', until: '2026-11-04T00:00:00Z' },
  { id: 'gold5', kind: 'bonus', coinsMultiplier: 99, from: '2026-11-03T00:00:00Z' },
  { id: 'wins', kind: 'challenge', title: 'ten', goal: 3, metric: 'wins', rewardCoins: 500, from: '2026-11-01T00:00:00Z', until: '2026-11-08T00:00:00Z' },
  { id: 'night', kind: 'info', title: 'secret night', secret: true, from: '2026-11-10T18:00:00Z', until: '2026-11-10T23:00:00Z' },
  { id: 'old', kind: 'info', title: 'old', from: '2026-10-20T00:00:00Z', until: '2026-10-25T00:00:00Z' },
  { id: 'ancient', kind: 'info', title: 'ancient', from: '2026-08-01T00:00:00Z', until: '2026-08-05T00:00:00Z' },
  { id: 'off', kind: 'info', enabled: false, title: 'disabled' },
  { id: 'bad', kind: 'nonsense' }, { title: 'no id', kind: 'info' },
] };
function make() { clock = T0; bank.length = 0; return createEvents({ cfg, playfab, now }); }

test('the board sorts events into live, soon and done, and hides what is disabled or invalid', () => {
  const b = make().board('p1');
  assert.deepEqual(b.live.map(e => e.id), ['gold', 'wins']);
  assert.deepEqual(b.soon.map(e => e.id), ['gold5', 'night']);
  assert.deepEqual(b.done.map(e => e.id), ['old']);
  assert.equal(b.live[0].endsInSec, 36 * 3600);
  assert.equal(b.soon[0].startsInSec, 12 * 3600);
  assert.equal(b.soon[1].startsInSec, 8 * 24 * 3600 + 6 * 3600);
});

test('a secret event hides its reward until it starts', () => {
  const e = make().board('p1').soon.find(x => x.id === 'night');
  assert.equal(e.secret, true); assert.equal(e.rewardCoins, 0);
});

test('the coins multiplier is the best running bonus, capped', () => {
  const ev = make();
  assert.equal(ev.coinsMultiplier(), 2);
  clock = Date.UTC(2026, 10, 3, 12);            // both bonuses run: 99 is cut to 5
  assert.equal(ev.coinsMultiplier(), 5);
  clock = Date.UTC(2026, 10, 20);               // none runs
  assert.equal(createEvents({ cfg: { events: [cfg.events[0]] }, now }).coinsMultiplier(), 1);
});

test('a challenge counts only wins inside its window, per player, and is claimed once', async () => {
  const ev = make();
  ev.record('p1', { won: false });
  ev.record('p1', { won: true }); ev.record('p1', { won: true });
  assert.equal(ev.board('p1').live[1].progress, 2);
  assert.equal(ev.board('p2').live[1].progress, 0);
  await assert.rejects(ev.claim('p1', { id: 'wins' }), { code: 'goal not reached' });
  ev.record('p1', { won: true }); ev.record('p1', { won: true });     // the fourth win is not counted over the goal
  const mine = ev.board('p1').live[1];
  assert.equal(mine.progress, 3); assert.equal(mine.claimable, true);
  assert.deepEqual(await ev.claim('p1', { id: 'wins' }), { ok: true, coins: 500 });
  assert.deepEqual(bank, [['p1', 'CO', 500]]);
  await assert.rejects(ev.claim('p1', { id: 'wins' }), { code: 'already claimed' });
  assert.equal(ev.board('p1').live[1].claimed, true);
});

test('a finished challenge can still be claimed during the grace days, then it is gone', async () => {
  const ev = make();
  for (let i = 0; i < 3; i++) ev.record('p1', { won: true });
  clock = Date.UTC(2026, 10, 9);                // a day after it ended
  const b = ev.board('p1');
  assert.equal(b.live.find(e => e.id === 'wins').claimable, true);
  clock = Date.UTC(2026, 10, 12);               // past the grace
  assert.equal(ev.board('p1').live.some(e => e.id === 'wins'), false);
  await assert.rejects(ev.claim('p1', { id: 'wins' }), { code: 'too late' });
});

test('a failed payout can be claimed again', async () => {
  const ev = createEvents({ cfg, playfab: { addCurrency: async () => { throw new Error('down'); } }, now: () => T0 });
  for (let i = 0; i < 3; i++) ev.record('p1', { won: true });
  await assert.rejects(ev.claim('p1', { id: 'wins' }), /down/);
  assert.equal(ev.board('p1').live.find(e => e.id === 'wins').claimed, false);
});

test('no events in the config means an empty board', () => {
  assert.deepEqual(createEvents({ cfg: {}, now }).board('p'), { ok: true, live: [], soon: [], done: [] });
});
