'use strict';
// OFFICIAL ROOMS: voice rooms that belong to the game's team (the "Mafia Office" room for new players, a support room for the staff...).
// An official room is a definition kept here (name, size, mic seats, admins, chat settings); its EOS lobby is kept alive by the room bot (a small
// program that runs next to this service and reports through the bot routes), so the room is always in the list. Players see it pinned at the top.
// The bot sends who is inside and how the stage looks every few seconds; this module turns that into the board's numbers and room log, and hands the
// bot the commands the admin gave (kick, take down from the mic...). Nothing here hears or records voice.

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

const PF_ID = /^[A-Za-z0-9]{6,32}$/;
const KEEP_LOG = 600;
const HISTORY = 50;
const FRESH_MS = 30 * 1000;          // the bot has to report at least this often for the room to count as open
const PRESENT_MS = 90 * 1000;        // a player who did not ping for this long is no longer in the room's chat
const COMMANDS = ['kick', 'down', 'mute'];

class RoomError extends Error {
  constructor(code, status = 400) { super(code); this.code = code; this.status = status; }
}

const text = (v, max) => (typeof v === 'string' ? v.trim().slice(0, max) : '');
const int = (v, lo, hi, dflt) => { const n = Number(v); return Number.isFinite(n) ? Math.min(hi, Math.max(lo, Math.round(n))) : dflt; };

