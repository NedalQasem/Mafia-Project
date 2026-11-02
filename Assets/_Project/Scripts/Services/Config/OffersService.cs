using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MafiaNight.Core;
using PlayFab;
using UnityEngine;
using UnityEngine.Networking;

using MafiaNight.Services.Net;

namespace MafiaNight.Services.Config
{
    /// <summary>One offer the server wants the launch banner to promote. It points at something the game already knows (an ad of the store catalog or a store item).</summary>
    public sealed class ServerOffer
    {
        public string AdId, ItemId, Mast, Headline, Body, Stamp, Image;      // Image = the id of a picture uploaded on the admin board ("" = none)
        public int EndsInSeconds;
    }

    [Serializable] internal sealed class OfferDto { public string adId, itemId, image, mast, headline, body, stamp, mastEn, headlineEn, bodyEn, stampEn; public int endsInSec; }
    [Serializable] internal sealed class OffersDto { public bool ok; public List<OfferDto> offers; }

    /// <summary>
    /// Asks our server which offers the launch banner shows (Server/rewards-api, section "offers" of config.json), so they can change without a new
    /// version of the game. The callback gets null when the server cannot be reached, and an empty list when it has no offers: either way the
    /// game falls back to the ads of its own store catalog.
    /// </summary>
    public static class OffersService
    {
        public static IEnumerator Fetch(Action<List<ServerOffer>> done)
        {
            string baseUrl = (GameConstants.ApiUrl ?? "").TrimEnd('/');
            string ticket = PlayFabSettings.staticPlayer?.ClientSessionTicket;
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(ticket)) { done?.Invoke(null); yield break; }
            var net = new NetResult();
            yield return Http.PostJson(baseUrl + "/v1/offers", "{}", ticket, 5, net, read: true);
            {
                OffersDto d = null;
                if (net.Success) { try { d = JsonUtility.FromJson<OffersDto>(net.Text); } catch { } }
                if (d == null || !d.ok) { done?.Invoke(null); yield break; }
                var list = new List<ServerOffer>();
                if (d.offers != null)
                    foreach (var o in d.offers)
                        list.Add(new ServerOffer { AdId = o.adId, ItemId = o.itemId, Image = o.image, Mast = Localized.Pick(o.mast, o.mastEn), Headline = Localized.Pick(o.headline, o.headlineEn), Body = Localized.Pick(o.body, o.bodyEn), Stamp = Localized.Pick(o.stamp, o.stampEn), EndsInSeconds = o.endsInSec });
                done?.Invoke(list);
            }
        }
    }
}
