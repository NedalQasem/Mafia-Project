'use strict';
// Small JSON file database: which matches were reported, who was paid, and how many matches each player was paid today.
// Written atomically (temp file + rename) so a crash never leaves half a file. Plenty for thousands of matches a day.

const fs = require('node:fs');
const path = require('node:path');

function createStore(file) {
  let data = { matches: {}, daily: {} };
  try { data = JSON.parse(fs.readFileSync(file, 'utf8')); } catch { /* first run */ }
  data.matches = data.matches || {}; data.daily = data.daily || {};
  let timer = null;

  function flushNow() {
    if (!file) return;
    fs.mkdirSync(path.dirname(file), { recursive: true });
    const tmp = file + '.tmp';
    fs.writeFileSync(tmp, JSON.stringify(data));
    fs.renameSync(tmp, file);
  }
  function save() { if (!file || timer) return; timer = setTimeout(() => { timer = null; try { flushNow(); } catch (e) { console.error('store write failed', e.message); } }, 300); timer.unref?.(); }

  return {
    match(id) { return data.matches[id]; },
    putMatch(id, m) { data.matches[id] = m; save(); },
    dailyCount(playFabId, day) { return (data.daily[day] && data.daily[day][playFabId]) || 0; },
    addDaily(playFabId, day) { (data.daily[day] = data.daily[day] || {})[playFabId] = this.dailyCount(playFabId, day) + 1; save(); },
    /** Forgets old matches and days so the file stays small. */
    purge(nowMs, keepMs, today) {
      for (const [id, m] of Object.entries(data.matches)) if (nowMs - m.firstSeen > keepMs) delete data.matches[id];
      for (const day of Object.keys(data.daily)) if (day < today) delete data.daily[day];
      save();
    },
    flushNow,
    snapshot() { return data; },
  };
}

module.exports = { createStore };
