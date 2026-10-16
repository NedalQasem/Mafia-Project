using System.Collections.Generic;
using MafiaNight.Core;
using MafiaNight.Data.Player;
using MafiaNight.Gameplay.Match;
using MafiaNight.Gameplay.Seating;
using MafiaNight.Services.Profile;
using MafiaNight.Services.Scenes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MafiaNight.DebugTools
{
    /// <summary>
    /// Debug panel for the profile / match / seating systems (OnGUI window + F1-F4 shortcuts).
    /// Requires the services from the Bootstrap scene (Play always starts there in the Editor).
    /// </summary>
    public class PlayerProfileTester : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private bool testOnStart = false;
        [SerializeField] private Key loadProfileKey = Key.F1;
        [SerializeField] private Key saveProfileKey = Key.F2;
        [SerializeField] private Key addXPKey = Key.F3;
        [SerializeField] private Key winGameKey = Key.F4;

        [Header("Test Values")]
        [SerializeField] private int xpToAdd = 150;
        [SerializeField] private bool testAsMafia = false;
        [SerializeField] private bool testAsMVP = false;

        [Header("GUI")]
        [SerializeField] private bool showGUI = true;

        [Header("Seat Testing")]
        [SerializeField] private int debugSeatCount = 6;

        private Rect windowRect = new Rect(20, 20, 250, 450);
        private readonly List<PlayerData> _debugSeatPlayers = new List<PlayerData>();

        private static IPlayerProfileService Profile => ServiceLocator.TryGet<IPlayerProfileService>(out var s) ? s : null;
        private static GameManager Game => ServiceLocator.TryGet<GameManager>(out var s) ? s : null;
        private static SeatManager Seats => ServiceLocator.TryGet<SeatManager>(out var s) ? s : null;

        private void Start()
        {
            if (Profile == null)
            {
                Debug.LogWarning("⚠️ [TEST] Services not found - start Play from the Bootstrap scene.");
            }

            if (testOnStart) TestLoadProfile();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[loadProfileKey].wasPressedThisFrame) TestLoadProfile();
            if (keyboard[saveProfileKey].wasPressedThisFrame) TestSaveProfile();
            if (keyboard[addXPKey].wasPressedThisFrame) TestAddXP();
            if (keyboard[winGameKey].wasPressedThisFrame) TestWinGame();
        }

        private void OnGUI()
        {
            if (!showGUI) return;
            windowRect = GUILayout.Window(0, windowRect, DrawTesterWindow, "Profile Tester");
        }

        private void DrawTesterWindow(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("<b>Scene Navigation</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Main Menu")) SafeLoadScene(GameConstants.SCENE_MAIN_MENU);
            if (GUILayout.Button("Lobby")) SafeLoadScene(GameConstants.SCENE_LOBBY);
            if (GUILayout.Button("Gameplay")) SafeLoadScene(GameConstants.SCENE_GAMEPLAY);
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.Label("<b>Game Flow</b>");
            if (GUILayout.Button("Fill Lobby (Add 5 Bots)")) TestFillLobby();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Game")) Game?.StartGame();
            if (GUILayout.Button("End Game (Force Result)")) Game?.ChangeGameState(GameState.GameOver);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("<b>Profile Actions</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save")) TestSaveProfile();
            if (GUILayout.Button("Load")) TestLoadProfile();
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.Label("<b>Progression</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"+{xpToAdd} XP")) TestAddXP();
            if (GUILayout.Button("Win")) TestWinGame();
            if (GUILayout.Button("Lose")) TestLoseGame();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("<b>Current Stats</b>");

            var profile = Profile?.LocalProfile;
            if (profile != null)
            {
                GUILayout.Label($"Name: {profile.DisplayName}");
                GUILayout.Label($"Level: {profile.Level} (XP: {profile.CurrentXP}/{profile.XPToNextLevel})");
                GUILayout.Label($"<color=yellow>Rank: {profile.Rank}</color> ({profile.RankPoints})");
            }
            else
            {
                GUILayout.Label("<color=red>No Profile Loaded</color>");
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private static void SafeLoadScene(string sceneName)
        {
            if (ServiceLocator.TryGet<ISceneLoader>(out var loader)) loader.LoadScene(sceneName);
            else SceneManager.LoadScene(sceneName);
        }

        private static void TestFillLobby()
        {
            var game = Game;
            if (game == null) return;

            for (int i = 0; i < 5; i++)
            {
                var visuals = new PlayerPublicData
                {
                    DisplayName = $"Bot {i + 1}",
                    IsVIP = i % 2 == 0,
                    ProfilePictureID = "default",
                    ProfileFrameID = "default"
                };
                game.RegisterPlayer(new PlayerData($"Bot {i + 1}", $"BOT_ID_{i}", $"BOT_EOS_{i}", visuals));
            }

            Debug.Log("✅ [TEST] Added 5 bots to GameManager");
        }

        [ContextMenu("Test Load Profile")]
        public void TestLoadProfile()
        {
            var profile = Profile;
            if (profile == null) return;

            if (profile.HasProfile)
            {
                LogProfile(profile.LocalProfile);
                return;
            }

            void OnLoaded(PlayerProfile p)
            {
                profile.OnProfileLoaded -= OnLoaded;
                LogProfile(p);
            }
            profile.OnProfileLoaded += OnLoaded;
            profile.RefreshProfile();
        }

        private static void LogProfile(PlayerProfile profile)
        {
            Debug.Log($"✅ [TEST] Profile: {profile.DisplayName}, Lvl {profile.Level}, {profile.Rank}");
        }

        [ContextMenu("Test Save Profile")]
        public void TestSaveProfile() => Profile?.SaveProfile();

        [ContextMenu("Test Add XP")]
        public void TestAddXP() => Profile?.AddXP(xpToAdd);

        [ContextMenu("Test Win Game")]
        public void TestWinGame() => RecordTestMatch(won: true);

        [ContextMenu("Test Lose Game")]
        public void TestLoseGame() => RecordTestMatch(won: false);

        private void RecordTestMatch(bool won)
        {
            var profile = Profile;
            if (profile == null || !profile.HasProfile)
            {
                Debug.LogError("[TEST] Profile not loaded!");
                return;
            }

            profile.RecordMatchResult(won, testAsMafia, won && testAsMVP);
            var p = profile.LocalProfile;
            Debug.Log($"{(won ? "✅ Win" : "❌ Lose")} [TEST]: {p.Rank} ({p.RankPoints} pts)");
        }

        #region Seat / Layout Testing

        [ContextMenu("Test: Generate Layout")]
        public void TestGenerateLayout()
        {
            var seats = Seats;
            if (seats == null)
            {
                Debug.LogError("SeatManager not found!");
                return;
            }

            _debugSeatPlayers.Clear();
            for (int i = 0; i < debugSeatCount; i++)
            {
                var visuals = new PlayerPublicData { DisplayName = $"Test Player {i + 1}", IsVIP = i % 3 == 0 };
                _debugSeatPlayers.Add(new PlayerData($"Test Player {i + 1}", $"ID_{i}", $"EOS_{i}", visuals));
            }

            seats.SetupSeats(_debugSeatPlayers);
        }

        [ContextMenu("Test: Add Player to Seat")]
        public void TestAddSeatPlayer()
        {
            var seats = Seats;
            if (seats == null) return;

            int nextId = _debugSeatPlayers.Count + 1;
            var visuals = new PlayerPublicData { DisplayName = $"New Player {nextId}" };
            _debugSeatPlayers.Add(new PlayerData($"New Player {nextId}", $"ID_{nextId}", $"EOS_{nextId}", visuals));
            seats.SetupSeats(_debugSeatPlayers);
        }

        [ContextMenu("Test: Remove Player from Seat")]
        public void TestRemoveSeatPlayer()
        {
            var seats = Seats;
            if (seats == null || _debugSeatPlayers.Count == 0) return;

            _debugSeatPlayers.RemoveAt(_debugSeatPlayers.Count - 1);
            seats.SetupSeats(_debugSeatPlayers);
        }

        #endregion
    }
}
