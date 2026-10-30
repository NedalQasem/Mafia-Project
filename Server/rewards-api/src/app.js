'use strict';
// The HTTP service. POST /v1/match-result is idempotent: a player's device sends the finished match, and sends it again to
// see whether the reward arrived (it answers "waiting" until more than half of the players reported the same match).

const http = require('node:http');
const crypto = require('node:crypto');
const R = require('./rewards');
const { PlayFabError } = require('./playfab');

const PROGRESS_KEY = 'Progress';

function createApp({ cfg, playfab, store, chat = null, friends = null, leaderboard = null, offers = null, events = null, mail = null, admin = null, now = () => Date.now(), log = console }) {
  const buckets = new Map();   // ip -> { count, resetAt }
  const locks = new Map();     // playFabId -> promise chain (one update per player at a time)
  const tickets = new Map();   // ticket -> { pf, until }: PlayFab is asked about a ticket at most once every few minutes

  async function who(ticket) {
    const t = now();
    const hit = tickets.get(ticket);
    if (hit && hit.until > t) return hit.pf;
    const pf = await playfab.authenticate(ticket);
    tickets.set(ticket, { pf, until: t + 5 * 60 * 1000 });
    if (tickets.size > 5000) for (const [k, v] of tickets) if (v.until < t) tickets.delete(k);
    return pf;
  }

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
      if (leaderboard) leaderboard.record(playFabId, { won: reward.won, role: rep.myRole, survived: rep.survived, durationSec: rep.durationSec, rankPointsGained: reward.rankPoints, rankPointsNow: progress.rankPoints });
      if (events) events.record(playFabId, { won: reward.won });
      store.addDaily(playFabId, today());
      if (admin) admin.noteMatch(match.id);
      store.putMatch(match.id, match);
    }
    // a running bonus event multiplies the coins (only the coins; xp and rank stay the same)
    const coins = events ? Math.round(reward.coins * events.coinsMultiplier()) : reward.coins;
    if (!g.coins) {
      if (coins > 0) await playfab.addCurrency(playFabId, cfg.currencyCode, coins);
      g.coins = true;
      store.putMatch(match.id, match);
    }
    return { granted: true, xp: reward.xp, coins, rankPoints: reward.rankPoints, won: reward.won };
  }

  async function handleMatchResult(ticket, body) {
    const playFabId = await who(ticket);
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

  function readJson(req, res) {
    return new Promise(resolve => {
      let size = 0; const chunks = [];
      req.on('data', c => { size += c.length; if (size > 8192) { send(res, 413, { ok: false, error: 'too big' }); req.destroy(); resolve(undefined); } else chunks.push(c); });
      req.on('end', () => {
        if (res.writableEnded) return resolve(undefined);
        if (size === 0) return resolve({});
        try { resolve(JSON.parse(Buffer.concat(chunks).toString('utf8'))); } catch { send(res, 400, { ok: false, error: 'bad json' }); resolve(undefined); }
      });
    });
  }

  function fail(res, e, what) {
    if (e instanceof PlayFabError && (e.status === 401 || e.status === 403 || e.code === 'InvalidSessionTicket' || e.code === 'NoUser')) return send(res, 401, { ok: false, error: 'bad ticket' });
    if (e && e.code && e.status && !(e instanceof PlayFabError)) return send(res, e.status, Object.assign({ ok: false, error: e.code }, e.extra || {}));
    log.error(what + ' failed:', e && e.message);
    send(res, 502, { ok: false, error: 'try again' });
  }

  const chatPost = {
    '/v1/chat/send': (pf, b) => chat.send(pf, b),
    '/v1/chat/read': async (pf, b) => chat.markRead(pf, b.channel, b.upTo),
    '/v1/chat/group/create': (pf, b) => chat.createGroup(pf, b),
    '/v1/chat/group/leave': (pf, b) => chat.leaveGroup(pf, b.id),
    '/v1/chat/report': async (pf, b) => chat.report(pf, b),
    '/v1/chat/block': async (pf, b) => chat.block(pf, b.player, !!b.on),
  };

  const friendsPost = friends ? {
    '/v1/friends/lookup': (pf, b) => friends.lookup(pf, b),
    '/v1/friends/request': (pf, b) => friends.request(pf, b),
    '/v1/friends/respond': (pf, b) => friends.respond(pf, b),
    '/v1/friends/cancel': async (pf, b) => friends.cancel(pf, b),
    '/v1/friends/remove': async (pf, b) => friends.remove(pf, b),
    '/v1/friends/invite': (pf, b) => friends.invite(pf, b),
    '/v1/presence': async (pf, b) => friends.setPresence(pf, b),
    '/v1/report/player': async (pf, b) => friends.reportPlayer(pf, b),
  } : {};
  const boardPost = leaderboard ? {
    '/v1/leaderboard': (pf, b) => leaderboard.board(pf, b),
    '/v1/wall': async () => leaderboard.wall(),
  } : {};
  const offersPost = offers ? { '/v1/offers': async () => ({ ok: true, offers: offers.active() }) } : {};
  const eventsPost = events ? {
    '/v1/events': async pf => events.board(pf),
    '/v1/events/claim': (pf, b) => withLock('pf:' + pf, () => events.claim(pf, b)),
  } : {};
  const mailPost = mail ? {
    '/v1/mail': async pf => mail.list(pf),
    '/v1/mail/claim': (pf, b) => withLock('pf:' + pf, () => mail.claim(pf, String(b.id || ''))),
    '/v1/mail/claim-all': pf => withLock('pf:' + pf, () => mail.claimAll(pf)),
  } : {};
  const posts = Object.assign({}, chat ? chatPost : {}, friendsPost, boardPost, offersPost, eventsPost, mailPost);

  const server = http.createServer((req, res) => {
    const ip = (cfg.trustProxy && req.headers['x-forwarded-for'] ? String(req.headers['x-forwarded-for']).split(',')[0].trim() : req.socket.remoteAddress) || 'unknown';
    const url = new URL(req.url, 'http://local');
    if (req.method === 'GET' && url.pathname === '/health') return send(res, 200, { ok: true });
    if (limited(ip)) return send(res, 429, { ok: false, error: 'slow down' });
    if (admin && admin.owns(url.pathname)) return admin.handle(req, res, url, ip);      // the admin board (its own login)

    const isResult = req.method === 'POST' && url.pathname === '/v1/match-result';
    const isChatPost = req.method === 'POST' && !!posts[url.pathname];
    const isChatGet = req.method === 'GET' && ((!!chat && (url.pathname === '/v1/chat/poll' || url.pathname === '/v1/chat/inbox' || url.pathname === '/v1/chat/blocked')) || (!!friends && (url.pathname === '/v1/friends' || url.pathname === '/v1/me')));
    if (!isResult && !isChatPost && !isChatGet) return send(res, 404, { ok: false, error: 'not found' });

    const ticket = req.headers['x-authorization'];
    if (typeof ticket !== 'string' || ticket.length < 10 || ticket.length > 2000) return send(res, 401, { ok: false, error: 'no ticket' });

    (async () => {
      try {
        if (isChatGet) {
          const pf = await who(ticket);
          if (url.pathname === '/v1/friends') return send(res, 200, await friends.list(pf));
          if (url.pathname === '/v1/me') return send(res, 200, await friends.me(pf));
          if (url.pathname === '/v1/chat/inbox') return send(res, 200, await chat.inbox(pf));
          if (url.pathname === '/v1/chat/blocked') return send(res, 200, chat.blockedList(pf));
          const out = await chat.poll(pf, parseInt(url.searchParams.get('since') || '0', 10), Math.min(parseInt(url.searchParams.get('wait') || '0', 10) * 1000 || 0, 25000), url.searchParams.get('history') === '1');
          return send(res, 200, Object.assign({ ok: true }, out));
        }
        const body = await readJson(req, res);
        if (body === undefined) return;
        if (isChatPost) {
          const pf = await who(ticket);
          return send(res, 200, await posts[url.pathname](pf, body || {}));
        }
        const out = await handleMatchResult(ticket, body);
        send(res, out.status, out.body);
      } catch (e) { fail(res, e, url.pathname); }
    })();
  });

  return { server, handleMatchResult };
}

module.exports = { createApp, PROGRESS_KEY };
