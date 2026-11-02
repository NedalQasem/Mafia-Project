# كيف تبدأ مشروع MenaGamingVoiceCore بجلسة منفصلة

1. أنشئ مجلد جديد **خارج** مشروع المافيا، مثلاً `F:\Game_Projects\menagaming-voice-core`.
2. انسخ لجواته محتويات هالمجلد (`voice-engine-starter`): `CLAUDE.md` و`handoff.md` و`00-master-plan.md`.
3. من داخل المجلد: `git init -b dev`، ثم `git add .` و`git commit -m "Initial plan and instructions"` (وأنشئ مستودع GitHub خاص وربطه لما تحب).
4. افتح جلسة Claude Code **جديدة** على ذاك المجلد، وأرسل هالرسالة:

> اقرأ CLAUDE.md ثم handoff.md ثم 00-master-plan.md، وابدأ المرحلة M0. رد عليّ بالعربي، ولخّص لي خطة الأسبوع الأول قبل ما تنفّذ.

5. **الملف المرجعي من هلأ هو النسخة اللي بالمجلد الجديد.** نسخة مشروع المافيا (`Docs/13-voice-engine-master-plan.md`) تبقى للإحالة فقط.
6. الربط مع لعبة المافيا لاحقاً بيصير بجلسة مشروع المافيا، عبر `IVoiceService` و`VoiceBackends` (مش من جلسة الصوت).
