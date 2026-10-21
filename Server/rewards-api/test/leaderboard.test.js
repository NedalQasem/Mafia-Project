'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createChat } = require('../src/chat');
const { createLeaderboard } = require('../src/leaderboard');
const { createApp } = require('../src/app');
const { createStore } = require('../src/store');

const P = n => 'PLAYER' + String(n).padStart(2, '0');

function setup(cfg = {}) {
  let t = Date.UTC(2026, 9, 21, 12, 0, 0);                 // Wednesday 21 Oct 2026, 15:00 in Amman (UTC+3)
  const clock = { now: () => t, advance: ms => { t += ms; } };
  const playfab = {
    async authenticate(tk) { return tk.slice(2); },
    async getDisplayName(pf) { return 'لاعب ' + pf.slice(-2); },
    async getProgress() { return { rankPoints: 2100, level: 5 }; },
    async setProgress() {}, async addCurrency() {},
  };
  const chat = createChat({ cfg: {}, playfab, now: clock.now, log: { error() {} } });
  const lb = createLeaderboard({ chat, cfg: { leaderboard: cfg }, now: clock.now, log: { error() {} } });
  return { lb, chat, clock, playfab };
}

const game = (over = {}) => Object.assign({ won: true, role: 'Civilian', survived: true, durationSec: 600, rankPointsGained: 50, rankPointsNow: 100 }, over);

test('a board lists players by value, and the top ten rule shows what is missing', async () => {
  const { lb } = setup();
  for (let i = 1; i <= 12; i++) for (let g = 0; g < i; g++) lb.record(P(i), game());
  const b = await lb.board(P(1), { period: 'day', cat: 1 });
  assert.equal(b.top.length, 12);
  assert.equal(b.top[0].value, 12); assert.equal(b.top[0].name, 'لاعب 12');
  assert.equal(b.top[11].value, 1); assert.equal(b.top[11].me, true);
  assert.equal(b.me.position, 12); assert.equal(b.me.value, 1);
  assert.equal(b.me.tenth, 3); assert.equal(b.me.need, 3);          // 3 - 1 + 1 to pass the tenth
  const best = await lb.board(P(12), { period: 'day', cat: 1 });
  assert.equal(best.me.position, 1); assert.equal(best.me.need, 0);
});

test('a player with nothing in a category has no position and does not appear', async () => {
  const { lb } = setup();
  lb.record(P(1), game({ won: false, rankPointsGained: -20 }));
  const wins = await lb.board(P(1), { period: 'day', cat: 1 });
  assert.equal(wins.top.length, 0); assert.equal(wins.me.position, 0);
  const pts = await lb.board(P(1), { period: 'day', cat: 0 });
  assert.equal(pts.top.length, 0);                                    // lost points do not make a score
});

test('the categories count what they say', async () => {
  const { lb } = setup();
  lb.record(P(1), game({ role: 'Mafia' }));
  lb.record(P(1), game({ role: 'Sheriff', survived: false }));
  lb.record(P(1), game({ role: 'Mafia', won: false, rankPointsGained: -20, durationSec: 1230 }));
  const v = async cat => (await lb.board(P(1), { period: 'week', cat })).me.value;
  assert.equal(await v(0), 80);            // 50 + 50 - 20
  assert.equal(await v(1), 2);
  assert.equal(await v(2), 1);             // mafia wins
  assert.equal(await v(3), 1);             // sheriff wins
  assert.equal(await v(4), 40);            // minutes: (600 + 600 + 1230) / 60
  assert.equal(await v(5), 3);
  assert.equal(await v(6), 2);             // survived
  assert.equal(await v(7), 2);             // best streak inside the period
});

test('the day resets at local midnight, the week on the chosen day, and the countdown follows', async () => {
  const { lb, clock } = setup();
  assert.equal(lb.secondsToReset('day'), 9 * 3600);                 // 15:00 local -> 24:00
  lb.record(P(1), game());
  assert.equal((await lb.board(P(1), { period: 'day', cat: 1 })).me.value, 1);
  clock.advance(10 * 3600 * 1000);                                  // Thursday 01:00 local
  assert.equal((await lb.board(P(1), { period: 'day', cat: 1 })).me.value, 0);
  assert.equal((await lb.board(P(1), { period: 'week', cat: 1 })).me.value, 1);
  // the week starts on Saturday (6): Thursday 01:00 is in its sixth day, so it ends after the rest of Thursday and all of Friday
  assert.equal(lb.secondsToReset('week'), 23 * 3600 + 86400);
  clock.advance(24 * 3600 * 1000);                                  // Friday
  clock.advance(24 * 3600 * 1000);                                  // Saturday: a new week
  assert.equal((await lb.board(P(1), { period: 'week', cat: 1 })).me.value, 0);
});

