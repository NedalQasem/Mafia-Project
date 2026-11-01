using MafiaNight.Core;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using MafiaNight.Services.Match;
using MafiaNight.Services.Scenes;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Starts the match screens: with a real match from the room when there is one, otherwise a practice match against bots on this device
    /// (also what runs when the scene is opened alone in the editor).
    /// </summary>
    public sealed class MatchSceneBinder : MonoBehaviour
    {
        [SerializeField] private int practicePlayers = 10;
        [SerializeField] private string practiceName = "أبو سالم";

        private LocalMatchSession practice;
        private bool rewardShown;       // the loot briefcase of this match's coins was already shown

        private static void GoToRoom(ISceneLoader scenes)
        {
            if (scenes != null) scenes.LoadScene(AppScenes.Rooms);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(AppScenes.Rooms);
        }

        private void Start()
        {
            var view = GetComponent<KitMatchView>();
            if (view == null) { enabled = false; return; }
            var flow = gameObject.AddComponent<MatchScreenFlow>();
            ServiceLocator.TryGet(out ISceneLoader scenes);
            flow.LeaveRequested += () => GoToRoom(scenes);
            var runner = MatchRunner.Instance;
            if (runner != null && runner.Session != null)
            {
                flow.Attach(view, runner.Session.Client);
                flow.SetReward(runner.Reward);
                runner.RewardChanged += () =>
                {
                    if (flow == null) return;
                    flow.SetReward(runner.Reward);
                    // the server paid: the coins of the night come out of the loot briefcase (once per match)
                    var r = runner.Reward;
                    if (r != null && r.Status == RewardStatus.Granted && r.Coins > 0 && !rewardShown)
                    {
                        rewardShown = true;
                        RewardFlow.Show(r.Won ? "فزت بالسهرة" : "انتهت السهرة", "جائزة السهرة", RewardFlow.FromPack(r.Coins, 0, null));
                    }
                    else if (r != null && r.Status != RewardStatus.Granted) rewardShown = false;
                };
                // another match with the same room: the host starts it, the others wait in the room
                flow.PlayAgainRequested += () =>
                {
                    var s = runner.Session;
                    if (s == null || !s.StartMatch()) GoToRoom(scenes);
                };
                return;
            }
            flow.PlayAgainRequested += () => { practice = new LocalMatchSession(practiceName, practicePlayers); flow.Attach(view, practice.Me, () => practice.Tick(Time.unscaledDeltaTime)); practice.Begin(); };
            practice = new LocalMatchSession(practiceName, practicePlayers);
            flow.Attach(view, practice.Me, () => practice.Tick(Time.unscaledDeltaTime));
            if (!practice.Begin()) Log.Warning("practice match could not start", "Match");
        }
    }
}
