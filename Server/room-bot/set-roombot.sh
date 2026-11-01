#!/usr/bin/env bash
# Writes /etc/mafia-roombot.env for the room bot: the game's public EOS ids, the EOS client secret (pasted once, nothing is shown) and the bot key
# that set-bot-key.sh already stored for the rewards service. Run it as root, once, from the repository root:   bash Server/room-bot/set-roombot.sh
set -e
KEY=$(grep '^BOT_KEY=' /etc/mafia-rewards.env | cut -d= -f2-)
[ -n "$KEY" ] || { echo "no BOT_KEY in /etc/mafia-rewards.env: run  bash /root/rewards-api/set-bot-key.sh  first"; exit 1; }
read -rsp "Paste the EOS client secret (the game's Environment asset, field clientSecret), then press Enter (nothing is shown): " S </dev/tty; echo
S=$(printf '%s' "$S" | tr -d '[:space:]')
[ "${#S}" -ge 16 ] || { echo "That is too short to be the client secret. Nothing was changed."; exit 1; }
cat > /etc/mafia-roombot.env <<ENV
EOS_PRODUCT_ID=3c77dadb6b6b47f58c82618942ad4794
EOS_SANDBOX_ID=26406e73c75d486494c9626d4ab2d087
EOS_DEPLOYMENT_ID=c96c080c4a6e4468844fe1960416d878
EOS_CLIENT_ID=xyza7891rUkzmHE8Lpj1Mgsf7TKOVM3J
EOS_CLIENT_SECRET=$S
BOT_KEY=$KEY
API_URL=https://api.mena-gaming.com
ENV
chown root:root /etc/mafia-roombot.env && chmod 600 /etc/mafia-roombot.env
unset S KEY
echo "saved. Start the bot:  docker compose -f Server/room-bot/docker-compose.roombot.yml up -d --build"
