# بورتريهات كروت الأدوار — مواصفات ونصوص التوليد

**الوضع:** أنا (Claude) ما بقدر أرسم صور واقعية بنفسي. أداة التوليد بـUnity (fal.ai / OpenRouter) بتحتاج مفتاح من المالك، ولسا مش مفعّلة. فالبديل: المالك يولّد الصور بأي أداة بيحبها (Midjourney، Gemini، ChatGPT، Firefly…) بالنصوص تحت، وأنا بركّبها بالإطار والطبقات.

## المواصفات (لكل الصور)
- **المقاس:** ٧٦٨×١٠٢٤ (نسبة ٣:٤)، وأنا بقصّها للنافذة. PNG أو JPG عالي الجودة.
- **التكوين:** من الرأس لوسط الجسم، الشخصية بالنص، نظرة لـ٣/٤ للكاميرا، فراغ بسيط تحت الصدر (ينحط عليه بنر الاسم).
- **الخلفية:** غامقة سادة (تدرّج داكن بلون الدور)، بدون نصوص ولا شعارات ولا علامات مائية.
- **الإضاءة والألوان (موحّدة لكل الكروت):** إضاءة سينمائية من اليسار مع ضوء حافة خفيف، حبيبات فيلم قديم، ألوان باهتة دافئة ١٩٣٠s نوار. لون التمييز لكل دور: مافيا قرمزي، شيخ أزرق، طبيب أخضر مزرق، مواطن نحاسي.
- **التسمية:** `portrait_<role>.png` بمجلد `Assets/_Project/Art/Cards/` (الأدوار: `civilian, mafia, doctor, sheikh, silencer, sniper, kid`).
- **أصلية:** لا نقلّد أي لعبة ولا شخصية معروفة ولا ممثل.
- **خلاصة نصوص التجنّب (تنضاف لكل نص):** `no text, no watermark, no logo, no extra fingers, no deformed hands, no modern clothes`.

## الأسلوب المشترك (يُلصق أول كل نص)
`cinematic photorealistic portrait, 1930s film noir, 85mm lens, shallow depth of field, soft key light from the left with a thin rim light, subtle film grain, muted warm color grade, dark plain gradient background, three-quarter view, head to mid-torso, centered, empty space at the bottom`

## نصوص الأدوار
1. **مافيا (قرمزي):** `a ruthless 1930s mafia boss in his forties, black fedora with a crimson hatband, black pinstripe three-piece suit, crimson silk tie, white pocket square, cold calm stare, faint scar on the cheek, background gradient deep crimson to black`
2. **شيخ المواطنين (أزرق):** `a dignified elderly Arab village elder, white ghutra with black agal, trimmed gray beard, wise stern eyes, simple cream thobe with a small brass star badge on the chest, background gradient deep navy blue to black`
3. **طبيب (أخضر مزرق):** `a calm 1930s town doctor in his thirties, round head mirror on the forehead, white coat over a waistcoat, stethoscope around the neck, kind but tired eyes, small leather doctor bag in the lower corner, background gradient deep teal to black`
4. **مواطن (نحاسي):** `an honest working-class man of the 1930s, worn flat cap, brown wool coat over a collarless shirt, weathered friendly face, a hint of stubble, background gradient warm bronze to black`
5. **كاتم الصوت (مافيا — قرمزي داكن):** `a quiet 1930s mafia enforcer, dark fedora tilted low, black leather glove raised with one finger pressed to the lips in a hushing gesture, dark scarf, expressionless eyes, background gradient dark crimson to black`
6. **القنّاص (مواطن — نحاسي/زيتوني):** `a lean 1930s sharpshooter, flat cap, wool scarf, one eye narrowed, holding a vintage rifle scope up near the face, calm focused expression, background gradient olive bronze to black`
7. **الولد (مواطن — نحاسي):** `a serious 13-year-old street kid of the 1930s, oversized newsboy cap, patched wool jacket, smudged cheek, defiant intelligent eyes, background gradient warm bronze to black`

## بعد ما توصل الصور
أنا بعمل: قص للنافذة، ضغط ASTC (قواعد `Docs/13`)، تركيبها مع طبقات الإطار والخلفية والحركة وظهر الكرت، وتحديث المعاينة `role-cards.html` بالصور الحقيقية قبل ما نبني بـUnity. الأدوار الجديدة (٥–٧) بنولّد صورها بس تحتاجها (لما يقول المالك «نفّذ»).
