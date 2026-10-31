using MafiaNight.Core;
using MafiaNight.Data.Player;
using MafiaNight.Services.Friends;
using MafiaNight.Services.Profile;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Settings screen: puts the player's real id number (the family number the support searches with) into the id strip and the version line,
    /// which the kit builds with a sample number. Until the server answers both show dots. Added by <see cref="KitAppBinder"/>.
    /// </summary>
    public sealed class AccountIdFlow : MonoBehaviour
    {
        private ArabicText idValue, versionLine, nameLabel, subLabel;
        private AvatarFrame avatar;
        private IPlayerProfileService profile;
        private FriendsService friends;

        private void Start()
        {
            var screen = FindAnyObjectByType<KitSettingsScreen>();
            if (screen == null) { enabled = false; return; }
            idValue = Find(screen.transform, "IdStrip/V");
            versionLine = Find(screen.transform, "Version/T");
            nameLabel = Find(screen.transform, "IdStrip/Name"); subLabel = Find(screen.transform, "IdStrip/Sub");
            foreach (var a in screen.GetComponentsInChildren<AvatarFrame>(true)) if (a.transform.parent != null && a.transform.parent.name == "IdStrip") avatar = a;
            if (ServiceLocator.TryGet(out profile)) { profile.OnProfileLoaded += OnProfile; profile.OnProfileUpdated += OnProfile; }
            friends = FriendsService.Ensure();
            friends.OnChanged += Paint;
            Paint();
            friends.Refresh();
        }

        private void OnDestroy()
        {
            if (friends != null) friends.OnChanged -= Paint;
            if (profile != null) { profile.OnProfileLoaded -= OnProfile; profile.OnProfileUpdated -= OnProfile; }
        }

        private void OnProfile(PlayerProfile _) => Paint();

        private void Paint()
        {
            string num = friends != null && friends.Me != null && !string.IsNullOrEmpty(friends.Me.Num) ? Ar(friends.Me.Num) : "······";
            if (idValue != null) idValue.Text = num;
            var p = profile != null ? profile.LocalProfile : null;
            if (p != null)
            {
                var step = RankLadder.FromPoints(p.RankPoints);
                if (nameLabel != null) KitText.Fit(nameLabel, string.IsNullOrEmpty(p.DisplayName) ? "لاعب" : p.DisplayName, 18, 34f);
                if (subLabel != null) subLabel.Text = KitTheme.RankNames[Mathf.Clamp(step.RankIndex, 0, KitTheme.RankNames.Length - 1)] + " · المستوى " + Ar(p.Level.ToString());
                if (avatar != null) avatar.Set(step.RankIndex);
            }
            if (versionLine != null) versionLine.Text = "المافيا · الإصدار " + Ar(Application.version.Replace('.', '٫')) + " · الحساب " + num;
        }

        private static ArabicText Find(Transform root, string path)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var hit = t.Find(path);
                if (hit != null) return hit.GetComponent<ArabicText>();
            }
            return null;
        }

        private static string Ar(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c >= '0' && c <= '9' ? (char)('٠' + (c - '0')) : c);
            return sb.ToString();
        }
    }
}
