# LiveKit للغرف الصوتية الكبيرة (تجربة)

صوت فقط، بدون تسجيل. مخصص لغرف السهرة وغرفة المكتب (متكلمون قلة وسامعون كتير). غرف المافيا بتضل على EOS (حد ١٦).

## التركيب على الـVPS
1. انسخ المجلد `Server/livekit` لـ`/root/livekit` على السيرفر.
2. `bash /root/livekit/set-livekit.sh` (بيعمل المفاتيح، بيفتح المنفذين 7881/tcp و7882/udp، وبيشغّل LiveKit وبيعيد تشغيل خدمة الجوائز).
3. بلوحة Hostinger (جدار الحماية)، افتح نفس المنفذين إذا كان عندك جدار حماية من عندهم: **7881 TCP** و**7882 UDP**.
4. **nginx الموقع:** `bash /root/livekit/add-nginx-location.sh` (بيضيف `location /lk/` جوّا بلوك 443 تبع api.mena-gaming.com، بنسخة احتياطية وتراجع تلقائي لو فشل فحص nginx).
5. فحص: `https://api.mena-gaming.com/lk/` لازم يرد `OK` (أو صفحة صغيرة) مش خطأ nginx.

## اختبار الحمل (بنقيس المعالج والنطاق قبل ما نعتمد عليه)
من أي جهاز (حتى جهازك) بـDocker، والمفتاح والسر من `/etc/mafia-livekit.env`:
```bash
docker run --rm livekit/livekit-cli load-test --url wss://api.mena-gaming.com/lk --api-key <KEY> --api-secret <SECRET> --room loadtest --audio-publishers 8 --subscribers 64 --duration 2m
```
(إذا تغيّرت أسماء الخيارات: `docker run --rm livekit/livekit-cli load-test --help`). وبنفس الوقت على السيرفر: `docker stats mafia-livekit` ومراقبة `top`: لو المعالج العام ظل تحت ~٦٠٪ والنطاق (من لوحة Hostinger) معقول، تمام.

## مراقبة
`docker logs -f mafia-livekit` والنطاق الشهري من لوحة Hostinger (الخطة ٤ تيرا).
