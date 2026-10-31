using MafiaNight.Core;
using MafiaNight.Data.Player;
using MafiaNight.Services.Friends;
using MafiaNight.Services.Profile;
using MafiaNight.UI.Kit;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
            var pill = idValue != null && idValue.transform.parent != null ? idValue.transform.parent.Find("Pill") : null;
            if (pill != null)                       // a tap on the id number copies it (the support asks for it)
            {
                var pillImg = pill.GetComponent<Image>(); if (pillImg != null) pillImg.raycastTarget = true;
                var btn = pill.gameObject.AddComponent<Button>(); btn.targetGraphic = pillImg; btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(CopyId);
                var tmp = idValue.GetComponent<TMPro.TMP_Text>(); if (tmp != null && !tmp.enableAutoSizing) { tmp.fontSizeMax = tmp.fontSize; tmp.fontSizeMin = 22f; tmp.enableAutoSizing = true; }
            }
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

        private Coroutine copied;

        private void CopyId()
        {
            string num = friends != null && friends.Me != null ? friends.Me.Num : null;
            if (string.IsNullOrEmpty(num) || idValue == null) return;
            GUIUtility.systemCopyBuffer = num;
            idValue.Text = "تم النسخ";
            if (copied != null) StopCoroutine(copied);
            copied = StartCoroutine(ShowNumberAgain());
        }

        private IEnumerator ShowNumberAgain()
        {
            yield return new WaitForSecondsRealtime(1.4f);
            copied = null;
            Paint();
        }

        private void Paint()
        {
            string num = friends != null && friends.Me != null && !string.IsNullOrEmpty(friends.Me.Num) ? Ar(friends.Me.Num) : "······";
            if (idValue != null && copied == null) idValue.Text = num;
            var p = profile != null ? profile.LocalProfile : null;
            if (p != null)
            {
                var step = RankLadder.FromPoints(p.RankPoints);
                if (nameLabel != null) KitText.Fit(nameLabel, string.IsNullOrEmpty(p.DisplayName) ? "لاعب" : p.DisplayName, 18, 34f);
                if (subLabel != null) subLabel.Text = Loc.F("{0} · المستوى {1}", "{0} · Level {1}", Loc.T(KitTheme.RankNames[Mathf.Clamp(step.RankIndex, 0, KitTheme.RankNames.Length - 1)]), Ar(p.Level.ToString()));
                if (avatar != null) avatar.Set(step.RankIndex);
            }
            if (versionLine != null) versionLine.Text = Loc.F("المافيا · الإصدار {0} · الحساب {1}", "Mafia · Version {0} · Account {1}", Loc.English ? Application.version : Ar(Application.version.Replace('.', '٫')), num);
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
