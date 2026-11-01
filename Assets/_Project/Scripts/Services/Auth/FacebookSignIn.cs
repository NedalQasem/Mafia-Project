using System;
using System.Collections;
using MafiaNight.Core;
using UnityEngine;

namespace MafiaNight.Services.Auth
{
    /// <summary>
    /// Facebook sign-in on Android (Facebook's own login SDK, through <c>Assets/Plugins/Android/FacebookLoginActivity.java</c>): shows Facebook's login
    /// (the Facebook app, or a browser tab without it) and gives back a user access token, which the rewards server checks with Facebook.
    /// Not available in the Editor, on other platforms, or until the app's client token is in <see cref="GameConstants.FACEBOOK_CLIENT_TOKEN"/>.
    /// </summary>
    public static class FacebookSignIn
    {
        public const string ReceiverName = "FacebookSignInReceiver";

        public static bool Available => Application.platform == RuntimePlatform.Android && GameConstants.FacebookReady;

        /// <summary>done(accessToken, null) on success; done(null, reason) otherwise (cancelled, failed, unavailable, timeout).</summary>
        public static IEnumerator SignIn(Action<string, string> done)
        {
            if (!Available) { done?.Invoke(null, "unavailable"); yield break; }
            var receiver = Receiver.Ensure();
            receiver.Reset();
            bool started = true;
            try
            {
                using (var bridge = new AndroidJavaClass("com.menagaming.mafia.FacebookLoginActivity"))
                    bridge.CallStatic("signIn", ReceiverName);
            }
            catch (Exception e) { Debug.LogWarning("[Facebook] bridge failed: " + e.Message); started = false; }
            if (!started) { done?.Invoke(null, "failed"); yield break; }
            float until = Time.unscaledTime + 300f;           // the player may type his Facebook password in the browser tab
            while (receiver.Token == null && receiver.Error == null && Time.unscaledTime < until) yield return null;
            if (receiver.Token != null) done?.Invoke(receiver.Token, null);
            else done?.Invoke(null, receiver.Error ?? "timeout");
        }

        /// <summary>The Java side answers through UnitySendMessage to an object with this name.</summary>
        private sealed class Receiver : MonoBehaviour
        {
            public string Token, Error;
            private static Receiver instance;

            public static Receiver Ensure()
            {
                if (instance != null) return instance;
                var go = new GameObject(ReceiverName); DontDestroyOnLoad(go);
                instance = go.AddComponent<Receiver>();
                return instance;
            }

            public void Reset() { Token = null; Error = null; }
            // called by the Java side (UnitySendMessage)
            public void OnFacebookToken(string token) { Token = token; }
            public void OnFacebookError(string reason) { Error = string.IsNullOrEmpty(reason) ? "failed" : reason; }
        }
    }
}
