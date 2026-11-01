using System.Collections.Generic;
using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.Data.Settings;
using MafiaNight.Services.Profile;
using MafiaNight.Services.Rooms;
using MafiaNight.Services.Voice;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Glue for the rooms screen: the list, quick join, the code sheet, the create-room screen and the voice room,
    /// all on top of <see cref="IRoomService"/>. Added by <see cref="KitAppBinder"/> when the scene has a rooms screen.
    /// </summary>
    public sealed class RoomsFlow : MonoBehaviour
    {
        private const string Tag = "RoomsFlow";
        private const float ListEvery = 10f;

        private IRoomService rooms;
        private IPlayerProfileService profile;
        private IVoiceService audio;
        private KitRoomsScreen screen;
        private KitCreateRoomScreen create;
        private KitCodeSheet codeSheet;
        private RoomScreenFlow roomFlow;
        private KitToast toast;

        private readonly Dictionary<string, RoomListing> byId = new Dictionary<string, RoomListing>();
        private string shownSignature = "";
        private float nextList;
        private bool listing, busyJoin;

        private void Start()
        {
            if (!ServiceLocator.TryGet(out rooms)) { Destroy(this); return; }
            ServiceLocator.TryGet(out profile);
            ServiceLocator.TryGet(out audio);
            screen = FindAnyObjectByType<KitRoomsScreen>();
            create = FindAnyObjectByType<KitCreateRoomScreen>(FindObjectsInactive.Include);
            codeSheet = FindAnyObjectByType<KitCodeSheet>(FindObjectsInactive.Include);
            toast = FindAnyObjectByType<KitToast>(FindObjectsInactive.Include);
            if (screen == null) { Destroy(this); return; }

            screen.OnJoinRoom += OnJoinCard;
            screen.OnQuickJoin += OnQuickJoin;
            screen.OnCreateRoom += OnCreate;
            screen.OnEnterCode += () => { if (codeSheet != null) codeSheet.Open(); };
            screen.SetRooms(new List<RoomInfo>());

            if (create != null)
            {
                create.OnBack += OnCreateBack;
                create.OnSubmit += OnSubmit;
                create.OnEnter += _ => { create.Close(); EnterVoice(); };
            }
            if (codeSheet != null) codeSheet.OnSubmit += OnCode;
            var cv = screen.GetComponentInParent<Canvas>();
            roomFlow = gameObject.AddComponent<RoomScreenFlow>();
            roomFlow.Init(rooms, audio, toast, cv != null ? cv.rootCanvas.transform : screen.transform);
            roomFlow.Left += () => nextList = 0f;
            rooms.OnRoomLeft += OnRoomLeft;
            rooms.OnRoomJoined += _ => { if (!roomFlow.IsOpen) EnterVoice(); };   // joined from a friend's invitation or from the family screen
            rooms.OnHostChanged += Say;
            nextList = 0f;
            if (rooms.InRoom) EnterVoice();   // coming back from a match: the room is still ours
            var runner = MafiaNight.Services.Match.MatchRunner.Instance;
            string why = runner != null ? runner.TakeEndReason() : "";
            if (!string.IsNullOrEmpty(why)) Say(why);
        }

        private void OnDestroy()
        {
            if (rooms == null) return;
            rooms.OnRoomLeft -= OnRoomLeft;
            rooms.OnHostChanged -= Say;
        }

        private bool OverlayOpen => (create != null && create.gameObject.activeSelf) || (roomFlow != null && roomFlow.IsOpen);

        private void Update()
        {
            if (audio != null) audio.Tick();
            if (rooms == null || screen == null || listing || OverlayOpen || Time.unscaledTime < nextList) return;
            RefreshList();
        }

        // ---------------------------------------------------------------------------- list

        private void RefreshList()
        {
            listing = true;
            nextList = Time.unscaledTime + ListEvery;
            rooms.RefreshList(r =>
            {
                listing = false;
                if (this == null || screen == null) return;
                if (!r.Ok)
                {
                    if (r.Error != RoomError.Busy && r.Error != RoomError.NotReady) Log.Warning("Room list failed: " + r.Error + " " + r.Detail, Tag);
                    nextList = Time.unscaledTime + (r.Error == RoomError.NotReady ? 3f : ListEvery);
                    return;
                }
                var list = new List<RoomListing>(r.Rooms);
                list.RemoveAll(x => !string.IsNullOrEmpty(x.OfficialId));   // official rooms come from the service (pinned first, with their own card)
                // EOS takes a few seconds to index a new room: show the player's own public room anyway
                var mine = rooms.CurrentRoom;
                if (mine != null && mine.Kind != RoomKind.Private && string.IsNullOrEmpty(mine.OfficialId) && !list.Exists(x => x.Id == mine.Id)) list.Insert(0, mine);
                OfficialRooms.Refresh(_ => { if (this != null && screen != null) Show(list); });   // a failed refresh keeps the last known official rooms
            });
        }

        private void Show(List<RoomListing> list)
        {
            var sig = new System.Text.StringBuilder();
            foreach (var o in OfficialRooms.Rooms) if (o.Open) sig.Append('#').Append(o.Id).Append(':').Append(o.LobbyId).Append(':').Append(o.Online).Append(':').Append(o.DisplayName).Append(';');
            foreach (var l in list) sig.Append(l.Id).Append(':').Append(l.Count).Append(':').Append(l.Name).Append(';');
            if (sig.ToString() == shownSignature) return;
            shownSignature = sig.ToString();
            byId.Clear();
            var infos = new List<RoomInfo>();
            foreach (var o in OfficialRooms.Rooms)
            {
                if (!o.Open) continue;
                // the room bot owns the lobby; this listing only carries what joining needs
                byId[o.LobbyId] = new RoomListing { Id = o.LobbyId, Name = o.DisplayName, OfficialId = o.Id, Kind = RoomKind.Public, Mode = RoomMode.Hangout, Count = o.Online, Capacity = o.Capacity, ChatEnabled = o.ChatEnabled, MicSeats = o.MicSeats, MicByRequest = o.MicMode == "request" };
                infos.Add(new RoomInfo { id = o.LobbyId, name = o.DisplayName, sub = o.DisplaySub, official = true, adminsOnline = o.AdminsOnline, pulse = (profile?.LocalProfile?.Level ?? 99) <= 3, owner = "", ownerRank = 0, ownerLevel = 1, count = o.Online, capacity = o.Capacity, speakingSeat = -1, type = RoomType.Vip });
            }
            foreach (var l in list)
            {
                byId[l.Id] = l;
                infos.Add(new RoomInfo
                {
                    id = l.Id, name = l.Name, owner = string.IsNullOrEmpty(l.OwnerName) ? "لاعب" : l.OwnerName,
                    ownerRank = l.OwnerRank, ownerLevel = l.OwnerLevel, count = l.Count, capacity = l.Capacity, speakingSeat = -1,
                    type = l.Kind == RoomKind.Vip ? RoomType.Vip : l.Kind == RoomKind.Private ? RoomType.Private : RoomType.Public,
                    mafiaMode = l.Mode == RoomMode.Mafia, isNew = l.IsNew, code = l.Code,
                });
            }
            screen.SetRooms(infos);
        }

        // ---------------------------------------------------------------------------- joining

        private void Say(string text) { if (toast != null) toast.Show(text); }

        private void OnJoinCard(RoomInfo info)
        {
            if (busyJoin || string.IsNullOrEmpty(info.id) || !byId.TryGetValue(info.id, out var listing)) return;
            busyJoin = true;
            rooms.Join(listing, Entered);
        }

        private void OnQuickJoin()
        {
            if (busyJoin) return;
            busyJoin = true;
            rooms.QuickJoin(Entered);
        }

        private void Entered(RoomResult r)
        {
            busyJoin = false;
            if (this == null) return;
            if (!r.Ok) { Say(RoomResult.Message(r.Error, RoomRules.PublicMinLevel)); if (r.Error != RoomError.NoRooms) Log.Warning("Join failed: " + r.Error + " " + r.Detail, Tag); nextList = 0f; return; }
            EnterVoice();
        }

        private void OnCode(string code)
        {
            codeSheet.SetBusy(true);
            rooms.JoinByCode(code, r =>
            {
                if (this == null) return;
                codeSheet.SetBusy(false);
                if (!r.Ok) { codeSheet.ShowMessage(RoomResult.Message(r.Error, RoomRules.PublicMinLevel)); return; }
                codeSheet.Close();
                EnterVoice();
            });
        }

        // ---------------------------------------------------------------------------- creating

        private void OnCreate()
        {
            if (create == null) return;
            var p = profile?.LocalProfile;
            string suggestion = p != null && !string.IsNullOrEmpty(p.DisplayName) ? "سهرة " + p.DisplayName : "";
            create.Open(Mathf.Max(1, p?.Level ?? 1), p != null && p.IsVIPActive(), suggestion);
        }

        private void OnSubmit(RoomSettings settings)
        {
            rooms.Create(settings, r =>
            {
                if (this == null || create == null) return;
                if (!r.Ok)
                {
                    Log.Warning("Create failed: " + r.Error + " " + r.Detail, Tag);
                    create.ShowError(RoomResult.Message(r.Error, RoomRules.PublicMinLevel));
                    return;
                }
                nextList = 0f;
                if (r.Room.Kind == RoomKind.Private) create.ShowTicket(r.Room);
                else { create.Close(); EnterVoice(); }
            });
        }

        private void OnCreateBack()
        {
            create.Close();
            if (rooms.InRoom) EnterVoice();   // backing out of the ticket still leaves you in the room you just opened
        }

        // ---------------------------------------------------------------------------- inside a room

        private void EnterVoice() { if (roomFlow != null) roomFlow.Open(); }

        private void OnRoomLeft(string reason)
        {
            if (!string.IsNullOrEmpty(reason)) { Say(reason); nextList = 0f; }
        }
    }
}
