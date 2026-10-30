'use strict';
// Entry point: reads config.json (path in CONFIG, default ./config.json) and the PlayFab secret key from the environment.
// The secret key is NEVER in a file of this project: it lives in the service's environment file on the server only.

const fs = require('node:fs');
const path = require('node:path');
const { createApp } = require('./app');
const { createPlayFab } = require('./playfab');
const { createStore } = require('./store');
const { createChat } = require('./chat');
const { createFriends } = require('./friends');
const { createLeaderboard } = require('./leaderboard');
const { createOffers } = require('./offers');
const { createEvents } = require('./events');
const { createMail } = require('./mail');
const { createCodes } = require('./codes');
const { createAdminStore } = require('./adminstore');
const { createAdmin } = require('./admin');

const cfgPath = process.env.CONFIG || path.join(__dirname, '..', 'config.json');
const cfg = JSON.parse(fs.readFileSync(cfgPath, 'utf8'));
if (process.env.HOST) cfg.host = process.env.HOST;            // inside Docker the service listens on 0.0.0.0 (the port is not published)
if (process.env.DATA_FILE) cfg.dataFile = process.env.DATA_FILE;
const secretKey = process.env.PLAYFAB_SECRET_KEY;
if (!secretKey) { console.error('PLAYFAB_SECRET_KEY is not set (put it in the service environment file).'); process.exit(1); }

const store = createStore(cfg.dataFile);
const playfab = createPlayFab({ titleId: cfg.titleId, secretKey });
const dataDir = require('node:path').dirname(cfg.dataFile);
const adminStore = createAdminStore({ file: dataDir + '/admin.json' });
const chat = createChat({ cfg, playfab, extraWords: () => adminStore.words, file: dataDir + '/chat.json', reportsFile: dataDir + '/chat-reports.jsonl' });
const friends = createFriends({ chat, cfg, file: dataDir + '/friends.json', reportsFile: dataDir + '/chat-reports.jsonl' });
const leaderboard = createLeaderboard({ chat, cfg, file: dataDir + '/leaderboard.json' });
// events and offers: the admin board's own list once it saved one, otherwise the ones of config.json
const offers = createOffers({ cfg, source: () => (adminStore.offers !== null ? adminStore.offers : cfg.offers) });
const mail = createMail({ cfg, playfab, file: dataDir + '/mail.json' });
const events = createEvents({ cfg, playfab, mail, packs: () => adminStore.packs, nameOf: async pf => (await chat.who(pf)).name, source: () => (adminStore.events !== null ? adminStore.events : cfg.events), file: dataDir + '/events.json' });
const codes = createCodes({ store: adminStore, mail, packs: () => adminStore.packs });
// the admin board: needs ADMIN_PASSWORD_HASH (made by set-admin.sh); without it the board does not exist
const admin = createAdmin({
  cfg, store: adminStore, chat, friends, leaderboard, mail, playfab, events, codes, reportsFile: dataDir + '/chat-reports.jsonl',
  adminCfg: Object.assign({}, cfg.admin, process.env.ADMIN_PASSWORD_HASH ? { passwordHash: process.env.ADMIN_PASSWORD_HASH } : {}, process.env.ADMIN_PATH ? { path: process.env.ADMIN_PATH } : {}),
});
const { server } = createApp({ cfg, playfab, store, chat, friends, leaderboard, offers, events, mail, codes, admin });
if (admin.enabled) console.log('admin board on ' + admin.base); else console.log('admin board is off (no ADMIN_PASSWORD_HASH)');

setInterval(() => { events.autoDistribute().catch(() => {}); }, 60 * 1000).unref();      // prizes of the events that ended
setInterval(() => { chat.purge(); friends.purge(); leaderboard.purge(); events.purge(); mail.purge(); }, 3600 * 1000).unref();
setInterval(() => store.purge(Date.now(), 48 * 3600 * 1000, new Date().toISOString().slice(0, 10)), 3600 * 1000).unref();
for (const sig of ['SIGINT', 'SIGTERM']) process.on(sig, () => { try { store.flushNow(); chat.flushNow(); friends.flushNow(); leaderboard.flushNow(); events.flushNow(); mail.flushNow(); adminStore.flushNow(); } finally { process.exit(0); } });

server.listen(cfg.port, cfg.host, () => console.log(`rewards api on ${cfg.host}:${cfg.port} (title ${cfg.titleId})`));
