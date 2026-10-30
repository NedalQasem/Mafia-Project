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

- **الكلمات الممنوعة:** فيه قائمة جاهزة بالكود (`src/bannedwords.js`) فيها الشتائم المشهورة بالعربي والفرانكو والإنجليزي، والكلمة بتنقارن بعد توحيد التشكيل والألف والياء والتاء المربوطة وبتتعوّض بنجوم. الكلمات القصيرة (مثل «كس») بتنمنع كلمة كاملة بس عشان ما نضرب كلمات بريئة (مثل «كسر»)، والطويلة بتنمنع حتى لو جزء من كلمة. لإضافة كلمات من عندك حطها بـ`config.json` بقسم `chat`: `bannedWords` (بأي مكان بالكلمة) و`bannedExact` (كلمة كاملة)، ثم `docker restart mafia-rewards`. لإطفاء القائمة الجاهزة: `"useDefaultBannedWords": false`.
- **تجربة محلية بدون PlayFab:** `node dev-server.js` بيشغّل نسخة تجريبية على `http://127.0.0.1:8788` ببلاغات ولاعبين وهميين (للمحرر فقط، بالمفتاح `PlayerPrefs MafiaApiUrl`).

## العائلة (الأصدقاء، نفس الخدمة)

كل لاعب بياخد رقم هوية من ٦ خانات، وبيضيف الناس فيه. الخدمة بتحفظ الأصدقاء والطلبات بملف `friends.json` (نفس الـvolume)، وبتعرف مين متصل وبأي غرفة من نبضة بترسلها اللعبة كل ٣٠ ثانية. الطلبات والدعوات بتوصل للطرف الثاني مع طلب الانتظار الخاص بالدردشة. البلاغات عن اللاعبين بتنكتب بنفس `chat-reports.jsonl` بنوع `player`.

- **الإعدادات (اختيارية، قسم `friends` بـ`config.json`):** `maxFriends` (12)، `inviteGapMs` (20000، الفاصل بين دعوتين لنفس الشخص)، `requestsPerDay` (30)، `presenceMs` (90000).

## التصنيفات (نفس الخدمة)

الخدمة نفسها بتكتب أرقام التصنيفات وقت ما بتدفع مكافآت المباراة (مرة وحدة لكل لاعب ومباراة)، فاللاعب ما بيقدر يبعت رقم من عنده. بتحفظها بملف `leaderboard.json`: لكل يوم ولكل أسبوع ثمان فئات (النقاط، الفوز، فوز كمافيا، فوز كمحقق، دقائق اللعب، المباريات، النجاة، سلسلة الفوز) بالإضافة لأرقام كل الأوقات لقاعة المشاهير. اليوم بينتهي منتصف الليل بتوقيت عمّان والأسبوع بيبدأ السبت.

- **الإعدادات (اختيارية، قسم `leaderboard` بـ`config.json`):** `tzHours` (3)، `weekStartDay` (6 = السبت)، `topSize` (12)، `keepDays` (9)، `keepWeeks` (6).
- **ما بيتحسب:** المباريات اللي انلعبت قبل ما تنزل هالنسخة. الجوائز اليومية/الأسبوعية والمواسم لسا ما انبنت.

## عروض لافتة بداية اللعبة (نفس الخدمة)

اللعبة بتفتح لافتة "جريدة الليل" بأول الشاشة الرئيسية فيها لحد ٣ عروض. اللعبة بتسأل السيرفر شو تعرض، فبتقدر تغيّر العروض والنصوص والعدّاد بدون نسخة جديدة من اللعبة: عدّل قسم `offers` بـ`config.json` (الملف الحقيقي على الـ VPS مش `config.example.json`) ثم `docker restart mafia-rewards`. إذا القسم فاضي أو السيرفر ما ردّ، اللعبة بتعرض إعلانات متجرها الخاصة.

