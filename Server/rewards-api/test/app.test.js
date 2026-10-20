'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const { createApp } = require('../src/app');
const { createStore } = require('../src/store');
const R = require('../src/rewards');
const { PlayFabError } = require('../src/playfab');

const cfg = {
  titleId: 'T', currencyCode: 'CO', requestsPerMinutePerIp: 1000, minPlayers: 4, maxPlayers: 16, minRounds: 1, minDurationSec: 60,
  quorumRatio: 0.5, reportWindowSeconds: 900, dailyMatchCap: 3, trustProxy: false,
  rewards: { baseXp: 20, winXp: 25, survivedXp: 5, baseCoins: 10, winCoins: 20, rankWin: 50, rankLose: -20 },
};

const PLAYERS = ['AAAA0001', 'AAAA0002', 'AAAA0003', 'AAAA0004', 'AAAA0005', 'AAAA0006', 'AAAA0007', 'AAAA0008'];

/** A PlayFab that lives in memory: tickets are "t-<playFabId>". */
function fakePlayFab() {
  const progress = {}, coins = {}, calls = [];
  const pf = {
    progress, coins, calls, failCoinsOnce: false, down: false,
    async authenticate(t) { if (!/^t-/.test(t)) throw new PlayFabError('bad', 400, 'InvalidSessionTicket'); return t.slice(2); },
    async getProgress(id) { if (pf.down) throw new PlayFabError('down', 503, 'Unreachable'); return progress[id] ? JSON.parse(JSON.stringify(progress[id])) : null; },
    async setProgress(id, key, v) { calls.push(['progress', id]); progress[id] = JSON.parse(JSON.stringify(v)); },
    async addCurrency(id, code, amount) {
      if (pf.failCoinsOnce) { pf.failCoinsOnce = false; throw new PlayFabError('boom', 500, 'X'); }
      calls.push(['coins', id, amount]); coins[id] = (coins[id] || 0) + amount;
    },
  };
  return pf;
}

function setup(over = {}) {
  const pf = fakePlayFab();
  let t = Date.UTC(2026, 9, 20, 12, 0, 0);
  const clock = { now: () => t, advance: ms => { t += ms; } };
  const store = createStore(null);
  const app = createApp({ cfg: { ...cfg, ...over }, playfab: pf, store, now: clock.now, log: { error() {} } });
  return { pf, store, clock, app };
}

const report = (id, over = {}) => ({ matchId: 'match-0001', players: PLAYERS, winner: 'Town', myRole: 'Civilian', survived: true, rounds: 3, durationSec: 600, ...over });
const send = (app, id, body) => app.handleMatchResult('t-' + id, body);

test('nothing is paid until more than half of the players reported the same match', async () => {
  const { app, pf } = setup();
  for (let i = 0; i < 4; i++) {
    const r = await send(app, PLAYERS[i], report(PLAYERS[i]));
    assert.equal(r.body.waiting, true, 'report ' + (i + 1));
  }
  assert.equal(pf.calls.length, 0);
  const r5 = await send(app, PLAYERS[4], report(PLAYERS[4]));
  assert.equal(r5.body.granted, true);
  assert.equal(pf.coins[PLAYERS[4]], 10 + 20);
});

test('the reports that came earlier are paid when they ask again after the quorum', async () => {
  const { app, pf } = setup();
  for (let i = 0; i < 5; i++) await send(app, PLAYERS[i], report(PLAYERS[i]));
  const again = await send(app, PLAYERS[0], report(PLAYERS[0]));
  assert.equal(again.body.granted, true);
  assert.equal(pf.coins[PLAYERS[0]], 30);
});

test('asking again never pays twice', async () => {
  const { app, pf } = setup();
  for (let i = 0; i < 5; i++) await send(app, PLAYERS[i], report(PLAYERS[i]));
  for (let k = 0; k < 5; k++) await send(app, PLAYERS[4], report(PLAYERS[4]));
  assert.equal(pf.coins[PLAYERS[4]], 30);
  assert.equal(pf.progress[PLAYERS[4]].games, 1);
  assert.equal(pf.calls.filter(c => c[0] === 'progress' && c[1] === PLAYERS[4]).length, 1);
});