function createOfficialRooms({ file = null, now = () => Date.now(), chat = null, isNew = null, log = console } = {}) {
  let state = { nextId: 1001, rooms: {}, log: [] };
  try { if (file) state = Object.assign(state, JSON.parse(fs.readFileSync(file, 'utf8'))); } catch { /* first run */ }
  let timer = null;
  function flushNow() {
    if (!file) return;
    try { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file + '.tmp', JSON.stringify(state)); fs.renameSync(file + '.tmp', file); }
    catch (e) { log.error('official rooms save failed', e.message); }
  }
  function save() { if (!file || timer) return; timer = setTimeout(() => { timer = null; flushNow(); }, 400); timer.unref?.(); }

  const beats = new Map();         // room id -> { at, lobbyId, members: [{ puid, pf, name }], stage: { head, seats, queue } } (the bot's last report)
  const present = new Map();       // pf -> { room, at } (who is inside a room's chat right now)
  let commands = [];               // pending orders for the bot: { id, room, kind, puid, pf, at }

  function note(room, kind, text2, pf = '', name = '') {
    state.log.push({ ts: now(), room, kind, pf, name: String(name || '').slice(0, 40), text: String(text2 || '').slice(0, 120) });
    if (state.log.length > KEEP_LOG) state.log.splice(0, state.log.length - KEEP_LOG);
    save();
  }

  // ------------------------------------------------------------------ the definitions

  function clean(b, id, old = {}) {
    if (!b || typeof b !== 'object') throw new RoomError('bad_room');
    const r = {
      id,
      name: text(b.name, 40), nameEn: text(b.nameEn, 40), sub: text(b.sub, 70), subEn: text(b.subEn, 70),
      capacity: int(b.capacity, 4, 64, old.capacity || 64), micSeats: int(b.micSeats, 2, 12, old.micSeats || 8),
      micMode: b.micMode === 'free' ? 'free' : 'request',
      pinned: b.pinned !== false, hidden: !!b.hidden, chatEnabled: b.chatEnabled !== false,
      slowMs: int(b.slowSeconds !== undefined ? Number(b.slowSeconds) * 1000 : b.slowMs, 0, 60000, old.slowMs !== undefined ? old.slowMs : 3000),
      welcome: text(b.welcome, 220), welcomeEn: text(b.welcomeEn, 220),
      admins: Array.isArray(old.admins) ? old.admins.slice() : [],
      createdAt: old.createdAt || now(),
    };
    if (!r.name && !r.nameEn) throw new RoomError('name_required');
    if (!r.name) r.name = r.nameEn;
    return r;
  }

  function create(b) {
    const id = String(state.nextId++);
    state.rooms[id] = clean(b, id);
    note(id, 'room', 'أُنشئت الغرفة');
    save();
    return state.rooms[id];
  }

  function update(id, b) {
    const old = state.rooms[id];
    if (!old) throw new RoomError('not_found', 404);
    state.rooms[id] = Object.assign(clean(b, id, old), { admins: old.admins, createdAt: old.createdAt });
    note(id, 'room', 'تغيّرت إعدادات الغرفة');
    save();
    return state.rooms[id];
  }

  function remove(id) {
    if (!state.rooms[id]) throw new RoomError('not_found', 404);
    delete state.rooms[id]; beats.delete(id);
    commands = commands.filter(c => c.room !== id);
    for (const [pf, p] of present) if (p.room === id) present.delete(pf);
    save();
  }

  function setAdmin(id, pf, on) {
    const r = state.rooms[id];
    if (!r) throw new RoomError('not_found', 404);
    if (!PF_ID.test(String(pf))) throw new RoomError('bad_player');
    const has = r.admins.includes(pf);
    if (on && !has) { r.admins.push(pf); note(id, 'admin', 'انضاف أدمن', pf); }
    if (!on && has) { r.admins = r.admins.filter(a => a !== pf); note(id, 'admin', 'انشال أدمن', pf); }
    save();
    return r.admins.slice();
  }

  const room = id => state.rooms[String(id)] || null;
  const isStaff = (pf, id) => { const r = room(id); return !!r && r.admins.includes(pf); };

  // ------------------------------------------------------------------ what the players see

  function isOpen(id) { const b = beats.get(String(id)); return !!b && !!b.lobbyId && now() - b.at < FRESH_MS; }
  function members(id) { const b = beats.get(String(id)); return b && now() - b.at < FRESH_MS ? b.members : []; }
  const visible = (r, pf) => !r.hidden || r.admins.includes(pf);

  /** The rooms a player sees, pinned ones first. The admins' ids are public on purpose: the game needs them to know who runs the stage. */
  function list(pf) {
    const out = Object.values(state.rooms).filter(r => visible(r, pf)).map(r => {
      const m = members(r.id);
      const b = beats.get(r.id);
      return {
        id: r.id, name: r.name, nameEn: r.nameEn, sub: r.sub, subEn: r.subEn, capacity: r.capacity, micSeats: r.micSeats, micMode: r.micMode,
        pinned: r.pinned, hidden: r.hidden, chatEnabled: r.chatEnabled, welcome: r.welcome, welcomeEn: r.welcomeEn,
        lobbyId: isOpen(r.id) ? b.lobbyId : '', online: m.length, adminsOnline: m.filter(x => r.admins.includes(x.pf)).length, admins: r.admins.slice(), staff: r.admins.includes(pf),
      };
    });
    out.sort((a, b) => (b.pinned - a.pinned) || (Number(a.id) - Number(b.id)));
    return { ok: true, rooms: out };
  }

  // ------------------------------------------------------------------ the room's chat (chat.js asks these questions)

  /** Entering the chat of a room: remembers that the player is inside and returns the last lines for him. */
  function enter(pf, id) {
    const r = room(id);
    if (!r || !visible(r, pf)) throw new RoomError('not_found', 404);
    present.set(pf, { room: r.id, at: now() });
    const history = chat ? chat.history('room:' + r.id, HISTORY) : [];
    return { ok: true, welcome: r.welcome, welcomeEn: r.welcomeEn, history };
  }
  function ping(pf) { const p = present.get(pf); if (p) p.at = now(); return { ok: true, inside: !!p }; }
  function leave(pf) { present.delete(pf); return { ok: true }; }
  function inside(pf, id) { const p = present.get(pf); return !!p && p.room === String(id) && now() - p.at < PRESENT_MS; }

  /** Can this player read and write the chat of the room? Only while he is inside it (the poll would send every player every room otherwise). */
  const canRead = (pf, id) => !!room(id) && inside(pf, id);
  const chatOpen = id => { const r = room(id); return !!r && r.chatEnabled; };
  const slowMs = (pf, id) => { const r = room(id); return !r || r.admins.includes(pf) ? 0 : r.slowMs; };

  // ------------------------------------------------------------------ the room bot

  /**
   * One report of the bot: for each room it keeps, the lobby id, who is inside and how the stage looks. Differences become log lines;
   * the answer carries the commands waiting for it.
   */
  function beat(report) {
    const t = now();
    for (const rep of (report && Array.isArray(report.rooms) ? report.rooms : [])) {
      const r = room(rep.id);
      if (!r) continue;
      const mem = (Array.isArray(rep.members) ? rep.members : []).slice(0, 80).map(x => ({ puid: String(x.puid || '').slice(0, 64), pf: String(x.pf || '').slice(0, 32), name: String(x.name || '').slice(0, 40), bot: !!x.bot }));
      const old = beats.get(r.id);
      const stage = rep.stage && typeof rep.stage === 'object' ? { head: String(rep.stage.head || ''), seats: (Array.isArray(rep.stage.seats) ? rep.stage.seats : []).map(String).slice(0, 12), queue: (Array.isArray(rep.stage.queue) ? rep.stage.queue : []).map(String).slice(0, 64) } : { head: '', seats: [], queue: [] };
      if (old && t - old.at < FRESH_MS * 2) {
        const before = new Map(old.members.map(m => [m.puid, m])), after = new Map(mem.map(m => [m.puid, m]));
        for (const [puid, m] of after) if (!before.has(puid)) note(r.id, 'join', (m.pf && r.admins.includes(m.pf) ? 'دخل كأدمن' : 'دخل الغرفة') + (isNew && m.pf && isNew(m.pf) ? ' (لاعب جديد)' : '') + (m.bot ? ' [بوت]' : ''), m.pf, m.name);
        for (const [puid, m] of before) if (!after.has(puid)) note(r.id, 'leave', 'طلع من الغرفة', m.pf, m.name);
        const nameOf = puid => (after.get(puid) || before.get(puid) || {}).name || '';
        const pfOf = puid => (after.get(puid) || before.get(puid) || {}).pf || '';
        for (const q of stage.queue) if (!old.stage.queue.includes(q)) note(r.id, 'hand', 'رفع إيده', pfOf(q), nameOf(q));
        for (const s of stage.seats) if (s && !old.stage.seats.includes(s)) note(r.id, 'mic', 'صار على المايك', pfOf(s), nameOf(s));
        for (const s of old.stage.seats) if (s && !stage.seats.includes(s)) note(r.id, 'mic', 'نزل عن المايك', pfOf(s), nameOf(s));
      }
      beats.set(r.id, { at: t, lobbyId: String(rep.lobbyId || '').slice(0, 80), members: mem, stage });
    }
    const out = commands.splice(0, commands.length);
    return { ok: true, commands: out, rooms: Object.values(state.rooms).map(r => ({ id: r.id, name: r.name, capacity: r.capacity, micSeats: r.micSeats, micMode: r.micMode, chatEnabled: r.chatEnabled, admins: r.admins.slice(), hidden: r.hidden })) };
  }

  /** What the admin tells the bot to do to somebody in a room. */
  function command(id, kind, puid, pf, byLabel = '') {
    if (!room(id)) throw new RoomError('not_found', 404);
    if (!COMMANDS.includes(kind)) throw new RoomError('bad_command');
    if (!puid || String(puid).length > 64) throw new RoomError('bad_player');
    commands.push({ id: crypto.randomBytes(4).toString('hex'), room: String(id), kind, puid: String(puid), pf: String(pf || ''), at: now() });
    note(id, 'admin', { kick: 'انطرد', down: 'انحط تحت (نزّلوه من المايك)', mute: 'انكتم' }[kind] + (byLabel ? ' بواسطة ' + byLabel : ''), pf);
    if (commands.length > 200) commands.splice(0, commands.length - 200);
  }

  // ------------------------------------------------------------------ what the admin board shows

  function adminView() {
    const rooms = Object.values(state.rooms).map(r => {
      const b = beats.get(r.id);
      const m = members(r.id);
      return Object.assign({}, r, {
        open: isOpen(r.id), lobbyId: isOpen(r.id) ? b.lobbyId : '', botSeenAt: b ? b.at : 0,
        online: m.length, adminsOnline: m.filter(x => r.admins.includes(x.pf)).length,
        members: m.map(x => Object.assign({}, x, { staff: r.admins.includes(x.pf), isNew: !!(isNew && x.pf && isNew(x.pf)) })),
        stage: b ? b.stage : { head: '', seats: [], queue: [] },
      });
    });
    return { ok: true, rooms, botAlive: [...beats.values()].some(b => now() - b.at < FRESH_MS) };
  }
  /** The stage the bot last reported for a room (seats and head by EOS id), or null when the bot is silent. */
  const stageOf = id => { const b = beats.get(String(id)); return b && now() - b.at < FRESH_MS ? b.stage : null; };
  const logOf = (id, n = 100) => state.log.filter(l => String(l.room) === String(id)).slice(-n).reverse();

  function stats(id) {
    const day = now() - 24 * 3600 * 1000;
    const l = state.log.filter(x => String(x.room) === String(id) && x.ts > day);
    return { joinsToday: l.filter(x => x.kind === 'join').length, chatToday: chat ? chat.countSince('room:' + id, day) : 0 };
  }

  return { create, update, remove, setAdmin, room, isStaff, list, enter, ping, leave, inside, canRead, chatOpen, slowMs, beat, command, adminView, logOf, stats, flushNow, isOpen, members, stageOf, RoomError, note };
}

module.exports = { createOfficialRooms, RoomError };
