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
const { createRecovery } = require('./recovery');
const { createImages } = require('./images');
const { createAccounts } = require('./accounts');
const { createGoogle, createFacebook, createIdentity } = require('./identity');
const FACEBOOK_APP_ID = '2057586301904403';       // public id of the game's Facebook app; its secret is FACEBOOK_APP_SECRET in /etc/mafia-rewards.env (set-facebook.sh)
const GOOGLE_WEB_CLIENT_ID = '1007236526273-19121cletqstpgahntdot10rvhmv5n48.apps.googleusercontent.com';       // public identifier of the game's Google web client (not a secret); config.json can list others in googleClientIds
const { createAdminStore } = require('./adminstore');
const { createOfficialRooms } = require('./officialrooms');
const { createBotFarm } = require('./botfarm');
const { createVoice } = require('./voice');
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
// new mail is announced on the chat poll at once (the game fetches the mail when it hears it)
const mail = createMail({ cfg, playfab, file: dataDir + '/mail.json', notify: pfs => { for (const pf of (pfs || chat.onlinePlayers())) chat.pushEvent(pf, { type: 'mail' }); } });
const { createLeaderPrizes } = require('./lbprizes');
const lbPrizes = createLeaderPrizes({ leaderboard, mail, store: adminStore, packs: () => adminStore.packs, cfg, file: dataDir + '/lbprizes.json' });
leaderboard.setPrizes(lbPrizes);
const events = createEvents({ cfg, playfab, mail, packs: () => adminStore.packs, nameOf: async pf => (await chat.who(pf)).name, source: () => (adminStore.events !== null ? adminStore.events : cfg.events), file: dataDir + '/events.json' });
const codes = createCodes({ store: adminStore, mail, packs: () => adminStore.packs });
const recovery = createRecovery({ store: adminStore, playfab });
const images = createImages({ dir: dataDir + '/images' });
const accounts = createAccounts({ cfg, file: dataDir + '/accounts.json', mail, playfab, recovery });
const identity = createIdentity({ accounts, playfab, chat, providers: {
  google: createGoogle({ clientIds: cfg.googleClientIds || [GOOGLE_WEB_CLIENT_ID] }),
  facebook: createFacebook({ appId: cfg.facebookAppId || FACEBOOK_APP_ID, appSecret: process.env.FACEBOOK_APP_SECRET || '' }),
} });
// official rooms (the Mafia Office room...): kept alive by the room bot, which talks to /v1/bot with BOT_KEY (set-bot-key.sh); a player is "new" for his first 7 days
const NEW_PLAYER_MS = 7 * 24 * 3600 * 1000;
const officialRooms = createOfficialRooms({ file: dataDir + '/officialrooms.json', chat, isNew: pf => { const t = accounts.firstSeen(pf); return t > 0 && Date.now() - t < NEW_PLAYER_MS; } });
chat.setRooms(officialRooms);
const botFarm = createBotFarm({ file: dataDir + '/bots.json' });
const botKey = process.env.BOT_KEY || '';
// the voice of the LiveKit rooms: LIVEKIT_API_KEY / LIVEKIT_API_SECRET / LIVEKIT_URL come from /etc/mafia-rewards.env (set-livekit.sh); without them there is no such voice
const voice = createVoice({ apiKey: process.env.LIVEKIT_API_KEY || '', apiSecret: process.env.LIVEKIT_API_SECRET || '', url: process.env.LIVEKIT_URL || '', officialRooms });
// the admin board: needs ADMIN_PASSWORD_HASH (made by set-admin.sh); without it the board does not exist
const admin = createAdmin({ lbPrizes, officialRooms, botFarm,
  cfg, store: adminStore, chat, friends, leaderboard, mail, playfab, events, codes, recovery, images, accounts, reportsFile: dataDir + '/chat-reports.jsonl',
  adminCfg: Object.assign({}, cfg.admin, process.env.ADMIN_PASSWORD_HASH ? { passwordHash: process.env.ADMIN_PASSWORD_HASH } : {}, process.env.ADMIN_PATH ? { path: process.env.ADMIN_PATH } : {}),
});
const { server } = createApp({ cfg, playfab, store, chat, friends, leaderboard, offers, events, mail, codes, recovery, images, accounts, identity, admin, officialRooms, botFarm, botKey, voice });
if (admin.enabled) console.log('admin board on ' + admin.base); else console.log('admin board is off (no ADMIN_PASSWORD_HASH)');

setInterval(() => { accounts.sweep().then(r => { if (r.warned || r.deleted || r.failed) console.log('guests', JSON.stringify(r)); }).catch(() => {}); }, 6 * 3600 * 1000).unref();     // warns and (only when config guests.purge is true) deletes abandoned guests
setInterval(() => { events.autoDistribute().catch(() => {}); }, 60 * 1000).unref();      // prizes of the events that ended
setInterval(() => { lbPrizes.payDue(); }, 60 * 1000).unref();                            // prizes of the day and the week that ended
setInterval(() => { chat.purge(); friends.purge(); leaderboard.purge(); events.purge(); mail.purge(); }, 3600 * 1000).unref();
setInterval(() => store.purge(Date.now(), 48 * 3600 * 1000, new Date().toISOString().slice(0, 10)), 3600 * 1000).unref();
for (const sig of ['SIGINT', 'SIGTERM']) process.on(sig, () => { try { store.flushNow(); chat.flushNow(); friends.flushNow(); leaderboard.flushNow(); events.flushNow(); mail.flushNow(); adminStore.flushNow(); accounts.flushNow(); officialRooms.flushNow(); botFarm.flushNow(); } finally { process.exit(0); } });

server.listen(cfg.port, cfg.host, () => console.log(`rewards api on ${cfg.host}:${cfg.port} (title ${cfg.titleId})`));
