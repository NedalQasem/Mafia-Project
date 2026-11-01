#!/usr/bin/env bash
# Makes the LiveKit API key and secret, stores them for BOTH containers, opens the two media ports and starts LiveKit.
# Run it as root, once:   bash /root/livekit/set-livekit.sh     (run it again to change the keys)
# The secret is never printed: it lives only in /etc/mafia-livekit.env and /etc/mafia-rewards.env (chmod 600).
set -e
cd "$(dirname "$0")"
KEY="API$(head -c 8 /dev/urandom | od -An -tx1 | tr -d ' \n')"
SECRET=$(head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n')
URL="${LIVEKIT_PUBLIC_URL:-wss://api.mena-gaming.com/lk}"
printf 'LIVEKIT_KEYS="%s: %s"\n' "$KEY" "$SECRET" > /etc/mafia-livekit.env
chown root:root /etc/mafia-livekit.env && chmod 600 /etc/mafia-livekit.env
touch /etc/mafia-rewards.env
grep -v '^LIVEKIT_' /etc/mafia-rewards.env > /etc/mafia-rewards.env.new || true
printf 'LIVEKIT_API_KEY=%s\nLIVEKIT_API_SECRET=%s\nLIVEKIT_URL=%s\n' "$KEY" "$SECRET" "$URL" >> /etc/mafia-rewards.env.new
mv /etc/mafia-rewards.env.new /etc/mafia-rewards.env
chown root:root /etc/mafia-rewards.env && chmod 600 /etc/mafia-rewards.env
if command -v ufw >/dev/null 2>&1; then ufw allow 7881/tcp && ufw allow 7882/udp; fi
docker compose -f docker-compose.livekit.yml up -d
( cd /root/rewards-api && docker compose -f docker-compose.rewards.yml up -d )
echo "done. LiveKit key (public part): $KEY"
echo "the secret is stored in /etc/mafia-livekit.env (needed for the load test: sudo cat it yourself)"
unset SECRET
