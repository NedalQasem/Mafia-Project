using System.Collections.Generic;
using System.Linq;
using MafiaNight.MatchRules;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    /// <summary>The written chat and the mic button of a match: who may write where, who reads what, and what the mic plate says.</summary>
    public class MatchChatTests
    {
        [Test]
        public void ByDayTheLivingWriteAtTheTable_AtNightOnlyTheMafiaWrite_AndTheyWriteSecretly()
        {
            foreach (var p in new[] { Phase.Dawn, Phase.Day, Phase.Voting, Phase.Verdict, Phase.Avenge })
            {
                var plan = MatchChat.For(p, Role.Civilian, true, false);
                Assert.AreEqual(ChatChannel.Table, plan.Channel, p.ToString()); Assert.IsFalse(plan.Locked, p.ToString());
            }
            var civ = MatchChat.For(Phase.Night, Role.Civilian, true, false);
            Assert.IsTrue(civ.Locked); Assert.IsFalse(civ.Tape);
            foreach (var mafia in new[] { Role.Mafia, Role.Silencer })
            {
                var plan = MatchChat.For(Phase.Night, mafia, true, false);
                Assert.AreEqual(ChatChannel.Mafia, plan.Channel, mafia.ToString()); Assert.IsFalse(plan.Locked); Assert.IsTrue(plan.Secret);
            }
        }

        [Test]
        public void TheSilencedPlayerCannotWriteByDay_ButTheNightIsNotHisSilence()
        {
            var day = MatchChat.For(Phase.Day, Role.Civilian, true, true);
            Assert.IsTrue(day.Locked); Assert.IsTrue(day.Tape);
            var night = MatchChat.For(Phase.Night, Role.Mafia, true, true);
            Assert.IsFalse(night.Locked, "the silence is one day; at night his mafia talk is not the day's");
        }

        [Test]
        public void TheSpectatorsHaveTheirOwnChannel_AndEverybodyIsInTheRoomBeforeAndAfter()
        {
            Assert.AreEqual(ChatChannel.Room, MatchChat.For(Phase.Day, Role.Civilian, false, false).Channel);
            Assert.AreEqual(ChatChannel.Room, MatchChat.For(Phase.Night, null, false, false).Channel);
            foreach (var p in new[] { Phase.Lobby, Phase.RoleReveal, Phase.GameOver })
            {
                var plan = MatchChat.For(p, Role.Mafia, true, false);
                Assert.AreEqual(ChatChannel.Room, plan.Channel, p.ToString()); Assert.IsFalse(plan.Locked);
            }
        }

        [Test]
        public void ALivingPlayerNeverReadsTheSpectators_AndTheMafiaLineStaysWithTheMafia()
        {
            // receiver alive, in a day: a line from a spectator is dropped, from a living player kept
            Assert.IsFalse(MatchChat.Accept(ChatChannel.Room, Phase.Day, true, false, false, false));
            Assert.IsTrue(MatchChat.Accept(ChatChannel.Table, Phase.Day, true, false, true, false));
            Assert.IsFalse(MatchChat.Accept(ChatChannel.Table, Phase.Day, true, false, false, false), "a dead player's table line is not a table line");
            // spectators read each other
            Assert.IsTrue(MatchChat.Accept(ChatChannel.Room, Phase.Day, false, false, false, false));
            // nobody writes at the table at night
            Assert.IsFalse(MatchChat.Accept(ChatChannel.Table, Phase.Night, true, false, true, false));
            // the mafia's secret line: only a mafia receiver and only from a living mate
            Assert.IsTrue(MatchChat.Accept(ChatChannel.Mafia, Phase.Night, true, true, true, true));
            Assert.IsFalse(MatchChat.Accept(ChatChannel.Mafia, Phase.Night, true, false, true, false), "a citizen never reads it");
            Assert.IsFalse(MatchChat.Accept(ChatChannel.Mafia, Phase.Day, true, true, true, true), "only at night");
            Assert.IsFalse(MatchChat.Accept(ChatChannel.Mafia, Phase.Night, true, true, true, false), "not from somebody who is not a mate");
            // before the match everything is the room's
            Assert.IsTrue(MatchChat.Accept(ChatChannel.Room, Phase.Lobby, true, false, true, false));
            Assert.IsTrue(MatchChat.Accept(ChatChannel.Room, Phase.GameOver, true, false, true, false));
        }

        [Test]
        public void ChannelCodesRoundTrip_AndAnOldClientsLineIsTheRoomsLine()
        {
            foreach (ChatChannel ch in System.Enum.GetValues(typeof(ChatChannel))) Assert.AreEqual(ch, MatchChat.FromCode(MatchChat.Code(ch)));
            Assert.AreEqual(ChatChannel.Room, MatchChat.FromCode(""));
            Assert.AreEqual(ChatChannel.Room, MatchChat.FromCode(null));
        }

        [Test]
        public void TheSilencerTalksWithHisMatesAtNight_LikeTheOtherMafia()
        {
            var mates = new[] { "m1" }; var all = new[] { "m1", "me", "c1", "c2" };
            var plan = MatchVoiceRules.For(Phase.Night, Role.Silencer, true, mates, all, all, "me");
            Assert.IsTrue(plan.MicAllowed);
            CollectionAssert.AreEquivalent(mates, plan.HearOnly);
            var citizen = MatchVoiceRules.For(Phase.Night, Role.Civilian, true, mates, all, all, "c1");
            Assert.IsFalse(citizen.MicAllowed);
        }

        // ------------------------------------------------------------------ the mic button

        private static MicState Mic(Phase p, Role? r, bool alive, bool silenced = false, bool avenger = false, bool allowed = true, bool on = true, bool connected = true)
            => MatchMic.Of(p, r, alive, silenced, avenger, allowed, on, connected);

        [Test]
        public void TheMicPlateSaysWhy()
        {
            Assert.AreEqual(MicLook.Open, Mic(Phase.Day, Role.Civilian, true).Look);
            Assert.AreEqual("مفتوح", Mic(Phase.Day, Role.Civilian, true).Reason);
            var night = Mic(Phase.Night, Role.Civilian, true, allowed: false);
            Assert.AreEqual(MicLook.Closed, night.Look); Assert.IsFalse(night.Tappable); Assert.AreEqual("مغلق · الليل", night.Reason);
            var silenced = Mic(Phase.Day, Role.Civilian, true, silenced: true, allowed: false);
            Assert.AreEqual("مُسكَت اليوم", silenced.Reason); Assert.IsFalse(silenced.Tappable);
            var mafia = Mic(Phase.Night, Role.Silencer, true);
            Assert.AreEqual(MicLook.Open, mafia.Look); StringAssert.Contains("مافيا", mafia.Reason);
            StringAssert.Contains("متفرجين", Mic(Phase.Day, Role.Civilian, false).Reason);
            StringAssert.Contains("الكل", Mic(Phase.Avenge, Role.Avenger, false, avenger: true).Reason);
        }

        [Test]
        public void ThePlayersOwnSwitchAndTheVoiceConnectionShowToo()
        {
            var off = Mic(Phase.Day, Role.Civilian, true, on: false);
            Assert.AreEqual(MicLook.Closed, off.Look); Assert.IsTrue(off.Tappable, "he may turn it on"); Assert.AreEqual("مكتوم منك", off.Reason);
            var down = Mic(Phase.Day, Role.Civilian, true, connected: false);
            Assert.AreEqual(MicLook.Waiting, down.Look); Assert.IsFalse(down.Tappable);
            Assert.IsFalse(Mic(Phase.Night, Role.Civilian, true, allowed: false, on: false).Tappable, "the match closes it: his switch changes nothing");
        }

        [Test]
        public void ThePlateIsAlwaysShort_ForEverySituation()
        {
            foreach (Phase p in System.Enum.GetValues(typeof(Phase)))
                foreach (var role in new Role?[] { null, Role.Civilian, Role.Mafia, Role.Silencer, Role.Avenger })
                    foreach (var alive in new[] { true, false })
                    {
                        var plan = MatchVoiceRules.For(p, role, alive, new string[0], new[] { "a" }, new[] { "a", "b" }, "b");
                        var m = MatchMic.Of(p, role, alive, false, false, plan.MicAllowed, true, true);
                        Assert.IsNotEmpty(m.Reason);
                        Assert.LessOrEqual(m.Reason.Length, 22, p + " " + role + " " + alive);
                    }
        }
    }
}
