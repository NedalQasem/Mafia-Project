'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createCodes, normalize } = require('../src/codes');
const { createMail } = require('../src/mail');
const { createAdminStore } = require('../src/adminstore');

const T0 = Date.UTC(2026, 10, 2, 12);
let clock = T0;
const now = () => clock;
const packs = [{ id: 'eid', name: { ar: 'عيد' }, coins: 1000, gems: 5, items: ['frame_ember'] }];

function make(codeList) {
  clock = T0;
  const store = createAdminStore({ file: null, now });
  store.codes = codeList;
  const mail = createMail({ cfg: { currencyCode: 'CO' }, playfab: null, now });
  const codes = createCodes({ store, mail, packs: () => packs, now });
  return { codes, mail, store };
}
const code = (extra = {}) => Object.assign({ code: 'MAFIA2026', packId: 'eid', maxUses: 0, uses: 0, until: '', enabled: true, by: {} }, extra);

test('a code is typed in any case, with spaces or dashes, and the package arrives as a telegram', () => {
  const { codes, mail } = make([code()]);
  assert.equal(normalize(' mafia-2026 '), 'MAFIA2026');
  const r = codes.redeem('P1', ' mafia 2026 ');
  assert.deepEqual(r.pack, { coins: 1000, gems: 5, items: ['frame_ember'] });
  const m = mail.list('P1').items[0];
  assert.equal(m.title.ar, 'كود ترويج: MAFIA2026'); assert.deepEqual(m.pack, { coins: 1000, gems: 5, items: ['frame_ember'] });
  assert.equal(codes.find('MAFIA2026').uses, 1);
});

test('a player uses a code once, others can still use it', () => {
  const { codes, mail } = make([code()]);
  codes.redeem('P1', 'MAFIA2026');
  assert.throws(() => codes.redeem('P1', 'MAFIA2026'), { code: 'already_used' });
  codes.redeem('P2', 'MAFIA2026');
  assert.equal(mail.list('P1').items.length, 1);
});

test('the limit of uses, the end date and the switch all answer the same "bad code"', () => {
  const { codes } = make([code({ code: 'FULL', maxUses: 1 }), code({ code: 'OLD', until: '2026-11-01T00:00:00Z' }), code({ code: 'OFF', enabled: false }), code({ code: 'GONE', packId: 'deleted' })]);
  codes.redeem('P1', 'FULL');
  ['FULL', 'OLD', 'OFF', 'GONE', 'NOPE', '', '!!'].forEach((c, i) => assert.throws(() => codes.redeem('Q' + i, c), { code: 'bad_code' }, c));      // a new player each time: five fails would lock one
});

test('five wrong codes in a row lock the player for ten minutes, a right one resets the count', () => {
  const { codes } = make([code()]);
  for (let i = 0; i < 4; i++) assert.throws(() => codes.redeem('P1', 'WRONG' + i), { code: 'bad_code' });
  codes.redeem('P1', 'MAFIA2026');                                                     // a right one in between: the count starts again
  for (let i = 0; i < 5; i++) assert.throws(() => codes.redeem('P1', 'BAD' + i), { code: 'bad_code' });
  assert.throws(() => codes.redeem('P1', 'MAFIA2026'), { code: 'locked' });             // even the right code waits
  assert.throws(() => codes.redeem('P2', 'NOPE'), { code: 'bad_code' });               // other players are not affected
  clock += 11 * 60 * 1000;
  assert.throws(() => codes.redeem('P1', 'MAFIA2026'), { code: 'already_used' });        // unlocked again (it was used before)
});

test('random codes are readable and not taken', () => {
  const { codes } = make([code()]);
  const seen = new Set();
  for (let i = 0; i < 50; i++) { const c = codes.randomCode(); assert.match(c, /^[A-HJ-NP-Z2-9]{8}$/); seen.add(c); }
  assert.ok(seen.size > 45);
});
