using UnityEngine;
using MafiaNight.Player;
using MafiaNight.Managers;
using MafiaNight.Systems;
using MafiaNight.Economy;
using MafiaNight.Gameplay;
using MafiaNight.Core;
using MafiaNight.Data.Customization;
using System.Collections.Generic;

namespace MafiaNight.Testing
{
    /// <summary>
    /// Test script for PlayerProfile system - With GUI Support
    /// Attach to any GameObject in scene for testing
    /// </summary>
    public class PlayerProfileTester : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private bool testOnStart = false;
        [SerializeField] private KeyCode testLoadKey = KeyCode.F1;
        [SerializeField] private KeyCode testSaveKey = KeyCode.F2;
        [SerializeField] private KeyCode testAddXPKey = KeyCode.F3;
        [SerializeField] private KeyCode testWinGameKey = KeyCode.F4;
        
        [Header("Test Values")]
        [SerializeField] private int xpToAdd = 150;
        [SerializeField] private int rankPointsToAdd = 100; // New for Rank Test
        [SerializeField] private bool testAsMafia = false;
        [SerializeField] private bool testAsMVP = false;
        
        [Header("GUI")]
        [SerializeField] private bool showGUI = true;
        
        // Window Rect for dragging
        private Rect windowRect = new Rect(20, 20, 250, 450);
        
