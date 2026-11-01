'use strict';
// Account kinds and the life of a guest.
//   guest  = a player who entered with the device only (no Google / Facebook / Apple, no office account): he can lose his progress with his phone.
//   linked = a player with at least one way to come back (a method linked by the app, or a user name and password made by the office).
// The server notes the first and the last time it saw every player. When "guests.purge" is on in config.json, a guest who did not come back for
// `days` (30) days is deleted from PlayFab; `warnDays` (7) days before that he gets a message in his in-game mail.
// The purge is OFF until the owner switches it on, and warnings are sent only when it is on (they would be a lie otherwise).

const fs = require('node:fs');
const path = require('node:path');

const DAY = 86400000;
const METHODS = ['google', 'facebook', 'apple'];

function createAccounts({ cfg = {}, file = null, mail = null, playfab = null, recovery = null, now = () => Date.now(), log = console }) {
  const g = Object.assign({ purge: false, days: 30, warnDays: 7 }, cfg.guests || {});
  let state = { players: {} };            // pf -> { first, last, methods: { google: id }, warned: ts }
  try { if (file) state = Object.assign(state, JSON.parse(fs.readFileSync(file, 'utf8'))); } catch { /* first run */ }
  let timer = null;

  function save() {
    if (!file || timer) return;
    timer = setTimeout(() => { timer = null; flushNow(); }, 1500);
    timer.unref?.();
  }
  function flushNow() {
    if (!file) return;
    try { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file + '.tmp', JSON.stringify(state)); fs.renameSync(file + '.tmp', file); }
    catch (e) { log.error('accounts save failed', e.message); }
  }

  /** Every time the server sees a player (a fresh PlayFab check). The last-seen time moves at most once an hour, so it costs no writes. */
  function touch(pf) {
    const t = now();
    const p = state.players[pf];
    if (!p) { state.players[pf] = { first: t, last: t, methods: {}, warned: 0 }; save(); return; }
    if (t - p.last >= 3600000) { p.last = t; p.warned = 0; save(); }
  }

  const isLinked = pf => {
    const p = state.players[pf];
    return !!((p && Object.keys(p.methods).length) || (recovery && recovery.info(pf)));
  };

  /** Ties a method (google, facebook, apple) to a player. A method can hold one id; another account holding the same id is the caller's business. */
  function link(pf, method, id) {
    if (!METHODS.includes(method)) throw Object.assign(new Error('bad_method'), { code: 'bad_method', status: 400 });
    touch(pf);
    state.players[pf].methods[method] = String(id).slice(0, 80); state.players[pf].warned = 0; save();
  }

  /** Who owns a method id (null = nobody). */
  function owner(method, id) {
    for (const [pf, p] of Object.entries(state.players)) if (p.methods[method] === String(id)) return pf;
    return null;
  }

  /** What the game shows in Settings: guest or not, which methods, and the days left before a guest is deleted. */
  function status(pf) {
    const p = state.players[pf]; const t = now();
    const linked = isLinked(pf);
    const methods = Object.keys((p && p.methods) || {});
    if (recovery && recovery.info(pf)) methods.push('office');
    const daysLeft = !linked && g.purge && p ? Math.max(0, Math.ceil((p.last + g.days * DAY - t) / DAY)) : null;
    return { ok: true, guest: !linked, methods, daysLeft, purge: !!g.purge };
  }

  function guests() { return Object.entries(state.players).filter(([pf]) => !isLinked(pf)); }

  /** The numbers for the admin board. */
  function summary() {
    const t = now(); const list = guests();
    return { total: Object.keys(state.players).length, guests: list.length, atRisk: list.filter(([, p]) => p.last + (g.days - g.warnDays) * DAY <= t).length, purge: !!g.purge, days: g.days };
  }

  /** Warns guests who are `warnDays` from deletion, then deletes the ones past `days`. Returns what it did. Does nothing unless purge is on. */
  async function sweep() {
    const out = { warned: 0, deleted: 0, failed: 0 };
    if (!g.purge) return out;
    const t = now();
    for (const [pf, p] of guests()) {
      if (p.last + g.days * DAY <= t) {
        try { if (playfab && playfab.deletePlayer) await playfab.deletePlayer(pf); delete state.players[pf]; out.deleted++; }
        catch (e) { out.failed++; log.error('guest delete failed', pf, e.message); }
      } else if (!p.warned && p.last + (g.days - g.warnDays) * DAY <= t && mail) {
        try {
          mail.send({ audience: 'players', recipients: [pf], kind: 'note', title: { ar: 'حسابك رح ينحذف بعد ' + g.warnDays + ' أيام', en: 'Your account will be deleted in ' + g.warnDays + ' days' }, body: { ar: 'دخلت كضيف ولسا ما ربطت حسابك. اللي ما بيدخل خلال شهر بنحذف حسابه. اربطه من الإعدادات ← الحساب بـ Google أو Facebook وبتضل محفوظ.', en: 'You joined as a guest and have not linked your account. Accounts that stay away for a month are deleted. Link it in Settings > Account to keep it.' } });
          p.warned = t; out.warned++;
        } catch (e) { log.error('guest warning failed', pf, e.message); }
      }
    }
    if (out.warned || out.deleted) save();
    return out;
  }

  /** When the server first saw this player (ms), or 0. */
  const firstSeen = pf => (state.players[pf] && state.players[pf].first) || 0;

  return { touch, link, owner, status, summary, sweep, isLinked, firstSeen, flushNow, METHODS };
}

module.exports = { createAccounts };
