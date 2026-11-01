// builds Docs/mockups/admin-images.html from the real admin board's CSS (so it looks exactly like it) plus static markup of the offer drawer with the new picture field
const fs = require('fs');
const root = 'F:/Game_Projects/Mafia Voice Chat project/';
const src = fs.readFileSync(root + 'Server/rewards-api/src/admin-ui.html', 'utf8');
const css = src.slice(src.indexOf('<style>') + 7, src.indexOf('</style>'));
const link = src.match(/<link rel="stylesheet" href="https:\/\/fonts[^>]*>/)[0];

const sky = "url('data:image/svg+xml;utf8," + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1300 800" preserveAspectRatio="xMidYMid slice"><defs><linearGradient id="a" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#0d0608"/><stop offset=".6" stop-color="#4a0f14"/><stop offset="1" stop-color="#8a2a18"/></linearGradient><radialGradient id="g" cx=".5" cy=".5" r=".5"><stop offset="0" stop-color="#ffd08a"/><stop offset=".35" stop-color="#ff7a2a" stop-opacity=".9"/><stop offset="1" stop-color="#ff7a2a" stop-opacity="0"/></radialGradient></defs><rect width="1300" height="800" fill="url(#a)"/><circle cx="650" cy="360" r="330" fill="url(#g)"/><circle cx="650" cy="360" r="150" fill="none" stroke="#ffe3b0" stroke-width="34"/><circle cx="650" cy="360" r="150" fill="none" stroke="#ff5a1a" stroke-width="14" opacity=".8"/><path fill="#08040a" d="M0 800V560l90-40 70 30 60-90 90 60 60-30 110 90 70-60 80 40 90-70 60 50 110-80 90 90 70-50 100 60 90-40 60 60 90-30 80 80V800z"/></svg>') + "')";

const extra = `
body{height:auto;padding:18px 14px 50px;background:#070504}
h1.page{font-family:Lalezar;font-weight:400;text-align:center;margin:0 0 4px;font-size:1.9rem}
p.sub{text-align:center;color:#8f8777;margin:0 auto 18px;max-width:760px;line-height:1.8;font-size:.85rem}
.state{margin:0 auto 34px;max-width:1040px}
.state>.cap{color:#8d744a;font-weight:800;font-size:.8rem;margin:0 0 8px}
.state .drawer{position:static;display:block;background:none;padding:0}
.state .edit{max-height:none;margin:0 auto}
/* the new picture field */
.pic{display:grid;gap:8px;margin:2px 0 12px}
.pic .head{display:flex;align-items:baseline;gap:8px}
.pic .head b{font-weight:800}
.pic .head small{color:var(--soft);font-weight:700;font-size:.72rem}
.zone{position:relative;display:grid;place-items:center;gap:4px;text-align:center;padding:18px 12px;border-radius:6px;border:2.5px dashed #6e5224;background:#1c170f0d;color:var(--soft);cursor:pointer}
.zone svg{width:30px;height:30px;stroke:#8a1c24;fill:none;stroke-width:2;stroke-linecap:round;stroke-linejoin:round}
.zone b{color:var(--ink);font-size:.9rem}
.zone span{font-size:.72rem;font-weight:700}
.zone.over{border-color:#8a1c24;background:#8a1c2412}
.shot{display:grid;grid-template-columns:200px 1fr;gap:12px;align-items:start}
.frame{position:relative;aspect-ratio:13/8;border-radius:4px;background:#222 center/cover no-repeat;box-shadow:0 0 0 2px #1c170f,0 6px 12px #0006;overflow:hidden}
.frame .safe{position:absolute;inset:15% 6%;border:1.5px dashed #ffffffcc;border-radius:2px;box-shadow:0 0 0 999px #00000045}
.frame.ev{aspect-ratio:2/1}
.shot .meta{display:grid;gap:5px;align-content:start}
.shot .meta .ok{display:flex;align-items:center;gap:6px;font-weight:800;font-size:.78rem;color:#1d6a4a}
.shot .meta .ok svg{width:16px;height:16px;stroke:#1d6a4a;fill:none;stroke-width:3;stroke-linecap:round;stroke-linejoin:round}
.shot .meta .bad{color:#8a1c24}
.shot .meta .bad svg{stroke:#8a1c24}
.shot .meta .m{font-size:.72rem;font-weight:700;color:var(--soft)}
.shot .acts{display:flex;gap:8px;margin-top:4px;flex-wrap:wrap}
.tog{display:flex;gap:6px}
.note{font-size:.7rem;font-weight:700;color:var(--soft);line-height:1.6;margin:0}
.note b{color:var(--crim)}
/* the preview with the picture */
.clip .photo{height:96px;margin:-2px -4px 8px;background:#222 center/cover no-repeat;box-shadow:0 0 0 2px #efe6cf,0 0 0 3px #1c170f55,0 4px 8px #0007;rotate:-.6deg}
.clip .photo.ev{height:78px}
.clip.dim .ghost{opacity:.5}
/* sizes table */
.sizes{max-width:1040px;margin:0 auto}
.sizes table{width:100%;border-collapse:collapse;font-size:.82rem}
.sizes th,.sizes td{padding:8px 10px;border-bottom:1px solid #1c170f33;text-align:start;vertical-align:top}
.sizes th{font-family:Lalezar;font-weight:400;font-size:.95rem;border-bottom:3px double var(--ink)}
.sizes code{direction:ltr;unicode-bidi:embed;white-space:nowrap;background:#1c170f14;padding:1px 6px;border-radius:3px;font-weight:800}
.err2{margin-top:8px;padding:8px 10px;border-radius:5px;background:#8a1c2414;box-shadow:inset 0 0 0 1.5px #8a1c24;color:#8a1c24;font-weight:800;font-size:.78rem}
`;

