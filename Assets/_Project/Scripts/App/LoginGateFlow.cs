using System.Collections;
using MafiaNight.Services.Auth;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// The first-start choice screen shown over the loading screen, before the game signs in (see <see cref="LoginGate"/>). "Guest" lifts the hold and the
    /// loading goes on; "office account" lifts the hold too (the new device needs its identity to take the old account) and opens the recovery sheet, and
    /// the loading waits behind the choice screen until the player either recovers (the game closes) or goes back and enters as a guest.
    /// </summary>
    public static class LoginGateFlow
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() { LoginGate.Screen = Run; }

        private static IEnumerator Run(MonoBehaviour host)
        {
            var th = KitTheme.Current;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (th == null || th.loginChoicePrefab == null || canvas == null) { LoginGate.Release(); yield break; }
            var root = canvas.rootCanvas.transform;
            var go = Object.Instantiate(th.loginChoicePrefab, root, false);
            go.transform.SetAsLastSibling();
            var screen = go.GetComponent<KitLoginChoice>();
            screen.SetAvailable(false, false);
            bool guest = false;
            screen.OnGuest += () => guest = true;
            screen.OnOffice += () =>
            {
                LoginGate.Release();
                if (th.recoverySheetPrefab == null) return;
                var sheetGo = Object.Instantiate(th.recoverySheetPrefab, root, false);
                sheetGo.transform.SetAsLastSibling();
                var sheet = sheetGo.GetComponent<KitRecoverySheet>();
                RecoveryFlow.Wire(sheet, host);
                sheet.Open();
            };
            while (!guest) yield return null;
            PlayerPrefs.SetInt(LoginGate.ChosenKey, 1); PlayerPrefs.Save();
            LoginGate.Release();
            screen.Close();
        }
    }
}
