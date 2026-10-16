using MafiaNight.Core;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MafiaNight.Editor
{
    /// <summary>
    /// Pressing Play in any scene starts from the Bootstrap scene (first scene in Build Settings),
    /// so services and login always exist. After Bootstrap finishes, the game returns to the scene you had open.
    /// Toggle: menu "Mafia/Always Start From Bootstrap".
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromBootstrap
    {
        private const string MenuPath = "Mafia/Always Start From Bootstrap";
        private const string EnabledPrefKey = "MafiaNight.PlayFromBootstrap";

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledPrefKey, true);
            set => EditorPrefs.SetBool(EnabledPrefKey, value);
        }

        static PlayFromBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += ApplyStartScene;
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            ApplyStartScene();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;

            ApplyStartScene();
            string activeScene = EditorSceneManager.GetActiveScene().name;
            SessionState.SetString(GameConstants.EDITOR_RETURN_SCENE_KEY, Enabled ? activeScene : "");
        }

        private static void ApplyStartScene()
        {
            if (!Enabled || EditorBuildSettings.scenes.Length == 0)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            string bootstrapPath = EditorBuildSettings.scenes[0].path;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(bootstrapPath);
        }
    }
}
