'use strict';
// The in-game mail ("البريد"): telegrams the admin sends to one player, a list of players, or everybody. A telegram has a title and a text in Arabic
// and English and, usually, a package (coins, gems, store items) the player collects with one tap. The server pays the package itself, once.
//   one / list / active7  -> one telegram per player (state.inbox)
//   all                   -> one broadcast that every player sees (state.broadcasts) with his own claim record
// A claim is safe to repeat: each step of the package (coins, gems, items) is marked when paid, so a failure half-way never pays twice.

const fs = require('node:fs');
const path = require('node:path');
const { ChatError } = require('./chat');

const MAX_INBOX = 100, KEEP_SENT = 100, SHOW_CLAIMED_MS = 7 * 86400000;

function createMail({ cfg = {}, playfab = null, now = () => Date.now(), log = console, file = null }) {
  const gemsCode = cfg.gemsCode || 'GM';
  let state = { nextId: 1, inbox: {}, broadcasts: [], claims: {}, sent: [] };
  try { if (file) state = Object.assign(state, JSON.parse(fs.readFileSync(file, 'utf8'))); } catch { /* first run */ }
  let timer = null;

  function save() {
    if (!file || timer) return;
    timer = setTimeout(() => { timer = null; flushNow(); }, 800);
    timer.unref?.();
  }
  function flushNow() {
    if (!file) return;
    try { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file + '.tmp', JSON.stringify(state)); fs.renameSync(file + '.tmp', file); }
    catch (e) { log.error('mail save failed', e.message); }
  }

  const bi = o => ({ ar: String((o && o.ar) || '').slice(0, 80), en: String((o && o.en) || '').slice(0, 80) });
  const bb = o => ({ ar: String((o && o.ar) || '').slice(0, 200), en: String((o && o.en) || '').slice(0, 200) });
  const packOf = p => ({ coins: Math.max(0, Math.floor(Number(p && p.coins) || 0)), gems: Math.max(0, Math.floor(Number(p && p.gems) || 0)), items: Array.isArray(p && p.items) ? p.items.map(String).slice(0, 10) : [] });
  const hasPack = p => p.coins > 0 || p.gems > 0 || p.items.length > 0;

  /**
   * Sends a telegram. audience: 'all' (a broadcast) or 'players' (recipients = list of PlayFab ids). Returns how many players it reaches ('all': 0 = everybody).
   */
  function send({ audience, recipients = [], title, body, pack, expiresInDays = 7, label = '' }) {
    const t = now(), exp = expiresInDays > 0 ? t + expiresInDays * 86400000 : 0;
    const base = { ts: t, title: bi(title), body: bb(body), pack: packOf(pack), exp, from: 'office' };
    const id = state.nextId++;
    let count = 0;
    if (audience === 'all') {
      state.broadcasts.push(Object.assign({ id: 'b' + id }, base)); count = 0;
    } else {
      for (const pf of recipients) {
        const list = (state.inbox[pf] = state.inbox[pf] || []);
        list.push(Object.assign({ id: 'm' + id + '-' + list.length + '-' + Math.floor(t % 100000), batch: 'b' + id, claimed: 0, paid: {} }, base));
        if (list.length > MAX_INBOX) list.splice(0, list.length - MAX_INBOX);
        count++;
      }
    }
    state.sent.push({ id: 'b' + id, ts: t, title: base.title, audience, count, pack: base.pack, label });
    if (state.sent.length > KEEP_SENT) state.sent.splice(0, state.sent.length - KEEP_SENT);
    save();
    return { id: 'b' + id, count };
  }

  function claimRecord(pf, bc) { const c = (state.claims[pf] = state.claims[pf] || {}); return (c[bc.id] = c[bc.id] || { claimed: 0, paid: {} }); }

  /** Everything the player's mail shows: his telegrams and the broadcasts, newest first, expired ones left out. */
  function list(pf) {
    const t = now(), out = [];
    const view = (m, rec) => ({ id: m.id, ts: m.ts, title: m.title, body: m.body, pack: m.pack, exp: m.exp, claimed: rec.claimed || 0 });
    for (const m of state.inbox[pf] || []) {
      if (m.exp && m.exp < t && !m.claimed) continue;
      if (m.claimed && t - m.claimed > SHOW_CLAIMED_MS) continue;
      out.push(view(m, m));
    }
    for (const b of state.broadcasts) {
      const rec = (state.claims[pf] && state.claims[pf][b.id]) || { claimed: 0 };
      if (b.exp && b.exp < t && !rec.claimed) continue;
      if (rec.claimed && t - rec.claimed > SHOW_CLAIMED_MS) continue;
      out.push(view(b, rec));
    }
    out.sort((a, b) => b.ts - a.ts);
    return { ok: true, unread: out.filter(m => !m.claimed).length, items: out.slice(0, 60) };
  }
  function unread(pf) { return list(pf).unread; }

  async function pay(pf, m, rec) {
    const p = m.pack;
    if (p.coins > 0 && !rec.paid.coins) { if (playfab) await playfab.addCurrency(pf, cfg.currencyCode, p.coins); rec.paid.coins = true; save(); }
    if (p.gems > 0 && !rec.paid.gems) { if (playfab) await playfab.addCurrency(pf, gemsCode, p.gems); rec.paid.gems = true; save(); }
    if (p.items.length && !rec.paid.items) { if (playfab) await playfab.grantItems(pf, p.items); rec.paid.items = true; save(); }
    rec.claimed = now(); save();
    return { coins: p.coins, gems: p.gems, items: p.items };
  }

  /** The player collects one telegram. Call it under his lock. */
  async function claim(pf, id) {
    const t = now();
    let m = (state.inbox[pf] || []).find(x => x.id === id), rec = m;
    if (!m) { m = state.broadcasts.find(x => x.id === id); if (m) rec = claimRecord(pf, m); }
    if (!m) throw new ChatError('not_found', 404);
    if (rec.claimed) throw new ChatError('already_claimed', 409);
    if (m.exp && m.exp < t) throw new ChatError('expired', 410);
    const r = await pay(pf, m, rec);
    return Object.assign({ ok: true, id }, r);
  }

  async function claimAll(pf) {
    const total = { coins: 0, gems: 0, items: [] };
    let n = 0, failed = 0;
    for (const m of list(pf).items) {
      if (m.claimed) continue;
      try { const r = await claim(pf, m.id); total.coins += r.coins; total.gems += r.gems; total.items.push(...r.items); n++; }
      catch (e) { if (!(e instanceof ChatError)) failed++; }
    }
    return { ok: true, claimed: n, failed, coins: total.coins, gems: total.gems, items: total.items };
  }

  /** For the admin board: what was sent, and how many of each broadcast / telegram batch were collected. */
  function sentList() {
    return state.sent.slice().reverse().map(s => {
      let claimed = 0, total = s.count;
      if (s.audience === 'all') { for (const c of Object.values(state.claims)) if (c[s.id] && c[s.id].claimed) claimed++; total = 0; }
      else for (const l of Object.values(state.inbox)) for (const x of l) if (x.batch === s.id && x.claimed) claimed++;
      return Object.assign({}, s, { claimed });
    });
  }
  /** Packages waiting to be collected by one player (for the player card). */
  const pendingFor = pf => list(pf).items.filter(m => !m.claimed).length;

  function purge() {
    const t = now();
    for (const [pf, l] of Object.entries(state.inbox)) {
      state.inbox[pf] = l.filter(m => !(m.claimed && t - m.claimed > SHOW_CLAIMED_MS) && !(m.exp && m.exp < t - 30 * 86400000));
      if (!state.inbox[pf].length) delete state.inbox[pf];
    }
    state.broadcasts = state.broadcasts.filter(b => !(b.exp && b.exp < t - 30 * 86400000));
    save();
  }

  return { send, list, unread, claim, claimAll, sentList, pendingFor, purge, flushNow };
}

module.exports = { createMail };
