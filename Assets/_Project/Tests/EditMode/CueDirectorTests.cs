using System.Collections.Generic;
using System.Linq;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    /// <summary>Which narration belongs to which moment, from one player's point of view.</summary>
    public class CueDirectorTests
    {
        private ClientMatchController client;
        private CueDirector director;
        private List<CuePlay> played;

        [SetUp]
        public void Setup()
        {
            client = new ClientMatchController("me", m => { });
            director = new CueDirector(client);
            played = new List<CuePlay>();
            director.Play += p => played.Add(p);
            client.Apply(new WireMessage(Msg.Start, "match1"));
        }

        [TearDown] public void Done() { director.Dispose(); }

        private void Table(Phase phase, params string[] deadIds)
        {
            var seats = new List<string>();
            foreach (var id in new[] { "me", "ali", "sara", "omar" })
            {
                seats.Add(id); seats.Add(id == "me" ? "أنت" : id); seats.Add(deadIds.Contains(id) ? "0" : "1"); seats.Add("0");
            }
            client.Apply(new WireMessage(Msg.State, new[] { phase.ToString(), "1", "20" }.Concat(seats).ToArray()));
        }

        private void Role(Role r) => client.Apply(new WireMessage(Msg.Role, r.ToString()));

        private void Step(string awake, params NightStep[] all) =>
            client.Apply(new WireMessage(Msg.Step, awake, string.Join(",", all.Select(x => x.ToString())), "0"));

        [Test]
        public void TheNightFallsAndThenEveryoneHearsEveryCall_TheSameForAllRoles()
        {
            foreach (var mine in new[] { MafiaNight.MatchRules.Role.Doctor, MafiaNight.MatchRules.Role.Civilian })
            {
                played.Clear();
                director.Dispose(); client = new ClientMatchController("me", m => { }); director = new CueDirector(client); director.Play += p => played.Add(p);
                client.Apply(new WireMessage(Msg.Start, "m2")); Role(mine);
                Table(Phase.Night);
                Assert.AreEqual(CueIds.NightFalls, played[0].Cue.Id);
                Step("Mafia", NightStep.Mafia, NightStep.Doctor);
                Assert.AreEqual(CueIds.Call(MafiaNight.MatchRules.Role.Mafia), played[1].Cue.Id, mine + ": the call is public");
                Step("", NightStep.Mafia, NightStep.Doctor);
                Assert.AreEqual(CueIds.Sleep(MafiaNight.MatchRules.Role.Mafia), played[2].Cue.Id, mine + ": and so is the going back to sleep");
                Step("Doctor", NightStep.Mafia, NightStep.Doctor);
                Assert.AreEqual(CueIds.Call(MafiaNight.MatchRules.Role.Doctor), played[3].Cue.Id);
                Assert.AreEqual(played[1].Cue.Duration, played[3].Cue.Duration, "every call lasts the same");
            }
        }

        [Test]
        public void TheDeadHearTheCallsToo_SoTheSilenceTellsNothing()
        {
            Role(MafiaNight.MatchRules.Role.Sheriff);
            Table(Phase.Night, "me");
            Step("Sheriff", NightStep.Mafia, NightStep.Sheriff);
            Assert.AreEqual(CueIds.Call(MafiaNight.MatchRules.Role.Sheriff), played[1].Cue.Id);
        }

        [Test]
        public void TheDawnTellsWhoDied_WhoWasSaved_TheShotAndTheSilence_OneAfterTheOther()
        {
            Role(MafiaNight.MatchRules.Role.Civilian);
            Table(Phase.Dawn, "ali");
            client.Apply(new WireMessage(Msg.Dawn, "ali", "0", "ali", "Civilian", "sara", "omar", "Civilian"));
            Assert.AreEqual(new[] { CueIds.DawnKill, CueIds.DawnShot, CueIds.DawnSilenced }, played.Select(p => p.Cue.Id).ToArray());
            Assert.AreEqual("ali", played[0].Subtitle.Contains("ali") ? "ali" : "", "the name is in the line");
            Assert.Less(played[0].Delay, played[1].Delay); Assert.Less(played[1].Delay, played[2].Delay);

            played.Clear();
            client.Apply(new WireMessage(Msg.Dawn, "omar", "1", "", "", ""));
            Assert.AreEqual(CueIds.DawnSaved, played.Single().Cue.Id);
            played.Clear();
            client.Apply(new WireMessage(Msg.Dawn, "", "0", "", "", ""));
            Assert.AreEqual(CueIds.DawnQuiet, played.Single().Cue.Id);
        }

        [Test]
        public void TheVerdictSaysIfTheMafiaWasCaught_AndTheDayAndTheVoteHaveTheirLines()
        {
            Role(MafiaNight.MatchRules.Role.Civilian);
            Table(Phase.Day); Assert.AreEqual(CueIds.DayStart, played.Last().Cue.Id);
            Table(Phase.Voting); Assert.AreEqual(CueIds.VoteStart, played.Last().Cue.Id);
            Table(Phase.Verdict, "ali");
            client.Apply(new WireMessage(Msg.Verdict, "ali", "Silencer", "3", "0"));
            Assert.AreEqual(CueIds.VerdictMafia, played.Last().Cue.Id, "the silencer is mafia");
            client.Apply(new WireMessage(Msg.Verdict, "ali", "Sniper", "3", "0"));
            Assert.AreEqual(CueIds.VerdictTown, played.Last().Cue.Id);
            client.Apply(new WireMessage(Msg.Verdict, "", "", "1", "2"));
            Assert.AreEqual(CueIds.VerdictNobody, played.Last().Cue.Id);
        }

        [Test]
        public void TheAvengersMomentAndTheEndHaveTheirLines()
        {
            Role(MafiaNight.MatchRules.Role.Civilian);
            Table(Phase.Avenge, "ali");
            client.Apply(new WireMessage(Msg.Avenge, "ali", "", "", "open"));
            Assert.AreEqual(CueIds.AvengeStart, played.Last().Cue.Id);
            client.Apply(new WireMessage(Msg.Avenge, "ali", "sara", "Civilian", "done"));
            Assert.AreEqual(CueIds.AvengeDone, played.Last().Cue.Id);
            StringAssert.Contains("sara", played.Last().Subtitle);
            client.Apply(new WireMessage(Msg.Over, "Town"));
            Assert.AreEqual(CueIds.OverTown, played.Last().Cue.Id);
            client.Apply(new WireMessage(Msg.Over, "Mafia"));
            Assert.AreEqual(CueIds.OverMafia, played.Last().Cue.Id);
        }

        [Test]
        public void EveryCueTheDirectorCanChooseExistsInTheCatalog()
        {
            foreach (var id in new[] { CueIds.MatchStart, CueIds.NightFalls, CueIds.DawnQuiet, CueIds.DawnKill, CueIds.DawnSaved, CueIds.DawnShot, CueIds.DawnSilenced, CueIds.DayStart, CueIds.VoteStart,
                CueIds.VerdictMafia, CueIds.VerdictTown, CueIds.VerdictNobody, CueIds.AvengeStart, CueIds.AvengeDone, CueIds.OverTown, CueIds.OverMafia })
                Assert.IsTrue(CueCatalog.Has(id), id);
            foreach (var cue in CueCatalog.All)
                if (!cue.Id.StartsWith("card.") && !cue.Id.StartsWith("sleep.")) Assert.IsNotEmpty(cue.Subtitle, cue.Id + " must have its line (the slip works with no audio)");
        }

        [Test]
        public void TheRecordingListMatchesTheCues_AndNoRecordedLineHasAPlayersName()
        {
            string list = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Docs/narrator-lines.md"));
            foreach (var cue in CueCatalog.All)
            {
                if (string.IsNullOrEmpty(cue.VoiceText)) continue;
                StringAssert.DoesNotContain("{name}", cue.VoiceText, cue.Id + ": a name cannot be recorded");
                StringAssert.Contains("| `" + CueCatalog.FileName(cue.Id) + "` | " + cue.VoiceText + " |", list, cue.Id + ": Docs/narrator-lines.md is out of date (the lines changed)");
                StringAssert.DoesNotContain(".", CueCatalog.FileName(cue.Id), "no dots in a clip's file name");
            }
            CollectionAssert.AreEqual(new[] { "m", "f" }, CueCatalog.Voices);
        }

        [Test]
        public void EveryMomentThatIsAboutAPlayerCarriesHisSeat_SoTheTableCanAimAtIt()
        {
            Role(MafiaNight.MatchRules.Role.Civilian);
            Table(Phase.Dawn, "ali");
            client.Apply(new WireMessage(Msg.Dawn, "ali", "0", "ali", "Civilian", "sara", "omar", "Civilian"));
            Assert.AreEqual("ali", played[0].SeatId, "the kill is about the victim");
            Assert.AreEqual("civilian", played[0].RoleId, "the table turns his card over");
            Assert.AreEqual("civilian", played[1].RoleId, "and the card of the one the sniper hit");
            Assert.AreEqual("omar", played[1].SeatId, "the shot is about the one it hit");
            Assert.AreEqual("sara", played[2].SeatId, "the silence is about the silenced");
            played.Clear();
            client.Apply(new WireMessage(Msg.Dawn, "omar", "1", "", "", ""));
            Assert.AreEqual("omar", played.Single().SeatId, "the shield is about the one it saved");
            played.Clear();
            Table(Phase.Verdict, "ali");
            client.Apply(new WireMessage(Msg.Verdict, "ali", "Civilian", "3", "0"));
            Assert.AreEqual("ali", played.Last().SeatId, "the stamp is about the one voted out");
            Assert.AreEqual("civilian", played.Last().RoleId);
            Assert.AreEqual("sheikh", CueCatalog.CardIdOf(MafiaNight.MatchRules.Role.Sheriff), "the Sheriff's card is the Elder");
            foreach (MafiaNight.MatchRules.Role r in System.Enum.GetValues(typeof(MafiaNight.MatchRules.Role))) Assert.IsNotNull(MafiaNight.Data.Cards.RoleCards.Find(CueCatalog.CardIdOf(r)), r + ": no card");
            played.Clear();
            client.Apply(new WireMessage(Msg.Dawn, "", "0", "", "", ""));
            Assert.AreEqual("", played.Single().SeatId, "a quiet night is about nobody");
            foreach (var id in new[] { CueIds.DawnKill, CueIds.DawnSaved, CueIds.DawnShot, CueIds.VerdictMafia, CueIds.VerdictTown, CueIds.AvengeDone })
                Assert.IsTrue(CueCatalog.Find(id).Steps.Any(s => s.Kind == CueStepKind.Animation && s.Value == "zoom_seat"), id + ": the close-up on the seat");
        }

        [Test]
        public void TheArabicVoiceReadsTheSameLineAsTheSlip_WithDiacriticsAndNoName()
        {
            foreach (var cue in CueCatalog.All)
            {
                if (string.IsNullOrEmpty(cue.Subtitle)) continue;
                string said = cue.VoiceText;
                Assert.AreEqual(CueCatalog.StripDiacritics(cue.Subtitle).Replace("{name}", CueCatalog.SomeoneInVoice), CueCatalog.StripDiacritics(said), cue.Id + ": the voice line and the slip line say different things");
                Assert.IsTrue(said.Any(ch => ch >= 'ً' && ch <= 'ْ'), cue.Id + ": the voice line has no diacritics (the voice model needs them to read the dialect)");
            }
        }

        [Test]
        public void EveryNarratedCueHasAnEnglishLine_ItIsInTheRecordingList_AndTheSlipFollowsTheLanguage()
        {
            string list = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Docs/narrator-lines.md"));
            string english = list.Substring(list.IndexOf("## الإنجليزي"));
            foreach (var cue in CueCatalog.All)
            {
                if (string.IsNullOrEmpty(cue.Subtitle)) continue;
                string en = CueCatalog.Text(cue, "en");
                Assert.AreNotEqual(cue.Subtitle, en, cue.Id + ": no English line (it would be told in Arabic)");
                Assert.IsFalse(en.Any(ch => ch >= '؀' && ch <= 'ۿ'), cue.Id + ": the English line has Arabic letters");
                StringAssert.DoesNotContain("{name}", CueCatalog.VoiceText(cue, "en"), cue.Id);
                StringAssert.Contains("| `" + CueCatalog.FileName(cue.Id) + "` | " + CueCatalog.VoiceText(cue, "en") + " |", english, cue.Id + ": the English list in Docs/narrator-lines.md is out of date");
            }
            Assert.AreEqual("Someone is out. They were Mafia!", CueCatalog.VoiceText(CueCatalog.Find(CueIds.VerdictMafia), "en"), "a line that starts with the name starts with a capital");
            Assert.AreEqual("Nada is out. They were Mafia!", CueCatalog.Format(CueCatalog.Find(CueIds.VerdictMafia), "Nada", "en"));
            Assert.AreEqual(CueCatalog.Find(CueIds.DayStart).Subtitle, CueCatalog.Text(CueCatalog.Find(CueIds.DayStart), "ar"));
            director.Language = "en";
            Role(MafiaNight.MatchRules.Role.Civilian); Table(Phase.Day);
            Assert.AreEqual("The discussion begins.", played.Last().Subtitle);
        }
    }
}
