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
            if (th == null || th.loginChoicePrefab == null) { LoginGate.Release(); yield break; }
            // its own canvas above the loading screen (that one sorts at 200): on a phone the first canvas found was a lower one,
            // so the choice screen sat hidden under the loading screen and the game waited at 0 % for a choice nobody could see
            var root = OverLoading().transform;
            var go = Object.Instantiate(th.loginChoicePrefab, root, false);
            go.transform.SetAsLastSibling();
            var screen = go.GetComponent<KitLoginChoice>();
            screen.SetAvailable(GoogleSignIn.Available, FacebookSignIn.Available);
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
                host.StartCoroutine(SocialStart("google", screen, () => proceed = true, () => busy = false));
            };
            screen.OnFacebook += () =>
            {
                if (busy) return;
                busy = true;
                host.StartCoroutine(SocialStart("facebook", screen, () => proceed = true, () => busy = false));
            };
            while (!proceed) yield return null;
            LoginGate.ResumePlayFab();          // a Google or Facebook attempt that did not finish must not leave the PlayFab sign-in waiting
            PlayerPrefs.SetInt(LoginGate.ChosenKey, 1); PlayerPrefs.Save();
            LoginGate.Release();
            screen.Close();
        }

        private const int OverLoadingOrder = 500;

        /// <summary>A screen-space canvas drawn above the loading screen, scaled like the game's (1080 x 2400 by width), with input.</summary>
        private static Canvas OverLoading()
        {
            var go = new GameObject("LoginGateCanvas", typeof(RectTransform));
            var c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = OverLoadingOrder;
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 2400); scaler.matchWidthOrHeight = 0f;
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return c;
        }

        /// <summary>The provider's own sign-in on the phone ("google" or "facebook"): done(token, null) or done(null, reason).</summary>
        internal static IEnumerator ProviderToken(string provider, System.Action<string, string> done)
        {
            if (provider == "facebook") yield return FacebookSignIn.SignIn(done);
            else yield return GoogleSignIn.SignIn(done);
        }

        internal static string ProviderName(string provider) => provider == "facebook" ? "Facebook" : "Google";

        private static IEnumerator SocialStart(string provider, KitLoginChoice screen, System.Action succeeded, System.Action finished)
        {
            screen.SetMessage("");
            string token = null, why = null;
            yield return ProviderToken(provider, (t, e) => { token = t; why = e; });
            if (token == null) { screen.SetMessage(ProviderReason(provider, why)); finished(); yield break; }

            // the device identity is needed to tie the device to the account, but the PlayFab sign-in waits until that account is open
            LoginGate.DeferPlayFabLogin();
            LoginGate.Release();
            ServiceLocator.TryGet<IAuthService>(out var auth);
            float until = Time.unscaledTime + 30f;
            while (auth != null && !auth.IsEOSLoggedIn && Time.unscaledTime < until) yield return null;
            if (auth == null || !auth.IsEOSLoggedIn) { screen.SetMessage("تعذّر الاتصال بالخادم. جرّب كمان مرة."); finished(); yield break; }

            IdentityResult res = null;
            yield return IdentityService.Login(provider, token, r => res = r);
            if (res == null || !res.Ok) { screen.SetMessage(ServerReason(res != null ? res.Error : "network", provider)); finished(); yield break; }

            bool linked = false, ok = false;
            auth.LinkDeviceToAccount(res.SessionTicket, (o, _) => { ok = o; linked = true; });
            while (!linked) yield return null;
            if (!ok) { screen.SetMessage("ما قدرنا نربط الجهاز بحسابك، جرّب كمان مرة."); finished(); yield break; }
            succeeded();
            finished();
        }

        internal static string ProviderReason(string provider, string why)
        {
            string n = ProviderName(provider);
            switch (why)
            {
                case "cancelled": return "";
                case "no_account": return "ما في حساب " + n + " على جهازك. أضف حساب من إعدادات الجهاز وجرّب.";
                case "unavailable": return "الدخول بـ " + n + " مش متاح هون.";
                default: return "تعذّر الدخول بـ " + n + ". جرّب كمان مرة.";
            }
        }

        internal static string ServerReason(string code, string provider = "google")
        {
            string n = ProviderName(provider);
            switch (code)
            {
                case "bad_token": return "تعذّر التحقق من حساب " + n + ".";
                case "not_configured": return "الدخول بـ " + n + " مش مفعّل بعد.";
                case "try_again": return "السيرفر مش جاهز هلأ، جرّب بعد شوي.";
                default: return "ما في اتصال بالسيرفر.";
            }
        }
    }
}
