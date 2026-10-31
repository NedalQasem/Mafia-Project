using System.Collections;
using MafiaNight.Core;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Config;
using MafiaNight.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.App
{
    /// <summary>
    /// Settings → الحساب: the "ربط الحساب" row opens <see cref="KitAccountSheet"/> with what the server says about this account (guest or linked, days left
    /// for a guest). Its Google row signs in with Google and ties that identity to this account; if the identity already has an account, a question
    /// (<see cref="KitConflictSheet"/>) lets the player open that account instead. Its office row opens the recovery sheet of the settings scene.
    /// </summary>
    public sealed class AccountFlow : MonoBehaviour
    {
        private const string RowName = "Link_ربط الحساب (حفظ تقدمك)";
        private const string SupportRow = "Link_تواصل مع الدعم";
        private bool loading, working;

        private void Start()
        {
            SetupSupportRow();
            Transform row = null;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t.name == RowName) { row = t; break; }
            if (row == null) { enabled = false; return; }
            var img = row.GetComponent<Image>(); if (img != null) img.raycastTarget = true;
            var btn = row.GetComponent<Button>(); if (btn == null) btn = row.gameObject.AddComponent<Button>();
            if (img != null) btn.targetGraphic = img;
            btn.onClick.AddListener(() => { if (!loading) StartCoroutine(Open()); });
        }

        // the help row shows the support address (the only one players see) and copies it when tapped
        private void SetupSupportRow()
        {
            Transform row = null;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t.name == SupportRow) { row = t; break; }
            var title = row != null ? row.Find("T") : null;
            if (title == null) return;
            var e = row.Find("E");
            if (e == null)
            {
                var clone = Instantiate(title.gameObject, row); clone.name = "E"; e = clone.transform;
                var r = (RectTransform)e; r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f); r.pivot = new Vector2(0f, 0.5f); r.anchoredPosition = new Vector2(96f, 0f); r.sizeDelta = new Vector2(480f, 60f);
                var tm = clone.GetComponent<TMP_Text>(); tm.alignment = TextAlignmentOptions.MidlineLeft; tm.fontSize = 24f; tm.color = new Color32(0xB9, 0xA9, 0x8A, 255);
            }
            var label = e.GetComponent<ArabicText>(); label.Text = GameConstants.SUPPORT_EMAIL;
            var img = row.GetComponent<Image>(); if (img != null) img.raycastTarget = true;
            var btn = row.GetComponent<Button>(); if (btn == null) btn = row.gameObject.AddComponent<Button>();
            if (img != null) btn.targetGraphic = img;
            btn.onClick.AddListener(() => { GUIUtility.systemCopyBuffer = GameConstants.SUPPORT_EMAIL; label.Text = "اننسخ العنوان"; StartCoroutine(RestoreLabel(label)); });
        }

        private IEnumerator RestoreLabel(ArabicText label)
        {
            yield return new WaitForSecondsRealtime(1.6f);
            if (label != null) label.Text = GameConstants.SUPPORT_EMAIL;
        }

        private static KitAccountSheet.Data ToData(AccountStatus st)
        {
            var data = new KitAccountSheet.Data { ready = GoogleSignIn.Available ? new[] { "google" } : new string[0] };
            if (st != null) { data.guest = st.Guest; data.purge = st.Purge; data.daysLeft = st.DaysLeft; data.methods = st.Methods; }
            else data.guest = true;           // no answer from the server: the sheet still opens, as for a guest
            return data;
        }

        private IEnumerator Open()
        {
            loading = true;
            AccountStatus st = null;
            yield return AccountService.Fetch(s => st = s);
            loading = false;
            var th = KitTheme.Current; var canvas = InviteFlow.FindCanvas();
            if (th == null || th.accountSheetPrefab == null || canvas == null) yield break;
            var go = Instantiate(th.accountSheetPrefab, canvas, false);
            go.transform.SetAsLastSibling();
            var sheet = go.GetComponent<KitAccountSheet>();
            sheet.OnRow += key =>
            {
                if (key == "office")
                {
                    var recovery = FindAnyObjectByType<KitRecoverySheet>(FindObjectsInactive.Include);
                    if (recovery != null) { sheet.Close(); recovery.Open(); }
                }
                else if (key == "google" && !working) StartCoroutine(LinkGoogle(sheet));
            };
            sheet.Show(ToData(st));
        }

        private IEnumerator LinkGoogle(KitAccountSheet sheet)
        {
            working = true;
            string token = null, why = null;
            yield return GoogleSignIn.SignIn((t, e) => { token = t; why = e; });
            if (token == null) { working = false; yield break; }              // cancelled or failed: the sheet stays as it was
            IdentityResult res = null;
            yield return IdentityService.LinkGoogle(token, r => res = r);
            if (res != null && res.Ok)
            {
                AccountStatus st = null;
                yield return AccountService.Fetch(s => st = s);
                if (sheet != null) sheet.Show(ToData(st));
                working = false; yield break;
            }
            if (res != null && res.Error == "account_exists") { ShowConflict(token, res.ExistingName); yield break; }
            working = false;
        }

        private void ShowConflict(string token, string existingName)
        {
            var th = KitTheme.Current; var canvas = InviteFlow.FindCanvas();
            if (th == null || th.conflictSheetPrefab == null || canvas == null) { working = false; return; }
            var go = Instantiate(th.conflictSheetPrefab, canvas, false);
            go.transform.SetAsLastSibling();
            var conflict = go.GetComponent<KitConflictSheet>();
            conflict.OnKeep += () => working = false;
            conflict.OnQuit += () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            };
            conflict.OnOpenOld += () => StartCoroutine(OpenOld(conflict, token));
            conflict.Show(existingName, "Google");
        }

        private IEnumerator OpenOld(KitConflictSheet conflict, string token)
        {
            conflict.SetBusy(true);
            IdentityResult res = null;
            yield return IdentityService.LoginGoogle(token, r => res = r);
            if (res == null || !res.Ok) { conflict.ShowMessage(LoginGateFlow.ServerReason(res != null ? res.Error : "network")); yield break; }
            ServiceLocator.TryGet<IAuthService>(out var auth);
            bool linked = false, ok = false;
            if (auth != null) auth.LinkDeviceToAccount(res.SessionTicket, (o, _) => { ok = o; linked = true; }); else linked = true;
            while (!linked) yield return null;
            if (!ok) { conflict.ShowMessage("ما قدرنا نربط الجهاز بحسابك، جرّب كمان مرة."); yield break; }
            conflict.ShowDone("رجع حسابك! سكّر اللعبة وافتحها من جديد.");
        }
    }
}
