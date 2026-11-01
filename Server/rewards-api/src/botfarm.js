'use strict';
// BOTS: made-up players the admin puts into rooms so the game can be tried alone and the rooms are not empty at the launch.
// This module is only the register: the bot profiles (name, level, style, chat lines), the orders ("3 bots into this room, sitting, chatting, playing"),
// the launch mode ("keep every public room at 4 members") and what the bot program reports it is doing. The bot program (a .NET service next to this one)
// reads the wanted state in the answer of every /v1/bot/beat and makes it true. A bot is not a PlayFab account: it never earns coins, rank points or
// a place on a leaderboard, never writes to anybody's mail and never sends a friend request.

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

class BotError extends Error {
  constructor(code, status = 400) { super(code); this.code = code; this.status = status; }
}

const FIRST = ['أحمد', 'محمد', 'خالد', 'سامر', 'مازن', 'عادل', 'يزن', 'رامي', 'فادي', 'سليم', 'هاني', 'زياد', 'نبيل', 'جاد', 'كريم', 'ليث', 'وليد', 'باسل', 'طارق', 'ماهر', 'لينا', 'سارة', 'ريم', 'نور', 'هديل', 'رنا', 'دانة', 'ميس', 'هبة', 'جود', 'سلمى', 'ريما', 'لمى', 'دعاء', 'أسيل'];
const NICK = ['أبو فارس', 'أبو سامر', 'أبو خليل', 'أبو جاسم', 'أم سعيد', 'أم خالد', 'الحج حسن', 'البيك', 'الأستاذ', 'الريّس', 'أبو الشباب', 'العم صالح'];
const STYLES = ['quiet', 'normal', 'chatty'];
const LINES = [
  'مساء الخير', 'أهلين فيكم', 'شو الأخبار؟', 'مين بدو يلعب دور؟', 'أنا معكم', 'يلا نبلّش', 'والله سهرة حلوة', 'مين المافيا بدي أعرف 😄', 'ما بتنخدع بسهولة',
  'كم لاعب بدنا؟', 'جاهز', 'لحظة بس', 'تمام', 'حلو كتير', 'هههه', 'أنا أول مرة بلعب هيك', 'مين بيشرحلي القواعد؟', 'الله يعطيكم العافية', 'نعيدها؟', 'دوري كان صعب هالمرة',
];
const MATCH_LINES = ['أنا شاكك بحدا', 'ما بتصدّقوني بس أنا مواطن', 'خلينا نسمع من الكل', 'مين الليلة قتلوه؟', 'صوتي عليه', 'ما كنت أنا والله'];

const text = (v, max) => (typeof v === 'string' ? v.trim().slice(0, max) : '');
const int = (v, lo, hi, d) => { const n = Number(v); return Number.isFinite(n) ? Math.min(hi, Math.max(lo, Math.round(n))) : d; };
const rankOfLevel = l => Math.min(7, Math.floor((l - 1) / 4));
const pick = (arr, n) => { const a = arr.slice(); for (let i = a.length - 1; i > 0; i--) { const j = crypto.randomInt(i + 1); [a[i], a[j]] = [a[j], a[i]]; } return a.slice(0, n); };

