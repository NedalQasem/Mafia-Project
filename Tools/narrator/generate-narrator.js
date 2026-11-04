#!/usr/bin/env node
/*
 * Generates the narrator's voice-over clips with ElevenLabs from Docs/narrator-lines.md.
 *
 *   node Tools/narrator/generate-narrator.js voices                 list the voices of the account (id, name)
 *   node Tools/narrator/generate-narrator.js generate [options]     generate the clips
 *
 * The key and the voices come from environment variables, never from a file (nothing secret is committed):
 *   ELEVENLABS_API_KEY   the account's API key
 *   ELEVENLABS_VOICE_M   voice id of the man narrator   (used for Arabic and English)
 *   ELEVENLABS_VOICE_F   voice id of the woman narrator (used for Arabic and English)
 * Optional:
 *   ELEVENLABS_MODEL      model id (default eleven_v3; eleven_multilingual_v2 is the steadier alternative)
 *   ELEVENLABS_STABILITY  0..1 (default 0.5; the v3 model only takes 0, 0.5 or 1)
 *   ELEVENLABS_SPEED      0.7..1.2 (default 0.9: slow and deliberate)
 *   ELEVENLABS_TAG        a style tag put before every line for the v3 model, e.g. "[low, calm]" (default none)
 *
 * generate options:
 *   --lang ar|en     only this language (default both)
 *   --voice m|f      only this voice (default both)
 *   --only a,b       only these file names (e.g. dawn_kill,call_mafia)
 *   --force          overwrite clips that already exist (default: skip them, so a rerun only does what is missing)
 *   --dry-run        print what would be generated and how many characters it costs, spend nothing
 *   --out <folder>   where to write (default Assets/_Project/Resources/Narrator); use another folder to audition
 */
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..', '..');
const API = 'https://api.elevenlabs.io/v1';

function fail(msg) { console.error('ERROR: ' + msg); process.exit(1); }
const sleep = ms => new Promise(r => setTimeout(r, ms));

// ---------------------------------------------------------------- the recording list
function readLines() {
    const doc = fs.readFileSync(path.join(root, 'Docs', 'narrator-lines.md'), 'utf8');
    const iAr = doc.indexOf('## العربي'), iEn = doc.indexOf('## الإنجليزي');
    if (iAr < 0 || iEn < 0) fail('Docs/narrator-lines.md must have the sections "## العربي" and "## الإنجليزي"');
    const sections = { ar: doc.slice(iAr, iEn), en: doc.slice(iEn) };
    const out = [];
    for (const lang of Object.keys(sections)) {
        const re = /^\|\s*`([a-z_]+)`\s*\|\s*(.+?)\s*\|\s*(\d+)\s*\|\s*$/gm;
        let m;
        while ((m = re.exec(sections[lang]))) out.push({ lang, file: m[1], text: m[2], seconds: Number(m[3]) });
    }
    return out;
}

// ---------------------------------------------------------------- calls
async function call(url, options, tries = 4) {
    for (let i = 1; ; i++) {
        let res;
        try { res = await fetch(url, options); }
        catch (e) { if (i >= tries) throw e; await sleep(1500 * i); continue; }
        if (res.ok) return res;
        const retry = res.status === 429 || res.status >= 500;
        if (!retry || i >= tries) {
            const body = await res.text().catch(() => '');
            fail('ElevenLabs answered ' + res.status + ' ' + res.statusText + ': ' + body.slice(0, 600));
        }
        await sleep(2000 * i);
    }
}

async function listVoices(key) {
    const res = await call(API + '/voices', { headers: { 'xi-api-key': key } });
    const data = await res.json();
    for (const v of data.voices || []) {
        const labels = v.labels ? Object.values(v.labels).join(', ') : '';
        console.log(v.voice_id + '  ' + (v.name || '').padEnd(28) + (v.category || '').padEnd(14) + labels);
    }
}

