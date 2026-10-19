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
    }
}
