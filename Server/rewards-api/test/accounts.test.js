'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createApp } = require('../src/app');
const { createChat } = require('../src/chat');
const { createStore } = require('../src/store');
const { createMail } = require('../src/mail');
const { createAccounts } = require('../src/accounts');

const DAY = 86400000, T0 = Date.UTC(2026, 10, 2, 12);

function setup({ purge = true } = {}) {
  let clock = T0; const now = () => clock;
  const deleted = [];
  const playfab = { authenticate: async t => t.replace('-ticket-0123456789', ''), getDisplayName: async pf => 'اسم ' + pf, deletePlayer: async pf => { deleted.push(pf); } };
  const mail = createMail({ cfg: {}, playfab, now });
  const recovery = { info: pf => (pf === 'OFFICE001' ? { username: 'x' } : null) };
  const accounts = createAccounts({ cfg: { guests: { purge, days: 30, warnDays: 7 } }, mail, playfab, recovery, now, log: { error() {} } });
  return { accounts, mail, deleted, tick: ms => { clock += ms; }, playfab, now };
}

test('a player seen by the server is a guest until he has a way to come back', () => {
  const s = setup();
  s.accounts.touch('GUEST001'); s.accounts.touch('OFFICE001'); s.accounts.touch('GOOGLE001');
  s.accounts.link('GOOGLE001', 'google', 'sub-123');
  assert.equal(s.accounts.status('GUEST001').guest, true);
  assert.equal(s.accounts.status('GOOGLE001').guest, false); assert.deepEqual(s.accounts.status('GOOGLE001').methods, ['google']);
  assert.equal(s.accounts.status('OFFICE001').guest, false); assert.deepEqual(s.accounts.status('OFFICE001').methods, ['office']);
  assert.equal(s.accounts.owner('google', 'sub-123'), 'GOOGLE001'); assert.equal(s.accounts.owner('google', 'other'), null);
  assert.throws(() => s.accounts.link('GUEST001', 'myspace', '1'), /bad_method/);
});

test('days left count from the last visit, and coming back resets them', () => {
  const s = setup();
  s.accounts.touch('GUEST001');
  assert.equal(s.accounts.status('GUEST001').daysLeft, 30);
  s.tick(10 * DAY); assert.equal(s.accounts.status('GUEST001').daysLeft, 20);
  s.accounts.touch('GUEST001'); assert.equal(s.accounts.status('GUEST001').daysLeft, 30);
  s.tick(1800000); s.accounts.touch('GUEST001');            // less than an hour later: not counted again
});

test('nothing is warned or deleted while the purge is off', async () => {
  const s = setup({ purge: false });
  s.accounts.touch('GUEST001'); s.tick(40 * DAY);
  assert.deepEqual(await s.accounts.sweep(), { warned: 0, deleted: 0, failed: 0 });
  assert.equal(s.deleted.length, 0); assert.equal(s.accounts.status('GUEST001').daysLeft, null);
});

test('a guest is warned in his mail a week before and deleted after a month; linked players are never touched', async () => {
  const s = setup();
  s.accounts.touch('GUEST001'); s.accounts.touch('GOOGLE001'); s.accounts.link('GOOGLE001', 'google', 's1');
  s.tick(20 * DAY); assert.deepEqual(await s.accounts.sweep(), { warned: 0, deleted: 0, failed: 0 });
  s.tick(4 * DAY);                                                                        // day 24: inside the last week
  assert.deepEqual(await s.accounts.sweep(), { warned: 1, deleted: 0, failed: 0 });
  const m = s.mail.list('GUEST001').items;
  assert.equal(m.length, 1); assert.equal(m[0].kind, 'note'); assert.match(m[0].title.ar, /7 أيام/);
  assert.equal(s.mail.list('GOOGLE001').items.length, 0);
  assert.deepEqual(await s.accounts.sweep(), { warned: 0, deleted: 0, failed: 0 });        // warned once only
  s.tick(7 * DAY);                                                                        // day 31
  assert.deepEqual(await s.accounts.sweep(), { warned: 0, deleted: 1, failed: 0 });
  assert.deepEqual(s.deleted, ['GUEST001']);
  assert.equal(s.accounts.summary().guests, 0);
});

test('a guest who comes back is not deleted and can be warned again later', async () => {
  const s = setup();
  s.accounts.touch('GUEST001'); s.tick(25 * DAY); await s.accounts.sweep();
  s.accounts.touch('GUEST001');                                                           // opened the game: the clock starts again
  s.tick(25 * DAY);
  assert.equal((await s.accounts.sweep()).warned, 1); assert.equal(s.mail.list('GUEST001').items.length, 2);
  assert.equal(s.deleted.length, 0);
});

test('a failed deletion is counted and retried, not forgotten', async () => {
  const s = setup(); let fail = true;
  s.playfab.deletePlayer = async pf => { if (fail) throw new Error('down'); s.deleted.push(pf); };
  s.accounts.touch('GUEST001'); s.tick(31 * DAY);
  assert.equal((await s.accounts.sweep()).failed, 1); fail = false;
  assert.equal((await s.accounts.sweep()).deleted, 1);
});

test('the game asks /v1/account and the server notes the player on the way', async () => {
  const cfg = { currencyCode: 'CO', requestsPerMinutePerIp: 100000, trustProxy: false, chat: { publicCooldownMs: 0, cooldownMs: 0 } };
  const s = setup();
  const chat = createChat({ cfg, playfab: s.playfab, now: s.now });
  const { server } = createApp({ cfg, playfab: s.playfab, store: createStore(null), chat, accounts: s.accounts, now: s.now });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  try {
    const res = await fetch('http://127.0.0.1:' + server.address().port + '/v1/account', { method: 'POST', headers: { 'content-type': 'application/json', 'x-authorization': 'NEWBIE0001-ticket-0123456789' }, body: '{}' });
    const j = await res.json();
    assert.equal(res.status, 200); assert.equal(j.guest, true); assert.equal(j.daysLeft, 30); assert.deepEqual(j.methods, []);
    assert.equal(s.accounts.summary().total, 1);
  } finally { server.close(); }
});
