# 07 — الاتجاه الفني: سهرة الحارة

> آخر تحديث: 2026-10-17
> النموذج التفاعلي: https://claude.ai/artifact/TzG2BiUN4TV6gUkXDCTGTb (خاص بحسابك)

---

## الفكرة بجملة

**المافيا مش كازينو، المافيا سهرة بالحارة.** فوانيس معلّقة، مشربيات خشب، حجر قديم، وديوانية فيها الكل بيحكي.

**المواد:** جلد مدبوغ بخياطة ذهبية وإطارات ذهب مصقول، مثل دفتر مختار قديم. هذا الطابع الفخم من التصميم الأصلي، بس حطيناه بجو الحارة.

---

## 1. الألوان

| الاسم | HEX | الاستخدام |
|---|---|---|
| ليل الحارة | `#120F1F` | الخلفية الأساسية |
| جدران بالعتمة | `#1C1730` | البطاقات والمساحات |
| جلد مدبوغ | `#3A2216` / `#5B3521` | البطاقات والأشرطة (مع ملمس حبيبي وخياطة ذهبية) |
| ذهب مصقول | `#FFF0B3` → `#E0AD4A` → `#8A5A1C` | الإطارات، الأزرار، حلقات الصور (تدرّج معدني) |
| خشب المشربية | `#3B2418` / `#57351F` | الأقواس والزخارف |
| ضو الفانوس | `#F4A83A` | الأزرار المهمة، العملات، العناوين المضيئة |
| زليج فيروزي | `#2F9A92` | **مين عم يحكي**، جاهز، الأمان |
| طين أحمر | `#C2563B` | المافيا، الكتم، التصويت، الخطر |
| حجر النهار | `#F3E6C9` | خلفية مرحلة النهار |
| حبر النهار | `#3A2617` | النص بالنهار |
| نص | `#EFE0C2` | النص الأساسي بالليل |

**قاعدة:** كل لون إله معنى. الفيروزي ما بينستخدم للزينة، بس للصوت والجاهزية.

---

## 2. الخطوط (عربي + Unity)

| الدور | الخط | ملاحظة |
|---|---|---|
| العناوين + صوت الراوي | **Reem Kufi** (Bold) | كوفي تراثي، مجاني من Google Fonts |
| النصوص والأزرار | **Tajawal** (Regular/Bold/ExtraBold) | واضح جداً على الموبايل |

**لـ Unity:** منحتاج نعمل TMP Font Asset لكل خط، ونضيف أداة بتوصّل الحروف العربية (TextMeshPro لحاله ما بيعرف يوصّل الحروف).

---

## 3. قواعد الشكل
1. **الديوانية هي القلب:** مقاعد بصور اللاعبين، وحلقة فيروزية حوالين اللي عم يحكي.
2. **الليل والنهار:** الليل عتمة وفوانيس، والنهار حجر فاتح. اللاعب بيعرف المرحلة من أول نظرة.
3. **الراوي:** كل حدث بجملة قصصية بخط Reem Kufi.
4. **جلد وذهب:** كل بطاقة جلد بإطار ذهبي وخياطة. الذهب للحواف والأزرار بس، مش للمساحات الكبيرة.
5. **قوس المشربية** للإطارات المهمة بس (شاشة التحميل، النتيجة، بطاقة الدور).
6. **زر ذهبي واحد** بكل شاشة.

---

## 4. الصور المطلوبة + الـ Prompts

### طريقة التوليد (مهم للتناسق)
- **أضف هالجملة بآخر كل Prompt** عشان كل الصور تطلع بنفس الستايل:
  > `, stylized 2.5D mobile game art, Levantine old-town heritage, tooled dark brown leather with fine gold stitching, polished gold metal trim, warm lantern lighting, deep indigo night palette with amber and turquoise accents, clean shapes, soft painterly shading, high detail, no text`
- **عناصر الواجهة (أيقونات، إطارات، أزرار):** اطلب خلفية شفافة أو خلفية لون واحد سادة (`isolated on plain dark background`) عشان نقصّها بسهولة.
- **ممنوع نص بالصور**، النص بنكتبه بالكود (عشان العربي يطلع صح).
- ولّد **٣ نسخ** من كل صورة واختار الأحلى.

### أ. الخلفيات (نسبة 9:16 عامودي)

| الصورة | Prompt |
|---|---|
| **الحارة بالليل** (القائمة الرئيسية) | `Vertical 9:16 view of a narrow old Levantine alley at night, stone walls, wooden mashrabiya balconies, hanging brass lanterns glowing amber, string lights, a cat on a step, empty space in the middle for UI` |
| **الحارة بالنهار** (مرحلة النهار) | `Same narrow old Levantine alley in warm morning light, sand-colored stone, laundry lines, potted plants, soft shadows, calm atmosphere, empty space in the middle for UI` |
| **الديوانية** (الغرفة الصوتية) | `Vertical 9:16 interior of a traditional Arab diwan majlis at night, low floor cushions in a circle, patterned carpet, brass coffee dallah, lanterns, mashrabiya window with moonlight, top-down three-quarter view, center area clear` |
| **شاشة التحميل** | `Vertical 9:16 silhouette of an old Arab town skyline at night, crescent moon, minarets and domes, warm lantern lights in windows, mist` |

### ب. عناصر الواجهة