        private void Start()
        {
            // Auto-create manager if missing (For testing in MainMenu without Bootstrap)
            if (PlayerProfileManager.Instance == null)
            {
                Debug.LogWarning("⚠️ [TEST] PlayerProfileManager not found! Creating temporary instance for testing...");
                var go = new GameObject("PlayerProfileManager_TEMP");
                go.AddComponent<PlayerProfileManager>();
                
                // Also need CurrencyManager probably
                if (CurrencyManager.Instance == null)
                {
                    var cmGo = new GameObject("CurrencyManager_TEMP");
                    cmGo.AddComponent<CurrencyManager>();
                }
                
                // Need PlayerRankManager for Rank tests
                if (PlayerRankManager.Instance == null)
                {
                    var prGo = new GameObject("PlayerRankManager_TEMP");
                    prGo.AddComponent<PlayerRankManager>();
                }
            }
            
            if (testOnStart)
            {
                TestLoadProfile();
            }
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(testLoadKey)) TestLoadProfile();
            if (Input.GetKeyDown(testSaveKey)) TestSaveProfile();
            if (Input.GetKeyDown(testAddXPKey)) TestAddXP();
            if (Input.GetKeyDown(testWinGameKey)) TestWinGame();
        }
        
        private void OnGUI()
        {
            if (!showGUI) return;
            
            // Draggable Window
            windowRect = GUILayout.Window(0, windowRect, DrawTesterWindow, "Profile Tester");
        }
        
        [Header("Scene Testing")]
        [SerializeField] private string gameplaySceneName = "Gameplay";

        private void DrawTesterWindow(int windowID)
        {
            GUILayout.BeginVertical();
            
            // --- SCENES ---
            GUILayout.Label("<b>Scene Navigation</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Main Menu")) SafeLoadScene(GameConstants.SCENE_MAIN_MENU);
            if (GUILayout.Button("Lobby")) SafeLoadScene(GameConstants.SCENE_LOBBY);
            if (GUILayout.Button("Gameplay")) SafeLoadScene(gameplaySceneName);
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // --- GAME FLOW (Lobby/Game) ---
            GUILayout.Label("<b>Game Flow</b>");
            if (GUILayout.Button("Fill Lobby (Add 5 Players)")) TestFillLobby();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Game")) 
            {
                if (GameManager.Instance != null) GameManager.Instance.StartGame();
            }
            if (GUILayout.Button("End Game (Force Result)")) 
            {
                if (GameManager.Instance != null) GameManager.Instance.ChangeGameState(GameState.GameOver);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            
            // --- PROFILE ACTIONS ---
            GUILayout.Label("<b>Profile Actions</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save")) TestSaveProfile();
            if (GUILayout.Button("Load")) TestLoadProfile();
            GUILayout.EndHorizontal();
            
            // --- PROGRESSION ---
            GUILayout.Space(5);
            GUILayout.Label("<b>Progression</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"+{xpToAdd} XP")) TestAddXP();
            if (GUILayout.Button("Win")) TestWinGame();
            if (GUILayout.Button("Lose")) TestLoseGame();
            GUILayout.EndHorizontal();

            // --- STATS DISPLAY ---
            GUILayout.Space(10);
            GUILayout.Label("<b>Current Stats</b>");
            
            if (PlayerProfileManager.Instance != null && PlayerProfileManager.Instance.LocalProfile != null)
            {
                var p = PlayerProfileManager.Instance.LocalProfile;
                GUILayout.Label($"Name: {p.DisplayName}");
                GUILayout.Label($"Level: {p.Level} (XP: {p.CurrentXP}/{p.XPToNextLevel})");
                GUILayout.Label($"<color=yellow>Rank: {p.Rank}</color> ({p.RankPoints})");
            }
            else
            {
                GUILayout.Label("<color=red>No Profile Loaded</color>");
            }
            
            GUILayout.EndVertical();
            
            // Make window draggable
            GUI.DragWindow();
        }

        private void SafeLoadScene(string sceneName)
        {
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadScene(sceneName);
            }
            else
            {
                // Fallback if Manager missing
                Debug.LogWarning("SceneTransitionManager not found, using direct SceneManager");
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            }
        }

        private void TestFillLobby()
        {
            if (GameManager.Instance == null) return;
            
            // Add 1 local + 5 dummies = 6 players
            // Ensure Local Player is registered first
            var localProfile = PlayerProfileManager.Instance.LocalProfile;
            if (localProfile != null)
            {
                // Verify local player is in GameManager
                // (Usually MainMenu logic does this, but for testing we assume it might not be)
            }

            // Add Dummies
            for (int i = 0; i < 5; i++)
            {
                var dummyVis = new PlayerPublicData 
                { 
                    DisplayName = $"Bot {i+1}", 
                    IsVIP = (i % 2 == 0),
                    ProfilePictureID = "default",
                    ProfileFrameID = "default"
                };
                
                var dummy = new PlayerData($"Bot {i+1}", $"BOT_ID_{i}", $"BOT_EOS_{i}", dummyVis);
                
                GameManager.Instance.RegisterPlayer(dummy);
            }
            
            Debug.Log("✅ [TEST] Added 5 Bots to Game Manager");
        }
        
        [ContextMenu("Test Load Profile")]
        public void TestLoadProfile()
        {
            var manager = PlayerProfileManager.Instance;
            if (manager == null) return;
            
            if (manager.LocalProfile != null) DisplayProfile(manager.LocalProfile);
            else
            {
                manager.OnProfileLoaded += DisplayProfileOnce;
                manager.RefreshProfile();
            }
        }

        private void DisplayProfileOnce(PlayerProfile profile)
        {
            PlayerProfileManager.Instance.OnProfileLoaded -= DisplayProfileOnce;
            DisplayProfile(profile);
        }

        private void DisplayProfile(PlayerProfile profile)
        {
            Debug.Log($"✅ [TEST] Profile Loaded: {profile.DisplayName}, Lvl {profile.Level}, {profile.Rank}");
        }
        
        [ContextMenu("Test Save Profile")]
        public void TestSaveProfile()
        {
            if (PlayerProfileManager.Instance != null) { PlayerProfileManager.Instance.SaveProfile(); Debug.Log("✅ Saved"); }
        }
        
        [ContextMenu("Test Add XP")]
        public void TestAddXP()
        {
            if (PlayerProfileManager.Instance != null) { PlayerProfileManager.Instance.AddXP(xpToAdd); }
        }
        
        [ContextMenu("Test Win Game")]
        public void TestWinGame()
        {
            if (CheckManagers())
            {
                var profile = PlayerProfileManager.Instance.LocalProfile;
                PlayerRankManager.Instance.AddRankPoints(profile, true, testAsMVP);
                PlayerProfileManager.Instance.RecordGameResult(true, testAsMafia);
                Debug.Log($"✅ [TEST] Win Game: {profile.Rank} ({profile.RankPoints} pts)");
            }
        }
        
        [ContextMenu("Test Lose Game")]
        public void TestLoseGame()
        {
            if (CheckManagers())
            {
                var profile = PlayerProfileManager.Instance.LocalProfile;
                PlayerRankManager.Instance.AddRankPoints(profile, false, false);
                PlayerProfileManager.Instance.RecordGameResult(false, testAsMafia);
                Debug.Log($"❌ [TEST] Lose Game: {profile.Rank} ({profile.RankPoints} pts)");
            }
        }
        
        public void TestChangeRankPoints(int amount)
        {
            if (CheckManagers())
            {
                var profile = PlayerProfileManager.Instance.LocalProfile;
                profile.RankPoints += amount;
                if (profile.RankPoints < 0) profile.RankPoints = 0;
                
                // Update Rank Enum based on new points
                profile.Rank = PlayerRankManager.Instance.GetRankFromPoints(profile.RankPoints);
                
                Debug.Log($"⚖️ [TEST] Manual Adjust: {amount} pts. New: {profile.Rank} ({profile.RankPoints})");
                PlayerProfileManager.Instance.SaveProfile(); // Save changes
            }
        }
        
        private bool CheckManagers()
        {
            if (PlayerProfileManager.Instance == null || PlayerRankManager.Instance == null)
            {
                Debug.LogError("Managers missing!");
                return false;
            }
            if (PlayerProfileManager.Instance.LocalProfile == null)
            {
                Debug.LogError("Profile not loaded!");
                return false;
            }
            return true;
        }

        #region Seat / Layou Testing

        [Header("Seat Testing")]
        [SerializeField] private int debugSeatCount = 6;
        private List<PlayerData> _debugSeatPlayers = new List<PlayerData>();

        [ContextMenu("Test: Generate Layout")]
        public void TestGenerateLayout()
        {
            if (SeatManager.Instance == null)
            {
                Debug.LogError("SeatManager not found!");
                return;
            }

            _debugSeatPlayers.Clear();
            for (int i = 0; i < debugSeatCount; i++)
            {
                var dummyVis = new PlayerPublicData 
                { 
                    DisplayName = $"Test Player {i+1}", 
                    IsVIP = (i % 3 == 0) 
                };
                
                var dummy = new PlayerData($"Test Player {i+1}", $"ID_{i}", $"EOS_{i}", dummyVis);
                _debugSeatPlayers.Add(dummy);
            }
            
            SeatManager.Instance.SetupSeats(_debugSeatPlayers);
            Debug.Log($"Generated Layout for {debugSeatCount} players");
        }

        [ContextMenu("Test: Add Player to Seat")]
        public void TestAddSeatPlayer()
        {
            if (SeatManager.Instance == null) return;

            int nextId = _debugSeatPlayers.Count + 1;
            var dummyVis = new PlayerPublicData { DisplayName = $"New Player {nextId}" };
            var dummy = new PlayerData($"New Player {nextId}", $"ID_{nextId}", $"EOS_{nextId}", dummyVis);
            
            _debugSeatPlayers.Add(dummy);
            SeatManager.Instance.SetupSeats(_debugSeatPlayers);
        }

        [ContextMenu("Test: Remove Player from Seat")]
        public void TestRemoveSeatPlayer()
        {
            if (SeatManager.Instance == null) return;

            if (_debugSeatPlayers.Count > 0)
            {
                _debugSeatPlayers.RemoveAt(_debugSeatPlayers.Count - 1);
                SeatManager.Instance.SetupSeats(_debugSeatPlayers);
            }
        }

        #endregion
    }
}
