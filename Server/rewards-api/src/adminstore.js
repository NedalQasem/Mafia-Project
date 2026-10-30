'use strict';
// What the admin board (MAFIA ADMIN BOARD) saves: its own list of events and offers (null = "still the ones of config.json"), the extra banned
// words, which reports were handled, a short log of what the admin did, and a count of paid matches per day for the home screen.
// One JSON file next to the other data files, written atomically.

const fs = require('node:fs');
const path = require('node:path');

const KEEP_AUDIT = 200, KEEP_DAYS = 14, KEEP_COUNTED_MS = 3 * 24 * 3600 * 1000;

function createAdminStore({ file = null, now = () => Date.now(), log = console } = {}) {
  let data = { events: null, offers: null, packs: [], words: { exact: [], contains: [] }, handled: {}, audit: [], days: {}, counted: {} };
  try { if (file) data = Object.assign(data, JSON.parse(fs.readFileSync(file, 'utf8'))); } catch { /* first run */ }
  data.words = Object.assign({ exact: [], contains: [] }, data.words);
  let timer = null;

  function flushNow() {
    if (!file) return;
    try { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file + '.tmp', JSON.stringify(data)); fs.renameSync(file + '.tmp', file); }
    catch (e) { log.error('admin store save failed', e.message); }
  }
  function save() {
    if (!file || timer) return;
    timer = setTimeout(() => { timer = null; flushNow(); }, 400);
    timer.unref?.();
  }

  return {
    get events() { return data.events; }, set events(v) { data.events = v; save(); },
    get offers() { return data.offers; }, set offers(v) { data.offers = v; save(); },
    get packs() { return data.packs; }, set packs(v) { data.packs = v; save(); },
    get words() { return data.words; },
    touch: save,
    handled: id => data.handled[id] || null,
    markHandled(id, action) { data.handled[id] = { at: now(), action }; save(); },
    unhandledSince: () => data.handled,
    audit(action, detail = '') {
      data.audit.push({ ts: now(), action, detail: String(detail).slice(0, 160) });
      if (data.audit.length > KEEP_AUDIT) data.audit.splice(0, data.audit.length - KEEP_AUDIT);
      save();
    },
    recentAudit: n => data.audit.slice(-n).reverse(),
    /** One paid match (called for every player paid; only the first call of a match counts). */
    noteMatch(matchId, day) {
      if (data.counted[matchId]) return;
      data.counted[matchId] = now();
      data.days[day] = (data.days[day] || 0) + 1;
      const t = now();
      for (const [id, at] of Object.entries(data.counted)) if (t - at > KEEP_COUNTED_MS) delete data.counted[id];
      const days = Object.keys(data.days).sort();
      while (days.length > KEEP_DAYS) delete data.days[days.shift()];
      save();
    },
    matchesOn: day => data.days[day] || 0,
    flushNow,
  };
}

module.exports = { createAdminStore };
