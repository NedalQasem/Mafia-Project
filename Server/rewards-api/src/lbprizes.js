'use strict';
// Prizes of the leaderboards (سبورة الليلة = the day, جريدة الأسبوع = the week). When a day or a week is over, the first places of each category that
// has prizes get them as a gift in the mail (the player collects it; nothing goes straight into the balance). Every finished period is paid once:
// the paid periods are kept in a small file. The prizes are set on the admin board; a prize is a package of the board (packId) or plain coins/gems.

const fs = require('node:fs');
const path = require('node:path');
const { ChatError } = require('./chat');
const { CATS } = require('./leaderboard');

const DAY_MS = 24 * 3600 * 1000;
const DEFAULT = {
  day: { points: [{ from: 1, to: 1, coins: 300 }] },
  week: { points: [{ from: 1, to: 1, coins: 5000 }, { from: 2, to: 2, coins: 2500 }, { from: 3, to: 3, coins: 1000 }] },
};
const CAT_AR = { points: 'نقاط الرتبة', wins: 'الانتصارات', mafiaWins: 'انتصارات المافيا', sheriffWins: 'أفضل محقق', secs: 'دقائق اللعب', games: 'الأكثر مباريات', survived: 'الناجي', streak: 'سلسلة الانتصارات' };
const CAT_EN = { points: 'Rank points', wins: 'Wins', mafiaWins: 'Mafia wins', sheriffWins: 'Best sheriff', secs: 'Minutes played', games: 'Most matches', survived: 'Survivor', streak: 'Win streak' };
const PERIOD_AR = { day: 'سبورة الليلة', week: 'جريدة الأسبوع' }, PERIOD_EN = { day: 'Tonight\'s board', week: 'The weekly paper' };
const MAX_RANK = 50, MAX_ROWS = 10, KEEP_PAID = 120;

