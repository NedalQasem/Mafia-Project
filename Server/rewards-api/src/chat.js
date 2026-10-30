'use strict';
// Text chat for the whole game: the public square, direct messages and small groups, with a slow mode, a word filter,
// reports, blocking and an automatic mute. Plain logic over an in-memory state that is saved to one JSON file.
// Players reach it through app.js (PlayFab session ticket = identity). Messages are delivered by long polling.

const fs = require('node:fs');
const path = require('node:path');
const DEFAULT_WORDS = require('./bannedwords');

const PUBLIC = 'public';
const RANK_THRESHOLDS = [0, 1000, 2000, 3000, 4000, 5000, 6000];   // same ladder as the game's RankRules

const PF_ID = /^[A-Za-z0-9]{6,32}$/;

class ChatError extends Error {
  constructor(code, status = 400, extra = {}) { super(code); this.code = code; this.status = status; this.extra = extra; }
}

function rankIndex(points) {
  let r = 0;
  for (let i = 1; i < RANK_THRESHOLDS.length; i++) if (points >= RANK_THRESHOLDS[i]) r = i;
  return r;
}

/** Makes Arabic spelling variants comparable (diacritics, tatweel, alef and yaa forms) so the word filter is hard to dodge. */
function normalize(s) {
  return String(s).toLowerCase()
    .replace(/[ً-ٰٟـ]/g, '')
    .replace(/[أإآٱ]/g, 'ا')
    .replace(/ى/g, 'ي')
    .replace(/ة/g, 'ه');
}

function dmChannel(a, b) { return 'dm:' + [a, b].sort().join(':'); }
function dmMembers(ch) { return ch.slice(3).split(':'); }

