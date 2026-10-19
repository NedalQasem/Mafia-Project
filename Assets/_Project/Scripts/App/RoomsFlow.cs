using System.Collections.Generic;
using MafiaNight.Core;
using MafiaNight.Data.Rooms;
using MafiaNight.Services.Profile;
using MafiaNight.Services.Rooms;
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
        private KitRoomsScreen screen;
        private KitCreateRoomScreen create;
        private KitCodeSheet codeSheet;
        private KitVoiceRoom voice;
        private KitToast toast;

        private readonly Dictionary<string, RoomListing> byId = new Dictionary<string, RoomListing>();
        private string shownSignature = "";
        private float nextList;
        private bool listing, busyJoin;

        private void Start()
        {
            if (!ServiceLocator.TryGet(out rooms)) { Destroy(this); return; }
            ServiceLocator.TryGet(out profile);
            screen = FindAnyObjectByType<KitRoomsScreen>();
            create = FindAnyObjectByType<KitCreateRoomScreen>(FindObjectsInactive.Include);
            codeSheet = FindAnyObjectByType<KitCodeSheet>(FindObjectsInactive.Include);
            voice = FindAnyObjectByType<KitVoiceRoom>(FindObjectsInactive.Include);
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
            if (voice != null)
            {
                voice.OnBack += OnVoiceBack;
                voice.OnInvite += OnInvite;
            }
            rooms.OnRoomChanged += OnRoomChanged;
            rooms.OnRoomLeft += OnRoomLeft;
            nextList = 0f;
        }

        private void OnDestroy()
        {
            if (rooms == null) return;
            rooms.OnRoomChanged -= OnRoomChanged;
            rooms.OnRoomLeft -= OnRoomLeft;
        }

        private bool OverlayOpen => (create != null && create.gameObject.activeSelf) || (voice != null && voice.gameObject.activeSelf);

        private void Update()
        {
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
                // EOS takes a few seconds to index a new room: show the player's own public room anyway
                var mine = rooms.CurrentRoom;
                if (mine != null && mine.Kind != RoomKind.Private && !list.Exists(x => x.Id == mine.Id)) list.Insert(0, mine);
                Show(list);
            });
        }

        private void Show(List<RoomListing> list)
        {
            var sig = new System.Text.StringBuilder();
            foreach (var l in list) sig.Append(l.Id).Append(':').Append(l.Count).Append(':').Append(l.Name).Append(';');
            if (sig.ToString() == shownSignature) return;
            shownSignature = sig.ToString();
            byId.Clear();
            var infos = new List<RoomInfo>();
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

        private void EnterVoice()
        {
            var room = rooms.CurrentRoom;
            if (voice == null || room == null) return;
            voice.Show(room.Name, SubText(room), Seats(), room.Capacity);
        }

        private static string SubText(RoomListing room) =>
            CoinPill.Format(room.Count) + " من " + CoinPill.Format(room.Capacity) + (room.Kind == RoomKind.Private && !string.IsNullOrEmpty(room.Code) ? " · " + room.Code : "");

        private List<SeatInfo> Seats()
        {
            var seats = new List<SeatInfo>();
            foreach (var m in rooms.Members) seats.Add(new SeatInfo { name = m.Name, rank = m.Rank, host = m.IsHost });
            return seats;
        }

        private void OnRoomChanged()
        {
            if (voice == null || !voice.gameObject.activeSelf || rooms.CurrentRoom == null) return;
            EnterVoice();
        }

        private void OnRoomLeft(string reason)
        {
            if (voice != null && voice.gameObject.activeSelf && !string.IsNullOrEmpty(reason)) voice.gameObject.SetActive(false);
            if (!string.IsNullOrEmpty(reason)) { Say(reason); nextList = 0f; }
        }

        private void OnVoiceBack()
        {
            voice.gameObject.SetActive(false);
            rooms.Leave(() => { nextList = 0f; });
        }

        private void OnInvite()
        {
            var room = rooms.CurrentRoom;
            if (room == null) return;
            string text = room.Kind == RoomKind.Private && !string.IsNullOrEmpty(room.Code)
                ? "تعال نسهر بلعبة المافيا! ادخل بالكود " + room.Code
                : "تعال نسهر بلعبة المافيا! ادخل غرفة «" + room.Name + "»";
            GUIUtility.systemCopyBuffer = text;
            Say("اننسخت الدعوة، الصقها لأصحابك.");
        }
    }
}
