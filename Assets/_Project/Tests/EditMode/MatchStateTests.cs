using MafiaNight.MatchRules;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    public class MatchStateTests
    {
        [Test]
        public void TheStateIsDecidedByThePhaseAndWhetherThePlayerLives()
        {
            Assert.AreEqual(MatchState.Idle, MatchStates.Of(Phase.Lobby, false));
            Assert.AreEqual(MatchState.Idle, MatchStates.Of(Phase.Lobby, true));
            foreach (var p in new[] { Phase.RoleReveal, Phase.Night, Phase.Dawn, Phase.Day, Phase.Voting, Phase.Verdict })
            {
                Assert.AreEqual(MatchState.Playing, MatchStates.Of(p, true), p.ToString());
                Assert.AreEqual(MatchState.Watching, MatchStates.Of(p, false), p.ToString());
            }
            Assert.AreEqual(MatchState.Finished, MatchStates.Of(Phase.GameOver, true));
            Assert.AreEqual(MatchState.Finished, MatchStates.Of(Phase.GameOver, false));
        }

        [Test]
        public void TheTableOpensByItselfOnlyWhenAMatchBegins()
        {
            Assert.IsTrue(MatchStates.OpensTable(Phase.Lobby, Phase.RoleReveal), "a match begins");
            Assert.IsTrue(MatchStates.OpensTable(Phase.GameOver, Phase.RoleReveal), "the next match of the same room");
            Assert.IsTrue(MatchStates.OpensTable(Phase.Lobby, Phase.Night), "a late member hears of a match that is already in its night");
            foreach (var next in new[] { Phase.Night, Phase.Dawn, Phase.Day, Phase.Voting, Phase.Verdict, Phase.GameOver })
                Assert.IsFalse(MatchStates.OpensTable(Phase.Day, next), "a change of phase must not pull a player back to the table: " + next);
            Assert.IsFalse(MatchStates.OpensTable(Phase.RoleReveal, Phase.Night));
            Assert.IsFalse(MatchStates.OpensTable(Phase.Lobby, Phase.Lobby));
            Assert.IsFalse(MatchStates.OpensTable(Phase.Night, Phase.GameOver));
        }
    }
}
