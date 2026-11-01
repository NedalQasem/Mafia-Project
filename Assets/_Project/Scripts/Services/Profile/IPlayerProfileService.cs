using System;
using MafiaNight.Data.Player;

namespace MafiaNight.Services.Profile
{
    /// <summary>
    /// Local player profile (level, rank, stats, cosmetics) persisted in PlayFab User Data.
    /// </summary>
    public interface IPlayerProfileService
    {
        PlayerProfile LocalProfile { get; }
        PlayerPublicData PublicData { get; }
        bool HasProfile { get; }
        bool IsLoading { get; }
        bool IsSaving { get; }
        /// <summary>True when the profile was just created because the server had none (first launch of this account).</summary>
        bool IsNewProfile { get; }

        event Action<PlayerProfile> OnProfileLoaded;
        event Action<PlayerProfile> OnProfileUpdated;
        event Action<string> OnLoadError;
        event Action OnProfileSaved;

        void LoadProfile(string playFabId);
        void RefreshProfile();
        void SaveProfile();

        void UpdateDisplayName(string newName, Action<bool> callback = null);
        bool AddXP(int amount);

        /// <summary>Applies rank points + statistics for a finished match and saves.</summary>
        // TODO(security): match results must be validated server-side before going live.
        void RecordMatchResult(bool won, bool wasMafia, bool isMvp);

        /// <summary>Wears a store item in its category (null itemId takes it off), saves, and fires OnProfileUpdated.</summary>
        void Equip(string categoryId, string itemId);

        void SetActiveFrame(string frameID);
        void SetActiveChatBackground(string bgID);
    }
}
