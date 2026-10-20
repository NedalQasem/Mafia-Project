'use strict';
// The HTTP service. POST /v1/match-result is idempotent: a player's device sends the finished match, and sends it again to
// see whether the reward arrived (it answers "waiting" until more than half of the players reported the same match).

const http = require('node:http');
const crypto = require('node:crypto');
const R = require('./rewards');
const { PlayFabError } = require('./playfab');

const PROGRESS_KEY = 'Progress';

function createApp({ cfg, playfab, store, now = () => Date.now(), log = console }) {
  const buckets = new Map();   // ip -> { count, resetAt }
  const locks = new Map();     // playFabId -> promise chain (one update per player at a time)

  function limited(ip) {
    const t = now();
    let b = buckets.get(ip);
    if (!b || t > b.resetAt) { b = { count: 0, resetAt: t + 60000 }; buckets.set(ip, b); }
    return ++b.count > cfg.requestsPerMinutePerIp;
  }

  function withLock(id, fn) {
    const prev = locks.get(id) || Promise.resolve();
    const next = prev.catch(() => {}).then(fn);
    locks.set(id, next);
    next.finally(() => { if (locks.get(id) === next) locks.delete(id); }).catch(() => {});
    return next;
  }

  function summaryHash(r) {
    return crypto.createHash('sha256').update(JSON.stringify([[...r.players].sort(), r.winner])).digest('hex');
  }

  const today = () => new Date(now()).toISOString().slice(0, 10);

  /** Pays one player of a match whose quorum is reached. Safe to call again: each step is remembered. */
  async function pay(match, group, playFabId) {
    const rep = group.reports[playFabId];
    const g = (match.grants[playFabId] = match.grants[playFabId] || { progress: false, coins: false });
    const reward = R.rewardFor({ myRole: rep.myRole, survived: rep.survived, winner: group.winner }, cfg);
    if (g.capped) return { granted: false, capped: true };
    if (!g.progress && !g.coins && store.dailyCount(playFabId, today()) >= cfg.dailyMatchCap) { g.capped = true; store.putMatch(match.id, match); return { granted: false, capped: true }; }
    if (!g.progress) {
      const progress = (await playfab.getProgress(playFabId, PROGRESS_KEY)) || R.newProgress();
      R.applyReward(progress, { myRole: rep.myRole }, reward, new Date(now()).toISOString());
      await playfab.setProgress(playFabId, PROGRESS_KEY, progress);
      g.progress = true;
      store.addDaily(playFabId, today());
      store.putMatch(match.id, match);
    }
    if (!g.coins) {
      if (reward.coins > 0) await playfab.addCurrency(playFabId, cfg.currencyCode, reward.coins);
      g.coins = true;
      store.putMatch(match.id, match);
    }
    return { granted: true, xp: reward.xp, coins: reward.coins, rankPoints: reward.rankPoints, won: reward.won };
  }

  async function handleMatchResult(ticket, body) {
    const playFabId = await playfab.authenticate(ticket);
    const bad = R.validateReport(body, cfg);
    if (bad) return { status: 400, body: { ok: false, error: bad } };
    if (!body.players.includes(playFabId)) return { status: 403, body: { ok: false, error: 'not a player of this match' } };

    return withLock('match:' + body.matchId, async () => {
      let match = store.match(body.matchId);
      if (!match) match = { id: body.matchId, firstSeen: now(), groups: {}, reporters: {}, grants: {} };
      const h = summaryHash(body);
      if (match.reporters[playFabId] === undefined) {
        if (now() - match.firstSeen > cfg.reportWindowSeconds * 1000) return { status: 410, body: { ok: false, error: 'report window closed' } };
        // reports that agree about who played and who won form a group; only a group with more than half of its players is paid,
        // so one player who invents a different story cannot pay himself and cannot block the honest ones
        const group = (match.groups[h] = match.groups[h] || { players: [...body.players].sort(), winner: body.winner, reports: {} });
        group.reports[playFabId] = { myRole: body.myRole, survived: body.survived, rounds: body.rounds, durationSec: body.durationSec };
        match.reporters[playFabId] = h;
        store.putMatch(match.id, match);
      }
      const group = match.groups[match.reporters[playFabId]];
      const need = R.quorumFor(group.players.length, cfg);
      const have = Object.keys(group.reports).length;
      if (have < need) return { status: 200, body: { ok: true, granted: false, waiting: true, reports: have, needed: need } };

      const r = await withLock('pf:' + playFabId, () => pay(match, group, playFabId));
      return { status: 200, body: { ok: true, ...r } };
    });
  }

  function send(res, status, obj) {
    const text = JSON.stringify(obj);
    res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Content-Length': Buffer.byteLength(text), 'Cache-Control': 'no-store' });
    res.end(text);
  }

  const server = http.createServer((req, res) => {
    const ip = (cfg.trustProxy && req.headers['x-forwarded-for'] ? String(req.headers['x-forwarded-for']).split(',')[0].trim() : req.socket.remoteAddress) || 'unknown';
    if (req.method === 'GET' && req.url === '/health') return send(res, 200, { ok: true });
    if (limited(ip)) return send(res, 429, { ok: false, error: 'slow down' });
    if (req.method !== 'POST' || req.url !== '/v1/match-result') return send(res, 404, { ok: false, error: 'not found' });

    const ticket = req.headers['x-authorization'];
    if (typeof ticket !== 'string' || ticket.length < 10 || ticket.length > 2000) return send(res, 401, { ok: false, error: 'no ticket' });

    let size = 0; const chunks = [];
    req.on('data', c => { size += c.length; if (size > 8192) { send(res, 413, { ok: false, error: 'too big' }); req.destroy(); } else chunks.push(c); });
    req.on('end', async () => {
      if (res.writableEnded) return;
      let body;
      try { body = JSON.parse(Buffer.concat(chunks).toString('utf8')); } catch { return send(res, 400, { ok: false, error: 'bad json' }); }
      try {
        const out = await handleMatchResult(ticket, body);
        send(res, out.status, out.body);
      } catch (e) {
        if (e instanceof PlayFabError && (e.status === 401 || e.status === 403 || e.code === 'InvalidSessionTicket' || e.code === 'NoUser')) return send(res, 401, { ok: false, error: 'bad ticket' });
        log.error('match-result failed:', e.message);
        send(res, 502, { ok: false, error: 'try again' });
      }
    });
  });

  return { server, handleMatchResult };
}

module.exports = { createApp, PROGRESS_KEY };
