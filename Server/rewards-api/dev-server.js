'use strict';
// A local test server with a pretend PlayFab: no secret key, nothing is stored on disk. For trying the game's chat and rewards
// screens on this computer:   node dev-server.js            (listens on http://127.0.0.1:8788)
// In the Unity editor point the game to it:   PlayerPrefs "MafiaApiUrl" = http://127.0.0.1:8788
// Tickets "t-<id>" mean the player <id>; any other ticket (the real one from the editor login) becomes the player "ED" + a hash of it.

const crypto = require('node:crypto');
const { createApp } = require('./src/app');
const { createChat } = require('./src/chat');
const { createStore } = require('./src/store');
const { createFriends } = require('./src/friends');
const { createLeaderboard } = require('./src/leaderboard');
const { createOffers } = require('./src/offers');
const { createMail } = require('./src/mail');
const { createCodes } = require('./src/codes');
const { createEvents } = require('./src/events');
const { createAdminStore } = require('./src/adminstore');
const { createAdmin, hashPassword } = require('./src/admin');
const { createRecovery } = require('./src/recovery');
const os = require('node:os');
const fs = require('node:fs');
const path = require('node:path');

const names = { OTHER0001: 'أم سعيد', OTHER0002: 'البيك', OTHER0003: 'لينا', OTHER0004: 'مازن' };
const progress = { OTHER0001: { rankPoints: 3200 }, OTHER0002: { rankPoints: 4100 }, OTHER0003: { rankPoints: 2100 }, OTHER0004: { rankPoints: 1100 } };

const playfab = {
  async authenticate(t) { return t.startsWith('t-') ? t.slice(2) : 'ED' + crypto.createHash('sha1').update(t).digest('hex').slice(0, 10).toUpperCase(); },
  async getDisplayName(pf) { return names[pf] || (pf.startsWith('ED') ? 'أبو سالم' : 'لاعب'); },
  async getProgress(pf) { return progress[pf] || null; },
  async getProgressKey() { return null; },
  async setProgress() {},
  // balances live in memory so the admin board's balance tile and the mail claims can be tried
  bal: {},
  async addCurrency(pf, code, n) { this.bal[pf + code] = (this.bal[pf + code] || 0) + n; },
  async subtractCurrency(pf, code, n) { if ((this.bal[pf + code] || 0) < n) throw new Error('insufficient'); this.bal[pf + code] -= n; },
  async grantItems() {},
  async setDisplayName(pf, n) { names[pf] = n; },
  async banUser() {}, async unbanUser() {},
  async linkServerCustomId() {}, async unlinkServerCustomId() {},
  async loginServerCustomId(id) { return { SessionTicket: 'dev-recovered-' + id, PlayFabId: id.replace(/^rec-/, '') }; },
  async getBalances(pf) { return { CO: this.bal[pf + 'CO'] || 0, GM: this.bal[pf + 'GM'] || 0 }; },
};

