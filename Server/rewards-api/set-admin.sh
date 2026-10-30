#!/usr/bin/env bash
# Sets the password of MAFIA ADMIN BOARD (the admin web page of the service). Run it as root, ONE command, in the folder of the service:
#   bash /root/rewards-api/set-admin.sh
# It asks for the password twice (hidden), stores only its hash in /etc/mafia-rewards.env, and restarts the service with it.
set -e
cd "$(dirname "$0")"
read -rsp "Admin password (at least 10 characters), then Enter (nothing is shown): " P </dev/tty; echo
read -rsp "Type it again: " P2 </dev/tty; echo
if [ "$P" != "$P2" ]; then echo "The two passwords are different. Nothing was changed."; exit 1; fi
if [ "${#P}" -lt 10 ]; then echo "Too short (at least 10 characters). Nothing was changed."; exit 1; fi
H=$(printf '%s' "$P" | docker exec -i mafia-rewards node src/admin-hash.js)
unset P P2
case "$H" in scrypt:*) ;; *) echo "Could not make the hash (is the service running? start it first). Nothing was changed."; exit 1;; esac
touch /etc/mafia-rewards.env
grep -v '^ADMIN_PASSWORD_HASH=' /etc/mafia-rewards.env > /etc/mafia-rewards.env.new || true
printf 'ADMIN_PASSWORD_HASH=%s\n' "$H" >> /etc/mafia-rewards.env.new
mv /etc/mafia-rewards.env.new /etc/mafia-rewards.env
chown root:root /etc/mafia-rewards.env && chmod 600 /etc/mafia-rewards.env
echo "saved: $(wc -l < /etc/mafia-rewards.env) lines in the env file (expect 2: the PlayFab key and the admin hash)"
docker compose -f docker-compose.rewards.yml up -d
echo "done. Open https://api.mena-gaming.com/admin"
