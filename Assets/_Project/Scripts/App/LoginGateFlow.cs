using System.Collections;
using MafiaNight.Core;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Config;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// The first-start choice screen shown over the loading screen, before the game signs in (see <see cref="LoginGate"/>).
    /// Guest: the sign-in goes on. Office account: the sign-in starts too (the new device needs its identity to take the old account) and the recovery
    /// sheet opens; the loading waits behind the choice screen. Google: the player picks his Google account first, the server finds or makes the account
    /// of that identity, the device is tied to it, and only then does the PlayFab sign-in go on, so no empty guest account is ever made for him.
    /// </summary>
    public static class LoginGateFlow
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() { LoginGate.Screen = Run; }

        private static IEnumerator Run(MonoBehaviour host)
        {
            var th = KitTheme.Current;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (th == null || th.loginChoicePrefab == null || canvas == null) { LoginGate.Release(); yield break; }
            var root = canvas.rootCanvas.transform;
            var go = Object.Instantiate(th.loginChoicePrefab, root, false);
            go.transform.SetAsLastSibling();
            var screen = go.GetComponent<KitLoginChoice>();
            screen.SetAvailable(GoogleSignIn.Available, false);
            bool proceed = false, busy = false;
            screen.OnGuest += () => { if (!busy) proceed = true; };
            screen.OnOffice += () =>
            {
                if (busy) return;
                LoginGate.Release();
                if (th.recoverySheetPrefab == null) return;
                var sheetGo = Object.Instantiate(th.recoverySheetPrefab, root, false);
                sheetGo.transform.SetAsLastSibling();
                var sheet = sheetGo.GetComponent<KitRecoverySheet>();
                RecoveryFlow.Wire(sheet, host);
                sheet.Open();
            };
            screen.OnGoogle += () =>
            {
                if (busy) return;
                busy = true;
                host.StartCoroutine(GoogleStart(screen, () => proceed = true, () => busy = false));
            };
            while (!proceed) yield return null;
            LoginGate.ResumePlayFab();          // a Google attempt that did not finish must not leave the PlayFab sign-in waiting
            PlayerPrefs.SetInt(LoginGate.ChosenKey, 1); PlayerPrefs.Save();
            LoginGate.Release();
            screen.Close();
        }

        private static IEnumerator GoogleStart(KitLoginChoice screen, System.Action succeeded, System.Action finished)
        {
            screen.SetMessage("");
            string token = null, why = null;
            yield return GoogleSignIn.SignIn((t, e) => { token = t; why = e; });
            if (token == null) { screen.SetMessage(GoogleReason(why)); finished(); yield break; }

            // the device identity is needed to tie the device to the account, but the PlayFab sign-in waits until that account is open
            LoginGate.DeferPlayFabLogin();
            LoginGate.Release();
            ServiceLocator.TryGet<IAuthService>(out var auth);
            float until = Time.unscaledTime + 30f;
            while (auth != null && !auth.IsEOSLoggedIn && Time.unscaledTime < until) yield return null;
            if (auth == null || !auth.IsEOSLoggedIn) { screen.SetMessage("تعذّر الاتصال بالخادم. جرّب كمان مرة."); finished(); yield break; }

            IdentityResult res = null;
            yield return IdentityService.LoginGoogle(token, r => res = r);
            if (res == null || !res.Ok) { screen.SetMessage(ServerReason(res != null ? res.Error : "network")); finished(); yield break; }

            bool linked = false, ok = false;
            auth.LinkDeviceToAccount(res.SessionTicket, (o, _) => { ok = o; linked = true; });
            while (!linked) yield return null;
            if (!ok) { screen.SetMessage("ما قدرنا نربط الجهاز بحسابك، جرّب كمان مرة."); finished(); yield break; }
            succeeded();
            finished();
        }

        internal static string GoogleReason(string why)
        {
            switch (why)
            {
                case "cancelled": return "";
                case "no_account": return "ما في حساب Google على جهازك. أضف حساب من إعدادات الجهاز وجرّب.";
                case "unavailable": return "الدخول بـ Google مش متاح هون.";
                default: return "تعذّر الدخول بـ Google. جرّب كمان مرة.";
            }
        }

        internal static string ServerReason(string code)
        {
            switch (code)
            {
                case "bad_token": return "تعذّر التحقق من حساب Google.";
                case "not_configured": return "الدخول بـ Google مش مفعّل بعد.";
                case "try_again": return "السيرفر مش جاهز هلأ، جرّب بعد شوي.";
                default: return "ما في اتصال بالسيرفر.";
            }
        }
    }
}
