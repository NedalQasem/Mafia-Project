# LiveKit للغرف الصوتية الكبيرة (تجربة)

صوت فقط، بدون تسجيل. مخصص لغرف السهرة وغرفة المكتب (متكلمون قلة وسامعون كتير). غرف المافيا بتضل على EOS (حد ١٦).

## التركيب على الـVPS
1. انسخ المجلد `Server/livekit` لـ`/root/livekit` على السيرفر.
2. `bash /root/livekit/set-livekit.sh` (بيعمل المفاتيح، بيفتح المنفذين 7881/tcp و7882/udp، وبيشغّل LiveKit وبيعيد تشغيل خدمة الجوائز).
3. بلوحة Hostinger (جدار الحماية)، افتح نفس المنفذين إذا كان عندك جدار حماية من عندهم: **7881 TCP** و**7882 UDP**.
4. بـnginx الموقع، جوّا بلوك `server` تبع `api.mena-gaming.com` (443) وقبل `location / {` ضيف:
```nginx
    location /lk/ {
        set $livekit http://mafia-livekit:7880;
        rewrite ^/lk/(.*)$ /$1 break;
        proxy_pass $livekit;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_read_timeout 3600s;
    }
```
   ثم `docker exec mena-gaming-nginx-1 nginx -t && docker exec mena-gaming-nginx-1 nginx -s reload` (نفس طريقة النسخة الاحتياطية بـREADME الخدمة).
5. فحص: `https://api.mena-gaming.com/lk/` لازم يرد `OK` (أو صفحة صغيرة) مش خطأ nginx.

## اختبار الحمل (بنقيس المعالج والنطاق قبل ما نعتمد عليه)
من أي جهاز (حتى جهازك) بـDocker، والمفتاح والسر من `/etc/mafia-livekit.env`:
```bash
docker run --rm livekit/livekit-cli load-test --url wss://api.mena-gaming.com/lk --api-key <KEY> --api-secret <SECRET> --room loadtest --audio-publishers 8 --subscribers 64 --duration 2m
```
(إذا تغيّرت أسماء الخيارات: `docker run --rm livekit/livekit-cli load-test --help`). وبنفس الوقت على السيرفر: `docker stats mafia-livekit` ومراقبة `top`: لو المعالج العام ظل تحت ~٦٠٪ والنطاق (من لوحة Hostinger) معقول، تمام.

## مراقبة
`docker logs -f mafia-livekit` والنطاق الشهري من لوحة Hostinger (الخطة ٤ تيرا).
