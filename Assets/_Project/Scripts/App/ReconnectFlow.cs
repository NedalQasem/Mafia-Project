using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.Services.Match;
using MafiaNight.Services.Rooms;
using MafiaNight.Services.Scenes;
using MafiaNight.UI.Kit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MafiaNight.App
{
    /// <summary>
    /// A player whose app closed or whose connection broke in the middle of a match can walk back in: this offers the room he was playing in
    /// (remembered by <see cref="LastMatchStore"/>) and joins it again; the host gives him his seat and role back. Lives across scenes.
    /// </summary>
    public sealed class ReconnectFlow : MonoBehaviour
    {
        private const float FirstCheckAfter = 4f;
        private const float WaitForTable = 10f;

        public static ReconnectFlow Instance { get; private set; }

        private IRoomService rooms;
        private ISceneLoader scenes;
        private float checkAt = -1f;
        private bool offered;
        private bool joining;
        private float joinedAt = -1f;

        public static ReconnectFlow Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("ReconnectFlow");
            DontDestroyOnLoad(go);
            return go.AddComponent<ReconnectFlow>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (Instance != this) return;
            if (!ServiceLocator.TryGet(out rooms)) { Destroy(gameObject); return; }
            ServiceLocator.TryGet(out scenes);
            rooms.OnRoomLeft += OnLeft;
            checkAt = Time.unscaledTime + FirstCheckAfter;
        }

        private void OnDestroy()
        {
            if (rooms != null) rooms.OnRoomLeft -= OnLeft;
            if (Instance == this) Instance = null;
        }

        /// <summary>The room was lost against his will (connection, kick-out of the lobby): offer to go back, a deliberate leave offers nothing.</summary>
        private void OnLeft(string reason)
        {
            if (string.IsNullOrEmpty(reason) || LastMatchStore.Load() == null) return;
            offered = false;
            checkAt = Time.unscaledTime + 1.5f;
        }

        private void Update()
        {
            if (joinedAt >= 0f && Time.unscaledTime - joinedAt > WaitForTable)
            {
                joinedAt = -1f;                                                      // back in the room but no match came to us: the room screen
                var c = MatchRunner.Instance != null && MatchRunner.Instance.Session != null ? MatchRunner.Instance.Session.Client : null;
                if (c != null && c.Phase != MafiaNight.MatchRules.Phase.Lobby && c.Phase != MafiaNight.MatchRules.Phase.GameOver) return;
                LastMatchStore.Clear();
                if (SceneManager.GetActiveScene().name != AppScenes.Rooms) Go(AppScenes.Rooms);
            }
            if (checkAt < 0f || Time.unscaledTime < checkAt || offered || joining) return;
            checkAt = -1f;
            if (rooms == null || rooms.InRoom || SceneManager.GetActiveScene().name == GameConstants.SCENE_MATCH) return;
            var saved = LastMatchStore.Load();
            if (saved == null) return;
            var canvas = InviteFlow.FindCanvas();
            if (canvas == null) { checkAt = Time.unscaledTime + 2f; return; }        // the screen is still loading
            offered = true;
            KitInviteBanner.Show(canvas, "عندك مباراة شغّالة", "غرفة «" + saved.RoomName + "» · ارجع لمقعدك", () => Return(saved), null, 25f);
        }

        private void Return(LastMatchStore.Entry saved)
        {
            if (joining) return;
            joining = true;
            var listing = new RoomListing { Id = saved.RoomId, Name = saved.RoomName, Code = saved.Code ?? "", Kind = saved.Kind };
            rooms.Join(listing, r =>
            {
                joining = false;
                var toast = FindAnyObjectByType<KitToast>(FindObjectsInactive.Include);
                if (!r.Ok)
                {
                    if (r.Error == RoomError.NotFound) LastMatchStore.Clear();
                    if (toast != null) toast.Show(r.Error == RoomError.NotFound ? "المباراة انتهت أو الغرفة انسكرت." : RoomResult.Message(r.Error, RoomRules.PublicMinLevel));
                    return;
                }
                if (!rooms.CurrentRoom.InMatch)
                {
                    LastMatchStore.Clear();
                    if (toast != null) toast.Show("المباراة انتهت. أنت بالغرفة هسا.");
                    if (SceneManager.GetActiveScene().name != AppScenes.Rooms) Go(AppScenes.Rooms);
                    return;
                }
                joinedAt = Time.unscaledTime;                                          // the host sends the table; the match screens open by themselves
            });
        }

        private void Go(string scene) { if (scenes != null) scenes.LoadScene(scene); else SceneManager.LoadScene(scene); }
    }
}
