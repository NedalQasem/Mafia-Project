using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MafiaNight.Core;
using PlayFab;
using UnityEngine;
using UnityEngine.Networking;

namespace MafiaNight.Services.Config
{
    /// <summary>Picks the text of the player's language out of an Arabic / English pair the server sends (Arabic is the fallback).</summary>
    public static class Localized
    {
        public static bool English => Application.systemLanguage == SystemLanguage.English;
        public static string Pick(string ar, string en)
        {
            bool hasAr = !string.IsNullOrEmpty(ar), hasEn = !string.IsNullOrEmpty(en);
            if (English && hasEn) return en;
            return hasAr ? ar : (hasEn ? en : "");
        }
    }

    /// <summary>One telegram of the mail: a title, a text and usually a package the player collects.</summary>
    public sealed class MailItem
    {
        public string Id, Title, Body;
        public int Coins, Gems;
        public string[] Items = new string[0];
        public long Ts, Exp, Claimed;        // milliseconds since 1970 (server clock); Exp and Claimed 0 = never / not yet. For a message, Claimed is the read time
        public string Kind = "", ImageId = "";     // a private message: "note" or "warn", maybe with a picture; "" = an ordinary telegram
        public bool Must;                    // a warning the player has to read before going on
        public bool IsMessage => Kind == "note" || Kind == "warn";
        public bool HasPack => Coins > 0 || Gems > 0 || Items.Length > 0;
    }

    public sealed class MailBoxData
    {
        public List<MailItem> Items = new List<MailItem>();
        public int Unread;
    }

    public sealed class MailClaimResult { public bool Ok; public int Coins, Gems; public string[] Items = new string[0]; public int Claimed; public string Error; }

    [Serializable] internal sealed class LangDto { public string ar, en; }
    [Serializable] internal sealed class PackDto { public int coins, gems; public string[] items; }
    [Serializable] internal sealed class MailItemDto { public string id, kind, image; public bool must; public long ts, exp, claimed; public LangDto title, body; public PackDto pack; }
    [Serializable] internal sealed class MailDto { public bool ok; public int unread; public List<MailItemDto> items; }
    [Serializable] internal sealed class MailClaimDto { public bool ok; public int coins, gems, claimed; public string[] items; public string error; }

    /// <summary>
    /// The in-game mail (Server/rewards-api, "البريد"): the telegrams the admin sends, with their packages. The server pays the package itself when
    /// the player claims it; the game only reads and asks.
    /// </summary>
    public static class MailService
    {
        private static string Base => (GameConstants.ApiUrl ?? "").TrimEnd('/');
        private static string Ticket => PlayFabSettings.staticPlayer?.ClientSessionTicket;

        /// <summary>The callback gets null when the server cannot be reached.</summary>
        public static IEnumerator Fetch(Action<MailBoxData> done)
        {
            string text = null;
            yield return Post("/v1/mail", "{}", (ok, t) => text = ok ? t : null);
            MailDto d = null;
            if (text != null) { try { d = JsonUtility.FromJson<MailDto>(text); } catch { } }
            if (d == null || !d.ok) { done?.Invoke(null); yield break; }
            var box = new MailBoxData { Unread = d.unread };
            if (d.items != null)
                foreach (var m in d.items)
                    box.Items.Add(new MailItem
                    {
                        Id = m.id, Ts = m.ts, Exp = m.exp, Claimed = m.claimed,
                        Title = Localized.Pick(m.title?.ar, m.title?.en), Body = Localized.Pick(m.body?.ar, m.body?.en),
                        Kind = m.kind ?? "", ImageId = m.image ?? "", Must = m.must,
                        Coins = m.pack != null ? m.pack.coins : 0, Gems = m.pack != null ? m.pack.gems : 0, Items = m.pack?.items ?? new string[0],
                    });
            done?.Invoke(box);
        }

        public static IEnumerator Claim(string id, Action<MailClaimResult> done)
        {
            string text = null;
            yield return Post("/v1/mail/claim", "{\"id\":\"" + Clean(id) + "\"}", (ok, t) => text = t);
            done?.Invoke(Parse(text));
        }

        /// <summary>The player typed a promo code: the package arrives as a telegram. The result carries the reason when it failed (bad_code, already_used, locked).</summary>
        public static IEnumerator Redeem(string code, Action<MailClaimResult> done)
        {
            string text = null;
            yield return Post("/v1/codes/redeem", "{\"code\":\"" + Clean(code) + "\"}", (ok, t) => text = t);
            done?.Invoke(Parse(text));
        }

        /// <summary>The player read a private message: the server remembers it (the admin sees it), and it stops counting as new.</summary>
        public static IEnumerator Read(string id, Action<bool> done)
        {
            string text = null;
            yield return Post("/v1/mail/read", "{\"id\":\"" + Clean(id) + "\"}", (ok, t) => text = t);
            var r = Parse(text);
            done?.Invoke(r.Ok);
        }

        public static string ImageUrl(string imageId) => string.IsNullOrEmpty(imageId) ? null : Base + "/v1/img/" + imageId;

        private static readonly Dictionary<string, Texture2D> images = new Dictionary<string, Texture2D>();

        /// <summary>Loads the picture of a message once per run (the picture name is random and never changes). The callback gets null when it fails.</summary>
        public static IEnumerator LoadImage(string imageId, Action<Texture2D> done)
        {
            string url = ImageUrl(imageId);
            if (url == null) { done?.Invoke(null); yield break; }
            if (images.TryGetValue(imageId, out var cached) && cached != null) { done?.Invoke(cached); yield break; }
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { done?.Invoke(null); yield break; }
                var tex = DownloadHandlerTexture.GetContent(req);
                images[imageId] = tex;
                done?.Invoke(tex);
            }
        }

        public static IEnumerator ClaimAll(Action<MailClaimResult> done)
        {
            string text = null;
            yield return Post("/v1/mail/claim-all", "{}", (ok, t) => text = t);
            done?.Invoke(Parse(text));
        }

        private static string Clean(string s) => (s ?? "").Replace("\\", "").Replace("\"", "");

        private static MailClaimResult Parse(string text)
        {
            MailClaimDto d = null;
            if (!string.IsNullOrEmpty(text)) { try { d = JsonUtility.FromJson<MailClaimDto>(text); } catch { } }
            if (d == null || !d.ok) return new MailClaimResult { Ok = false, Error = d != null ? d.error : "network" };
            return new MailClaimResult { Ok = true, Coins = d.coins, Gems = d.gems, Items = d.items ?? new string[0], Claimed = d.claimed };
        }

        // done(ok, body): the body is also given for an error status, so a refused claim can still be read
        private static IEnumerator Post(string path, string json, Action<bool, string> done)
        {
            string ticket = Ticket;
            if (string.IsNullOrEmpty(Base) || string.IsNullOrEmpty(ticket)) { done?.Invoke(false, null); yield break; }
            using (var req = new UnityWebRequest(Base + path, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Authorization", ticket);
                req.timeout = 10;
                yield return req.SendWebRequest();
                done?.Invoke(req.result == UnityWebRequest.Result.Success, req.downloadHandler != null ? req.downloadHandler.text : null);
            }
        }
    }
}