/** store: the admin store (lbPrizes: the config set on the board, null = the default). packs(): the packages of the board. */
function createLeaderPrizes({ leaderboard, mail, store = null, packs = () => [], cfg = {}, now = () => Date.now(), log = console, file = null }) {
  let state = { paid: {} };          // "day:2026-10-30" -> { at, count }
  try { if (file) state = Object.assign(state, JSON.parse(fs.readFileSync(file, 'utf8'))); } catch { /* first run */ }
  function save() {
    if (!file) return;
    const keys = Object.keys(state.paid).sort();
    while (keys.length > KEEP_PAID) delete state.paid[keys.shift()];
    try { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file + '.tmp', JSON.stringify(state)); fs.renameSync(file + '.tmp', file); }
    catch (e) { log.error('lbprizes save failed', e.message); }
  }

  function config() {
    if (store && store.lbPrizes) return store.lbPrizes;
    return (cfg.leaderboard && cfg.leaderboard.prizes) || DEFAULT;
  }

  /** Checks a config from the admin board: { day: { cat: [rows] }, week: {...} }, rows { from, to, packId } or { from, to, coins, gems }. */
  function clean(raw) {
    const out = { day: {}, week: {} };
    for (const period of ['day', 'week']) {
      const src = raw && raw[period] && typeof raw[period] === 'object' ? raw[period] : {};
      for (const [cat, rows] of Object.entries(src)) {
        if (!CATS.includes(cat)) throw new ChatError('bad_category');
        if (!Array.isArray(rows) || !rows.length) continue;
        if (rows.length > MAX_ROWS) throw new ChatError('too_many_rows');
        const list = [];
        for (const r of rows) {
          const from = Math.floor(Number(r && r.from)), to = Math.floor(Number(r && r.to));
          if (!(from >= 1 && to >= from && to <= MAX_RANK)) throw new ChatError('bad_ranks');
          if (r.packId) {
            if (!packs().some(p => p.id === r.packId)) throw new ChatError('pack_not_found', 404);
            list.push({ from, to, packId: String(r.packId) });
          } else {
            const coins = Math.floor(Number(r.coins) || 0), gems = Math.floor(Number(r.gems) || 0);
            if (!(coins >= 0 && coins <= 1000000 && gems >= 0 && gems <= 10000) || (!coins && !gems)) throw new ChatError('bad_prize');
            list.push({ from, to, coins, gems });
          }
        }
        list.sort((a, b) => a.from - b.from);
        for (let i = 1; i < list.length; i++) if (list[i].from <= list[i - 1].to) throw new ChatError('overlap');
        out[period][cat] = list;
      }
    }
    return out;
  }

  function resolve(row) {
    if (row.packId) {
      const p = packs().find(x => x.id === row.packId);
      return p ? { coins: p.coins || 0, gems: p.gems || 0, items: p.items || [], name: (p.name && (p.name.ar || p.name.en)) || '' } : null;
    }
    return { coins: row.coins || 0, gems: row.gems || 0, items: [], name: '' };
  }

  /** What the game shows above a board (period, category index): [{ from, to, coins, gems, items, name }]. */
  function forBoard(period, catIndex) {
    const rows = ((config()[period] || {})[CATS[catIndex]]) || [];
    return rows.map(r => { const k = resolve(r); return k ? Object.assign({ from: r.from, to: r.to }, k) : null; }).filter(Boolean);
  }

  /** Pays the prizes of one finished period. Returns how many telegrams went out (0 when nobody placed). */
  function payPeriod(period, key) {
    const id = period + ':' + key;
    if (state.paid[id]) return 0;
    let count = 0;
    for (const [cat, rows] of Object.entries(config()[period] || {})) {
      const idx = CATS.indexOf(cat);
      if (idx < 0) continue;
      const ranking = leaderboard.rankedAt(period, key, idx);
      for (const row of rows) {
        const pack = resolve(row);
        if (!pack) continue;
        for (let rank = row.from; rank <= row.to; rank++) {
          const winner = ranking[rank - 1];
          if (!winner) break;
          mail.send({
            audience: 'players', recipients: [winner.pf], expiresInDays: 14, label: 'lb:' + id + ':' + cat,
            title: { ar: 'جائزة ' + PERIOD_AR[period] + ' · المركز ' + rank, en: PERIOD_EN[period] + ' prize · rank ' + rank },
            body: { ar: CAT_AR[cat] + ' · ' + key + '. مبروك، استلم جائزتك.', en: CAT_EN[cat] + ' · ' + key + '. Well done, collect your prize.' },
            pack,
          });
          count++;
        }
      }
    }
    state.paid[id] = { at: now(), count };
    save();
    return count;
  }

  /** Called every minute: pays yesterday and last week once they are over (only the last one: an old period is never paid late). */
  function payDue() {
    const t = now();
    const out = [];
    for (const [period, key] of [['day', leaderboard.dayKey(t - DAY_MS)], ['week', leaderboard.weekKey(t - 7 * DAY_MS)]]) {
      if (state.paid[period + ':' + key]) continue;
      try { out.push({ period, key, count: payPeriod(period, key) }); } catch (e) { log.error('lb prizes failed', period, key, e.message); }
    }
    return out;
  }

  /** For the admin board: the config, what each row gives, and the last paid periods. */
  function admin() {
    const conf = config();
    const shown = {};
    for (const period of ['day', 'week']) {
      shown[period] = {};
      for (const [cat, rows] of Object.entries(conf[period] || {})) shown[period][cat] = rows.map(r => Object.assign({}, r, { gives: resolve(r) }));
    }
    const history = Object.entries(state.paid).sort((a, b) => b[1].at - a[1].at).slice(0, 20).map(([id, v]) => ({ id, at: v.at, count: v.count }));
    return { ok: true, config: shown, isDefault: !(store && store.lbPrizes), cats: CATS.map(c => ({ id: c, ar: CAT_AR[c] })), history };
  }

  return { config, clean, forBoard, payPeriod, payDue, admin, DEFAULT };
}

module.exports = { createLeaderPrizes, DEFAULT_LB_PRIZES: DEFAULT };
