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

### 3. تسجيل دخول PlayFab قابل للتخمين — 🟡 الكود جاهز، باقي الإعداد
- **المكان:** `Services/Auth/PlayFabEOSLogin.cs` (`LoginToPlayFab`)
- **المشكلة:** `LoginWithCustomID` بيستخدم الـ ProductUserId. اللي بيعرف الرقم بيقدر يدخل على الحساب.
- **✅ تم بالكود:** دعم `LoginWithOpenIdConnect` بتوكن EOS Connect (JWT موقّع من Epic).
- **⏳ باقي عليك:**
  1. في PlayFab Game Manager → Title Settings → **OpenID Connect** → أضف اتصال جديد
     (Issuer: `https://api.epicgames.dev`، و Client ID = الـ Client ID تبع EOS)
  2. حط اسم الاتصال في حقل `openIdConnectionId` على `PlayFabEOSLogin` (مشهد Bootstrap)
  3. بعدها احذف مسار CustomID من الكود
- **ملاحظة:** حسابات PlayFab القديمة كانت مربوطة بـ EpicAccountId. صارت الآن على الـ ProductUserId، فالحسابات التجريبية القديمة ما رح تُستخدم.

### 4. الأدوار السرية
- **المكان:** `Gameplay/Match/GameManager.cs` (`AssignRoles`)
- **المشكلة (مستقبلية):** لما نضيف الشبكة، لازم **ما نبعت دور كل لاعب لكل الأجهزة**.
- **الحل:** الـ Host يبعت لكل لاعب دوره بس (رسالة خاصة)، والمافيا يعرفوا بعض.

---

## 🟠 تسجيل الدخول على الموبايل — الكود ✅، إعدادات EOS ⏳

- **المكان:** `Services/Auth/EOSSDKComponent.cs`
- **✅ تم:**
  - **EOS Connect + DeviceID** (الافتراضي): دخول تلقائي بدون حساب Epic، وبينتج ProductUserId (للـ Lobbies والصوت)
  - وضع **DevAuthTool** بالـ Editor بس، لتجربة أكثر من لاعب على نفس الجهاز
  - تهيئة EOS على Android (`EOSSDK.init` + `AndroidInitializeOptions`) + مكتبات androidx في `mainTemplate.gradle`
  - تجديد التوكن تلقائياً قبل ما ينتهي (~ساعة)
  - خيار `sdkLogLevel` لتشخيص مشاكل EOS
- **⏳ مشكلة حالية (إعداد، مش كود):**
  الدخول بالـ DeviceID بيرجع `AccessDenied`، والسبب من سيرفر Epic:
  `errors.com.epicgames.eos.auth.deployment_user_access_forbidden`
  الـ Sandbox الحالي (`p-...`) هو Dev/Stage وخاص، فبيرفض المستخدمين المجهولين.
  **الحل:** استخدم **Sandbox و Deployment تبع Live** في `EOSSDKComponent` (من Developer Portal)،
  أو افتح الوصول للـ Dev sandbox إذا البورتال بيسمح.
  **للتطوير حالياً:** وضع DevAuthTool بيشتغل لأعضاء المنظمة (اعمل credential اسمه `TestUser1` بالأداة).
- **⏳ لاحقاً:** ربط الحساب بـ Google/Apple عشان اللاعب ما يخسر تقدّمه لو غيّر جهازه.
- **⚠️ ملاحظة للمرحلة الجاية:** كل نسخ اللعبة على نفس الكمبيوتر بتاخد **نفس الـ DeviceID**. لتجربة أكثر من لاعب على جهاز واحد استخدم وضع DevAuthTool بأسماء credentials مختلفة.

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

---

## ✅ إعدادات EOS في البورتال (2026-10-17)

| البند | القيمة |
|---|---|
| Sandbox | **Live** (`26406e73…`). الـ Dev خاص وبيرفض DeviceID |
| Deployment | **Live Deployment** (`c96c080c…`) |
| Client باللعبة | **MafiaMobileClient** (`xyza7891rUkz…`) على Policy **MafiaMobileP2PPolicy** (Peer2Peer: Lobbies, Voice, Sessions, Stats…) |
| Client القديم | **MafiaGameClient** (TrustedServer). **ما بينحط باللعبة أبداً**، بس للسيرفر/CloudScript لاحقاً |
| الصوت (RTC) | مفعّل بالكود (`WindowsRTCOptions` + xaudio2 على Windows، `RTCOptions` على Android) |
| حد الصوت بالغرفة | **16** (مجرّب) |

⚠️ **الـ Client Secret محفوظ بمشهد Bootstrap.** هذا طبيعي لـ Client من نوع Peer2Peer، بس **خلّي الـ repo على GitHub خاص**.
⏳ **Android:** لازم نضيف صلاحية المايك (`RECORD_AUDIO`) ونطلبها من اللاعب وقت التشغيل لما نبني خدمة الصوت.
