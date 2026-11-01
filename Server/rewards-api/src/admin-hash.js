'use strict';
// Prints the hash of an admin password for the env file (ADMIN_PASSWORD_HASH=...). The password is read from stdin so it never appears in a command line:
//   printf '%s' "the password" | node src/admin-hash.js
const { hashPassword } = require('./admin');
let pw = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', c => { pw += c; });
process.stdin.on('end', () => {
  pw = pw.replace(/[\r\n]+$/, '');
  if (pw.length < 10) { console.error('The password must have at least 10 characters. Nothing was changed.'); process.exit(1); }
  console.log(hashPassword(pw));
});