function createBotFarm({ file = null, now = () => Date.now(), log = console } = {}) {
  let state = {
    nextProfile: 1, nextOrder: 1, profiles: [], orders: [],
    launch: { enabled: false, minPerRoom: 4, leaveWhenReal: true, fromHour: 16, toHour: 2, maxBots: 30 },
    matchLines: MATCH_LINES.slice(),
  };
  try { if (file) state = Object.assign(state, JSON.parse(fs.readFileSync(file, 'utf8'))); } catch { /* first run */ }
  let timer = null;
  function flushNow() {
    if (!file) return;
    try { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file + '.tmp', JSON.stringify(state)); fs.renameSync(file + '.tmp', file); }
    catch (e) { log.error('bot farm save failed', e.message); }
  }
  function save() { if (!file || timer) return; timer = setTimeout(() => { timer = null; flushNow(); }, 400); timer.unref?.(); }

  let last = { at: 0, lobbies: [], bots: [], real: 0 };       // what the bot program reported

  // ------------------------------------------------------------------ profiles

  function cleanProfile(b, id, old = {}) {
    if (!b || typeof b !== 'object') throw new BotError('bad_profile');
    const name = text(b.name, 24);
    if (!name) throw new BotError('name_required');
    const level = int(b.level, 1, 40, old.level || 5);
    return {
      id, name, level, rank: rankOfLevel(level), style: STYLES.includes(b.style) ? b.style : (old.style || 'normal'),
      lines: Array.isArray(b.lines) ? b.lines.map(l => text(l, 120)).filter(Boolean).slice(0, 30) : (old.lines || pick(LINES, 8)),
    };
  }

  function saveProfile(b) {
    if (b.id) {
      const i = state.profiles.findIndex(p => p.id === String(b.id));
      if (i < 0) throw new BotError('not_found', 404);
      state.profiles[i] = cleanProfile(b, state.profiles[i].id, state.profiles[i]);
      save(); return state.profiles[i];
    }
    const p = cleanProfile(b, 'b' + state.nextProfile++);
    if (state.profiles.some(x => x.name === p.name)) throw new BotError('name_taken');
    state.profiles.push(p); save(); return p;
  }

  function removeProfile(id) {
    const i = state.profiles.findIndex(p => p.id === String(id));
    if (i < 0) throw new BotError('not_found', 404);
    state.profiles.splice(i, 1);
    save();
  }

  /** Makes up to <count> new bots with Arabic names, levels in a range and random styles. Names already used are skipped. */
  function generate({ count = 10, levelFrom = 2, levelTo = 15 } = {}) {
    count = int(count, 1, 40, 10); levelFrom = int(levelFrom, 1, 40, 2); levelTo = Math.max(levelFrom, int(levelTo, 1, 40, 15));
    const used = new Set(state.profiles.map(p => p.name));
    const made = [];
    for (let tries = 0; made.length < count && tries < count * 20; tries++) {
      const name = crypto.randomInt(5) === 0 ? NICK[crypto.randomInt(NICK.length)] : FIRST[crypto.randomInt(FIRST.length)] + (crypto.randomInt(3) === 0 ? ' ' + 'الشامي النجار حداد قاسم زيدان'.split(' ')[crypto.randomInt(5)] : '');
      if (used.has(name)) continue;
      used.add(name);
      const level = levelFrom + crypto.randomInt(levelTo - levelFrom + 1);
      made.push(saveProfile({ name, level, style: STYLES[crypto.randomInt(3)] }));
    }
    return made;
  }

  // ------------------------------------------------------------------ orders

  /**
   * "Put <count> bots into this room." room is "official:1001" (a room of officialrooms.js) or "lobby:<EOS lobby id>" (any room a player made).
   * sit: they take mic seats (muted); chat: they write now and then; play: they take part in the match when it starts; startWhenFull: the host is
   * a real player and the order fills the room so a test match can start at once (the board's "complete with bots and start" button).
   */
  function addOrder(b) {
    const room = text(b && b.room, 90);
    if (!/^(official:\d{1,6}|lobby:[A-Za-z0-9_-]{4,80})$/.test(room)) throw new BotError('bad_room');
    const count = int(b.count, 1, 30, 1);
    const used = state.orders.reduce((n, o) => n + o.count, 0);
    if (used + count > state.launch.maxBots + 20) throw new BotError('too_many');
    const o = { id: 'o' + state.nextOrder++, room, count, sit: b.sit !== false, chat: b.chat !== false, play: b.play !== false, fillTo: int(b.fillTo, 0, 16, 0), at: now() };
    state.orders.push(o); save(); return o;
  }

  function removeOrder(id) {
    const i = state.orders.findIndex(o => o.id === String(id));
    if (i < 0) throw new BotError('not_found', 404);
    state.orders.splice(i, 1); save();
  }

  function setLaunch(b) {
    const l = state.launch;
    l.enabled = !!b.enabled; l.minPerRoom = int(b.minPerRoom, 1, 16, l.minPerRoom); l.leaveWhenReal = b.leaveWhenReal !== false;
    l.fromHour = int(b.fromHour, 0, 23, l.fromHour); l.toHour = int(b.toHour, 0, 23, l.toHour); l.maxBots = int(b.maxBots, 0, 60, l.maxBots);
    save(); return l;
  }

  // ------------------------------------------------------------------ the bot program

  /** One report of the bot program: the rooms it sees, the bots it runs, how many real players there are. Answers with what is wanted. */
  function beat(report) {
    const r = report && typeof report === 'object' ? report : {};
    last = {
      at: now(),
      lobbies: (Array.isArray(r.lobbies) ? r.lobbies : []).slice(0, 100).map(l => ({ id: String(l.id || '').slice(0, 80), name: String(l.name || '').slice(0, 40), count: int(l.count, 0, 99, 0), capacity: int(l.capacity, 0, 99, 0), mafia: !!l.mafia, locked: !!l.locked, inMatch: !!l.inMatch, real: int(l.real, 0, 99, 0) })),
      bots: (Array.isArray(r.bots) ? r.bots : []).slice(0, 100).map(b => ({ id: String(b.id || '').slice(0, 20), room: String(b.room || '').slice(0, 90), status: String(b.status || '').slice(0, 30), role: String(b.role || '').slice(0, 20) })),
      real: int(r.real, 0, 99999, 0),
    };
    return { profiles: state.profiles, orders: state.orders, launch: state.launch, matchLines: state.matchLines };
  }

  const alive = () => now() - last.at < 30 * 1000;

  function view() {
    return {
      ok: true, alive: alive(), profiles: state.profiles, orders: state.orders, launch: state.launch,
      lobbies: alive() ? last.lobbies : [], running: alive() ? last.bots : [], real: alive() ? last.real : 0,
      stats: { total: state.profiles.length, running: alive() ? last.bots.length : 0, rooms: alive() ? new Set(last.bots.map(b => b.room)).size : 0, matches: alive() ? last.bots.filter(b => b.status === 'match').length : 0 },
    };
  }

  return { saveProfile, removeProfile, generate, addOrder, removeOrder, setLaunch, beat, view, flushNow, BotError };
}

module.exports = { createBotFarm, BotError };
