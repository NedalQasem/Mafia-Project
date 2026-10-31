#!/usr/bin/env bash
# Makes the shared key between this service and the room bot (the program that keeps the Mafia Office room open and runs the bots) and stores it
# in /etc/mafia-rewards.env as BOT_KEY, then restarts the service. Run it as root, once:   bash /root/rewards-api/set-bot-key.sh
# It prints the key ONCE so it can be given to the bot (its BOT_KEY setting); do not paste it anywhere else. Run it again to change the key.
set -e
cd "$(dirname "$0")"
K=$(head -c 24 /dev/urandom | od -An -tx1 | tr -d ' \n')
touch /etc/mafia-rewards.env
grep -v '^BOT_KEY=' /etc/mafia-rewards.env > /etc/mafia-rewards.env.new || true
printf 'BOT_KEY=%s\n' "$K" >> /etc/mafia-rewards.env.new
mv /etc/mafia-rewards.env.new /etc/mafia-rewards.env
chown root:root /etc/mafia-rewards.env && chmod 600 /etc/mafia-rewards.env
docker compose -f docker-compose.rewards.yml up -d
echo "done. The room bot key is:"
echo "$K"
unset K
