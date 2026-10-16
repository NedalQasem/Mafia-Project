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
        string EOSAccountId { get; }
        string PlayFabId { get; }

        /// <summary>Raised once PlayFab login succeeds. Argument: PlayFab ID.</summary>
        event Action<string> OnPlayFabLoggedIn;

        /// <summary>Raised when EOS or PlayFab login fails. Argument: error message.</summary>
        event Action<string> OnLoginFailed;
    }
}
