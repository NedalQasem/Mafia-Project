#!/usr/bin/env bash
# Stores the Facebook App Secret of the game (developers.facebook.com -> the app -> App settings -> Basic -> App secret -> Show).
# Run it as root, ONE command, then paste the secret once:   bash /root/rewards-api/set-facebook.sh
# It is kept only in /etc/mafia-rewards.env (next to the PlayFab key and the admin hash), and the service is restarted with it.
set -e
cd "$(dirname "$0")"
read -rsp "Paste the Facebook App Secret, then press Enter (nothing is shown): " S </dev/tty; echo
S=$(printf '%s' "$S" | tr -d '[:space:]')
case "$S" in *[!0-9a-fA-F]*|"") echo "That does not look like an App Secret (letters a-f and digits only). Nothing was changed."; exit 1;; esac
if [ "${#S}" -lt 24 ]; then echo "Too short to be an App Secret. Nothing was changed."; exit 1; fi
touch /etc/mafia-rewards.env
grep -v '^FACEBOOK_APP_SECRET=' /etc/mafia-rewards.env > /etc/mafia-rewards.env.new || true
printf 'FACEBOOK_APP_SECRET=%s\n' "$S" >> /etc/mafia-rewards.env.new
mv /etc/mafia-rewards.env.new /etc/mafia-rewards.env
chown root:root /etc/mafia-rewards.env && chmod 600 /etc/mafia-rewards.env
unset S
echo "saved: $(wc -l < /etc/mafia-rewards.env) lines in the env file (expect 3: the PlayFab key, the admin hash, the Facebook secret)"
docker compose -f docker-compose.rewards.yml up -d
echo "done. Facebook sign-in is on."
