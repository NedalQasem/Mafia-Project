#!/usr/bin/env bash
# Writes /etc/mafia-rewards.env with the PlayFab secret key you paste (hidden, nothing is printed).
# Run it as root, ONE command, then paste the key once:   bash /root/rewards-api/set-key.sh
set -e
read -rsp "Paste the PlayFab secret key, then press Enter (nothing is shown): " K </dev/tty; echo
K=$(printf '%s' "$K" | tr -d '[:space:]')
if [ "${#K}" -lt 20 ]; then echo "That is too short to be a key. Nothing was changed."; exit 1; fi
printf 'PLAYFAB_SECRET_KEY=%s\n' "$K" > /etc/mafia-rewards.env
chown root:root /etc/mafia-rewards.env
chmod 600 /etc/mafia-rewards.env
unset K
echo "saved: $(wc -l < /etc/mafia-rewards.env) line, $(wc -c < /etc/mafia-rewards.env) bytes (expect 1 line, about 70 bytes)"
