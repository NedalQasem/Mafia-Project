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

const names = { OTHER0001: 'أم سعيد', OTHER0002: 'البيك', OTHER0003: 'لينا', OTHER0004: 'مازن' };
const progress = { OTHER0001: { rankPoints: 3200 }, OTHER0002: { rankPoints: 4100 }, OTHER0003: { rankPoints: 2100 }, OTHER0004: { rankPoints: 1100 } };

const playfab = {
  async authenticate(t) { return t.startsWith('t-') ? t.slice(2) : 'ED' + crypto.createHash('sha1').update(t).digest('hex').slice(0, 10).toUpperCase(); },
  async getDisplayName(pf) { return names[pf] || (pf.startsWith('ED') ? 'أبو سالم' : 'لاعب'); },
  async getProgress(pf) { return progress[pf] || null; },
  async getProgressKey() { return null; },
  async setProgress() {}, async addCurrency() {},
};

const cfg = {
  titleId: 'DEV', currencyCode: 'CO', requestsPerMinutePerIp: 100000, minPlayers: 4, maxPlayers: 16, minRounds: 1, minDurationSec: 60, quorumRatio: 0.5,
  reportWindowSeconds: 900, dailyMatchCap: 30, trustProxy: false,
  rewards: { baseXp: 20, winXp: 25, survivedXp: 5, baseCoins: 10, winCoins: 20, rankWin: 50, rankLose: -20 },
  chat: { publicCooldownMs: 2000 },
};
const chat = createChat({ cfg, playfab });
const friends = createFriends({ chat, cfg });
const leaderboard = createLeaderboard({ chat, cfg });
// demo numbers so the leaderboards screen has something to show
const demo = ['أبو فارس', 'أم سعيد', 'البيك', 'لينا', 'الحج حسن', 'سارة', 'مازن', 'أبو خليل', 'ريما', 'الدكتور', 'نور', 'أبو جاسم', 'هدى', 'الأستاذ'];
demo.forEach((n, i) => {
  const pf = i < 4 ? 'OTHER000' + (i + 1) : 'DEMO' + String(i).padStart(4, '0');
  names[pf] = n; progress[pf] = { rankPoints: 5200 - i * 330, level: 30 - i };
  const games = 14 - i;
  for (let g = 0; g < games; g++) leaderboard.record(pf, { won: (g + i) % 3 !== 0, role: g % 4 === 0 ? 'Mafia' : g % 5 === 0 ? 'Sheriff' : 'Civilian', survived: g % 2 === 0, durationSec: 900 + g * 40, rankPointsGained: (g + i) % 3 !== 0 ? 50 : -20, rankPointsNow: 5200 - i * 330 });
});
const { server } = createApp({ cfg, playfab, store: createStore(null), chat, friends, leaderboard });
server.listen(8788, '127.0.0.1', () => console.log('dev server on http://127.0.0.1:8788 (pretend PlayFab; players OTHER0001..OTHER0004 exist)'));
