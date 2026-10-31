'use strict';
// Signing in with another identity (Google now, Facebook and Apple later). Our server is the broker:
//   1. the game gets a token from the provider on the phone and sends it here,
//   2. the server checks the token with the provider, and finds the PlayFab account that belongs to that identity,
//   3. "login": returns a PlayFab session of that account (an account is made for a new identity), so the game can tie the device to it;
//      "link": ties the identity to the account of the player who is already in (a guest becoming a real account).
// An identity belongs to one account. Trying to link one that already belongs to another account answers "account_exists" with that account's name,
// so the game can ask the player whether to open it instead.
// PlayFab side: every identity is a "server custom id" (g-<id> for Google) on its account.

const { ChatError } = require('./chat');

const ISSUERS = ['accounts.google.com', 'https://accounts.google.com'];

/** Checks a Google ID token with Google's own tokeninfo endpoint (free, no keys). Returns { sub, email, name }. */
function createGoogle({ clientIds = [], fetchFn = globalThis.fetch, now = () => Date.now() }) {
  const allowed = new Set(clientIds.filter(Boolean));
  async function verify(idToken) {
    const tok = String(idToken || '');
    if (!allowed.size) throw new ChatError('not_configured', 503);
    if (tok.length < 20 || tok.length > 4096 || tok.split('.').length !== 3) throw new ChatError('bad_token', 401);
    let j;
    try {
      const res = await fetchFn('https://oauth2.googleapis.com/tokeninfo?id_token=' + encodeURIComponent(tok), { signal: AbortSignal.timeout(8000) });
      if (!res.ok) throw new ChatError('bad_token', 401);
      j = await res.json();
    } catch (e) { if (e instanceof ChatError) throw e; throw new ChatError('try_again', 502); }
    if (!j || !j.sub || !allowed.has(j.aud) || !ISSUERS.includes(j.iss) || !(Number(j.exp) * 1000 > now())) throw new ChatError('bad_token', 401);
    return { sub: String(j.sub), email: j.email || '', name: j.name || '' };
  }
  return { verify, enabled: allowed.size > 0 };
}

/**
 * Checks a Facebook user access token with Facebook's debug_token endpoint, using the app's own token (app id | app secret).
 * The token must be valid and made for THIS app. Returns { sub } (the app-scoped Facebook user id).
 */
function createFacebook({ appId = '', appSecret = '', fetchFn = globalThis.fetch, now = () => Date.now() }) {
  const enabled = !!(appId && appSecret);
  async function verify(accessToken) {
    const tok = String(accessToken || '');
    if (!enabled) throw new ChatError('not_configured', 503);
    if (tok.length < 20 || tok.length > 4096 || /\s/.test(tok)) throw new ChatError('bad_token', 401);
    let d;
    try {
      const url = 'https://graph.facebook.com/debug_token?input_token=' + encodeURIComponent(tok) + '&access_token=' + encodeURIComponent(appId + '|' + appSecret);
      const res = await fetchFn(url, { signal: AbortSignal.timeout(8000) });
      const j = await res.json().catch(() => null);
      if (!res.ok && !(j && j.data)) throw new ChatError(res.status >= 500 ? 'try_again' : 'bad_token', res.status >= 500 ? 502 : 401);
      d = j && j.data;
    } catch (e) { if (e instanceof ChatError) throw e; throw new ChatError('try_again', 502); }
    const exp = Number(d && d.expires_at) || 0;          // 0 = does not expire
    if (!d || d.is_valid !== true || String(d.app_id) !== String(appId) || !d.user_id || (exp && exp * 1000 <= now())) throw new ChatError('bad_token', 401);
    return { sub: String(d.user_id) };
  }
  return { verify, enabled };
}

function createIdentity({ accounts, playfab, providers = {}, chat = null, log = console }) {
  const customId = (provider, id) => provider.charAt(0) + '-' + id;
  const prov = name => { const p = providers[name]; if (!p || !p.enabled) throw new ChatError('not_configured', 503); return p; };

  /** Opens the account of an identity (making one for a new identity). Returns a PlayFab session. */
  async function login(provider, idToken) {
    const id = (await prov(provider).verify(idToken)).sub;
    const owner = accounts.owner(provider, id);
    let session;
    try { session = await playfab.loginServerCustomId(customId(provider, id), !owner); } catch (e) { log.error('identity login failed', e.message); throw new ChatError('try_again', 502); }
    const pf = session.PlayFabId || owner;
    if (!owner) { accounts.touch(pf); accounts.link(pf, provider, id); }
    return { ok: true, sessionTicket: session.SessionTicket, playFabId: pf, created: !owner };
  }

  /** Ties an identity to the account of the player who is signed in. */
  async function link(pf, provider, idToken) {
    const id = (await prov(provider).verify(idToken)).sub;
    const owner = accounts.owner(provider, id);
    if (owner === pf) return { ok: true, already: true };
    if (owner) {
      let name = '';
      try { if (chat) name = (await chat.who(owner)).name || ''; } catch { /* the name is only for the question */ }
      throw new ChatError('account_exists', 409, { name });
    }
    try { await playfab.linkServerCustomId(pf, customId(provider, id), false); } catch (e) { log.error('identity link failed', e.message); throw new ChatError('try_again', 502); }
    accounts.link(pf, provider, id);
    return { ok: true, already: false };
  }

  return { login, link };
}

module.exports = { createGoogle, createFacebook, createIdentity };