const uploadSvg = '<svg viewBox="0 0 24 24"><path d="M12 16V5M7 10l5-5 5 5M5 19h14"/></svg>';
const okSvg = '<svg viewBox="0 0 24 24"><path d="M5 12l5 5 9-10"/></svg>';
const badSvg = '<svg viewBox="0 0 24 24"><path d="M6 6l12 12M18 6L6 18"/></svg>';

const fields = `
        <div class="kinds"><button type="button" class="on">إعلان من المتجر (adId)</button><button type="button">عنصر من المتجر (itemId)</button></div>
        <div class="f"><label>معرّف الإعلان</label><input value="ad_ember" readonly><small>لازم يكون موجود باللعبة وبمتجر PlayFab.</small></div>
        <div class="row2"><div class="f"><label>العنوان الصغير (اختياري)</label><input value="عرض الليلة فقط" readonly></div><div class="f"><label>الختم (اختياري)</label><input value="−٣٠٪" readonly></div></div>
        <div class="f"><label>العنوان (اختياري، سطر جديد = سطر ثاني)</label><textarea readonly>إطار الجمر
بنصف الثمن تقريباً</textarea></div>`;
const tail = `
        <div class="row2"><div class="f"><label>يبدأ</label><input type="datetime-local" value="2026-11-01T18:00" readonly></div><div class="f"><label>ينتهي</label><input type="datetime-local" value="2026-11-02T18:00" readonly></div></div>
        <div class="foot-act"><button class="btn red" type="button">احفظ وطبّق</button><button class="btn ghost" type="button">إلغاء</button></div>`;

const preview = (withPhoto, bg) => `
    <div class="prev"><h4>هيك رح يظهر باللعبة</h4>
      <div class="clip"><div class="pg"></div><span class="tape"></span><span class="stamp st">−٣٠٪</span>
        <div class="kick"><span>عرض الليلة فقط</span><span>العرض ١</span></div>
        ${withPhoto ? `<div class="photo" style="background-image:${bg}"></div>` : ''}
        <h5>إطار الجمر<br>بنصف الثمن تقريباً</h5><p>حلقة نارية تتوهج حول صورتك في كل غرفة.</p>
        <div class="cd">ينتهي خلال <b>٠٢:١٤:١٩</b></div></div>
      <small>${withPhoto ? 'الصورة بتطلع بمكان رسمة العنصر' : 'بدون صورة: بتطلع رسمة العنصر متل هلّق'}</small></div>`;

