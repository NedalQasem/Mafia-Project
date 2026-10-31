using MafiaNight.Core;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Rooms;
using MafiaNight.UI.Kit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MafiaNight.App
{
    /// <summary>
    /// The "how do you want to come in" screen (<see cref="KitLoginChoice"/>) on the very first start of the game: the device already holds a guest
    /// account by then, so "enter as a guest" just closes it, and "I have an office account" opens the recovery sheet. Players who already played
    /// before this screen existed never see it. Google and Facebook arrive with their own sign-in later and show "soon" until then.
    /// </summary>
    public sealed class LoginChoiceFlow : MonoBehaviour
    {
        public const string ChosenKey = "mafia.login.chosen";
        private IRoomService rooms;
        private float showAt;
        private bool shown;
        private KitLoginChoice screen;

        private void Start()
        {
            // The first-start choice now comes before the sign-in (LoginGateFlow), and Google / Facebook link from Settings, so this second screen with its "soon" tags is not shown any more.
            if (PlayerPrefs.GetInt(ChosenKey, 0) != 1) { PlayerPrefs.SetInt(ChosenKey, 1); PlayerPrefs.Save(); }
            Destroy(this); return;
            #pragma warning disable 0162
            if (SceneManager.GetActiveScene().name != AppScenes.Home || PlayerPrefs.GetInt(ChosenKey, 0) == 1) { Destroy(this); return; }
            if (PlayerPrefs.HasKey("mafia.launch.seen")) { PlayerPrefs.SetInt(ChosenKey, 1); PlayerPrefs.Save(); Destroy(this); return; }       // an old player: he is already in
            ServiceLocator.TryGet(out rooms);
            showAt = Time.unscaledTime + 0.8f;
            #pragma warning restore 0162
        }

        private void Update()
        {
            if (shown || Time.unscaledTime < showAt) return;
            if (rooms != null && rooms.InRoom) return;
            if (!ServiceLocator.TryGet<IAuthService>(out var auth) || !auth.IsPlayFabLoggedIn) return;
            Show();
        }

        private void Show()
        {
            var th = KitTheme.Current; var canvas = InviteFlow.FindCanvas();
            if (th == null || th.loginChoicePrefab == null || canvas == null) { Destroy(this); return; }
            shown = true;
            var go = Instantiate(th.loginChoicePrefab, canvas, false);
            go.transform.SetAsLastSibling();
            screen = go.GetComponent<KitLoginChoice>();
            screen.SetAvailable(false, false);
            screen.OnGuest += Choose;
            screen.OnOffice += OpenOffice;
        }

        private void Choose()
        {
            PlayerPrefs.SetInt(ChosenKey, 1); PlayerPrefs.Save();
            if (screen != null) screen.Close();
            Destroy(this);
        }

        private void OpenOffice()
        {
            var th = KitTheme.Current; var canvas = InviteFlow.FindCanvas();
            if (th == null || th.recoverySheetPrefab == null || canvas == null) return;
            var go = Instantiate(th.recoverySheetPrefab, canvas, false);
            go.transform.SetAsLastSibling();
            var sheet = go.GetComponent<KitRecoverySheet>();
            RecoveryFlow.Wire(sheet, this);
            sheet.Open();
        }
    }
}
