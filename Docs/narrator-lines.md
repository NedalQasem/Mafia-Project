# أسطر الراوي للتسجيل (صوتين: ذكر وأنثى، عربي وإنجليزي)

**كيف:** لكل سطر بالجدولين ولّد ملف صوت **مرتين**: بصوت الراوي الذكر وبصوت الراوية الأنثى، بنفس اسم الملف. الصيغة mp3. الملفات بتنحط هون (واللعبة بتلاقيها لحالها، ما في شي ثاني تغيّره):

- عربي ذكر: `Assets/_Project/Resources/Narrator/ar/m/<اسم الملف>.mp3`
- عربي أنثى: `Assets/_Project/Resources/Narrator/ar/f/<اسم الملف>.mp3`
- إنجليزي ذكر: `Assets/_Project/Resources/Narrator/en/m/<اسم الملف>.mp3`
- إنجليزي أنثى: `Assets/_Project/Resources/Narrator/en/f/<اسم الملف>.mp3`

**بالسكربت:** `node Tools/narrator/generate-narrator.js` بيقرأ الجدولين تحت وبيولّد كل الملفات بأسمائها (شوف `Tools/narrator/README.md`). المفتاح بيجي من متغير بيئة، ما بينكتب بأي ملف.

**ملاحظات:** النص بالجدول هو اللي بينقال (بدون أسماء لاعبين، لأنها بتتغير كل مباراة؛ الاسم بيظهر على شريط الورق). الأسطر نفسها بتتعدّل من `MatchCues.cs`، ولازم يتحدّث هالملف معها (فيه اختبار بيفشل لو انحرفوا). المدة بالثواني هي مدة المشهد باللعبة: خلّي الصوت أقصر منها. أسماء الملفات بدون نقاط عمداً. السكربت بيقرأ الأسطر من الجدول بصيغة `| \`اسم\` | نص | مدة |` فلا تغيّر الصيغة.

## العربي (ar)

| اسم الملف | النص | مدة المشهد |
|---|---|---|
| `avenge_done` | الثأري اختار أحد اللاعبين. خرجوا الاتنين. | 7 |
| `avenge_start` | تم إخراج الثأري! وعنده قرار: بياخد معه لاعب. | 6 |
| `call_doctor` | يا دكتور… اصحى. مين بدك تحمي الليلة؟ | 20 |
| `call_mafia` | يا مافيا… اصحوا. مين بدكم تقتلوا الليلة؟ | 20 |
| `call_sheriff` | يا شيخ… اصحى. مين بدك تفحص الليلة؟ | 20 |
| `call_silencer` | يا كاتم الصوت… اصحى. مين بدك تسكّت بكرة؟ ما بيحكي النهار كله. | 20 |
| `call_sniper` | يا قنّاص… اصحى. معك طلقة وحدة. بدك تقنص الليلة ولا نأجلها؟ | 20 |
| `dawn_kill` | طلع الصبح. هالليلة… قامت المافيا بقتل أحد اللاعبين. | 9 |
| `dawn_quiet` | طلع الصبح. ما مات أحد الليلة. | 6 |
| `dawn_saved` | طلع الصبح. المافيا حاولت تقتل أحد اللاعبين… بس في مين أنقذه! | 9 |
| `dawn_shot` | وسُمعت طلقة قنّاص بالليل… سقط أحد اللاعبين. | 9 |
| `dawn_silenced` | كاتم الصوت كتم صوت أحد اللاعبين لهالنهار. | 4 |
| `day_start` | النقاش بلّش. | 3 |
| `match_start` | تم توزيع الأدوار. السهرة بدأت. | 4 |
| `night_falls` | الكل ينام… أغمض عينيك. | 4 |
| `night_idle` | الليل عم يمشي… اهدأ. | 20 |
| `over_mafia` | انتهت السهرة. فازت المافيا! | 8 |
| `over_town` | انتهت السهرة. فاز أهل الحارة! | 8 |
| `sleep_doctor` | تمام. الدكتور ينام. | 2 |
| `sleep_mafia` | تمام. المافيا ينامون. | 2 |
| `sleep_sheriff` | تمام. الشيخ ينام. | 2 |
| `sleep_silencer` | تمام. كاتم الصوت ينام. | 2 |
| `sleep_sniper` | تمام. القنّاص ينام. | 2 |
| `verdict_mafia` | تم إخراج أحد اللاعبين. كان من المافيا! | 7 |
| `verdict_nobody` | ما استقروا على أحد. ما انطرد حدا. | 4 |
| `verdict_town` | تم إخراج أحد اللاعبين. كان مواطناً بريئاً… | 7 |
| `vote_start` | وقت التصويت. مين بتشكّوا فيه؟ | 3 |

المجموع: 27 سطر، 918 حرف للصوت الواحد.

## الإنجليزي (en)

| اسم الملف | النص | مدة المشهد |
|---|---|---|
| `avenge_done` | The Avenger chose someone. Both are out. | 7 |
| `avenge_start` | The Avenger is out! And they have one last decision: they take someone with them. | 6 |
| `call_doctor` | Doctor… wake up. Who do you protect tonight? | 20 |
| `call_mafia` | Mafia… wake up. Who do you take out tonight? | 20 |
| `call_sheriff` | Sheriff… wake up. Who do you investigate tonight? | 20 |
| `call_silencer` | Silencer… wake up. Who do you silence for tomorrow? They won't say a word all day. | 20 |
| `call_sniper` | Sniper… wake up. You have one bullet. Do you take the shot tonight, or wait? | 20 |
| `dawn_kill` | Morning has come. Last night… the Mafia killed someone. | 9 |
| `dawn_quiet` | Morning has come. Nobody died tonight. | 6 |
| `dawn_saved` | Morning has come. The Mafia tried to kill someone… but someone saved them! | 9 |
| `dawn_shot` | A sniper's shot rang out in the night… someone has fallen. | 9 |
| `dawn_silenced` | The Silencer has taken someone's voice for the day. | 4 |
| `day_start` | The discussion begins. | 3 |
| `match_start` | The roles are dealt. The evening begins. | 4 |
| `night_falls` | Everyone, go to sleep… close your eyes. | 4 |
| `night_idle` | The night moves on… stay quiet. | 20 |
| `over_mafia` | The night is over. The Mafia wins! | 8 |
| `over_town` | The night is over. The neighborhood wins! | 8 |
| `sleep_doctor` | Good. The Doctor goes back to sleep. | 2 |
| `sleep_mafia` | Good. The Mafia goes back to sleep. | 2 |
| `sleep_sheriff` | Good. The Sheriff goes back to sleep. | 2 |
| `sleep_silencer` | Good. The Silencer goes back to sleep. | 2 |
| `sleep_sniper` | Good. The Sniper goes back to sleep. | 2 |
| `verdict_mafia` | Someone is out. They were Mafia! | 7 |
| `verdict_nobody` | No one could agree. Nobody is out. | 4 |
| `verdict_town` | Someone is out. They were an innocent citizen… | 7 |
| `vote_start` | Time to vote. Who do you suspect? | 3 |
