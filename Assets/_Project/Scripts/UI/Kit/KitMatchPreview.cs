using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>Shows one frame of the match screens at a time (the frames are built from Docs/mockups/match.html by the editor builder).</summary>
    public class KitMatchPreview : MonoBehaviour
    {
        [SerializeField] private GameObject[] frames;
        [SerializeField] private string[] names;
        [SerializeField] private int index;

        public int Count => frames != null ? frames.Length : 0;
        public string[] Names => names;

        private void Start() { Show(index); }

        public void Show(int i)
        {
            if (frames == null || frames.Length == 0) return;
            index = (i % frames.Length + frames.Length) % frames.Length;
            for (int k = 0; k < frames.Length; k++) if (frames[k] != null) frames[k].SetActive(k == index);
        }

        public bool Show(string frameName)
        {
            for (int k = 0; k < names.Length; k++) if (names[k] == frameName) { Show(k); return true; }
            return false;
        }

        public void Next() { Show(index + 1); }
        public void Prev() { Show(index - 1); }
    }
}
