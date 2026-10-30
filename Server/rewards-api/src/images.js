'use strict';
// Pictures the admin attaches to private messages. The admin page shrinks them before sending (a JPEG of at most ~900 px); the server only checks that
// the bytes really are a JPEG or a PNG, keeps them in a folder under a random name, and serves them to the game by that name.

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

const MAX_BYTES = 1024 * 1024;
const ID_RE = /^[a-f0-9]{24}$/;

function kindOf(buf) {
  if (buf.length > 4 && buf[0] === 0xff && buf[1] === 0xd8 && buf[2] === 0xff) return { ext: 'jpg', mime: 'image/jpeg' };
  if (buf.length > 8 && buf.subarray(0, 8).equals(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]))) return { ext: 'png', mime: 'image/png' };
  return null;
}

/** dir = null keeps the pictures in memory (tests). */
function createImages({ dir = null } = {}) {
  const mem = new Map();
  if (dir) fs.mkdirSync(dir, { recursive: true });

  /** Returns the id of the stored picture, or throws an Error with code 'bad_image' / 'image_too_big'. */
  function save(buf) {
    if (!Buffer.isBuffer(buf) || !buf.length) throw Object.assign(new Error('bad_image'), { code: 'bad_image' });
    if (buf.length > MAX_BYTES) throw Object.assign(new Error('image_too_big'), { code: 'image_too_big' });
    const k = kindOf(buf);
    if (!k) throw Object.assign(new Error('bad_image'), { code: 'bad_image' });
    const id = crypto.randomBytes(12).toString('hex');
    if (dir) fs.writeFileSync(path.join(dir, id + '.' + k.ext), buf); else mem.set(id, { buf, mime: k.mime });
    return id;
  }

  /** { buf, mime } or null. */
  function get(id) {
    if (!ID_RE.test(String(id))) return null;
    if (!dir) return mem.get(id) || null;
    for (const [ext, mime] of [['jpg', 'image/jpeg'], ['png', 'image/png']]) {
      try { return { buf: fs.readFileSync(path.join(dir, id + '.' + ext)), mime }; } catch { /* next */ }
    }
    return null;
  }

  return { save, get, exists: id => !!get(id), MAX_BYTES, ID_RE };
}

module.exports = { createImages };