const drawer = (title, pic, prev) => `
<div class="drawer on"><div class="paper edit"><div class="form"><h2><span>${title}</span><button class="x" type="button">×</button></h2>
  <form>${fields}${pic}${tail}</form></div>${prev}</div></div>`;

const picEmpty = `
        <div class="pic"><div class="head"><b>صورة العرض</b><small>اختيارية · بدون صورة بتظهر رسمة العنصر</small></div>
          <div class="zone">${uploadSvg}<b>اسحب الصورة لهون أو اضغط لتختار</b><span>JPG أو PNG · المقاس المثالي <code>١٣٠٠ × ٨٠٠</code> · أقصى حجم ١ ميغا</span></div>
          <p class="note">الصورة بتتقصّ تلقائياً على قد المكان. خلّي الشي المهم (وجه، اسم، سعر) بالنص، لأنه الأطراف ممكن تنقصّ شوي على بعض الشاشات.</p></div>`;
const picDone = `
        <div class="pic"><div class="head"><b>صورة العرض</b><small>اختيارية</small></div>
          <div class="shot"><div class="frame" style="background-image:${sky}"><i class="safe"></i></div>
            <div class="meta"><span class="ok">${okSvg}المقاس مناسب · ١٣٠٠ × ٨٠٠</span><span class="m">ember-offer.jpg · ٣٤٢ كيلو</span>
              <span class="m">المربع المنقّط = الجزء اللي بيطلع دايماً على كل الشاشات</span>
              <div class="acts"><button class="btn ghost sm" type="button">غيّر الصورة</button><button class="btn ghost sm del" type="button">احذف الصورة</button></div></div></div></div>`;
const picBad = `
        <div class="pic"><div class="head"><b>صورة العرض</b><small>اختيارية</small></div>
          <div class="zone">${uploadSvg}<b>اسحب الصورة لهون أو اضغط لتختار</b><span>JPG أو PNG · المقاس المثالي <code>١٣٠٠ × ٨٠٠</code> · أقصى حجم ١ ميغا</span></div>
          <div class="err2">الصورة صغيرة كتير (٣٢٠ × ٢٠٠). لازم تكون ٨٠٠ عرض على الأقل لتطلع حادّة على الموبايلات الكبيرة.</div></div>`;

const evFields = `
        <div class="kinds"><button type="button" class="on">إعلان (info)</button><button type="button">مكافأة مضاعفة (bonus)</button><button type="button">تحدّي (challenge)</button></div>
        <div class="f"><label>العنوان</label><input value="ليلة العصابة الكبرى" readonly></div>
        <div class="f"><label>الوصف</label><textarea readonly>بطولة ليلية بجوائز نادرة. شارك وكوّن اسمك بالحارة.</textarea></div>`;
const evPic = `
        <div class="pic"><div class="head"><b>صورة الحدث</b><small>اختيارية · بتطلع فوق بطاقة الحدث</small></div>
          <div class="shot"><div class="frame ev" style="background-image:${sky}"><i class="safe" style="inset:12% 6%"></i></div>
            <div class="meta"><span class="ok">${okSvg}المقاس مناسب · ١٢٠٠ × ٦٠٠</span><span class="m">night-event.jpg · ٢٨٠ كيلو</span>
              <div class="acts"><button class="btn ghost sm" type="button">غيّر الصورة</button><button class="btn ghost sm del" type="button">احذف الصورة</button></div></div></div></div>`;
const evDrawer = `
<div class="drawer on"><div class="paper edit"><div class="form"><h2><span>تعديل حدث</span><button class="x" type="button">×</button></h2>
  <form>${evFields}${evPic}<div class="foot-act"><button class="btn red" type="button">احفظ وطبّق</button><button class="btn ghost" type="button">إلغاء</button></div></form></div>
  <div class="prev"><h4>هيك رح يظهر باللعبة</h4>
    <div class="clip"><div class="pg"></div><span class="tape"></span>
      <div class="photo ev" style="background-image:${sky};margin:-2px -4px 8px"></div>
      <div class="kick"><span>بطولة</span><span>الحدث ٣</span></div><h5>ليلة العصابة الكبرى</h5><p>بطولة ليلية بجوائز نادرة. شارك وكوّن اسمك بالحارة.</p></div>
    <small>الصورة بتطلع فوق البطاقة بعرضها كامل</small></div></div></div>`;