test('xp, level, rank points and stats are written for a win and for a loss', async () => {
  const { app, pf } = setup();
  const mk = (i, over) => report(PLAYERS[i], over);
  for (let i = 0; i < 4; i++) await send(app, PLAYERS[i], mk(i, { myRole: 'Civilian' }));
  await send(app, PLAYERS[4], mk(4, { myRole: 'Mafia', survived: false }));      // mafia on a town win: a loss
  await send(app, PLAYERS[0], mk(0, { myRole: 'Civilian' }));
  const winner = pf.progress[PLAYERS[0]];
  assert.equal(winner.xp, 50); assert.equal(winner.rankPoints, 50); assert.equal(winner.wins, 1); assert.equal(winner.citizenWins, 1);
  const loser = pf.progress[PLAYERS[4]];
  assert.equal(loser.xp, 20); assert.equal(loser.rankPoints, 0, 'rank points never go below zero');
  assert.equal(loser.losses, 1); assert.equal(loser.mafiaGames, 1); assert.equal(loser.mafiaWins, 0);
  assert.equal(pf.coins[PLAYERS[4]], 10);
});

test('level ups follow the game curve', () => {
  const p = R.newProgress();
  R.addXp(p, 100);                                // level 1 needs 150
  assert.equal(p.level, 1);
  R.addXp(p, 60);
  assert.equal(p.level, 2); assert.equal(p.xp, 10); assert.equal(p.xpToNext, 200);
});

test('a lone player who invents another story is not paid and does not block the honest ones', async () => {
  const { app, pf } = setup();
  const liar = await send(app, PLAYERS[0], report(PLAYERS[0], { winner: 'Mafia', myRole: 'Mafia' }));
  assert.equal(liar.body.waiting, true);
  for (let i = 1; i < 6; i++) await send(app, PLAYERS[i], report(PLAYERS[i]));
  const honest = await send(app, PLAYERS[5], report(PLAYERS[5]));
  assert.equal(honest.body.granted, true);
  const liarAgain = await send(app, PLAYERS[0], report(PLAYERS[0], { winner: 'Mafia', myRole: 'Mafia' }));
  assert.equal(liarAgain.body.granted, false);
  assert.equal(pf.coins[PLAYERS[0]], undefined);
});

test('a reporter who is not in the player list is refused', async () => {
  const { app } = setup();
  const r = await send(app, 'OUTSIDER1', report('OUTSIDER1'));
  assert.equal(r.status, 403);
});

test('bad shapes are refused', async () => {
  const { app } = setup();
  const bad = [
    report(PLAYERS[0], { matchId: 'x' }), report(PLAYERS[0], { players: PLAYERS.slice(0, 3) }), report(PLAYERS[0], { winner: 'Nobody' }),
    report(PLAYERS[0], { myRole: 'King' }), report(PLAYERS[0], { rounds: 0 }), report(PLAYERS[0], { durationSec: 5 }),
    report(PLAYERS[0], { players: [...PLAYERS.slice(0, 7), PLAYERS[0]] }), report(PLAYERS[0], { survived: 'yes' }), null,
  ];
  for (const b of bad) assert.equal((await send(app, PLAYERS[0], b)).status, 400, JSON.stringify(b && Object.keys(b)));
});

test('a ticket that PlayFab does not know is refused', async () => {
  const { app } = setup();
  await assert.rejects(() => app.handleMatchResult('nope', report('x')), e => e.status === 400);
});

test('a player is paid for at most dailyMatchCap matches a day', async () => {
  const { app, pf, clock } = setup();
  for (let m = 0; m < 5; m++) {
    for (let i = 0; i < 5; i++) await send(app, PLAYERS[i], report(PLAYERS[i], { matchId: 'match-day-' + m }));
    await send(app, PLAYERS[0], report(PLAYERS[0], { matchId: 'match-day-' + m }));   // asks again once the quorum is there
  }
  assert.equal(pf.progress[PLAYERS[0]].games, 3);
  assert.equal(pf.coins[PLAYERS[0]], 90);
  // the next day the counter starts again
  clock.advance(24 * 3600 * 1000);
  for (let i = 0; i < 5; i++) await send(app, PLAYERS[i], report(PLAYERS[i], { matchId: 'match-next-day' }));
  await send(app, PLAYERS[0], report(PLAYERS[0], { matchId: 'match-next-day' }));
  assert.equal(pf.progress[PLAYERS[0]].games, 4);
});

