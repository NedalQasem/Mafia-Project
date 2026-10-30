'use strict';
// Promo codes: offers made outside the game (a post, a story, a message). The admin makes a code that points at a package; a player types it in the
// mail screen and the package arrives as a telegram in his mail. A player can use a code once; a code can have a limit of uses and an end date.
// Wrong codes are counted per player: five in a row lock him for ten minutes, so nobody can guess codes.

const crypto = require('node:crypto');
const { ChatError } = require('./chat');

const ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';        // no 0/O/1/I to read out loud
const CODE_RE = /^[A-Z0-9]{3,20}$/;
const MAX_FAILS = 5, LOCK_MS = 10 * 60 * 1000;

const normalize = c => String(c || '').trim().toUpperCase().replace(/[\s-]+/g, '');

/** store: { codes: [..], touch() } (the admin store), mail: the mail module, packs: () => the admin's packages. */
function createCodes({ store, mail, packs = () => [], now = () => Date.now() }) {
  const fails = new Map();      // pf -> { n, until }

  const list = () => store.codes;
  const find = c => list().find(x => x.code === c) || null;
  const usable = (c, t) => c && c.enabled !== false && (!c.until || Date.parse(c.until) > t) && (!c.maxUses || c.uses < c.maxUses);

  function randomCode() {
    for (let tries = 0; tries < 20; tries++) {
      let s = '';
      for (let i = 0; i < 8; i++) s += ALPHABET[crypto.randomInt(ALPHABET.length)];
      if (!find(s)) return s;
    }
    throw new ChatError('try_again', 503);
  }

  /** A player types a code. All the "no" answers are the same (bad_code) so a code cannot be probed. */
  function redeem(pf, raw) {
    const t = now();
    const f = fails.get(pf);
    if (f && f.until > t) throw new ChatError('locked', 429, { retryMs: f.until - t });
    const code = normalize(raw), c = CODE_RE.test(code) ? find(code) : null;
    if (!usable(c, t)) {
      const n = (f && !(f.until && f.until <= t) ? f.n : 0) + 1;
      fails.set(pf, { n, until: n >= MAX_FAILS ? t + LOCK_MS : 0 });
      throw new ChatError('bad_code', 404);
    }
    if (c.by && c.by[pf]) throw new ChatError('already_used', 409);
    const pack = (packs() || []).find(p => p.id === c.packId);
    if (!pack) throw new ChatError('bad_code', 404);       // the package was deleted: the code is dead
    fails.delete(pf);
    c.by = c.by || {}; c.by[pf] = t; c.uses = (c.uses || 0) + 1; store.touch();
    mail.send({
      audience: 'players', recipients: [pf], expiresInDays: 30, label: 'code:' + c.code,
      title: { ar: 'كود ترويج: ' + c.code, en: 'Promo code: ' + c.code },
      body: { ar: 'الكود انفعّل. هاد البكج إلك.', en: 'The code worked. This package is yours.' },
      pack: { coins: pack.coins || 0, gems: pack.gems || 0, items: pack.items || [] },
    });
    return { ok: true, pack: { coins: pack.coins || 0, gems: pack.gems || 0, items: pack.items || [] } };
  }

  return { redeem, list, find, normalize, randomCode, CODE_RE };
}

module.exports = { createCodes, normalize, CODE_RE };
