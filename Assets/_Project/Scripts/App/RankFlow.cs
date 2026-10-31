using MafiaNight.Core;
using MafiaNight.Data.Player;
using MafiaNight.Services.Friends;
using MafiaNight.Services.Profile;
using MafiaNight.Services.Scenes;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Fills "my rank" (KitRankScreen) with the player's name, his account number and his rank points, and keeps it fresh when the profile
    /// changes or arrives late. "See it in the store" opens the store. Added by <see cref="KitAppBinder"/> in the rank scene.
    /// </summary>
    public sealed class RankFlow : MonoBehaviour
    {
        private KitRankScreen screen;
        private IPlayerProfileService profile;
        private ISceneLoader scenes;
        private FriendsService friends;

        private void Start()
        {
            screen = FindAnyObjectByType<KitRankScreen>(FindObjectsInactive.Include);
            if (screen == null || !ServiceLocator.TryGet(out profile)) { Destroy(this); return; }
            ServiceLocator.TryGet(out scenes);
            friends = FriendsService.Ensure();
            screen.OnStore += () => { if (scenes != null) scenes.LoadScene(AppScenes.Store); };
            profile.OnProfileLoaded += OnProfile;
            profile.OnProfileUpdated += OnProfile;
            friends.OnChanged += Refresh;
            Refresh();
            friends.Refresh();
        }

        private void OnDestroy()
        {
            if (profile != null) { profile.OnProfileLoaded -= OnProfile; profile.OnProfileUpdated -= OnProfile; }
            if (friends != null) friends.OnChanged -= Refresh;
        }

        private void OnProfile(PlayerProfile _) => Refresh();

        private void Refresh()
        {
            var p = profile.LocalProfile;
            string name = p != null && !string.IsNullOrEmpty(p.DisplayName) ? p.DisplayName : "لاعب";
            string number = friends != null && friends.Me != null && !string.IsNullOrEmpty(friends.Me.Num) ? friends.Me.Num : "";
            screen.Show(name, number, p != null ? p.RankPoints : 0);
        }
    }
}
