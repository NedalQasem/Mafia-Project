using TMPro;
using UnityEngine;

namespace MafiaNight.App
{
    /// <summary>
    /// A tiny "v1.1.0 dev" label in the corner of every screen of a development build, so a tester always knows which build is on the phone
    /// (Docs/CHANGELOG.md says what each version contains). Release builds do not show it. The player-facing version line is in the settings screen.
    /// </summary>
    public static class VersionTag
    {
        private static GameObject tag;

        public static void Ensure()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            if (tag != null) return;
            tag = new GameObject("VersionTag", typeof(RectTransform), typeof(Canvas));
            Object.DontDestroyOnLoad(tag);
            var canvas = tag.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;

            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(tag.transform, false);
            var t = text.GetComponent<TextMeshProUGUI>();
            t.text = "v" + Application.version + (Debug.isDebugBuild ? " dev" : "");
            t.fontSize = Mathf.Max(14f, Screen.height * 0.012f);
            t.color = new Color(1f, 1f, 1f, 0.45f);
            t.alignment = TextAlignmentOptions.BottomLeft;
            t.raycastTarget = false;
            var r = t.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(0.5f, 0f); r.pivot = new Vector2(0f, 0f);
            r.anchoredPosition = new Vector2(Screen.width * 0.012f, Screen.height * 0.004f); r.sizeDelta = new Vector2(0f, t.fontSize * 1.8f);
        }
    }
}
