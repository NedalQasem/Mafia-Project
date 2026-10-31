using System;
using System.Collections;
using System.Text;
using MafiaNight.Core;
using PlayFab;
using UnityEngine;
using UnityEngine.Networking;

namespace MafiaNight.Services.Config
{
    /// <summary>What kind of account the player has: a guest (only the device holds it) or a linked one, and the days a guest has left.</summary>
    public sealed class AccountStatus
    {
        public bool Guest, Purge;
        public int DaysLeft;
        public string[] Methods = new string[0];
    }

    [Serializable] internal sealed class AccountDto { public bool ok, guest, purge; public int daysLeft; public string[] methods; }

    /// <summary>Asks the rewards server (Server/rewards-api, /v1/account) what kind of account this player has.</summary>
    public static class AccountService
    {
        private static string Base => (GameConstants.ApiUrl ?? "").TrimEnd('/');

        /// <summary>The callback gets null when the server cannot be reached.</summary>
        public static IEnumerator Fetch(Action<AccountStatus> done)
        {
            string ticket = PlayFabSettings.staticPlayer?.ClientSessionTicket;
            if (string.IsNullOrEmpty(Base) || string.IsNullOrEmpty(ticket)) { done?.Invoke(null); yield break; }
            string text = null;
            using (var req = new UnityWebRequest(Base + "/v1/account", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Authorization", ticket);
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) text = req.downloadHandler.text;
            }
            AccountDto d = null;
            if (!string.IsNullOrEmpty(text)) { try { d = JsonUtility.FromJson<AccountDto>(text); } catch { } }
            if (d == null || !d.ok) { done?.Invoke(null); yield break; }
            done?.Invoke(new AccountStatus { Guest = d.guest, Purge = d.purge, DaysLeft = d.daysLeft, Methods = d.methods ?? new string[0] });
        }
    }
}
