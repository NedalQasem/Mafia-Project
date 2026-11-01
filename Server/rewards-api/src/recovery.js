'use strict';
// Account recovery: a player who lost his device (the game signs in with the device) gets back his old account with a user name and a password the
// admin made for him. The server checks them, then asks PlayFab for a session of the OLD account (a "server custom id" is linked to it for this);
// the game uses that session to link the new device to the old account, so the next normal sign-ins land there.
// Passwords are kept as scrypt hashes. Wrong tries are limited per user name (five lock it for 15 minutes) and per address.

const { hashPassword, verifyPassword } = require('./admin');
const { ChatError } = require('./chat');

const NAME_RE = /^[a-z0-9._-]{4,24}$/;
const MAX_FAILS = 5, LOCK_MS = 15 * 60 * 1000, IP_FAILS = 20, IP_WINDOW_MS = 60 * 60 * 1000;
const customId = pf => 'rec-' + pf;

/** store: { recovery: { [pf]: { username, hash, at, last, count } }, touch() } (the admin store). */
function createRecovery({ store, playfab, now = () => Date.now(), log = console }) {
  const fails = new Map();      // username -> { n, until }
  const ipFails = new Map();    // ip -> { n, resetAt }
  const records = () => store.recovery;
  const byName = name => Object.entries(records()).find(([, r]) => r.username === name) || null;

  function generatePassword() {
    const A = 'abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789', crypto = require('node:crypto');
    let s = ''; for (let i = 0; i < 12; i++) s += A[crypto.randomInt(A.length)];
    return s.slice(0, 4) + '-' + s.slice(4, 8) + '-' + s.slice(8);
  }

  /** The admin makes (or replaces) the user name and password of one player. Returns the password: it is shown once and never stored. */
  async function set(pf, usernameRaw, passwordRaw) {
    const username = String(usernameRaw || '').trim().toLowerCase();
    if (!NAME_RE.test(username)) throw new ChatError('bad_username', 400);
    const password = passwordRaw ? String(passwordRaw) : generatePassword();
    if (password.length < 8 || password.length > 64) throw new ChatError('bad_password', 400);
    const taken = byName(username);
    if (taken && taken[0] !== pf) throw new ChatError('username_taken', 409);
    const prev = records()[pf];
    if (!prev && playfab) { try { await playfab.linkServerCustomId(pf, customId(pf)); } catch (e) { throw new ChatError('playfab_failed', 409); } }
    records()[pf] = { username, hash: hashPassword(password), at: now(), last: prev ? prev.last : 0, count: prev ? prev.count : 0 };
    fails.delete(username); store.touch();
    return { username, password };
  }

  async function remove(pf) {
    if (!records()[pf]) throw new ChatError('not_found', 404);
    delete records()[pf]; store.touch();
    if (playfab && playfab.unlinkServerCustomId) { try { await playfab.unlinkServerCustomId(pf, customId(pf)); } catch (e) { log.error('unlink failed', e.message); } }
  }

  const info = pf => { const r = records()[pf]; return r ? { username: r.username, at: r.at, last: r.last, count: r.count } : null; };

  /** Sign in with the user name and password; answers with a PlayFab session of the old account. Every refusal is the same "bad_login". */
  async function login(usernameRaw, password, ip) {
    const t = now();
    const ipf = ipFails.get(ip);
    if (ipf && ipf.resetAt > t && ipf.n >= IP_FAILS) throw new ChatError('locked', 429, { retryMs: ipf.resetAt - t });
    const username = String(usernameRaw || '').trim().toLowerCase();
    const f = fails.get(username);
    if (f && f.until > t) throw new ChatError('locked', 429, { retryMs: f.until - t });
    const hit = NAME_RE.test(username) ? byName(username) : null;
    const ok = hit && verifyPassword(String(password || ''), hit[1].hash);
    if (!ok) {
      const n = (f && !(f.until && f.until <= t) ? f.n : 0) + 1;
      fails.set(username, { n, until: n >= MAX_FAILS ? t + LOCK_MS : 0 });
      const w = ipf && ipf.resetAt > t ? ipf : { n: 0, resetAt: t + IP_WINDOW_MS };
      w.n += 1; ipFails.set(ip, w);
      throw new ChatError('bad_login', 401);
    }
    fails.delete(username);
    let session;
    try { session = await playfab.loginServerCustomId(customId(hit[0])); } catch { throw new ChatError('try_again', 502); }
    hit[1].last = t; hit[1].count = (hit[1].count || 0) + 1; store.touch();
    return { ok: true, sessionTicket: session.SessionTicket, playFabId: session.PlayFabId || hit[0] };
  }

  return { set, remove, info, login, NAME_RE };
}

module.exports = { createRecovery };
