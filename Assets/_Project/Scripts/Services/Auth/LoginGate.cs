using System;
using System.Collections;
using UnityEngine;

namespace MafiaNight.Services.Auth
{
    /// <summary>
    /// On the very first start of the game the player chooses how to come in (guest, office account, later Google / Facebook) BEFORE the game signs in,
    /// so no empty guest account is made for someone who then signs in another way. While <see cref="Hold"/> is on, the EOS login waits; the app
    /// (<c>LoginGateFlow</c>) sets <see cref="Screen"/> to show the choice screen over the loading screen and calls <see cref="Release"/> when the player is ready.
    /// Players who already played (the launch banner was seen) or already chose are never held.
    /// </summary>
    public static class LoginGate
    {
        public const string ChosenKey = "mafia.login.chosen", SeenKey = "mafia.launch.seen";

        /// <summary>The sign-in must wait for the player's choice.</summary>
        public static bool Hold { get; private set; }

        /// <summary>Shows the choice screen and finishes when the player is ready to go on. Set by the app layer; the argument is a host for coroutines.</summary>
        public static Func<MonoBehaviour, IEnumerator> Screen;

        /// <summary>Raised once when the hold is lifted.</summary>
        public static event Action Released;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Hold = PlayerPrefs.GetInt(ChosenKey, 0) != 1 && !PlayerPrefs.HasKey(SeenKey);
            Released = null;
        }

        /// <summary>The player chose (or there is nobody to ask): the sign-in can start.</summary>
        public static void Release()
        {
            if (!Hold) return;
            Hold = false;
            Released?.Invoke();
        }
    }
}
