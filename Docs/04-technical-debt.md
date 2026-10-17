# 04 — الديون التقنية والأمان

> آخر تحديث: 2026-10-17
> أشياء تقنية **لازم تتصلّح** قبل الإطلاق. كل بند فيه مكانه بالكود.
> 🔴 قبل أي نسخة عامة · 🟠 قبل الإطلاق · 🟡 لما يصير وقت

---

## 🔴 الأمان

### 1. العملات بتنضاف من جهاز اللاعب
- **المكان:** `Assets/_Project/Scripts/Services/Economy/CurrencyManager.cs` (`AddCurrency`)
- **المشكلة:** `AddUserVirtualCurrency` بينادي من الـ client. أي لاعب بيقدر يعطي نفسه عملات بلا حدود.
- **نفس المشكلة:** جوائز النتائج في `ResultsUIManager.cs`، واسترجاع رسوم الـ Lobby في `LobbyUIManager.cs`.
- **الحل:**
  1. نقل منح العملات لـ **PlayFab CloudScript / Azure Functions**
  2. من لوحة PlayFab: تعطيل *"Allow client to add virtual currency"*

### 2. نتائج المباريات والرتب من جهاز اللاعب
- **المكان:** `Services/Profile/PlayerProfileManager.cs` (`RecordMatchResult`، `SaveProfile`)
- **المشكلة:** اللاعب بيرسل الـ XP والرتبة والإحصائيات بنفسه عبر `UpdateUserData`.
- **الحل:** الـ Host يبعت نتيجة المباراة للسيرفر، والسيرفر يحسب ويحفظ. وقواعد `RankRules.cs` لازم تتكرر بالسيرفر.

### 3. تسجيل دخول PlayFab قابل للتخمين
- **المكان:** `Services/Auth/PlayFabEOSLogin.cs` (`LoginToPlayFab`)
- **المشكلة:** `LoginWithCustomID` بيستخدم رقم حساب EOS. اللي بيعرف الرقم بيقدر يدخل على الحساب.
- **الحل:** `LoginWithOpenIdConnect` باستخدام توكن EOS، فـ PlayFab بيتحقق من الهوية بنفسه.

### 4. الأدوار السرية
- **المكان:** `Gameplay/Match/GameManager.cs` (`AssignRoles`)
- **المشكلة (مستقبلية):** لما نضيف الشبكة، لازم **ما نبعت دور كل لاعب لكل الأجهزة**.
- **الحل:** الـ Host يبعت لكل لاعب دوره بس (رسالة خاصة)، والمافيا يعرفوا بعض.

---

## 🔴 تسجيل الدخول على الموبايل

- **المكان:** `Services/Auth/EOSSDKComponent.cs` (`StartLogin`)
- **المشكلة:** الدخول حالياً `Developer` عبر **DevAuthTool**، وهذا بيشتغل **داخل Unity بس**. على الموبايل ما رح يشتغل أبداً.
- **الحل:** **EOS Connect + DeviceID** (دخول تلقائي بدون حساب Epic)، ولاحقاً ربط بحساب Google/Apple.
- **ملاحظة:** حالياً DevAuthTool ما فيه حساب اسمه `TestUser1`، فالدخول بيفشل بـ `NotFound`.

---

## 🟠 الشبكة (المرحلة الجاية)

- **المكان:** `Gameplay/Match/GameManager.cs`
- **المشكلة:** المباراة بتشتغل محلياً على كل جهاز لحاله. كل لاعب رح يشوف لعبة مختلفة.
- **الحل:**
  - **EOS Lobbies:** إنشاء غرفة والانضمام بكود
  - **صاحب الغرفة (Host)** هو الوحيد اللي بيشغّل `GameManager`، وبيبعت الحالة للباقي
  - **EOS P2P:** رسائل اللعب (تصويت، أفعال الليل)
  - **EOS RTC:** الصوت (غرفة للكل بالنهار، وغرفة للمافيا بالليل)
  - التعامل مع خروج الـ Host بنص المباراة

---

## 🟠 الإشراف (Moderation)

- [ ] زر إبلاغ عن لاعب (يحفظ البلاغ في PlayFab)
- [ ] كتم وحظر محلي
- [ ] حظر تلقائي مؤقت بعد عدد بلاغات معيّن (من السيرفر)
- [ ] صفحة سياسة الخصوصية وشروط الاستخدام (مطلوبة للمتاجر)

---

## 🟡 تنظيف وتحسينات

| البند | المكان | الملاحظة |
|---|---|---|
| تحذير "2 PlayFabSharedSettings" | `Assets/PlayFabSDK/.../Resources` و `Assets/PlayFabEditorExtensions/.../Resources` | نسختين من ملف الإعدادات. احذف وحدة بعد التأكد إنهم متطابقين |
| `Debug.Log` قديمة | Gameplay و UI | حوّلها لـ `Log.Info` (بتنحذف من نسخة الإطلاق تلقائياً) |
| صور غير مستخدمة | `Assets/_Project/Art/_Unused` | قرّر: تستخدمها أو تحذفها |
| مجلد `_Recovery` | `Assets/_Recovery` | مشاهد احتياطية من Unity بعد crash. احذفه إذا ما بتحتاجه |
| ملفات Linux/Mac من EOS | `Assets/ThirdParty/EOS-SDK/SDK/Bin` | ~95MB مش ضرورية للموبايل. ممكن تنحذف لتصغير المشروع |
| `RoomTierData` (3 أنواع غرف) | MainMenu | خلّيها غرفة وحدة بالبداية (راجع خطر الغرف الفاضية) |

---

## 🏗️ مرجع سريع للبنية الحالية

```
Assets/_Project/Scripts/
├── Core/        ServiceLocator, PersistentService, Log, GameConstants
├── Data/        PlayerProfile, RankRules, RoomTierData, Customization
├── Services/
│   ├── Auth/      IAuthService       ← EOSSDKComponent + PlayFabEOSLogin
│   ├── Economy/   ICurrencyService   ← CurrencyManager
│   ├── Profile/   IPlayerProfileService ← PlayerProfileManager
│   ├── Scenes/    ISceneLoader       ← SceneTransitionManager
│   └── Session/   RoomSession
├── Gameplay/    Match (GameManager), Seating, Player
├── UI/          Screens, Components, Animations
├── Bootstrap/   BootstrapManager (نقطة البداية)
├── DebugTools/  أدوات اختبار
└── Editor/      PlayFromBootstrap, AndroidNamespaceFixer
```

**قواعد:**
- أي خدمة جديدة: interface + تسجيل في `ServiceLocator` + بالـ asmdef الصح
- نقل أي ملف بالمشروع: من داخل Unity (عشان ما تنكسر الروابط)
- اتجاه الاعتماد: `UI → Gameplay → Services → Data → Core` (ما في رجوع للخلف)
