using MafiaNight.Core;
using MafiaNight.Services.Auth;
using MafiaNight.Services.Rooms;
using UnityEngine;

namespace MafiaNight.Services.Match
{
    /// <summary>
    /// Keeps one <see cref="MatchSession"/> alive while the player sits in a room (opened on joining, closed on leaving) and ticks it.
    /// Screens reach the session through <see cref="Session"/>.
    /// </summary>
    public sealed class MatchRunner : MonoBehaviour
    {
        public static MatchRunner Instance { get; private set; }
        public MatchSession Session { get; private set; }

        private IRoomService rooms;
        private IEosPlatform eos;
        private int lastMemberCount;
        private System.Collections.Generic.HashSet<string> known = new System.Collections.Generic.HashSet<string>();

        private void Awake() { Instance = this; }

        private void Start()
        {
            if (!ServiceLocator.TryGet(out rooms) || !ServiceLocator.TryGet(out eos)) { Destroy(this); return; }
            rooms.OnRoomJoined += _ => Open();
            rooms.OnRoomLeft += _ => Close();
            rooms.OnRoomChanged += OnRoomChanged;
            if (rooms.InRoom) Open();
        }

        private void OnDestroy()
        {
            if (rooms != null) rooms.OnRoomChanged -= OnRoomChanged;
            Close();
            if (Instance == this) Instance = null;
        }

        private void Open()
        {
            if (Session != null) return;
            Session = new MatchSession(eos, rooms);
            known.Clear();
            foreach (var m in rooms.Members) known.Add(m.Puid);
        }

        private void Close() { Session?.Dispose(); Session = null; }

        private void OnRoomChanged()
        {
            if (Session == null) return;
            var now = new System.Collections.Generic.HashSet<string>();
            foreach (var m in rooms.Members) now.Add(m.Puid);
            foreach (var id in known) if (!now.Contains(id)) Session.MemberLeft(id);
            known = now;
        }

        private void Update() { Session?.Tick(Time.unscaledDeltaTime); }
    }
}
