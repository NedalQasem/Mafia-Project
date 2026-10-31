#!/usr/bin/env bash
# Adds the /lk/ location (LiveKit signalling over the website's nginx) inside the 443 block of api.mena-gaming.com.
# Safe: it keeps a backup, tests nginx before reloading and puts the old file back if the test fails. Run as root:
#   bash /root/livekit/add-nginx-location.sh                      (the nginx folder is /opt/mena-gaming/nginx, the container mena-gaming-nginx-1)
#   NGINX_DIR=... NGINX_CONTAINER=... bash add-nginx-location.sh  (when they are named differently)
set -e
DIR="${NGINX_DIR:-/opt/mena-gaming/nginx}"
CONTAINER="${NGINX_CONTAINER:-mena-gaming-nginx-1}"
CONF="$DIR/nginx.conf"
[ -f "$CONF" ] || { echo "no $CONF"; exit 1; }
if grep -q 'location /lk/' "$CONF"; then echo "already added: nothing to do"; exit 0; fi
cp "$CONF" "$CONF.bak.lk"
cat > /tmp/add-lk.awk <<'AWK'
/^[ \t]*server[ \t]*\{/ { is443 = 0; api = 0 }
/listen[ \t]+443/ { is443 = 1 }
/server_name api\.mena-gaming\.com;/ { api = 1 }
is443 && api && !done && /^[ \t]*location \/ \{/ {
  print "    location /lk/ {"
  print "        set $livekit http://mafia-livekit:7880;"
  print "        rewrite ^/lk/(.*)$ /$1 break;"
  print "        proxy_pass $livekit;"
  print "        proxy_http_version 1.1;"
  print "        proxy_set_header Upgrade $http_upgrade;"
  print "        proxy_set_header Connection \"upgrade\";"
  print "        proxy_set_header Host $host;"
  print "        proxy_read_timeout 3600s;"
  print "    }"
  done = 1
}
{ print }
END { if (!done) exit 3 }
AWK
awk -f /tmp/add-lk.awk "$CONF.bak.lk" > "$CONF" || { cp "$CONF.bak.lk" "$CONF"; echo "could not find the api.mena-gaming.com 443 block: nothing changed"; exit 1; }
if docker exec "$CONTAINER" nginx -t && docker exec "$CONTAINER" nginx -s reload; then echo "done: /lk/ is on"; else cp "$CONF.bak.lk" "$CONF"; docker exec "$CONTAINER" nginx -s reload || true; echo "nginx test failed: the old file is back"; exit 1; fi
