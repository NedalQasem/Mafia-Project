using System;
using System.Collections.Generic;
using System.Linq;
using MafiaNight.Core;
using MafiaNight.MatchRules.Stage;
using MafiaNight.Services.Match;
using MafiaNight.Services.Rooms;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// The bots the admin board puts into a room of a player, run by that room's host: every few seconds the host reports the room to the rewards service and hears
    /// how many bots are wanted (the board lists the room by its number, "complete to 4", sitting, chatting, playing). The bots are listed as virtual members
    /// (<see cref="StageSync.SetVirtuals"/>: they show in the count, on the seats, in the chat) and, when the host starts a match, they play it (<see cref="MatchSession"/>).
    /// A bot is not an account: it earns nothing. While a match runs the bots stay as they are. Created by <see cref="RoomScreenFlow"/>.
    /// </summary>
    public sealed class RoomBotsFlow : MonoBehaviour
    {
        private const string Tag = "RoomBots";
        private const float AskIdle = 20f;        // no bots wanted: only the board's room list needs us
        private const float AskActive = 8f;       // bots are wanted: follow the real players coming and going

        private IRoomService rooms;
        private Func<MatchSession> getSession;
        private BotCrew crew;
        private MatchSession crewSession;
        private string crewRoom = "";
        private BotWish wish = new BotWish();
        private float nextTick, nextAsk;
        private bool asking;
        private int askedReal = -1;
        private readonly System.Random rng = new System.Random();

        public void Init(IRoomService rooms, Func<MatchSession> getSession) { this.rooms = rooms; this.getSession = getSession; }

        private int RealCount() => rooms.Members.Count(m => !m.IsKeeper);

        private void Update()
        {
            if (rooms == null || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 1f;

            var session = getSession();
            var room = rooms.CurrentRoom;
            bool mine = session != null && room != null && string.IsNullOrEmpty(room.OfficialId) && rooms.IAmHost && session.Stage != null && session.Stage.IAmHost;
            if (!mine) { Drop(); return; }

            if (crew == null || crewSession != session || crewRoom != room.Id)
            {
                Drop();
                crewSession = session; crewRoom = room.Id; wish = new BotWish(); nextAsk = 0f; askedReal = -1;
                crew = new BotCrew(session.Stage, () => DateTime.UtcNow, rng, (who, text) => session.BotSay(who, text), RealCount);
            }

            // somebody came or went: the number of bots wanted changes with him
            if (wish.Wanted > 0 && askedReal != RealCount()) nextAsk = Mathf.Min(nextAsk, Time.unscaledTime + 1f);
            if (!asking && Time.unscaledTime >= nextAsk) Ask(room, session);

            session.BotsPlay = wish.Play;
            if (!session.MatchRunning) crew.Configure(wish.Profiles, wish.Wanted, wish.Sit, wish.Chat && room.ChatEnabled);      // while a match runs the bots stay exactly as they are
            crew.Tick();
        }

        private void Ask(MafiaNight.Data.Rooms.RoomListing room, MatchSession session)
        {
            asking = true;
            int real = RealCount();
            RoomBots.Report(room, real, real + session.Stage.Virtuals.Count, session.MatchRunning, w =>
            {
                asking = false;
                if (this == null) return;
                askedReal = real;
                if (w != null)
                {
                    if (w.Wanted != wish.Wanted) Log.Info("Bots wanted in this room: " + w.Wanted + " (sit " + w.Sit + ", chat " + w.Chat + ", play " + w.Play + ")", Tag);
                    wish = w;
                }
                nextAsk = Time.unscaledTime + (wish.Wanted > 0 ? AskActive : AskIdle);
            });
        }

        /// <summary>The room is gone, or this device is not its host any more: the bots go with it.</summary>
        private void Drop()
        {
            if (crew == null) return;
            try { crewSession?.Stage?.SetVirtuals(new List<VirtualMember>()); } catch (Exception e) { Log.Warning("Dropping the bots: " + e.Message, Tag); }
            crew = null; crewSession = null; crewRoom = ""; wish = new BotWish();
        }

        private void OnDestroy() { Drop(); }
    }
}
