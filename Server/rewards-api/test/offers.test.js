'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createOffers } = require('../src/offers');

const T0 = Date.UTC(2026, 9, 30, 12, 0, 0);

test('only enabled offers inside their dates are served, at most three, with the seconds left', () => {
  const cfg = { offers: [
    { adId: 'ad_ember', until: '2026-10-30T14:00:00Z', stamp: '−٣٠٪' },
    { itemId: 'chat_port', enabled: false },
    { adId: 'ad_old', until: '2026-10-30T11:00:00Z' },
    { adId: 'ad_future', from: '2026-11-01T00:00:00Z' },
    { itemId: 'pack_chest', mast: 'عرض الكوينز' },
    { adId: 'ad_c' }, { adId: 'ad_d' },
    { headline: 'points at nothing' },
  ] };
  const list = createOffers({ cfg, now: () => T0 }).active();
  assert.deepEqual(list.map(o => o.adId || o.itemId), ['ad_ember', 'pack_chest', 'ad_c']);
  assert.equal(list[0].endsInSec, 7200);
  assert.equal(list[1].endsInSec, 0);
  assert.equal(list[0].stamp, '−٣٠٪'); assert.equal(list[1].mast, 'عرض الكوينز');
});

test('no offers in the config means an empty list, and long texts are cut', () => {
  assert.deepEqual(createOffers({ cfg: {}, now: () => T0 }).active(), []);
  const o = createOffers({ cfg: { offers: [{ adId: 'a', headline: 'x'.repeat(500), body: 5 }] }, now: () => T0 }).active()[0];
  assert.equal(o.headline.length, 80); assert.equal(o.body, '');
});