const sizes = `
<div class="paper card sizes"><h3>مقاسات الصور (بتنكتب جنب كل خانة صورة)</h3>
<table><tr><th>وين</th><th>المقاس المثالي</th><th>النسبة</th><th>الحد الأدنى</th><th>ملاحظات</th></tr>
<tr><td><b>صورة العرض</b><br><span class="m">لافتة «جريدة الليل» عند فتح اللعبة</span></td><td><code>١٣٠٠ × ٨٠٠</code></td><td>١٣ لكل ٨</td><td><code>٨٠٠ × ٤٩٠</code></td><td>ارتفاع المكان بيتغيّر مع طول الشاشة (٢٣٠–١٥٠)، فبتتقصّ من فوق وتحت شوي. الشي المهم بالنص.</td></tr>
<tr><td><b>صورة الحدث</b><br><span class="m">بطاقة بـ«أخبار الحارة»</span></td><td><code>١٢٠٠ × ٦٠٠</code></td><td>٢ لكل ١</td><td><code>٧٢٠ × ٣٦٠</code></td><td>بعرض البطاقة كامل. لو فيه كتابة بالصورة خلّيها بعيدة عن الأطراف.</td></tr>
<tr><td><b>صورة الرسالة الخاصة</b><br><span class="m">موجودة هلّق</span></td><td><code>٩٠٠ عرض</code></td><td>حرّة</td><td>—</td><td>بتتصغّر لحالها قبل الرفع.</td></tr></table>
<p class="note" style="margin-top:10px"><b>للكل:</b> JPG أو PNG، أقصى حجم ١ ميغا. اللوحة بتصغّر الصورة لحالها إذا كانت أكبر من المقاس المثالي وبتضغطها. لا تحط فيها أسعار أو تواريخ (بتتغيّر بالنص، والصورة ما بتتغيّر). بعد الحفظ بتطلع عند اللاعبين فوراً بدون تحديث اللعبة.</p></div>`;

const html = `<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>صور العروض والأحداث من اللوحة</title>${link}<style>${css}${extra}</style></head><body>
<h1 class="page">صور العروض والأحداث من لوحة الإدارة</h1>
<p class="sub">بتضيف صورة لأي عرض أو حدث من اللوحة، وبتظهر عند اللاعبين فوراً بدون نسخة جديدة من اللعبة. الصورة اختيارية؛ بدونها كل شي بيضل متل هلّق. شكل اللوحة هو نفس الأدراج اللي عندك، ومعاينة «هيك رح يظهر باللعبة» بتبيّن الصورة قبل ما تحفظ.</p>
<section class="state" id="s1"><p class="cap">١ · العرض: بدون صورة بعد</p>${drawer('تعديل عرض', picEmpty, preview(false))}</section>
<section class="state" id="s2"><p class="cap">٢ · العرض: بعد رفع الصورة (المربع المنقّط = الجزء اللي بيظهر دايماً)</p>${drawer('تعديل عرض', picDone, preview(true, sky))}</section>
<section class="state" id="s3"><p class="cap">٣ · رسالة خطأ لما الصورة ما بتنفع</p>${drawer('تعديل عرض', picBad, preview(false))}</section>
<section class="state" id="s4"><p class="cap">٤ · الحدث: صورة بعرض البطاقة</p>${evDrawer}</section>
<section class="state" id="s5"><p class="cap">٥ · جدول المقاسات (بيطلع جنب الخانات وبالتوثيق)</p>${sizes}</section>
</body></html>`;
fs.writeFileSync(root + 'Docs/mockups/admin-images.html', html);
console.log('ok', html.length);
