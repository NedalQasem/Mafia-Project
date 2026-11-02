'use strict';
// The test journal: what the game writes on a test phone (every log line, screens, taps, room and voice events, and what it finds illogical)
// arrives here in batches and is kept per player and per day, so a problem can be read afterwards (admin board: /admin/diag).
// Lines look like "HH:mm:ss.fff +seconds KIND Tag | text" (UTC); a batch also says which run of the game (sid) and which phone it came from.

const fs = require('node:fs');
const path = require('node:path');

const MAX_LINES_PER_BATCH = 600;
const MAX_LINE = 700;
const MAX_DAY_BYTES = 4 * 1024 * 1024;       // one player's journal of one day
const KEEP_DAYS = 14;

function createDiag({ dir, nameOf = async () => '', now = () => Date.now() } = {}) {
  const safePf = pf => (typeof pf === 'string' && /^[A-Za-z0-9]{3,40}$/.test(pf) ? pf : null);
  const safeDay = d => (typeof d === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(d) ? d : null);
  const dayOf = t => new Date(t).toISOString().slice(0, 10);
  const pfDir = pf => path.join(dir, pf);

  function readMeta(pf) { try { return JSON.parse(fs.readFileSync(path.join(pfDir(pf), 'meta.json'), 'utf8')); } catch { return {}; } }

  /** A batch from the game (the player is the signed-in one). */
  async function upload(pf, body) {
    const id = safePf(pf);
    if (!id || !dir) return { ok: false, error: 'bad_request' };
    const lines = Array.isArray(body && body.lines) ? body.lines.slice(0, MAX_LINES_PER_BATCH) : [];
    if (!lines.length) return { ok: true, saved: 0 };
    const sid = String((body && body.sid) || '').replace(/[^A-Za-z0-9]/g, '').slice(0, 12) || 'unknown';
    const tag = sid.slice(0, 4);
    const t = now();
    const file = path.join(pfDir(id), dayOf(t) + '.log');
    fs.mkdirSync(pfDir(id), { recursive: true });
    let size = 0; try { size = fs.statSync(file).size; } catch { /* a new day */ }
    if (size > MAX_DAY_BYTES) return { ok: true, saved: 0, full: true };
    const text = lines.map(l => tag + ' ' + String(l).replace(/[\r\n]+/g, ' ').slice(0, MAX_LINE)).join('\n') + '\n';
    fs.appendFileSync(file, text);
    const meta = readMeta(id);
    meta.lastAt = t;
    meta.device = String((body && body.device) || meta.device || '').slice(0, 80);
    meta.build = String((body && body.build) || meta.build || '').slice(0, 30);
    try { const n = await nameOf(id); if (n) meta.name = n; } catch { /* the name is a convenience */ }
    fs.writeFileSync(path.join(pfDir(id), 'meta.json'), JSON.stringify(meta));
    return { ok: true, saved: lines.length };
  }

  /** The players who sent a journal, newest first, with the days they have. */
  function players() {
    if (!dir) return [];
    let names = [];
    try { names = fs.readdirSync(dir); } catch { return []; }
    const out = [];
    for (const pf of names) {
      if (!safePf(pf)) continue;
      let days = [];
      try { days = fs.readdirSync(pfDir(pf)).filter(f => f.endsWith('.log')).map(f => f.slice(0, -4)).sort().reverse(); } catch { continue; }
      if (!days.length) continue;
      const m = readMeta(pf);
      out.push({ pf, name: m.name || '', device: m.device || '', build: m.build || '', lastAt: m.lastAt || 0, days });
    }
    return out.sort((a, b) => b.lastAt - a.lastAt);
  }

  /** One player's journal of one day (the lines in the order they arrived). */
  function log(pf, day) {
    const id = safePf(pf), d = safeDay(day);
    if (!id || !d) return null;
    try { return fs.readFileSync(path.join(pfDir(id), d + '.log'), 'utf8'); } catch { return null; }
  }

  /**
   * Several players' journals of one day on one timeline (sorted by the UTC time of each line), each line marked with the player's name:
   * for a test with two phones, the room as both of them lived it.
   */
  function merged(pfs, day) {
    const rows = [];
    for (const pf of pfs) {
      const text = log(pf, day);
      if (text === null) continue;
      const m = readMeta(pf);
      const who = (m.name || pf).slice(0, 14);
      let n = 0;
      for (const line of text.split('\n')) {
        if (!line) continue;
        const sp = line.indexOf(' ');                    // "sid HH:mm:ss.fff +..." : the run tag, then the line
        const body = line.slice(sp + 1);
        rows.push({ key: body.slice(0, 12), n: n++, text: '[' + who + ' ' + line.slice(0, sp) + '] ' + body });
      }
    }
    rows.sort((a, b) => (a.key < b.key ? -1 : a.key > b.key ? 1 : a.n - b.n));
    return rows.map(r => r.text).join('\n') + '\n';
  }

  function purge() {
    if (!dir) return;
    const limit = dayOf(now() - KEEP_DAYS * 24 * 3600 * 1000);
    for (const p of players()) for (const d of p.days) if (d < limit) { try { fs.unlinkSync(path.join(pfDir(p.pf), d + '.log')); } catch { /* gone */ } }
  }

  /** The small page of the admin board that lists the players and opens a journal as plain text. */
  function page(base) {
    return `<!doctype html><html lang="ar" dir="rtl"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>سجل التجربة</title>
<style>body{background:#120d0a;color:#e9dcc0;font:15px system-ui,sans-serif;margin:0;padding:16px}h1{font-size:20px;margin:0 0 4px}p{color:#9a8c70;margin:0 0 14px}
table{border-collapse:collapse;width:100%}td,th{border-bottom:1px solid #2a2018;padding:8px 6px;text-align:right}a{color:#e8c27a}label{display:inline-flex;gap:6px;align-items:center}
.pill{display:inline-block;background:#2a2018;border-radius:10px;padding:2px 8px;margin:2px}button{background:#b99048;border:0;border-radius:8px;padding:8px 14px;font-weight:700;cursor:pointer}</style>
<h1>سجل التجربة</h1><p>كل شي صار على حسابات التجربة: سجلات اللعبة، الشاشات، الضغطات، أحداث الغرف والصوت، وكل شي مش منطقي (سطور AN). الأوقات UTC.</p>
<div id="t">جاري التحميل…</div><p id="m"></p>
<script>
const B=${JSON.stringify(base)};
fetch(B+'/api/diag/players').then(r=>r.json()).then(d=>{
  if(!d.ok){document.getElementById('t').textContent='سجّل الدخول من لوحة الإدارة أولاً';return}
  const rows=d.players.map(p=>'<tr><td><label><input type="checkbox" class="c" value="'+p.pf+'"> '+(p.name||p.pf)+'</label></td><td>'+p.device+'</td><td>'+p.build+'</td><td>'+new Date(p.lastAt).toLocaleString()+'</td><td>'+p.days.map(x=>'<a class="pill" target="_blank" href="'+B+'/api/diag/log?pf='+p.pf+'&day='+x+'">'+x+'</a>').join('')+'</td></tr>').join('');
  document.getElementById('t').innerHTML='<table><tr><th>اللاعب</th><th>الجهاز</th><th>النسخة</th><th>آخر إرسال</th><th>الأيام</th></tr>'+rows+'</table><p><br>حدّد أكثر من لاعب لتشوف سجلاتهم على خط زمني واحد: <input id="d" size="10" value="'+new Date().toISOString().slice(0,10)+'"> <button onclick="go()">اعرضهم معاً</button></p>';
});
function go(){const v=[...document.querySelectorAll('.c:checked')].map(x=>x.value);if(!v.length){document.getElementById('m').textContent='حدّد لاعب واحد على الأقل';return}
window.open(B+'/api/diag/log?pf='+v.join(',')+'&day='+document.getElementById('d').value,'_blank')}
</script></html>`;
  }

  return { upload, players, log, merged, purge, page };
}

module.exports = { createDiag };
