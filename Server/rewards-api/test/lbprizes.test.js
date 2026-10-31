'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createChat } = require('../src/chat');
const { createLeaderboard } = require('../src/leaderboard');
const { createMail } = require('../src/mail');
const { createAdminStore } = require('../src/adminstore');
const { createLeaderPrizes } = require('../src/lbprizes');

const DAY = 24 * 3600 * 1000;
const T0 = Date.UTC(2026, 10, 4, 9);          // a Wednesday, 12:00 local (UTC+3)

function setup() {
  let clock = T0; const now = () => clock;
  const playfab = { getDisplayName: async pf => 'اسم ' + pf, getProgress: async () => null };
  const chat = createChat({ cfg: {}, playfab, now });
  const leaderboard = createLeaderboard({ chat, cfg: {}, now });
  const mail = createMail({ cfg: { currencyCode: 'CO' }, playfab: null, now });
  const store = createAdminStore({ file: null, now });
  const prizes = createLeaderPrizes({ leaderboard, mail, store, packs: () => store.packs, now, log: { error() {} } });
  leaderboard.setPrizes(prizes);
  const play = (pf, points, won = true) => leaderboard.record(pf, { won, role: 'Citizen', survived: true, durationSec: 600, rankPointsGained: points, rankPointsNow: points });
  const inbox = pf => mail.list(pf).items;
  return { leaderboard, mail, store, prizes, play, inbox, tick: ms => { clock += ms; }, now };
}

test('the default: the first of the day gets 300 coins by mail when the day is over, once', async () => {
  const s = setup();
  s.play('PLAYER0001', 90); s.play('PLAYER0002', 50); s.play('PLAYER0003', 0, false);
  assert.deepEqual(s.prizes.payDue().find(x => x.period === 'day').count, 0);       // yesterday: nobody played, nothing to give (and it is marked paid)
  assert.equal(s.inbox('PLAYER0001').length, 0);                                       // today is not over
  s.tick(DAY);
  const paid = s.prizes.payDue();
  assert.equal(paid.find(x => x.period === 'day').count, 1);
  const got = s.inbox('PLAYER0001');
  assert.equal(got.length, 1); assert.equal(got[0].pack.coins, 300); assert.match(got[0].title.ar, /سبورة الليلة · المركز 1/);
  assert.equal(s.inbox('PLAYER0002').length, 0);
  assert.deepEqual(s.prizes.payDue(), []);                                             // never twice
  assert.equal(s.inbox('PLAYER0001').length, 1);
});

test('the week pays its first three when the week is over (5000, 2500, 1000), and a short ranking pays only who placed', async () => {
  const s = setup();
  s.play('PLAYER0001', 30); s.play('PLAYER0002', 90); s.play('PLAYER0003', 60); s.play('PLAYER0004', 10);
  s.prizes.payDue();                                    // last week (empty) is settled now
  s.tick(7 * DAY);
  s.prizes.payDue();
  const coins = pf => s.inbox(pf).filter(m => /جريدة الأسبوع/.test(m.title.ar)).map(m => m.pack.coins);
  assert.deepEqual(coins('PLAYER0002'), [5000]); assert.deepEqual(coins('PLAYER0003'), [2500]); assert.deepEqual(coins('PLAYER0001'), [1000]); assert.deepEqual(coins('PLAYER0004'), []);
  // a week with two players: places 1 and 2 only
  const t = setup(); t.play('PLAYER0005', 20); t.play('PLAYER0006', 10); t.prizes.payDue(); t.tick(7 * DAY); t.prizes.payDue();
  assert.equal(t.inbox('PLAYER0005').filter(m => /الأسبوع/.test(m.title.ar)).length, 1); assert.equal(t.inbox('PLAYER0006').filter(m => /الأسبوع/.test(m.title.ar)).length, 1);
});

test('the board answers its prizes; the admin config replaces the default, checks its rows and can use packages', async () => {
  const s = setup();
  s.play('PLAYER0001', 40);
  let b = await s.leaderboard.board('PLAYER0001', { period: 'week', cat: 0 });
  assert.deepEqual(b.prizes.map(p => p.coins), [5000, 2500, 1000]);
  assert.deepEqual((await s.leaderboard.board('PLAYER0001', { period: 'day', cat: 1 })).prizes, []);      // wins: no prize by default
  s.store.packs = [{ id: 'pk1', name: { ar: 'حزمة الملوك' }, coins: 700, gems: 5, items: ['frame_rose'] }];
  for (const [bad, code] of [[{ day: { nope: [{ from: 1, to: 1, coins: 1 }] } }, 'bad_category'], [{ day: { wins: [{ from: 2, to: 1, coins: 5 }] } }, 'bad_ranks'], [{ day: { wins: [{ from: 1, to: 1 }] } }, 'bad_prize'],
    [{ day: { wins: [{ from: 1, to: 3, coins: 5 }, { from: 2, to: 2, coins: 1 }] } }, 'overlap'], [{ day: { wins: [{ from: 1, to: 1, packId: 'zz' }] } }, 'pack_not_found']])
    assert.throws(() => s.prizes.clean(bad), e => e.code === code, code);
  s.store.lbPrizes = s.prizes.clean({ day: { wins: [{ from: 1, to: 1, packId: 'pk1' }, { from: 2, to: 5, gems: 3 }] }, week: {} });
  b = await s.leaderboard.board('PLAYER0001', { period: 'day', cat: 1 });
  assert.deepEqual(b.prizes[0], { from: 1, to: 1, coins: 700, gems: 5, items: ['frame_rose'], name: 'حزمة الملوك' });
  assert.deepEqual((await s.leaderboard.board('PLAYER0001', { period: 'week', cat: 0 })).prizes, []);     // the week has no prizes now
  s.prizes.payDue(); s.tick(DAY); s.prizes.payDue();
  const got = s.inbox('PLAYER0001');
  assert.equal(got.length, 1); assert.deepEqual(got[0].pack, { coins: 700, gems: 5, items: ['frame_rose'] });
  const a = s.prizes.admin();
  assert.equal(a.isDefault, false); assert.equal(a.config.day.wins[0].gives.name, 'حزمة الملوك'); assert.ok(a.history.length >= 2);
});
