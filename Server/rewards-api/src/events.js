'use strict';
// The events board ("أخبار الحارة") of the home screen. They live in config.json (section "events"), so the owner starts a new event, changes
// its texts or ends it without a new version of the game: edit the file, then  docker restart mafia-rewards.
//   kind "bonus"     - the coins of every match are multiplied while it runs ("coinsMultiplier", the server applies it when it pays a match)
//   kind "challenge" - win ("metric": "wins") or play ("games") "goal" matches inside the window, then claim "rewardCoins" once
//   kind "info"      - only an announcement (a tournament night, a new map); it may hide its reward with "secret": true until it starts
// "from" / "until" are ISO dates. Counting is done here, from the matches the server itself paid, so a client can never write progress.

const fs = require('node:fs');
const path = require('node:path');
const { ChatError } = require('./chat');

const KINDS = ['bonus', 'challenge', 'info'];
const MAX = { live: 6, soon: 3, done: 3 };
const MAX_MULTIPLIER = 5, MAX_GOAL = 1000, MAX_REWARD = 10000;
const METRICS = ['wins', 'games', 'points', 'secs'];
const DIST = '@dist';      // state key of the prize distributions (an event id can never start with @)

function createEvents({ cfg = {}, source = null, playfab = null, mail = null, packs = () => [], nameOf = null, now = () => Date.now(), log = console, file = null }) {
  const c = Object.assign({ archiveDays: 14, claimGraceDays: 3 }, cfg.eventsSettings || {});
  // the events come from the admin board when it has its own list (source), otherwise from config.json; read on every call so an edit applies at once
  const items = () => { const l = source ? source() : cfg.events; return (Array.isArray(l) ? l : []).filter(e => e && typeof e.id === 'string' && e.id && e.enabled !== false && KINDS.includes(e.kind)); };
  let state = {};   // eventId -> playFabId -> { p: progress, c: claimed }
  try { if (file) state = JSON.parse(fs.readFileSync(file, 'utf8')); } catch { /* first run */ }
  let timer = null;

  function save() {
    if (!file || timer) return;
    timer = setTimeout(() => { timer = null; flushNow(); }, 1000);
    timer.unref?.();
  }
  function flushNow() {
    if (!file) return;
    try { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file + '.tmp', JSON.stringify(state)); fs.renameSync(file + '.tmp', file); }
    catch (e) { log.error('events save failed', e.message); }
  }

  const text = (v, max) => (typeof v === 'string' ? v.slice(0, max) : '');
  const num = (v, max, def = 0) => (Number.isFinite(v) ? Math.max(0, Math.min(max, Math.floor(v))) : def);
  const when = v => { const t = v ? Date.parse(v) : NaN; return Number.isFinite(t) ? t : 0; };
  function slot(id, pf) {
    const t = (state[id] = state[id] || {});
    return (t[pf] = t[pf] || { p: 0, c: false });
  }

  // ---------------------------------------------------------------- ranking inside an event (any kind of event can have one)

  function rankCfg(e) {
    const r = e && e.ranking;
    if (!r || r.enabled !== true) return null;
    return { metric: METRICS.includes(r.metric) ? r.metric : 'wins', winners: Math.max(1, Math.min(20, Math.floor(Number(r.winners)) || 5)), prizes: Array.isArray(r.prizes) ? r.prizes : [], distribution: r.distribution === 'manual' ? 'manual' : 'auto' };
  }
  const distOf = id => (state[DIST] && state[DIST][id]) || null;
  /** Players of an event by their value (most first; the one who got there first wins a tie). Players with nothing are not ranked. */
  function ranked(e) {
    const rc = rankCfg(e);
    if (!rc) return [];
    const out = [];
    for (const [pf, s] of Object.entries(state[e.id] || {})) {
      const v = s.r ? s.r[rc.metric] || 0 : 0;
      if (v > 0) out.push({ pf, value: v, t: s.t || 0 });
    }
    out.sort((a, b) => b.value - a.value || a.t - b.t);
    return out;
  }
  function placeOf(list, pf) { const i = list.findIndex(x => x.pf === pf); return i < 0 ? null : { rank: i + 1, value: list[i].value }; }
  function prizeFor(rc, rank) { return rc.prizes.find(p => rank >= p.from && rank <= p.to) || null; }
  function packOf(id) { const p = (packs() || []).find(x => x.id === id); return p ? { coins: p.coins || 0, gems: p.gems || 0, items: p.items || [], name: p.name || {} } : null; }

  function span(e) { return { from: when(e.from), until: when(e.until) }; }
  function running(e, t) { const s = span(e); return (!s.from || s.from <= t) && (!s.until || s.until > t); }
  function goalOf(e) { return e.kind === 'challenge' ? Math.max(1, num(e.goal, MAX_GOAL, 1)) : 0; }
  function rewardOf(e) { return e.kind === 'challenge' ? num(e.rewardCoins, MAX_REWARD) : 0; }

  /** The multiplier the coins of a match get right now: the best one among the bonus events that are running. */
  function coinsMultiplier() {
    const t = now();
    let best = 1;
    for (const e of items()) if (e.kind === 'bonus' && running(e, t)) best = Math.max(best, Math.min(MAX_MULTIPLIER, Number(e.coinsMultiplier) || 1));
    return best;
  }

  /** Called once per player and match when he is paid: moves the challenges that are running. */
  function record(pf, { won, points = 0, secs = 0 }) {
    const t = now();
    for (const e of items()) {
      const rc = rankCfg(e);
      if (rc && running(e, t) && !distOf(e.id)) {      // the ranking stops counting once its prizes were given
        const s = slot(e.id, pf); s.r = s.r || { wins: 0, games: 0, points: 0, secs: 0 };
        s.r.games += 1; if (won) s.r.wins += 1; s.r.points += Math.floor(Number(points) || 0); s.r.secs += Math.floor(Number(secs) || 0); s.t = t;
        save();
      }
      if (e.kind !== 'challenge' || !running(e, t)) continue;
      const metric = e.metric === 'games' ? 'games' : 'wins';
      if (metric === 'wins' && !won) continue;
      const s = slot(e.id, pf);
      s.p = Math.min(goalOf(e), s.p + 1);
      save();
    }
  }

  /** Everything the board shows to one player. */
  function board(pf) {
    const t = now();
    const live = [], soon = [], done = [];
    for (const e of items()) {
      const s = span(e), g = goalOf(e), mine = (state[e.id] && state[e.id][pf]) || { p: 0, c: false };
      const claimable = g > 0 && mine.p >= g && !mine.c && rewardOf(e) > 0;
      const out = {
        id: e.id, kind: e.kind, kicker: text(e.kicker, 24), title: text(e.title, 60), body: text(e.body, 160), metric: e.metric === 'games' ? 'games' : 'wins',
        multiplier: e.kind === 'bonus' ? Math.min(MAX_MULTIPLIER, Number(e.coinsMultiplier) || 1) : 0, goal: g, progress: Math.min(g, mine.p), rewardCoins: rewardOf(e),
        claimed: !!mine.c, claimable: false, secret: false, endsInSec: 0, startsInSec: 0, ranking: null,
      };
      const rc = rankCfg(e);
      if (rc) out.ranking = { metric: rc.metric, winners: rc.winners, distributed: !!distOf(e.id), mine: placeOf(ranked(e), pf) };
      if (running(e, t)) {
        out.endsInSec = s.until ? Math.max(0, Math.floor((s.until - t) / 1000)) : 0;
        out.claimable = claimable;
        live.push(out);
      } else if (s.from && s.from > t) {
        out.startsInSec = Math.floor((s.from - t) / 1000);
        if (e.secret === true) { out.secret = true; out.rewardCoins = 0; out.multiplier = 0; out.goal = 0; }
        soon.push(out);
      } else if (s.until && s.until <= t) {
        const age = t - s.until;
        if (claimable && age < c.claimGraceDays * 86400000) { out.claimable = true; live.push(out); }   // finished in time: he may still collect
        else if (age < c.archiveDays * 86400000) done.push(out);
      }
    }
    const byEnd = (a, b) => (a.endsInSec || 1e12) - (b.endsInSec || 1e12);
    live.sort(byEnd); soon.sort((a, b) => a.startsInSec - b.startsInSec);
    return { ok: true, live: live.slice(0, MAX.live), soon: soon.slice(0, MAX.soon), done: done.slice(0, MAX.done) };
  }

  function prizeList(rc) {
    return rc.prizes.map(p => { const k = packOf(p.packId); return k ? { from: p.from, to: p.to, coins: k.coins, gems: k.gems, items: k.items, name: k.name } : null; }).filter(Boolean);
  }

  /** The ranking of one event for a player: the top ten, his own place, the prizes. */
  async function ranking(pf, { id } = {}) {
    const e = items().find(x => x.id === id), rc = rankCfg(e);
    if (!rc) throw new ChatError('no_ranking', 404);
    const list = ranked(e), top = list.slice(0, 10), s = span(e), t = now();
    const rows = [];
    for (let i = 0; i < top.length; i++) rows.push({ rank: i + 1, name: nameOf ? await nameOf(top[i].pf) : 'لاعب', value: top[i].value, me: top[i].pf === pf });
    const mine = placeOf(list, pf);
    return { ok: true, id: e.id, metric: rc.metric, winners: rc.winners, rows, mine, prizes: prizeList(rc), endsInSec: s.until && s.until > t ? Math.floor((s.until - t) / 1000) : 0, distributed: !!distOf(e.id) };
  }

  /** For the admin board: the top fifty with names and the prize each one gets. */
  async function adminRanking(id) {
    const all = items(); const e = all.find(x => x.id === id), rc = rankCfg(e);
    if (!rc) throw new ChatError('no_ranking', 404);
    const list = ranked(e).slice(0, 50), rows = [];
    for (let i = 0; i < list.length; i++) {
      const pz = i + 1 <= rc.winners ? prizeFor(rc, i + 1) : null, k = pz ? packOf(pz.packId) : null;
      rows.push({ rank: i + 1, pf: list[i].pf, name: nameOf ? await nameOf(list[i].pf) : list[i].pf, value: list[i].value, prize: k });
    }
    const s = span(e), t = now(), d = distOf(e.id);
    return { ok: true, id: e.id, title: e.title, metric: rc.metric, winners: rc.winners, distribution: rc.distribution, rows, endsInSec: s.until && s.until > t ? Math.floor((s.until - t) / 1000) : 0, distributed: d ? { at: d.at, by: d.by, count: d.list.length } : null };
  }

  /** Gives the prizes: one telegram with the pack to each winner, once for ever. */
  async function distribute(id, { by = 'admin' } = {}) {
    const e = items().find(x => x.id === id), rc = rankCfg(e);
    if (!rc) throw new ChatError('no_ranking', 404);
    if (distOf(id)) throw new ChatError('already_distributed', 409);
    if (!mail) throw new ChatError('no_mail', 404);
    const list = ranked(e).slice(0, rc.winners), sent = [];
    for (let i = 0; i < list.length; i++) {
      const rank = i + 1, pz = prizeFor(rc, rank), k = pz ? packOf(pz.packId) : null;
      if (!k) continue;
      mail.send({
        audience: 'players', recipients: [list[i].pf], expiresInDays: 30, label: 'ranking:' + e.id,
        title: { ar: 'جائزة المركز ' + rank + ' · ' + text(e.title, 40), en: 'Rank ' + rank + ' prize · ' + text(e.titleEn || e.title, 40) },
        body: { ar: 'مبروك! جبت المركز ' + rank + ' بتصنيف الحدث.', en: 'Congratulations! You finished in place ' + rank + ' of the event ranking.' },
        pack: { coins: k.coins, gems: k.gems, items: k.items },
      });
      sent.push({ pf: list[i].pf, rank, value: list[i].value });
    }
    (state[DIST] = state[DIST] || {})[id] = { at: now(), by, list: sent };
    save();
    return { ok: true, count: sent.length, list: sent };
  }

  let distributing = false;
  /** Called every minute: gives the prizes of the events that ended and are set to automatic. */
  async function autoDistribute() {
    if (distributing) return;
    distributing = true;
    try {
      const t = now();
      for (const e of items()) {
        const rc = rankCfg(e), s = span(e);
        if (rc && rc.distribution === 'auto' && s.until && s.until <= t && !distOf(e.id)) { try { await distribute(e.id, { by: 'auto' }); } catch (err) { log.error('auto distribute failed', e.id, err.message); } }
      }
    } finally { distributing = false; }
  }

  /** The player collects the reward of a finished challenge. Marked first, so two taps can never pay twice. */
  async function claim(pf, { id } = {}) {
    const e = items().find(x => x.id === id);
    const g = e ? goalOf(e) : 0;
    if (!e || e.kind !== 'challenge') throw new ChatError('unknown event', 404);
    const t = now(), s = span(e);
    if (s.from && s.from > t) throw new ChatError('not started', 409);
    if (s.until && s.until + c.claimGraceDays * 86400000 <= t) throw new ChatError('too late', 410);
    const mine = slot(e.id, pf), coins = rewardOf(e);
    if (mine.p < g || coins <= 0) throw new ChatError('goal not reached', 409);
    if (mine.c) throw new ChatError('already claimed', 409);
    mine.c = true; save();
    try { if (playfab) await playfab.addCurrency(pf, cfg.currencyCode, coins); }
    catch (err) { mine.c = false; save(); throw err; }
    return { ok: true, coins };
  }

  return { board, record, claim, ranking, adminRanking, distribute, autoDistribute, coinsMultiplier, flushNow, purge: () => { const l = source ? source() : cfg.events; const ids = new Set((Array.isArray(l) ? l : []).map(e => e && e.id)); for (const k of Object.keys(state)) if (k !== DIST && !ids.has(k)) delete state[k]; save(); } };
}

module.exports = { createEvents };
