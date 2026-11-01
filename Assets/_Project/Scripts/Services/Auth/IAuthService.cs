using System;

namespace MafiaNight.Services.Auth
{
    /// <summary>
    /// Login pipeline: EOS (identity) → PlayFab (backend account).
    /// </summary>
    public interface IAuthService
    {
        bool IsEOSLoggedIn { get; }
        bool IsPlayFabLoggedIn { get; }
        /// <summary>EOS Product User ID (PUID) - the player's identity for Lobbies, P2P and voice.</summary>
        string ProductUserId { get; }
        string PlayFabId { get; }

        /// <summary>Raised once PlayFab login succeeds. Argument: PlayFab ID.</summary>
        event Action<string> OnPlayFabLoggedIn;

        /// <summary>Raised when EOS or PlayFab login fails. Argument: error message.</summary>
        event Action<string> OnLoginFailed;

        /// <summary>Tries the login again from the step that failed (EOS first, then PlayFab).</summary>
        void RetryLogin();

        /// <summary>
        /// Account recovery, last step: with the session of the OLD account (from the recovery login), ties this device's EOS identity to that account,
        /// so every next sign-in on this device lands on it. done(true, "") on success; done(false, reason) otherwise, and the current session stays as it was.
        /// </summary>
        void LinkDeviceToAccount(string oldAccountSessionTicket, Action<bool, string> done);
    }
}
