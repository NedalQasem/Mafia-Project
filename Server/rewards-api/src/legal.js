'use strict';
// The public pages the stores and Facebook ask for: the privacy policy (/privacy) and how to have your data deleted (/data-deletion).
// Arabic first, English under it. Plain pages from one template (legal.html), no tracking, no scripts.

const fs = require('node:fs');
const path = require('node:path');

const SUPPORT = 'supportmafia@mena-gaming.com';
const UPDATED_AR = '٣١ تشرين الأول ٢٠٢٦', UPDATED_EN = 'October 31, 2026';

const PRIVACY = `
<h1>سياسة الخصوصية</h1>
<small>لعبة المافيا (MAFIA) · آخر تحديث: ${UPDATED_AR}</small>

<h2>شو بنجمع</h2>
<ul>
<li><b>هوية الجهاز:</b> رقم بتعطيه خدمات Epic Online Services لجهازك حتى تدخل بدون كلمة سر.</li>
<li><b>حسابك باللعبة:</b> رقم حسابك عند Microsoft PlayFab، الاسم اللي بتختاره، مستواك ورتبتك، رصيدك (كوينز وجواهر) والأغراض اللي بتملكها.</li>
<li><b>إذا ربطت Google أو Facebook:</b> رقم حسابك عندهم (والاسم والإيميل إذا سمحت فيهم)، بس حتى نعرف حسابك لما تدخل من جهاز ثاني. ما بننشر شي على حسابك وما بنوصل لأصحابك.</li>
<li><b>اللعب:</b> نتائج المباريات وإحصائياتك (للتصنيفات والجوائز)، رسائل الدردشة الكتابية، والبلاغات.</li>
<li><b>الصوت:</b> المحادثة الصوتية بالغرف بتمرّ مباشرة بين اللاعبين عبر Epic Online Services، و<b>ما بنسجّلها ولا بنخزّنها</b>.</li>
</ul>

<h2>ليش</h2>
<p>حتى تشتغل اللعبة: تدخل لحسابك، تحفظ تقدّمك ورصيدك، تلعب مع غيرك، وتوصلك الجوائز والهدايا والرسائل. والبلاغات والدردشة حتى نحمي اللاعبين من الإساءة والغش.</p>

<h2>مع مين بنشاركها</h2>
<p>ما بنبيع بياناتك لحدا. بتمرّ بس بالخدمات اللي اللعبة مبنية عليها: Epic Games (الدخول والصوت)، Microsoft PlayFab (الحساب والرصيد)، Google وMeta (Facebook) إذا اخترت تدخل فيهم، وسيرفرنا الخاص.</p>

<h2>قديش بنحتفظ فيها</h2>
<p>طول ما حسابك موجود. حساب الضيف اللي ما انربط ممكن ينحذف بعد ٣٠ يوم من آخر دخول (بننبّهك قبلها جوّا اللعبة). الرسائل القديمة والسجلات بتنحذف تلقائياً مع الوقت.</p>

<h2>حقوقك</h2>
<p>بتقدر تطلب نسخة من بياناتك أو حذفها بأي وقت: شوف <a href="__BASE__/data-deletion">طريقة حذف البيانات</a>.</p>

<h2>الأطفال</h2>
<p>اللعبة مش موجّهة للأطفال تحت ١٣ سنة.</p>

<h2>تواصل معنا</h2>
<p><a href="mailto:${SUPPORT}">${SUPPORT}</a></p>

<div class="en">
<h1>Privacy Policy</h1>
<small>MAFIA · Last updated: ${UPDATED_EN}</small>
<h2>What we collect</h2>
<ul>
<li><b>Device identity:</b> an ID that Epic Online Services gives your device so you can sign in without a password.</li>
<li><b>Your game account:</b> your Microsoft PlayFab account ID, the display name you choose, your level and rank, your balance (coins and gems) and the items you own.</li>
<li><b>If you link Google or Facebook:</b> your account ID with them (and your name and email if you allow it), only to find your account when you sign in on another device. We never post on your behalf or access your friends.</li>
<li><b>Play:</b> match results and statistics (for leaderboards and rewards), text chat messages, and reports.</li>
<li><b>Voice:</b> voice chat in rooms goes directly between players through Epic Online Services and is <b>never recorded or stored</b>.</li>
</ul>
<h2>Why</h2>
<p>To run the game: sign you in, keep your progress and balance, let you play with others, and deliver prizes, gifts and messages. Reports and chat are kept to protect players from abuse and cheating.</p>
<h2>Who we share it with</h2>
<p>We never sell your data. It only passes through the services the game is built on: Epic Games (sign-in and voice), Microsoft PlayFab (account and balance), Google and Meta (Facebook) if you choose to sign in with them, and our own server.</p>
<h2>How long we keep it</h2>
<p>As long as your account exists. A guest account that is never linked may be deleted 30 days after its last sign-in (you are warned in the game first). Old messages and logs are removed automatically over time.</p>
<h2>Your rights</h2>
<p>You can ask for a copy of your data or for its deletion at any time: see <a href="__BASE__/data-deletion">how to delete your data</a>.</p>
<h2>Children</h2>
<p>The game is not directed to children under 13.</p>
<h2>Contact</h2>
<p><a href="mailto:${SUPPORT}">${SUPPORT}</a></p>
</div>`;

