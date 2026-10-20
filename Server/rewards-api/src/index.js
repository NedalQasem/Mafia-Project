'use strict';
// Entry point: reads config.json (path in CONFIG, default ./config.json) and the PlayFab secret key from the environment.
// The secret key is NEVER in a file of this project: it lives in the service's environment file on the server only.

const fs = require('node:fs');
const path = require('node:path');
const { createApp } = require('./app');
const { createPlayFab } = require('./playfab');
const { createStore } = require('./store');

const cfgPath = process.env.CONFIG || path.join(__dirname, '..', 'config.json');
const cfg = JSON.parse(fs.readFileSync(cfgPath, 'utf8'));
const secretKey = process.env.PLAYFAB_SECRET_KEY;
if (!secretKey) { console.error('PLAYFAB_SECRET_KEY is not set (put it in the service environment file).'); process.exit(1); }

const store = createStore(cfg.dataFile);
const playfab = createPlayFab({ titleId: cfg.titleId, secretKey });
const { server } = createApp({ cfg, playfab, store });

setInterval(() => store.purge(Date.now(), 48 * 3600 * 1000, new Date().toISOString().slice(0, 10)), 3600 * 1000).unref();
for (const sig of ['SIGINT', 'SIGTERM']) process.on(sig, () => { try { store.flushNow(); } finally { process.exit(0); } });

server.listen(cfg.port, cfg.host, () => console.log(`rewards api on ${cfg.host}:${cfg.port} (title ${cfg.titleId})`));
