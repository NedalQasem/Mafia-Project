# خدمة الجوائز (Rewards API)

سيرفر صغير (Node.js) على الـ VPS. اللعبة بتبعتله نتيجة المباراة، وهو بيحسب الجوائز وبيكتبها على PlayFab بالمفتاح السري. **اللعبة ما بتعطي نفسها جوائز أبداً.**

## كيف بيشتغل
1. نهاية المباراة: كل لاعب جهازه بيبعت `POST /v1/match-result` بتذكرة جلسة PlayFab الخاصة فيه (الهيدر `X-Authorization`).
2. السيرفر بيتحقق من التذكرة عند PlayFab (`AuthenticateSessionTicket`) فبيعرف مين اللاعب بدون ما يصدّق اسمه من الرسالة.
3. التقارير اللي بتتفق على **مين لعب ومين فاز** بتتجمّع. ما بيندفع شي إلا إذا **أكتر من نص اللاعبين** بلّغوا نفس النتيجة. لاعب واحد بيألّف نتيجة ما بيقدر يدفع لنفسه.
4. عند اكتمال النصاب: الـ XP والمستوى ونقاط الرتبة والإحصائيات بتنكتب بـ PlayFab `UserReadOnlyData` (مفتاح `Progress`، اللاعب بيقراه بس ما بيقدر يكتبه)، والكوينز بتنزاد `AddUserVirtualCurrency` (كود `CO`).
5. نفس الطلب ممكن يتكرر بأمان: ما بيندفع مرتين أبداً، وإذا فشلت خطوة (PlayFab واقع) بتكمل لما يرجع.
6. حماية إضافية: سقف مباريات مدفوعة باليوم لكل لاعب، نافذة تبليغ 15 دقيقة، حد طلبات لكل IP، وحجم الطلب 8KB.

كل الأرقام (XP، كوينز، السقف، النصاب...) بـ `config.json`.

## الدردشة (نفس الخدمة)

الخدمة بتشغّل كمان دردشة اللعبة: الساحة العامة، الرسائل الخاصة، المجموعات، الإبلاغ والحظر والكتم التلقائي. اللعبة بتفتح طلب انتظار طويل (long poll) كل ~20 ثانية لتسمع الرسائل الجديدة. الرسائل بتنحفظ بملف `chat.json` بنفس الـvolume (آخر 300 بالعامة و200 بكل محادثة)، والبلاغات بملف `chat-reports.jsonl` (سطر لكل بلاغ، بتقراها بـ`docker exec mafia-rewards cat /data/chat-reports.jsonl`).

- **الكلمات الممنوعة:** حطها بـ`config.json` بقسم `chat.bannedWords` (مصفوفة كلمات) ثم `docker restart mafia-rewards`. بتقارن بعد توحيد التشكيل والألف والياء والتاء المربوطة، وبتعوّض الكلمة بنجوم.
- **تجربة محلية بدون PlayFab:** `node dev-server.js` بيشغّل نسخة تجريبية على `http://127.0.0.1:8788` ببلاغات ولاعبين وهميين (للمحرر فقط، بالمفتاح `PlayerPrefs MafiaApiUrl`).

## التركيب على الـ VPS (Docker، جنب موقع mena-gaming.com)

