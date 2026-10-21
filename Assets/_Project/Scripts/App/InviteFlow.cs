using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.Services.Friends;
using MafiaNight.Services.Match;
using MafiaNight.Services.Scenes;
using MafiaNight.Services.Chat;
using MafiaNight.Services.Rooms;
using MafiaNight.UI.Kit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MafiaNight.App
{
    /// <summary>
    /// Shows a friend's invitation to a room over whatever screen the player is on, and takes him to the room when he presses join.
    /// Lives across scenes (created by the app binder).
    /// </summary>
    public sealed class InviteFlow : MonoBehaviour
    {
        public static InviteFlow Instance { get; private set; }

        private FriendsService fs;
        private ISceneLoader scenes;

        public static InviteFlow Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("InviteFlow");
            DontDestroyOnLoad(go);
            return go.AddComponent<InviteFlow>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (Instance != this) return;
            fs = FriendsService.Ensure();
            fs.OnInvite += OnInvite;
            ServiceLocator.TryGet(out scenes);
        }

        private void OnDestroy()
        {
            if (fs != null) fs.OnInvite -= OnInvite;
            if (Instance == this) Instance = null;
        }

        private static bool InMatch()
        {
            if (SceneManager.GetActiveScene().name == AppScenes.Match) return true;
            var s = MatchRunner.Instance != null ? MatchRunner.Instance.Session : null;
            if (s == null) return false;
            var p = s.Client.Phase;
            return p != MafiaNight.MatchRules.Phase.Lobby && p != MafiaNight.MatchRules.Phase.GameOver;
        }

        /// <summary>The canvas of the screen the player is looking at (not the persistent transition canvas of the scene loader).</summary>
        private static Transform FindCanvas()
        {
            Canvas best = null;
            var active = SceneManager.GetActiveScene();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.gameObject.scene == active && (best == null || c.sortingOrder >= best.sortingOrder)) best = c;
            return best != null ? best.transform : null;
        }

        private void OnInvite(ServerEvent e)
        {
            if (e.Room == null || InMatch()) return;                    // not in the middle of a match
            var canvas = FindCanvas();
            if (canvas == null) return;
            string sub = "غرفة «" + e.Room.Name + "» · " + e.Room.Count + " من " + e.Room.Cap;
            KitInviteBanner.Show(canvas, e.Name + " يدعوك", sub, () => Join(e.Room), null);
        }

        private void Join(RoomRef room)
        {
            fs.JoinRoom(room, r =>
            {
                var toast = FindAnyObjectByType<KitToast>(FindObjectsInactive.Include);
                if (!r.Ok) { if (toast != null) toast.Show(RoomResult.Message(r.Error, RoomRules.PublicMinLevel)); return; }
                if (SceneManager.GetActiveScene().name == AppScenes.Rooms) return;     // the rooms screen shows the room it already has
                if (scenes != null) scenes.LoadScene(AppScenes.Rooms); else SceneManager.LoadScene(AppScenes.Rooms);
            });
        }
    }
}
