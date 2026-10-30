'use strict';
// MAFIA ADMIN BOARD: a small web page + JSON API, served by this same service under cfg.admin.path (default /admin), so it needs no new domain.
// One admin password (stored only as a scrypt hash in the env file). A session lives in a HttpOnly cookie; every change needs the header X-Admin: 1
// (a page of another site cannot send it). Wrong passwords lock the address for a while. Everything the admin does is written to a short log.

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { ChatError } = require('./chat');
const { normalize: normCode, CODE_RE } = require('./codes');

const PF_ID = /^[A-Za-z0-9]{6,32}$/;
const ID_RE = /^[A-Za-z0-9_-]{1,40}$/;
const REF_RE = /^[A-Za-z0-9_-]{1,64}$/;
const KINDS = ['bonus', 'challenge', 'info'];
const MAX_WORDS = 500;
const METRICS = ['wins', 'games', 'points', 'secs'];

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

function cleanEvent(b, id, packs = []) {
  if (!b || typeof b !== 'object') throw new AdminError('bad_event');
  if (!KINDS.includes(b.kind)) throw new AdminError('bad_kind');
  const e = { id, kind: b.kind, kicker: text(b.kicker, 24), title: text(b.title, 60), body: text(b.body, 160), kickerEn: text(b.kickerEn, 24), titleEn: text(b.titleEn, 60), bodyEn: text(b.bodyEn, 160), enabled: b.enabled !== false };
  if (!e.title && !e.titleEn) throw new AdminError('title_required');
  if (!e.title) e.title = e.titleEn;       // the Arabic title is the fallback of the game: never empty
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
  if (b.ranking && b.ranking.enabled === true) {
    const r = b.ranking;
    if (!METRICS.includes(r.metric)) throw new AdminError('bad_metric');
    const winners = Math.floor(Number(r.winners));
    if (!(winners >= 1 && winners <= 20)) throw new AdminError('bad_winners');
    const prizes = (Array.isArray(r.prizes) ? r.prizes : []).slice(0, 20).map(p => ({ from: Math.floor(Number(p.from)), to: Math.floor(Number(p.to)), packId: text(p.packId, 40) }));
    for (const p of prizes) {
      if (!(p.from >= 1 && p.to >= p.from && p.to <= winners)) throw new AdminError('bad_prize_ranks');
      if (!packs.some(k => k.id === p.packId)) throw new AdminError('pack_not_found');
    }
    e.ranking = { enabled: true, metric: r.metric, winners, prizes, distribution: r.distribution === 'manual' ? 'manual' : 'auto' };
  }
  return e;
}

function cleanOffer(b, id) {
  if (!b || typeof b !== 'object') throw new AdminError('bad_offer');
  const o = { id, enabled: b.enabled !== false, mast: text(b.mast, 40), headline: text(b.headline, 80), body: text(b.body, 140), stamp: text(b.stamp, 12), mastEn: text(b.mastEn, 40), headlineEn: text(b.headlineEn, 80), bodyEn: text(b.bodyEn, 140), stampEn: text(b.stampEn, 12) };
  const adId = text(b.adId, 64), itemId = text(b.itemId, 64);
  if (adId && !REF_RE.test(adId)) throw new AdminError('bad_adId');
  if (itemId && !REF_RE.test(itemId)) throw new AdminError('bad_itemId');
  if (!adId && !itemId) throw new AdminError('ad_or_item_required');
  if (adId) o.adId = adId; else o.itemId = itemId;
  o.from = isoOrEmpty(b.from, 'from'); o.until = isoOrEmpty(b.until, 'until');
  if (o.from && o.until && Date.parse(o.until) <= Date.parse(o.from)) throw new AdminError('until_before_from');
  return o;
}

