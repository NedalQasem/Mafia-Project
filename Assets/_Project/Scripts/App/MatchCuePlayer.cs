using System.Collections;
using MafiaNight.MatchRules;
using MafiaNight.MatchRules.Net;
using MafiaNight.UI.Kit;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// Plays what the <see cref="CueDirector"/> chooses: the narrator's line on a slip of paper, the voice-over clip when the project has one, and the named sound effects when it has them.
    /// Clips are looked up in Resources as <c>Narrator/&lt;language&gt;/&lt;m or f&gt;/&lt;file&gt;</c> (see <see cref="CueCatalog.ClipPath"/>, for example Narrator/ar/m/dawn_kill) and <c>Narrator/sfx/&lt;name&gt;</c>; a clip that is not there is skipped
    /// and the slip still tells the line, so the narration works with no audio at all. The table animations of the cues are not played here yet (the screens draw their own).
    /// </summary>
    public sealed class MatchCuePlayer : MonoBehaviour
    {
        /// <summary>PlayerPrefs key of the narrator's volume (0..1, default 1).</summary>
        public const string VolumeKey = "mafia.narrator";
        /// <summary>PlayerPrefs key of the narrator's voice: "m" (a man, the default) or "f" (a woman).</summary>
        public const string VoiceKey = "mafia.narrator.voice";
        private const string LangKey = "mafia.lang";

        private CueDirector director;
        private KitNarratorSlip slip;
        private KitMomentFx fx;
        private AudioSource voice;

        public void Attach(ClientMatchController client, KitNarratorSlip slipView, KitMomentFx momentFx = null)
        {
            if (director != null) director.Dispose();
            slip = slipView; fx = momentFx;
            if (voice == null) { voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false; voice.spatialBlend = 0f; }
            director = new CueDirector(client) { Language = Language() };      // the slip tells the line in the language the player chose
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
                if (step.Kind == CueStepKind.Subtitle || (step.Kind == CueStepKind.Animation && fx == null)) continue;
                if (step.At > elapsed) { yield return new WaitForSecondsRealtime(step.At - elapsed); elapsed = step.At; }
                if (step.Kind == CueStepKind.Animation) fx.Play(step.Value, play.SeatId, play.RoleId, Mathf.Max(0.5f, play.Cue.Duration - step.At));      // the table's own animations (close-up, shot, shield, stamp)
                else Clip(step);
            }
        }

        private void Clip(CueStep step)
        {
            AudioClip clip;
            if (step.Kind == CueStepKind.Sound) clip = Resources.Load<AudioClip>("Narrator/sfx/" + step.Value);
            else
            {
                string id = step.Value.Substring(step.Value.IndexOf('/') + 1);
                string mine = PlayerPrefs.GetString(VoiceKey, "m") == "f" ? "f" : "m", other = mine == "f" ? "m" : "f";
                // his voice in his language first, then the other voice, then the Arabic narrator: a missing clip never silences the line, and the slip still tells it
                clip = Resources.Load<AudioClip>(CueCatalog.ClipPath(Language(), mine, id)) ?? Resources.Load<AudioClip>(CueCatalog.ClipPath(Language(), other, id))
                    ?? Resources.Load<AudioClip>(CueCatalog.ClipPath("ar", mine, id)) ?? Resources.Load<AudioClip>(CueCatalog.ClipPath("ar", other, id));
            }
            if (clip == null || voice == null) return;
            voice.PlayOneShot(clip, Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f)));
        }

        private static string Language() => PlayerPrefs.GetString(LangKey, "ar") == "en" ? "en" : "ar";
    }
}
