'use strict';
// Leaderboards. The server writes the numbers itself while it pays a finished match (so a client can never post a score):
// per day and per week for eight categories, plus the all-time figures behind the hall of fame.

const fs = require('node:fs');
const path = require('node:path');
const { ChatError } = require('./chat');

// the order is the order of the chips in the game
const CATS = ['points', 'wins', 'mafiaWins', 'sheriffWins', 'secs', 'games', 'survived', 'streak'];
const DAY_MS = 24 * 3600 * 1000;

function createLeaderboard({ chat, cfg = {}, now = () => Date.now(), log = console, file = null }) {
  const c = Object.assign({ tzHours: 3, weekStartDay: 6, topSize: 12, keepDays: 9, keepWeeks: 6 }, cfg.leaderboard || {});
  let state = { days: {}, weeks: {}, streak: {}, all: {} };
  try { if (file) state = Object.assign(state, JSON.parse(fs.readFileSync(file, 'utf8'))); } catch { /* first run */ }
  let timer = null;
  let prizes = null;                // the prizes of the boards (lbprizes.js), set after both exist

  function save() {
    if (!file || timer) return;
    timer = setTimeout(() => { timer = null; flushNow(); }, 1000);
    timer.unref?.();
  }
  function flushNow() {
    if (!file) return;
    try { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file + '.tmp', JSON.stringify(state)); fs.renameSync(file + '.tmp', file); }
    catch (e) { log.error('leaderboard save failed', e.message); }
  }

  // ---------------------------------------------------------------- periods (local midnight; the week starts on weekStartDay, 0 = Sunday)

  function local(t = now()) { return new Date(t + c.tzHours * 3600 * 1000); }
  function dayKey(t = now()) { return local(t).toISOString().slice(0, 10); }
  function weekKey(t = now()) {
    const l = local(t);
    const back = (l.getUTCDay() - c.weekStartDay + 7) % 7;
    return dayKey(t - back * DAY_MS);
  }
  function secondsToReset(period, t = now()) {
    const l = local(t);
    const intoDay = l.getUTCHours() * 3600 + l.getUTCMinutes() * 60 + l.getUTCSeconds();
    const toMidnight = 86400 - intoDay;
    if (period === 'day') return toMidnight;
    const back = (l.getUTCDay() - c.weekStartDay + 7) % 7;
    return (6 - back) * 86400 + toMidnight;
  }

  function blank() { return { points: 0, wins: 0, mafiaWins: 0, sheriffWins: 0, secs: 0, games: 0, survived: 0, streak: 0 }; }
  function slot(table, key, pf) {
    const t = (table[key] = table[key] || {});
    return (t[pf] = t[pf] || blank());
  }

  // ---------------------------------------------------------------- writing (called once per player and match, when he is paid)

  function record(pf, { won, role, survived, durationSec, rankPointsGained, rankPointsNow }) {
    const s = (state.streak[pf] = state.streak[pf] || { cur: 0, best: 0 });
    if (won) { s.cur += 1; if (s.cur > s.best) s.best = s.cur; } else s.cur = 0;
    const all = (state.all[pf] = state.all[pf] || { points: 0, wins: 0, mafiaWins: 0, sheriffWins: 0, secs: 0, games: 0, survived: 0, bestStreak: 0 });
    all.points = rankPointsNow;
    all.games += 1; all.secs += durationSec; all.bestStreak = s.best;
    if (survived) all.survived += 1;
    if (won) { all.wins += 1; if (role === 'Mafia') all.mafiaWins += 1; if (role === 'Sheriff') all.sheriffWins += 1; }
    for (const e of [slot(state.days, dayKey(), pf), slot(state.weeks, weekKey(), pf)]) {
      e.points += rankPointsGained; e.games += 1; e.secs += durationSec;
      if (survived) e.survived += 1;
      if (won) { e.wins += 1; if (role === 'Mafia') e.mafiaWins += 1; if (role === 'Sheriff') e.sheriffWins += 1; }
      if (s.cur > e.streak) e.streak = s.cur;
    }
    save();
  }

  // ---------------------------------------------------------------- reading

  // key: a finished day or week (its dayKey / weekKey); without it the current one
  function table(period, key) {
    if (period === 'day') return state.days[key || dayKey()] || {};
    if (period === 'week') return state.weeks[key || weekKey()] || {};
    throw new ChatError('bad_period');
  }

  /** What the game shows for a category: minutes instead of seconds, never below zero. */
  function shown(cat, e) {
    const v = e ? e[CATS[cat]] : 0;
    return Math.max(0, cat === 4 ? Math.floor(v / 60) : v);
  }

  function ranked(period, cat, key) {
    const t = table(period, key);
    const list = [];
    for (const [pf, e] of Object.entries(t)) { const v = shown(cat, e); if (v > 0) list.push({ pf, value: v, games: e.games }); }
    list.sort((a, b) => b.value - a.value || a.games - b.games || (a.pf < b.pf ? -1 : 1));
    return list;
  }

  async function board(pf, { period, cat }) {
    cat = parseInt(cat, 10);
    if (!(cat >= 0 && cat < CATS.length)) throw new ChatError('bad_category');
    const all = ranked(period, cat);
    const top = [];
    for (const row of all.slice(0, c.topSize)) {
      const p = await chat.who(row.pf);
      top.push({ pf: row.pf, name: p.name, rank: p.rank, level: p.level, value: row.value, me: row.pf === pf });
    }
    const mine = all.findIndex(r => r.pf === pf);
    const tenth = all.length >= 10 ? all[9].value : 0;
    const myValue = mine >= 0 ? all[mine].value : 0;
    const p = await chat.who(pf);
    return {
      ok: true, period, cat, resetIn: secondsToReset(period), top, prizes: prizes ? prizes.forBoard(period, cat) : [],
      me: { name: p.name, rank: p.rank, level: p.level, value: myValue, position: mine >= 0 ? mine + 1 : 0, tenth, need: mine >= 0 && mine < 10 ? 0 : Math.max(1, tenth - myValue + 1) },
    };
  }

  /** The hall of fame: the current godfather (most rank points) and the records of all time. */
  async function wall() {
    let god = null;
    for (const [pf, a] of Object.entries(state.all)) if (a.points > 0 && (!god || a.points > god.points)) god = { pf, points: a.points };
    if (god) { const p = await chat.who(god.pf); god = { pf: god.pf, name: p.name, rank: p.rank, points: god.points }; }
    const best = async (pick, unit = v => v) => {
      let top = null;
      for (const [pf, a] of Object.entries(state.all)) { const v = pick(a); if (v > 0 && (!top || v > top.value)) top = { pf, value: v }; }
      if (!top) return null;
      const p = await chat.who(top.pf);
      return { name: p.name, value: unit(top.value) };
    };
    return {
      ok: true, godfather: god, seasons: [],
      records: {
        streak: await best(a => a.bestStreak),
        games: await best(a => a.games),
        mafiaWins: await best(a => a.mafiaWins),
        sheriffWins: await best(a => a.sheriffWins),
        survived: await best(a => a.survived),
        hours: await best(a => a.secs, v => Math.floor(v / 3600)),
      },
    };
  }

  function purge() {
    const days = Object.keys(state.days).sort();
    while (days.length > c.keepDays) delete state.days[days.shift()];
    const weeks = Object.keys(state.weeks).sort();
    while (weeks.length > c.keepWeeks) delete state.weeks[weeks.shift()];
    save();
  }

  /** All-time numbers of one player (null when he never finished a paid match) and how many players have any. */
  function playerStats(pf) { return state.all[pf] ? Object.assign({}, state.all[pf]) : null; }
  function playerCount() { return Object.keys(state.all).length; }
  /** Players who finished a paid match in the last N days (by the day tables). */
  function activePlayers(days) {
    const out = new Set();
    for (let i = 0; i < days; i++) { const t = state.days[dayKey(now() - i * DAY_MS)]; if (t) for (const pf of Object.keys(t)) out.add(pf); }
    return [...out];
  }
  /** The ranking of a finished day or week in one category (for its prizes): [{ pf, value }] best first, only players above zero. */
  function rankedAt(period, key, cat) { return ranked(period, cat, key).map(r => ({ pf: r.pf, value: r.value })); }
  function setPrizes(p) { prizes = p; }
  return { record, board, wall, purge, flushNow, playerStats, playerCount, activePlayers, secondsToReset, dayKey, weekKey, rankedAt, setPrizes, CATS, _state: () => state };
}

module.exports = { createLeaderboard, CATS };
