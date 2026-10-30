'use strict';
// MAFIA ADMIN BOARD: a small web page + JSON API, served by this same service under cfg.admin.path (default /admin), so it needs no new domain.
// One admin password (stored only as a scrypt hash in the env file). A session lives in a HttpOnly cookie; every change needs the header X-Admin: 1
// (a page of another site cannot send it). Wrong passwords lock the address for a while. Everything the admin does is written to a short log.

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { ChatError } = require('./chat');

const PF_ID = /^[A-Za-z0-9]{6,32}$/;
const ID_RE = /^[A-Za-z0-9_-]{1,40}$/;
const REF_RE = /^[A-Za-z0-9_-]{1,64}$/;
const KINDS = ['bonus', 'challenge', 'info'];
const MAX_WORDS = 500;

class AdminError extends Error {
  constructor(code, status = 400) { super(code); this.code = code; this.status = status; }
}

// ------------------------------------------------------------------ password hash  ("scrypt:<salt hex>:<hash hex>", no "$" so it is safe in an env file)

function hashPassword(password) {
  const salt = crypto.randomBytes(16);
  const h = crypto.scryptSync(String(password), salt, 32);
  return 'scrypt:' + salt.toString('hex') + ':' + h.toString('hex');
}
function verifyPassword(password, stored) {
  const [kind, saltHex, hashHex] = String(stored || '').split(':');
  if (kind !== 'scrypt' || !saltHex || !hashHex) return false;
  try {
    const want = Buffer.from(hashHex, 'hex');
    if (want.length < 16) return false;
    const got = crypto.scryptSync(String(password), Buffer.from(saltHex, 'hex'), want.length);
    return want.length === got.length && crypto.timingSafeEqual(want, got);
  } catch { return false; }
}

// ------------------------------------------------------------------ cleaning what the page sends

const text = (v, max) => (typeof v === 'string' ? v.trim().slice(0, max) : '');
function isoOrEmpty(v, name) {
  if (v === undefined || v === null || v === '') return '';
  const t = Date.parse(v);
  if (!Number.isFinite(t)) throw new AdminError('bad_' + name);
  return new Date(t).toISOString();
}
function newId(prefix) { return prefix + '-' + Date.now().toString(36) + crypto.randomBytes(2).toString('hex'); }

function cleanEvent(b, id) {
  if (!b || typeof b !== 'object') throw new AdminError('bad_event');
  if (!KINDS.includes(b.kind)) throw new AdminError('bad_kind');
  const e = { id, kind: b.kind, kicker: text(b.kicker, 24), title: text(b.title, 60), body: text(b.body, 160), enabled: b.enabled !== false };
  if (!e.title) throw new AdminError('title_required');
  e.from = isoOrEmpty(b.from, 'from'); e.until = isoOrEmpty(b.until, 'until');
  if (e.from && e.until && Date.parse(e.until) <= Date.parse(e.from)) throw new AdminError('until_before_from');
  if (e.kind === 'bonus') {
    const m = Number(b.coinsMultiplier);
    if (!Number.isFinite(m) || m < 1 || m > 5) throw new AdminError('bad_multiplier');
    e.coinsMultiplier = m;
  } else if (e.kind === 'challenge') {
    const goal = Math.floor(Number(b.goal)), reward = Math.floor(Number(b.rewardCoins));
    if (!(goal >= 1 && goal <= 1000)) throw new AdminError('bad_goal');
    if (!(reward >= 0 && reward <= 10000)) throw new AdminError('bad_reward');
    e.goal = goal; e.rewardCoins = reward; e.metric = b.metric === 'games' ? 'games' : 'wins';
  } else {
    e.secret = b.secret === true;
  }
  return e;
}

function cleanOffer(b, id) {
  if (!b || typeof b !== 'object') throw new AdminError('bad_offer');
  const o = { id, enabled: b.enabled !== false, mast: text(b.mast, 40), headline: text(b.headline, 80), body: text(b.body, 140), stamp: text(b.stamp, 12) };
  const adId = text(b.adId, 64), itemId = text(b.itemId, 64);
  if (adId && !REF_RE.test(adId)) throw new AdminError('bad_adId');
  if (itemId && !REF_RE.test(itemId)) throw new AdminError('bad_itemId');
  if (!adId && !itemId) throw new AdminError('ad_or_item_required');
  if (adId) o.adId = adId; else o.itemId = itemId;
  o.from = isoOrEmpty(b.from, 'from'); o.until = isoOrEmpty(b.until, 'until');
  if (o.from && o.until && Date.parse(o.until) <= Date.parse(o.from)) throw new AdminError('until_before_from');
  return o;
}

