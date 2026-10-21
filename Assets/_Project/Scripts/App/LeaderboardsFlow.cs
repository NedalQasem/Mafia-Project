using MafiaNight.Services.Leaderboard;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>Gives the leaderboards screen its real numbers from our server (the server writes them while it pays finished matches).</summary>
    public sealed class LeaderboardsFlow : MonoBehaviour
    {
        private KitLeaderboardsScreen screen;
        private LeaderboardService service;
        private KitToast toast;

        private void Start()
        {
            screen = FindAnyObjectByType<KitLeaderboardsScreen>(FindObjectsInactive.Include);
            if (screen == null) { Destroy(this); return; }
            service = LeaderboardService.Ensure();
            toast = FindAnyObjectByType<KitToast>(FindObjectsInactive.Include);
            screen.OnLoadBoard += LoadBoard;
            screen.OnLoadWall += LoadWall;
            screen.Reload();       // the screen may have asked before this flow was listening
        }

        private void OnDestroy()
        {
            if (screen == null) return;
            screen.OnLoadBoard -= LoadBoard;
            screen.OnLoadWall -= LoadWall;
        }

        private void LoadBoard(int period, int cat)
        {
            service.LoadBoard(period, cat, r =>
            {
                if (screen == null) return;
                if (!r.Ok) { screen.ShowBoard(period, cat, new LeaderBoardView { failed = true }); return; }
                var v = new LeaderBoardView { resetInSeconds = r.ResetInSeconds, mePosition = r.MePosition, need = r.Need };
                foreach (var x in r.Top) v.list.Add(new LeaderEntry { name = x.Name, rank = x.Rank, value = x.Value, me = x.Me });
                if (r.Me != null) v.me = new LeaderEntry { name = r.Me.Name + " (أنت)", rank = r.Me.Rank, value = r.Me.Value, me = true };
                v.fill = r.Need == 0 ? 1f : (r.Tenth > 0 && r.Me != null ? (float)r.Me.Value / r.Tenth : 0.05f);
                screen.ShowBoard(period, cat, v);
            });
        }

        private void LoadWall()
        {
            service.LoadWall(r =>
            {
                if (screen == null) return;
                var v = new LeaderWallView();
                if (r.Ok)
                {
                    if (r.GodfatherName != null) v.godfather = new LeaderWallItem { name = r.GodfatherName, value = CoinPill.Format(r.GodfatherPoints) };
                    v.records[0] = Item(r.Streak); v.records[1] = Item(r.Games); v.records[2] = Item(r.Hours);
                    v.records[3] = Item(r.MafiaWins); v.records[4] = Item(r.SheriffWins); v.records[5] = Item(r.Survived);
                }
                else if (toast != null) toast.Show("تعذّر تحميل قاعة المشاهير. تأكد من الاتصال");
                screen.ShowWall(v);
            });
        }

        private static LeaderWallItem Item(WallRecord r) => r == null ? null : new LeaderWallItem { name = r.Name, value = CoinPill.Format(r.Value) };
    }
}
