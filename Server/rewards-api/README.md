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

## التركيب على الـ VPS (Ubuntu)

> **قاعدة:** موقع `mena-gaming.com` الحالي ما نلمسه. نضيف سبدومين جديد `api.mena-gaming.com` بإعداد منفصل. وما بتكتب المفتاح السري بأي شات أو ملف بالمشروع.

### 1) السبدومين
بلوحة الدومين (DNS) أضف سجل **A**: الاسم `api` يشير لعنوان IP تبع الـ VPS. (انتظر دقائق للانتشار؛ تتأكد بـ `ping api.mena-gaming.com`).

### 2) تجهيز السيرفر
```bash
ssh <user>@<ip-الـVPS>
sudo apt update && sudo apt -y upgrade
# Node.js 22 (إذا ما كان مركّب أو أقدم من 20):
node --version
curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash -
sudo apt -y install nodejs
```

### 3) رفع الخدمة (من جهازك)
```bash
scp -r Server/rewards-api <user>@<ip>:/tmp/rewards-api
```
على السيرفر:
```bash
sudo useradd --system --home /opt/mafia-rewards --shell /usr/sbin/nologin mafia
sudo mkdir -p /opt/mafia-rewards /var/lib/mafia-rewards
sudo cp -r /tmp/rewards-api/* /opt/mafia-rewards/
sudo cp /opt/mafia-rewards/config.example.json /opt/mafia-rewards/config.json
sudo chown -R mafia:mafia /opt/mafia-rewards /var/lib/mafia-rewards
cd /opt/mafia-rewards && sudo -u mafia node --test      # لازم تنجح 15 اختبار
```

### 4) المفتاح السري (بإيدك على السيرفر فقط)
1. بلوحة PlayFab: **Settings ← Secret keys ← New Secret Key** (سمّيه مثلاً `rewards-vps`). **احذف المفتاح المؤقت القديم** إذا لسا موجود.
2. على السيرفر:
```bash
sudo nano /etc/mafia-rewards.env
# اكتب سطر واحد فقط (بدون مسافات ولا علامات اقتباس):
# PLAYFAB_SECRET_KEY=المفتاح_هنا
sudo chown root:root /etc/mafia-rewards.env
sudo chmod 600 /etc/mafia-rewards.env
```

### 5) تشغيل دائم (systemd)
```bash
sudo tee /etc/systemd/system/mafia-rewards.service >/dev/null <<'EOF'
[Unit]
Description=Mafia rewards API
After=network.target

[Service]
User=mafia
WorkingDirectory=/opt/mafia-rewards
EnvironmentFile=/etc/mafia-rewards.env
ExecStart=/usr/bin/node src/index.js
Restart=always
RestartSec=3
NoNewPrivileges=true
ProtectSystem=strict
ReadWritePaths=/var/lib/mafia-rewards
PrivateTmp=true

[Install]
WantedBy=multi-user.target
EOF
sudo systemctl daemon-reload
sudo systemctl enable --now mafia-rewards
curl http://127.0.0.1:8787/health      # {"ok":true}
sudo journalctl -u mafia-rewards -n 20 --no-pager
```

### 6) HTTPS على السبدومين
أولاً شوف شو بيشغّل موقعك الحالي (لا تعدّل عليه):
```bash
sudo ss -ltnp | grep -E ':80|:443'
```
- **إذا nginx:** أضف ملف جديد فقط:
```bash
sudo tee /etc/nginx/sites-available/api.mena-gaming.com >/dev/null <<'EOF'
server {
    listen 80;
    server_name api.mena-gaming.com;
    client_max_body_size 16k;
    location / {
        proxy_pass http://127.0.0.1:8787;
        proxy_set_header X-Forwarded-For $remote_addr;
        proxy_set_header Host $host;
    }
}
EOF
sudo ln -s /etc/nginx/sites-available/api.mena-gaming.com /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
sudo apt -y install certbot python3-certbot-nginx
sudo certbot --nginx -d api.mena-gaming.com
```
- **إذا Apache، أو لوحة ثانية (CloudPanel/Plesk/..):** ابعتلي اسمها وبعطيك الإعداد المناسب، لا تجرّب لحاله.

اختبار نهائي من أي جهاز: `https://api.mena-gaming.com/health` لازم يرد `{"ok":true}`.

### 7) جدار الحماية (إذا ufw مفعّل)
```bash
sudo ufw status
sudo ufw allow OpenSSH && sudo ufw allow 80 && sudo ufw allow 443   # قبل ما تفعّله، حتى ما تنقفل
```
المنفذ 8787 ما بينفتح للخارج (الخدمة سامعة على 127.0.0.1 بس).

## بعد ما تشتغل
- بلوحة PlayFab: عطّل **Allow client to add virtual currency** (Settings ← API Features).
- مراقبة: `sudo journalctl -u mafia-rewards -f`
- نسخة احتياطية: الملف `/var/lib/mafia-rewards/state.json` (مباريات الـ48 ساعة الأخيرة فقط، فخسارته مش خطيرة).
- تحديثات: `sudo apt update && sudo apt upgrade` شهرياً، وأعد تشغيل الخدمة بعد تحديث الكود: `sudo systemctl restart mafia-rewards`.

## ملاحظات أمان
- المفتاح السري بس بـ `/etc/mafia-rewards.env` (صلاحية 600). ما ينحط بـ Git ولا بالشات ولا باللعبة.
- لو تسرّب المفتاح: احذفه من PlayFab فوراً وأنشئ غيره.
- اللاعب بيقدر يكذب بدوره أو بنجاته (بيوصله بونص صغير)، لكنه ما بيقدر يألّف مباراة لحاله ولا يتخطى السقف اليومي. الاتفاق مع أغلبية اللاعبين على نتيجة مزوّرة ممكن نظرياً، والسقف اليومي بيحدّ من أثره.
- لو الخدمة وقعت: اللعبة بتشتغل عادي، والجوائز بتتأخر وبتتعوّض لما ترجع (الأجهزة بتعيد المحاولة).