function createAdmin({ cfg = {}, adminCfg = {}, store, chat, friends = null, leaderboard = null, reportsFile = null, htmlFile = null, now = () => Date.now(), log = console }) {
  const base = '/' + String(adminCfg.path || '/admin').replace(/^\/+|\/+$/g, '');
  const passwordHash = adminCfg.passwordHash || '';
  const sessionMs = (adminCfg.sessionHours || 12) * 3600 * 1000;
  const html = (() => { try { return fs.readFileSync(htmlFile || path.join(__dirname, 'admin-ui.html'), 'utf8').replace(/__BASE__/g, base); } catch { return '<!doctype html><title>admin</title>admin page missing'; } })();
  const sessions = new Map();     // token -> until
  const fails = new Map();        // ip -> { n, until }
  const MAX_FAILS = 5, LOCK_MS = 15 * 60 * 1000;

  const enabled = !!passwordHash;
  const owns = p => enabled && (p === base || p.startsWith(base + '/'));
  const day = (t = now()) => (leaderboard && leaderboard.dayKey ? leaderboard.dayKey(t) : new Date(t).toISOString().slice(0, 10));

  // ------------------------------------------------------------------ plumbing

  function send(res, status, body, headers = {}) {
    const isStr = typeof body === 'string';
    const out = isStr ? body : JSON.stringify(body);
    res.writeHead(status, Object.assign({
      'Content-Type': isStr ? 'text/html; charset=utf-8' : 'application/json; charset=utf-8', 'Content-Length': Buffer.byteLength(out),
      'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff', 'X-Frame-Options': 'DENY', 'Referrer-Policy': 'no-referrer', 'X-Robots-Tag': 'noindex, nofollow',
    }, headers));
    res.end(out);
  }

  function readJson(req) {
    return new Promise((resolve, reject) => {
      let size = 0; const chunks = [];
      req.on('data', c => { size += c.length; if (size > 16384) { reject(new AdminError('too_big', 413)); req.destroy(); } else chunks.push(c); });
      req.on('end', () => { if (!size) return resolve({}); try { resolve(JSON.parse(Buffer.concat(chunks).toString('utf8'))); } catch { reject(new AdminError('bad_json')); } });
      req.on('error', () => reject(new AdminError('read_failed')));
    });
  }

  function cookieOf(req, name) {
    for (const part of String(req.headers.cookie || '').split(';')) { const [k, ...v] = part.trim().split('='); if (k === name) return v.join('='); }
    return '';
  }
  function authed(req) {
    const t = cookieOf(req, 'mab');
    const until = t && sessions.get(t);
    if (!until) return false;
    if (until < now()) { sessions.delete(t); return false; }
    return true;
  }
  function setCookie(req, token, maxAgeSec) {
    const secure = String(req.headers['x-forwarded-proto'] || '') === 'https' ? '; Secure' : '';
    return 'mab=' + token + '; HttpOnly; SameSite=Strict; Path=' + base + '; Max-Age=' + maxAgeSec + secure;
  }

  // ------------------------------------------------------------------ events and offers (the admin's own list, or config.json's until the first edit)

  const eventList = () => (store.events !== null ? store.events : (Array.isArray(cfg.events) ? cfg.events : []));
  const offerList = () => (store.offers !== null ? store.offers : (Array.isArray(cfg.offers) ? cfg.offers : []).map((o, i) => Object.assign({ id: 'cfg-' + i }, o)));
  const ensureOwnEvents = () => { if (store.events === null) store.events = eventList().filter(e => e && e.id).map(e => Object.assign({}, e)); return store.events; };
  const ensureOwnOffers = () => { if (store.offers === null) store.offers = offerList().map(o => Object.assign({}, o)); return store.offers; };

  function statusOf(e) {
    const t = now(), from = e.from ? Date.parse(e.from) : 0, until = e.until ? Date.parse(e.until) : 0;
    if (e.enabled === false) return 'off';
    if (from && from > t) return 'soon';
    if (until && until <= t) return 'done';
    return 'live';
  }
  const withStatus = l => l.map(e => Object.assign({}, e, { status: statusOf(e) }));

  // ------------------------------------------------------------------ reports

  function readReports() {
    if (!reportsFile) return [];
    let raw = '';
    try {
      const st = fs.statSync(reportsFile), len = Math.min(st.size, 600 * 1024), fd = fs.openSync(reportsFile, 'r');
      const buf = Buffer.alloc(len); fs.readSync(fd, buf, 0, len, st.size - len); fs.closeSync(fd);
      raw = buf.toString('utf8');
    } catch { return []; }
    const lines = raw.split('\n').filter(Boolean);
    if (lines.length && raw.length >= 600 * 1024) lines.shift();     // the first line of a cut file may be half
    const out = [];
    for (const line of lines) {
      let r; try { r = JSON.parse(line); } catch { continue; }
      if (!r || !r.target || !r.reporter) continue;
      out.push({ id: crypto.createHash('sha1').update(line).digest('hex').slice(0, 12), ts: r.ts, type: r.type === 'player' ? 'player' : 'message', reporter: r.reporter, target: r.target, reason: String(r.reason || ''), text: String(r.text || r.note || '') });
    }
    return out.reverse();
  }

  async function nameOf(pf) { try { return (await chat.who(pf)).name; } catch { return 'لاعب'; } }

  async function reportsView() {
    const all = readReports().slice(0, 80);
    const names = new Map();
    const rows = [];
    for (const r of all) {
      for (const pf of [r.reporter, r.target]) if (!names.has(pf)) names.set(pf, await nameOf(pf));
      const h = store.handled(r.id);
      rows.push(Object.assign({}, r, { reporterName: names.get(r.reporter), targetName: names.get(r.target), status: h ? 'handled' : 'new', action: h ? h.action : '', muted: !!chat.muteUntil(r.target) }));
    }
    return rows;
  }

  // ------------------------------------------------------------------ players

  async function playerCard(pf) {
    const p = await chat.who(pf);
    const f = friends ? friends._state() : null;
    const stats = leaderboard && leaderboard.playerStats ? leaderboard.playerStats(pf) : null;
    const against = readReports().filter(r => r.target === pf).length;
    return { pf, name: p.name, rank: p.rank, level: p.level, num: f && f.nums[pf] ? f.nums[pf] : null, stats, reportsAgainst: against, mutedUntil: chat.muteUntil(pf) || 0 };
  }

  async function findPlayers(q) {
    q = String(q || '').trim();
    if (!q) return [];
    const f = friends ? friends._state() : null;
    let ids = [];
    if (/^\d{6}$/.test(q) && f && f.byNum[q]) ids = [f.byNum[q]];
    else if (PF_ID.test(q) && !/^\d+$/.test(q)) ids = [q];
    else ids = chat.searchPeople(q).map(x => x.pf);
    const out = [];
    for (const pf of ids.slice(0, 5)) out.push(await playerCard(pf));
    return out;
  }

  // ------------------------------------------------------------------ overview

  function overview() {
    const days = [];
    for (let i = 6; i >= 0; i--) { const d = day(now() - i * 86400000); days.push({ day: d, matches: store.matchesOn(d) }); }
    const reports = readReports();
    const open = reports.filter(r => !store.handled(r.id)).length;
    const live = withStatus(eventList()).filter(e => e.status === 'live').length;
    return {
      ok: true, online: chat.onlineCount(), players: leaderboard && leaderboard.playerCount ? leaderboard.playerCount() : 0,
      matchesToday: store.matchesOn(day()), days, openReports: open, liveEvents: live, activity: store.recentAudit(8),
      serverTime: now(),
    };
  }

  // ------------------------------------------------------------------ the API

  async function api(req, res, rest, url, ip) {
    const method = req.method;
    if (rest === '/api/login' && method === 'POST') {
      const f = fails.get(ip);
      if (f && f.until > now()) return send(res, 429, { ok: false, error: 'locked', retryMs: f.until - now() });
      const b = await readJson(req);
      if (verifyPassword(b.password, passwordHash)) {
        fails.delete(ip);
        const token = crypto.randomBytes(32).toString('hex');
        sessions.set(token, now() + sessionMs);
        if (sessions.size > 50) for (const [t, u] of sessions) if (u < now()) sessions.delete(t);
        store.audit('login', ip);
        return send(res, 200, { ok: true }, { 'Set-Cookie': setCookie(req, token, Math.floor(sessionMs / 1000)) });
      }
      const n = (f && !(f.until && f.until <= now()) ? f.n : 0) + 1;      // an old lock that has run out starts the count again
      fails.set(ip, { n, until: n >= MAX_FAILS ? now() + LOCK_MS : 0 });
      store.audit('login_failed', ip);
      return send(res, 401, { ok: false, error: 'wrong_password', left: Math.max(0, MAX_FAILS - n) });
    }
    if (!authed(req)) return send(res, 401, { ok: false, error: 'not_logged_in' });
    if (method === 'POST' && req.headers['x-admin'] !== '1') return send(res, 403, { ok: false, error: 'missing_header' });

    if (rest === '/api/logout' && method === 'POST') {
      sessions.delete(cookieOf(req, 'mab'));
      return send(res, 200, { ok: true }, { 'Set-Cookie': setCookie(req, '', 0) });
    }
    if (rest === '/api/overview' && method === 'GET') return send(res, 200, overview());

    // ---- events
    if (rest === '/api/events' && method === 'GET') return send(res, 200, { ok: true, source: store.events !== null ? 'admin' : 'config', events: withStatus(eventList()) });
    if (rest === '/api/events/save' && method === 'POST') {
      const b = await readJson(req);
      const list = ensureOwnEvents();
      const idx = b.id ? list.findIndex(e => e.id === b.id) : -1;
      if (b.id && !ID_RE.test(String(b.id))) throw new AdminError('bad_id');
      const e = cleanEvent(b, idx >= 0 ? list[idx].id : (b.id && ID_RE.test(b.id) ? b.id : newId('ev')));
      if (idx < 0 && list.some(x => x.id === e.id)) throw new AdminError('id_taken', 409);
      if (idx >= 0) list[idx] = e; else list.push(e);
      store.events = list;
      store.audit('event_save', e.id + ' ' + e.title);
      return send(res, 200, { ok: true, id: e.id });
    }
    if (rest === '/api/events/delete' && method === 'POST') {
      const b = await readJson(req); const list = ensureOwnEvents();
      const n = list.length; store.events = list.filter(e => e.id !== b.id);
      if (store.events.length === n) throw new AdminError('not_found', 404);
      store.audit('event_delete', String(b.id));
      return send(res, 200, { ok: true });
    }
    if (rest === '/api/events/toggle' && method === 'POST') {
      const b = await readJson(req); const list = ensureOwnEvents(); const e = list.find(x => x.id === b.id);
      if (!e) throw new AdminError('not_found', 404);
      e.enabled = b.enabled === true; store.events = list;
      store.audit('event_toggle', e.id + ' ' + e.enabled);
      return send(res, 200, { ok: true });
    }

    // ---- offers
    if (rest === '/api/offers' && method === 'GET') return send(res, 200, { ok: true, source: store.offers !== null ? 'admin' : 'config', offers: withStatus(offerList()) });
    if (rest === '/api/offers/save' && method === 'POST') {
      const b = await readJson(req); const list = ensureOwnOffers();
      if (b.id && !ID_RE.test(String(b.id))) throw new AdminError('bad_id');
      const idx = b.id ? list.findIndex(o => o.id === b.id) : -1;
      const o = cleanOffer(b, idx >= 0 ? list[idx].id : newId('of'));
      if (idx >= 0) list[idx] = o; else list.push(o);
      store.offers = list;
      store.audit('offer_save', o.id + ' ' + (o.adId || o.itemId));
      return send(res, 200, { ok: true, id: o.id });
    }
    if (rest === '/api/offers/delete' && method === 'POST') {
      const b = await readJson(req); const list = ensureOwnOffers();
      const n = list.length; store.offers = list.filter(o => o.id !== b.id);
      if (store.offers.length === n) throw new AdminError('not_found', 404);
      store.audit('offer_delete', String(b.id));
      return send(res, 200, { ok: true });
    }
    if (rest === '/api/offers/toggle' && method === 'POST') {
      const b = await readJson(req); const list = ensureOwnOffers(); const o = list.find(x => x.id === b.id);
      if (!o) throw new AdminError('not_found', 404);
      o.enabled = b.enabled === true; store.offers = list;
      store.audit('offer_toggle', o.id + ' ' + o.enabled);
      return send(res, 200, { ok: true });
    }

    // ---- banned words
    if (rest === '/api/words' && method === 'GET') return send(res, 200, { ok: true, exact: store.words.exact, contains: store.words.contains });
    if (rest === '/api/words/add' && method === 'POST') {
      const b = await readJson(req);
      const list = b.list === 'contains' ? store.words.contains : b.list === 'exact' ? store.words.exact : null;
      const w = text(b.word, 30);
      if (!list) throw new AdminError('bad_list');
      if (!w || /\s/.test(w)) throw new AdminError('bad_word');
      if (list.includes(w)) return send(res, 200, { ok: true, duplicate: true });
      if (list.length >= MAX_WORDS) throw new AdminError('too_many');
      list.push(w); store.touch(); store.audit('word_add', b.list + ': ' + w);
      return send(res, 200, { ok: true });
    }
    if (rest === '/api/words/remove' && method === 'POST') {
      const b = await readJson(req);
      const list = b.list === 'contains' ? store.words.contains : b.list === 'exact' ? store.words.exact : null;
      if (!list) throw new AdminError('bad_list');
      const i = list.indexOf(b.word);
      if (i < 0) throw new AdminError('not_found', 404);
      list.splice(i, 1); store.touch(); store.audit('word_remove', b.list + ': ' + b.word);
      return send(res, 200, { ok: true });
    }

    // ---- reports and players
    if (rest === '/api/reports' && method === 'GET') return send(res, 200, { ok: true, reports: await reportsView() });
    if (rest === '/api/reports/handle' && method === 'POST') {
      const b = await readJson(req);
      const r = readReports().find(x => x.id === b.id);
      if (!r) throw new AdminError('not_found', 404);
      if (b.action === 'mute') { chat.adminMute(r.target, Number(b.minutes) || 120); store.audit('report_mute', r.target + ' ' + (Number(b.minutes) || 120) + 'm'); }
      else if (b.action !== 'ignore') throw new AdminError('bad_action');
      else store.audit('report_ignore', r.id);
      store.markHandled(r.id, b.action);
      return send(res, 200, { ok: true });
    }
    if (rest === '/api/players' && method === 'GET') return send(res, 200, { ok: true, players: await findPlayers(url.searchParams.get('q')) });
    if (rest === '/api/players/mute' && method === 'POST') {
      const b = await readJson(req);
      const until = chat.adminMute(String(b.pf), Number(b.minutes) || 0);
      store.audit(until ? 'player_mute' : 'player_unmute', String(b.pf) + (until ? ' ' + b.minutes + 'm' : ''));
      return send(res, 200, { ok: true, mutedUntil: until });
    }
    return send(res, 404, { ok: false, error: 'not_found' });
  }

  /** Handles a request under the admin path. Call only when owns(pathname) is true. */
  async function handle(req, res, url, ip) {
    try {
      const rest = url.pathname.slice(base.length) || '/';
      if (rest === '/' && req.method === 'GET') return send(res, 200, html);
      if (!rest.startsWith('/api/')) return send(res, 404, { ok: false, error: 'not_found' });
      await api(req, res, rest, url, ip);
    } catch (e) {
      if (res.writableEnded) return;
      if (e instanceof AdminError || e instanceof ChatError) return send(res, e.status || 400, { ok: false, error: e.code || e.message });
      log.error('admin failed:', e && e.message);
      send(res, 500, { ok: false, error: 'server_error' });
    }
  }

  return {
    enabled, base, owns, handle,
    noteMatch: matchId => store.noteMatch(matchId, day()),
    eventList: () => eventList(), offerList: () => (store.offers !== null ? store.offers : null),
    extraWords: () => store.words,
  };
}

module.exports = { createAdmin, hashPassword, verifyPassword, AdminError };