const cfg = {
  titleId: 'DEV', currencyCode: 'CO', requestsPerMinutePerIp: 100000, minPlayers: 4, maxPlayers: 16, minRounds: 1, minDurationSec: 60, quorumRatio: 0.5,
  reportWindowSeconds: 900, dailyMatchCap: 30, trustProxy: false,
  rewards: { baseXp: 20, winXp: 25, survivedXp: 5, baseCoins: 10, winCoins: 20, rankWin: 50, rankLose: -20 },
  chat: { publicCooldownMs: 2000 },
};
// the admin board has its own data in a temp folder; password for the local test: dev-password-123, address http://127.0.0.1:8788/admin
const devDir = fs.mkdtempSync(path.join(os.tmpdir(), 'mafia-dev-'));
const reportsFile = path.join(devDir, 'chat-reports.jsonl');
const adminStore = createAdminStore({ file: path.join(devDir, 'admin.json') });
const chat = createChat({ cfg, playfab, extraWords: () => adminStore.words, reportsFile });
const friends = createFriends({ chat, cfg, reportsFile });
const leaderboard = createLeaderboard({ chat, cfg });
// demo numbers so the leaderboards screen has something to show
const demo = ['أبو فارس', 'أم سعيد', 'البيك', 'لينا', 'الحج حسن', 'سارة', 'مازن', 'أبو خليل', 'ريما', 'الدكتور', 'نور', 'أبو جاسم', 'هدى', 'الأستاذ'];
demo.forEach((n, i) => {
  const pf = i < 4 ? 'OTHER000' + (i + 1) : 'DEMO' + String(i).padStart(4, '0');
  names[pf] = n; progress[pf] = { rankPoints: 5200 - i * 330, level: 30 - i };
  const games = 14 - i;
  for (let g = 0; g < games; g++) leaderboard.record(pf, { won: (g + i) % 3 !== 0, role: g % 4 === 0 ? 'Mafia' : g % 5 === 0 ? 'Sheriff' : 'Civilian', survived: g % 2 === 0, durationSec: 900 + g * 40, rankPointsGained: (g + i) % 3 !== 0 ? 50 : -20, rankPointsNow: 5200 - i * 330 });
});
// demo events (relative to now) for the events board; the test player can win matches through the bot practice match to move the challenge
const iso = h => new Date(Date.now() + h * 3600 * 1000).toISOString();
cfg.events = [
  { id: 'dev-gold', kind: 'bonus', kicker: 'عرض الأسبوع', title: 'عطلة الذهب المضاعف', body: 'كل ربح من المباريات يتضاعف طول العطلة. العب أكثر واجمع أكثر.', coinsMultiplier: 2, from: iso(-12), until: iso(26) },
  { id: 'dev-wins', kind: 'challenge', kicker: 'مهمّة', title: 'تحدّي ١٠ انتصارات', body: 'اربح ١٠ مباريات قبل نهاية الأسبوع واستلم مكافأتك.', metric: 'wins', goal: 10, rewardCoins: 500, from: iso(-24), until: iso(96) },
  { id: 'dev-race', kind: 'info', kicker: 'سباق الأسبوع', title: 'سباق الانتصارات', body: 'أكتر ٥ لاعبين انتصاراً بياخدوا جوائز كبيرة.', from: iso(-48), until: iso(5), ranking: { enabled: true, metric: 'wins', winners: 3, distribution: 'auto', prizes: [{ from: 1, to: 1, packId: 'pk-gold' }, { from: 2, to: 2, packId: 'pk-silver' }, { from: 3, to: 3, packId: 'pk-small' }] } },
  { id: 'dev-night', kind: 'info', kicker: 'بطولة', title: 'ليلة العصابة الكبرى', body: 'بطولة ليلية بجوائز نادرة.', secret: true, from: iso(48), until: iso(53) },
  { id: 'dev-old', kind: 'info', kicker: 'أرشيف', title: 'أسبوع الرصاصة الذهبية', body: 'انتهى الحدث. شكراً لمشاركتك، وننتظرك بالحدث القادم.', from: iso(-200), until: iso(-100) },
];
const mail = createMail({ cfg, playfab, notify: pfs => { for (const pf of (pfs || chat.onlinePlayers())) chat.pushEvent(pf, { type: 'mail' }); } });
adminStore.packs = [{ id: 'pk-gold', name: { ar: 'جائزة ذهبية', en: 'Gold prize' }, coins: 5000, gems: 0, items: [] }, { id: 'pk-silver', name: { ar: 'جائزة فضية', en: 'Silver prize' }, coins: 3000, gems: 0, items: [] }, { id: 'pk-small', name: { ar: 'تشجيعية', en: 'Encouragement' }, coins: 1000, gems: 2, items: [] }];
adminStore.codes = [{ code: 'MAFIA2026', packId: 'pk-gold', maxUses: 1000, uses: 312, until: '', enabled: true, by: {} }, { code: 'INSTA50', packId: 'pk-small', maxUses: 0, uses: 48, until: '', enabled: true, by: {} }];
const codes = createCodes({ store: adminStore, mail, packs: () => adminStore.packs });
const events = createEvents({ cfg, playfab, mail, packs: () => adminStore.packs, nameOf: async pf => (await chat.who(pf)).name, source: () => (adminStore.events !== null ? adminStore.events : cfg.events) });
const offers = createOffers({ cfg, source: () => (adminStore.offers !== null ? adminStore.offers : cfg.offers) });
cfg.offers = [{ adId: 'ad_ember', stamp: '−٣٠٪', until: iso(2) }, { itemId: 'chat_port', mast: 'وصل حديثاً', headline: 'خلفية الميناء\nسفن وليل أسود', stamp: 'جديد' }];
fs.writeFileSync(reportsFile, [
  { ts: iso(-0.05), reporter: 'OTHER0001', target: 'OTHER0002', channel: 'public', messageId: 1, text: 'كلام مسيء', reason: 'abuse' },
  { ts: iso(-1), type: 'player', reporter: 'OTHER0003', target: 'OTHER0002', reason: 'cheat', note: '' },
  { ts: iso(-5), type: 'player', reporter: 'OTHER0004', target: 'OTHER0001', reason: 'spam', note: '' },
].map(r => JSON.stringify(r)).join('\n') + '\n');
const recovery = createRecovery({ store: adminStore, playfab });
const admin = createAdmin({ cfg, store: adminStore, chat, friends, leaderboard, mail, playfab, events, codes, recovery, reportsFile, adminCfg: { passwordHash: hashPassword('dev-password-123'), path: '/admin' } });
for (let i = 0; i < 40; i++) adminStore.noteMatch('dev-m' + i, leaderboard.dayKey(Date.now() - (i % 7) * 86400000));
for (const [pf, n] of [['OTHER0001', 9], ['OTHER0002', 7], ['OTHER0003', 5], ['OTHER0004', 2]]) for (let i = 0; i < n; i++) events.record(pf, { won: true, points: 50, secs: 600 });
const { server } = createApp({ cfg, playfab, store: createStore(null), chat, friends, leaderboard, events, offers, mail, codes, recovery, admin });
server.listen(8788, '127.0.0.1', () => console.log('dev server on http://127.0.0.1:8788 (pretend PlayFab; players OTHER0001..OTHER0004 exist) · admin board: http://127.0.0.1:8788/admin password dev-password-123'));
