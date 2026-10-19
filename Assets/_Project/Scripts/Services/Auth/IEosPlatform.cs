using System;
using Epic.OnlineServices;
using Epic.OnlineServices.Platform;

namespace MafiaNight.Services.Auth
{
    /// <summary>
    /// The EOS platform as a service: owns the <see cref="PlatformInterface"/> and the local Product User ID (PUID).
    /// Rooms, voice and match transport depend on this contract, never on the MonoBehaviour that implements it.
    /// </summary>
    public interface IEosPlatform
    {
        /// <summary>The SDK handle (null until the platform was created).</summary>
        PlatformInterface Platform { get; }

        /// <summary>The logged-in player's EOS Product User ID (null / invalid until login succeeds).</summary>
        ProductUserId LocalUserId { get; }

        bool IsLoggedIn { get; }

        /// <summary>Raised on the first successful login (not on silent token refreshes).</summary>
        event Action<ProductUserId> OnLoggedIn;

        event Action<Result> OnLoginFailed;

        /// <summary>Retries the login (and the platform creation if it failed).</summary>
        void StartLogin();

        /// <summary>The Connect ID token (JWT), e.g. for PlayFab OpenID Connect.</summary>
        bool TryGetIdToken(out string jwt);
    }
}
