using System;
using System.Collections;
using System.Text;
using MafiaNight.Core;
using MafiaNight.Services.Auth;
using UnityEngine;
using UnityEngine.Networking;

using MafiaNight.Services.Net;

namespace MafiaNight.Services.Config
{
    public sealed class RecoveryResult
    {
        public bool Ok;
        /// <summary>When it failed: bad_login, locked, try_again, network, no_identity or link_failed.</summary>
        public string Error;
    }

    [Serializable] internal sealed class RecoveryDto { public bool ok; public string sessionTicket, playFabId, error; }

    /// <summary>
    /// Account recovery (Server/rewards-api): the player types the user name and password the support gave him; the server answers with a session of
    /// the old account and this device is tied to it. The game has to be opened again afterwards, so the account is loaded from the start.
    /// </summary>
    public static class RecoveryService
    {
        private static string Base => (GameConstants.ApiUrl ?? "").TrimEnd('/');

        public static IEnumerator Recover(string username, string password, Action<RecoveryResult> done)
        {
            if (string.IsNullOrEmpty(Base) || !ServiceLocator.TryGet<IAuthService>(out var auth)) { done?.Invoke(new RecoveryResult { Error = "network" }); yield break; }

            string text = null;
            {
                string json = "{\"username\":\"" + Escape(username) + "\",\"password\":\"" + Escape(password) + "\"}";
                var net = new NetResult();
                yield return Http.PostJson(Base + "/v1/recover/login", json, null, 15, net);
                text = net.Text;
            }

            RecoveryDto d = null;
            if (!string.IsNullOrEmpty(text)) { try { d = JsonUtility.FromJson<RecoveryDto>(text); } catch { } }
            if (d == null) { done?.Invoke(new RecoveryResult { Error = "network" }); yield break; }
            if (!d.ok || string.IsNullOrEmpty(d.sessionTicket)) { done?.Invoke(new RecoveryResult { Error = string.IsNullOrEmpty(d.error) ? "bad_login" : d.error }); yield break; }

            RecoveryResult result = null;
            auth.LinkDeviceToAccount(d.sessionTicket, (ok, reason) => result = new RecoveryResult { Ok = ok, Error = ok ? null : reason });
            while (result == null) yield return null;
            done?.Invoke(result);
        }

        // passwords may hold quotes and backslashes, so they are escaped rather than cleaned
        private static string Escape(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.Append(' ');
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
