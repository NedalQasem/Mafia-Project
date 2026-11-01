'use strict';
// The family (friends) of each player: a short id number to add somebody by, friend requests, the friends list with who is online
// and which room he sits in, and invitations to a room. News (a request arrived, an invite) reaches the other player with his next chat poll.

const fs = require('node:fs');
const path = require('node:path');
const { ChatError } = require('./chat');

const NUM = /^[0-9]{6}$/;
const PF_ID = /^[A-Za-z0-9]{6,32}$/;

function createFriends({ chat, cfg = {}, now = () => Date.now(), log = console, file = null, reportsFile = null }) {
  const f = Object.assign({ maxFriends: 12, inviteGapMs: 20000, requestsPerDay: 30, presenceMs: 90000 }, cfg.friends || {});
  let state = { nums: {}, byNum: {}, friends: {}, requests: {}, seen: {}, sent: {} };
  try { if (file) state = Object.assign(state, JSON.parse(fs.readFileSync(file, 'utf8'))); } catch { /* first run */ }
  const presence = new Map();          // pf -> { at, room }
  const lastInvite = new Map();        // "from>to" -> ms
  let timer = null;

  function save() {
    if (!file || timer) return;
    timer = setTimeout(() => { timer = null; flushNow(); }, 500);
    timer.unref?.();
  }
  function flushNow() {
    if (!file) return;
    try { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file + '.tmp', JSON.stringify(state)); fs.renameSync(file + '.tmp', file); }
    catch (e) { log.error('friends save failed', e.message); }
  }

  // ---------------------------------------------------------------- ids

  function numberFor(pf) {
    if (state.nums[pf]) return state.nums[pf];
    let n;
    do { n = String(100000 + Math.floor(Math.random() * 900000)); } while (state.byNum[n]);
    state.nums[pf] = n; state.byNum[n] = pf;
    save();
    return n;
  }

  function friendsOf(pf) { return state.friends[pf] || (state.friends[pf] = []); }
  function areFriends(a, b) { return friendsOf(a).includes(b); }

  async function card(pf) {
    const p = await chat.who(pf);
    return { pf, num: numberFor(pf), name: p.name, rank: p.rank, level: p.level };
  }

  function roomOf(pf) {
    const p = presence.get(pf);
    return p && now() - p.at < f.presenceMs ? p.room : null;
  }

  function lastOnline(pf) {
    const t = Math.max(chat.seenAt(pf), (presence.get(pf) || {}).at || 0, state.seen[pf] || 0);
    if (t) state.seen[pf] = t;
    return t;
  }

  // ---------------------------------------------------------------- the lists

  async function me(pf) { return Object.assign({ ok: true }, await card(pf), { max: f.maxFriends }); }

  async function list(pf) {
    const friends = [];
    for (const other of friendsOf(pf)) {
      const c = await card(other);
      const online = chat.isOnline(other);
      friends.push(Object.assign(c, { online, room: online ? roomOf(other) : null, seen: lastOnline(other) }));
    }
    friends.sort((a, b) => (b.room ? 2 : b.online ? 1 : 0) - (a.room ? 2 : a.online ? 1 : 0) || b.seen - a.seen);
    const incoming = [];
    for (const [from, at] of Object.entries(state.requests[pf] || {})) incoming.push(Object.assign(await card(from), { at }));
    const outgoing = [];
    for (const [to, reqs] of Object.entries(state.requests)) if (reqs[pf]) outgoing.push(Object.assign(await card(to), { at: reqs[pf] }));
    save();
    return { ok: true, me: await card(pf), max: f.maxFriends, friends, incoming, outgoing };
  }

  async function lookup(pf, { num }) {
    if (!NUM.test(String(num))) throw new ChatError('bad_number');
    const target = state.byNum[String(num)];
    if (!target) throw new ChatError('unknown_number', 404);
    const c = await card(target);
    return { ok: true, found: true, pf: target, num: c.num, name: c.name, level: c.level, rank: c.rank, self: target === pf, friend: areFriends(pf, target), pending: !!(state.requests[target] || {})[pf] };
  }

  // ---------------------------------------------------------------- requests

  function sentToday(pf) {
    const dayAgo = now() - 24 * 3600 * 1000;
    state.sent[pf] = (state.sent[pf] || []).filter(t => t > dayAgo);
    return state.sent[pf].length;
  }

  function makeFriends(a, b) {
    if (!areFriends(a, b)) friendsOf(a).push(b);
    if (!areFriends(b, a)) friendsOf(b).push(a);
    if (state.requests[a]) delete state.requests[a][b];
    if (state.requests[b]) delete state.requests[b][a];
    save();
  }

  async function request(pf, { num, player }) {
    let target = player;
    if (!target) {
      if (!NUM.test(String(num))) throw new ChatError('bad_number');
      target = state.byNum[String(num)];
      if (!target) throw new ChatError('unknown_number', 404);
    }
    if (!PF_ID.test(String(target))) throw new ChatError('bad_player');
    if (target === pf) throw new ChatError('self');
    if (areFriends(pf, target)) throw new ChatError('already_friends');
    if (friendsOf(pf).length >= f.maxFriends) throw new ChatError('you_full', 403);
    if (sentToday(pf) >= f.requestsPerDay) throw new ChatError('too_many_requests', 429);
    // he asked me first: that is a yes
    if ((state.requests[pf] || {})[target]) {
      if (friendsOf(target).length >= f.maxFriends) throw new ChatError('target_full', 403);
      makeFriends(pf, target);
      chat.pushEvent(target, { type: 'friend_accepted', pf, name: (await chat.who(pf)).name });
      return { ok: true, accepted: true };
    }
    const mine = (state.requests[target] = state.requests[target] || {});
    if (mine[pf]) return { ok: true, pending: true };
    (state.sent[pf] = state.sent[pf] || []).push(now());
    // a player who blocked the sender in chat is not told about the request
    if (!chat.blockedList(target).blocked.includes(pf)) {
      mine[pf] = now();
      const c = await card(pf);
      chat.pushEvent(target, { type: 'friend_request', pf, name: c.name, level: c.level });
    } else mine[pf] = now();
    save();
    return { ok: true, pending: true };
  }

  async function respond(pf, { player, accept }) {
    const reqs = state.requests[pf] || {};
    if (!reqs[player]) throw new ChatError('no_request', 404);
    if (!accept) { delete reqs[player]; save(); return { ok: true }; }
    if (friendsOf(pf).length >= f.maxFriends) throw new ChatError('you_full', 403);
    if (friendsOf(player).length >= f.maxFriends) { delete reqs[player]; save(); throw new ChatError('target_full', 403); }
    makeFriends(pf, player);
    chat.pushEvent(player, { type: 'friend_accepted', pf, name: (await chat.who(pf)).name });
    return { ok: true };
  }

  function cancel(pf, { player }) {
    if (state.requests[player]) delete state.requests[player][pf];
    save();
    return { ok: true };
  }

  function remove(pf, { player }) {
    state.friends[pf] = friendsOf(pf).filter(x => x !== player);
    state.friends[player] = friendsOf(player).filter(x => x !== pf);
    save();
    return { ok: true };
  }

  // ---------------------------------------------------------------- where is everybody, and invitations

  function cleanRoom(room) {
    if (!room || typeof room !== 'object') return null;
    const id = String(room.id || '').slice(0, 64);
    if (!id) return null;
    return {
      id, name: String(room.name || '').slice(0, 40), count: Math.max(0, Math.min(99, parseInt(room.count, 10) || 0)), cap: Math.max(0, Math.min(99, parseInt(room.cap, 10) || 0)),
      kind: ['public', 'private', 'vip'].includes(room.kind) ? room.kind : 'public', locked: !!room.locked, code: String(room.code || '').slice(0, 8), inMatch: !!room.inMatch,
    };
  }

  function setPresence(pf, { room }) {
    chat.touch(pf);
    presence.set(pf, { at: now(), room: cleanRoom(room) });
    state.seen[pf] = now();
    return { ok: true, online: chat.onlineCount() };
  }

  async function invite(pf, { friend, room }) {
    if (!areFriends(pf, friend)) throw new ChatError('not_friends', 403);
    const r = cleanRoom(room);
    if (!r) throw new ChatError('bad_room');
    const key = pf + '>' + friend;
    const wait = (lastInvite.get(key) || 0) + f.inviteGapMs - now();
    if (wait > 0) throw new ChatError('slow', 429, { retryMs: wait });
    lastInvite.set(key, now());
    const c = await card(pf);
    chat.pushEvent(friend, { type: 'invite', pf, name: c.name, level: c.level, room: r });
    return { ok: true };
  }

  // ---------------------------------------------------------------- reports about a player (not about one message)

  const REASONS = ['abuse', 'harassment', 'cheat', 'voice', 'name', 'spam', 'other'];
  function reportPlayer(pf, { player, reason, note }) {
    if (!PF_ID.test(String(player)) || player === pf) throw new ChatError('bad_player');
    if (!REASONS.includes(reason)) throw new ChatError('bad_reason');
    const entry = { ts: new Date(now()).toISOString(), type: 'player', reporter: pf, target: player, reason, note: String(note || '').slice(0, 100) };
    if (reportsFile) { try { fs.mkdirSync(path.dirname(reportsFile), { recursive: true }); fs.appendFileSync(reportsFile, JSON.stringify(entry) + '\n'); } catch (e) { log.error('report write failed', e.message); } }
    return { ok: true };
  }

  function purge() {
    const dayAgo = now() - 24 * 3600 * 1000;
    for (const [k, t] of lastInvite) if (t < dayAgo) lastInvite.delete(k);
    for (const [pf, p] of presence) if (now() - p.at > 24 * 3600 * 1000) presence.delete(pf);
    // old unanswered requests (30 days) go away
    const old = now() - 30 * 24 * 3600 * 1000;
    for (const reqs of Object.values(state.requests)) for (const [from, t] of Object.entries(reqs)) if (t < old) delete reqs[from];
    save();
  }

  return { me, list, lookup, request, respond, cancel, remove, setPresence, invite, reportPlayer, purge, flushNow, _state: () => state };
}

module.exports = { createFriends };
