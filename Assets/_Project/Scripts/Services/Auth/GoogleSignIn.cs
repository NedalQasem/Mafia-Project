using System;
using System.Collections;
using MafiaNight.Core;
using UnityEngine;

namespace MafiaNight.Services.Auth
{
    /// <summary>
    /// Google sign-in on Android (the Credential Manager, through <c>Assets/Plugins/Android/GoogleSignInBridge.java</c>): shows Google's own account chooser
    /// and gives back an ID token meant for the game's web client, which the rewards server checks. Not available in the Editor or on other platforms.
    /// </summary>
    public static class GoogleSignIn
    {
        public const string ReceiverName = "GoogleSignInReceiver";

        public static bool Available => Application.platform == RuntimePlatform.Android;

        /// <summary>done(idToken, null) on success; done(null, reason) otherwise (cancelled, no_account, failed, unavailable, timeout).</summary>
        public static IEnumerator SignIn(Action<string, string> done)
        {
            if (!Available) { done?.Invoke(null, "unavailable"); yield break; }
            var receiver = Receiver.Ensure();
            receiver.Reset();
            bool started = true;
            try
            {
                using (var bridge = new AndroidJavaClass("com.menagaming.mafia.GoogleSignInBridge"))
                    bridge.CallStatic("signIn", GameConstants.GOOGLE_WEB_CLIENT_ID, ReceiverName);
            }
            catch (Exception e) { Debug.LogWarning("[Google] bridge failed: " + e.Message); started = false; }
            if (!started) { done?.Invoke(null, "failed"); yield break; }
            float until = Time.unscaledTime + 180f;
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
            // called by the Java bridge (UnitySendMessage)
            public void OnGoogleToken(string token) { Token = token; }
            public void OnGoogleError(string reason) { Error = string.IsNullOrEmpty(reason) ? "failed" : reason; }
        }
    }
}
