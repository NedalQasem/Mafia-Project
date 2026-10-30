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

function createEvents({ cfg = {}, source = null, playfab = null, now = () => Date.now(), log = console, file = null }) {
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
  function record(pf, { won }) {
    const t = now();
    for (const e of items()) {
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
        claimed: !!mine.c, claimable: false, secret: false, endsInSec: 0, startsInSec: 0,
      };
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

  return { board, record, claim, coinsMultiplier, flushNow, purge: () => { const l = source ? source() : cfg.events; const ids = new Set((Array.isArray(l) ? l : []).map(e => e && e.id)); for (const k of Object.keys(state)) if (!ids.has(k)) delete state[k]; save(); } };
}

module.exports = { createEvents };
