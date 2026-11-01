using System;
using System.Collections;
using System.Text;
using MafiaNight.Core;
using PlayFab;
using UnityEngine;
using UnityEngine.Networking;

namespace MafiaNight.Services.Config
{
    public sealed class IdentityResult
    {
        public bool Ok, Created, Already;
        /// <summary>bad_token, account_exists, not_configured, try_again, network.</summary>
        public string Error;
        /// <summary>When the identity belongs to another account: that account's name (for the question to the player).</summary>
        public string ExistingName;
        public string SessionTicket, PlayFabId;
    }

    [Serializable] internal sealed class IdentityDto { public bool ok, created, already; public string error, name, sessionTicket, playFabId; }

    /// <summary>Sign in with Google or Facebook through the rewards server (/v1/auth/&lt;provider&gt;/login and /link). The server checks the token with the provider.</summary>
    public static class IdentityService
    {
        private static string Base => (GameConstants.ApiUrl ?? "").TrimEnd('/');

        /// <summary>Opens the account that belongs to this Google identity (the server makes one for a new identity); the result carries a PlayFab session of it.</summary>
        public static IEnumerator LoginGoogle(string idToken, Action<IdentityResult> done) => Login("google", idToken, done);

        /// <summary>Opens the account of a "google" or "facebook" identity (the token is Google's ID token or Facebook's access token).</summary>
        public static IEnumerator Login(string provider, string token, Action<IdentityResult> done)
        {
            yield return Post("/v1/auth/" + provider + "/login", Field(provider), token, false, done);
        }

        /// <summary>Ties a "google" or "facebook" identity to the account of the player who is signed in.</summary>
        public static IEnumerator Link(string provider, string token, Action<IdentityResult> done)
        {
            yield return Post("/v1/auth/" + provider + "/link", Field(provider), token, true, done);
        }

        private static string Field(string provider) => provider == "facebook" ? "accessToken" : "idToken";

        /// <summary>Ties this Google identity to the account of the player who is signed in.</summary>
        public static IEnumerator LinkGoogle(string idToken, Action<IdentityResult> done) => Link("google", idToken, done);

        private static IEnumerator Post(string path, string field, string idToken, bool withTicket, Action<IdentityResult> done)
        {
            string ticket = PlayFabSettings.staticPlayer?.ClientSessionTicket;
            if (string.IsNullOrEmpty(Base) || (withTicket && string.IsNullOrEmpty(ticket))) { done?.Invoke(new IdentityResult { Error = "network" }); yield break; }
            string text = null;
            using (var req = new UnityWebRequest(Base + path, "POST"))
            {
                // a Google JWT or a Facebook token only holds letters, digits, '-', '_' and '.', so nothing needs escaping; anything else is dropped
                var clean = new StringBuilder();
                foreach (char c in idToken ?? "") if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.') clean.Append(c);
                string json = "{\"" + field + "\":\"" + clean + "\"}";
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                if (withTicket) req.SetRequestHeader("X-Authorization", ticket);
                req.timeout = 20;
                yield return req.SendWebRequest();
                text = req.downloadHandler != null ? req.downloadHandler.text : null;
            }
            IdentityDto d = null;
            if (!string.IsNullOrEmpty(text)) { try { d = JsonUtility.FromJson<IdentityDto>(text); } catch { } }
            if (d == null) { done?.Invoke(new IdentityResult { Error = "network" }); yield break; }
            if (!d.ok) { done?.Invoke(new IdentityResult { Error = string.IsNullOrEmpty(d.error) ? "try_again" : d.error, ExistingName = d.name }); yield break; }
            done?.Invoke(new IdentityResult { Ok = true, Created = d.created, Already = d.already, SessionTicket = d.sessionTicket, PlayFabId = d.playFabId });
        }
    }
}
