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
const { server } = createApp({ cfg, playfab, store: createStore(null), chat, friends });
server.listen(8788, '127.0.0.1', () => console.log('dev server on http://127.0.0.1:8788 (pretend PlayFab; players OTHER0001..OTHER0004 exist)'));
