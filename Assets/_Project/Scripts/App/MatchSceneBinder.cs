using MafiaNight.Core;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using MafiaNight.Services.Match;
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

        private void Start()
        {
            var view = GetComponent<KitMatchView>();
            if (view == null) { enabled = false; return; }
            var flow = gameObject.AddComponent<MatchScreenFlow>();
            var runner = MatchRunner.Instance;
            if (runner != null && runner.Session != null)
            {
                flow.Attach(view, runner.Session.Client);
                return;
            }
            practice = new LocalMatchSession(practiceName, practicePlayers);
            flow.Attach(view, practice.Me, () => practice.Tick(Time.unscaledDeltaTime));
            if (!practice.Begin()) Log.Warning("practice match could not start", "Match");
        }
    }
}