const DELETION = `
<h1>حذف بياناتك</h1>
<small>لعبة المافيا (MAFIA) · آخر تحديث: ${UPDATED_AR}</small>
<p>بتقدر تطلب حذف حسابك وكل بياناته (الاسم، التقدّم، الرصيد، الأغراض، الرسائل، وربط Google أو Facebook) بأي وقت:</p>
<ul>
<li>ابعت إيميل لـ <a href="mailto:${SUPPORT}?subject=Delete%20my%20data">${SUPPORT}</a> بعنوان «حذف بياناتي».</li>
<li>اكتب فيه <b>رقم هويتك</b> (٦ أرقام، بتلاقيه باللعبة: الإعدادات ← رقم الهوية) أو اسمك باللعبة.</li>
<li>إذا دخلت بـ Facebook وبدك تشيل ربطه بس: من إعدادات Facebook ← التطبيقات ومواقع الويب ← احذف «MAFIA».</li>
</ul>
<p>بنحذف الحساب وبياناته خلال ٣٠ يوم من الطلب، وبنرد عليك لما يخلص. الحذف نهائي وما بيرجع (الرصيد والأغراض بتروح).</p>

<div class="en">
<h1>Delete your data</h1>
<small>MAFIA · Last updated: ${UPDATED_EN}</small>
<p>You can ask us to delete your account and all of its data (name, progress, balance, items, messages, and the Google or Facebook link) at any time:</p>
<ul>
<li>Email <a href="mailto:${SUPPORT}?subject=Delete%20my%20data">${SUPPORT}</a> with the subject "Delete my data".</li>
<li>Include your <b>player ID</b> (6 digits, in the game under Settings) or your in-game name.</li>
<li>To only remove the Facebook link: Facebook Settings → Apps and Websites → remove "MAFIA".</li>
</ul>
<p>We delete the account and its data within 30 days of the request and reply when it is done. Deletion is final (balance and items are lost).</p>
</div>`;

/**
 * The pages live under the game's own folder (/mafia/privacy, /mafia/data-deletion), as the company may have other games on this host.
 * The first addresses without the folder answer with a permanent redirect to the new ones.
 */
function createLegal({ base = '/mafia', htmlFile = null } = {}) {
  let tpl = '';
  try { tpl = fs.readFileSync(htmlFile || path.join(__dirname, 'legal.html'), 'utf8'); } catch { tpl = '<!doctype html><meta charset="utf-8"><title>MAFIA</title>__BODY__'; }
  const page = (title, body) => tpl.replace('__TITLE__', title).replace('__BODY__', body).split('__BASE__').join(base);
  const pages = { [base + '/privacy']: page('Privacy', PRIVACY), [base + '/data-deletion']: page('Data deletion', DELETION) };
  const moved = { '/privacy': base + '/privacy', '/data-deletion': base + '/data-deletion' };
  return { page: p => pages[p] || null, movedTo: p => moved[p] || null };
}

module.exports = { createLegal, SUPPORT };