async function speak(key, voiceId, text, settings) {
    const body = {
        text: (settings.tag ? settings.tag + ' ' : '') + text,
        model_id: settings.model,
        voice_settings: { stability: settings.stability, similarity_boost: 0.75, style: 0.4, use_speaker_boost: true, speed: settings.speed },
    };
    const res = await call(API + '/text-to-speech/' + voiceId + '?output_format=mp3_44100_128', {
        method: 'POST',
        headers: { 'xi-api-key': key, 'Content-Type': 'application/json', Accept: 'audio/mpeg' },
        body: JSON.stringify(body),
    });
    return Buffer.from(await res.arrayBuffer());
}

// ---------------------------------------------------------------- main
function parseArgs(argv) {
    const o = { cmd: argv[0], lang: null, voice: null, only: null, force: false, dry: false, out: path.join(root, 'Assets', '_Project', 'Resources', 'Narrator') };
    for (let i = 1; i < argv.length; i++) {
        const a = argv[i];
        if (a === '--lang') o.lang = argv[++i];
        else if (a === '--voice') o.voice = argv[++i];
        else if (a === '--only') o.only = String(argv[++i]).split(',');
        else if (a === '--force') o.force = true;
        else if (a === '--dry-run') o.dry = true;
        else if (a === '--out') o.out = path.resolve(argv[++i]);
        else fail('unknown option ' + a);
    }
    return o;
}

async function main() {
    const o = parseArgs(process.argv.slice(2));
    const key = process.env.ELEVENLABS_API_KEY;
    if (o.cmd === 'voices') { if (!key) fail('set ELEVENLABS_API_KEY first'); return listVoices(key); }
    if (o.cmd !== 'generate') fail('usage: generate-narrator.js voices | generate [--lang ar|en] [--voice m|f] [--only a,b] [--force] [--dry-run] [--out folder]');

    const voices = { m: process.env.ELEVENLABS_VOICE_M, f: process.env.ELEVENLABS_VOICE_F };
    const settings = {
        model: process.env.ELEVENLABS_MODEL || 'eleven_v3',
        stability: Number(process.env.ELEVENLABS_STABILITY || 0.5),
        speed: Number(process.env.ELEVENLABS_SPEED || 0.9),
        tag: process.env.ELEVENLABS_TAG || '',
    };

    let jobs = [];
    for (const line of readLines())
        for (const v of ['m', 'f']) jobs.push({ ...line, voice: v, target: path.join(o.out, line.lang, v, line.file + '.mp3') });
    if (o.lang) jobs = jobs.filter(j => j.lang === o.lang);
    if (o.voice) jobs = jobs.filter(j => j.voice === o.voice);
    if (o.only) jobs = jobs.filter(j => o.only.includes(j.file));
    if (!o.force) jobs = jobs.filter(j => !fs.existsSync(j.target));
    if (!jobs.length) return console.log('nothing to do (every clip exists; use --force to redo them)');

    const chars = jobs.reduce((n, j) => n + j.text.length, 0);
    console.log(jobs.length + ' clips, ' + chars + ' characters (about that many credits), model ' + settings.model);
    if (o.dry) { for (const j of jobs) console.log('  ' + j.lang + '/' + j.voice + '/' + j.file + '  ' + j.text); return; }
    if (!key) fail('set ELEVENLABS_API_KEY first');
    for (const v of new Set(jobs.map(j => j.voice))) if (!voices[v]) fail('set ELEVENLABS_VOICE_' + v.toUpperCase() + ' (run "voices" to see the ids)');

    let done = 0;
    for (const j of jobs) {
        const audio = await speak(key, voices[j.voice], j.text, settings);
        fs.mkdirSync(path.dirname(j.target), { recursive: true });
        fs.writeFileSync(j.target + '.tmp', audio);
        fs.renameSync(j.target + '.tmp', j.target);
        const seconds = audio.length * 8 / 128000;          // 128 kbps mp3
        const warn = seconds > j.seconds ? '   <-- LONGER than its scene (' + j.seconds + 's): shorten the line or raise ELEVENLABS_SPEED' : '';
        console.log('[' + (++done) + '/' + jobs.length + '] ' + j.lang + '/' + j.voice + '/' + j.file + '.mp3  ~' + seconds.toFixed(1) + 's' + warn);
    }
    console.log('done. Open Unity so it imports the new clips.');
}

main().catch(e => fail(e && e.message ? e.message : String(e)));