const bi = (o, max) => ({ ar: text(o && o.ar, max), en: text(o && o.en, max) });
function cleanPack(b, id) {
  if (!b || typeof b !== 'object') throw new AdminError('bad_pack');
  const name = bi(b.name, 40);
  if (!name.ar && !name.en) throw new AdminError('name_required');
  const coins = Math.floor(Number(b.coins) || 0), gems = Math.floor(Number(b.gems) || 0);
  if (!(coins >= 0 && coins <= 1000000)) throw new AdminError('bad_coins');
  if (!(gems >= 0 && gems <= 10000)) throw new AdminError('bad_gems');
  const items = Array.isArray(b.items) ? b.items.map(x => text(x, 64)).filter(Boolean) : [];
  if (items.length > 10 || items.some(x => !REF_RE.test(x))) throw new AdminError('bad_items');
  if (!coins && !gems && !items.length) throw new AdminError('empty_pack');
  return { id, name, coins, gems, items };
}

function createAdmin({ cfg = {}, adminCfg = {}, store, chat, friends = null, leaderboard = null, mail = null, playfab = null, reportsFile = null, events = null, codes = null, htmlFile = null, now = () => Date.now(), log = console }) {
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
    let balances = null;
    if (playfab && playfab.getBalances) { try { const b = await playfab.getBalances(pf); balances = { coins: b[cfg.currencyCode] || 0, gems: b[cfg.gemsCode || 'GM'] || 0 }; } catch { /* PlayFab down: no balances */ } }
    return { pf, name: p.name, rank: p.rank, level: p.level, num: f && f.nums[pf] ? f.nums[pf] : null, stats, reportsAgainst: against, mutedUntil: chat.muteUntil(pf) || 0, balances, pendingMail: mail ? mail.pendingFor(pf) : 0, ban: store.bans()[pf] || null, log: store.auditFor(pf, 12) };
  }

  function resolveTarget(t) {
    t = String(t || '').trim();
    const f = friends ? friends._state() : null;
    if (/^\d{6}$/.test(t) && f && f.byNum[t]) return f.byNum[t];
    if (PF_ID.test(t) && !/^\d+$/.test(t)) return t;
    throw new AdminError('unknown_player', 404);
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
      const e = cleanEvent(b, idx >= 0 ? list[idx].id : (b.id && ID_RE.test(b.id) ? b.id : newId('ev')), store.packs);
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

    if (rest === '/api/events/ranking' && method === 'GET') { if (!events) throw new AdminError('no_events', 404); return send(res, 200, await events.adminRanking(String(url.searchParams.get('id') || ''))); }
    if (rest === '/api/events/distribute' && method === 'POST') {
      if (!events) throw new AdminError('no_events', 404);
      const b = await readJson(req);
      const r = await events.distribute(String(b.id || ''), { by: 'admin' });
      store.audit('ranking_distribute', String(b.id) + ' · ' + r.count + ' players');
      return send(res, 200, r);
    }

    // ---- promo codes
    if (rest === '/api/codes' && method === 'GET') { if (!codes) throw new AdminError('no_codes', 404); return send(res, 200, { ok: true, codes: codes.list().map(c => ({ code: c.code, packId: c.packId, maxUses: c.maxUses || 0, uses: c.uses || 0, until: c.until || '', enabled: c.enabled !== false })) }); }
    if (rest === '/api/codes/save' && method === 'POST') {
      if (!codes) throw new AdminError('no_codes', 404);
      const b = await readJson(req);
      const code = b.code ? normCode(b.code) : codes.randomCode();
      if (!CODE_RE.test(code)) throw new AdminError('bad_code_format');
      if (!store.packs.some(p => p.id === b.packId)) throw new AdminError('pack_not_found', 404);
      const maxUses = Math.floor(Number(b.maxUses) || 0);
      if (!(maxUses >= 0 && maxUses <= 1000000)) throw new AdminError('bad_max_uses');
      const until = isoOrEmpty(b.until, 'until');
      const list = codes.list().slice(), old = list.find(c => c.code === code);
      if (old && b.isNew === true) throw new AdminError('code_exists', 409);
      const c = Object.assign(old || { code, uses: 0, by: {} }, { packId: b.packId, maxUses, until, enabled: b.enabled !== false });
      if (!old) list.push(c);
      store.codes = list; store.audit('code_save', code + ' · ' + b.packId);
      return send(res, 200, { ok: true, code });
    }
    if (rest === '/api/codes/toggle' && method === 'POST') {
      if (!codes) throw new AdminError('no_codes', 404);
      const b = await readJson(req); const c = codes.find(normCode(b.code));
      if (!c) throw new AdminError('not_found', 404);
      c.enabled = b.enabled === true; store.touch(); store.audit('code_toggle', c.code + ' ' + c.enabled);
      return send(res, 200, { ok: true });
    }
    if (rest === '/api/codes/delete' && method === 'POST') {
      if (!codes) throw new AdminError('no_codes', 404);
      const b = await readJson(req); const code = normCode(b.code); const n = codes.list().length;
      store.codes = codes.list().filter(c => c.code !== code);
      if (store.codes.length === n) throw new AdminError('not_found', 404);
      store.audit('code_delete', code);
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
      store.audit(until ? 'player_mute' : 'player_unmute', String(b.pf) + (until ? ' ' + b.minutes + 'm' : ''), String(b.pf));
      return send(res, 200, { ok: true, mutedUntil: until });
    }
    // ---- packages
    if (rest === '/api/packs' && method === 'GET') return send(res, 200, { ok: true, packs: store.packs });
    if (rest === '/api/packs/save' && method === 'POST') {
      const b = await readJson(req); const list = store.packs.slice();
      if (b.id && !ID_RE.test(String(b.id))) throw new AdminError('bad_id');
      const idx = b.id ? list.findIndex(x => x.id === b.id) : -1;
      const p = cleanPack(b, idx >= 0 ? list[idx].id : newId('pk'));
      if (idx >= 0) list[idx] = p; else list.push(p);
      store.packs = list; store.audit('pack_save', p.id + ' ' + (p.name.ar || p.name.en));
      return send(res, 200, { ok: true, id: p.id });
    }
    if (rest === '/api/packs/delete' && method === 'POST') {
      const b = await readJson(req); const n = store.packs.length;
      store.packs = store.packs.filter(x => x.id !== b.id);
      if (store.packs.length === n) throw new AdminError('not_found', 404);
      store.audit('pack_delete', String(b.id));
      return send(res, 200, { ok: true });
    }

    // ---- mail
    if (rest === '/api/mail/sent' && method === 'GET') { if (!mail) throw new AdminError('no_mail', 404); return send(res, 200, { ok: true, sent: mail.sentList() }); }
    if (rest === '/api/mail/send' && method === 'POST') {
      if (!mail) throw new AdminError('no_mail', 404);
      const b = await readJson(req);
      const title = bi(b.title, 80), body = bi(b.body, 200);
      if (!title.ar && !title.en) throw new AdminError('title_required');
      let pack = { coins: 0, gems: 0, items: [] }, label = '';
      if (b.packId) { const p = store.packs.find(x => x.id === b.packId); if (!p) throw new AdminError('pack_not_found', 404); pack = p; label = p.name.ar || p.name.en; }
      else if (b.pack && (b.pack.coins || b.pack.gems || (b.pack.items && b.pack.items.length))) { pack = cleanPack(Object.assign({ name: { ar: 'custom' } }, b.pack), 'custom'); label = 'custom'; }
      const days = Math.max(0, Math.min(365, Math.floor(Number(b.expiresInDays) || 0)));
      let audience = 'players', recipients = [], who = '';
      if (b.audience === 'all') {
        if (b.confirm !== true) throw new AdminError('confirm_required');
        audience = 'all'; who = 'all';
      } else if (b.audience === 'active7') {
        if (b.confirm !== true) throw new AdminError('confirm_required');
        recipients = leaderboard && leaderboard.activePlayers ? leaderboard.activePlayers(7).slice(0, 5000) : []; who = 'active7:' + recipients.length;
      } else if (b.audience === 'one' || b.audience === 'list') {
        const targets = Array.isArray(b.targets) ? b.targets : [b.targets];
        if (!targets.length || targets.length > 200) throw new AdminError('bad_targets');
        recipients = [...new Set(targets.map(resolveTarget))]; who = recipients.join(',').slice(0, 60);
      } else throw new AdminError('bad_audience');
      if (audience === 'players' && !recipients.length) throw new AdminError('no_recipients');
      const r = mail.send({ audience, recipients, title, body, pack, expiresInDays: days, label });
      store.audit('mail_send', (title.ar || title.en) + ' -> ' + who + (label ? ' · ' + label : ''), recipients.length === 1 ? recipients[0] : '');
      return send(res, 200, { ok: true, id: r.id, count: r.count });
    }

    // ---- support: rename, ban, unban
    if (rest === '/api/players/rename' && method === 'POST') {
      if (!playfab || !playfab.setDisplayName) throw new AdminError('no_playfab', 404);
      const b = await readJson(req); const pf = String(b.pf || '');
      if (!PF_ID.test(pf)) throw new AdminError('bad_player');
      const name = text(b.name, 25);
      if (name.length < 3) throw new AdminError('bad_name');
      const before = (await chat.who(pf)).name;
      try { await playfab.setDisplayName(pf, name); } catch (e) { throw new AdminError(/NameNotAvailable|already/i.test(String(e && (e.code || e.message))) ? 'name_taken' : 'playfab_failed', 409); }
      chat.forgetPerson(pf);
      store.audit('rename', before + ' -> ' + name, pf);
      return send(res, 200, { ok: true, name });
    }
    if (rest === '/api/players/ban' && method === 'POST') {
      if (!playfab || !playfab.banUser) throw new AdminError('no_playfab', 404);
      const b = await readJson(req); const pf = String(b.pf || '');
      if (!PF_ID.test(pf)) throw new AdminError('bad_player');
      const reason = text(b.reason, 140);
      if (reason.length < 3) throw new AdminError('reason_required');
      const hours = Math.floor(Number(b.hours) || 0);
      if (!(hours >= 0 && hours <= 24 * 365)) throw new AdminError('bad_hours');
      try { await playfab.banUser(pf, reason, hours); } catch { throw new AdminError('playfab_failed', 409); }
      store.setBan(pf, { at: now(), reason, until: hours ? now() + hours * 3600000 : 0 });
      store.audit('ban', (hours ? hours + 'h' : 'forever') + ' · ' + reason, pf);
      return send(res, 200, { ok: true });
    }
    if (rest === '/api/players/unban' && method === 'POST') {
      if (!playfab || !playfab.unbanUser) throw new AdminError('no_playfab', 404);
      const b = await readJson(req); const pf = String(b.pf || '');
      if (!PF_ID.test(pf)) throw new AdminError('bad_player');
      try { await playfab.unbanUser(pf); } catch { throw new AdminError('playfab_failed', 409); }
      store.setBan(pf, null); store.audit('unban', '', pf);
      return send(res, 200, { ok: true });
    }

    // ---- balance of a player (add or take coins / gems), always with a reason
    if (rest === '/api/players/balance' && method === 'POST') {
      if (!playfab || !playfab.addCurrency) throw new AdminError('no_playfab', 404);
      const b = await readJson(req);
      const pf = String(b.pf || '');
      if (!PF_ID.test(pf)) throw new AdminError('bad_player');
      const amount = Math.floor(Number(b.amount));
      if (!(amount >= 1 && amount <= 1000000)) throw new AdminError('bad_amount');
      const reason = text(b.reason, 100);
      if (reason.length < 3) throw new AdminError('reason_required');
      const code = b.currency === 'gems' ? (cfg.gemsCode || 'GM') : b.currency === 'coins' ? cfg.currencyCode : null;
      if (!code) throw new AdminError('bad_currency');
      if (b.op !== 'add' && b.op !== 'remove') throw new AdminError('bad_op');
      try { if (b.op === 'add') await playfab.addCurrency(pf, code, amount); else await playfab.subtractCurrency(pf, code, amount); }
      catch (e) { throw new AdminError(b.op === 'remove' ? 'insufficient_or_failed' : 'playfab_failed', 409); }
      store.audit('balance_' + b.op, pf + ' ' + (b.op === 'add' ? '+' : '-') + amount + ' ' + b.currency + ' · ' + reason, pf);
      return send(res, 200, { ok: true });
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
