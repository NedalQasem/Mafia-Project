using System.Collections;
using MafiaNight.Services.Config;
using MafiaNight.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.App
{
    /// <summary>
    /// Settings → الحساب: the "ربط الحساب" row opens <see cref="KitAccountSheet"/> with what the server says about this account (guest or linked, days left
    /// for a guest). Its office row opens the recovery sheet that already lives in the settings scene. The Google and Facebook rows come later.
    /// </summary>
    public sealed class AccountFlow : MonoBehaviour
    {
        private const string RowName = "Link_ربط الحساب (حفظ تقدمك)";
        private bool loading;

        private void Start()
        {
            Transform row = null;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t.name == RowName) { row = t; break; }
            if (row == null) { enabled = false; return; }
            var img = row.GetComponent<Image>(); if (img != null) img.raycastTarget = true;
            var btn = row.GetComponent<Button>(); if (btn == null) btn = row.gameObject.AddComponent<Button>();
            if (img != null) btn.targetGraphic = img;
            btn.onClick.AddListener(() => { if (!loading) StartCoroutine(Open()); });
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
                if (key != "office") return;
                var recovery = FindAnyObjectByType<KitRecoverySheet>(FindObjectsInactive.Include);
                if (recovery != null) { sheet.Close(); recovery.Open(); }
            };
            var data = new KitAccountSheet.Data();
            if (st != null) { data.guest = st.Guest; data.purge = st.Purge; data.daysLeft = st.DaysLeft; data.methods = st.Methods; }
            else data.guest = true;           // no answer from the server: the sheet still opens, as for a guest
            sheet.Show(data);
        }
    }
}
