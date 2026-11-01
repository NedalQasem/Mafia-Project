'use strict';
// VOICE TOKENS for LiveKit (the voice of the rooms where many players listen and a few speak). LiveKit itself runs as its own container
// (Server/livekit); this module only decides who may join which room and who may speak, and signs the token the game connects with.
// A token is a JWT (HS256) signed with the LiveKit API secret, so no library is needed.
//
// Room names:  "official:<id>"  a room of officialrooms.js: any player who can see it may listen; only the admins and the people the room bot
//                               reports on a mic seat may speak (the stage is asked at the moment of the token, so a player who is brought onto a seat asks again).
//              "lobby:<EOS lobby id>"  a room a player opened: everybody may listen and speak, the game keeps the seats (as it does for EOS voice).

const crypto = require('node:crypto');

class VoiceError extends Error {
  constructor(code, status = 400) { super(code); this.code = code; this.status = status; }
}

const b64 = o => Buffer.from(typeof o === 'string' ? o : JSON.stringify(o)).toString('base64url');
const ROOM_RE = /^(official:\d{1,6}|lobby:[A-Za-z0-9_-]{4,80})$/;

function createVoice({ apiKey = '', apiSecret = '', url = '', officialRooms = null, now = () => Date.now(), ttlSeconds = 6 * 3600 } = {}) {
  const enabled = !!(apiKey && apiSecret && url);

  function sign(claims) {
    const head = b64({ alg: 'HS256', typ: 'JWT' }), body = b64(claims);
    const sig = crypto.createHmac('sha256', apiSecret).update(head + '.' + body).digest('base64url');
    return head + '.' + body + '.' + sig;
  }

  /** Can this player speak in the room right now? (official rooms only; the others leave it to the game) */
  function mayPublish(pf, room) {
    if (!room.startsWith('official:')) return true;
    const id = room.slice(9);
    if (officialRooms.isStaff(pf, id)) return true;
    const mem = officialRooms.members(id).find(m => m.pf === pf);
    if (!mem) return false;
    const stage = officialRooms.stageOf(id);
    return !!stage && (stage.seats.includes(mem.puid) || stage.head === mem.puid);
  }

  /**
   * @param pf the player (from his PlayFab ticket)
   * @param name his display name (shown in the voice room)
   * @param room "official:1001" or "lobby:<id>"
   */
  function token(pf, name, room) {
    if (!enabled) throw new VoiceError('voice_off', 404);
    room = String(room || '');
    if (!ROOM_RE.test(room)) throw new VoiceError('bad_room');
    if (room.startsWith('official:')) {
      if (!officialRooms) throw new VoiceError('not_found', 404);
      const id = room.slice(9);
      const found = officialRooms.list(pf).rooms.find(r => r.id === id);
      if (!found) throw new VoiceError('not_found', 404);
    }
    const canPublish = mayPublish(pf, room);
    const t = Math.floor(now() / 1000);
    const jwt = sign({
      iss: apiKey, sub: pf, name: String(name || '').slice(0, 40), nbf: t - 10, exp: t + ttlSeconds,
      video: { room, roomJoin: true, canPublish, canSubscribe: true, canPublishData: false, canPublishSources: ['microphone'] },
    });
    return { ok: true, url, token: jwt, canPublish, room };
  }

  return { enabled, token, VoiceError };
}

module.exports = { createVoice, VoiceError };
