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

const cfgPath = process.env.CONFIG || path.join(__dirname, '..', 'config.json');
const cfg = JSON.parse(fs.readFileSync(cfgPath, 'utf8'));
if (process.env.HOST) cfg.host = process.env.HOST;            // inside Docker the service listens on 0.0.0.0 (the port is not published)
if (process.env.DATA_FILE) cfg.dataFile = process.env.DATA_FILE;
const secretKey = process.env.PLAYFAB_SECRET_KEY;
if (!secretKey) { console.error('PLAYFAB_SECRET_KEY is not set (put it in the service environment file).'); process.exit(1); }

const store = createStore(cfg.dataFile);
const playfab = createPlayFab({ titleId: cfg.titleId, secretKey });
const dataDir = require('node:path').dirname(cfg.dataFile);
const chat = createChat({ cfg, playfab, file: dataDir + '/chat.json', reportsFile: dataDir + '/chat-reports.jsonl' });
const friends = createFriends({ chat, cfg, file: dataDir + '/friends.json', reportsFile: dataDir + '/chat-reports.jsonl' });
const { server } = createApp({ cfg, playfab, store, chat, friends });

setInterval(() => { chat.purge(); friends.purge(); }, 3600 * 1000).unref();
setInterval(() => store.purge(Date.now(), 48 * 3600 * 1000, new Date().toISOString().slice(0, 10)), 3600 * 1000).unref();
for (const sig of ['SIGINT', 'SIGTERM']) process.on(sig, () => { try { store.flushNow(); chat.flushNow(); friends.flushNow(); } finally { process.exit(0); } });

server.listen(cfg.port, cfg.host, () => console.log(`rewards api on ${cfg.host}:${cfg.port} (title ${cfg.titleId})`));