test('reports after the window are ignored', async () => {
  const { app, clock } = setup();
  await send(app, PLAYERS[0], report(PLAYERS[0]));
  clock.advance(1000 * 1000);
  const late = await send(app, PLAYERS[1], report(PLAYERS[1]));
  assert.equal(late.status, 410);
});

test('if PlayFab is down nothing is lost: the same call works later and pays once', async () => {
  const { app, pf } = setup();
  for (let i = 0; i < 4; i++) await send(app, PLAYERS[i], report(PLAYERS[i]));
  pf.down = true;
  await assert.rejects(() => send(app, PLAYERS[4], report(PLAYERS[4])));
  pf.down = false;
  const ok = await send(app, PLAYERS[4], report(PLAYERS[4]));
  assert.equal(ok.body.granted, true);
  assert.equal(pf.coins[PLAYERS[4]], 30);
});

test('if the coins step fails after the xp was written, the retry pays only the coins', async () => {
  const { app, pf } = setup();
  for (let i = 0; i < 4; i++) await send(app, PLAYERS[i], report(PLAYERS[i]));
  pf.failCoinsOnce = true;
  await assert.rejects(() => send(app, PLAYERS[4], report(PLAYERS[4])));
  assert.equal(pf.progress[PLAYERS[4]].games, 1);
  const ok = await send(app, PLAYERS[4], report(PLAYERS[4]));
  assert.equal(ok.body.granted, true);
  assert.equal(pf.progress[PLAYERS[4]].games, 1, 'xp is not added a second time');
  assert.equal(pf.coins[PLAYERS[4]], 30);
});

test('the http layer: a ticket is needed, answers are json, big bodies are refused', async () => {
  const { app } = setup();
  await new Promise(r => app.server.listen(0, '127.0.0.1', r));
  const port = app.server.address().port;
  const call = (method, path, headers, body) => new Promise((resolve, reject) => {
    const req = http.request({ host: '127.0.0.1', port, method, path, headers }, res => {
      let d = ''; res.on('data', c => d += c); res.on('end', () => resolve({ status: res.statusCode, json: d ? JSON.parse(d) : null }));
    });
    req.on('error', reject); if (body) req.write(body); req.end();
  });
  try {
    assert.equal((await call('GET', '/health', {})).json.ok, true);
    assert.equal((await call('POST', '/v1/match-result', {}, '{}')).status, 401);
    assert.equal((await call('GET', '/nothing', {})).status, 404);
    const good = await call('POST', '/v1/match-result', { 'X-Authorization': 't-' + PLAYERS[0], 'Content-Type': 'application/json' }, JSON.stringify(report(PLAYERS[0])));
    assert.equal(good.status, 200); assert.equal(good.json.waiting, true);
    const junk = await call('POST', '/v1/match-result', { 'X-Authorization': 't-' + PLAYERS[0] }, '{not json');
    assert.equal(junk.status, 400);
    const huge = await call('POST', '/v1/match-result', { 'X-Authorization': 't-' + PLAYERS[0] }, 'x'.repeat(20000)).catch(e => ({ status: 'closed' }));
    assert.ok(huge.status === 413 || huge.status === 'closed');
  } finally { await new Promise(r => app.server.close(r)); }
});

test('the per-ip rate limit answers 429', async () => {
  const { app } = setup({ requestsPerMinutePerIp: 3 });
  await new Promise(r => app.server.listen(0, '127.0.0.1', r));
  const port = app.server.address().port;
  const get = () => new Promise(res => http.get({ host: '127.0.0.1', port, path: '/nothing' }, r => { r.resume(); res(r.statusCode); }));
  try {
    const codes = []; for (let i = 0; i < 5; i++) codes.push(await get());
    assert.deepEqual(codes, [404, 404, 404, 429, 429]);
  } finally { await new Promise(r => app.server.close(r)); }
});
