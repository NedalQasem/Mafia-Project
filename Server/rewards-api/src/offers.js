'use strict';
// The offers of the banner that opens when the game starts. They live in config.json (section "offers"), so the owner changes what the
// banner promotes, its texts and its countdown without a new version of the game: edit the file, then  docker restart mafia-rewards.
// Each entry points at something the game already knows: "adId" (an ad of the game's store catalog, e.g. "ad_ember") or "itemId"
// (a store item). Texts left out keep the ones of the game. "until" (ISO date) makes the offer end by itself and drives the countdown.

const MAX = 3;

function createOffers({ cfg = {}, source = null, now = () => Date.now() }) {
  // from the admin board's own list when it has one (source), otherwise from config.json; read on every call so an edit applies at once
  const items = () => { const l = source ? source() : cfg.offers; return Array.isArray(l) ? l : []; };

  function text(v, max) { return typeof v === 'string' ? v.slice(0, max) : ''; }

  function active() {
    const t = now();
    const out = [];
    for (const o of items()) {
      if (!o || o.enabled === false) continue;
      const adId = text(o.adId, 64), itemId = text(o.itemId, 64);
      if (!adId && !itemId) continue;
      const from = o.from ? Date.parse(o.from) : 0, until = o.until ? Date.parse(o.until) : 0;
      if (from && from > t) continue;
      if (until && until <= t) continue;
      out.push({
        adId, itemId,
        mast: text(o.mast, 40), headline: text(o.headline, 80), body: text(o.body, 140), stamp: text(o.stamp, 12),
        mastEn: text(o.mastEn, 40), headlineEn: text(o.headlineEn, 80), bodyEn: text(o.bodyEn, 140), stampEn: text(o.stampEn, 12),
        endsInSec: until ? Math.max(0, Math.floor((until - t) / 1000)) : 0,
      });
      if (out.length === MAX) break;
    }
    return out;
  }

  return { active };
}

module.exports = { createOffers };