> موقعك بيشتغل بـ Docker: حاوية nginx بتمسك 80 و443، ومعها frontend وbackend وpostgres. الخدمة بتشتغل **حاوية منفصلة** على نفس الشبكة، بدون ما نغيّر `docker-compose.yml` تبع الموقع. **ما بنركّب nginx ولا certbot على السيرفر نفسه** (بيتصادموا مع حاوية الموقع على 80/443).
>
> **انتبه للمكان:** أوامر `[جهازك]` بتتنفذ بـ PowerShell على ويندوز (الـ prompt بيبدأ بـ `PS C:\`). أوامر `[السيرفر]` بتتنفذ بعد `ssh root@IP` (الـ prompt بيبدأ بـ `root@srv`). ما تنسخ أسطر الشرح اللي فيها `#`، ولا نص عربي.

### 1) السبدومين
بلوحة الدومين: سجل **A** باسم `api` يشير لعنوان الـ VPS. تأكد من السيرفر: `getent hosts api.mena-gaming.com` لازم يرجّع عنوان السيرفر.

### 2) رفع الخدمة — [جهازك]
```powershell
cd "F:\Game_Projects\Mafia Voice Chat project\Server"
scp -r rewards-api root@IP_السيرفر:/root/rewards-api
```
(بتكتب عنوان السيرفر الحقيقي مكان `IP_السيرفر`، وبتدخل باسورد السيرفر.)

### 3) المفتاح السري — [السيرفر]
من PlayFab: **Settings ← Secret keys ← New Secret Key**، وبعدين احذف المفتاح المؤقت القديم. بالسيرفر الأمر الأول بيطلب منك تلصق المفتاح وهو **مخفي**:
```bash
read -rsp "PlayFab secret key: " K; echo
printf 'PLAYFAB_SECRET_KEY=%s\n' "$K" | sudo tee /etc/mafia-rewards.env >/dev/null; unset K
sudo chown root:root /etc/mafia-rewards.env && sudo chmod 600 /etc/mafia-rewards.env
sudo wc -l -c /etc/mafia-rewards.env      # سطر واحد، حوالي 40-80 بايت
```

### 4) تشغيل الحاوية — [السيرفر]
```bash
cd /root/rewards-api && cp config.example.json config.json
docker compose -f docker-compose.rewards.yml up -d --build
docker logs mafia-rewards                       # rewards api on 0.0.0.0:8787
docker exec mena-gaming-nginx-1 wget -qO- http://mafia-rewards:8787/health     # {"ok":true}
```
إذا اسم شبكة الموقع غير `mena-gaming_default` (شوفه بـ `docker network ls`): `SITE_NETWORK=الاسم docker compose -f docker-compose.rewards.yml up -d --build`.

### 5) شهادة HTTPS — [السيرفر]
نفس طريقة الموقع (webroot على نفس الـ volumes)، بحاوية certbot مؤقتة:
```bash
docker run --rm -v mena-gaming_certbot_conf:/etc/letsencrypt -v mena-gaming_certbot_www:/var/www/certbot certbot/certbot certonly --webroot -w /var/www/certbot -d api.mena-gaming.com --agree-tos --register-unsafely-without-email
```
التجديد: آلية تجديد شهادة الموقع بتشمل هالشهادة لأنها بنفس الـ volume (راجع `DEPLOY.md` تبع الموقع).

### 6) تحويل api.mena-gaming.com للخدمة — [السيرفر]
بنضيف بلوكين لآخر ملف nginx تبع الموقع، مع نسخة احتياطية، والتراجع تلقائي إذا فشل الفحص:
```bash
cd /opt/mena-gaming/nginx && cp nginx.conf nginx.conf.bak
cat >> nginx.conf <<'NGINX'

# ---- rewards API ----
server {
    listen 80;
    server_name api.mena-gaming.com;
    location /.well-known/acme-challenge/ { root /var/www/certbot; }
    location / { return 301 https://$host$request_uri; }
}
server {
    listen 443 ssl;
    http2 on;
    server_name api.mena-gaming.com;
    server_tokens off;
    ssl_certificate     /etc/letsencrypt/live/api.mena-gaming.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.mena-gaming.com/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    add_header Strict-Transport-Security "max-age=31536000" always;
    client_max_body_size 16k;
    resolver 127.0.0.11 valid=10s;
    location / {
        set $rewards http://mafia-rewards:8787;
        proxy_pass $rewards;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $remote_addr;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 30s;
    }
}
NGINX
docker exec mena-gaming-nginx-1 nginx -t && docker exec mena-gaming-nginx-1 nginx -s reload || { cp nginx.conf.bak nginx.conf; echo "رجعنا الإعداد القديم"; }
```
اختبار: `https://api.mena-gaming.com/health` من أي جهاز لازم يرد `{"ok":true}`، وموقعك الأساسي يضل شغّال.

### جدار الحماية
المنفذ 8787 ما بينفتح (الحاوية ما بتنشره). قواعد ufw الحالية (22/80/443) كافية.

## بعد ما تشتغل
- بلوحة PlayFab: عطّل **Allow client to add virtual currency** (Settings ← API Features).
- مراقبة: `docker logs -f mafia-rewards`
- الحالة (مباريات الـ48 ساعة الأخيرة فقط، فخسارتها مش خطيرة) بـ docker volume اسمه `rewards-data`.
- تحديث الكود: ارفع المجلد من جديد ثم `docker compose -f docker-compose.rewards.yml up -d --build`. وتحديثات النظام: `sudo apt update && sudo apt upgrade` شهرياً.

## ملاحظات أمان
- المفتاح السري بس بـ `/etc/mafia-rewards.env` (صلاحية 600، بيدخل الحاوية كمتغير بيئة). ما ينحط بـ Git ولا بالشات ولا باللعبة.
- لو تسرّب المفتاح: احذفه من PlayFab فوراً وأنشئ غيره.
- اللاعب بيقدر يكذب بدوره أو بنجاته (بيوصله بونص صغير)، لكنه ما بيقدر يألّف مباراة لحاله ولا يتخطى السقف اليومي. الاتفاق مع أغلبية اللاعبين على نتيجة مزوّرة ممكن نظرياً، والسقف اليومي بيحدّ من أثره.
- لو الخدمة وقعت: اللعبة بتشتغل عادي، والجوائز بتتأخر وبتتعوّض لما ترجع (الأجهزة بتعيد المحاولة).
