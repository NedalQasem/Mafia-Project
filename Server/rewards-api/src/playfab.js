'use strict';
// The few PlayFab Server API calls the service needs. The secret key never leaves this process.

class PlayFabError extends Error {
  constructor(message, status, code) { super(message); this.status = status; this.code = code; }
}

function createPlayFab({ titleId, secretKey, baseUrl, fetchImpl }) {
  const f = fetchImpl || fetch;
  const root = (baseUrl || `https://${titleId}.playfabapi.com`).replace(/\/$/, '');

  async function call(path, body) {
    let res;
    try {
      res = await f(root + path, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-SecretKey': secretKey },
        body: JSON.stringify(body),
        signal: AbortSignal.timeout(10000),
      });
    } catch (e) {
      throw new PlayFabError('PlayFab unreachable: ' + e.message, 503, 'Unreachable');
    }
    let json = null;
    try { json = await res.json(); } catch { /* not json */ }
    if (!res.ok || !json || json.code !== 200) {
      throw new PlayFabError((json && (json.errorMessage || json.error)) || 'PlayFab error ' + res.status, res.status, json && json.error);
    }
    return json.data;
  }

  return {
    /** Turns a session ticket into the PlayFab id of its owner (throws when the ticket is not valid). */
    async authenticate(sessionTicket) {
      const d = await call('/Server/AuthenticateSessionTicket', { SessionTicket: sessionTicket });
      const id = d && d.UserInfo && d.UserInfo.PlayFabId;
      if (!id) throw new PlayFabError('no user', 401, 'NoUser');
      return id;
    },
    /** The player's display name, or null when he has none. */
    async getDisplayName(playFabId) {
      const d = await call('/Server/GetPlayerProfile', { PlayFabId: playFabId, ProfileConstraints: { ShowDisplayName: true } });
      return (d && d.PlayerProfile && d.PlayerProfile.DisplayName) || null;
    },
    async getProgress(playFabId, key) {
      const d = await call('/Server/GetUserReadOnlyData', { PlayFabId: playFabId, Keys: [key] });
      const entry = d && d.Data && d.Data[key];
      if (!entry || !entry.Value) return null;
      try { return JSON.parse(entry.Value); } catch { return null; }
    },
    async setProgress(playFabId, key, value) {
      await call('/Server/UpdateUserReadOnlyData', { PlayFabId: playFabId, Data: { [key]: JSON.stringify(value) }, Permission: 'Public' });
    },
    async addCurrency(playFabId, code, amount) {
      await call('/Server/AddUserVirtualCurrency', { PlayFabId: playFabId, VirtualCurrency: code, Amount: amount });
    },
  };
}

module.exports = { createPlayFab, PlayFabError };