```json
"offers": [
  { "adId": "ad_ember", "until": "2026-11-05T21:00:00Z", "stamp": "−٣٠٪" },
  { "itemId": "chat_port", "mast": "وصل حديثاً", "headline": "خلفية الميناء
سفن وليل أسود", "body": "غيّر لون مجلسك.", "stamp": "جديد" }
]
```

- **`adId`** إعلان من كتالوج المتجر داخل اللعبة (مثل `ad_ember`: بياخد منه السعر المخفّض والعنصر)، أو **`itemId`** عنصر من المتجر. لازم واحد منهم، وإلا العرض بيتجاهل. العناصر والأسعار المخفّضة نفسها لازم تكون موجودة باللعبة وبمتجر PlayFab، السيرفر بس بيختار شو يُروَّج.
- **`mast`، `headline`، `body`، `stamp`**: نصوص اختيارية بتحل مكان نصوص اللعبة (`
` بالعنوان = سطر ثاني).
- **`from` و`until`**: تاريخ بداية ونهاية (ISO). بعد `until` العرض بيختفي لحاله، والعدّاد بالصفحة بيعدّ للنهاية.
- **`enabled`: false** بيطفي العرض بدون حذفه. أقصى ٣ عروض.

## لوحة الإدارة MAFIA ADMIN BOARD (نفس الخدمة)

صفحة ويب بتتحكم منها بالأحداث والعروض والكلمات الممنوعة والبلاغات واللاعبين، بدل ما تعدّل `config.json` وتعمل restart. بتنخدم من نفس الخدمة على مسار `/admin` (يعني `https://api.mena-gaming.com/admin`) بدون دومين جديد ولا تعديل على nginx ولا على موقعك. التصميم: `Docs/mockups/admin-board.html`.

- **كلمة السر:** بتحطها مرة وحدة بأمر واحد على الـ VPS (بيسألك مرتين وهي مخفية، وبيخزّن **الهاش بس** بـ `/etc/mafia-rewards.env`، مش الكلمة): `bash /root/rewards-api/set-admin.sh`. بدون هالسطر اللوحة **مطفية بالكامل** (المسار بيرجع 404). لتغييرها شغّل الأمر من جديد.
- **الحماية:** ٥ محاولات خاطئة بتقفل نفس العنوان ١٥ دقيقة، الجلسة كوكي `HttpOnly` + `SameSite=Strict` لمدة ١٢ ساعة، وكل تغيير لازم هيدر `X-Admin: 1`. الصفحة بتظهر بـ `noindex` وما بتنفتح داخل إطار.
- **التخزين:** ملف `admin.json` بنفس الـvolume (`/data`): قوائم الأحداث والعروض، الكلمات الإضافية، البلاغات المعالَجة، سجلّ النشاط (آخر ٢٠٠ إجراء)، وعدّاد المباريات اليومي. التعديلات بتنطبق فوراً بدون restart.
- **الأحداث والعروض:** قبل أول تعديل من اللوحة بتضل جاية من `config.json`. أول حفظ بينقلها كلها للوحة، وبعدها اللوحة هي المرجع (`events` و`offers` بالـconfig بيتجاهلوا).
- **الكلمات:** بتنضاف فوق القائمة الجاهزة بالكود و`config.json`، وبتنطبق على الدردشة فوراً. نوعان: كلمة كاملة وداخل الكلمة.
- **البلاغات:** من `chat-reports.jsonl` (آخر ٨٠). الكتم من اللوحة هو نفس الكتم التلقائي (بيمنع الدردشة). البحث عن لاعب بالاسم بيلاقي بس اللي ظهروا من آخر تشغيل للسيرفر؛ برقمه (٦ خانات) أو معرّف PlayFab دايماً بيلاقيه.
- **تغيير المسار:** `ADMIN_PATH=/اسم-سري` بنفس ملف البيئة (أو `admin.path` بـ `config.json`).
- **تجربة محلية:** `node dev-server.js` بيفتح `http://127.0.0.1:8788/admin` بكلمة سر `dev-password-123` وبيانات تجريبية.

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
