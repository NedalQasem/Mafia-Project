using System.Collections;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Plays what the <see cref="CueDirector"/> chooses: the narrator's line on a slip of paper, the voice-over clip when the project has one, and the named sound effects when it has them.
    /// Clips are looked up in Resources as <c>Narrator/&lt;language&gt;/&lt;cue id&gt;</c> (for example Narrator/ar/dawn.kill) and <c>Narrator/sfx/&lt;name&gt;</c>; a clip that is not there is skipped
    /// and the slip still tells the line, so the narration works with no audio at all. The table animations of the cues are not played here yet (the screens draw their own).
    /// </summary>
    public sealed class MatchCuePlayer : MonoBehaviour
    {
        /// <summary>PlayerPrefs key of the narrator's volume (0..1, default 1).</summary>
        public const string VolumeKey = "mafia.narrator";
        private const string LangKey = "mafia.lang";

        private CueDirector director;
        private KitNarratorSlip slip;
        private AudioSource voice;

        public void Attach(ClientMatchController client, KitNarratorSlip slipView)
        {
            if (director != null) director.Dispose();
            slip = slipView;
            if (voice == null) { voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false; voice.spatialBlend = 0f; }
            director = new CueDirector(client);
            director.Play += OnPlay;
        }

        private void OnDestroy() { if (director != null) director.Dispose(); }

        private void OnPlay(CuePlay play) { StartCoroutine(Run(play)); }

        private IEnumerator Run(CuePlay play)
        {
            if (play.Delay > 0f) yield return new WaitForSecondsRealtime(play.Delay);
            if (slip != null && !string.IsNullOrEmpty(play.Subtitle)) slip.Show(play.Subtitle, Mathf.Min(play.Cue.Duration, 6f));
            float elapsed = 0f;
            foreach (var step in play.Cue.Steps)
            {
                if (step.Kind != CueStepKind.Voice && step.Kind != CueStepKind.Sound) continue;
                if (step.At > elapsed) { yield return new WaitForSecondsRealtime(step.At - elapsed); elapsed = step.At; }
                Clip(step);
            }
        }

        private void Clip(CueStep step)
        {
            string path = step.Kind == CueStepKind.Sound ? "Narrator/sfx/" + step.Value : "Narrator/" + Language() + "/" + step.Value.Substring(step.Value.IndexOf('/') + 1);
            var clip = Resources.Load<AudioClip>(path);
            if (clip == null && step.Kind == CueStepKind.Voice) clip = Resources.Load<AudioClip>("Narrator/" + step.Value);            // the Arabic narrator when the chosen language has no clip
            if (clip == null || voice == null) return;
            voice.PlayOneShot(clip, Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f)));
        }

        private static string Language() => PlayerPrefs.GetString(LangKey, "ar") == "en" ? "en" : "ar";
    }
}
