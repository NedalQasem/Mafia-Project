'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createEvents } = require('../src/events');
const { createMail } = require('../src/mail');

const T0 = Date.UTC(2026, 10, 2, 12);
let clock = T0;
const now = () => clock;
const packs = [{ id: 'gold', name: { ar: 'ذهبية' }, coins: 5000, gems: 0, items: ['frame_ember'] }, { id: 'silver', name: { ar: 'فضية' }, coins: 3000, gems: 0, items: [] }, { id: 'small', name: { ar: 'تشجيعية' }, coins: 1000, gems: 2, items: [] }];
const ranking = (extra = {}) => Object.assign({ enabled: true, metric: 'wins', winners: 4, distribution: 'auto', prizes: [{ from: 1, to: 1, packId: 'gold' }, { from: 2, to: 2, packId: 'silver' }, { from: 3, to: 4, packId: 'small' }] }, extra);
const race = (extra = {}) => Object.assign({ id: 'race', kind: 'info', title: 'سباق', titleEn: 'Race', from: '2026-11-01T00:00:00Z', until: '2026-11-08T00:00:00Z', ranking: ranking() }, extra);

function make(list) {
  clock = T0;
  const playfab = { added: [], addCurrency: async (pf, code, n) => { playfab.added.push([pf, code, n]); }, grantItems: async () => {} };
  const mail = createMail({ cfg: { currencyCode: 'CO' }, playfab, now });
  const ev = createEvents({ cfg: { currencyCode: 'CO' }, source: () => list, playfab, mail, packs: () => packs, nameOf: async pf => 'اسم ' + pf, now });
  return { ev, mail, playfab };
}
const win = (ev, pf, n = 1) => { for (let i = 0; i < n; i++) ev.record(pf, { won: true, points: 50, secs: 600 }); };

test('only matches inside the window count, most wins first, the one who got there first wins a tie', async () => {
  const { ev } = make([race()]);
  win(ev, 'A', 3); win(ev, 'B', 5); win(ev, 'C', 3);
  ev.record('D', { won: false, points: -20, secs: 300 });            // played but never won: not ranked by wins
  const r = await ev.ranking('A', { id: 'race' });
  assert.deepEqual(r.rows.map(x => x.name), ['اسم B', 'اسم A', 'اسم C']);
  assert.deepEqual(r.rows.map(x => x.value), [5, 3, 3]);
  assert.deepEqual(r.mine, { rank: 2, value: 3 });
  assert.equal(r.rows[1].me, true);
  clock = Date.UTC(2026, 10, 9); win(ev, 'D', 9);                    // after the end nothing counts
  assert.equal((await ev.ranking('A', { id: 'race' })).rows.length, 3);
});

test('the metrics: games, rank points and minutes', async () => {
  for (const [metric, want] of [['games', 2], ['points', 100], ['secs', 1200]]) {
    const { ev } = make([race({ ranking: ranking({ metric }) })]);
    win(ev, 'A', 2);
    assert.equal((await ev.ranking('A', { id: 'race' })).mine.value, want, metric);
  }
});

test('the board shows my place and the prizes are listed with their packs', async () => {
  const { ev } = make([race()]);
  win(ev, 'A', 2); win(ev, 'B', 4);
  const mine = ev.board('A').live[0].ranking;
  assert.deepEqual(mine, { metric: 'wins', winners: 4, distributed: false, mine: { rank: 2, value: 2 } });
  assert.equal(ev.board('Z').live[0].ranking.mine, null);
  const r = await ev.ranking('A', { id: 'race' });
  assert.deepEqual(r.prizes.map(p => [p.from, p.to, p.coins]), [[1, 1, 5000], [2, 2, 3000], [3, 4, 1000]]);
  assert.ok(r.endsInSec > 0);
  await assert.rejects(ev.ranking('A', { id: 'nope' }), { code: 'no_ranking' });
});

test('an event without a ranking has none', async () => {
  const { ev } = make([race({ ranking: undefined })]);
  assert.equal(ev.board('A').live[0].ranking, null);
  await assert.rejects(ev.ranking('A', { id: 'race' }), { code: 'no_ranking' });
});

test('distributing sends one telegram per winner with the pack of his rank, once, and freezes the counting', async () => {
  const { ev, mail } = make([race()]);
  win(ev, 'A', 9); win(ev, 'B', 7); win(ev, 'C', 5); win(ev, 'D', 3); win(ev, 'E', 1);     // E is fifth: no prize
  const d = await ev.distribute('race', { by: 'admin' });
  assert.equal(d.count, 4);
  const got = pf => mail.list(pf).items[0];
  assert.deepEqual(got('A').pack, { coins: 5000, gems: 0, items: ['frame_ember'] }); assert.equal(got('A').title.ar, 'جائزة المركز 1 · سباق'); assert.equal(got('A').title.en, 'Rank 1 prize · Race');
  assert.equal(got('B').pack.coins, 3000); assert.equal(got('C').pack.coins, 1000); assert.equal(got('D').pack.gems, 2);
  assert.equal(mail.list('E').items.length, 0);
  await assert.rejects(ev.distribute('race'), { code: 'already_distributed' });
  win(ev, 'E', 50);                                                                          // frozen: E cannot climb after the prizes
  assert.equal((await ev.ranking('A', { id: 'race' })).rows.find(r => r.name === 'اسم E').value, 1);
  assert.equal((await ev.ranking('A', { id: 'race' })).distributed, true);
  assert.equal((await ev.adminRanking('race')).distributed.count, 4);
});

test('a rank without a prize rule gets nothing, and a pack that was deleted is skipped', async () => {
  const { ev, mail } = make([race({ ranking: ranking({ winners: 3, prizes: [{ from: 1, to: 1, packId: 'gold' }, { from: 3, to: 3, packId: 'deleted' }] }) })]);
  win(ev, 'A', 3); win(ev, 'B', 2); win(ev, 'C', 1);
  assert.equal((await ev.distribute('race')).count, 1);
  assert.equal(mail.list('A').items.length, 1); assert.equal(mail.list('B').items.length, 0); assert.equal(mail.list('C').items.length, 0);
});

test('auto distribution waits for the end, and manual events are left to the admin', async () => {
  const { ev, mail } = make([race(), race({ id: 'manual', ranking: ranking({ distribution: 'manual' }) })]);
  win(ev, 'A', 2);
  await ev.autoDistribute(); assert.equal(mail.list('A').items.length, 0);                    // still running
  clock = Date.UTC(2026, 10, 9);
  await ev.autoDistribute(); await ev.autoDistribute();                                      // twice: still only one telegram per event
  assert.equal(mail.list('A').items.length, 1);
  assert.equal((await ev.adminRanking('race')).distributed.by, 'auto');
  assert.equal((await ev.adminRanking('manual')).distributed, null);
});
