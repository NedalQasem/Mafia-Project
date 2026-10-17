# 06 — المعمارية: الحالية والمستهدفة

> آخر تحديث: 2026-10-17
> الهدف: أساس صحيح قبل ما نبدأ تطوير الغرف والمباراة على الشبكة.

---

## 1. الوضع الحالي ✅

```
           ┌──────────────────────────────────────────────┐
           │  Bootstrap (Composition Root)                │
           │  login → profile → wallet → أول مشهد         │
           └──────────────┬───────────────────────────────┘
                          │ ServiceLocator
   ┌──────────────────────┼─────────────────────────────┐
   ▼                      ▼                             ▼
 UI (Screens)  →  Gameplay (Match, Seating)  →  Services (Auth, Economy, Profile, Scenes)
                                                       │
                                               Data (Profile, RankRules) → Core
```

| البند | الحالة |
|---|---|
| طبقات منفصلة + asmdef باتجاه واحد | ✅ |
| خدمات خلف Interfaces + ServiceLocator | ✅ |
| Bootstrap كنقطة بداية، و Play من أي مشهد | ✅ |
| دخول EOS Connect (DeviceID) + Android | ✅ الكود، ⏳ إعداد Sandbox |
| PlayFab: بروفايل + عملات | ✅ (⚠️ العملات من الجهاز، راجع 04) |
| الشبكة (غرف، صوت، مباراة) | ❌ لسا |
| اختبارات آلية | ❌ ما في |

---

## 2. الفجوات قبل التطوير الحقيقي

| # | الفجوة | ليش مهمة | الحل |
|---|---|---|---|
| 1 | `EOSSDKComponent` مش مسجّل كخدمة | الغرف والصوت بدهم الـ Platform والـ ProductUserId | Interface اسمه `IEosPlatform` |
| 2 | `GameManager` بيخلط القواعد مع Unity والتوقيت | ما بنقدر نختبره، وما بنقدر نشغّله على الـ Host بس | فصل **قواعد المباراة** (C# صافي) عن **النقل** (EOS) |
| 3 | ما في اختبارات | أي تعديل بقواعد المافيا ممكن يكسر شي بدون ما ننتبه | EditMode tests للقواعد (الأدوار، التصويت، الفوز، الرتب) |
| 4 | خليط callbacks و coroutines و `async void` | صعب نتتبع الأخطاء بالشبكة | توحيد على `Awaitable`/`Task` لعمليات EOS الجديدة |
| 5 | `Debug.Log` قديمة (~80 سطر) | سجلات بنسخة الإطلاق | تحويلها لـ `Log.Info` تدريجياً |

---

## 3. المعمارية المستهدفة للشبكة

```
UI ─────────────► IRoomService ◄────── RoomBrowser / RoomScreen
                  IVoiceService ◄───── أزرار المايك، مؤشر "عم يحكي"
                  IMatchService ◄───── GameplayUI

Services/Networking (MafiaNight.Networking.asmdef)
 ├─ EosPlatform        : IEosPlatform   (Platform, LocalUserId, IsLoggedIn)
 ├─ EosRoomService     : IRoomService   (EOS Lobbies: إنشاء/بحث/دخول/خروج/attributes/أعضاء)
 ├─ EosVoiceService    : IVoiceService  (EOS RTC عبر الـ Lobby: كتم، صوت، مين عم يحكي)
 └─ EosMatchTransport  : IMatchTransport (EOS P2P: رسائل المباراة Host ↔ لاعبين)

Gameplay/Match
 ├─ MatchRules   (C# صافي، بدون Unity): توزيع الأدوار، التصويت، نتيجة الليل، الفوز
 ├─ MatchState   (C# صافي): المرحلة، اللاعبين، الأصوات، المؤقت
 ├─ HostMatchController : بيشتغل عند صاحب الغرفة بس، بيطبق القواعد وبيبعت الحالة
 └─ ClientMatchView     : عند الكل، بيستقبل الحالة وبيعرضها
```

### مبادئ
1. **صاحب الغرفة (Host) هو المرجع:** هو الوحيد اللي بيوزّع الأدوار وبيحسم التصويت.
2. **الأدوار السرية ما بتنبعت للكل:** كل لاعب بيستلم دوره برسالة خاصة.
3. **قواعد المافيا C# صافي:** بنختبرها بدون Unity وبدون شبكة.
4. **الواجهة ما بتعرف EOS:** بتحكي مع `IRoomService` و `IVoiceService` بس. لو بدنا نغيّر EOS بيوم، بنغيّر طبقة وحدة.
5. **خروج الـ Host:** الـ Lobby بينقل الملكية لعضو ثاني، والمباراة بتتلغى أو بتنتقل (نقرر لاحقاً).

### ترتيب البناء المقترح
1. `IEosPlatform` + تحديث SDKs
2. `MatchRules` + اختبارات (بدون شبكة)
3. `IRoomService` (EOS Lobbies) + شاشة قائمة الغرف
4. `IVoiceService` (RTC بالغرفة)
5. `IMatchTransport` + `HostMatchController` + `ClientMatchView`
6. اختبار 6 لاعبين حقيقيين

---

## 4. نسخ الـ SDK

| SDK | المثبّت | الأحدث | ملاحظات |
|---|---|---|---|
| **EOS C# SDK** | ✅ **1.19.2.1** (حُدّث 2026-10-17) | 1.19.2.1 (2026-09-22) | فيه **إصلاح صوت "روبوتي"** بين اللاعبين. 1.19 بيلغي دعم Android 32-bit و Android 6/7 |
| **PlayFab Unity SDK** | ✅ **2.244.260922** (حُدّث 2026-10-17) | 2.244.260922 | محدّث. الاتصال و Title ID مجرّبين |

**متطلبات EOS 1.19 على Android:**
- ✅ أقل نسخة Android 8 (`AndroidMinSdkVersion: 26`): متوفر عنّا
- ✅ **ARM64 بس** (شلنا ARMv7)
- ✅ StaticSTDC: هو اللي منستخدمه

**مصادر:**
- [EOS SDK Release Notes](https://dev.epicgames.com/docs/epic-online-services/release-notes)
- [PlayFab UnitySDK Releases](https://github.com/PlayFab/UnitySDK/releases)