| العنصر | Prompt | ملاحظة Unity |
|---|---|---|
| **إطار قوس مشربية** | `Ornate wooden mashrabiya arch frame, empty center, front view, symmetrical, isolated on plain dark background` | 9-Slice |
| **لوحة جلد للبطاقات** | `Rectangular dark brown tooled leather panel with polished gold frame and fine gold saddle stitching along the inner edge, subtle geometric embossing, front view, isolated` | 9-Slice |
| **زر فانوس (ذهبي)** | `Rounded rectangular game button made of polished brass with warm glow, empty, front view, isolated` | 9-Slice |
| **زر ثانوي** | `Rounded rectangular game button of dark brown leather with polished gold outline and gold stitching, empty, front view, isolated` | 9-Slice |
| **شريط جلد (القائمة السفلية)** | `Long horizontal dark leather strip with gold top edge and gold stitching, slight curve, front view, isolated` | 9-Slice |
| **إطار صورة اللاعب** | `Circular avatar frame of polished gold with small geometric star engraving, empty center, front view, isolated` | فوق الصورة |
| **فانوس** | `Single traditional Arabic brass lantern with colored glass glowing amber, front view, isolated` | حركة تمايل |

### ج. الأيقونات (مربع 1:1)

| الأيقونة | Prompt |
|---|---|
| الحارة (الرئيسية) | `Game icon of a small old Arab house doorway with arch and lantern, front view, isolated` |
| الأصحاب | `Game icon of two coffee cups (finjan) side by side with steam, front view, isolated` |
| الرتب | `Game icon of a brass star medal with turquoise gem, front view, isolated` |
| السوق | `Game icon of an old market sack with coins and spices, front view, isolated` |
| عملة | `Game icon of an old gold coin with geometric star engraving, front view, isolated` |
| جوهرة | `Game icon of a turquoise gemstone, faceted, glowing, front view, isolated` |
| مايك شغّال / مكتوم | `Game icon of a vintage brass microphone, front view, isolated` (الكتم منعمله بالكود بخط أحمر) |

### د. بطاقات الأدوار (نسبة 3:4)

| الدور | Prompt |
|---|---|
| **المافيا** | `Character card portrait of a mysterious man in a dark suit and red keffiyeh scarf hiding his face, holding a lantern, night alley, dramatic shadow` |
| **المختار** | `Character card portrait of a dignified elderly neighborhood mukhtar with white mustache, traditional robe and red fez, holding prayer beads, warm light` |
| **الحكيم** | `Character card portrait of a kind traditional healer with herbs and small glass bottles, gentle smile, warm lantern light` |
| **البصّاص** | `Character card portrait of a sharp-eyed young man peeking from behind a mashrabiya window, curious expression, night` |
| **أهل الحارة** | `Character card group portrait of ordinary neighbors: a baker, a grandmother, a shop keeper, standing together in an alley, friendly` |

### هـ. صور اللاعبين (Avatars)
- نفس الستايل، نص الجسم، خلفية لون واحد، **١٢ صورة متنوعة** (رجال، نساء، كبار، شباب) كبداية للاختيار.
- Prompt: `Bust portrait of a [young woman with hijab / old man with keffiyeh / young man with cap / ...], friendly expression, plain amber background, centered`

---

## 5. الترتيب المقترح للتنفيذ
1. **خطوط + دعم العربي** بـ Unity (Reem Kufi + Tajawal + أداة توصيل الحروف)
2. **الديوانية** (المقاعد، حلقة الصوت، المايك) ← أهم شاشة
3. **الحارة** (الرئيسية)
4. **شاشة اللعب** (ليل/نهار + الراوي + بطاقة الدور + التصويت)
5. **التحميل + النتيجة**

الصور ممكن تتولّد بالتوازي: ابدأ بالخلفيات الثلاث + الإطارات + بطاقات الأدوار.

---

## 6. إحساس اللعبة (مش موقع) — تحديث 2026-10-17

**المشكلة اللي صلّحناها:** النسخة الأولى كانت قوائم وبطاقات، فطلعت شكلها موقع. اللعبة لازم تكون **مشاهد**.

| القاعدة | التطبيق |
|---|---|
| **كل شاشة مشهد مرسوم** | الحارة بالليل، داخل الديوانية، خريطة البيوت. مش خلفية لون واحد |
| **الواجهة طايفة فوق المشهد** | شريط علوي (صورة + عملات بزر +)، أزرار جانبية بتطفو، شريط جلد تحت |
| **أزرار سميكة بتنضغط** | ذهبي = الزر الأهم، أخضر = تأكيد/دعوة، خشب = ثانوي |
| **أماكن مش جداول** | الديوانية: اللاعبين **حوالين سجادة**. اللعب: كل لاعب إله **بيت** بالحارة |
| **حركة دايمة خفيفة** | فوانيس بتتمايل، نجوم بتلمع، أزرار بتطفو، الزر الرئيسي بينبض، موجة فيروزية حوالين اللي عم يحكي |
| **شارات ومكافآت** | رقم أحمر على الهدية والمهام، شريط "فاز!" مع أشعة ونجوم وصناديق مكافآت |

### صور إضافية لازمة

| الصورة | Prompt |
|---|---|
| **بيت لاعب** (خريطة الليل، ٣ ألوان) | `Single small old Levantine house facade with arched wooden door and round window, gold trim edges, front view, isolated, game map piece` |
| **بيت معتم** (لاعب مات) | نفس الـ Prompt + `all lights off, cracked window, dark and gloomy` |
| **سجادة الديوانية** (من فوق) | `Top-down view of an oval traditional red Persian-style carpet with gold medallion in the center, isolated` |
| **وسادة مقعد** | `Top-down view of a round floor cushion of red velvet with gold tassels, isolated` |
| **صندوق مكافأة** | `Treasure chest of dark leather and gold metal straps, slightly open with warm glow, front view, isolated` |
| **شمس وقمر** | `Stylized crescent moon with soft glow, isolated` / `Stylized warm sun with soft rays, isolated` |