test('a streak survives the period change but a loss breaks it', async () => {
  const { lb, clock } = setup();
  lb.record(P(1), game()); lb.record(P(1), game());
  clock.advance(24 * 3600 * 1000);
  lb.record(P(1), game());                                          // third win in a row, counted on the new day
  assert.equal((await lb.board(P(1), { period: 'day', cat: 7 })).me.value, 3);
  lb.record(P(1), game({ won: false, rankPointsGained: -20 }));
  lb.record(P(1), game());
  assert.equal((await lb.board(P(1), { period: 'day', cat: 7 })).me.value, 3);   // best of the day stays
  assert.equal((await lb.wall()).records.streak.value, 3);
});

test('the hall of fame shows the godfather and the records of all time', async () => {
  const { lb } = setup();
  assert.equal((await lb.wall()).godfather, null);
  lb.record(P(1), game({ rankPointsNow: 900, role: 'Mafia', durationSec: 7200 }));
  lb.record(P(2), game({ rankPointsNow: 1500 }));
  lb.record(P(2), game({ rankPointsNow: 1550 }));
  const w = await lb.wall();
  assert.equal(w.godfather.pf, P(2)); assert.equal(w.godfather.points, 1550);
  assert.equal(w.records.games.name, 'لاعب 02'); assert.equal(w.records.games.value, 2);
  assert.equal(w.records.mafiaWins.name, 'لاعب 01');
  assert.equal(w.records.hours.name, 'لاعب 01'); assert.equal(w.records.hours.value, 2);
  assert.deepEqual(w.seasons, []);
});

test('old days and weeks are dropped', async () => {
  const { lb, clock } = setup({ keepDays: 2, keepWeeks: 1 });
  for (let d = 0; d < 5; d++) { lb.record(P(1), game()); clock.advance(8 * 24 * 3600 * 1000 / 2); }
  lb.purge();
  assert.ok(Object.keys(lb._state().days).length <= 2);
  assert.ok(Object.keys(lb._state().weeks).length <= 1);
});

test('bad requests are refused', async () => {
  const { lb } = setup();
  await assert.rejects(lb.board(P(1), { period: 'year', cat: 0 }), e => e.code === 'bad_period');
  await assert.rejects(lb.board(P(1), { period: 'day', cat: 99 }), e => e.code === 'bad_category');
});

test('a paid match writes the numbers once, however often the players ask again', async () => {
  const { lb, chat, playfab, clock } = setup();
  const cfg = { titleId: 'T', currencyCode: 'CO', requestsPerMinutePerIp: 1000, minPlayers: 4, maxPlayers: 16, minRounds: 1, minDurationSec: 60, quorumRatio: 0.5, reportWindowSeconds: 900, dailyMatchCap: 30, trustProxy: false, rewards: { baseXp: 1, winXp: 1, survivedXp: 1, baseCoins: 1, winCoins: 1, rankWin: 50, rankLose: -20 } };
  const app = createApp({ cfg, playfab, store: createStore(null), chat, leaderboard: lb, now: clock.now, log: { error() {} } });
  const players = [P(1), P(2), P(3), P(4)];
  const body = (role, survived) => ({ matchId: 'MATCH-0001', players, winner: 'Town', myRole: role, survived, rounds: 3, durationSec: 900 });
  const ticket = pf => 't-' + pf;
  for (let i = 0; i < 3; i++) { const out = await app.handleMatchResult(ticket(players[i]), body(i === 0 ? 'Mafia' : 'Civilian', true)); assert.equal(out.status, 200); }
  for (let i = 0; i < 3; i++) await app.handleMatchResult(ticket(players[i]), body(i === 0 ? 'Mafia' : 'Civilian', true));   // asking again
  const b = await lb.board(P(2), { period: 'day', cat: 1 });
  assert.equal(b.me.value, 1);                                       // P2 won once
  assert.equal((await lb.board(P(1), { period: 'day', cat: 1 })).me.value, 0);   // the mafia lost
  assert.equal((await lb.board(P(1), { period: 'day', cat: 5 })).me.value, 1);   // but played once
  assert.equal((await lb.board(P(2), { period: 'day', cat: 0 })).me.value, 50);
});