function createChat({ cfg, playfab, extraWords = null, now = () => Date.now(), log = console, file = null, reportsFile = null }) {
  const c = Object.assign({
    maxLen: 300, publicCooldownMs: 5000, cooldownMs: 1000, publicKeep: 300, convKeep: 200, historyPerChannel: 50,
    maxGroups: 10, maxGroupMembers: 10, autoMuteReports: 3, autoMuteMinutes: 120, bannedWords: [], bannedExact: [], useDefaultBannedWords: true, pollMaxMs: 25000, onlineWithinMs: 45000,
  }, cfg.chat || {});
  const dflt = c.useDefaultBannedWords !== false;
  const banned = [...(dflt ? DEFAULT_WORDS.CONTAINS : []), ...c.bannedWords].map(normalize).filter(Boolean);                 // hit anywhere inside a word
  const bannedExact = new Set([...(dflt ? DEFAULT_WORDS.EXACT : []), ...c.bannedExact].map(normalize).filter(Boolean));   // hit only as a whole word
  // words added from the admin board (extraWords() -> { exact: [], contains: [] }) are merged in without a restart
  let extraKey = '', extraContains = [], extraExact = new Set();
  function syncExtra() {
    const x = extraWords ? extraWords() : null;
    const key = x ? JSON.stringify(x) : '';
    if (key === extraKey) return;
    extraKey = key;
    extraContains = ((x && x.contains) || []).map(normalize).filter(Boolean);
    extraExact = new Set(((x && x.exact) || []).map(normalize).filter(Boolean));
  }

  let state = { nextId: 0, messages: { [PUBLIC]: [] }, groups: {}, reads: {}, blocks: {}, mutes: {}, reported: {} };
  try { if (file) state = Object.assign(state, JSON.parse(fs.readFileSync(file, 'utf8'))); } catch { /* first run */ }
  const lastSend = new Map();           // pf -> { ch -> ms }
  const lastSeen = new Map();           // pf -> ms of the last poll
  const waiters = new Map();            // pf -> { since, resolve, timer }
  const people = new Map();             // pf -> { name, rank, level, at }
  const events = new Map();             // pf -> [event]: short-lived news for a player (friend requests, invites) delivered with his next poll
  let saveTimer = null;

  function save() {
    if (!file || saveTimer) return;
    saveTimer = setTimeout(() => {
      saveTimer = null;
      try { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file + '.tmp', JSON.stringify(state)); fs.renameSync(file + '.tmp', file); }
      catch (e) { log.error('chat save failed', e.message); }
    }, 500);
    saveTimer.unref?.();
  }

  // ---------------------------------------------------------------- who is who

  async function who(pf) {
    const cached = people.get(pf);
    if (cached && now() - cached.at < 10 * 60 * 1000) return cached;
    let name = 'لاعب', rank = 0, level = 1;
    try { name = (await playfab.getDisplayName(pf)) || name; } catch { /* keep the default */ }
    try { const p = await playfab.getProgress(pf, 'Progress'); if (p) { rank = rankIndex(p.rankPoints || 0); level = p.level || 1; } } catch { /* keep the default */ }
    const entry = { name, rank, level, at: now() };
    people.set(pf, entry);
    return entry;
  }

  // ---------------------------------------------------------------- filter and rules

  function clean(text) {
    let t = String(text == null ? '' : text).replace(/[\u0000-\u0008\u000B-\u001F\u007F]/g, ' ').replace(/\s+/g, ' ').trim();
    if (!t) throw new ChatError('empty');
    if (t.length > c.maxLen) t = t.slice(0, c.maxLen);
    return t;
  }

  /** Hides banned words behind stars: each word is compared after normalizing its spelling. (Letters split by spaces are not caught; reports cover that.) */
  function filter(text) {
    syncExtra();
    if (!banned.length && !bannedExact.size && !extraContains.length && !extraExact.size) return text;
    return text.split(' ').map(word => {
      const n = normalize(word);
      const bare = n.replace(/^[^\p{L}\p{N}]+|[^\p{L}\p{N}]+$/gu, '');          // "كس!" is still the word "كس"
      return bannedExact.has(bare) || extraExact.has(bare) || banned.some(w => n.includes(w)) || extraContains.some(w => n.includes(w)) ? '*'.repeat(word.length) : word;
    }).join(' ');
  }

  function checkMute(pf) {
    const until = state.mutes[pf];
    if (until && until > now()) throw new ChatError('muted', 403, { retryMs: until - now() });
    if (until) { delete state.mutes[pf]; save(); }
  }

  function checkCooldown(pf, ch) {
    const gap = ch === PUBLIC ? c.publicCooldownMs : c.cooldownMs;
    const mine = lastSend.get(pf) || {};
    const wait = (mine[ch] || 0) + gap - now();
    if (wait > 0) throw new ChatError('slow', 429, { retryMs: wait });
    mine[ch] = now(); lastSend.set(pf, mine);
  }

  function blocked(by, other) { return (state.blocks[by] || []).includes(other); }

  // ---------------------------------------------------------------- reading rules

  function canSee(pf, ch) {
    if (ch === PUBLIC) return true;
    if (ch.startsWith('dm:')) return dmMembers(ch).includes(pf);
    if (ch.startsWith('grp:')) { const g = state.groups[ch.slice(4)]; return !!g && g.members.includes(pf); }
    return false;
  }

  function visibleChannels(pf) {
    const list = [PUBLIC];
    for (const ch of Object.keys(state.messages)) if (ch !== PUBLIC && canSee(pf, ch)) list.push(ch);
    for (const gid of Object.keys(state.groups)) if (state.groups[gid].members.includes(pf) && !list.includes('grp:' + gid)) list.push('grp:' + gid);
    return list;
  }

  function collect(pf, since, history) {
    const out = [];
    for (const ch of visibleChannels(pf)) {
      let msgs = (state.messages[ch] || []).filter(m => m.id > since);
      if (history && msgs.length > c.historyPerChannel) msgs = msgs.slice(-c.historyPerChannel);
      if (ch === PUBLIC) msgs = msgs.filter(m => !blocked(pf, m.from));
      out.push(...msgs);
    }
    out.sort((a, b) => a.id - b.id);
    return out;
  }

  function pack(m) { return { id: m.id, ch: m.ch, from: m.from, name: m.name, rank: m.rank, text: m.text, ts: m.ts, sys: m.sys ? 1 : 0 }; }

  // ---------------------------------------------------------------- sending

  function addMessage(ch, from, name, rank, text, sys) {
    const m = { id: ++state.nextId, ch, from, name, rank, text, ts: now(), sys: !!sys };
    const list = (state.messages[ch] = state.messages[ch] || []);
    list.push(m);
    const keep = ch === PUBLIC ? c.publicKeep : c.convKeep;
    if (list.length > keep) list.splice(0, list.length - keep);
    save();
    // wake the players who are waiting for news and may see this message
    for (const [pf, w] of [...waiters]) {
      if (!canSee(pf, ch)) continue;
      if (ch === PUBLIC && blocked(pf, from)) continue;
      clearTimeout(w.timer); waiters.delete(pf);
      w.resolve({ cursor: Math.max(w.since, m.id), messages: collect(pf, w.since, false).map(pack), events: takeEvents(pf) });
    }
    return m;
  }

  async function send(pf, { channel, text }) {
    if (typeof channel !== 'string') throw new ChatError('bad_channel');
    let ch = channel;
    if (ch !== PUBLIC) {
      if (ch.startsWith('dm:')) {
        const other = ch.slice(3);
        if (!PF_ID.test(other) || other === pf) throw new ChatError('bad_channel');
        ch = dmChannel(pf, other);
      } else if (ch.startsWith('grp:')) {
        if (!canSee(pf, ch)) throw new ChatError('not_member', 403);
      } else throw new ChatError('bad_channel');
    }
    const t = clean(text);
    checkMute(pf);
    if (ch.startsWith('dm:')) {
      const other = dmMembers(ch).find(x => x !== pf);
      if (blocked(pf, other)) throw new ChatError('you_blocked', 403);
      checkCooldown(pf, ch);
      if (blocked(other, pf)) return { ok: true, id: 0, ch, text: filter(t), dropped: true };   // looks delivered to the sender, never reaches the other
    } else checkCooldown(pf, ch);
    const me = await who(pf);
    const m = addMessage(ch, pf, me.name, me.rank, filter(t), false);
    return { ok: true, id: m.id, ch, ts: m.ts, text: m.text };
  }

  // ---------------------------------------------------------------- polling

  async function poll(pf, since, waitMs, history) {
    lastSeen.set(pf, now());
    since = Number.isFinite(since) && since >= 0 ? since : 0;
    const msgs = collect(pf, since, history || since === 0);
    const evs = takeEvents(pf);
    if (msgs.length || evs.length || !waitMs) return { cursor: msgs.length ? msgs[msgs.length - 1].id : (since === 0 ? state.nextId : since), messages: msgs.map(pack), events: evs };
    const old = waiters.get(pf);
    if (old) { clearTimeout(old.timer); old.resolve({ cursor: since, messages: [], events: [] }); }
    return new Promise(resolve => {
      const timer = setTimeout(() => { waiters.delete(pf); lastSeen.set(pf, now()); resolve({ cursor: since, messages: [], events: [] }); }, Math.min(waitMs, c.pollMaxMs));
      timer.unref?.();
      waiters.set(pf, { since, resolve, timer });
    });
  }

  // ---------------------------------------------------------------- news for a player (used by the friends part)

  function takeEvents(pf) { const l = events.get(pf); if (!l || !l.length) return []; events.delete(pf); return l; }

  /** Queues a piece of news for a player and wakes his waiting poll. */
  function pushEvent(pf, evt) {
    const list = events.get(pf) || [];
    list.push(Object.assign({ at: now() }, evt));
    if (list.length > 50) list.shift();
    events.set(pf, list);
    const w = waiters.get(pf);
    if (w) { clearTimeout(w.timer); waiters.delete(pf); w.resolve({ cursor: w.since, messages: [], events: takeEvents(pf) }); }
  }

  function isOnline(pf) { return now() - (lastSeen.get(pf) || 0) < c.onlineWithinMs; }
  function seenAt(pf) { return lastSeen.get(pf) || 0; }
  function touch(pf) { lastSeen.set(pf, now()); }
  function onlineCount() { let n = 0; const t = now(); for (const at of lastSeen.values()) if (t - at < c.onlineWithinMs) n++; return n; }

  // ---------------------------------------------------------------- the list of conversations

  async function inbox(pf) {
    const reads = state.reads[pf] || {};
    const items = [];
    for (const ch of visibleChannels(pf)) {
      if (ch === PUBLIC) continue;
      const msgs = state.messages[ch] || [];
      const last = msgs[msgs.length - 1] || null;
      const unread = msgs.filter(m => m.id > (reads[ch] || 0) && m.from !== pf && !m.sys).length;
      if (ch.startsWith('dm:')) {
        const other = dmMembers(ch).find(x => x !== pf);
        const p = await who(other);
        items.push({ ch, kind: 'dm', other, name: p.name, rank: p.rank, online: now() - (lastSeen.get(other) || 0) < c.onlineWithinMs, unread, last: last && pack(last) });
      } else {
        const g = state.groups[ch.slice(4)];
        const members = [];
        for (const m of g.members) { const p = await who(m); members.push({ pf: m, name: p.name }); }
        items.push({ ch, kind: 'group', name: g.name, owner: g.owner, members, unread, last: last && pack(last) });
      }
    }
    items.sort((a, b) => ((b.last && b.last.id) || 0) - ((a.last && a.last.id) || 0));
    return { ok: true, conversations: items, cursor: state.nextId };
  }

  function markRead(pf, ch, upTo) {
    if (!canSee(pf, ch)) throw new ChatError('not_member', 403);
    const r = (state.reads[pf] = state.reads[pf] || {});
    r[ch] = Math.max(r[ch] || 0, Number.isFinite(upTo) ? upTo : state.nextId);
    save();
    return { ok: true };
  }

  // ---------------------------------------------------------------- groups

  async function createGroup(pf, { name, members }) {
    let n = clean(name);
    if (n.length < 2 || n.length > 24) throw new ChatError('bad_name');
    n = filter(n);
    if (!Array.isArray(members)) throw new ChatError('bad_members');
    const ids = [...new Set(members.filter(m => typeof m === 'string' && PF_ID.test(m) && m !== pf))];
    if (ids.length < 1 || ids.length + 1 > c.maxGroupMembers) throw new ChatError('bad_members');
    const mine = Object.values(state.groups).filter(g => g.members.includes(pf)).length;
    if (mine >= c.maxGroups) throw new ChatError('too_many_groups', 403);
    for (const m of ids) { try { await playfab.getDisplayName(m); } catch { throw new ChatError('unknown_player'); } }
    const gid = Math.random().toString(16).slice(2, 10) + Math.random().toString(16).slice(2, 6);
    state.groups[gid] = { id: gid, name: n, owner: pf, members: [pf, ...ids] };
    const me = await who(pf);
    addMessage('grp:' + gid, pf, me.name, me.rank, me.name + ' أنشأ المجموعة', true);
    return { ok: true, id: gid, ch: 'grp:' + gid };
  }

  async function leaveGroup(pf, gid) {
    const g = state.groups[gid];
    if (!g || !g.members.includes(pf)) throw new ChatError('not_member', 403);
    const me = await who(pf);
    addMessage('grp:' + gid, pf, me.name, me.rank, me.name + ' غادر المجموعة', true);
    g.members = g.members.filter(m => m !== pf);
    if (g.members.length === 0) { delete state.groups[gid]; delete state.messages['grp:' + gid]; }
    else if (g.owner === pf) g.owner = g.members[0];
    save();
    return { ok: true };
  }

  // ---------------------------------------------------------------- blocking and reports

  function block(pf, other, on) {
    if (typeof other !== 'string' || !PF_ID.test(other) || other === pf) throw new ChatError('bad_player');
    const list = (state.blocks[pf] = state.blocks[pf] || []);
    const i = list.indexOf(other);
    if (on && i === -1) list.push(other); else if (!on && i !== -1) list.splice(i, 1);
    save();
    return { ok: true, blocked: list.length };
  }

  function blockedList(pf) { return { ok: true, blocked: [...(state.blocks[pf] || [])] }; }

  function report(pf, { channel, messageId, reason }) {
    if (!canSee(pf, channel)) throw new ChatError('not_member', 403);
    const m = (state.messages[channel] || []).find(x => x.id === messageId);
    if (!m || m.sys) throw new ChatError('no_message', 404);
    if (m.from === pf) throw new ChatError('own_message');
    const key = pf + ':' + m.id;
    if (state.reported[key]) return { ok: true, duplicate: true };
    state.reported[key] = now();
    const entry = { ts: new Date(now()).toISOString(), reporter: pf, target: m.from, channel, messageId: m.id, text: m.text, reason: String(reason || '').slice(0, 100) };
    if (reportsFile) { try { fs.mkdirSync(path.dirname(reportsFile), { recursive: true }); fs.appendFileSync(reportsFile, JSON.stringify(entry) + '\n'); } catch (e) { log.error('report write failed', e.message); } }
    // enough different players reporting the same person in a day: mute him for a while
    const dayAgo = now() - 24 * 3600 * 1000;
    const reporters = new Set();
    for (const [k, t] of Object.entries(state.reported)) {
      if (t < dayAgo) continue;
      const [rep, id] = k.split(':');
      const msg = findMessageById(Number(id));
      if (msg && msg.from === m.from) reporters.add(rep);
    }
    let muted = false;
    if (reporters.size >= c.autoMuteReports) { state.mutes[m.from] = now() + c.autoMuteMinutes * 60 * 1000; muted = true; }
    save();
    return { ok: true, muted };
  }

  function findMessageById(id) {
    for (const list of Object.values(state.messages)) { const m = list.find(x => x.id === id); if (m) return m; }
    return null;
  }

  /** Forget reports and sends that are old (called now and then). */
  function purge() {
    const dayAgo = now() - 24 * 3600 * 1000;
    for (const [k, t] of Object.entries(state.reported)) if (t < dayAgo) delete state.reported[k];
    for (const [pf, until] of Object.entries(state.mutes)) if (until < now()) delete state.mutes[pf];
    save();
  }

  function flushNow() {
    if (!file) return;
    try { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file + '.tmp', JSON.stringify(state)); fs.renameSync(file + '.tmp', file); } catch (e) { log.error('chat save failed', e.message); }
  }

  // ---------------------------------------------------------------- for the admin board

  /** Mutes a player for some minutes (0 lifts the mute). Returns the time the mute ends (ms), or 0. */
  function adminMute(pf, minutes) {
    if (!PF_ID.test(String(pf))) throw new ChatError('bad_player');
    const m = Math.max(0, Math.min(10080, Math.floor(Number(minutes) || 0)));
    if (m === 0) delete state.mutes[pf]; else state.mutes[pf] = now() + m * 60 * 1000;
    save();
    return state.mutes[pf] || 0;
  }
  /** Forgets the cached name of a player (after the admin renamed him). */
  function forgetPerson(pf) { people.delete(pf); }
  function muteUntil(pf) { const u = state.mutes[pf] || 0; return u > now() ? u : 0; }
  /** Players seen since the service started whose name contains the text (the names are not stored anywhere else). */
  function searchPeople(text) {
    const q = normalize(String(text || '').trim());
    if (!q) return [];
    const out = [];
    for (const [pf, p] of people) if (normalize(p.name).includes(q)) out.push({ pf, name: p.name, rank: p.rank, level: p.level });
    return out.slice(0, 20);
  }

  return { who, pushEvent, isOnline, onlineCount, seenAt, touch, send, poll, inbox, markRead, createGroup, leaveGroup, block, blockedList, report, purge, flushNow, adminMute, muteUntil, searchPeople, forgetPerson, ChatError, _state: () => state };
}

module.exports = { createChat, ChatError, normalize, rankIndex, dmChannel, PUBLIC };
