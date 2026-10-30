using MafiaNight.Services.Config;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Settings screen glue for the account recovery: the "get an old account back" row opens the sheet, the sheet sends the user name and password
    /// through RecoveryService, and after a success the game is closed so the next start signs in to the old account.
    /// </summary>
    public sealed class RecoveryFlow : MonoBehaviour
    {
        private KitRecoverySheet sheet;

        private void Start()
        {
            sheet = FindAnyObjectByType<KitRecoverySheet>(FindObjectsInactive.Include);
            if (sheet == null) { enabled = false; return; }
            if (sheet.Opener != null) sheet.Opener.onClick.AddListener(sheet.Open);
            sheet.OnSubmit += Submit;
            sheet.OnQuit += Quit;
        }

        private void OnDestroy()
        {
            if (sheet == null) return;
            if (sheet.Opener != null) sheet.Opener.onClick.RemoveListener(sheet.Open);
            sheet.OnSubmit -= Submit;
            sheet.OnQuit -= Quit;
        }

        private void Submit(string username, string password)
        {
            sheet.SetBusy(true);
            StartCoroutine(RecoveryService.Recover(username, password, r =>
            {
                if (sheet == null) return;
                if (r.Ok) sheet.ShowDone("رجع حسابك! سكّر اللعبة وافتحها من جديد.");
                else sheet.ShowMessage(ErrorText(r.Error));
            }));
        }

        private static string ErrorText(string code)
        {
            switch (code)
            {
                case "bad_login": return "اسم المستخدم أو كلمة السر غلط.";
                case "locked": return "محاولات كثيرة. استنى ربع ساعة وجرّب.";
                case "try_again": return "السيرفر مش جاهز هلأ، جرّب بعد شوي.";
                case "no_identity": return "استنى لحد ما يخلص تسجيل الدخول وجرّب.";
                case "link_failed": return "ما قدرنا نربط الجهاز بحسابك، جرّب كمان مرة.";
                default: return "ما في اتصال بالسيرفر.";
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
